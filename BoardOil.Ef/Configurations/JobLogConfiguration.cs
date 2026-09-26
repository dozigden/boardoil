using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class JobLogConfiguration : IEntityTypeConfiguration<EntityJobLog>
{
    public void Configure(EntityTypeBuilder<EntityJobLog> log)
    {
        log.HasKey(x => x.Id);
        log.Property(x => x.Level).IsRequired();
        log.Property(x => x.Message).HasMaxLength(2048).IsRequired();
        log.Property(x => x.DataJson).HasMaxLength(32768);
        log.Property(x => x.LoggedAtUtc).IsRequired();
        log.Property(x => x.CreatedAtUtc).IsRequired();
        log.Property(x => x.UpdatedAtUtc).IsRequired();
        log.HasIndex(x => x.JobId);
        log.ToTable("JobLogs");
    }
}
