using Api.Domain.Integrations;

namespace Api.Infrastructure.Carriers;

/// <summary>
/// Configuração das transportadoras (seção "Carriers"). Endpoints e flags ficam
/// aqui; segredos (tokens) vêm do store de credenciais, não daqui.
/// </summary>
public sealed class CarrierOptions
{
    public const string SectionName = "Carriers";

    /// <summary>CEP de origem da loja (de onde os pedidos são despachados).</summary>
    public string OriginZipCode { get; set; } = string.Empty;

    /// <summary>Ambiente das credenciais a usar (Homolog/Production).</summary>
    public IntegrationEnvironment Environment { get; set; } = IntegrationEnvironment.Homolog;

    public CarrierEndpoint Correios { get; set; } = new();
    public CarrierEndpoint Loggi { get; set; } = new();
    public CarrierEndpoint Fretops { get; set; } = new();
}

public sealed class CarrierEndpoint
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Códigos de serviço usados na cotação (ex.: Correios SEDEX/PAC contratuais).
    /// Vazio até o contrato liberar as APIs de Preço/Prazo.
    /// </summary>
    public List<string> ServiceCodes { get; set; } = [];
}
