using System.Text.Json;
using BoardOil.Contracts.CustomField;
using BoardOil.Services.CustomField;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CustomFieldDefinitionInputsTests
{
    [Theory]
    [InlineData("integer", "defaultValue", "\"abc\"", "Whole number required")]
    [InlineData("integer", "defaultValue", "\"5\"", "Whole number required")]
    [InlineData("integer", "defaultValue", "1.5", "Whole number required")]
    [InlineData("integer", "min", "2147483648", "2147483647 maximum")]
    [InlineData("integer", "max", "-2147483649", "-2147483648 minimum")]
    [InlineData("integer", "max", "1e400", "2147483647 maximum")]
    [InlineData("text", "minLength", "true", "Whole number required")]
    [InlineData("text", "maxLength", "[]", "Whole number required")]
    [InlineData("text", "maxLength", "{}", "Whole number required")]
    [InlineData("date", "defaultValue", "\"2026-02-30\"", "Date in YYYY-MM-DD format required")]
    [InlineData("date", "defaultValue", "\"2026-10-06T00:00:00Z\"", "Date in YYYY-MM-DD format required")]
    [InlineData("date", "defaultValue", "1", "Date in YYYY-MM-DD format required")]
    [InlineData("date", "defaultValue", "{}", "Date in YYYY-MM-DD format required")]
    public void InvalidScalars_ShouldReturnValidationWithoutThrowingOrCoercing(
        string type, string property, string value, string message)
    {
        var json = $$"""
            [{"code":"F","name":"Field","nullable":true,"{{property}}":{{value}},"dataType":"{{type}}"}]
            """;

        var inputs = CustomFieldJsonCodec.DeserialiseDefinitionInputs(json);
        var result = CustomFieldDefinitionInputs.Parse(inputs);

        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        var error = Assert.Single(result.Errors);
        Assert.Equal($"customFields[0].{property}", error.Property);
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void ValidInputs_ShouldProduceTheExistingDefinitionsAndPreserveOrder()
    {
        const string json = """
            [
              {"code":"Text","name":"Reference","emoji":"📎","nullable":true,"showOnCard":true,"dataType":"text","defaultValue":"Reference","minLength":1,"maxLength":250},
              {"code":"Points","name":"Estimate","dataType":"integer","defaultValue":0,"min":-2147483648,"max":2147483647},
              {"code":"Due","name":"Due date","defaultValue":"2024-02-29","dataType":"date"}
            ]
            """;

        var result = CustomFieldDefinitionInputs.Parse(CustomFieldJsonCodec.DeserialiseDefinitionInputs(json));

        Assert.True(result.IsValid);
        Assert.Equal(CustomFieldJsonCodec.DeserialiseDefinitions(json), result.Value);
    }

    [Fact]
    public void Parsing_ShouldRetainInvalidInputForLaterValidation()
    {
        const string json = """
            [{"code":"F","name":"Field","dataType":"integer","defaultValue":"1e-400","min":1.5}]
            """;

        var inputs = CustomFieldJsonCodec.DeserialiseDefinitionInputs(json);

        var field = Assert.IsType<IntegerCustomFieldDefinitionInput>(Assert.Single(inputs));
        Assert.Equal("1e-400", field.DefaultValue!.Value.GetString());
        Assert.Equal("1.5", field.Min!.Value.GetRawText());
    }

    [Fact]
    public void MultipleInvalidScalars_ShouldReturnAllOriginalPropertyPaths()
    {
        const string json = """
            [
              {"dataType":"integer","code":"F","name":"Field","defaultValue":"bad","min":1.5,"max":2147483648},
              {"dataType":"date","code":"D","name":"Date","defaultValue":"bad"}
            ]
            """;

        var result = CustomFieldDefinitionInputs.Parse(CustomFieldJsonCodec.DeserialiseDefinitionInputs(json));

        Assert.Null(result.Value);
        Assert.Equal(["customFields[0].defaultValue", "customFields[0].min", "customFields[0].max", "customFields[1].defaultValue"],
            result.Errors.Select(error => error.Property));
    }

    [Fact]
    public void OmittedFields_ShouldRemainOmitted()
    {
        var result = CustomFieldDefinitionInputs.Parse(null);

        Assert.True(result.IsValid);
        Assert.Null(result.Value);
    }

    [Fact]
    public void NullAndOmittedScalars_ShouldRemainUnset()
    {
        const string json = """
            [
              {"dataType":"integer","code":"F","name":"Field","nullable":true,"defaultValue":null,"min":null},
              {"dataType":"date","code":"D","name":"Date","nullable":true}
            ]
            """;

        var result = CustomFieldDefinitionInputs.Parse(CustomFieldJsonCodec.DeserialiseDefinitionInputs(json));

        Assert.True(result.IsValid);
        Assert.Equal(CustomFieldJsonCodec.DeserialiseDefinitions(json), result.Value);
    }

    [Theory]
    [InlineData("[")]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("""[{"code":"F","name":"Field"}]""")]
    [InlineData("""[{"code":"F","name":"Field","dataType":"unknown"}]""")]
    [InlineData("""[{"code":"F","name":"Field","dataType":"date","maxLength":2}]""")]
    [InlineData("""[{"code":"F","name":"Field","dataType":"integer","min":1,"min":2}]""")]
    public void MalformedDefinitionShapes_ShouldRemainParsingFailures(string json)
    {
        Assert.Throws<JsonException>(() => CustomFieldJsonCodec.DeserialiseDefinitionInputs(json));
    }
}
