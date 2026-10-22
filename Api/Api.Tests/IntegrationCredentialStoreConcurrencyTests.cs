using Api.Domain.Integrations;
using Api.Infrastructure.Integrations;
using Api.Infrastructure.Persistence;
using Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.Tests;

/// <summary>
/// Regressão: as transportadoras leem credenciais em paralelo (agregador usa Task.WhenAll).
/// O store precisa usar um DbContext isolado por chamada — senão o EF lança
/// "A second operation was started on this context instance".
/// </summary>
public class IntegrationCredentialStoreConcurrencyTests
{
    private const string EncryptionKey = "oEqPvD0nIrL1Vix84aJUB9oNM8OQ0nEu/CWAuOo7oGA=";

    private static (IntegrationCredentialStore store, ICredentialProtector protector, IServiceScopeFactory scopes)
        BuildStore()
    {
        // Nome FIXO do banco InMemory — todos os DbContext (um por chamada do store)
        // precisam apontar para o mesmo banco, senão cada um vê um banco vazio.
        var dbName = $"creds-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();

        var scopes = provider.GetRequiredService<IServiceScopeFactory>();
        var protector = new AesGcmCredentialProtector(
            Options.Create(new CredentialProtectionOptions { EncryptionKey = EncryptionKey }));
        var store = new IntegrationCredentialStore(scopes, protector, TimeProvider.System);
        return (store, protector, scopes);
    }

    private static async Task SeedAsync(IServiceScopeFactory scopes, ICredentialProtector protector)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.IntegrationCredentials.AddRange(
            Credential(protector, IntegrationType.Correios, "AccessKey", "correios-key"),
            Credential(protector, IntegrationType.Loggi, "ClientId", "loggi-id"),
            Credential(protector, IntegrationType.Loggi, "ClientSecret", "loggi-secret"),
            Credential(protector, IntegrationType.Loggi, "CompanyId", "123"));
        await db.SaveChangesAsync();
    }

    private static IntegrationCredential Credential(
        ICredentialProtector protector, IntegrationType integration, string key, string value) => new()
    {
        Integration = integration,
        Environment = IntegrationEnvironment.Production,
        Key = key,
        EncryptedValue = protector.Protect(value),
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Concurrent_reads_do_not_collide_and_decrypt_correctly()
    {
        var (store, protector, scopes) = BuildStore();
        await SeedAsync(scopes, protector);

        // 100 leituras concorrentes, misturando GetValueAsync e GetAllAsync (como carriers em paralelo).
        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(async () =>
        {
            if (i % 2 == 0)
            {
                var key = await store.GetValueAsync(IntegrationType.Correios, IntegrationEnvironment.Production, "AccessKey");
                Assert.Equal("correios-key", key);
            }
            else
            {
                var all = await store.GetAllAsync(IntegrationType.Loggi, IntegrationEnvironment.Production);
                Assert.Equal("loggi-id", all["ClientId"]);
                Assert.Equal("123", all["CompanyId"]);
            }
        }));

        // Não deve lançar "second operation on DbContext".
        await Task.WhenAll(tasks);
    }
}
