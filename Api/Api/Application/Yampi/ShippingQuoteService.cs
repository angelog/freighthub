using Api.Domain.Carriers;
using Api.Domain.Rules;
using Api.Infrastructure.Carriers;
using Api.Infrastructure.Persistence;
using Api.Infrastructure.Yampi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Application.Yampi;

public sealed class ShippingQuoteService(
    AppDbContext db,
    RuleEngine ruleEngine,
    CarrierQuoteAggregator carriers,
    IOptions<CarrierOptions> carrierOptions,
    IOptions<YampiOptions> yampiOptions,
    TimeProvider timeProvider,
    ILogger<ShippingQuoteService> logger) : IShippingQuoteService
{
    private readonly CarrierOptions _carrierOptions = carrierOptions.Value;
    private readonly YampiOptions _yampiOptions = yampiOptions.Value;

    public async Task<QuoteOutcome> QuoteAsync(
        YampiQuoteRequest request, CancellationToken cancellationToken = default)
    {
        var context = YampiMapping.ToShippingContext(request, timeProvider.GetUtcNow());

        // 1) Regras internas (sem custo de rede).
        var rules = await db.ShippingRules
            .AsNoTracking()
            .Where(r => r.IsActive)
            .ToListAsync(cancellationToken);

        var evaluation = ruleEngine.Evaluate(rules, context);
        if (evaluation.HasDecision)
        {

            var quotes = await QueryCarriersWithBudgetAsync(context, cancellationToken);
            var cheapest = quotes.OrderBy(q => q.Price).FirstOrDefault();

            if (evaluation.Result.IsFreeShipping)
            {
                var free = YampiMapping.FreeShippingQuote(cheapest, _yampiOptions.Fallback);
                return new QuoteOutcome([free], RuleDecided: true, quotes.Count, UsedFallback: cheapest is null);
            }


            var fixedQuote = YampiMapping.RuleFixedQuote(evaluation, cheapest, _yampiOptions.Fallback);
            return new QuoteOutcome([fixedQuote], RuleDecided: true, quotes.Count, UsedFallback: cheapest is null);
        }

        // 2) Transportadoras, com orçamento de tempo (para não estourar os 4s da Yampi).
        var carrierQuotes = await QueryCarriersWithBudgetAsync(context, cancellationToken);
        if (carrierQuotes.Count > 0)
        {
            var quotes = YampiMapping.CarrierQuotes(carrierQuotes);
            return new QuoteOutcome(quotes, RuleDecided: false, carrierQuotes.Count, UsedFallback: false);
        }

        // 3) Fallback configurável.
        if (_yampiOptions.Fallback.Enabled)
            return new QuoteOutcome([YampiMapping.FallbackQuote(_yampiOptions.Fallback)], false, 0, UsedFallback: true);

        logger.LogWarning("Nenhuma cotação disponível e fallback desabilitado; respondendo sem opções.");
        return new QuoteOutcome([], false, 0, false);
    }

    private async Task<IReadOnlyList<CarrierQuote>> QueryCarriersWithBudgetAsync(
        Domain.Shipping.ShippingContext context, CancellationToken cancellationToken)
    {
        var carrierRequest = YampiMapping.ToCarrierRequest(context, _carrierOptions.OriginZipCode);
        if (carrierRequest is null)
        {
            logger.LogWarning("Carriers:OriginZipCode inválido; sem cotação de transportadoras.");
            return [];
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_yampiOptions.CarrierTimeoutMs);

        try
        {
            return await carriers.GetQuotesAsync(carrierRequest, cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Cotação de transportadoras excedeu {TimeoutMs}ms; usando fallback.",
                _yampiOptions.CarrierTimeoutMs);
            return [];
        }
    }
}
