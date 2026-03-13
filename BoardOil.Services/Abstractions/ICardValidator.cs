using BoardOil.Services.Contracts;

namespace BoardOil.Services.Abstractions;

public interface ICardValidator
{
    IReadOnlyList<ValidationError> ValidateCreate(CreateCardRequest request);
    IReadOnlyList<ValidationError> ValidateUpdate(UpdateCardRequest request);
}
