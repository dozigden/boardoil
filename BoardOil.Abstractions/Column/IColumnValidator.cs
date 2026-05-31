using BoardOil.Contracts.Column;
using BoardOil.Contracts.Common;

namespace BoardOil.Abstractions.Column;

public interface IColumnValidator
{
    IReadOnlyList<ValidationError> ValidateCreate(CreateColumnRequest request);
    IReadOnlyList<ValidationError> ValidateUpdate(UpdateColumnRequest request);
}
