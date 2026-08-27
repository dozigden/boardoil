using BoardOil.Services.Tag;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class TagEmojiValidatorTests
{
    [Theory]
    [InlineData("🐈‍⬛")]
    [InlineData("👩🏽‍💻")]
    [InlineData("A")]
    public void ValidateAndNormalise_WhenValueIsSingleGrapheme_ShouldAcceptIt(string value)
    {
        var result = TagEmojiValidator.ValidateAndNormalise(value, "emoji");

        Assert.Null(result.Error);
        Assert.Equal(value, result.CanonicalEmoji);
    }

    [Fact]
    public void ValidateAndNormalise_WhenValueContainsMultipleGraphemes_ShouldRejectIt()
    {
        var result = TagEmojiValidator.ValidateAndNormalise("not-emoji", "emoji");

        Assert.Null(result.CanonicalEmoji);
        Assert.NotNull(result.Error);
    }
}
