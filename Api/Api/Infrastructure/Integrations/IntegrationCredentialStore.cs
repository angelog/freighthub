using Api.Domain.Integrations;
using Api.Infrastructure.Persistence;
using Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.Integrations;

/// <summary>
/// Store de credenciais respaldado no PostgreSQL, com decifragem via <see cref="ICredentialProtector"/>.
/// Cada operação usa um <see cref="AppDbContext"/> próprio (escopo isolado): as transportadoras
/// leem credenciais em paralelo, e o DbContext não é seguro para uso concorrente.
/// </summary>
public sealed class IntegrationCredentialStore(
    IServiceScopeFactory scopeFactory,
    ICredentialProtector protector,
    TimeProvider timeProvider) : IIntegrationCredentialStore
{
    public async Task<IReadOnlyList<CredentialInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.IntegrationCredentials
            .AsNoTracking()
            .OrderBy(c => c.Integration).ThenBy(c => c.Key)
            .Select(c => new CredentialInfo(c.Integration, c.Environment, c.Key, c.IsActive, c.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        IntegrationType integration,
        IntegrationEnvironment environment,
        string key,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var removed = await db.IntegrationCredentials
            .Where(c => c.Integration == integration && c.Environment == environment && c.Key == key)
            .ExecuteDeleteAsync(cancellationToken);

        return removed > 0;
    }

    public async Task<string?> GetValueAsync(
        IntegrationType integration,
        IntegrationEnvironment environment,
        string key,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var credential = await db.IntegrationCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Integration == integration
                     && c.Environment == environment
                     && c.Key == key
                     && c.IsActive,
                cancellationToken);

        return credential is null ? null : protector.Unprotect(credential.EncryptedValue);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(
        IntegrationType integration,
        IntegrationEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var credentials = await db.IntegrationCredentials
            .AsNoTracking()
            .Where(c => c.Integration == integration && c.Environment == environment && c.IsActive)
            .ToListAsync(cancellationToken);

        return credentials.ToDictionary(c => c.Key, c => protector.Unprotect(c.EncryptedValue));
    }

    public async Task SetAsync(
        IntegrationType integration,
        IntegrationEnvironment environment,
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var encrypted = protector.Protect(value);
        var now = timeProvider.GetUtcNow();

        var existing = await db.IntegrationCredentials
            .FirstOrDefaultAsync(
                c => c.Integration == integration && c.Environment == environment && c.Key == key,
                cancellationToken);

        if (existing is null)
        {
            db.IntegrationCredentials.Add(new IntegrationCredential
            {
                Integration = integration,
                Environment = environment,
                Key = key,
                EncryptedValue = encrypted,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            existing.EncryptedValue = encrypted;
            existing.IsActive = true;
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
