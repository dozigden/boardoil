using BoardOil.Services.Contracts;
using Microsoft.AspNetCore.Http;

namespace BoardOil.Api.Extensions;

public static class ApiResultHttpExtensions
{
    public static IResult ToHttpResult(this ApiResult result) =>
        Results.Json(result, statusCode: result.StatusCode);

    public static IResult ToHttpResult<T>(this ApiResult<T> result) =>
        Results.Json(result, statusCode: result.StatusCode);

    public static async Task<IResult> ToHttpResult(this Task<ApiResult> task) =>
        (await task).ToHttpResult();

    public static async Task<IResult> ToHttpResult<T>(this Task<ApiResult<T>> task) =>
        (await task).ToHttpResult();
}
