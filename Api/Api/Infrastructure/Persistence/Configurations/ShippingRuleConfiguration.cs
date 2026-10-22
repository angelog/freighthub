using Api.Domain.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento TPH da hierarquia de regras de frete. A tabela única
/// "shipping_rules" guarda todos os tipos, discriminados pela coluna "rule_type".
/// As colunas específicas de cada tipo recebem nomes explícitos para manter o
/// schema legível (facilita o CRUD do portal).
/// </summary>
public sealed class ShippingRuleConfiguration : IEntityTypeConfiguration<ShippingRule>
{
    public void Configure(EntityTypeBuilder<ShippingRule> builder)
    {
        builder.ToTable("shipping_rules");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Priority).IsRequired();
        builder.Property(r => r.IsActive).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        builder.HasIndex(r => new { r.IsActive, r.Priority });

        // Discriminador TPH.
        builder.HasDiscriminator<string>("rule_type")
            .HasValue<FreeShippingAboveValueRule>("free_above_value")
            .HasValue<MinimumOrderFixedShippingRule>("min_order_fixed")
            .HasValue<MaximumOrderFixedShippingRule>("max_order_fixed")
            .HasValue<WeekdayPricingRule>("weekday")
            .HasValue<RegionFreeShippingRule>("region_free")
            .HasValue<SpecificDateRule>("specific_date")
            .HasValue<DateRangeRule>("date_range");

        builder.Property("rule_type").HasMaxLength(50);
    }
}

/// <summary>Colunas específicas da regra de frete grátis por valor.</summary>
public sealed class FreeShippingAboveValueRuleConfiguration : IEntityTypeConfiguration<FreeShippingAboveValueRule>
{
    public void Configure(EntityTypeBuilder<FreeShippingAboveValueRule> builder)
    {
        builder.Property(r => r.MinimumOrderValue)
            .HasColumnName("free_above_min_value")
            .HasPrecision(12, 2);
    }
}

/// <summary>Colunas específicas da regra de frete fixo por valor mínimo.</summary>
public sealed class MinimumOrderFixedShippingRuleConfiguration : IEntityTypeConfiguration<MinimumOrderFixedShippingRule>
{
    public void Configure(EntityTypeBuilder<MinimumOrderFixedShippingRule> builder)
    {
        builder.Property(r => r.MinimumOrderValue)
            .HasColumnName("min_fixed_threshold")
            .HasPrecision(12, 2);

        builder.Property(r => r.FixedPrice)
            .HasColumnName("min_fixed_price")
            .HasPrecision(12, 2);
    }
}

/// <summary>Colunas específicas da regra de frete fixo por valor máximo.</summary>
public sealed class MaximumOrderFixedShippingRuleConfiguration : IEntityTypeConfiguration<MaximumOrderFixedShippingRule>
{
    public void Configure(EntityTypeBuilder<MaximumOrderFixedShippingRule> builder)
    {
        builder.Property(r => r.MaximumOrderValue)
            .HasColumnName("max_fixed_threshold")
            .HasPrecision(12, 2);

        builder.Property(r => r.FixedPrice)
            .HasColumnName("max_fixed_price")
            .HasPrecision(12, 2);
    }
}

/// <summary>Colunas específicas da regra de preço por dia da semana.</summary>
public sealed class WeekdayPricingRuleConfiguration : IEntityTypeConfiguration<WeekdayPricingRule>
{
    public void Configure(EntityTypeBuilder<WeekdayPricingRule> builder)
    {
        builder.Property(r => r.DayOfWeek)
            .HasColumnName("weekday_day")
            .HasConversion<int>();

        builder.Property(r => r.IsFree).HasColumnName("weekday_is_free");

        builder.Property(r => r.Price)
            .HasColumnName("weekday_price")
            .HasPrecision(12, 2);
    }
}

/// <summary>Colunas específicas da regra pontual por data exata.</summary>
public sealed class SpecificDateRuleConfiguration : IEntityTypeConfiguration<SpecificDateRule>
{
    public void Configure(EntityTypeBuilder<SpecificDateRule> builder)
    {
        builder.Property(r => r.Date).HasColumnName("specific_date");
        builder.Property(r => r.IsFree).HasColumnName("specific_is_free");

        builder.Property(r => r.Price)
            .HasColumnName("specific_price")
            .HasPrecision(12, 2);
    }
}

/// <summary>Colunas específicas da regra pontual por intervalo de datas.</summary>
public sealed class DateRangeRuleConfiguration : IEntityTypeConfiguration<DateRangeRule>
{
    public void Configure(EntityTypeBuilder<DateRangeRule> builder)
    {
        builder.Property(r => r.StartDate).HasColumnName("range_start");
        builder.Property(r => r.EndDate).HasColumnName("range_end");
        builder.Property(r => r.IsFree).HasColumnName("range_is_free");

        builder.Property(r => r.Price)
            .HasColumnName("range_price")
            .HasPrecision(12, 2);
    }
}

/// <summary>
/// Faixas de CEP da regra de frete grátis por região, numa tabela própria
/// ("cep_ranges") como coleção owned da RegionFreeShippingRule.
/// </summary>
public sealed class RegionFreeShippingRuleConfiguration : IEntityTypeConfiguration<RegionFreeShippingRule>
{
    public void Configure(EntityTypeBuilder<RegionFreeShippingRule> builder)
    {
        builder.OwnsMany(r => r.CepRanges, nav =>
        {
            nav.ToTable("cep_ranges");
            nav.WithOwner().HasForeignKey("shipping_rule_id");
            nav.Property<int>("id");
            nav.HasKey("id");
            nav.Property(c => c.Start).HasColumnName("cep_start").HasMaxLength(8).IsRequired();
            nav.Property(c => c.End).HasColumnName("cep_end").HasMaxLength(8).IsRequired();
        });
    }
}
