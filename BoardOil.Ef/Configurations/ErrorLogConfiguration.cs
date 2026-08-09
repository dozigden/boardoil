using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class ErrorLogConfiguration : IEntityTypeConfiguration<EntityErrorLog>
{
    public void Configure(EntityTypeBuilder<EntityErrorLog> errorLog)
    {
        errorLog.HasKey(x => x.Id);
        errorLog.Property(x => x.OccurredAtUtc).IsRequired();
        errorLog.Property(x => x.Source).HasMaxLength(32).IsRequired();
        errorLog.Property(x => x.Area).HasMaxLength(64).IsRequired();
        errorLog.Property(x => x.ExceptionType).HasMaxLength(512).IsRequired();
        errorLog.Property(x => x.Message).HasMaxLength(2048).IsRequired();
        errorLog.Property(x => x.StackTrace).HasMaxLength(32768);
        errorLog.Property(x => x.TraceIdentifier).HasMaxLength(128);
        errorLog.Property(x => x.RequestMethod).HasMaxLength(16);
        errorLog.Property(x => x.RequestPath).HasMaxLength(2048);
        errorLog.Property(x => x.ContextJson).HasMaxLength(32768);
        errorLog.Property(x => x.CreatedAtUtc).IsRequired();
        errorLog.Property(x => x.UpdatedAtUtc).IsRequired();

        errorLog.HasIndex(x => x.OccurredAtUtc);
        errorLog.HasIndex(x => new { x.Source, x.Area });
        errorLog.HasIndex(x => x.TraceIdentifier);
        errorLog.ToTable("ErrorLogs");
    }
}
