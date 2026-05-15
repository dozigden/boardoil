using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Card;

public interface ICardValidator
{
    Task<IReadOnlyList<ValidationError>> ValidateCreateAsync(int boardId, CreateCardRequest request);
    Task<IReadOnlyList<ValidationError>> ValidateUpdateAsync(int boardId, UpdateCardRequest request);
}
