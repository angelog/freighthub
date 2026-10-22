using Api.Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Persistence.Configurations;

public sealed class ShippingQuoteLogConfiguration : IEntityTypeConfiguration<ShippingQuoteLog>
{
    public void Configure(EntityTypeBuilder<ShippingQuoteLog> builder)
    {
        builder.ToTable("shipping_quote_logs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.CreatedAt).IsRequired();
        builder.Property(l => l.Source).HasMaxLength(20).IsRequired();
        builder.Property(l => l.DestinationZipCode).HasMaxLength(8);
        builder.Property(l => l.CartAmount).HasPrecision(12, 2);
        builder.Property(l => l.RequestBody).HasColumnType("jsonb");
        builder.Property(l => l.ResponseBody).HasColumnType("jsonb");

        // Índice para o expurgo por data (retenção de 7 dias).
        builder.HasIndex(l => l.CreatedAt);
    }
}
