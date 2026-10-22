using Api.Domain.Integrations;

namespace Api.Infrastructure.Integrations;

/// <summary>Metadados de uma credencial (sem o valor — que nunca é exposto).</summary>
public sealed record CredentialInfo(
    IntegrationType Integration,
    IntegrationEnvironment Environment,
    string Key,
    bool IsActive,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Acesso às credenciais de integração. Abstrai o banco e a decifragem: quem
/// consome recebe sempre o valor em claro, sem saber que estava cifrado.
/// </summary>
public interface IIntegrationCredentialStore
{
    /// <summary>Lista os metadados das credenciais cadastradas (nunca o valor).</summary>
    Task<IReadOnlyList<CredentialInfo>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Remove uma credencial. Retorna false se não existia.</summary>
    Task<bool> DeleteAsync(
        IntegrationType integration,
        IntegrationEnvironment environment,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>Valor decifrado de uma credencial específica (ou null se não existir/inativa).</summary>
    Task<string?> GetValueAsync(
        IntegrationType integration,
        IntegrationEnvironment environment,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>Todas as credenciais ativas de uma integração/ambiente, já decifradas, por chave.</summary>
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(
        IntegrationType integration,
        IntegrationEnvironment environment,
        CancellationToken cancellationToken = default);

    /// <summary>Cria ou atualiza uma credencial (cifra o valor antes de persistir).</summary>
    Task SetAsync(
        IntegrationType integration,
        IntegrationEnvironment environment,
        string key,
        string value,
        CancellationToken cancellationToken = default);
}
