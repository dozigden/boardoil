using System.Text.Json;
using BoardOil.Api.Configuration;
using BoardOil.Contracts.Card;
using BoardOil.Services.CustomField;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class CustomFieldInputBindingTests
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
        options.Converters.Add(new CustomFieldValuesJsonConverter());
        var json = $$$"""{"title":"Card","description":"","tagNames":[],"cardTypeId":1,"customFieldValues":{"Points":{{{value}}}}}""";

        var request = JsonSerializer.Deserialize<UpdateCardRequest>(json, options)!;
        var result = CustomFieldValidator.ValidateAndNormaliseValues(
            [new BoardOil.Contracts.CustomField.IntegerCustomFieldDefinition { Code = "Points", Name = "Estimate", Nullable = true }],
            request.CustomFieldValues!);

        Assert.Equal(value, request.CustomFieldValues!["Points"].GetRawText());
        Assert.False(result.IsValid);
        Assert.Equal("customFieldValues.Points", Assert.Single(result.Errors).Property);
        Assert.Equal("Whole number required", Assert.Single(result.Errors).Message);
    }
}
