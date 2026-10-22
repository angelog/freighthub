using Api.Domain.Integrations;

namespace Api.Infrastructure.Yampi;

/// <summary>Configuração do webhook da Yampi (seção "Yampi").</summary>
public sealed class YampiOptions
{
    public const string SectionName = "Yampi";

    /// <summary>Ambiente das credenciais (onde está a SecretKey da Yampi).</summary>
    public IntegrationEnvironment Environment { get; set; } = IntegrationEnvironment.Production;

    /// <summary>Se false, o webhook aceita sem validar assinatura (apenas para dev/teste).</summary>
    public bool RequireSignature { get; set; } = true;

    /// <summary>Orçamento de tempo para consultar transportadoras antes de cair no fallback (ms).</summary>
    public int CarrierTimeoutMs { get; set; } = 3500;

    /// <summary>Dias de retenção do log de cotações.</summary>
    public int LogRetentionDays { get; set; } = 7;

    public YampiFallback Fallback { get; set; } = new();
}

/// <summary>Frete de fallback quando as transportadoras não respondem a tempo.</summary>
public sealed class YampiFallback
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "Frete padrão";
    public string Service { get; set; } = "PADRAO";
    public decimal Price { get; set; } = 29.90m;
    public int Days { get; set; } = 7;
}
