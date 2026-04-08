using BoardOil.Api.Extensions;
using BoardOil.Api.Auth;
using BoardOil.Api.Configuration;
using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Contracts;
using BoardOil.Services.Auth;
using Microsoft.AspNetCore.Http;

namespace BoardOil.Api.Endpoints;

public static class BoardEndpoints
{
    public static IEndpointRouteBuilder MapBoardEndpoints(this IEndpointRouteBuilder app)
    {
        var boardEndpoints = app
            .MapGroup("/api/boards")
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .AddEndpointFilter<RequireActorUserIdFilter>();

        boardEndpoints.MapGet(string.Empty, async (IBoardService boardService, HttpContext httpContext) =>
            (await boardService.GetBoardsAsync(httpContext.GetActorUserId())).ToHttpResult());

        boardEndpoints.MapGet("/{boardId:int}", async (int boardId, IBoardService boardService, HttpContext httpContext) =>
            (await boardService.GetBoardAsync(boardId, httpContext.GetActorUserId())).ToHttpResult());

        boardEndpoints.MapGet("/{boardId:int}/export", async (
            int boardId,
            IBoardExportService boardExportService,
            BoardOilBuildInfo buildInfo,
            HttpContext httpContext) =>
        {
            var result = await boardExportService.ExportBoardAsync(boardId, httpContext.GetActorUserId(), buildInfo.Version);
            if (!result.Success)
            {
                return result.ToHttpResult();
            }

            var export = result.Data!;
            return Results.File(export.Content, export.ContentType, export.FileName);
        });

        boardEndpoints.MapPost(string.Empty, async (CreateBoardRequest request, IBoardService boardService, HttpContext httpContext) =>
            (await boardService.CreateBoardAsync(request, httpContext.GetActorUserId())).ToHttpResult());

        boardEndpoints.MapPost("/import", async (
            HttpRequest request,
            IBoardPackageImportService boardPackageImportService,
            HttpContext httpContext) =>
        {
            var importRequestResult = await TryReadBoardPackageImportRequestAsync(request);
            if (!importRequestResult.Success)
            {
                return importRequestResult.ToHttpResult();
            }

            return (await boardPackageImportService.ImportBoardPackageAsync(importRequestResult.Data!, httpContext.GetActorUserId())).ToHttpResult();
        });

        boardEndpoints.MapPost("/import/tasksmd", async (ImportTasksMdBoardRequest request, IBoardTasksMdImportService boardTasksMdImportService, HttpContext httpContext) =>
            (await boardTasksMdImportService.ImportTasksMdBoardAsync(request, httpContext.GetActorUserId())).ToHttpResult());

        boardEndpoints.MapPut("/{boardId:int}", async (int boardId, UpdateBoardRequest request, IBoardService boardService, HttpContext httpContext) =>
            (await boardService.UpdateBoardAsync(boardId, request, httpContext.GetActorUserId())).ToHttpResult());

        boardEndpoints.MapDelete("/{boardId:int}", async (int boardId, IBoardService boardService, HttpContext httpContext) =>
            (await boardService.DeleteBoardAsync(boardId, httpContext.GetActorUserId())).ToHttpResult());

        boardEndpoints.MapGet("/{boardId:int}/members", async (int boardId, IBoardMemberService boardMemberService, HttpContext httpContext) =>
            (await boardMemberService.GetMembersAsync(boardId, httpContext.GetActorUserId())).ToHttpResult());

        boardEndpoints.MapPost("/{boardId:int}/members", async (int boardId, AddBoardMemberRequest request, IBoardMemberService boardMemberService, HttpContext httpContext) =>
            (await boardMemberService.AddMemberAsync(boardId, request, httpContext.GetActorUserId())).ToHttpResult());

        boardEndpoints.MapPatch("/{boardId:int}/members/{userId:int}", async (int boardId, int userId, UpdateBoardMemberRoleRequest request, IBoardMemberService boardMemberService, HttpContext httpContext) =>
            (await boardMemberService.UpdateMemberRoleAsync(boardId, userId, request, httpContext.GetActorUserId())).ToHttpResult());

        boardEndpoints.MapDelete("/{boardId:int}/members/{userId:int}", async (int boardId, int userId, IBoardMemberService boardMemberService, HttpContext httpContext) =>
            (await boardMemberService.RemoveMemberAsync(boardId, userId, httpContext.GetActorUserId())).ToHttpResult());

        return app;
    }

    private static async Task<ApiResult<ImportBoardPackageRequest>> TryReadBoardPackageImportRequestAsync(HttpRequest request)
    {
        if (!request.HasFormContentType)
        {
            return ValidationFailure("file", "Board package upload must use multipart/form-data.");
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync();
        }
        catch (InvalidDataException)
        {
            return ValidationFailure("file", "Board package upload could not be read.");
        }

        var packageFile = form.Files.GetFile("file");
        if (packageFile is null)
        {
            return ValidationFailure("file", "Board package ZIP file is required.");
        }

        if (packageFile.Length <= 0)
        {
            return ValidationFailure("file", "Board package ZIP file cannot be empty.");
        }

        byte[] packageContent;
        await using (var packageStream = packageFile.OpenReadStream())
        {
            using var memoryStream = new MemoryStream();
            await packageStream.CopyToAsync(memoryStream);
            packageContent = memoryStream.ToArray();
        }

        var boardName = form.TryGetValue("name", out var boardNameValues)
            ? boardNameValues.ToString()
            : null;

        return ApiResults.Ok(new ImportBoardPackageRequest(boardName, packageContent));
    }

    private static ApiResult<ImportBoardPackageRequest> ValidationFailure(string property, string message) =>
        ApiResults.BadRequest<ImportBoardPackageRequest>(
            "Validation failed.",
            new Dictionary<string, string[]>
            {
                [property] = [message]
            });
}
