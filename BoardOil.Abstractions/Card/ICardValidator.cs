using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Card;

public interface ICardValidator
{
    IReadOnlyList<ValidationError> ValidateCreate(CreateCardRequest request);
    IReadOnlyList<ValidationError> ValidateUpdate(UpdateCardRequest request);
}
