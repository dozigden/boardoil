using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Jobs;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class JobHandlerScopeTests : TestBaseDb
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddLogging();
        services.AddScoped<IJobHandler, WritingHandler>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedJob_ShouldHaveDurableHandlerWrites(bool callerHasAmbientScope)
    {
        var job = new EntityJob { Type = "review-write", RunAfterUtc = DateTime.UtcNow.AddMinutes(-1) };
        DbContextForArrange.Jobs.Add(job);
        await DbContextForArrange.SaveChangesAsync();
        var scopes = ResolveService<IDbContextScopeFactory>();
        using (var caller = callerHasAmbientScope ? scopes.Create() : null)
        {
            if (caller is not null)
            {
                caller.DbContexts.Get<BoardOilDbContext>().Jobs.Add(new EntityJob
                {
                    Type = "caller-unsaved", RunAfterUtc = DateTime.UtcNow.AddDays(1)
                });
            }

            Assert.True(await ResolveService<IJobRunner>().RunNextDueAsync().WaitAsync(TimeSpan.FromSeconds(5)));

            if (caller is not null)
            {
                using var restored = scopes.Create();
                Assert.Same(caller.DbContexts.Get<BoardOilDbContext>(), restored.DbContexts.Get<BoardOilDbContext>());
            }
        }

        var persisted = await DbContextForAssert.Jobs.AsNoTracking().ToListAsync();
        Assert.Equal(JobStatus.Completed, Assert.Single(persisted, x => x.Id == job.Id).Status);
        Assert.Contains(persisted, x => x.Type == "handler-effect");
        Assert.DoesNotContain(persisted, x => x.Type == "caller-unsaved");
    }

    private sealed class WritingHandler(IDbContextScopeFactory scopes) : IJobHandler
    {
        public string Type => "review-write";
        public async Task<JobHandlerResult> HandleAsync(JobContext context, CancellationToken cancellationToken)
        {
            using var scope = scopes.Create();
            scope.DbContexts.Get<BoardOilDbContext>().Jobs.Add(new EntityJob
            {
                Type = "handler-effect", RunAfterUtc = DateTime.UtcNow.AddDays(1)
            });
            await scope.SaveChangesAsync(cancellationToken);
            return JobHandlerResult.Succeeded();
        }
    }
}
