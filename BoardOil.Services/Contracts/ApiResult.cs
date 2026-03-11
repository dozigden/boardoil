namespace BoardOil.Services.Contracts;

public sealed record ApiResult<T>(
    bool Success,
    T? Data,
    int StatusCode,
    string? Message = null,
    Dictionary<string, string[]>? ValidationErrors = null);

public static class ApiResult
{
    public static ApiResult<T> Ok<T>(T data) =>
        new(true, data, 200);

    public static ApiResult<T> Created<T>(T data) =>
        new(true, data, 201);

    public static ApiResult<T> BadRequest<T>(string message, Dictionary<string, string[]>? validationErrors = null) =>
        new(false, default, 400, message, validationErrors);

    public static ApiResult<T> NotFound<T>(string message) =>
        new(false, default, 404, message);

    public static ApiResult<T> InternalError<T>(string message) =>
        new(false, default, 500, message);
}
