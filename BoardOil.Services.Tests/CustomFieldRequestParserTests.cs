using System.Text.Json;
using BoardOil.Contracts.CustomField;
using BoardOil.Services.CustomField;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CustomFieldRequestParserTests
{
    private static readonly JsonSerializerOptions InputOptions = new(JsonSerializerDefaults.Web)
    {
        AllowOutOfOrderMetadataProperties = true
    };

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

        var inputs = JsonSerializer.Deserialize<CustomFieldDefinitionInput[]>(json, InputOptions)!;
        var result = CustomFieldRequestParser.Parse(new(Assert.Single(inputs), new AllCardTypes()));

        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        var error = Assert.Single(result.Errors);
        Assert.Equal($"definition.{property}", error.Property);
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void ValidInputs_ShouldProduceTheExistingTypedDefinitions()
    {
        const string json = """
            [
              {"code":"Text","name":"Reference","emoji":"📎","nullable":true,"showOnCard":true,"dataType":"text","defaultValue":"Reference","minLength":1,"maxLength":250},
              {"code":"Points","name":"Estimate","dataType":"integer","defaultValue":0,"min":-2147483648,"max":2147483647},
              {"code":"Due","name":"Due date","defaultValue":"2024-02-29","dataType":"date"}
            ]
            """;

        var results = JsonSerializer.Deserialize<CustomFieldDefinitionInput[]>(json, InputOptions)!.Select(input => CustomFieldRequestParser.Parse(new(input, new AllCardTypes()))).ToArray();

        Assert.All(results, result => Assert.True(result.IsValid));
        Assert.Equal(CustomFieldJsonCodec.DeserialiseDefinitions(json), results.Select(result => result.Value!.Definition));
    }

    [Fact]
    public void MultipleInvalidScalars_ShouldReturnAllOriginalPropertyPaths()
    {
        const string json = """
            [
              {"dataType":"integer","code":"F","name":"Field","defaultValue":"bad","min":1.5,"max":2147483648}
            ]
            """;

        var result = CustomFieldRequestParser.Parse(new(Assert.Single(JsonSerializer.Deserialize<CustomFieldDefinitionInput[]>(json, InputOptions)!), new AllCardTypes()));

        Assert.Null(result.Value);
        Assert.Equal(["definition.defaultValue", "definition.min", "definition.max"],
            result.Errors.Select(error => error.Property));
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

        var results = JsonSerializer.Deserialize<CustomFieldDefinitionInput[]>(json, InputOptions)!.Select(input => CustomFieldRequestParser.Parse(new(input, new AllCardTypes()))).ToArray();

        Assert.All(results, result => Assert.True(result.IsValid));
        Assert.Equal(CustomFieldJsonCodec.DeserialiseDefinitions(json), results.Select(result => result.Value!.Definition));
    }

}
