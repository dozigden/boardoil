namespace BoardOil.Abstractions.Board;

public interface IBoardBootstrapService
{
    Task EnsureDefaultBoardAsync();
}
