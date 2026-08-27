using BoardOil.Services.Style;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class StyleDefinitionCodecTests
{
    public static TheoryData<string, string, string> ProductionDerivedSemanticShapes => new()
    {
        { "auto", "{}", "{}" },
        {
            "gradient",
            """{"leftColor":"#112233","rightColor":"#445566","textColorMode":"custom","textColor":"#AABBCC","borderMode":"auto"}""",
            """{"leftColor":"#112233","rightColor":"#445566","textColorMode":"custom","borderMode":"auto","textColor":"#AABBCC"}"""
        },
        {
            "gradient",
            """{"leftColor":"#112233","rightColor":"#445566","textColorMode":"auto","borderMode":"auto"}""",
            """{"leftColor":"#112233","rightColor":"#445566","textColorMode":"auto","borderMode":"auto"}"""
        },
        {
            "gradient",
            """{"leftColor":"#112233","rightColor":"#445566","textColorMode":"custom","textColor":"#AABBCC"}""",
            """{"leftColor":"#112233","rightColor":"#445566","textColorMode":"custom","borderMode":"auto","textColor":"#AABBCC"}"""
        },
        {
            "gradient",
            """{"leftColor":"#112233","rightColor":"#445566","textColorMode":"auto"}""",
            """{"leftColor":"#112233","rightColor":"#445566","textColorMode":"auto","borderMode":"auto"}"""
        },
        { "presets", """{"presetIndex":2}""", """{"presetIndex":2}""" },
        { "presets", """{"presetIndex":2,"textColorMode":"auto"}""", """{"presetIndex":2}""" },
        {
            "solid",
            """{"backgroundColor":"#112233","textColorMode":"auto","borderMode":"auto"}""",
            """{"backgroundColor":"#112233","textColorMode":"auto","borderMode":"auto"}"""
        },
        {
            "solid",
            """{"backgroundColor":"#112233","textColorMode":"auto"}""",
            """{"backgroundColor":"#112233","textColorMode":"auto","borderMode":"auto"}"""
        }
    };

    [Theory]
    [MemberData(nameof(ProductionDerivedSemanticShapes))]
    public void ParseCompatible_WhenGivenProductionDerivedShape_ShouldCanonicalise(
        string styleName,
        string stylePropertiesJson,
        string expectedCanonicalJson)
    {
        var result = StyleDefinitionCodec.ParseCompatible(styleName, stylePropertiesJson);

        Assert.True(result.IsValid);
        Assert.Equal(styleName, result.StyleName);
        Assert.Equal(expectedCanonicalJson, result.StylePropertiesJson);
    }

    [Theory]
    [InlineData("solid", """{"backgroundColor":"#112233","textColorMode":"auto"}""")]
    [InlineData("gradient", """{"leftColor":"#112233","rightColor":"#445566","textColorMode":"auto"}""")]
    public void ParseForWrite_WhenBorderModeIsMissing_ShouldReject(
        string styleName,
        string stylePropertiesJson)
    {
        var result = StyleDefinitionCodec.ParseForWrite(styleName, stylePropertiesJson);

        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationErrors, x => x.Property == "stylePropertiesJson");
    }

    [Fact]
    public void ParseForWrite_WhenPropertiesContainExtras_ShouldStripThem()
    {
        var result = StyleDefinitionCodec.ParseForWrite(
            " presets ",
            """{"presetIndex":4,"textColorMode":"auto","unknown":"value"}""");

        Assert.True(result.IsValid);
        Assert.Equal("presets", result.StyleName);
        Assert.Equal("""{"presetIndex":4}""", result.StylePropertiesJson);
    }

    [Fact]
    public void ParseCompatible_WhenPresetIndexIsLegacyIntegerString_ShouldCanonicaliseToNumber()
    {
        var result = StyleDefinitionCodec.ParseCompatible(
            "presets",
            """{"presetIndex":"4"}""");

        Assert.True(result.IsValid);
        Assert.Equal("presets", result.StyleName);
        Assert.Equal("""{"presetIndex":4}""", result.StylePropertiesJson);
    }

    [Fact]
    public void ParseForWrite_WhenManualColoursAreValid_ShouldNormaliseAndCanonicalise()
    {
        var result = StyleDefinitionCodec.ParseForWrite(
            "SOLID",
            """{"textColor":" #aabbcc ","borderColor":"#ddeeff","backgroundColor":"#abcdef","textColorMode":"custom","borderMode":"custom"}""");

        Assert.True(result.IsValid);
        Assert.Equal("solid", result.StyleName);
        Assert.Equal(
            """{"backgroundColor":"#ABCDEF","textColorMode":"custom","borderMode":"custom","borderColor":"#DDEEFF","textColor":"#AABBCC"}""",
            result.StylePropertiesJson);
    }

    [Theory]
    [InlineData(null, "{}")]
    [InlineData("unknown", "{}")]
    [InlineData("solid", null)]
    [InlineData("solid", "[]")]
    [InlineData("solid", "{not json")]
    [InlineData("solid", """{"backgroundColor":"blue","textColorMode":"auto","borderMode":"auto"}""")]
    [InlineData("solid", """{"backgroundColor":"#112233","textColorMode":"custom","borderMode":"auto"}""")]
    [InlineData("gradient", """{"leftColor":"#112233","rightColor":"#445566","textColorMode":"auto","borderMode":"invalid"}""")]
    [InlineData("presets", """{"presetIndex":-1}""")]
    [InlineData("presets", """{"presetIndex":12}""")]
    [InlineData("presets", """{"presetIndex":"2"}""")]
    public void ParseForWrite_WhenDefinitionIsInvalid_ShouldReject(
        string? styleName,
        string? stylePropertiesJson)
    {
        var result = StyleDefinitionCodec.ParseForWrite(styleName, stylePropertiesJson);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.ValidationErrors);
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("presets")]
    [InlineData("solid")]
    [InlineData("gradient")]
    public void CreateDefault_ForEveryStyleKind_ShouldProduceValidCanonicalDefinition(string styleName)
    {
        var definition = StyleDefinitionCodec.CreateDefault(styleName);
        var canonicalJson = StyleDefinitionCodec.Serialise(definition);

        var result = StyleDefinitionCodec.ParseForWrite(styleName, canonicalJson);

        Assert.True(result.IsValid);
        Assert.Equal(canonicalJson, result.StylePropertiesJson);
    }
}
