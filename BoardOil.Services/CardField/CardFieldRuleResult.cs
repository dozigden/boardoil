using System.Text.Json;
using BoardOil.Contracts.Common;

namespace BoardOil.Services.CardField;

public sealed record CardFieldRuleResult<T>(T? Value, IReadOnlyList<ValidationError> Errors) where T : class
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record LostCardFieldValue(string DisplayName, string Value);

public sealed record CardFieldConversionResult(
    IReadOnlyDictionary<string, JsonElement> Values,
    IReadOnlyList<LostCardFieldValue> LostValues);
