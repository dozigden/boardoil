using BoardOil.Ef;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class EfEntityConventionTests
{
    private static readonly string PersistenceEntitiesNamespace = typeof(EntityTag).Namespace!;
    private const string PersistenceEntityPrefix = "Entity";

    [Fact]
    public void EfEntities_ShouldUseEntityPrefix()
    {
        var efEntityTypeNames = GetEfEntityTypeNames();
        Assert.NotEmpty(efEntityTypeNames);
        Assert.All(efEntityTypeNames, name => Assert.StartsWith("Entity", name, StringComparison.Ordinal));
    }

    [Fact]
    public void EfEntities_ShouldMapToNonEntityTableNames()
    {
        using var context = CreateDbContext();

        var efEntityMappings = context.Model.GetEntityTypes()
            .Where(x => x.ClrType.Namespace == PersistenceEntitiesNamespace)
            .Select(x => new
            {
                EntityTypeName = x.ClrType.Name,
                TableName = x.GetTableName()
            })
            .ToList();

        Assert.NotEmpty(efEntityMappings);

        Assert.All(efEntityMappings, mapping =>
        {
            Assert.False(string.IsNullOrWhiteSpace(mapping.TableName));
            Assert.False(mapping.TableName!.StartsWith("Entity", StringComparison.Ordinal));
            Assert.NotEqual(mapping.EntityTypeName, mapping.TableName);
        });
    }

    [Fact]
    public void AllMappedTables_ShouldUseEntityTypesFromPersistenceNamespace()
    {
        using var context = CreateDbContext();

        var tableMappings = context.Model.GetEntityTypes()
            .Where(x => !string.IsNullOrWhiteSpace(x.GetTableName()))
            .Select(x => new
            {
                ClrTypeName = x.ClrType.Name,
                ClrTypeNamespace = x.ClrType.Namespace,
                TableName = x.GetTableName()
            })
            .ToList();

        Assert.NotEmpty(tableMappings);

        foreach (var mapping in tableMappings)
        {
            Assert.True(
                string.Equals(mapping.ClrTypeNamespace, PersistenceEntitiesNamespace, StringComparison.Ordinal),
                $"Table '{mapping.TableName}' maps to '{mapping.ClrTypeNamespace}.{mapping.ClrTypeName}', not '{PersistenceEntitiesNamespace}'.");

            Assert.True(
                mapping.ClrTypeName.StartsWith(PersistenceEntityPrefix, StringComparison.Ordinal),
                $"Table '{mapping.TableName}' maps to '{mapping.ClrTypeName}', which does not start with '{PersistenceEntityPrefix}'.");
        }
    }

    private static IReadOnlyList<string> GetEfEntityTypeNames() =>
        typeof(EntityTag).Assembly.GetTypes()
            .Where(x => x.IsClass && !x.IsAbstract && x.Namespace == PersistenceEntitiesNamespace)
            .Select(x => x.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

    private static BoardOilDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        return new BoardOilDbContext(options);
    }
}
