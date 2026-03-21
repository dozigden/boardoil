using BoardOil.Abstractions.Tag;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Tag;
using BoardOil.Ef.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class TagRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase(ambientDbContextLocator), ITagRepository
{
    public async Task<IReadOnlyList<TagRecord>> GetAllAsync() =>
        await DbContext.Tags
            .OrderBy(x => x.Name)
            .Select(x => x.ToRecord())
            .ToListAsync();

    public Task<TagRecord?> GetByNormalisedNameAsync(string normalisedName) =>
        DbContext.Tags
            .Where(x => x.NormalisedName == normalisedName)
            .Select(x => x.ToRecord())
            .FirstOrDefaultAsync();

    public void Add(CreateTagRecord tag)
    {
        DbContext.Tags.Add(new EntityTag
        {
            Name = tag.Name,
            NormalisedName = tag.NormalisedName,
            StyleName = tag.StyleName,
            StylePropertiesJson = tag.StylePropertiesJson,
            CreatedAtUtc = tag.CreatedAtUtc,
            UpdatedAtUtc = tag.UpdatedAtUtc
        });
    }

    public async Task<bool> UpdateAsync(UpdateTagRecord tag)
    {
        var entity = await DbContext.Tags.FirstOrDefaultAsync(x => x.NormalisedName == tag.NormalisedName);
        if (entity is null)
        {
            return false;
        }

        entity.Name = tag.Name;
        entity.StyleName = tag.StyleName;
        entity.StylePropertiesJson = tag.StylePropertiesJson;
        entity.UpdatedAtUtc = tag.UpdatedAtUtc;
        return true;
    }

    public async Task<bool> DeleteAsync(string normalisedName)
    {
        var entity = await DbContext.Tags.FirstOrDefaultAsync(x => x.NormalisedName == normalisedName);
        if (entity is null)
        {
            return false;
        }

        DbContext.Tags.Remove(entity);
        return true;
    }
}

file static class TagRepositoryMappings
{
    public static TagRecord ToRecord(this EntityTag tag) =>
        new(
            tag.Name,
            tag.NormalisedName,
            tag.StyleName,
            tag.StylePropertiesJson,
            tag.CreatedAtUtc,
            tag.UpdatedAtUtc);
}
