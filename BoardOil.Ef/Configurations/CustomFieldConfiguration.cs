using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class CustomFieldConfiguration : IEntityTypeConfiguration<EntityCustomField>
{
    public void Configure(EntityTypeBuilder<EntityCustomField> field)
    {
        field.ToTable("CustomFields");
        field.HasKey(x => x.Id);
        field.HasAlternateKey(x => new { x.BoardId, x.Id });
        field.Property(x => x.Code).IsRequired();
        field.Property(x => x.Name).IsRequired();
        field.Property(x => x.NormalisedCode).IsRequired();
        field.Property(x => x.NormalisedName).IsRequired();
        field.Property(x => x.DataType).HasConversion<string>().IsRequired();
        field.Property(x => x.ConfigurationJson).IsRequired();
        field.HasIndex(x => new { x.BoardId, x.NormalisedCode }).IsUnique();
        field.HasIndex(x => new { x.BoardId, x.NormalisedName }).IsUnique();
    }
}

public sealed class CustomFieldCardTypeConfiguration : IEntityTypeConfiguration<EntityCustomFieldCardType>
{
    public void Configure(EntityTypeBuilder<EntityCustomFieldCardType> assignment)
    {
        assignment.ToTable("CustomFieldCardTypes");
        assignment.HasKey(x => x.Id);
        assignment.HasIndex(x => new { x.CustomFieldId, x.CardTypeId }).IsUnique();
        assignment.HasOne(x => x.CustomField).WithMany(x => x.CardTypeAssignments)
            .HasForeignKey(x => new { x.BoardId, x.CustomFieldId })
            .HasPrincipalKey(x => new { x.BoardId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        assignment.HasOne(x => x.CardType).WithMany()
            .HasForeignKey(x => new { x.BoardId, x.CardTypeId })
            .HasPrincipalKey(x => new { x.BoardId, x.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}
