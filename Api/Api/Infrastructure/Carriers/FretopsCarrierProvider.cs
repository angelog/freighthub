using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.Domain.Carriers;
using Api.Domain.Integrations;
using Api.Infrastructure.Integrations;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.Carriers;

/// <summary>
/// Client da Fretops (Partner Quote API). Autentica via header estático
/// (X-Fretops-Api-Key) e cota em POST /partner/v1/quotes. O HttpClient já vem
/// com o pipeline de resiliência (ver <see cref="CarrierResilience"/>).
/// Credencial (ApiKey) vem do store cifrado.
///
/// Particularidade desta transportadora: cada opção devolvida traz um
/// <c>checkout_ref</c> que a Fretops usa para localizar a cotação quando o
/// pedido é pago (via webhook do app Fretops, fora deste fluxo). Esse valor
/// precisa ser preservado em <see cref="CarrierQuote.ExternalQuoteId"/> e
/// devolvido à Yampi sem alteração — não pode ser substituído por um id
/// gerado por nós.
/// </summary>
public sealed class FretopsCarrierProvider(
    HttpClient httpClient,
    IIntegrationCredentialStore credentials,
    IOptions<CarrierOptions> options,
    ILogger<FretopsCarrierProvider> logger) : ICarrierProvider
{
    private readonly CarrierOptions _options = options.Value;

    public CarrierType Carrier => CarrierType.Fretops;

    public async Task<IReadOnlyList<CarrierQuote>> GetQuotesAsync(
        CarrierQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var creds = await credentials.GetAllAsync(
            IntegrationType.Fretops, _options.Environment, cancellationToken);

        if (!creds.TryGetValue("ApiKey", out var apiKey))
        {
            logger.LogWarning(
                "Credencial da Fretops (ApiKey) ausente para {Environment}; sem cotação.",
                _options.Environment);
            return [];
        }

        if (request.Items is not { Count: > 0 })
        {
            logger.LogWarning("Cotação sem itens; a Fretops exige items[] por SKU.");
            return [];
        }

        using var quoteRequest = new HttpRequestMessage(HttpMethod.Post, "/partner/v1/quotes")
        {
            Content = JsonContent.Create(BuildQuoteBody(request))
        };
        quoteRequest.Headers.Add("X-Fretops-Api-Key", apiKey);
        quoteRequest.Headers.Add("Idempotency-Key", BuildIdempotencyKey(request));

        using var response = await httpClient.SendAsync(quoteRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Fretops retornou {Status} na cotação: {Body}", (int)response.StatusCode, error);
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseQuotes(doc.RootElement);
    }

    private static object BuildQuoteBody(CarrierQuoteRequest request) => new
    {
        destination_postal_code = request.DestinationZipCode,
        declared_value_brl = request.DeclaredValue,
        items = request.Items!.Select(i => new
        {
            sku = i.Sku,
            quantity = i.Quantity,
            weight_g = i.WeightG,
            length_cm = i.LengthCm,
            width_cm = i.WidthCm,
            height_cm = i.HeightCm
        })
    };

    /// <summary>
    /// Chave de idempotência determinística (hash do conteúdo da cotação), não um
    /// GUID por chamada. A Fretops trata "mesma Idempotency-Key + mesmo body" como
    /// a mesma cotação — uma chave estável por conteúdo evita criar cotações novas
    /// a cada reconsulta do mesmo carrinho e garante que um retry automático (ex.:
    /// 503 UPSTREAM_UNAVAILABLE) reenvie exatamente a mesma chave.
    /// </summary>
    private static string BuildIdempotencyKey(CarrierQuoteRequest request)
    {
        var sb = new StringBuilder()
            .Append(request.DestinationZipCode)
            .Append('|')
            .Append(request.DeclaredValue.ToString(CultureInfo.InvariantCulture));

        foreach (var item in request.Items!.OrderBy(i => i.Sku, StringComparer.Ordinal))
        {
            sb.Append('|').Append(item.Sku)
                .Append(':').Append(item.Quantity)
                .Append(':').Append(item.WeightG)
                .Append(':').Append(item.LengthCm)
                .Append(':').Append(item.WidthCm)
                .Append(':').Append(item.HeightCm);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexStringLower(hash);
    }

    private static IReadOnlyList<CarrierQuote> ParseQuotes(JsonElement root)
    {
        var quotes = new List<CarrierQuote>();

        if (!root.TryGetProperty("options", out var options))
            return quotes;

        foreach (var option in options.EnumerateArray())
        {
            var name = option.TryGetProperty("name", out var n) ? n.GetString() ?? "Fretops" : "Fretops";
            var service = option.TryGetProperty("service", out var s) ? s.GetString() : null;
            var price = option.GetProperty("price").GetDecimal();
            var days = option.TryGetProperty("days", out var d) ? d.GetInt32() : 0;
            var checkoutRef = option.GetProperty("checkout_ref").GetInt64();

            quotes.Add(new CarrierQuote
            {
                Carrier = CarrierType.Fretops,
                ServiceName = name,
                // Código "service" bruto da Fretops (ex.: "FRETOPS"); YampiMapping o
                // devolve ao checkout sem reformatar, como no exemplo do manual deles.
                ExternalServiceCode = service,
                Price = price,
                EstimatedDays = days,
                ExternalQuoteId = checkoutRef
            });
        }

        return quotes;
    }
}
