using BoardOil.Abstractions;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardTransferServiceTests : TestBaseDb
{
    [Fact]
    public async Task TransferCardAsync_WithDestinationDefaults_ShouldRehomeCardAndPreserveIdentityAndComments()
    {
        // Arrange
        var source = CreateBoard("Source").AddColumn("Todo").AddCard("Move me", "Description").Build();
        var destination = CreateBoard("Destination").AddColumn("Doing").AddCard("Existing").Build();
        var card = source.GetCard("Move me");
        var originalEntityId = card.Id;
        var originalBoardCardId = card.BoardCardId;
        var originalCreatedUtc = card.CardCreatedUtc;
        var sourceCardType = new EntityCardType
        {
            BoardId = source.BoardId,
            Name = "Bug",
            StyleName = "auto",
            StylePropertiesJson = "{}",
            IsSystem = false,
        };
        card.CardType = sourceCardType;
        card.CardTags.Add(new EntityCardTag { Tag = CreateTag(source.BoardId, "Feature", "auto") });
        card.Slick = CreateSlick(source.BoardId, "Release", "solid");
        card.AssignedUserId = ActorUserId;
        card.Comments.Add(new EntityCardComment
        {
            AuthorUserId = ActorUserId,
            Text = "Keep this",
            PostedAtUtc = DateTime.UtcNow,
        });
        await DbContextForArrange.SaveChangesAsync();
        var destinationDefaultCardTypeId = await DbContextForArrange.CardTypes
            .Where(x => x.BoardId == destination.BoardId && x.IsSystem)
            .Select(x => x.Id)
            .SingleAsync();
        var service = ResolveService<ICardService>();
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());

        // Act
        var result = await service.TransferCardAsync(
            source.BoardId,
            originalBoardCardId,
            new TransferCardRequest(
                destination.BoardId,
                destination.GetColumn("Doing").Id,
                CardTransferPolicies.DestinationDefaults),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(destination.BoardId, result.Data!.BoardId);
        Assert.Equal(2, result.Data.Card.Id);
        Assert.Equal(ActorUserId, result.Data.Card.AssignedUserId);
        Assert.Equal(originalCreatedUtc, result.Data.Card.CardCreatedUtc);
        Assert.Equal(destinationDefaultCardTypeId, result.Data.Card.CardTypeId);
        Assert.Empty(result.Data.Card.Tags);
        Assert.Null(result.Data.Card.SlickId);
        var stored = await DbContextForAssert.Cards
            .Include(x => x.Comments)
            .SingleAsync(x => x.Id == originalEntityId);
        Assert.Equal(destination.BoardId, stored.BoardId);
        Assert.Equal(destination.GetColumn("Doing").Id, stored.BoardColumnId);
        Assert.Single(stored.Comments);
        Assert.Equal("Keep this", stored.Comments.Single().Text);
        Assert.Equal(originalEntityId, stored.Id);
        Assert.Equal(2, stored.BoardCardId);
        Assert.Equal(["Move me", "Existing"], await GetOrderedTitlesAsync(DbContextForAssert, destination.GetColumn("Doing").Id));

        Assert.Equal([(source.BoardId, originalBoardCardId)], events.CardDeletedEvents);
        Assert.Equal(destination.BoardId, Assert.Single(events.CardCreatedEvents).BoardId);
    }

    [Fact]
    public async Task TransferCardAsync_WithKeepMatching_ShouldUseDestinationDefinitionsAndDropMissingContent()
    {
        // Arrange
        var source = CreateBoard("Source").AddColumn("Todo").AddCard("Move me").Build();
        var destination = CreateBoard("Destination").AddColumn("Doing").Build();
        var sourceCard = source.GetCard("Move me");
        sourceCard.CardType = new EntityCardType
        {
            BoardId = source.BoardId,
            Name = "Bug",
            StyleName = "auto",
            StylePropertiesJson = "{}",
            IsSystem = false,
        };
        var matchingSourceTag = CreateTag(source.BoardId, "Feature", "source-tag-style");
        var missingSourceTag = CreateTag(source.BoardId, "Missing", "missing-style");
        sourceCard.CardTags.Add(new EntityCardTag { Tag = matchingSourceTag });
        sourceCard.CardTags.Add(new EntityCardTag { Tag = missingSourceTag });
        sourceCard.Slick = CreateSlick(source.BoardId, "Release", "source-slick-style");
        var destinationTag = CreateTag(destination.BoardId, "feature", "destination-tag-style");
        var destinationSlick = CreateSlick(destination.BoardId, "release", "destination-slick-style");
        await DbContextForArrange.SaveChangesAsync();
        var destinationDefaultCardTypeId = await DbContextForArrange.CardTypes
            .Where(x => x.BoardId == destination.BoardId && x.IsSystem)
            .Select(x => x.Id)
            .SingleAsync();
        var service = ResolveService<ICardService>();
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());

        // Act
        var result = await service.TransferCardAsync(
            source.BoardId,
            sourceCard.BoardCardId,
            new TransferCardRequest(
                destination.BoardId,
                destination.GetColumn("Doing").Id,
                CardTransferPolicies.KeepMatching),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(destinationDefaultCardTypeId, result.Data!.Card.CardTypeId);
        Assert.Equal(["feature"], result.Data.Card.TagNames);
        Assert.Equal(destinationTag.Id, Assert.Single(result.Data.Card.Tags).Id);
        Assert.Equal(destinationSlick.Id, result.Data.Card.SlickId);
        Assert.Equal("destination-tag-style", result.Data.Card.Tags.Single().StyleName);
        Assert.Empty(events.ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task TransferCardAsync_WithCopyMissing_ShouldCopyCompleteDefinitionsAndClearInvalidAssignee()
    {
        // Arrange
        var source = CreateBoard("Source").AddColumn("Todo").AddCard("Move me").Build();
        var destination = CreateBoard("Destination").AddColumn("Doing").Build();
        var sourceCard = source.GetCard("Move me");
        var sourceCardType = new EntityCardType
        {
            BoardId = source.BoardId,
            Name = "Bug",
            Emoji = "🕷️",
            StyleName = "gradient",
            StylePropertiesJson = "{\"leftColor\":\"#111111\",\"rightColor\":\"#222222\"}",
            IsSystem = false,
        };
        var sourceTag = CreateTag(source.BoardId, "Urgent", "gradient", "🔥");
        var sourceSlick = CreateSlick(source.BoardId, "Release", "solid");
        sourceCard.CardType = sourceCardType;
        sourceCard.CardTags.Add(new EntityCardTag { Tag = sourceTag });
        sourceCard.Slick = sourceSlick;
        var sourceOnlyUser = new EntityUser
        {
            UserName = "source-only",
            Email = "source-only@localhost",
            NormalisedEmail = "source-only@localhost",
            PasswordHash = "test-hash",
            Role = UserRole.Standard,
            IsActive = true,
        };
        DbContextForArrange.Users.Add(sourceOnlyUser);
        DbContextForArrange.BoardMembers.Add(new EntityBoardMember
        {
            BoardId = source.BoardId,
            User = sourceOnlyUser,
            Role = BoardMemberRole.Contributor,
        });
        sourceCard.AssignedUser = sourceOnlyUser;
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardService>();
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());

        // Act
        var result = await service.TransferCardAsync(
            source.BoardId,
            sourceCard.BoardCardId,
            new TransferCardRequest(
                destination.BoardId,
                destination.GetColumn("Doing").Id,
                CardTransferPolicies.CopyMissing),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Data!.Card.AssignedUserId);
        var copiedCardType = await DbContextForAssert.CardTypes.SingleAsync(x =>
            x.BoardId == destination.BoardId && x.Name == "Bug");
        Assert.Equal("🕷️", copiedCardType.Emoji);
        Assert.Equal(sourceCardType.StylePropertiesJson, copiedCardType.StylePropertiesJson);
        Assert.False(copiedCardType.IsSystem);
        var copiedTag = await DbContextForAssert.Tags.SingleAsync(x =>
            x.BoardId == destination.BoardId && x.NormalisedName == "URGENT");
        Assert.Equal("🔥", copiedTag.Emoji);
        Assert.Equal(sourceTag.StylePropertiesJson, copiedTag.StylePropertiesJson);
        var copiedSlick = await DbContextForAssert.Slicks.SingleAsync(x =>
            x.BoardId == destination.BoardId && x.NormalisedName == "RELEASE");
        Assert.Equal(sourceSlick.StylePropertiesJson, copiedSlick.StylePropertiesJson);
        Assert.Equal([destination.BoardId], events.ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task TransferCardAsync_WithCopyMissingAsDestinationContributor_ShouldReturnForbiddenAndLeaveCardUnchanged()
    {
        // Arrange
        var source = CreateBoard("Source").AddColumn("Todo").AddCard("Move me").Build();
        var destination = CreateBoard("Destination").AddColumn("Doing").Build();
        var destinationMembership = await DbContextForArrange.BoardMembers.SingleAsync(x =>
            x.BoardId == destination.BoardId && x.UserId == ActorUserId);
        destinationMembership.Role = BoardMemberRole.Contributor;
        await DbContextForArrange.SaveChangesAsync();
        var card = source.GetCard("Move me");
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.TransferCardAsync(
            source.BoardId,
            card.BoardCardId,
            new TransferCardRequest(
                destination.BoardId,
                destination.GetColumn("Doing").Id,
                CardTransferPolicies.CopyMissing),
            ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        var stored = await DbContextForAssert.Cards.SingleAsync(x => x.Id == card.Id);
        Assert.Equal(source.BoardId, stored.BoardId);
        Assert.Equal(card.BoardCardId, stored.BoardCardId);
    }

    [Fact]
    public async Task TransferCardAsync_WithoutSourceMovePermission_ShouldReturnForbidden()
    {
        // Arrange
        var source = CreateBoard("Source").AddColumn("Todo").AddCard("Move me").Build();
        var destination = CreateBoard("Destination").AddColumn("Doing").Build();
        var sourceMembership = await DbContextForArrange.BoardMembers.SingleAsync(x =>
            x.BoardId == source.BoardId && x.UserId == ActorUserId);
        DbContextForArrange.BoardMembers.Remove(sourceMembership);
        await DbContextForArrange.SaveChangesAsync();
        var card = source.GetCard("Move me");
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.TransferCardAsync(
            source.BoardId,
            card.BoardCardId,
            new TransferCardRequest(
                destination.BoardId,
                destination.GetColumn("Doing").Id,
                CardTransferPolicies.KeepMatching),
            ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task TransferCardAsync_WithoutDestinationCreatePermission_ShouldReturnForbidden()
    {
        // Arrange
        var source = CreateBoard("Source").AddColumn("Todo").AddCard("Move me").Build();
        var destination = CreateBoard("Destination").AddColumn("Doing").Build();
        var destinationMembership = await DbContextForArrange.BoardMembers.SingleAsync(x =>
            x.BoardId == destination.BoardId && x.UserId == ActorUserId);
        DbContextForArrange.BoardMembers.Remove(destinationMembership);
        await DbContextForArrange.SaveChangesAsync();
        var card = source.GetCard("Move me");
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.TransferCardAsync(
            source.BoardId,
            card.BoardCardId,
            new TransferCardRequest(
                destination.BoardId,
                destination.GetColumn("Doing").Id,
                CardTransferPolicies.KeepMatching),
            ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task TransferCardAsync_WithInvalidPolicy_ShouldReturnValidationErrorAndLeaveCardUnchanged()
    {
        // Arrange
        var source = CreateBoard("Source").AddColumn("Todo").AddCard("Move me").Build();
        var destination = CreateBoard("Destination").AddColumn("Doing").Build();
        var card = source.GetCard("Move me");
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.TransferCardAsync(
            source.BoardId,
            card.BoardCardId,
            new TransferCardRequest(destination.BoardId, destination.GetColumn("Doing").Id, "unknown"),
            ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("transferPolicy", result.ValidationErrors!.Keys);
        var stored = await DbContextForAssert.Cards.SingleAsync(x => x.Id == card.Id);
        Assert.Equal(source.BoardId, stored.BoardId);
    }

    [Fact]
    public async Task TransferCardAsync_WhenDestinationNumberConflicts_ShouldRollBackEntireTransfer()
    {
        // Arrange
        var source = CreateBoard("Source").AddColumn("Todo").AddCard("Move me").Build();
        var destination = CreateBoard("Destination").AddColumn("Doing").AddCard("Existing").Build();
        var card = source.GetCard("Move me");
        var destinationSequence = await DbContextForArrange.BoardCardIdSequences
            .SingleAsync(x => x.BoardId == destination.BoardId);
        destinationSequence.NextCardId = 1;
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.TransferCardAsync(
            source.BoardId,
            card.BoardCardId,
            new TransferCardRequest(
                destination.BoardId,
                destination.GetColumn("Doing").Id,
                CardTransferPolicies.DestinationDefaults),
            ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        var stored = await DbContextForAssert.Cards.SingleAsync(x => x.Id == card.Id);
        Assert.Equal(source.BoardId, stored.BoardId);
        Assert.Equal(source.GetColumn("Todo").Id, stored.BoardColumnId);
        Assert.Equal(card.BoardCardId, stored.BoardCardId);
        var persistedNextCardId = await DbContextForAssert.BoardCardIdSequences
            .Where(x => x.BoardId == destination.BoardId)
            .Select(x => x.NextCardId)
            .SingleAsync();
        Assert.Equal(1, persistedNextCardId);
    }

    private EntityTag CreateTag(int boardId, string name, string styleName, string? emoji = null)
    {
        var tag = new EntityTag
        {
            BoardId = boardId,
            Name = name,
            NormalisedName = name.Trim().ToUpperInvariant(),
            Emoji = emoji,
            StyleName = styleName,
            StylePropertiesJson = "{\"presetIndex\":1}",
        };
        DbContextForArrange.Tags.Add(tag);
        return tag;
    }

    private EntitySlick CreateSlick(int boardId, string name, string styleName)
    {
        var slick = new EntitySlick
        {
            BoardId = boardId,
            Name = name,
            NormalisedName = name.Trim().ToUpperInvariant(),
            StyleName = styleName,
            StylePropertiesJson = "{\"presetIndex\":2}",
        };
        DbContextForArrange.Slicks.Add(slick);
        return slick;
    }
}

public sealed class CardTransferConcurrencyServiceTests : TestBaseDb
{
    [Fact]
    public async Task TransferCardAsync_WhenPersistenceReportsConcurrency_ShouldReturnConflictAndLeaveCardUnchanged()
    {
        // Arrange
        var source = CreateBoard("Source").AddColumn("Todo").AddCard("Move me").Build();
        var destination = CreateBoard("Destination").AddColumn("Doing").Build();
        var card = source.GetCard("Move me");
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.TransferCardAsync(
            source.BoardId,
            card.BoardCardId,
            new TransferCardRequest(
                destination.BoardId,
                destination.GetColumn("Doing").Id,
                CardTransferPolicies.KeepMatching),
            ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(
            "The card or destination changed while the card was being moved. Reload and try again.",
            result.Message);
        var stored = await DbContextForAssert.Cards.SingleAsync(x => x.Id == card.Id);
        Assert.Equal(source.BoardId, stored.BoardId);
        Assert.Equal(card.BoardCardId, stored.BoardCardId);
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IBoardCardIdAllocator>();
        services.AddScoped<IBoardCardIdAllocator, ConcurrencyThrowingCardIdAllocator>();
    }

    private sealed class ConcurrencyThrowingCardIdAllocator : IBoardCardIdAllocator
    {
        public Task<int> AllocateNextAsync(int boardId, CancellationToken cancellationToken = default) =>
            Task.FromException<int>(new ConcurrencyException("The card changed."));
    }
}
