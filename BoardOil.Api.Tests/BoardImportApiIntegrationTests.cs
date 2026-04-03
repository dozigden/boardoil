using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Tag;
using BoardOil.TasksMd;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class BoardImportApiIntegrationTests : TestBaseIntegration
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly FakeTasksMdClient _tasksMdClient = new();

    protected override BoardOilApiFactory CreateFactory(string databasePath) =>
        new(
            databasePath,
            configureTestServices: services =>
            {
                services.RemoveAll<ITasksMdClient>();
                services.AddSingleton<ITasksMdClient>(_tasksMdClient);
            });

    [Fact]
    public async Task ImportTasksMdBoard_ShouldCreateBoardFromImportedModel()
    {
        _tasksMdClient.Model = new TasksMdBoardImportModel(
            [
                new TasksMdImportedColumn(
                    "Todo",
                    [
                        new TasksMdImportedCard("Card A", "Clean description", ["Urgent", "Discovered"]),
                        new TasksMdImportedCard("Card B", string.Empty, [])
                    ]),
                new TasksMdImportedColumn("In Progress", [new TasksMdImportedCard("Card C", "Working", ["Urgent"])])
            ],
            [new TasksMdImportedTag("Urgent", "#bf616a")]);

        var response = await Client.PostAsJsonAsync(
            "/api/boards/import/tasksmd",
            new ImportTasksMdBoardRequest("https://tasks.example.net/"));
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.True(payload!.Success);
        Assert.NotNull(payload.Data);
        Assert.Equal("tasks.example.net", payload.Data!.Name);
        Assert.Equal(["Todo", "In Progress"], payload.Data.Columns.Select(x => x.Title).ToArray());
        Assert.Equal(["Card A", "Card B"], payload.Data.Columns[0].Cards.Select(x => x.Title).ToArray());
        Assert.Equal(["Discovered", "Urgent"], payload.Data.Columns[0].Cards[0].TagNames);
        Assert.Equal("Clean description", payload.Data.Columns[0].Cards[0].Description);

        var tagsResponse = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<TagDto>>>(
            $"/api/boards/{payload.Data.Id}/tags",
            JsonOptions);
        Assert.NotNull(tagsResponse);
        Assert.NotNull(tagsResponse!.Data);
        var tags = tagsResponse.Data!;
        Assert.Contains(tags, x => x.Name == "Urgent" && x.StylePropertiesJson.Contains("#bf616a", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tags, x => x.Name == "Discovered");
    }

    [Fact]
    public async Task ImportTasksMdBoard_WhenUrlIsInvalid_ShouldReturnBadRequest()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/boards/import/tasksmd",
            new ImportTasksMdBoardRequest("notaurl"));

        Assert.Equal(400, (int)response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.NotNull(payload.ValidationErrors);
        Assert.Contains("url", payload.ValidationErrors!.Keys);
    }

    private sealed class FakeTasksMdClient : ITasksMdClient
    {
        public TasksMdBoardImportModel Model { get; set; } = new([], []);

        public Task<TasksMdBoardImportModel> LoadBoardAsync(Uri baseUri, CancellationToken cancellationToken = default)
        {
            _ = baseUri;
            _ = cancellationToken;
            return Task.FromResult(Model);
        }
    }

    private sealed record ApiEnvelope<T>(
        bool Success,
        T? Data,
        int StatusCode,
        string? Message,
        Dictionary<string, string[]>? ValidationErrors);
}
