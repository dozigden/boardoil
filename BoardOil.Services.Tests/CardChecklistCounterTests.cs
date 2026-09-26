using System.Text.Json;
using BoardOil.Services.Card;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardChecklistCounterTests
{
    public static IEnumerable<object[]> CompatibilityCases()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "card-checklist-counts.json"));
        var cases = JsonSerializer.Deserialize<List<CompatibilityCase>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return cases.Select(test => new object[] { test.Name, test.Description, test.Completed, test.Total });
    }

    [Theory]
    [MemberData(nameof(CompatibilityCases))]
    public void Count_ShouldMatchEditor(string name, string description, int completed, int total)
    {
        var result = CardChecklistCounter.Count(description);

        Assert.True(result == new ChecklistCounts(completed, total), $"{name}: expected {completed}/{total}, received {result.Completed}/{result.Total}");
    }

    [Fact]
    public void Count_ShouldHandleMaximumLengthDescription()
    {
        var description = string.Concat(Enumerable.Repeat("- [x] Task\n", 2000));

        var result = CardChecklistCounter.Count(description);

        Assert.Equal(new ChecklistCounts(2000, 2000), result);
    }

    private sealed record CompatibilityCase(string Name, string Description, int Completed, int Total);
}
