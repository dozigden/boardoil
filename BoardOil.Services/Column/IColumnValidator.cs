using BoardOil.Services.Contracts;

namespace BoardOil.Services.Column;

public interface IColumnValidator
{
    IReadOnlyList<ValidationError> ValidateCreate(CreateColumnRequest request);
    IReadOnlyList<ValidationError> ValidateUpdate(UpdateColumnRequest request);
}
