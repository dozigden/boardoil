using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class BoardMemberConfiguration : IEntityTypeConfiguration<EntityBoardMember>
{
    public void Configure(EntityTypeBuilder<EntityBoardMember> boardMember)
    {
        boardMember.HasKey(x => x.Id);
        boardMember.Property(x => x.Role).IsRequired();
        boardMember.Property(x => x.CreatedAtUtc).IsRequired();
        boardMember.Property(x => x.UpdatedAtUtc).IsRequired();
        boardMember.ToTable("BoardMembers");
        boardMember.HasIndex(x => new { x.BoardId, x.UserId }).IsUnique();
        boardMember.HasIndex(x => new { x.BoardId, x.Role });
    }
}
