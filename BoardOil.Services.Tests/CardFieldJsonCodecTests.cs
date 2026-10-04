using System.Text.Json;
using BoardOil.Contracts.CardField;
using BoardOil.Services.CardField;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardFieldJsonCodecTests
{
    [Fact]
    public void Definitions_ShouldRoundTripAllVariantsAndPreserveOrder()
    {
        IReadOnlyList<CardFieldDefinition> definitions =
        [
            new TextFieldDefinition
            {
                FieldCode = "Reference",
                DisplayName = "Customer reference",
                Emoji = "📎",
                Nullable = true,
                ShowOnCard = true,
                DefaultValue = "Unassigned",
                MinLength = 1,
                MaxLength = 250
            },
            new IntegerFieldDefinition
            {
                FieldCode = "Estimate",
                DisplayName = "Estimated effort",
                DefaultValue = 0,
                Min = 0,
                Max = 100
            },
            new DateFieldDefinition
            {
                FieldCode = "DueDate",
                DisplayName = "Due date",
                DefaultValue = new DateOnly(2026, 11, 1)
            }
        ];

        var json = CardFieldJsonCodec.SerialiseDefinitions(definitions);
        var restored = CardFieldJsonCodec.DeserialiseDefinitions(json);

        Assert.Equal(definitions, restored);
        using var document = JsonDocument.Parse(json);
        var fields = document.RootElement;
        Assert.Equal("text", fields[0].GetProperty("type").GetString());
        Assert.Equal("Reference", fields[0].GetProperty("fieldCode").GetString());
        Assert.Equal("📎", fields[0].GetProperty("emoji").GetString());
        Assert.Equal(250, fields[0].GetProperty("maxLength").GetInt32());
        Assert.False(fields[0].TryGetProperty("max", out _));
        Assert.Equal("integer", fields[1].GetProperty("type").GetString());
        Assert.Equal(0, fields[1].GetProperty("defaultValue").GetInt32());
        Assert.False(fields[1].TryGetProperty("maxLength", out _));
        Assert.Equal("date", fields[2].GetProperty("type").GetString());
        Assert.Equal("2026-11-01", fields[2].GetProperty("defaultValue").GetString());
        Assert.False(fields[2].TryGetProperty("max", out _));
    }

    [Fact]
    public void DeserialiseDefinitions_ShouldAcceptDiscriminatorAfterRegularProperties()
    {
        const string Json = """
            [{"fieldCode":"DueDate","displayName":"Due date","defaultValue":"2024-02-29","type":"date"}]
            """;

        var definitions = CardFieldJsonCodec.DeserialiseDefinitions(Json);

        var definition = Assert.IsType<DateFieldDefinition>(Assert.Single(definitions));
        Assert.Equal(new DateOnly(2024, 2, 29), definition.DefaultValue);
    }

    [Fact]
    public void Definitions_ShouldPreserveNullDefaultsAndUnsetLimits()
    {
        const string Json = """
            [
              {"type":"text","fieldCode":"Reference","displayName":"Reference","nullable":true},
              {"type":"integer","fieldCode":"Estimate","displayName":"Estimate","nullable":true},
              {"type":"date","fieldCode":"DueDate","displayName":"Due date","nullable":true}
            ]
            """;

        var json = CardFieldJsonCodec.SerialiseDefinitions(CardFieldJsonCodec.DeserialiseDefinitions(Json));

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
    [InlineData("""[{"fieldCode":"F","displayName":"Field"}]""")]
    [InlineData("""[{"type":"boolean","fieldCode":"F","displayName":"Field"}]""")]
    [InlineData("""[{"type":"text","displayName":"Field"}]""")]
    [InlineData("""[{"type":"text","fieldCode":"F"}]""")]
    [InlineData("""[{"type":"text","fieldCode":null,"displayName":"Field"}]""")]
    [InlineData("""[{"type":"text","fieldCode":"F","displayName":null}]""")]
    [InlineData("""[{"type":"text","fieldCode":"F","displayName":"Field","min":1}]""")]
    [InlineData("""[{"type":"integer","fieldCode":"F","displayName":"Field","maxLength":5}]""")]
    [InlineData("""[{"type":"date","fieldCode":"F","displayName":"Field","max":5}]""")]
    [InlineData("""[{"type":"text","fieldCode":"F","displayName":"Field","unexpected":true}]""")]
    [InlineData("""[{"type":"text","fieldCode":"F","displayName":"Field","defaultValue":5}]""")]
    [InlineData("""[{"type":"integer","fieldCode":"F","displayName":"Field","defaultValue":"5"}]""")]
    [InlineData("""[{"type":"integer","fieldCode":"F","displayName":"Field","defaultValue":1.5}]""")]
    [InlineData("""[{"type":"integer","fieldCode":"F","displayName":"Field","defaultValue":2147483648}]""")]
    [InlineData("""[{"type":"date","fieldCode":"F","displayName":"Field","defaultValue":"2026-02-29"}]""")]
    [InlineData("""[{"type":"date","fieldCode":"F","displayName":"Field","defaultValue":"2026-11-01T00:00:00Z"}]""")]
    [InlineData("""[{"type":"text","fieldCode":"F","displayName":"Field","displayName":"Other"}]""")]
    public void DeserialiseDefinitions_ShouldRejectMalformedShapes(string json)
    {
        Assert.Throws<JsonException>(() => CardFieldJsonCodec.DeserialiseDefinitions(json));
    }

    [Fact]
    public void Values_ShouldRoundTripNativeScalarsAndPreserveFieldCodeCasing()
    {
        const string Json = """
            {"Estimate":5,"DueDate":"2026-11-01","Reference":"ABC-123","Notes":null,"Minimum":-2147483648,"Maximum":2147483647}
            """;

        var values = CardFieldJsonCodec.DeserialiseValues(Json);
        var json = CardFieldJsonCodec.SerialiseValues(values);

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
        Assert.Throws<JsonException>(() => CardFieldJsonCodec.DeserialiseValues(json));
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

        Assert.Throws<JsonException>(() => CardFieldJsonCodec.SerialiseValues(values));
    }

    [Fact]
    public void SerialiseValues_ShouldRejectUndefinedElements()
    {
        IReadOnlyDictionary<string, JsonElement> values = new Dictionary<string, JsonElement> { ["F"] = default };

        Assert.Throws<JsonException>(() => CardFieldJsonCodec.SerialiseValues(values));
    }

    [Fact]
    public void SerialiseDefinitions_ShouldRejectNullEntries()
    {
        IReadOnlyList<CardFieldDefinition> definitions = [null!];

        Assert.Throws<JsonException>(() => CardFieldJsonCodec.SerialiseDefinitions(definitions));
    }

    [Fact]
    public void EmptyCollections_ShouldRoundTrip()
    {
        var definitions = CardFieldJsonCodec.SerialiseDefinitions(CardFieldJsonCodec.DeserialiseDefinitions("[]"));
        var values = CardFieldJsonCodec.SerialiseValues(CardFieldJsonCodec.DeserialiseValues("{}"));

        Assert.Equal("[]", definitions);
        Assert.Equal("{}", values);
    }
}
