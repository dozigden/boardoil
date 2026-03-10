namespace BoardOil.Services.Abstractions;

public interface IBoardBootstrapService
{
    Task EnsureDefaultBoardAsync(CancellationToken cancellationToken = default);
}
