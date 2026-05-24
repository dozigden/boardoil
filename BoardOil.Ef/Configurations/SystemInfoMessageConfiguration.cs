using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class SystemInfoMessageConfiguration : IEntityTypeConfiguration<EntitySystemInfoMessage>
{
    public void Configure(EntityTypeBuilder<EntitySystemInfoMessage> systemInfoMessage)
    {
        systemInfoMessage.HasKey(x => x.Id);
        systemInfoMessage.Property(x => x.Enabled).IsRequired();
        systemInfoMessage.Property(x => x.Emoji).HasMaxLength(64);
        systemInfoMessage.Property(x => x.Title).HasMaxLength(200).IsRequired();
        systemInfoMessage.Property(x => x.Description).IsRequired();
        systemInfoMessage.Property(x => x.StyleName).HasMaxLength(32).IsRequired();
        systemInfoMessage.Property(x => x.StylePropertiesJson).HasMaxLength(4000).IsRequired();
        systemInfoMessage.Property(x => x.CreatedAtUtc).IsRequired();
        systemInfoMessage.Property(x => x.UpdatedAtUtc).IsRequired();
        systemInfoMessage.ToTable("SystemInfoMessage", table =>
        {
            table.HasCheckConstraint(
                "CK_SystemInfoMessage_StyleName",
                "\"StyleName\" IN ('auto', 'presets', 'solid')");
        });
    }
}
