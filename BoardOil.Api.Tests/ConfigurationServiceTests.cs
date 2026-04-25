using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Configuration;
using BoardOil.Contracts.Configuration;
using BoardOil.Persistence.Abstractions.Configuration;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class ConfigurationServiceTests
{
    [Fact]
    public async Task GetConfigurationAsync_WhenNoOverride_ShouldReturnNullBaseUrl()
    {
        // Arrange
        var repository = new InMemoryAppSettingRepository();
        var scopes = new FakeDbContextScopeFactory();
        var service = CreateService(repository, scopes);

        // Act
        var result = await service.GetConfigurationAsync();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.AllowInsecureCookies);
        Assert.Null(result.Data.McpPublicBaseUrl);
    }

    [Fact]
    public async Task UpdateConfigurationAsync_WithValidUrl_ShouldNormaliseAndPersist()
    {
        // Arrange
        var repository = new InMemoryAppSettingRepository();
        var scopes = new FakeDbContextScopeFactory();
        var service = CreateService(repository, scopes);

        // Act
        var result = await service.UpdateConfigurationAsync(new UpdateConfigurationRequest("https://boardoil.example.com/"));

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("https://boardoil.example.com", result.Data!.McpPublicBaseUrl);
        Assert.Equal(1, scopes.SaveChangesCallCount);

        var persisted = await repository.GetByKeyAsync("mcp_public_base_url");
        Assert.NotNull(persisted);
        Assert.Equal("https://boardoil.example.com", persisted!.Value);
    }

    [Fact]
    public async Task UpdateConfigurationAsync_WithNullValue_ShouldClearExistingOverride()
    {
        // Arrange
        var repository = new InMemoryAppSettingRepository();
        repository.Add(new EntityAppSetting
        {
            Key = "mcp_public_base_url",
            Value = "https://old.example.com",
            UpdatedAtUtc = DateTime.UtcNow
        });
        var scopes = new FakeDbContextScopeFactory();
        var service = CreateService(repository, scopes);

        // Act
        var result = await service.UpdateConfigurationAsync(new UpdateConfigurationRequest(null));

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data!.McpPublicBaseUrl);
        Assert.Equal(1, scopes.SaveChangesCallCount);
        Assert.Null(await repository.GetByKeyAsync("mcp_public_base_url"));
    }

    [Fact]
    public async Task UpdateConfigurationAsync_WithInvalidUrl_ShouldReturnBadRequestAndLeaveStateUnchanged()
    {
        // Arrange
        var repository = new InMemoryAppSettingRepository();
        repository.Add(new EntityAppSetting
        {
            Key = "mcp_public_base_url",
            Value = "https://stable.example.com",
            UpdatedAtUtc = DateTime.UtcNow
        });
        var scopes = new FakeDbContextScopeFactory();
        var service = CreateService(repository, scopes);

        // Act
        var result = await service.UpdateConfigurationAsync(new UpdateConfigurationRequest("relative/path"));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(0, scopes.SaveChangesCallCount);

        var persisted = await repository.GetByKeyAsync("mcp_public_base_url");
        Assert.NotNull(persisted);
        Assert.Equal("https://stable.example.com", persisted!.Value);
    }

    private static ConfigurationService CreateService(InMemoryAppSettingRepository repository, FakeDbContextScopeFactory scopes)
    {
        var jwtOptions = new JwtAuthOptions
        {
            AllowInsecureCookies = true
        };
        return new ConfigurationService(jwtOptions, TimeProvider.System, scopes, repository);
    }

    private sealed class InMemoryAppSettingRepository : IAppSettingRepository
    {
        private readonly List<EntityAppSetting> _settings = [];
        private int _nextId = 1;

        public IQueryable<EntityAppSetting> Query() => _settings.AsQueryable();

        public EntityAppSetting? Get(int id) => _settings.FirstOrDefault(x => x.Id == id);

        public void Add(EntityAppSetting entity)
        {
            if (entity.Id == 0)
            {
                entity.Id = _nextId++;
            }

            _settings.Add(entity);
        }

        public void AddRange(IEnumerable<EntityAppSetting> entities)
        {
            foreach (var entity in entities)
            {
                Add(entity);
            }
        }

        public void Remove(EntityAppSetting entity)
        {
            _settings.Remove(entity);
        }

        public void RemoveRange(IEnumerable<EntityAppSetting> entities)
        {
            foreach (var entity in entities.ToArray())
            {
                _settings.Remove(entity);
            }
        }

        public Task<EntityAppSetting?> GetByKeyAsync(string key)
        {
            var setting = _settings.SingleOrDefault(x => string.Equals(x.Key, key, StringComparison.Ordinal));
            return Task.FromResult(setting);
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
}
