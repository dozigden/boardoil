using BoardOil.Services.Tag;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class TagStyleSchemaValidatorTests
{
    [Fact]
    public void IsValidJsonObject_WhenInputIsNullOrWhitespace_ShouldReturnFalse()
    {
        Assert.False(TagStyleSchemaValidator.IsValidJsonObject(null!));
        Assert.False(TagStyleSchemaValidator.IsValidJsonObject(string.Empty));
        Assert.False(TagStyleSchemaValidator.IsValidJsonObject("   "));
    }

    [Fact]
    public void IsValidJsonObject_WhenInputIsNonObjectJson_ShouldReturnFalse()
    {
        Assert.False(TagStyleSchemaValidator.IsValidJsonObject("[]"));
        Assert.False(TagStyleSchemaValidator.IsValidJsonObject("\"value\""));
    }

    [Fact]
    public void IsValidJsonObject_WhenInputIsObjectJson_ShouldReturnTrue()
    {
        Assert.True(TagStyleSchemaValidator.IsValidJsonObject("{}"));
        Assert.True(TagStyleSchemaValidator.IsValidJsonObject("{\"any\":\"shape\"}"));
    }
}
