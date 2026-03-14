using BoardOil.Ef;
using BoardOil.Services.Abstractions;
using BoardOil.Services.Implementations;

namespace BoardOil.Services.Tests.Infrastructure;

public static class TestServiceFactory
{
    public static CardService CreateCardService(BoardOilDbContext dbContext)
    {
        ICardRepository repository = new CardRepository(dbContext);
        return new CardService(repository, new CardValidator());
    }
}
