using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class ScheduledJobSchedulerStateConfiguration : IEntityTypeConfiguration<EntityScheduledJobSchedulerState>
{
    public void Configure(EntityTypeBuilder<EntityScheduledJobSchedulerState> state)
    {
        state.HasKey(x => x.Id);
        state.Property(x => x.Name).HasMaxLength(120).IsRequired();
        state.Property(x => x.LastRunTimeUtc).IsRequired();
        state.Property(x => x.PendingDueAtUtc);
        state.Property(x => x.CreatedAtUtc).IsRequired();
        state.Property(x => x.UpdatedAtUtc).IsRequired();
        state.HasIndex(x => x.Name).IsUnique();
        state.ToTable("ScheduledJobSchedulerStates");
    }
}
