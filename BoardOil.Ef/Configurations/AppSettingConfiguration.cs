using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class AppSettingConfiguration : IEntityTypeConfiguration<EntityAppSetting>
{
    public void Configure(EntityTypeBuilder<EntityAppSetting> appSetting)
    {
        appSetting.HasKey(x => x.Id);
        appSetting.Property(x => x.Key).HasMaxLength(120).IsRequired();
        appSetting.Property(x => x.Value).HasMaxLength(4000).IsRequired();
        appSetting.Property(x => x.UpdatedAtUtc).IsRequired();
        appSetting.ToTable("AppSettings");
        appSetting.HasIndex(x => x.Key).IsUnique();
    }
}
