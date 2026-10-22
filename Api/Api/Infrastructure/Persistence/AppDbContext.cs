using Api.Domain.Carriers;
using Api.Domain.Integrations;
using Api.Domain.Quotes;
using Api.Domain.Rules;
using Api.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.Persistence;

/// <summary>
/// Contexto de acesso a dados da aplicação (PostgreSQL).
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Regras de frete. Mapeadas via TPH (Table-Per-Hierarchy): todas as regras
    /// ficam na mesma tabela, distinguidas por uma coluna discriminadora.
    /// </summary>
    public DbSet<ShippingRule> ShippingRules => Set<ShippingRule>();

    /// <summary>Credenciais das integrações (Correios/Loggi/Yampi), com valor cifrado.</summary>
    public DbSet<IntegrationCredential> IntegrationCredentials => Set<IntegrationCredential>();

    /// <summary>Liga/desliga de cada transportadora em runtime.</summary>
    public DbSet<CarrierSetting> CarrierSettings => Set<CarrierSetting>();

    /// <summary>Log de cotações (webhook/simulador), com retenção de 7 dias.</summary>
    public DbSet<ShippingQuoteLog> ShippingQuoteLogs => Set<ShippingQuoteLog>();

    /// <summary>Usuários do portal administrativo.</summary>
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica automaticamente todas as configurações de entidade
        // (IEntityTypeConfiguration) declaradas neste assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
