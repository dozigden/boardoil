using System.Text.Json;
using BoardOil.Contracts.Common;

namespace BoardOil.Services.CustomField;

public sealed record CustomFieldValidationResult<T>(T? Value, IReadOnlyList<ValidationError> Errors) where T : class
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record LostCustomFieldValue(string Name, string Value);

public sealed record CustomFieldConversionResult(
    IReadOnlyDictionary<string, JsonElement> Values,
    IReadOnlyList<LostCustomFieldValue> LostValues);
