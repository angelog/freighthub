using Api.Domain.Carriers;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.Carriers;

public sealed class CarrierSettingsStore(
    AppDbContext db,
    IOptions<CarrierOptions> options,
    TimeProvider timeProvider) : ICarrierSettingsStore
{
    private readonly CarrierOptions _options = options.Value;

    public async Task<IReadOnlyDictionary<CarrierType, bool>> GetEnabledStatesAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await db.CarrierSettings.AsNoTracking().ToListAsync(cancellationToken);

        return Enum.GetValues<CarrierType>().ToDictionary(
            carrier => carrier,
            carrier => rows.FirstOrDefault(r => r.Carrier == carrier)?.IsEnabled ?? DefaultFor(carrier));
    }

    public async Task<bool> IsEnabledAsync(CarrierType carrier, CancellationToken cancellationToken = default)
    {
        var row = await db.CarrierSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Carrier == carrier, cancellationToken);

        return row?.IsEnabled ?? DefaultFor(carrier);
    }

    public async Task SetEnabledAsync(
        CarrierType carrier, bool isEnabled, CancellationToken cancellationToken = default)
    {
        var existing = await db.CarrierSettings
            .FirstOrDefaultAsync(r => r.Carrier == carrier, cancellationToken);

        if (existing is null)
            db.CarrierSettings.Add(new CarrierSetting
            {
                Carrier = carrier,
                IsEnabled = isEnabled,
                UpdatedAt = timeProvider.GetUtcNow()
            });
        else
        {
            existing.IsEnabled = isEnabled;
            existing.UpdatedAt = timeProvider.GetUtcNow();
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Padrão vindo da configuração estática quando não há registro no banco.</summary>
    private bool DefaultFor(CarrierType carrier) => carrier switch
    {
        CarrierType.Loggi => _options.Loggi.Enabled,
        CarrierType.Correios => _options.Correios.Enabled,
        CarrierType.Fretops => _options.Fretops.Enabled,
        _ => false
    };
}
