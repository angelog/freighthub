using Api.Domain.Rules;
using Api.Infrastructure.Carriers;
using Api.Infrastructure.Integrations;
using Api.Infrastructure.Persistence;
using Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure;

/// <summary>
/// Registro dos serviços da camada de infraestrutura (banco, clients externos, etc.).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' não configurada. " +
                "Defina em appsettings ou via variável de ambiente " +
                "ConnectionStrings__DefaultConnection.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Motor de regras — sem estado, seguro como singleton.
        services.AddSingleton<RuleEngine>();

        services.AddSingleton(TimeProvider.System);
        services.AddMemoryCache();

        // Criptografia e store de credenciais das integrações.
        services.Configure<CredentialProtectionOptions>(
            configuration.GetSection(CredentialProtectionOptions.SectionName));
        services.AddSingleton<ICredentialProtector, AesGcmCredentialProtector>();
        services.AddScoped<IIntegrationCredentialStore, IntegrationCredentialStore>();

        // Transportadoras (Correios/Loggi conforme configuração).
        services.AddCarriers(configuration);

        return services;
    }
}
