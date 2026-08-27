using BoardOil.Abstractions;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Configuration;
using BoardOil.Data.Abstractions.Configuration;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Configuration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class SystemInfoMessageServiceTests
{
    [Fact]
    public async Task GetAsync_WhenNotConfigured_ShouldReturnNull()
    {
        // Arrange
        var repository = new InMemorySystemInfoMessageRepository();
        var scopes = new FakeDbContextScopeFactory();
        var boardEvents = new FakeBoardEvents();
        var service = new SystemInfoMessageService(scopes, repository, boardEvents);

        // Act
        var result = await service.GetAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task UpdateAsync_WithValidValue_ShouldValidateAndPersist()
    {
        // Arrange
        var repository = new InMemorySystemInfoMessageRepository();
        var scopes = new FakeDbContextScopeFactory();
        var boardEvents = new FakeBoardEvents();
        var service = new SystemInfoMessageService(scopes, repository, boardEvents);
        var request = new SystemInfoMessageDto(
            true,
            "⚠️",
            "Maintenance window",
            "Service update incoming.",
            "presets",
            """{"presetIndex":4,"textColorMode":"auto","unknown":"value"}""");

        // Act
        var result = await service.UpdateAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("presets", result.Data!.StyleName);
        Assert.Equal("""{"presetIndex":4}""", result.Data.StylePropertiesJson);
        Assert.Equal(1, scopes.SaveChangesCallCount);
        Assert.Single(boardEvents.SystemInfoMessageUpdatedEvents);

        var persisted = await repository.GetCurrentAsync();
        Assert.NotNull(persisted);
        Assert.Equal("Maintenance window", persisted!.Title);
        Assert.Equal("""{"presetIndex":4}""", persisted.StylePropertiesJson);
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidStyle_ShouldReturnBadRequestAndNotSave()
    {
        // Arrange
        var repository = new InMemorySystemInfoMessageRepository();
        var scopes = new FakeDbContextScopeFactory();
        var boardEvents = new FakeBoardEvents();
        var service = new SystemInfoMessageService(scopes, repository, boardEvents);
        var request = new SystemInfoMessageDto(
            true,
            "🚧",
            "Heads up",
            "This should fail.",
            "gradient",
            """{"leftColor":"#111111","rightColor":"#999999"}""");

        // Act
        var result = await service.UpdateAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(0, scopes.SaveChangesCallCount);
        Assert.Empty(boardEvents.SystemInfoMessageUpdatedEvents);
    }

    private sealed class InMemorySystemInfoMessageRepository : ISystemInfoMessageRepository
    {
        private readonly List<EntitySystemInfoMessage> _messages = [];
        private int _nextId = 1;

        public IQueryable<EntitySystemInfoMessage> Query() => _messages.AsQueryable();

        public EntitySystemInfoMessage? Get(int id) => _messages.FirstOrDefault(x => x.Id == id);

        public void Add(EntitySystemInfoMessage entity)
        {
            if (entity.Id == 0)
            {
                entity.Id = _nextId++;
            }

            _messages.Add(entity);
        }

        public void AddRange(IEnumerable<EntitySystemInfoMessage> entities)
        {
            foreach (var entity in entities)
            {
                Add(entity);
            }
        }

        public void Remove(EntitySystemInfoMessage entity)
        {
            _messages.Remove(entity);
        }

        public void RemoveRange(IEnumerable<EntitySystemInfoMessage> entities)
        {
            foreach (var entity in entities.ToArray())
            {
                _messages.Remove(entity);
            }
        }

        public Task<EntitySystemInfoMessage?> GetCurrentAsync()
        {
            var current = _messages.OrderBy(x => x.Id).FirstOrDefault();
            return Task.FromResult(current);
        }
    }

    private sealed class FakeDbContextScopeFactory : IDbContextScopeFactory
    {
        public int SaveChangesCallCount { get; private set; }

        public IDbContextScope Create(DbContextScopeOption joiningOption = DbContextScopeOption.JoinExisting)
        {
            _ = joiningOption;
            return new FakeScope(this);
        }

        public IDbContextReadOnlyScope CreateReadOnly(DbContextScopeOption joiningOption = DbContextScopeOption.JoinExisting)
        {
            _ = joiningOption;
            return new FakeScope(this);
        }

        public IDbContextScope CreateWithTransaction(System.Data.IsolationLevel isolationLevel)
        {
            _ = isolationLevel;
            return new FakeScope(this);
        }

        public IDbContextReadOnlyScope CreateReadOnlyWithTransaction(System.Data.IsolationLevel isolationLevel)
        {
            _ = isolationLevel;
            return new FakeScope(this);
        }

        public IDisposable SuppressAmbientContext() => new NoopDisposable();

        private sealed class FakeScope(FakeDbContextScopeFactory owner) : IDbContextReadOnlyScope
        {
            public IDbContextCollection DbContexts { get; } = new FakeDbContextCollection();

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                _ = cancellationToken;
                owner.SaveChangesCallCount++;
                return Task.FromResult(0);
            }

            public Task Transaction(Func<IDbContextTransactionScope, IDbContextTransaction, Task> executor)
            {
                _ = executor;
                return Task.CompletedTask;
            }

            public void Dispose()
            {
            }
        }

        private sealed class FakeDbContextCollection : IDbContextCollection
        {
            public TDbContext Get<TDbContext>() where TDbContext : DbContext =>
                throw new NotSupportedException();

            public int Commit() => 0;

            public Task<int> CommitAsync(CancellationToken cancellationToken = default)
            {
                _ = cancellationToken;
                return Task.FromResult(0);
            }

            public void Rollback()
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class FakeBoardEvents : IBoardEvents
    {
        public List<SystemInfoMessageDto?> SystemInfoMessageUpdatedEvents { get; } = [];

        public Task ColumnCreatedAsync(int boardId, BoardOil.Contracts.Column.ColumnDto column) => Task.CompletedTask;
        public Task ColumnUpdatedAsync(int boardId, BoardOil.Contracts.Column.ColumnDto column) => Task.CompletedTask;
        public Task ColumnDeletedAsync(int boardId, int columnId) => Task.CompletedTask;
        public Task CardCreatedAsync(int boardId, BoardOil.Contracts.Card.CardDto card) => Task.CompletedTask;
        public Task CardUpdatedAsync(int boardId, BoardOil.Contracts.Card.CardDto card) => Task.CompletedTask;
        public Task CardDeletedAsync(int boardId, int cardId) => Task.CompletedTask;
        public Task CardMovedAsync(int boardId, BoardOil.Contracts.Card.CardDto card) => Task.CompletedTask;
        public Task CommentCreatedAsync(int boardId, BoardOil.Contracts.Card.CardCommentDto comment) => Task.CompletedTask;
        public Task ResyncRequestedAsync(int boardId) => Task.CompletedTask;

        public Task SystemInfoMessageUpdatedAsync(SystemInfoMessageDto? systemInfoMessage)
        {
            SystemInfoMessageUpdatedEvents.Add(systemInfoMessage);
            return Task.CompletedTask;
        }
    }
}
