using System.Text.Json;
using BoardOil.Contracts.Board;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Board;
using BoardOil.Services.Tag;
using BoardOil.Services.Tests.Infrastructure;
using BoardOil.TasksMd;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardImportServiceTests : TestBaseDb
{
    private readonly FakeTasksMdClient _tasksMdClient = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<ITasksMdClient>();
        services.AddSingleton<ITasksMdClient>(_tasksMdClient);
        services.AddScoped<BoardImportService>();
    }

    [Fact]
    public async Task ImportTasksMdBoardAsync_ShouldCreateBoardWithImportedColumnsCardsTagsAndOwner()
    {
        _tasksMdClient.Model = new TasksMdBoardImportModel(
            [
                new TasksMdImportedColumn(
                    "Todo",
                    [
                        new TasksMdImportedCard("Card A", "Description", ["Urgent", "MissingTag"]),
                        new TasksMdImportedCard("Card B", string.Empty, [])
                    ]),
                new TasksMdImportedColumn("Done", [new TasksMdImportedCard("Card C", "Done now", ["Urgent"])])
            ],
            [new TasksMdImportedTag("Urgent", "#bf616a")]);

        var service = ResolveService<BoardImportService>();
        var result = await service.ImportTasksMdBoardAsync(
            new ImportTasksMdBoardRequest("https://tasks.example.net/"),
            ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("tasks.example.net", result.Data!.Name);
        Assert.Equal(["Todo", "Done"], result.Data.Columns.Select(x => x.Title).ToArray());
        Assert.Equal(["Card A", "Card B"], result.Data.Columns[0].Cards.Select(x => x.Title).ToArray());

        var boardId = result.Data.Id;
        var board = DbContextForAssert.Boards.Single(x => x.Id == boardId);
        Assert.Equal("tasks.example.net", board.Name);

        var ownerMembership = DbContextForAssert.BoardMembers.Single(x => x.BoardId == boardId && x.UserId == ActorUserId);
        Assert.Equal(BoardMemberRole.Owner, ownerMembership.Role);

        var columns = DbContextForAssert.Columns.Where(x => x.BoardId == boardId).OrderBy(x => x.SortKey).ToList();
        Assert.Equal(["Todo", "Done"], columns.Select(x => x.Title).ToArray());

        var todoCards = DbContextForAssert.Cards
            .Where(x => x.BoardColumnId == columns[0].Id)
            .OrderBy(x => x.SortKey)
            .ToList();
        Assert.Equal(["Card A", "Card B"], todoCards.Select(x => x.Title).ToArray());

        var tags = DbContextForAssert.Tags.Where(x => x.BoardId == boardId).OrderBy(x => x.Name).ToList();
        Assert.Equal(["MissingTag", "Urgent"], tags.Select(x => x.Name).ToArray());

        var urgentTag = tags.Single(x => x.Name == "Urgent");
        var urgentStyle = JsonDocument.Parse(urgentTag.StylePropertiesJson).RootElement;
        Assert.Equal("#bf616a", urgentStyle.GetProperty("backgroundColor").GetString());
        Assert.Equal("auto", urgentStyle.GetProperty("textColorMode").GetString());

        var missingTag = tags.Single(x => x.Name == "MissingTag");
        Assert.Equal(TagStyleSchemaValidator.SolidStyleName, missingTag.StyleName);
    }

    [Fact]
    public async Task ImportTasksMdBoardAsync_WhenUrlIsInvalid_ShouldReturnBadRequest()
    {
        var service = ResolveService<BoardImportService>();

        var result = await service.ImportTasksMdBoardAsync(new ImportTasksMdBoardRequest("notaurl"), ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("url", result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task ImportTasksMdBoardAsync_WhenClientFails_ShouldReturnBadRequestAndWriteNothing()
    {
        _tasksMdClient.ExceptionToThrow = new TasksMdClientException(
            "Unable to fetch tasksmd data.",
            [new TasksMdClientValidationError("url", "Unable to fetch tasksmd data.")]);

        var service = ResolveService<BoardImportService>();

        var result = await service.ImportTasksMdBoardAsync(
            new ImportTasksMdBoardRequest("https://tasks.example.net/"),
            ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("url", result.ValidationErrors!.Keys);
        Assert.Empty(DbContextForAssert.Boards.Where(x => x.Name == "tasks.example.net"));
    }

    private sealed class FakeTasksMdClient : ITasksMdClient
    {
        public TasksMdBoardImportModel Model { get; set; } = new([], []);
        public Exception? ExceptionToThrow { get; set; }

        public Task<TasksMdBoardImportModel> LoadBoardAsync(Uri baseUri, CancellationToken cancellationToken = default)
        {
            _ = baseUri;
            _ = cancellationToken;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Model);
        }
    }
}
