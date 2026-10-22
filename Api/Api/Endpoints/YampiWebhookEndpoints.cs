using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Api.Application.Yampi;
using Api.Domain.Quotes;
using Api.Infrastructure.Persistence;
using Api.Infrastructure.Yampi;
using Microsoft.Extensions.Options;

namespace Api.Endpoints;

public static class YampiWebhookEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static void MapYampiWebhookEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/shipping").WithTags("Yampi Webhook");

        // Webhook público chamado pela Yampi no checkout ("Frete por API").
        group.MapPost("/quote", async (
            HttpRequest http,
            IYampiSignatureValidator signatureValidator,
            IShippingQuoteService quoteService,
            AppDbContext db,
            IOptions<YampiOptions> options,
            ILogger<YampiWebhookLogCategory> logger,
            CancellationToken cancellationToken) =>
        {
            var startedAt = Stopwatch.GetTimestamp();

            string rawBody;
            using (var reader = new StreamReader(http.Body, Encoding.UTF8))
                rawBody = await reader.ReadToEndAsync(cancellationToken);

            var signature = http.Headers[HmacYampiSignatureValidator.HeaderName].FirstOrDefault();
            var signatureValid = await signatureValidator.IsValidAsync(rawBody, signature, cancellationToken);

            if (options.Value.RequireSignature && !signatureValid)
                return Results.Unauthorized();

            YampiQuoteRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<YampiQuoteRequest>(rawBody, JsonOptions);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Corpo da requisição inválido." });
            }

            if (request is null || request.Skus.Count == 0)
                return Results.BadRequest(new { error = "Requisição sem itens (skus)." });

            QuoteOutcome outcome;
            try
            {
                outcome = await quoteService.QuoteAsync(request, cancellationToken);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            var response = new YampiQuoteResponse { Quotes = outcome.Quotes };
            var responseJson = JsonSerializer.Serialize(response, JsonOptions);
            var durationMs = (int)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

            await TryLogAsync(db, logger, rawBody, responseJson, request, outcome, signatureValid, durationMs, cancellationToken);

            return Results.Text(responseJson, "application/json");
        })
        .WithName("YampiShippingQuote")
        .WithSummary("Webhook de cálculo de frete chamado pela Yampi no checkout.");
    }

    private static async Task TryLogAsync(
        AppDbContext db,
        ILogger logger,
        string rawBody,
        string responseJson,
        YampiQuoteRequest request,
        QuoteOutcome outcome,
        bool signatureValid,
        int durationMs,
        CancellationToken cancellationToken)
    {
        try
        {
            db.ShippingQuoteLogs.Add(new ShippingQuoteLog
            {
                CreatedAt = DateTimeOffset.UtcNow,
                Source = "webhook",
                DestinationZipCode = new string(request.Zipcode.Where(char.IsDigit).ToArray()),
                CartAmount = request.Amount,
                RequestBody = rawBody,
                ResponseBody = responseJson,
                DurationMs = durationMs,
                RuleDecided = outcome.RuleDecided,
                CarrierQuoteCount = outcome.CarrierCount,
                UsedFallback = outcome.UsedFallback,
                SignatureValid = signatureValid
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Log é auditoria; nunca deve derrubar a resposta ao checkout.
            logger.LogWarning(ex, "Falha ao gravar log de cotação da Yampi.");
        }
    }

    /// <summary>Categoria de log dedicada ao webhook.</summary>
    public sealed class YampiWebhookLogCategory;
}
