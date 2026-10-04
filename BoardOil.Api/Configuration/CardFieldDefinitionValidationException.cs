using BoardOil.Contracts.Common;

namespace BoardOil.Api.Configuration;

public sealed class CardFieldDefinitionValidationException(IReadOnlyList<ValidationError> errors)
    : Exception("Validation failed.")
{
    public IReadOnlyList<ValidationError> Errors { get; } = errors;
}
