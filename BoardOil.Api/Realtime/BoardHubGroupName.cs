namespace BoardOil.Api.Realtime;

internal static class BoardHubGroupName
{
    public static string For(int boardId) => $"board:{boardId}";
}
