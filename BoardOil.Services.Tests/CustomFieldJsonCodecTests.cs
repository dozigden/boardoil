using System.Text.Json;
using BoardOil.Contracts.CustomField;
using BoardOil.Services.CustomField;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CustomFieldJsonCodecTests
{
    [Fact]
    public void Definitions_ShouldRoundTripAllVariantsAndPreserveOrder()
    {
        IReadOnlyList<CustomFieldDefinition> definitions =
        [
            new TextCustomFieldDefinition
            {
                Code = "Reference",
                Name = "Customer reference",
                Emoji = "📎",
                Nullable = true,
                ShowOnCard = true,
                DefaultValue = "Unassigned",
                MinLength = 1,
                MaxLength = 250
            },
            new IntegerCustomFieldDefinition
            {
                Code = "Estimate",
                Name = "Estimated effort",
                DefaultValue = 0,
                Min = 0,
                Max = 100
            },
            new DateCustomFieldDefinition
            {
                Code = "DueDate",
                Name = "Due date",
                DefaultValue = new DateOnly(2026, 11, 1)
            }
        ];

        var json = CustomFieldJsonCodec.SerialiseDefinitions(definitions);
        var restored = CustomFieldJsonCodec.DeserialiseDefinitions(json);

        Assert.Equal(definitions, restored);
        using var document = JsonDocument.Parse(json);
        var fields = document.RootElement;
        Assert.Equal("text", fields[0].GetProperty("dataType").GetString());
        Assert.Equal("Reference", fields[0].GetProperty("code").GetString());
        Assert.Equal("📎", fields[0].GetProperty("emoji").GetString());
        Assert.Equal(250, fields[0].GetProperty("maxLength").GetInt32());
        Assert.False(fields[0].TryGetProperty("max", out _));
        Assert.Equal("integer", fields[1].GetProperty("dataType").GetString());
        Assert.Equal(0, fields[1].GetProperty("defaultValue").GetInt32());
        Assert.False(fields[1].TryGetProperty("maxLength", out _));
        Assert.Equal("date", fields[2].GetProperty("dataType").GetString());
        Assert.Equal("2026-11-01", fields[2].GetProperty("defaultValue").GetString());
        Assert.False(fields[2].TryGetProperty("max", out _));
    }

    [Fact]
    public void DeserialiseDefinitions_ShouldAcceptDiscriminatorAfterRegularProperties()
    {
        const string Json = """
            [{"code":"DueDate","name":"Due date","defaultValue":"2024-02-29","dataType":"date"}]
            """;

        var definitions = CustomFieldJsonCodec.DeserialiseDefinitions(Json);

        var definition = Assert.IsType<DateCustomFieldDefinition>(Assert.Single(definitions));
        Assert.Equal(new DateOnly(2024, 2, 29), definition.DefaultValue);
    }

    [Fact]
    public void Definitions_ShouldPreserveNullDefaultsAndUnsetLimits()
    {
        const string Json = """
            [
              {"dataType":"text","code":"Reference","name":"Reference","nullable":true},
              {"dataType":"integer","code":"Estimate","name":"Estimate","nullable":true},
              {"dataType":"date","code":"DueDate","name":"Due date","nullable":true}
            ]
            """;

        var json = CustomFieldJsonCodec.SerialiseDefinitions(CustomFieldJsonCodec.DeserialiseDefinitions(Json));

        using var document = JsonDocument.Parse(json);
        foreach (var field in document.RootElement.EnumerateArray())
        {
            Assert.Equal(JsonValueKind.Null, field.GetProperty("defaultValue").ValueKind);
            Assert.Equal(JsonValueKind.Null, field.GetProperty("emoji").ValueKind);
            Assert.True(field.GetProperty("nullable").GetBoolean());
            Assert.False(field.GetProperty("showOnCard").GetBoolean());
        }
        Assert.Equal(JsonValueKind.Null, document.RootElement[0].GetProperty("minLength").ValueKind);
        Assert.Equal(JsonValueKind.Null, document.RootElement[0].GetProperty("maxLength").ValueKind);
        Assert.Equal(JsonValueKind.Null, document.RootElement[1].GetProperty("min").ValueKind);
        Assert.Equal(JsonValueKind.Null, document.RootElement[1].GetProperty("max").ValueKind);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[")]
    [InlineData("[null]")]
    [InlineData("[1]")]
    [InlineData("""[{"code":"F","name":"Field"}]""")]
    [InlineData("""[{"dataType":"boolean","code":"F","name":"Field"}]""")]
    [InlineData("""[{"dataType":"text","name":"Field"}]""")]
    [InlineData("""[{"dataType":"text","code":"F"}]""")]
    [InlineData("""[{"dataType":"text","code":null,"name":"Field"}]""")]
    [InlineData("""[{"dataType":"text","code":"F","name":null}]""")]
    [InlineData("""[{"dataType":"text","code":"F","name":"Field","min":1}]""")]
    [InlineData("""[{"dataType":"integer","code":"F","name":"Field","maxLength":5}]""")]
    [InlineData("""[{"dataType":"date","code":"F","name":"Field","max":5}]""")]
    [InlineData("""[{"dataType":"text","code":"F","name":"Field","unexpected":true}]""")]
    [InlineData("""[{"dataType":"text","code":"F","name":"Field","defaultValue":5}]""")]
    [InlineData("""[{"dataType":"integer","code":"F","name":"Field","defaultValue":"5"}]""")]
    [InlineData("""[{"dataType":"integer","code":"F","name":"Field","defaultValue":1.5}]""")]
    [InlineData("""[{"dataType":"integer","code":"F","name":"Field","defaultValue":2147483648}]""")]
    [InlineData("""[{"dataType":"date","code":"F","name":"Field","defaultValue":"2026-02-29"}]""")]
    [InlineData("""[{"dataType":"date","code":"F","name":"Field","defaultValue":"2026-11-01T00:00:00Z"}]""")]
    [InlineData("""[{"dataType":"text","code":"F","name":"Field","name":"Other"}]""")]
    public void DeserialiseDefinitions_ShouldRejectMalformedShapes(string json)
    {
        Assert.Throws<JsonException>(() => CustomFieldJsonCodec.DeserialiseDefinitions(json));
    }

    [Fact]
    public void Values_ShouldRoundTripNativeScalarsAndPreserveFieldCodeCasing()
    {
        const string Json = """
            {"Estimate":5,"DueDate":"2026-11-01","Reference":"ABC-123","Notes":null,"Minimum":-2147483648,"Maximum":2147483647}
            """;

        var values = CustomFieldJsonCodec.DeserialiseValues(Json);
        var json = CustomFieldJsonCodec.SerialiseValues(values);

        Assert.Equal(5, values["estimate"].GetInt32());
        using var document = JsonDocument.Parse(json);
        var fields = document.RootElement;
        Assert.Equal(6, fields.EnumerateObject().Count());
        Assert.Equal(5, fields.GetProperty("Estimate").GetInt32());
        Assert.Equal("2026-11-01", fields.GetProperty("DueDate").GetString());
        Assert.Equal("ABC-123", fields.GetProperty("Reference").GetString());
        Assert.Equal(JsonValueKind.Null, fields.GetProperty("Notes").ValueKind);
        Assert.Equal(int.MinValue, fields.GetProperty("Minimum").GetInt32());
        Assert.Equal(int.MaxValue, fields.GetProperty("Maximum").GetInt32());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("""{"F":true}""")]
    [InlineData("""{"F":[]}""")]
    [InlineData("""{"F":{}}""")]
    [InlineData("""{"F":1.5}""")]
    [InlineData("""{"F":2147483648}""")]
    [InlineData("""{"F":-2147483649}""")]
    [InlineData("""{"F":1,"F":2}""")]
    [InlineData("""{"F":1,"f":2}""")]
    public void DeserialiseValues_ShouldRejectMalformedShapes(string json)
    {
        Assert.Throws<JsonException>(() => CustomFieldJsonCodec.DeserialiseValues(json));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("1.5")]
    public void SerialiseValues_ShouldRejectUnsupportedScalars(string valueJson)
    {
        using var document = JsonDocument.Parse(valueJson);
        IReadOnlyDictionary<string, JsonElement> values = new Dictionary<string, JsonElement>
        {
            ["F"] = document.RootElement
        };

        Assert.Throws<JsonException>(() => CustomFieldJsonCodec.SerialiseValues(values));
    }

    [Fact]
    public void SerialiseValues_ShouldRejectUndefinedElements()
    {
        IReadOnlyDictionary<string, JsonElement> values = new Dictionary<string, JsonElement> { ["F"] = default };

        Assert.Throws<JsonException>(() => CustomFieldJsonCodec.SerialiseValues(values));
    }

    [Fact]
    public void SerialiseDefinitions_ShouldRejectNullEntries()
    {
        IReadOnlyList<CustomFieldDefinition> definitions = [null!];

        Assert.Throws<JsonException>(() => CustomFieldJsonCodec.SerialiseDefinitions(definitions));
    }

    [Fact]
    public void EmptyCollections_ShouldRoundTrip()
    {
        var definitions = CustomFieldJsonCodec.SerialiseDefinitions(CustomFieldJsonCodec.DeserialiseDefinitions("[]"));
        var values = CustomFieldJsonCodec.SerialiseValues(CustomFieldJsonCodec.DeserialiseValues("{}"));

        Assert.Equal("[]", definitions);
        Assert.Equal("{}", values);
    }
}
