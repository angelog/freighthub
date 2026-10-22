namespace Api.Infrastructure.Auth;

/// <summary>Configuração do JWT (seção "Jwt"). A chave de assinatura vem de env em produção.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Chave de assinatura HS256 (mínimo 32 bytes/256 bits).</summary>
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "freighthub";
    public string Audience { get; set; } = "freighthub";

    /// <summary>Validade do token em minutos (default 8h).</summary>
    public int ExpiryMinutes { get; set; } = 480;
}
