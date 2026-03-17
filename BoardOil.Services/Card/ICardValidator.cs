using BoardOil.Services.Contracts;

namespace BoardOil.Services.Card;

public interface ICardValidator
{
    IReadOnlyList<ValidationError> ValidateCreate(CreateCardRequest request);
    IReadOnlyList<ValidationError> ValidateUpdate(UpdateCardRequest request);
}
