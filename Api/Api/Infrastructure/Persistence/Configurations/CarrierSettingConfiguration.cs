using Api.Domain.Carriers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Persistence.Configurations;

public sealed class CarrierSettingConfiguration : IEntityTypeConfiguration<CarrierSetting>
{
    public void Configure(EntityTypeBuilder<CarrierSetting> builder)
    {
        builder.ToTable("carrier_settings");
        builder.HasKey(c => c.Carrier);

        builder.Property(c => c.Carrier).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.IsEnabled).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();
    }
}
