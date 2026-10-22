using Api.Domain.Users;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.Auth;

/// <summary>
/// Garante um usuário admin inicial quando a base de usuários está vazia,
/// para permitir o primeiro login. Não faz nada se já existir algum usuário.
/// </summary>
public sealed class AdminSeeder(
    IServiceScopeFactory scopeFactory,
    IPasswordHasher<User> passwordHasher,
    IOptions<InitialAdminOptions> options,
    TimeProvider timeProvider,
    ILogger<AdminSeeder> logger) : IHostedService
{
    private readonly InitialAdminOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await db.Users.AnyAsync(cancellationToken))
            return;

        if (string.IsNullOrWhiteSpace(_options.Email) || string.IsNullOrWhiteSpace(_options.Password))
        {
            logger.LogWarning(
                "Nenhum usuário cadastrado e Auth:InitialAdmin não configurado; " +
                "defina Email/Password para criar o admin inicial.");
            return;
        }

        var now = timeProvider.GetUtcNow();
        var admin = new User
        {
            Name = _options.Name,
            Email = _options.Email.Trim().ToLowerInvariant(),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, _options.Password);

        db.Users.Add(admin);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Usuário admin inicial criado: {Email}", admin.Email);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
