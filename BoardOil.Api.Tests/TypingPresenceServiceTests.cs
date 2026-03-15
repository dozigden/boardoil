using BoardOil.Api.Configuration;
using BoardOil.Api.Realtime;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class TypingPresenceServiceTests
{
    [Fact]
    public async Task SweepExpiredAsync_WhenEntryWasRefreshedMidSweep_ShouldKeepRefreshedEntry()
    {
        // Arrange
        var proxy = new GateableClientProxy();
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 03, 15, 12, 0, 0, TimeSpan.Zero));
        var service = new TypingPresenceService(
            new StubHubContext(proxy),
            new BoardOilRuntimeOptions { TypingTtlSeconds = 1 },
            time);

        await service.StartTypingAsync(1, "title", "UserA");
        await service.StartTypingAsync(2, "title", "UserB");
        time.Advance(TimeSpan.FromMilliseconds(1100));

        proxy.BlockNextSend();

        // Act
        var sweepTask = service.SweepExpiredAsync();
        await proxy.WaitForBlockedSendAsync();

        await service.StartTypingAsync(1, "title", "UserA");
        await service.StartTypingAsync(2, "title", "UserB");

        proxy.ReleaseBlockedSend();
        await sweepTask;

        // Assert
        var active = service.GetActiveEntries();
        Assert.Equal(2, active.Count);
        Assert.Contains(active, x => x.CardId == 1 && x.UserLabel == "UserA");
        Assert.Contains(active, x => x.CardId == 2 && x.UserLabel == "UserB");
    }

    private sealed class StubHubContext(IClientProxy proxy) : IHubContext<BoardHub>
    {
        public IHubClients Clients { get; } = new StubHubClients(proxy);
        public IGroupManager Groups { get; } = new StubGroupManager();
    }

    private sealed class StubHubClients(IClientProxy proxy) : IHubClients
    {
        public IClientProxy All => proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => proxy;
        public IClientProxy Client(string connectionId) => proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => proxy;
        public IClientProxy Group(string groupName) => proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => proxy;
        public IClientProxy User(string userId) => proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => proxy;
    }

    private sealed class StubGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ManualTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan by)
        {
            _utcNow = _utcNow.Add(by);
        }
    }

    private sealed class GateableClientProxy : IClientProxy
    {
        private readonly object _gateLock = new();
        private TaskCompletionSource<bool>? _blockedSendStartedSignal;
        private TaskCompletionSource<bool>? _blockedSendReleasedSignal;
        private bool _blockArmed;

        public void BlockNextSend()
        {
            lock (_gateLock)
            {
                _blockedSendStartedSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _blockedSendReleasedSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _blockArmed = true;
            }
        }

        public async Task WaitForBlockedSendAsync()
        {
            Task waitTask;
            lock (_gateLock)
            {
                waitTask = _blockedSendStartedSignal?.Task
                    ?? throw new InvalidOperationException("Call BlockNextSend before waiting.");
            }

            await waitTask;
        }

        public void ReleaseBlockedSend()
        {
            lock (_gateLock)
            {
                _blockedSendReleasedSignal?.TrySetResult(true);
            }
        }

        public async Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Task? releaseTask = null;
            lock (_gateLock)
            {
                if (_blockArmed)
                {
                    _blockedSendStartedSignal?.TrySetResult(true);
                    releaseTask = _blockedSendReleasedSignal?.Task;
                    _blockArmed = false;
                }
            }

            if (releaseTask is not null)
            {
                await releaseTask.WaitAsync(cancellationToken);
            }
        }
    }
}
