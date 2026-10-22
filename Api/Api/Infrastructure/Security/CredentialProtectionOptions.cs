namespace Api.Infrastructure.Security;

/// <summary>
/// Configuração da criptografia de credenciais. A chave é lida da seção
/// "Credentials" (ex.: variável de ambiente Credentials__EncryptionKey no Coolify).
/// </summary>
public sealed class CredentialProtectionOptions
{
    public const string SectionName = "Credentials";

    /// <summary>
    /// Chave-mestra AES de 256 bits, codificada em Base64 (32 bytes).
    /// Gere com: <c>openssl rand -base64 32</c>.
    /// </summary>
    public string EncryptionKey { get; set; } = string.Empty;
}
