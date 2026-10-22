using System.Security.Cryptography;
using Api.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Api.Tests;

public class CredentialProtectorTests
{
    // Chave dev fixa (Base64, 32 bytes) só para os testes.
    private const string TestKey = "oEqPvD0nIrL1Vix84aJUB9oNM8OQ0nEu/CWAuOo7oGA=";

    private static AesGcmCredentialProtector CreateProtector(string key = TestKey) =>
        new(Options.Create(new CredentialProtectionOptions { EncryptionKey = key }));

    [Fact]
    public void Protect_then_Unprotect_roundtrips()
    {
        var protector = CreateProtector();
        const string secret = "Bearer super-secret-token-123";

        var payload = protector.Protect(secret);
        var recovered = protector.Unprotect(payload);

        Assert.Equal(secret, recovered);
    }

    [Fact]
    public void Protected_payload_is_not_plaintext()
    {
        var protector = CreateProtector();
        const string secret = "company_id=42";

        var payload = protector.Protect(secret);

        Assert.DoesNotContain("company_id", payload);
        Assert.NotEqual(secret, payload);
    }

    [Fact]
    public void Protect_produces_different_payloads_each_time()
    {
        var protector = CreateProtector();

        // nonce aleatório => ciphertexts distintos para o mesmo texto.
        Assert.NotEqual(protector.Protect("same"), protector.Protect("same"));
    }

    [Fact]
    public void Unprotect_with_wrong_key_fails()
    {
        var payload = CreateProtector().Protect("secret");
        var otherKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var otherProtector = CreateProtector(otherKey);

        Assert.Throws<AuthenticationTagMismatchException>(() => otherProtector.Unprotect(payload));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-base64!!!")]
    [InlineData("c2hvcnQ=")] // Base64 válido, mas < 32 bytes
    public void Invalid_key_is_rejected_on_construction(string badKey)
    {
        Assert.Throws<InvalidOperationException>(() => CreateProtector(badKey));
    }
}
