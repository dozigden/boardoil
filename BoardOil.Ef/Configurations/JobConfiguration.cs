using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class JobConfiguration : IEntityTypeConfiguration<EntityJob>
{
    public void Configure(EntityTypeBuilder<EntityJob> job)
    {
        job.HasKey(x => x.Id);
        job.Property(x => x.Type).HasMaxLength(200).IsRequired();
        job.Property(x => x.Status).IsRequired();
        job.Property(x => x.RunAfterUtc).IsRequired();
        job.Property(x => x.PayloadJson).HasMaxLength(32768).HasDefaultValue("{}").IsRequired();
        job.Property(x => x.ResultJson).HasMaxLength(32768).HasDefaultValue("{}").IsRequired();
        job.Property(x => x.ErrorMessage).HasMaxLength(2048);
        job.Property(x => x.CorrelationId).HasMaxLength(128);
        job.Property(x => x.CreatedAtUtc).IsRequired();
        job.Property(x => x.UpdatedAtUtc).IsRequired();

        job.HasIndex(x => new { x.Status, x.RunAfterUtc });
        job.HasIndex(x => x.Type);
        job.HasIndex(x => x.UserId);
        job.HasIndex(x => x.CorrelationId);
        job.HasMany(x => x.Logs)
            .WithOne(x => x.Job)
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);
        job.ToTable("Jobs");
    }
}
