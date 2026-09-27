using BoardOil.Services.Configuration;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class SystemTimeZoneSettingsTests
{
    [Fact]
    public void Validate_ShouldAcceptEveryPickerOption()
    {
        var ids = SystemTimeZoneSettings.GetSupportedIds();

        Assert.Contains("UTC", ids);
        Assert.Contains("Europe/London", ids);
        Assert.All(ids, id => Assert.Null(SystemTimeZoneSettings.Validate(id)));
    }

    [Fact]
    public void Validate_ShouldRejectAliasMissingFromPicker()
    {
        Assert.DoesNotContain("Europe/Belfast", SystemTimeZoneSettings.GetSupportedIds());

        var error = SystemTimeZoneSettings.Validate("Europe/Belfast");

        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ShouldAllowSurroundingWhitespace()
    {
        Assert.Null(SystemTimeZoneSettings.Validate(" Europe/London "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-zone")]
    [InlineData("GMT Standard Time")]
    public void Validate_ShouldRejectUnsupportedValues(string? id)
    {
        Assert.NotNull(SystemTimeZoneSettings.Validate(id));
    }
}
