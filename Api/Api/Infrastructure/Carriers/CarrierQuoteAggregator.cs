using Api.Domain.Carriers;

namespace Api.Infrastructure.Carriers;

/// <summary>
/// Consulta as transportadoras <b>habilitadas</b> em paralelo e agrega as cotações.
/// O estado ligado/desligado vem do <see cref="ICarrierSettingsStore"/> (runtime).
/// A falha de uma transportadora (timeout, erro, circuit aberto) não derruba as
/// demais — apenas é registrada e ignorada.
/// </summary>
public sealed class CarrierQuoteAggregator(
    IEnumerable<ICarrierProvider> providers,
    ICarrierSettingsStore settings,
    ILogger<CarrierQuoteAggregator> logger)
{
    public async Task<IReadOnlyList<CarrierQuote>> GetQuotesAsync(
        CarrierQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var enabled = await settings.GetEnabledStatesAsync(cancellationToken);

        var active = providers
            .Where(p => enabled.TryGetValue(p.Carrier, out var on) && on)
            .ToList();

        if (active.Count == 0)
        {
            logger.LogInformation("Nenhuma transportadora habilitada para cotação.");
            return [];
        }

        var tasks = active.Select(async provider =>
        {
            try
            {
                return await provider.GetQuotesAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Transportadora {Carrier} falhou ao cotar; ignorando.", provider.Carrier);
                return [];
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.SelectMany(q => q).ToList();
    }
}
