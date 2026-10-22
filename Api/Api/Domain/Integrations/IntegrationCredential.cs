namespace Api.Domain.Integrations;

/// <summary>
/// Uma credencial de integração (par chave/valor) por integração e ambiente.
/// O modelo chave-valor permite adicionar novos campos (token, company_id, secret…)
/// sem migration nova. O <see cref="EncryptedValue"/> é sempre armazenado cifrado —
/// a decifragem acontece na camada de leitura (store), nunca em texto puro no banco.
/// </summary>
public sealed class IntegrationCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public IntegrationType Integration { get; set; }

    public IntegrationEnvironment Environment { get; set; }

    /// <summary>Nome do campo (ex.: "BearerToken", "CompanyId", "SecretKey").</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Valor cifrado (payload base64 produzido pelo protector).</summary>
    public string EncryptedValue { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
