namespace Api.Infrastructure.Yampi;

/// <summary>
/// Valida a assinatura HMAC-SHA256 (base64) enviada pela Yampi no header
/// <c>X-Yampi-Hmac-SHA256</c>, calculada sobre o corpo bruto da requisição.
/// </summary>
public interface IYampiSignatureValidator
{
    Task<bool> IsValidAsync(string rawBody, string? headerSignature, CancellationToken cancellationToken = default);
}
