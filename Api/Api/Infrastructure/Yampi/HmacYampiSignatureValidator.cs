using System.Security.Cryptography;
using System.Text;
using Api.Domain.Integrations;
using Api.Infrastructure.Integrations;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.Yampi;

public sealed class HmacYampiSignatureValidator(
    IIntegrationCredentialStore credentials,
    IOptions<YampiOptions> options,
    ILogger<HmacYampiSignatureValidator> logger) : IYampiSignatureValidator
{
    public const string HeaderName = "X-Yampi-Hmac-SHA256";
    private readonly YampiOptions _options = options.Value;

    public async Task<bool> IsValidAsync(
        string rawBody, string? headerSignature, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(headerSignature))
            return false;

        var secret = await credentials.GetValueAsync(
            IntegrationType.Yampi, _options.Environment, "SecretKey", cancellationToken);

        if (string.IsNullOrWhiteSpace(secret))
        {
            logger.LogWarning(
                "SecretKey da Yampi não configurada para {Environment}; não é possível validar a assinatura.",
                _options.Environment);
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var computedBase64 = Convert.ToBase64String(computed);

        // Comparação em tempo constante (evita timing attack).
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedBase64),
            Encoding.UTF8.GetBytes(headerSignature));
    }
}
