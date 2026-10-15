using System.Text.Json;
using BoardOil.Contracts.CustomField;
using BoardOil.Services.CustomField;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CustomFieldValidatorTests
{
    private static TextCustomFieldDefinition Text => new() { Code = "F", Name = "Field", Nullable = true };
    private static IntegerCustomFieldDefinition Integer => new() { Code = "F", Name = "Field", Nullable = true };
    private static DateCustomFieldDefinition Date => new() { Code = "F", Name = "Field", Nullable = true };

    public static TheoryData<CustomFieldDefinition, string, string> DefinitionMessages => new()
    {
        { Text with { Code = "" }, "code", "Required" },
        { Text with { Code = new string('a', 41) }, "code", "40 characters maximum" },
        { Text with { Code = "my_field" }, "code", "Characters A-Z, a-z, 0-9 only" },
        { Text with { Name = " " }, "name", "Required" },
        { Text with { Name = new string('a', 41) }, "name", "40 characters maximum" },
        { Text with { Name = "Two\nlines" }, "name", "Single line only" },
        { Text with { Nullable = false }, "defaultValue", "Required" },
        { Text with { DefaultValue = "a", MinLength = 2 }, "defaultValue", "2 characters minimum" },
        { Text with { DefaultValue = "abc", MaxLength = 2 }, "defaultValue", "2 characters maximum" },
        { Integer with { DefaultValue = 0, Min = 1 }, "defaultValue", "1 minimum" },
        { Integer with { DefaultValue = 2, Max = 1 }, "defaultValue", "1 maximum" }
    };

    [Theory]
    [MemberData(nameof(DefinitionMessages))]
    public void Definitions_ShouldReturnConcisePropertyMessages(CustomFieldDefinition field, string property, string message)
    {
        var result = CustomFieldValidator.ValidateAndNormaliseDefinitions([field]);

        var error = Assert.Single(result.Errors, item => item.Property == $"customFields[0].{property}");
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void Definitions_ShouldNormaliseWithoutChangingOriginals()
    {
        var original = Text with
        {
            Code = " Ref1 ", Name = " Customer ref. / ID ", Emoji = " 📎 ",
            DefaultValue = " Unknown ", MinLength = 1, ShowOnCard = true
        };

        var result = CustomFieldValidator.ValidateAndNormaliseDefinitions([original]);

        Assert.True(result.IsValid);
        var field = Assert.IsType<TextCustomFieldDefinition>(Assert.Single(result.Value!));
        Assert.Equal("Ref1", field.Code);
        Assert.Equal("Customer ref. / ID", field.Name);
        Assert.Equal("📎", field.Emoji);
        Assert.Equal("Unknown", field.DefaultValue);
        Assert.True(field.ShowOnCard);
        Assert.Equal(" Ref1 ", original.Code);
        Assert.Equal(" Unknown ", original.DefaultValue);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void Definitions_ShouldEnforceFieldCount(int count, bool valid)
    {
        var fields = Enumerable.Range(0, count)
            .Select(index => (CustomFieldDefinition)(Text with { Code = $"F{index}", Name = $"Field {index}" }))
            .ToArray();

        var result = CustomFieldValidator.ValidateAndNormaliseDefinitions(fields);

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

    public static TheoryData<CustomFieldDefinition, string> InvalidDefinitions => new()
    {
        { Text with { Code = "" }, "code" },
        { Text with { Code = "KST_Field" }, "code" },
        { Text with { Code = "my_field" }, "code" },
        { Text with { Code = "my field" }, "code" },
        { Text with { Code = "my-field" }, "code" },
        { Text with { Code = new string('a', 41) }, "code" },
        { Text with { Name = " " }, "name" },
        { Text with { Name = "Two\nlines" }, "name" },
        { Text with { Name = new string('a', 41) }, "name" },
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
    public void Definitions_ShouldRejectInvalidConfiguration(CustomFieldDefinition field, string property)
    {
        var result = CustomFieldValidator.ValidateAndNormaliseDefinitions([field]);

        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        Assert.Contains(result.Errors, error => error.Property == $"customFields[0].{property}");
    }

    [Theory]
    [InlineData("f", "Other", "code")]
    [InlineData("Other", "field", "name")]
    public void Definitions_ShouldRejectDuplicateIdentitiesIgnoringCase(string code, string name, string property)
    {
        var fields = new[] { Text, Text with { Code = code, Name = name } };

        var result = CustomFieldValidator.ValidateAndNormaliseDefinitions(fields);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Property == $"customFields[1].{property}");
    }

    [Fact]
    public void Definitions_ShouldAcceptBoundaryLengthsAndNullDefaultDespiteMinimumLength()
    {
        var field = Text with
        {
            Code = new string('a', 40), Name = new string('b', 40),
            MinLength = 250, MaxLength = 250, DefaultValue = " "
        };

        var result = CustomFieldValidator.ValidateAndNormaliseDefinitions([field]);

        Assert.True(result.IsValid);
        Assert.Null(Assert.IsType<TextCustomFieldDefinition>(Assert.Single(result.Value!)).DefaultValue);
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

        var result = CustomFieldValidator.ValidateAndNormaliseValues([field], supplied);

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Value!["F"].GetString());
        Assert.Equal(value, supplied["F"].GetString());
    }

    public static TheoryData<CustomFieldDefinition, object?> InvalidValues => new()
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
    public void Values_ShouldRejectInvalidDirectEditsWithoutUsingDefaults(CustomFieldDefinition field, object? value)
    {
        var supplied = Values(value);

        var result = CustomFieldValidator.ValidateAndNormaliseValues([field], supplied);

        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        Assert.Equal("customFieldValues.F", Assert.Single(result.Errors).Property);
    }

    public static TheoryData<CustomFieldDefinition, object?> BoundaryValues => new()
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
    public void Values_ShouldAcceptInclusiveBoundaries(CustomFieldDefinition field, object? value)
    {
        var supplied = Values(value);

        var result = CustomFieldValidator.ValidateAndNormaliseValues([field], supplied);

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

        var result = CustomFieldValidator.ValidateAndNormaliseValues([Text], values);

        Assert.False(result.IsValid);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Values_ShouldUseCanonicalCodeCasing()
    {
        var supplied = new Dictionary<string, JsonElement> { ["f"] = JsonSerializer.SerializeToElement("value") };

        var result = CustomFieldValidator.ValidateAndNormaliseValues([Text], supplied);

        Assert.True(result.IsValid);
        Assert.Equal("F", Assert.Single(result.Value!).Key);
    }

    private static IReadOnlyDictionary<string, JsonElement> Values(object? value) =>
        new Dictionary<string, JsonElement> { ["F"] = JsonSerializer.SerializeToElement(value) };
}
