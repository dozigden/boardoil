using System.Text.Json;
using BoardOil.Contracts.CardField;
using BoardOil.Services.CardField;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardFieldDefinitionInputsTests
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
            [{"fieldCode":"F","displayName":"Field","nullable":true,"{{property}}":{{value}},"type":"{{type}}"}]
            """;

        var inputs = CardFieldJsonCodec.DeserialiseDefinitionInputs(json);
        var result = CardFieldDefinitionInputs.Parse(inputs);

        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        var error = Assert.Single(result.Errors);
        Assert.Equal($"fields[0].{property}", error.Property);
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void ValidInputs_ShouldProduceTheExistingDefinitionsAndPreserveOrder()
    {
        const string json = """
            [
              {"fieldCode":"Text","displayName":"Reference","emoji":"📎","nullable":true,"showOnCard":true,"type":"text","defaultValue":"Reference","minLength":1,"maxLength":250},
              {"fieldCode":"Points","displayName":"Estimate","type":"integer","defaultValue":0,"min":-2147483648,"max":2147483647},
              {"fieldCode":"Due","displayName":"Due date","defaultValue":"2024-02-29","type":"date"}
            ]
            """;

        var result = CardFieldDefinitionInputs.Parse(CardFieldJsonCodec.DeserialiseDefinitionInputs(json));

        Assert.True(result.IsValid);
        Assert.Equal(CardFieldJsonCodec.DeserialiseDefinitions(json), result.Value);
    }

    [Fact]
    public void Parsing_ShouldRetainInvalidInputForLaterValidation()
    {
        const string json = """
            [{"fieldCode":"F","displayName":"Field","type":"integer","defaultValue":"1e-400","min":1.5}]
            """;

        var inputs = CardFieldJsonCodec.DeserialiseDefinitionInputs(json);

        var field = Assert.IsType<IntegerFieldDefinitionInput>(Assert.Single(inputs));
        Assert.Equal("1e-400", field.DefaultValue!.Value.GetString());
        Assert.Equal("1.5", field.Min!.Value.GetRawText());
    }

    [Fact]
    public void MultipleInvalidScalars_ShouldReturnAllOriginalPropertyPaths()
    {
        const string json = """
            [
              {"type":"integer","fieldCode":"F","displayName":"Field","defaultValue":"bad","min":1.5,"max":2147483648},
              {"type":"date","fieldCode":"D","displayName":"Date","defaultValue":"bad"}
            ]
            """;

        var result = CardFieldDefinitionInputs.Parse(CardFieldJsonCodec.DeserialiseDefinitionInputs(json));

        Assert.Null(result.Value);
        Assert.Equal(["fields[0].defaultValue", "fields[0].min", "fields[0].max", "fields[1].defaultValue"],
            result.Errors.Select(error => error.Property));
    }

    [Fact]
    public void OmittedFields_ShouldRemainOmitted()
    {
        var result = CardFieldDefinitionInputs.Parse(null);

        Assert.True(result.IsValid);
        Assert.Null(result.Value);
    }

    [Fact]
    public void NullAndOmittedScalars_ShouldRemainUnset()
    {
        const string json = """
            [
              {"type":"integer","fieldCode":"F","displayName":"Field","nullable":true,"defaultValue":null,"min":null},
              {"type":"date","fieldCode":"D","displayName":"Date","nullable":true}
            ]
            """;

        var result = CardFieldDefinitionInputs.Parse(CardFieldJsonCodec.DeserialiseDefinitionInputs(json));

        Assert.True(result.IsValid);
        Assert.Equal(CardFieldJsonCodec.DeserialiseDefinitions(json), result.Value);
    }

    [Theory]
    [InlineData("[")]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("""[{"fieldCode":"F","displayName":"Field"}]""")]
    [InlineData("""[{"fieldCode":"F","displayName":"Field","type":"unknown"}]""")]
    [InlineData("""[{"fieldCode":"F","displayName":"Field","type":"date","maxLength":2}]""")]
    [InlineData("""[{"fieldCode":"F","displayName":"Field","type":"integer","min":1,"min":2}]""")]
    public void MalformedDefinitionShapes_ShouldRemainParsingFailures(string json)
    {
        Assert.Throws<JsonException>(() => CardFieldJsonCodec.DeserialiseDefinitionInputs(json));
    }
}
