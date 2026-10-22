namespace Api.Domain.Integrations;

/// <summary>Integração externa à qual uma credencial pertence.</summary>
public enum IntegrationType
{
    Correios = 1,
    Loggi = 2,
    Yampi = 3,
    Fretops = 4
}

/// <summary>Ambiente da credencial — permite manter homologação e produção lado a lado.</summary>
public enum IntegrationEnvironment
{
    Homolog = 1,
    Production = 2
}
