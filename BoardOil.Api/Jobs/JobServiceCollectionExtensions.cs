namespace BoardOil.Api.Jobs;

public static class JobServiceCollectionExtensions
{
    public static IServiceCollection AddBoardOilJobs(this IServiceCollection services)
    {
        services.AddHostedService<JobSchedulerService>();
        return services;
    }
}
