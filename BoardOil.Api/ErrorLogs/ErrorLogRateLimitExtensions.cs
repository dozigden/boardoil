using System.Threading.RateLimiting;

namespace BoardOil.Api.ErrorLogs;

public static class ErrorLogRateLimitExtensions
{
    public const string ClientErrorReportPolicy = "client-error-report";
    public const int ClientErrorReportsPerMinute = 30;

    public static IServiceCollection AddErrorLogRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(ClientErrorReportPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = ClientErrorReportsPerMinute,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1),
                        AutoReplenishment = true
                    }));
        });

        return services;
    }
}
