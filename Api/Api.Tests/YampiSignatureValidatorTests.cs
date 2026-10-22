using System.Security.Cryptography;
using System.Text;
using Api.Domain.Integrations;
using Api.Infrastructure.Integrations;
using Api.Infrastructure.Yampi;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api.Tests;

public class YampiSignatureValidatorTests
{
    private const string Secret = "minha-secret-de-teste";
    private const string Body = """{"zipcode":"04543000","amount":50.00}""";

    private static HmacYampiSignatureValidator CreateValidator(string? storedSecret = Secret)
    {
        var store = new FakeCredentialStore(storedSecret);
        var options = Options.Create(new YampiOptions { Environment = IntegrationEnvironment.Production });
        return new HmacYampiSignatureValidator(store, options, NullLogger<HmacYampiSignatureValidator>.Instance);
    }

    private static string Sign(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)));
    }

    [Fact]
    public async Task Valid_signature_passes()
    {
        var validator = CreateValidator();
        Assert.True(await validator.IsValidAsync(Body, Sign(Body, Secret)));
    }

    [Fact]
    public async Task Wrong_signature_fails()
    {
        var validator = CreateValidator();
        Assert.False(await validator.IsValidAsync(Body, "assinatura-invalida"));
    }

    [Fact]
    public async Task Tampered_body_fails()
    {
        var validator = CreateValidator();
        var signature = Sign(Body, Secret);
        Assert.False(await validator.IsValidAsync(Body + " ", signature));
    }

    [Fact]
    public async Task Missing_signature_header_fails()
    {
        var validator = CreateValidator();
        Assert.False(await validator.IsValidAsync(Body, null));
    }

    [Fact]
    public async Task Missing_secret_fails()
    {
        var validator = CreateValidator(storedSecret: null);
        Assert.False(await validator.IsValidAsync(Body, Sign(Body, Secret)));
    }

    private sealed class FakeCredentialStore(string? secret) : IIntegrationCredentialStore
    {
        public Task<IReadOnlyList<CredentialInfo>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CredentialInfo>>([]);

        public Task<bool> DeleteAsync(
            IntegrationType integration, IntegrationEnvironment environment, string key,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<string?> GetValueAsync(
            IntegrationType integration, IntegrationEnvironment environment, string key,
            CancellationToken cancellationToken = default)
            => Task.FromResult(secret);

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(
            IntegrationType integration, IntegrationEnvironment environment,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

        public Task SetAsync(
            IntegrationType integration, IntegrationEnvironment environment, string key, string value,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
