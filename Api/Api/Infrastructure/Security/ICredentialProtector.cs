namespace Api.Infrastructure.Security;

/// <summary>
/// Cifra e decifra valores sensíveis de credenciais. A chave-mestra vive fora do
/// banco (variável de ambiente / secret do Coolify); o banco só guarda o texto cifrado.
/// </summary>
public interface ICredentialProtector
{
    /// <summary>Cifra um valor em claro e devolve o payload (base64) a ser persistido.</summary>
    string Protect(string plaintext);

    /// <summary>Decifra um payload produzido por <see cref="Protect"/>.</summary>
    string Unprotect(string payload);
}
