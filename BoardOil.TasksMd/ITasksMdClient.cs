namespace BoardOil.TasksMd;

public interface ITasksMdClient
{
    Task<TasksMdBoardImportModel> LoadBoardAsync(Uri baseUri, CancellationToken cancellationToken = default);
}
