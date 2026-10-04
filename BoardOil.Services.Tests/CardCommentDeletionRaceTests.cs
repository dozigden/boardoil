using System.Data;
using BoardOil.Abstractions;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef.Scope;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardCommentDeletionRaceTests : TestBaseDb
{
    private int _commentIdToDelete;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mutation_WhenCommentDeletedAfterRead_ShouldRejectUpdateButSucceedOnDeleteWithoutRecreatingIt(bool update)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var comment = new EntityCardComment { CardId = card.Id, AuthorUserId = ActorUserId, Text = "Original", PostedAtUtc = DateTime.UtcNow };
        DbContextForArrange.CardComments.Add(comment);
        await DbContextForArrange.SaveChangesAsync();
        _commentIdToDelete = comment.Id;
        var service = ResolveService<ICardCommentService>();

        var result = update
            ? await service.UpdateCommentAsync(board.BoardId, card.BoardCardId, comment.Id, new("Older draft"), ActorUserId)
            : await service.DeleteCommentAsync(board.BoardId, card.BoardCardId, comment.Id, ActorUserId);

        Assert.Equal(update ? 404 : 200, result.StatusCode);
        Assert.Empty(await DbContextForAssert.CardComments.ToListAsync());
        Assert.Equal(card.CardUpdatedUtc, (await DbContextForAssert.Cards.SingleAsync()).CardUpdatedUtc);
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        Assert.Empty(events.CommentUpdatedEvents);
        Assert.Empty(events.CommentDeletedEvents);
        Assert.Empty(events.CardUpdatedEvents);
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton<IDbContextScopeFactory>(provider => new DeleteBeforeSaveScopeFactory(
            new DbContextScopeFactory(provider.GetRequiredService<IDbContextFactory>()), async () =>
            {
                await using var deleter = CreateDbContextForAct();
                await deleter.CardComments.Where(x => x.Id == _commentIdToDelete).ExecuteDeleteAsync();
            }));
    }

    private sealed class DeleteBeforeSaveScopeFactory(IDbContextScopeFactory inner, Func<Task> beforeSave) : IDbContextScopeFactory
    {
        public IDbContextScope Create(DbContextScopeOption joiningOption = DbContextScopeOption.JoinExisting) => new DeleteBeforeSaveScope(inner.Create(joiningOption), beforeSave);
        public IDbContextReadOnlyScope CreateReadOnly(DbContextScopeOption joiningOption = DbContextScopeOption.JoinExisting) => inner.CreateReadOnly(joiningOption);
        public IDbContextScope CreateWithTransaction(IsolationLevel isolationLevel) => inner.CreateWithTransaction(isolationLevel);
        public IDbContextReadOnlyScope CreateReadOnlyWithTransaction(IsolationLevel isolationLevel) => inner.CreateReadOnlyWithTransaction(isolationLevel);
        public IDisposable SuppressAmbientContext() => inner.SuppressAmbientContext();
    }

    private sealed class DeleteBeforeSaveScope(IDbContextScope inner, Func<Task> beforeSave) : IDbContextScope
    {
        public IDbContextCollection DbContexts => inner.DbContexts;
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await beforeSave();
            return await inner.SaveChangesAsync(cancellationToken);
        }
        public async Task Transaction(Func<IDbContextTransactionScope, IDbContextTransaction, Task> executor)
        {
            await beforeSave();
            await inner.Transaction(executor);
        }
        public void Dispose() => inner.Dispose();
    }
}
