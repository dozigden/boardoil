using BoardOil.Contracts.Column;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Column;

public interface IColumnValidator
{
    IReadOnlyList<ValidationError> ValidateCreate(CreateColumnRequest request);
    IReadOnlyList<ValidationError> ValidateUpdate(UpdateColumnRequest request);
}
