namespace BoardOil.Api.Configuration;

public sealed class CsrfOptions
{
    public string CookieName { get; init; } = "boardoil_csrf";
    public string HeaderName { get; init; } = "X-BoardOil-CSRF";

    public static CsrfOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("BoardOilCsrf");
        return new CsrfOptions
        {
            CookieName = section["CookieName"] ?? "boardoil_csrf",
            HeaderName = section["HeaderName"] ?? "X-BoardOil-CSRF"
        };
    }
}
