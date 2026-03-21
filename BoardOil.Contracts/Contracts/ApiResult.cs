namespace BoardOil.Contracts.Contracts;

public record ApiResult(
    bool Success,
    int StatusCode,
    string? Message = null,
    Dictionary<string, string[]>? ValidationErrors = null)
{
    public static implicit operator ApiResult(ApiError error) =>
        new(false, error.StatusCode, error.Message, error.ValidationErrors);
}

public sealed record ApiResult<T>(
    bool Success,
    T? Data,
    int StatusCode,
    string? Message = null,
    Dictionary<string, string[]>? ValidationErrors = null)
    : ApiResult(Success, StatusCode, Message, ValidationErrors)
{
    public static implicit operator ApiResult<T>(T data) =>
        new(true, data, 200);

    public static implicit operator ApiResult<T>(ApiError error) =>
        new(false, default, error.StatusCode, error.Message, error.ValidationErrors);
}

public sealed record ApiError(
    int StatusCode,
    string Message,
    Dictionary<string, string[]>? ValidationErrors = null);

public static class ApiResults
{
    public static ApiResult Ok() =>
        new(true, 200);

    public static ApiResult<T> Ok<T>(T data) =>
        new(true, data, 200);

    public static ApiResult<T> Created<T>(T data) =>
        new(true, data, 201);

    public static ApiResult<T> BadRequest<T>(string message, Dictionary<string, string[]>? validationErrors = null) =>
        new(false, default, 400, message, validationErrors);

    public static ApiResult<T> Unauthorized<T>(string message) =>
        new(false, default, 401, message);

    public static ApiResult<T> Forbidden<T>(string message) =>
        new(false, default, 403, message);

    public static ApiResult<T> NotFound<T>(string message) =>
        new(false, default, 404, message);

    public static ApiResult<T> InternalError<T>(string message) =>
        new(false, default, 500, message);
}

public static class ApiErrors
{
    public static ApiError BadRequest(string message) =>
        new(400, message);

    public static ApiError BadRequest(string message, IReadOnlyList<ValidationError> validationErrors) =>
        new(400, message, ToValidationDictionary(validationErrors));

    public static ApiError Unauthorized(string message) =>
        new(401, message);

    public static ApiError Forbidden(string message) =>
        new(403, message);

    public static ApiError NotFound(string message) =>
        new(404, message);

    public static ApiError InternalError(string message) =>
        new(500, message);

    private static Dictionary<string, string[]> ToValidationDictionary(IReadOnlyList<ValidationError> validationErrors) =>
        validationErrors
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Property) ? string.Empty : x.Property)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Message).ToArray());
}
