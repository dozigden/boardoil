using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.CardType;
using BoardOil.Data.Abstractions.Column;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Slick;
using BoardOil.Data.Abstractions.Tag;
using BoardOil.Services.Card;
using BoardOil.Services.Ordering;
using BoardOil.Services.Tag;

namespace BoardOil.Services.Board.Import;

public sealed class BoardPackageImportWriter(
    IBoardRepository boardRepository,
    IColumnRepository columnRepository,
    ICardRepository cardRepository,
    ICardCommentRepository cardCommentRepository,
    IArchivedCardRepository archivedCardRepository,
    ICardTypeRepository cardTypeRepository,
    ITagRepository tagRepository,
    ISlickRepository slickRepository,
    ImportedUserResolver importedUserResolver,
    IDbContextScopeFactory scopeFactory)
{
    public async Task<ApiResult<BoardDto>> PersistBoardPackageImportAsync(BoardPackageImportPlan importPlan, int actorUserId)
    {
        importedUserResolver.Reset();
        var sortKeyPlanResult = CreateSortKeyPlan(importPlan.Columns);
        if (sortKeyPlanResult.Error is not null)
        {
            return sortKeyPlanResult.Error;
        }
        var sortKeyPlan = sortKeyPlanResult.Plan!;

        using var scope = scopeFactory.Create();

        var now = DateTime.UtcNow;
        var board = new EntityBoard
        {
            Name = importPlan.BoardName,
            Description = importPlan.BoardDescription,
            SlickCohesionModeEnabled = importPlan.SlickCohesionModeEnabled
        };

        board.Members.Add(new EntityBoardMember
        {
            UserId = actorUserId,
            Role = BoardMemberRole.Owner,
        });

        boardRepository.Add(board);

        var systemCardType = CardTypeDefaults.CreateSystemForBoard(board, now);
        board.CardTypes.Add(systemCardType);
        systemCardType.Name = importPlan.SystemCardTypeName;
        systemCardType.Emoji = importPlan.SystemCardTypeEmoji;
        systemCardType.StyleName = importPlan.SystemCardTypeStyleName;
        systemCardType.StylePropertiesJson = importPlan.SystemCardTypeStylePropertiesJson;
        var cardTypesByNormalisedName = new Dictionary<string, EntityCardType>(StringComparer.Ordinal)
        {
            [importPlan.SystemCardTypeNormalisedName] = systemCardType
        };

        foreach (var cardType in importPlan.CardTypes)
        {
            var createdCardType = new EntityCardType
            {
                Board = board,
                Name = cardType.Name,
                Emoji = cardType.Emoji,
                StyleName = cardType.StyleName,
                StylePropertiesJson = cardType.StylePropertiesJson,
                IsSystem = false,
            };

            cardTypeRepository.Add(createdCardType);
            cardTypesByNormalisedName.Add(cardType.NormalisedName, createdCardType);
        }

        var tagsByNormalisedName = new Dictionary<string, EntityTag>(StringComparer.Ordinal);
        foreach (var tagDefinition in importPlan.TagDefinitions)
        {
            var createdTag = new EntityTag
            {
                Board = board,
                Name = tagDefinition.Name,
                NormalisedName = tagDefinition.NormalisedName,
                StyleName = tagDefinition.StyleName,
                StylePropertiesJson = tagDefinition.StylePropertiesJson,
                Emoji = tagDefinition.Emoji,
            };

            tagRepository.Add(createdTag);
            tagsByNormalisedName.Add(tagDefinition.NormalisedName, createdTag);
        }

        var slicksByNormalisedName = new Dictionary<string, EntitySlick>(StringComparer.Ordinal);
        foreach (var slickDefinition in importPlan.SlickDefinitions)
        {
            var createdSlick = new EntitySlick
            {
                Board = board,
                Name = slickDefinition.Name,
                NormalisedName = slickDefinition.NormalisedName,
                StyleName = slickDefinition.StyleName,
                StylePropertiesJson = slickDefinition.StylePropertiesJson,
            };

            slickRepository.Add(createdSlick);
            slicksByNormalisedName.Add(slickDefinition.NormalisedName, createdSlick);
        }

        var createdColumns = new List<EntityBoardColumn>(importPlan.Columns.Count);
        var createdCardsByColumn = new Dictionary<EntityBoardColumn, List<EntityBoardCard>>();

        for (var columnIndex = 0; columnIndex < importPlan.Columns.Count; columnIndex++)
        {
            var importedColumn = importPlan.Columns[columnIndex];
            var createdColumn = new EntityBoardColumn
            {
                Board = board,
                Title = importedColumn.Title,
                SortKey = sortKeyPlan.ColumnKeys[columnIndex],
            };
            columnRepository.Add(createdColumn);
            createdColumns.Add(createdColumn);

            var createdCards = new List<EntityBoardCard>(importedColumn.Cards.Count);

            for (var cardIndex = 0; cardIndex < importedColumn.Cards.Count; cardIndex++)
            {
                var importedCard = importedColumn.Cards[cardIndex];
                var assignedUser = await importedUserResolver.ResolveImportedAssignedUserAsync(importedCard.AssignedUserNormalisedEmail);
                var createdCard = new EntityBoardCard
                {
                    BoardColumn = createdColumn,
                    CardType = cardTypesByNormalisedName[importedCard.CardTypeNormalisedName],
                    AssignedUserId = assignedUser?.Id,
                    AssignedUser = assignedUser,
                    Slick = importedCard.SlickNormalisedName is null
                        ? null
                        : slicksByNormalisedName[importedCard.SlickNormalisedName],
                    Title = importedCard.Title,
                    Description = importedCard.Description,
                    ExternalUrl = importedCard.ExternalUrl,
                    SortKey = sortKeyPlan.CardKeysByColumn[columnIndex][cardIndex],
                };

                foreach (var importedTagName in importedCard.TagNames)
                {
                    var normalisedTagName = BoardPackageImportNormalisation.NormaliseTagName(importedTagName);
                    if (!tagsByNormalisedName.TryGetValue(normalisedTagName, out var tag))
                    {
                        tag = new EntityTag
                        {
                            Board = board,
                            Name = importedTagName,
                            NormalisedName = normalisedTagName,
                            StyleName = TagStyleSchemaValidator.PresetsStyleName,
                            StylePropertiesJson = TagStyleSchemaValidator.BuildDefaultStylePropertiesJson(TagStyleSchemaValidator.PresetsStyleName),
                            Emoji = null,
                        };

                        tagRepository.Add(tag);
                        tagsByNormalisedName.Add(normalisedTagName, tag);
                    }

                    createdCard.CardTags.Add(new EntityCardTag { Tag = tag });
                }

                cardRepository.Add(createdCard);
                createdCards.Add(createdCard);

                foreach (var importedComment in importedCard.Comments)
                {
                    var commentAuthor = await importedUserResolver.ResolveImportedCommentAuthorAsync(importedComment.AuthorNormalisedEmail);
                    cardCommentRepository.Add(new EntityCardComment
                    {
                        Card = createdCard,
                        AuthorUserId = commentAuthor?.Id,
                        AuthorUser = commentAuthor,
                        Text = importedComment.Text,
                        PostedAtUtc = importedComment.PostedAtUtc
                    });
                }
            }

            createdCardsByColumn.Add(createdColumn, createdCards);
        }

        if (importPlan.ArchivedCards.Count > 0)
        {
            var assignedArchivedOriginalCardIds = await AllocateArchivedOriginalCardIdsAsync(importPlan.ArchivedCards);
            for (var archivedCardIndex = 0; archivedCardIndex < importPlan.ArchivedCards.Count; archivedCardIndex++)
            {
                var importedArchivedCard = importPlan.ArchivedCards[archivedCardIndex];
                var searchTagsJson = JsonSerializer.Serialize<IReadOnlyList<string>>(importedArchivedCard.TagNames);
                var searchTextNormalised = BoardPackageImportNormalisation.BuildArchiveSearchText(importedArchivedCard.Title, importedArchivedCard.TagNames);

                archivedCardRepository.Add(new EntityArchivedCard
                {
                    Board = board,
                    OriginalCardId = assignedArchivedOriginalCardIds[archivedCardIndex],
                    ArchivedAtUtc = importedArchivedCard.ArchivedAtUtc,
                    SnapshotJson = importedArchivedCard.SnapshotJson,
                    SearchTitle = importedArchivedCard.Title,
                    SearchTagsJson = searchTagsJson,
                    SearchTextNormalised = searchTextNormalised
                });
            }
        }

        await scope.SaveChangesAsync();

        var columnDtos = createdColumns
            .OrderBy(x => x.SortKey)
            .Select(column => new BoardColumnDto(
                column.Id,
                column.Title,
                column.SortKey,
                column.CreatedAtUtc,
                column.UpdatedAtUtc,
                createdCardsByColumn.GetValueOrDefault(column, [])
                    .OrderBy(card => card.SortKey)
                    .Select(card => card.ToCardDto())
                    .ToList()))
            .ToList();

        return ApiResults.Created(new BoardDto(
            board.Id,
            board.Name,
            board.Description,
            board.SlickCohesionModeEnabled,
            board.CreatedAtUtc,
            board.UpdatedAtUtc,
            BoardMemberRole.Owner.ToString(),
            columnDtos));
    }

    private static BoardPackageImportSortKeyPlanResult CreateSortKeyPlan(
        IReadOnlyList<ColumnImportDefinition> columns)
    {
        try
        {
            var columnKeys = SortKeyGenerator.CreateEvenlySpaced(columns.Count);
            var cardKeysByColumn = columns
                .Select(column => SortKeyGenerator.CreateEvenlySpaced(column.Cards.Count))
                .ToList();
            return new BoardPackageImportSortKeyPlanResult(
                new BoardPackageImportSortKeyPlan(columnKeys, cardKeysByColumn),
                null);
        }
        catch (InvalidOperationException)
        {
            return UnableToAllocateSortKeys();
        }
        catch (ArgumentException)
        {
            return UnableToAllocateSortKeys();
        }
    }

    private static BoardPackageImportSortKeyPlanResult UnableToAllocateSortKeys() =>
        new(
            null,
            ApiErrors.BadRequest("Board package contains too many columns or cards to allocate order keys."));

    private async Task<IReadOnlyList<int>> AllocateArchivedOriginalCardIdsAsync(IReadOnlyList<ArchivedCardImportDefinition> importedArchivedCards)
    {
        var requestedOriginalCardIds = importedArchivedCards
            .Select(x => x.OriginalCardId)
            .Distinct()
            .ToList();
        var existingOriginalCardIds = await archivedCardRepository.ListExistingOriginalCardIdsAsync(requestedOriginalCardIds);
        var nextFallbackOriginalCardId = await ResolveNextImportedArchivedOriginalCardIdAsync();
        var assignedOriginalCardIds = new HashSet<int>(existingOriginalCardIds);
        var assignedValues = new List<int>(importedArchivedCards.Count);

        foreach (var importedArchivedCard in importedArchivedCards)
        {
            var assignedOriginalCardId = importedArchivedCard.OriginalCardId;
            if (assignedOriginalCardId <= 0 || !assignedOriginalCardIds.Add(assignedOriginalCardId))
            {
                assignedOriginalCardId = nextFallbackOriginalCardId;
                while (!assignedOriginalCardIds.Add(assignedOriginalCardId))
                {
                    assignedOriginalCardId--;
                }

                nextFallbackOriginalCardId = assignedOriginalCardId - 1;
            }

            assignedValues.Add(assignedOriginalCardId);
        }

        return assignedValues;
    }

    private async Task<int> ResolveNextImportedArchivedOriginalCardIdAsync()
    {
        var minimumOriginalCardId = await archivedCardRepository.GetMinimumOriginalCardIdAsync() ?? 0;
        return Math.Min(0, minimumOriginalCardId) - 1;
    }

    private sealed record BoardPackageImportSortKeyPlan(
        IReadOnlyList<string> ColumnKeys,
        IReadOnlyList<IReadOnlyList<string>> CardKeysByColumn);

    private sealed record BoardPackageImportSortKeyPlanResult(
        BoardPackageImportSortKeyPlan? Plan,
        ApiError? Error);
}
