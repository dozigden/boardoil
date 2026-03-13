using BoardOil.Services.Contracts;

namespace BoardOil.Services.Abstractions;

public interface IColumnValidator
{
    IReadOnlyList<ValidationError> ValidateCreate(CreateColumnRequest request);
    IReadOnlyList<ValidationError> ValidateUpdate(UpdateColumnRequest request);
}
