using Api.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Persistence.Configurations;

public sealed class IntegrationCredentialConfiguration : IEntityTypeConfiguration<IntegrationCredential>
{
    public void Configure(EntityTypeBuilder<IntegrationCredential> builder)
    {
        builder.ToTable("integration_credentials");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Integration).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(c => c.Environment).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.Key).HasMaxLength(100).IsRequired();
        builder.Property(c => c.EncryptedValue).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        // Uma credencial por (integração, ambiente, chave).
        builder.HasIndex(c => new { c.Integration, c.Environment, c.Key }).IsUnique();
    }
}
