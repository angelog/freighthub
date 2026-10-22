using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

public static class QuoteLogEndpoints
{
    public sealed record QuoteLogDto(
        Guid Id,
        DateTimeOffset CreatedAt,
        string Source,
        string DestinationZipCode,
        decimal CartAmount,
        int DurationMs,
        bool RuleDecided,
        int CarrierQuoteCount,
        bool UsedFallback,
        bool SignatureValid,
        string RequestBody,
        string ResponseBody);

    public static void MapQuoteLogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/quote-logs").WithTags("Quote Logs").RequireAuthorization();

        // Histórico das cotações (retenção de 7 dias). Mais recentes primeiro.
        group.MapGet("/", async (AppDbContext db, int? limit, CancellationToken ct) =>
        {
            var take = Math.Clamp(limit ?? 100, 1, 500);
            var logs = await db.ShippingQuoteLogs
                .AsNoTracking()
                .OrderByDescending(l => l.CreatedAt)
                .Take(take)
                .Select(l => new QuoteLogDto(
                    l.Id, l.CreatedAt, l.Source, l.DestinationZipCode, l.CartAmount, l.DurationMs,
                    l.RuleDecided, l.CarrierQuoteCount, l.UsedFallback, l.SignatureValid,
                    l.RequestBody, l.ResponseBody))
                .ToListAsync(ct);

            return Results.Ok(logs);
        })
        .WithName("ListQuoteLogs")
        .WithSummary("Histórico de cotações dos últimos 7 dias.");
    }
}
