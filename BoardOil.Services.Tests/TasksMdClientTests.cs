using System.Net;
using System.Net.Http;
using System.Text;
using BoardOil.TasksMd;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class TasksMdClientTests
{
    [Fact]
    public async Task LoadBoardAsync_ShouldOrderColumnsAndCards_AndStripTagTokens()
    {
        var handler = new StubHttpMessageHandler(new Dictionary<string, string>
        {
            ["/_api/resource"] = """
                [
                  {
                    "name": "In Progress",
                    "files": [
                      { "name": "A", "content": "doing" }
                    ]
                  },
                  {
                    "name": "Todo",
                    "files": [
                      { "name": "Dup", "content": "[tag:One]\nfirst dup" },
                      { "name": "Task", "content": "before [tag:Two]\n\nBody" },
                      { "name": "Dup", "content": "second dup [tag:one]" }
                    ]
                  }
                ]
                """,
            ["/_api/tags"] = """
                {
                  "One": "var( --color-alt-1 );",
                  "Two": "var(--color-alt-2)",
                  "Three": "var(--unknown)"
                }
                """,
            ["/_api/sort"] = """
                {
                  "Todo": ["Dup", "Task", "Dup"],
                  "In Progress": ["A"]
                }
                """
        });
        var httpClient = new HttpClient(handler);
        var client = new TasksMdClient(httpClient);

        var result = await client.LoadBoardAsync(new Uri("https://tasks.example.net/"));

        Assert.Equal(["Todo", "In Progress"], result.Columns.Select(x => x.Name).ToArray());
        Assert.Equal(["Dup", "Task", "Dup"], result.Columns[0].Cards.Select(x => x.Name).ToArray());
        Assert.Equal("first dup", result.Columns[0].Cards[0].Description);
        Assert.Equal("before\n\nBody", result.Columns[0].Cards[1].Description);
        Assert.Equal("second dup", result.Columns[0].Cards[2].Description);
        Assert.Equal(["One"], result.Columns[0].Cards[0].TagNames);
        Assert.Equal(["Two"], result.Columns[0].Cards[1].TagNames);
        Assert.Equal(["one"], result.Columns[0].Cards[2].TagNames);

        var oneTag = result.Tags.Single(x => x.Name == "One");
        var twoTag = result.Tags.Single(x => x.Name == "Two");
        var threeTag = result.Tags.Single(x => x.Name == "Three");
        Assert.Equal("#a51d2d", oneTag.HexColor);
        Assert.Equal("#c64600", twoTag.HexColor);
        Assert.Null(threeTag.HexColor);
    }

    [Fact]
    public async Task LoadBoardAsync_WhenSortPayloadIsInvalid_ShouldThrowTasksMdClientException()
    {
        var handler = new StubHttpMessageHandler(new Dictionary<string, string>
        {
            ["/_api/resource"] = "[]",
            ["/_api/tags"] = "{}",
            ["/_api/sort"] = "[]"
        });
        var httpClient = new HttpClient(handler);
        var client = new TasksMdClient(httpClient);

        var exception = await Assert.ThrowsAsync<TasksMdClientException>(() =>
            client.LoadBoardAsync(new Uri("https://tasks.example.net/")));

        Assert.Contains("sort payload", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(exception.ValidationErrors, x => x.Property == "url");
    }

    private sealed class StubHttpMessageHandler(Dictionary<string, string> payloadByPath) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _ = cancellationToken;

            if (!payloadByPath.TryGetValue(request.RequestUri?.AbsolutePath ?? string.Empty, out var payload))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(string.Empty)
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        }
    }
}
