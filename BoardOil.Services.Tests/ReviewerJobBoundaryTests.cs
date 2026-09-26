using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Jobs;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class ReviewerJobBoundaryTests : TestBaseDb
{
    private readonly ReviewHandler _handler = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IJobHandler>(_handler);
    }

    [Fact]
    public async Task PlainTextHandlerResult_ShouldReachTerminalWithoutStrandingRunning()
    {
        var job = new EntityJob { Type = "review", Status = JobStatus.Pending, RunAfterUtc = DateTime.UtcNow.AddMinutes(-1) };
        DbContextForArrange.Jobs.Add(job);
        await DbContextForArrange.SaveChangesAsync();
        _handler.Result = JobHandlerResult.Succeeded("plain result from handler");

        await ResolveService<IJobRunner>().RunNextDueAsync().WaitAsync(TimeSpan.FromSeconds(5));

        var saved = await DbContextForAssert.Jobs.AsNoTracking().Include(x => x.Logs).SingleAsync();
        Assert.Equal(JobStatus.Completed, saved.Status);
        Assert.Equal("""{"message":"plain result from handler"}""", saved.ResultJson);
        var terminalLog = Assert.Single(saved.Logs, log => log.Message == "Job completed.");
        Assert.Equal(saved.ResultJson, terminalLog.DataJson);
        Assert.Equal(1, _handler.Calls);
    }

    [Fact]
    public async Task RunnerInsideAmbientScope_ShouldCommitOwnTransitionsWithoutSavingCallerWork()
    {
        var job = new EntityJob { Type = "review", Status = JobStatus.Pending, RunAfterUtc = DateTime.UtcNow.AddMinutes(-1) };
        DbContextForArrange.Jobs.Add(job);
        await DbContextForArrange.SaveChangesAsync();
        JobStatus? observedStatus = null;
        _handler.BeforeReturn = async () =>
        {
            await using var independent = CreateDbContextForAct();
            observedStatus = (await independent.Jobs.AsNoTracking().SingleAsync(x => x.Id == job.Id)).Status;
        };

        using (var callerScope = ResolveService<IDbContextScopeFactory>().Create())
        {
            callerScope.DbContexts.Get<BoardOilDbContext>().Jobs.Add(new EntityJob
            {
                Type = "caller-unsaved", Status = JobStatus.Pending, RunAfterUtc = DateTime.UtcNow.AddDays(1)
            });
            await ResolveService<IJobRunner>().RunNextDueAsync().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(JobStatus.Running, observedStatus);
            var durable = await DbContextForAssert.Jobs.AsNoTracking().Include(x => x.Logs).SingleAsync();
            Assert.Equal(job.Id, durable.Id);
            Assert.Equal(JobStatus.Completed, durable.Status);
            Assert.Equal(2, durable.Logs.Count);
            Assert.Single(durable.Logs, log => log.Message == "Job started.");
            Assert.Single(durable.Logs, log => log.Message == "Job completed.");
        }

        Assert.Equal(job.Id, (await DbContextForAssert.Jobs.AsNoTracking().SingleAsync()).Id);
    }

    private sealed class ReviewHandler : IJobHandler
    {
        public string Type => "review";
        public int Calls { get; private set; }
        public JobHandlerResult Result { get; set; } = JobHandlerResult.Succeeded();
        public Func<Task>? BeforeReturn { get; set; }

        public async Task<JobHandlerResult> HandleAsync(JobContext context, CancellationToken cancellationToken)
        {
            Calls++;
            if (BeforeReturn is not null)
            {
                await BeforeReturn();
            }

            return Result;
        }
    }
}
