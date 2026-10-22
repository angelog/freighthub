namespace Api.Domain.Carriers;

/// <summary>
/// Configuração operacional (runtime) de uma transportadora: liga/desliga sem
/// redeploy. Quando não há registro para uma transportadora, o padrão vem da
/// configuração estática (CarrierOptions).
/// </summary>
public sealed class CarrierSetting
{
    /// <summary>Transportadora (chave primária).</summary>
    public CarrierType Carrier { get; set; }

    public bool IsEnabled { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
