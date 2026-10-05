using System.Text.Json;
using BoardOil.Contracts.CardField;
using BoardOil.Services.CardField;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardFieldConverterTests
{
    private static TextFieldDefinition Text => new() { FieldCode = "F", DisplayName = "Field", Nullable = true };
    private static IntegerFieldDefinition Integer => new() { FieldCode = "F", DisplayName = "Field", Nullable = true };
    private static DateFieldDefinition Date => new() { FieldCode = "F", DisplayName = "Field", Nullable = true };

    [Fact]
    public void BetweenTypes_ShouldMatchDisplayNamesIgnoringCaseAndEmoji()
    {
        var source = Integer with { FieldCode = "Old", DisplayName = "Effort", Emoji = "📎" };
        var target = Integer with { FieldCode = "New", DisplayName = "EFFORT", Emoji = "📅" };
        var values = Values(5, "Old");

        var result = CardFieldConverter.ConvertBetweenTypes([source], values, [target]);

        Assert.True(result.IsValid);
        Assert.Equal(5, Assert.Single(result.Value!.Values).Value.GetInt32());
        Assert.Contains("New", result.Value.Values.Keys);
        Assert.Empty(result.Value.LostValues);
        Assert.Contains("Old", values.Keys);
    }

    [Fact]
    public void BetweenTypes_ShouldNotMatchCodeWhenDisplayNameDiffers()
    {
        var source = Text with { DisplayName = "Original" };
        var target = Text with { DisplayName = "Different", DefaultValue = "Fallback" };

        var result = CardFieldConverter.ConvertBetweenTypes([source], Values("Original value"), [target]);

        Assert.True(result.IsValid);
        Assert.Equal("Fallback", result.Value!.Values["F"].GetString());
        Assert.Equal(new LostCardFieldValue("Original", "Original value"), Assert.Single(result.Value.LostValues));
    }

    public static TheoryData<CardFieldDefinition, CardFieldDefinition, object?, object?> SuccessfulConversions => new()
    {
        { Text, Text, "value", "value" },
        { Integer, Integer, 0, 0 },
        { Date, Date, "2024-02-29", "2024-02-29" },
        { Integer, Text, int.MinValue, "-2147483648" },
        { Integer, Text, int.MaxValue, "2147483647" },
        { Date, Text, "2024-02-29", "2024-02-29" },
        { Text, Integer, " -12 ", -12 },
        { Text, Integer, "+12", 12 },
        { Text, Integer, "00012", 12 },
        { Text, Integer, "-2147483648", int.MinValue },
        { Text, Integer, "2147483647", int.MaxValue },
        { Text, Integer, null, null },
        { Text, Date, null, null },
        { Integer, Date, null, null }
    };

    [Theory]
    [MemberData(nameof(SuccessfulConversions))]
    public void BetweenTypes_ShouldPreserveConvertibleValues(
        CardFieldDefinition source, CardFieldDefinition target, object? value, object? expected)
    {
        var values = Values(value);

        var result = CardFieldConverter.ConvertBetweenTypes([source], values, [target]);

        Assert.True(result.IsValid);
        Assert.Equal(JsonSerializer.Serialize(expected), result.Value!.Values["F"].GetRawText());
        Assert.Empty(result.Value.LostValues);
    }

    public static TheoryData<CardFieldDefinition, CardFieldDefinition, object, object?> FailedConversions => new()
    {
        { Text, Integer, "no number", null },
        { Text, Integer with { DefaultValue = 7 }, "1.0", 7 },
        { Text, Integer, "1,000", null },
        { Text, Integer, "1e2", null },
        { Text, Integer, "2147483648", null },
        { Text, Integer with { Max = 5, DefaultValue = 2 }, "6", 2 },
        { Text, Date, "2024-02-29", null },
        { Text, Date with { DefaultValue = new DateOnly(2026, 1, 1) }, "2024-02-29", "2026-01-01" },
        { Integer, Date, 20240229, null },
        { Date, Integer, "2024-02-29", null },
        { Integer, Text with { MaxLength = 1, DefaultValue = "X" }, 12, "X" },
        { Date, Text with { MaxLength = 5 }, "2024-02-29", null },
        { Text, Text with { MinLength = 3, DefaultValue = "Unknown" }, "ab", "Unknown" },
        { Text, Text with { MaxLength = 2 }, "abc", null },
        { Integer, Integer with { Min = 0 }, -1, null },
        { Integer, Integer with { Max = 5, Nullable = false, DefaultValue = 0 }, 6, 0 }
    };

    [Theory]
    [MemberData(nameof(FailedConversions))]
    public void BetweenTypes_ShouldUseFallbackAndRecordLossWhenConversionOrValidationFails(
        CardFieldDefinition source, CardFieldDefinition target, object value, object? fallback)
    {
        var values = Values(value);
        var expectedLoss = value.ToString();

        var result = CardFieldConverter.ConvertBetweenTypes([source], values, [target]);

        Assert.True(result.IsValid);
        Assert.Equal(JsonSerializer.Serialize(fallback), result.Value!.Values["F"].GetRawText());
        Assert.Equal(new LostCardFieldValue("Field", expectedLoss!), Assert.Single(result.Value.LostValues));
    }

    [Fact]
    public void BetweenTypes_ShouldInitialiseUnmatchedTargetsWithDefaults()
    {
        CardFieldDefinition[] targets =
        [
            Text with { DefaultValue = "Default" },
            Integer with { FieldCode = "I", DisplayName = "Integer", Nullable = false, DefaultValue = 0 },
            Date with { FieldCode = "D", DisplayName = "Date" }
        ];

        var result = CardFieldConverter.ConvertBetweenTypes([], new Dictionary<string, JsonElement>(), targets);

        Assert.True(result.IsValid);
        Assert.Equal("Default", result.Value!.Values["F"].GetString());
        Assert.Equal(0, result.Value.Values["I"].GetInt32());
        Assert.Equal(JsonValueKind.Null, result.Value.Values["D"].ValueKind);
        Assert.Empty(result.Value.LostValues);
    }

    [Fact]
    public void Conversion_ShouldRecordOnlyPopulatedLossesInDefinitionOrder()
    {
        CardFieldDefinition[] fields =
        [
            Text,
            Integer with { FieldCode = "I", DisplayName = "Integer" },
            Text with { FieldCode = "T", DisplayName = "Text" },
            Date with { FieldCode = "D", DisplayName = "Date" }
        ];
        var values = new Dictionary<string, JsonElement>
        {
            ["D"] = JsonSerializer.SerializeToElement<string?>(null),
            ["T"] = JsonSerializer.SerializeToElement("A useful value"),
            ["I"] = JsonSerializer.SerializeToElement(0),
            ["F"] = JsonSerializer.SerializeToElement("  ")
        };

        var result = CardFieldConverter.ConvertBetweenTypes(fields, values, []);

        Assert.True(result.IsValid);
        Assert.Empty(result.Value!.Values);
        Assert.Equal(new[] { new LostCardFieldValue("Integer", "0"), new LostCardFieldValue("Text", "A useful value") }, result.Value.LostValues);
    }

    [Fact]
    public void DefinitionChanges_ShouldPreserveIdentityByCodeThroughRenamesReorderingAndDefaultChanges()
    {
        var original = Text with { DefaultValue = "Old default" };
        var other = Integer with { FieldCode = "I", DisplayName = "Other" };
        var renamed = original with { FieldCode = "f", DisplayName = "Renamed", DefaultValue = "New default" };
        var values = new Dictionary<string, JsonElement>
        {
            ["F"] = JsonSerializer.SerializeToElement("Existing value"),
            ["I"] = JsonSerializer.SerializeToElement(5)
        };

        var result = CardFieldConverter.ApplyDefinitionChanges([original, other], values, [other, renamed]);

        Assert.True(result.IsValid);
        Assert.Equal("Existing value", result.Value!.Values["f"].GetString());
        Assert.Equal(5, result.Value.Values["I"].GetInt32());
        Assert.Empty(result.Value.LostValues);
        Assert.Equal("Old default", original.DefaultValue);
    }

    [Fact]
    public void DefinitionChanges_ShouldTreatNewCodeAsRemovalAndAdditionDespiteMatchingName()
    {
        var target = Text with { FieldCode = "New", DefaultValue = "Unused" };

        var result = CardFieldConverter.ApplyDefinitionChanges([Text], Values("Old value"), [target]);

        Assert.True(result.IsValid);
        Assert.Equal(JsonValueKind.Null, result.Value!.Values["New"].ValueKind);
        Assert.Equal(new LostCardFieldValue("Field", "Old value"), Assert.Single(result.Value.LostValues));
    }

    [Fact]
    public void DefinitionChanges_ShouldDefaultOnlyNonNullableAdditions()
    {
        CardFieldDefinition[] additions =
        [
            Text with { DefaultValue = "Unused" },
            Integer with { FieldCode = "I", DisplayName = "Integer", Nullable = false, DefaultValue = 7 },
            Date with { FieldCode = "D", DisplayName = "Date", DefaultValue = new DateOnly(2026, 1, 1) }
        ];

        var result = CardFieldConverter.ApplyDefinitionChanges([], new Dictionary<string, JsonElement>(), additions);

        Assert.True(result.IsValid);
        Assert.Equal(JsonValueKind.Null, result.Value!.Values["F"].ValueKind);
        Assert.Equal(7, result.Value.Values["I"].GetInt32());
        Assert.Equal(JsonValueKind.Null, result.Value.Values["D"].ValueKind);
        Assert.Empty(result.Value.LostValues);
    }

    [Theory]
    [InlineData(false, "Required")]
    [InlineData(true, null)]
    public void DefinitionChanges_ShouldFillNullsOnlyWhenBecomingNonNullable(bool nullable, string? expected)
    {
        var target = Text with { Nullable = nullable, DefaultValue = "Required", MinLength = 5 };

        var result = CardFieldConverter.ApplyDefinitionChanges([Text], Values(null), [target]);

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Value!.Values["F"].GetString());
        Assert.Empty(result.Value.LostValues);
    }

    [Theory]
    [InlineData("12", 12, false)]
    [InlineData("101", 0, true)]
    [InlineData("Unparseable", 0, true)]
    public void DefinitionChanges_ShouldConvertKindChanges(string value, int expected, bool lost)
    {
        var target = Integer with { DisplayName = "Renamed", Max = 100, Nullable = false, DefaultValue = 0 };

        var result = CardFieldConverter.ApplyDefinitionChanges([Text], Values(value), [target]);

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Value!.Values["F"].GetInt32());
        Assert.Equal(lost ? 1 : 0, result.Value.LostValues.Count);
        if (lost)
        {
            Assert.Equal(new LostCardFieldValue("Field", value), result.Value.LostValues[0]);
        }
    }

    public static TheoryData<CardFieldDefinition, CardFieldDefinition, object> TightenedLimits => new()
    {
        { Text, Text with { MaxLength = 2, DefaultValue = "OK" }, "Long value" },
        { Text, Text with { MinLength = 3, DefaultValue = "Default" }, "ab" },
        { Integer, Integer with { Min = 0, DefaultValue = 0 }, -1 },
        { Integer, Integer with { Max = 5, DefaultValue = 0 }, 6 }
    };

    [Theory]
    [MemberData(nameof(TightenedLimits))]
    public void DefinitionChanges_ShouldReplaceInvalidValuesAndRecordLosses(
        CardFieldDefinition source, CardFieldDefinition target, object value)
    {
        var removed = Text with { FieldCode = "Removed", DisplayName = "Removed field" };
        var values = new Dictionary<string, JsonElement>
        {
            ["F"] = JsonSerializer.SerializeToElement(value),
            ["Removed"] = JsonSerializer.SerializeToElement("Keep me")
        };
        var originalJson = CardFieldJsonCodec.SerialiseValues(values);

        var result = CardFieldConverter.ApplyDefinitionChanges([removed, source], values, [target]);

        Assert.True(result.IsValid);
        if (target is TextFieldDefinition text) Assert.Equal(text.DefaultValue, result.Value!.Values["F"].GetString());
        else Assert.Equal(0, result.Value!.Values["F"].GetInt32());
        Assert.Equal(2, result.Value.LostValues.Count);
        Assert.Contains(result.Value.LostValues, loss => loss.DisplayName == "Field" && loss.Value == value.ToString());
        Assert.Equal(originalJson, CardFieldJsonCodec.SerialiseValues(values));
    }

    [Fact]
    public void DefinitionChanges_ShouldAcceptTighterLimitsWhenExistingValueRemainsValid()
    {
        var target = Integer with { Min = 0, Max = 5 };

        var result = CardFieldConverter.ApplyDefinitionChanges([Integer], Values(5), [target]);

        Assert.True(result.IsValid);
        Assert.Equal(5, result.Value!.Values["F"].GetInt32());
        Assert.Empty(result.Value.LostValues);
    }

    [Fact]
    public void Conversion_ShouldRejectStaleSourceValuesInsteadOfDiscardingUnknownData()
    {
        var values = Values("Orphan", "Removed");

        var result = CardFieldConverter.ConvertBetweenTypes([], values, [Text]);

        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        Assert.Equal("fields.Removed", Assert.Single(result.Errors).Property);
    }

    private static IReadOnlyDictionary<string, JsonElement> Values(object? value, string code = "F") =>
        new Dictionary<string, JsonElement> { [code] = JsonSerializer.SerializeToElement(value) };
}
