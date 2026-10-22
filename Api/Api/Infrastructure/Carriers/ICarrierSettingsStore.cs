using Api.Domain.Carriers;

namespace Api.Infrastructure.Carriers;

/// <summary>
/// Estado de ativação (ligado/desligado) das transportadoras em runtime.
/// Quando não há registro no banco, cai no padrão da configuração estática.
/// </summary>
public interface ICarrierSettingsStore
{
    /// <summary>Estado efetivo (banco ou padrão) de cada transportadora conhecida.</summary>
    Task<IReadOnlyDictionary<CarrierType, bool>> GetEnabledStatesAsync(CancellationToken cancellationToken = default);

    /// <summary>Estado efetivo de uma transportadora específica.</summary>
    Task<bool> IsEnabledAsync(CarrierType carrier, CancellationToken cancellationToken = default);

    /// <summary>Liga/desliga uma transportadora (persistido).</summary>
    Task SetEnabledAsync(CarrierType carrier, bool isEnabled, CancellationToken cancellationToken = default);
}
