using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class BoardCardIdSequenceConfiguration : IEntityTypeConfiguration<EntityBoardCardIdSequence>
{
    public void Configure(EntityTypeBuilder<EntityBoardCardIdSequence> sequence)
    {
        sequence.HasKey(x => x.Id);
        sequence.Property(x => x.BoardId).IsRequired();
        sequence.Property(x => x.NextCardId).IsRequired();
        sequence.ToTable("BoardCardIdSequences");
        sequence.HasIndex(x => x.BoardId).IsUnique();
    }
}
