namespace Api.Application.Yampi;

/// <summary>Resultado de uma cotação para o webhook, com metadados para logging.</summary>
public sealed record QuoteOutcome(
    IReadOnlyList<YampiQuote> Quotes,
    bool RuleDecided,
    int CarrierCount,
    bool UsedFallback);

/// <summary>
/// Calcula as opções de frete para o webhook da Yampi: regras internas primeiro,
/// depois transportadoras (com orçamento de tempo) e, por fim, fallback configurável.
/// </summary>
public interface IShippingQuoteService
{
    Task<QuoteOutcome> QuoteAsync(YampiQuoteRequest request, CancellationToken cancellationToken = default);
}
