using BoardOil.Abstractions.Tag;
using BoardOil.Contracts.Tag;
using BoardOil.Ef.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class TagRepository(BoardOilDbContext dbContext) : ITagRepository
{
    public async Task<IReadOnlyList<TagRecord>> GetAllAsync() =>
        await dbContext.Tags
            .OrderBy(x => x.Name)
            .Select(x => x.ToRecord())
            .ToListAsync();

    public Task<TagRecord?> GetByNormalisedNameAsync(string normalisedName) =>
        dbContext.Tags
            .Where(x => x.NormalisedName == normalisedName)
            .Select(x => x.ToRecord())
            .FirstOrDefaultAsync();

    public async Task<TagRecord> CreateAsync(CreateTagRecord tag)
    {
        var entity = dbContext.Tags.Add(new Tag
        {
            Name = tag.Name,
            NormalisedName = tag.NormalisedName,
            StyleName = tag.StyleName,
            StylePropertiesJson = tag.StylePropertiesJson,
            CreatedAtUtc = tag.CreatedAtUtc,
            UpdatedAtUtc = tag.UpdatedAtUtc
        }).Entity;

        await dbContext.SaveChangesAsync();
        return entity.ToRecord();
    }

    public async Task<bool> UpdateAsync(UpdateTagRecord tag)
    {
        var entity = await dbContext.Tags.FirstOrDefaultAsync(x => x.NormalisedName == tag.NormalisedName);
        if (entity is null)
        {
            return false;
        }

        entity.Name = tag.Name;
        entity.StyleName = tag.StyleName;
        entity.StylePropertiesJson = tag.StylePropertiesJson;
        entity.UpdatedAtUtc = tag.UpdatedAtUtc;
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(string normalisedName)
    {
        var entity = await dbContext.Tags.FirstOrDefaultAsync(x => x.NormalisedName == normalisedName);
        if (entity is null)
        {
            return false;
        }

        dbContext.Tags.Remove(entity);
        await dbContext.SaveChangesAsync();
        return true;
    }
}

file static class TagRepositoryMappings
{
    public static TagRecord ToRecord(this Tag tag) =>
        new(
            tag.Name,
            tag.NormalisedName,
            tag.StyleName,
            tag.StylePropertiesJson,
            tag.CreatedAtUtc,
            tag.UpdatedAtUtc);
}
