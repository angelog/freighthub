using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Domain.Carriers;
using Api.Domain.Integrations;
using Api.Infrastructure.Integrations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.Carriers;

/// <summary>
/// Client real da Loggi (Quotation API v1). Autentica via OAuth2 (v2/oauth2/token),
/// cacheia o token e cota preço/prazo em POST /v1/companies/{companyId}/quotations.
/// O HttpClient já vem com o pipeline de resiliência (ver <see cref="CarrierResilience"/>).
/// Credenciais (ClientId/ClientSecret/CompanyId/ExternalServiceId) vêm do store cifrado.
/// </summary>
public sealed class LoggiCarrierProvider(
    HttpClient httpClient,
    IIntegrationCredentialStore credentials,
    IOptions<CarrierOptions> options,
    IMemoryCache cache,
    ILogger<LoggiCarrierProvider> logger) : ICarrierProvider
{
    private readonly CarrierOptions _options = options.Value;

    public CarrierType Carrier => CarrierType.Loggi;

    public async Task<IReadOnlyList<CarrierQuote>> GetQuotesAsync(
        CarrierQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var creds = await credentials.GetAllAsync(
            IntegrationType.Loggi, _options.Environment, cancellationToken);

        if (!creds.TryGetValue("ClientId", out var clientId) ||
            !creds.TryGetValue("ClientSecret", out var clientSecret) ||
            !creds.TryGetValue("CompanyId", out var companyId))
        {
            logger.LogWarning(
                "Credenciais da Loggi (ClientId/ClientSecret/CompanyId) ausentes para {Environment}; sem cotação.",
                _options.Environment);
            return [];
        }

        creds.TryGetValue("ExternalServiceId", out var externalServiceId);

        var token = await GetAccessTokenAsync(clientId, clientSecret, cancellationToken);
        var body = BuildQuoteBody(request, externalServiceId);

        using var quoteRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/v1/companies/{companyId}/quotations")
        {
            Content = JsonContent.Create(body)
        };
        quoteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(quoteRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Loggi retornou {Status} na cotação: {Body}", (int)response.StatusCode, error);
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseQuotes(doc.RootElement);
    }

    private async Task<string> GetAccessTokenAsync(
        string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        var cacheKey = $"loggi:token:{clientId}";
        if (cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
            return cached;

        using var response = await httpClient.PostAsJsonAsync(
            "/v2/oauth2/token",
            new { client_id = clientId, client_secret = clientSecret },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        var token = root.GetProperty("idToken").GetString()
            ?? throw new InvalidOperationException("Resposta de auth da Loggi sem idToken.");

        // Cacheia com folga de 5 min antes da expiração real (default 1h se não informado).
        var ttl = TimeSpan.FromSeconds(3300);
        if (root.TryGetProperty("expiresIn", out var exp)
            && double.TryParse(exp.GetString(), out var seconds) && seconds > 600)
        {
            ttl = TimeSpan.FromSeconds(seconds - 300);
        }

        cache.Set(cacheKey, token, ttl);
        return token;
    }

    private object BuildQuoteBody(CarrierQuoteRequest request, string? externalServiceId)
    {
        var (units, nanos) = ToUnitsNanos(request.DeclaredValue);
        var package = new
        {
            weightG = Math.Max(1, (int)Math.Round(request.TotalWeightKg * 1000m)),
            lengthCm = (int)Math.Round(request.LengthCm ?? 20m),
            widthCm = (int)Math.Round(request.WidthCm ?? 15m),
            heightCm = (int)Math.Round(request.HeightCm ?? 10m),
            goodsValue = new { currencyCode = "BRL", units, nanos }
        };

        // A API exige apenas UM entre externalServiceIds e pickupTypes.
        if (!string.IsNullOrWhiteSpace(externalServiceId))
        {
            return new
            {
                shipFrom = new { correios = new { cep = request.OriginZipCode } },
                shipTo = new { correios = new { cep = request.DestinationZipCode } },
                packages = new[] { package },
                externalServiceIds = new[] { externalServiceId }
            };
        }

        return new
        {
            shipFrom = new { correios = new { cep = request.OriginZipCode } },
            shipTo = new { correios = new { cep = request.DestinationZipCode } },
            packages = new[] { package },
            pickupTypes = new[] { "PICKUP_TYPE_SPOT" }
        };
    }

    private static IReadOnlyList<CarrierQuote> ParseQuotes(JsonElement root)
    {
        var quotes = new List<CarrierQuote>();

        if (!root.TryGetProperty("packagesQuotations", out var packages))
            return quotes;

        foreach (var package in packages.EnumerateArray())
        {
            if (!package.TryGetProperty("quotations", out var quotations))
                continue;

            foreach (var q in quotations.EnumerateArray())
            {
                var price = ReadAmount(q.GetProperty("price").GetProperty("totalAmount"));
                var days = q.TryGetProperty("sloInDays", out var slo) ? slo.GetInt32() : 0;
                var name = q.TryGetProperty("freightTypeLabel", out var label)
                    ? label.GetString() ?? "Loggi"
                    : "Loggi";

                quotes.Add(new CarrierQuote
                {
                    Carrier = CarrierType.Loggi,
                    ServiceName = name,
                    Price = price,
                    EstimatedDays = days
                });
            }
        }

        return quotes;
    }

    /// <summary>Converte o par units/nanos da Loggi (protobuf Money) em decimal.</summary>
    private static decimal ReadAmount(JsonElement amount)
    {
        var units = amount.GetProperty("units");
        long unitsValue = units.ValueKind == JsonValueKind.String
            ? long.Parse(units.GetString()!)
            : units.GetInt64();

        var nanos = amount.TryGetProperty("nanos", out var n) ? n.GetInt32() : 0;
        return unitsValue + (nanos / 1_000_000_000m);
    }

    private static (long units, int nanos) ToUnitsNanos(decimal value)
    {
        var units = (long)decimal.Truncate(value);
        var nanos = (int)decimal.Round((value - units) * 1_000_000_000m, 0);
        return (units, nanos);
    }
}
