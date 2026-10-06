using System.Text.Json;
using BoardOil.Api.Configuration;
using BoardOil.Contracts.Card;
using BoardOil.Services.CardField;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class CardFieldInputBindingTests
{
    [Theory]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    [InlineData("1e-400")]
    [InlineData("\"5\"")]
    [InlineData("true")]
    [InlineData("[]")]
    [InlineData("{}")]
    public void CardValues_ShouldBindRawInputWithoutThrowingBeforeValidation(string value)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new CardFieldValuesJsonConverter());
        var json = $$$"""{"title":"Card","description":"","tagNames":[],"cardTypeId":1,"fields":{"Points":{{{value}}}}}""";

        var request = JsonSerializer.Deserialize<UpdateCardRequest>(json, options)!;
        var result = CardFieldRules.NormaliseValues(
            [new BoardOil.Contracts.CardField.IntegerFieldDefinition { FieldCode = "Points", DisplayName = "Estimate", Nullable = true }],
            request.Fields!);

        Assert.Equal(value, request.Fields!["Points"].GetRawText());
        Assert.False(result.IsValid);
        Assert.Equal("fields.Points", Assert.Single(result.Errors).Property);
        Assert.Equal("Whole number required", Assert.Single(result.Errors).Message);
    }
}
