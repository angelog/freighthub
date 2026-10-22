using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Domain.Carriers;
using Api.Domain.Integrations;
using Api.Infrastructure.Integrations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.Carriers;

/// <summary>
/// Client real dos Correios (CWS). Autentica sozinho via
/// <c>POST /token/v1/autentica/cartaopostagem</c> (Basic login:códigoDeAcesso + cartão/contrato/DR),
/// obtendo um token curto (~24h, categoria Pl0) que carrega as autorizações do contrato
/// (Preço 38202 e Prazo 38210). O token é cacheado e renovado automaticamente — sem
/// chave estática de 180 dias. Para cada código de serviço, consulta Preço + Prazo.
/// </summary>
public sealed class CorreiosCarrierProvider(
    HttpClient httpClient,
    IIntegrationCredentialStore credentials,
    IOptions<CarrierOptions> options,
    IMemoryCache cache,
    ILogger<CorreiosCarrierProvider> logger) : ICarrierProvider
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    private static readonly Dictionary<string, string> ServiceNames = new()
    {
        ["03220"] = "SEDEX",
        ["03298"] = "PAC",
        ["04014"] = "SEDEX",
        ["04510"] = "PAC",
    };

    private readonly CarrierOptions _options = options.Value;

    public CarrierType Carrier => CarrierType.Correios;

    public async Task<IReadOnlyList<CarrierQuote>> GetQuotesAsync(
        CarrierQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var creds = await credentials.GetAllAsync(
            IntegrationType.Correios, _options.Environment, cancellationToken);

        if (!creds.TryGetValue("Login", out var login) ||
            !creds.TryGetValue("CodigoAcesso", out var codigoAcesso) ||
            !creds.TryGetValue("CartaoPostagem", out var cartao) ||
            !creds.TryGetValue("Contrato", out var contrato) ||
            !creds.TryGetValue("Dr", out var drRaw) ||
            !int.TryParse(drRaw, out var dr))
        {
            logger.LogWarning(
                "Credenciais dos Correios incompletas para {Environment} " +
                "(precisa de Login, CodigoAcesso, CartaoPostagem, Contrato, Dr); sem cotação.",
                _options.Environment);
            return [];
        }

        var codes = _options.Correios.ServiceCodes;
        if (codes.Count == 0)
        {
            logger.LogInformation("Nenhum código de serviço configurado (Carriers:Correios:ServiceCodes).");
            return [];
        }

        var token = await GetAccessTokenAsync(login, codigoAcesso, cartao, contrato, dr, cancellationToken);

        var tasks = codes.Select(code => QuoteServiceAsync(code, token, request, cancellationToken));
        var results = await Task.WhenAll(tasks);
        return results.OfType<CarrierQuote>().ToList();
    }

    private async Task<string> GetAccessTokenAsync(
        string login, string codigoAcesso, string cartao, string contrato, int dr,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"correios:token:{login}";
        if (cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
            return cached;

        using var req = new HttpRequestMessage(HttpMethod.Post, "/token/v1/autentica/cartaopostagem")
        {
            Content = JsonContent.Create(new { numero = cartao, contrato, dr }),
        };
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{login}:{codigoAcesso}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using var response = await httpClient.SendAsync(req, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        var token = root.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("Resposta de auth dos Correios sem token.");

        cache.Set(cacheKey, token, ResolveTtl(root));
        return token;
    }

    /// <summary>TTL do cache a partir de expiraEm/zoneOffset (com folga); fallback de 12h.</summary>
    private static TimeSpan ResolveTtl(JsonElement root)
    {
        if (root.TryGetProperty("expiraEm", out var exp) && exp.GetString() is { } expiraEm)
        {
            var offset = root.TryGetProperty("zoneOffset", out var z) ? z.GetString() : null;
            if (DateTimeOffset.TryParse($"{expiraEm}{offset}", out var expiresAt))
            {
                var ttl = expiresAt - DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);
                if (ttl > TimeSpan.FromMinutes(1))
                    return ttl;
            }
        }

        return TimeSpan.FromHours(12);
    }

    private async Task<CarrierQuote?> QuoteServiceAsync(
        string code, string token, CarrierQuoteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var priceTask = GetPriceAsync(code, token, request, cancellationToken);
            var daysTask = GetDeadlineDaysAsync(code, token, request, cancellationToken);
            await Task.WhenAll(priceTask, daysTask);

            var price = priceTask.Result;
            var days = daysTask.Result;
            if (price is null || days is null)
                return null;

            return new CarrierQuote
            {
                Carrier = CarrierType.Correios,
                ServiceName = ServiceNames.GetValueOrDefault(code, $"Correios {code}"),
                ExternalServiceCode = code,
                Price = price.Value,
                EstimatedDays = days.Value,
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Correios: falha ao cotar serviço {Code}.", code);
            return null;
        }
    }

    private async Task<decimal?> GetPriceAsync(
        string code, string token, CarrierQuoteRequest request, CancellationToken cancellationToken)
    {
        var weightG = Math.Max(1, (int) Math.Round(request.TotalWeightKg * 1000m));
        var url = $"/preco/v1/nacional/{code}"
            + $"?cepOrigem={request.OriginZipCode}&cepDestino={request.DestinationZipCode}"
            + $"&psObjeto={weightG}&tpObjeto=2"
            + $"&comprimento={(int) Math.Round(request.LengthCm ?? 20m)}"
            + $"&largura={(int) Math.Round(request.WidthCm ?? 15m)}"
            + $"&altura={(int) Math.Round(request.HeightCm ?? 10m)}";

        using var doc = await SendAsync(url, token, code, "Preço", cancellationToken);
        if (doc is null || !doc.RootElement.TryGetProperty("pcFinal", out var pcFinal))
            return null;

        return decimal.TryParse(pcFinal.GetString(), NumberStyles.Currency, PtBr, out var value) ? value : null;
    }

    private async Task<int?> GetDeadlineDaysAsync(
        string code, string token, CarrierQuoteRequest request, CancellationToken cancellationToken)
    {
        var url = $"/prazo/v1/nacional/{code}"
            + $"?cepOrigem={request.OriginZipCode}&cepDestino={request.DestinationZipCode}";

        using var doc = await SendAsync(url, token, code, "Prazo", cancellationToken);
        if (doc is null)
            return null;

        return doc.RootElement.TryGetProperty("prazoEntrega", out var prazo) ? prazo.GetInt32() : null;
    }

    private async Task<JsonDocument?> SendAsync(
        string url, string token, string code, string api, CancellationToken cancellationToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(req, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Correios {Api} {Code} retornou {Status}: {Body}", api, code, (int) response.StatusCode, body);
            return null;
        }

        return JsonDocument.Parse(body);
    }
}
