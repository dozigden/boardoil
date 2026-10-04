using System.Text.Json;
using BoardOil.Contracts.CardField;
using BoardOil.Services.CardField;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardFieldRulesTests
{
    private static TextFieldDefinition Text => new() { FieldCode = "F", DisplayName = "Field", Nullable = true };
    private static IntegerFieldDefinition Integer => new() { FieldCode = "F", DisplayName = "Field", Nullable = true };
    private static DateFieldDefinition Date => new() { FieldCode = "F", DisplayName = "Field", Nullable = true };

    public static TheoryData<CardFieldDefinition, string, string> DefinitionMessages => new()
    {
        { Text with { FieldCode = "" }, "fieldCode", "Required" },
        { Text with { FieldCode = new string('a', 41) }, "fieldCode", "40 characters maximum" },
        { Text with { FieldCode = "my_field" }, "fieldCode", "Characters A-Z, a-z, 0-9 only" },
        { Text with { DisplayName = " " }, "displayName", "Required" },
        { Text with { DisplayName = new string('a', 41) }, "displayName", "40 characters maximum" },
        { Text with { DisplayName = "Two\nlines" }, "displayName", "Single line only" },
        { Text with { Nullable = false }, "defaultValue", "Required" },
        { Text with { DefaultValue = "a", MinLength = 2 }, "defaultValue", "2 characters minimum" },
        { Text with { DefaultValue = "abc", MaxLength = 2 }, "defaultValue", "2 characters maximum" },
        { Integer with { DefaultValue = 0, Min = 1 }, "defaultValue", "1 minimum" },
        { Integer with { DefaultValue = 2, Max = 1 }, "defaultValue", "1 maximum" }
    };

    [Theory]
    [MemberData(nameof(DefinitionMessages))]
    public void Definitions_ShouldReturnConcisePropertyMessages(CardFieldDefinition field, string property, string message)
    {
        var result = CardFieldRules.NormaliseDefinitions([field]);

        var error = Assert.Single(result.Errors, item => item.Property == $"fields[0].{property}");
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void Definitions_ShouldNormaliseWithoutChangingOriginals()
    {
        var original = Text with
        {
            FieldCode = " Ref1 ", DisplayName = " Customer ref. / ID ", Emoji = " 📎 ",
            DefaultValue = " Unknown ", MinLength = 1, ShowOnCard = true
        };

        var result = CardFieldRules.NormaliseDefinitions([original]);

        Assert.True(result.IsValid);
        var field = Assert.IsType<TextFieldDefinition>(Assert.Single(result.Value!));
        Assert.Equal("Ref1", field.FieldCode);
        Assert.Equal("Customer ref. / ID", field.DisplayName);
        Assert.Equal("📎", field.Emoji);
        Assert.Equal("Unknown", field.DefaultValue);
        Assert.True(field.ShowOnCard);
        Assert.Equal(" Ref1 ", original.FieldCode);
        Assert.Equal(" Unknown ", original.DefaultValue);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void Definitions_ShouldEnforceFieldCount(int count, bool valid)
    {
        var fields = Enumerable.Range(0, count)
            .Select(index => (CardFieldDefinition)(Text with { FieldCode = $"F{index}", DisplayName = $"Field {index}" }))
            .ToArray();

        var result = CardFieldRules.NormaliseDefinitions(fields);

        Assert.Equal(valid, result.IsValid);
        if (valid)
        {
            Assert.Equal(fields, result.Value);
        }
        else
        {
            Assert.Null(result.Value);
        }
    }

    public static TheoryData<CardFieldDefinition, string> InvalidDefinitions => new()
    {
        { Text with { FieldCode = "" }, "fieldCode" },
        { Text with { FieldCode = "KST_Field" }, "fieldCode" },
        { Text with { FieldCode = "my_field" }, "fieldCode" },
        { Text with { FieldCode = "my field" }, "fieldCode" },
        { Text with { FieldCode = "my-field" }, "fieldCode" },
        { Text with { FieldCode = new string('a', 41) }, "fieldCode" },
        { Text with { DisplayName = " " }, "displayName" },
        { Text with { DisplayName = "Two\nlines" }, "displayName" },
        { Text with { DisplayName = new string('a', 41) }, "displayName" },
        { Text with { Emoji = "📎📎" }, "emoji" },
        { Text with { MinLength = -1 }, "minLength" },
        { Text with { MinLength = 251 }, "minLength" },
        { Text with { MaxLength = -1 }, "maxLength" },
        { Text with { MaxLength = 251 }, "maxLength" },
        { Text with { MinLength = 5, MaxLength = 4 }, "maxLength" },
        { Integer with { Min = 5, Max = 4 }, "max" },
        { Text with { Nullable = false }, "defaultValue" },
        { Text with { Nullable = false, DefaultValue = "  " }, "defaultValue" },
        { Integer with { Nullable = false }, "defaultValue" },
        { Date with { Nullable = false }, "defaultValue" },
        { Text with { DefaultValue = "Two\nlines" }, "defaultValue" },
        { Text with { DefaultValue = "a", MinLength = 2 }, "defaultValue" },
        { Text with { DefaultValue = new string('a', 251) }, "defaultValue" },
        { Integer with { DefaultValue = 0, Min = 1 }, "defaultValue" },
        { Integer with { DefaultValue = 2, Max = 1 }, "defaultValue" }
    };

    [Theory]
    [MemberData(nameof(InvalidDefinitions))]
    public void Definitions_ShouldRejectInvalidConfiguration(CardFieldDefinition field, string property)
    {
        var result = CardFieldRules.NormaliseDefinitions([field]);

        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        Assert.Contains(result.Errors, error => error.Property == $"fields[0].{property}");
    }

    [Theory]
    [InlineData("f", "Other", "fieldCode")]
    [InlineData("Other", "field", "displayName")]
    public void Definitions_ShouldRejectDuplicateIdentitiesIgnoringCase(string code, string name, string property)
    {
        var fields = new[] { Text, Text with { FieldCode = code, DisplayName = name } };

        var result = CardFieldRules.NormaliseDefinitions(fields);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Property == $"fields[1].{property}");
    }

    [Fact]
    public void Definitions_ShouldAcceptBoundaryLengthsAndNullDefaultDespiteMinimumLength()
    {
        var field = Text with
        {
            FieldCode = new string('a', 40), DisplayName = new string('b', 40),
            MinLength = 250, MaxLength = 250, DefaultValue = " "
        };

        var result = CardFieldRules.NormaliseDefinitions([field]);

        Assert.True(result.IsValid);
        Assert.Null(Assert.IsType<TextFieldDefinition>(Assert.Single(result.Value!)).DefaultValue);
    }

    [Fact]
    public void CreateValues_ShouldUseDefaultsForAllKindsAndNullForUnsetNullableFields()
    {
        CardFieldDefinition[] fields =
        [
            Text with { FieldCode = "Ref", DisplayName = "Reference", DefaultValue = " Unknown " },
            Integer with { FieldCode = "Points", DisplayName = "Points", Nullable = false, DefaultValue = 0 },
            Date with { FieldCode = "Due", DisplayName = "Due", DefaultValue = new DateOnly(2024, 2, 29) },
            Text with { FieldCode = "Note", DisplayName = "Note" }
        ];

        var result = CardFieldRules.CreateValues(fields);

        Assert.True(result.IsValid);
        Assert.Equal(4, result.Value!.Count);
        Assert.Equal("Unknown", result.Value["Ref"].GetString());
        Assert.Equal(0, result.Value["Points"].GetInt32());
        Assert.Equal("2024-02-29", result.Value["Due"].GetString());
        Assert.Equal(JsonValueKind.Null, result.Value["Note"].ValueKind);
    }

    [Fact]
    public void CreateValues_ShouldAcceptPartialOverridesIncludingExplicitNull()
    {
        CardFieldDefinition[] fields =
        [
            Text with { DefaultValue = "Default" },
            Integer with { FieldCode = "I", DisplayName = "Integer", DefaultValue = 7 }
        ];
        var supplied = Values(null);

        var result = CardFieldRules.CreateValues(fields, supplied);

        Assert.True(result.IsValid);
        Assert.Equal(JsonValueKind.Null, result.Value!["F"].ValueKind);
        Assert.Equal(7, result.Value["I"].GetInt32());
        Assert.Single(supplied);
    }

    [Theory]
    [InlineData("  hello  ", "hello")]
    [InlineData("", null)]
    [InlineData(" \t ", null)]
    [InlineData(null, null)]
    public void Values_ShouldNormaliseTextAndAllowExplicitClearing(string? value, string? expected)
    {
        var field = Text with { MinLength = 5, DefaultValue = "Default" };
        var supplied = Values(value);

        var result = CardFieldRules.NormaliseValues([field], supplied);

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Value!["F"].GetString());
        Assert.Equal(value, supplied["F"].GetString());
    }

    public static TheoryData<CardFieldDefinition, object?> InvalidValues => new()
    {
        { Text, 1 },
        { Text, true },
        { Text, new[] { "a" } },
        { Text, new { value = "a" } },
        { Text, "one\ntwo" },
        { Text, "one\rtwo" },
        { Text, "one\u2028two" },
        { Text, new string('a', 251) },
        { Text with { MinLength = 3 }, "ab" },
        { Text with { MaxLength = 2 }, "abc" },
        { Text with { Nullable = false, DefaultValue = "Default" }, " " },
        { Integer, "12" },
        { Integer, 1.5 },
        { Integer, 2147483648L },
        { Integer, -2147483649L },
        { Integer with { Min = 0 }, -1 },
        { Integer with { Max = 0 }, 1 },
        { Integer with { Nullable = false, DefaultValue = 0 }, null },
        { Date, "2025-02-29" },
        { Date, "2024-2-9" },
        { Date, "2024-02-29T00:00:00Z" },
        { Date, " 2024-02-29 " },
        { Date, 20240229 }
    };

    [Theory]
    [MemberData(nameof(InvalidValues))]
    public void Values_ShouldRejectInvalidDirectEditsWithoutUsingDefaults(CardFieldDefinition field, object? value)
    {
        var supplied = Values(value);

        var result = CardFieldRules.NormaliseValues([field], supplied);

        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        Assert.Equal("fields.F", Assert.Single(result.Errors).Property);
    }

    public static TheoryData<CardFieldDefinition, object?> BoundaryValues => new()
    {
        { Text, new string('a', 250) },
        { Text with { MinLength = 2, MaxLength = 2 }, "ab" },
        { Text with { MaxLength = 0 }, null },
        { Integer, int.MinValue },
        { Integer, int.MaxValue },
        { Integer with { Min = 0, Max = 0 }, 0 },
        { Date, "2024-02-29" },
        { Date, "0001-01-01" },
        { Date, "9999-12-31" }
    };

    [Theory]
    [MemberData(nameof(BoundaryValues))]
    public void Values_ShouldAcceptInclusiveBoundaries(CardFieldDefinition field, object? value)
    {
        var supplied = Values(value);

        var result = CardFieldRules.NormaliseValues([field], supplied);

        Assert.True(result.IsValid);
        Assert.Equal(supplied["F"].GetRawText(), result.Value!["F"].GetRawText());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"F\":null,\"Unknown\":null}")]
    [InlineData("{\"F\":null,\"f\":null}")]
    public void Values_ShouldRejectMissingUnknownOrDuplicateCodes(string json)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

        var result = CardFieldRules.NormaliseValues([Text], values);

        Assert.False(result.IsValid);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Values_ShouldUseCanonicalCodeCasing()
    {
        var supplied = new Dictionary<string, JsonElement> { ["f"] = JsonSerializer.SerializeToElement("value") };

        var result = CardFieldRules.NormaliseValues([Text], supplied);

        Assert.True(result.IsValid);
        Assert.Equal("F", Assert.Single(result.Value!).Key);
    }

    private static IReadOnlyDictionary<string, JsonElement> Values(object? value) =>
        new Dictionary<string, JsonElement> { ["F"] = JsonSerializer.SerializeToElement(value) };
}
