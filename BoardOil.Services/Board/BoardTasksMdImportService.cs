using System.Text.Json;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.Column;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Persistence.Abstractions.Tag;
using BoardOil.Services.Card;
using BoardOil.Services.Ordering;
using BoardOil.Services.Tag;
using BoardOil.TasksMd;

namespace BoardOil.Services.Board;

public sealed class BoardTasksMdImportService(
    IBoardRepository boardRepository,
    IColumnRepository columnRepository,
    ICardRepository cardRepository,
    ITagRepository tagRepository,
    ITasksMdClient tasksMdClient,
    IDbContextScopeFactory scopeFactory) : IBoardTasksMdImportService
{
    private const int MaxBoardNameLength = 120;
    private const int MaxColumnNameLength = 200;
    private const int MaxCardTitleLength = 200;
    private const int MaxCardDescriptionLength = 20_000;
    private const int MaxTagNameLength = 40;

    public async Task<ApiResult<BoardDto>> ImportTasksMdBoardAsync(ImportTasksMdBoardRequest request, int actorUserId)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return ValidationFail([new ValidationError("url", "tasksmd URL is required.")]);
        }

        if (!Uri.TryCreate(request.Url.Trim(), UriKind.Absolute, out var sourceUrl))
        {
            return ValidationFail([new ValidationError("url", "tasksmd URL must be absolute.")]);
        }

        if (sourceUrl.Scheme is not ("http" or "https"))
        {
            return ValidationFail([new ValidationError("url", "tasksmd URL must use http or https.")]);
        }

        TasksMdBoardImportModel importModel;
        try
        {
            importModel = await tasksMdClient.LoadBoardAsync(sourceUrl);
        }
        catch (TasksMdClientException exception)
        {
            var validationErrors = exception.ValidationErrors
                .Select(x => new ValidationError(x.Property, x.Message))
                .ToList();
            if (validationErrors.Count == 0)
            {
                validationErrors.Add(new ValidationError("url", exception.Message));
            }

            return ValidationFail(validationErrors);
        }

        var boardName = sourceUrl.Host;
        var validationResult = ValidateTasksMdImportModel(boardName, importModel);
        if (validationResult is not null)
        {
            return validationResult;
        }

        using var scope = scopeFactory.Create();

        var now = DateTime.UtcNow;
        var board = new EntityBoard
        {
            Name = boardName,
            Description = string.Empty,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        board.Members.Add(new EntityBoardMember
        {
            UserId = actorUserId,
            Role = BoardMemberRole.Owner,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        boardRepository.Add(board);
        var systemCardType = CardTypeDefaults.CreateSystemForBoard(board, now);
        board.CardTypes.Add(systemCardType);

        var tagsByNormalisedName = new Dictionary<string, EntityTag>(StringComparer.Ordinal);
        foreach (var importedTag in importModel.Tags)
        {
            var canonicalTagName = importedTag.Name.Trim();
            var normalisedTagName = NormaliseTagName(canonicalTagName);
            if (tagsByNormalisedName.ContainsKey(normalisedTagName))
            {
                continue;
            }

            var createdTag = CreateTag(board, canonicalTagName, importedTag.HexColor, now);
            tagRepository.Add(createdTag);
            tagsByNormalisedName.Add(normalisedTagName, createdTag);
        }

        var createdColumns = new List<EntityBoardColumn>(importModel.Columns.Count);
        var createdCardsByColumn = new Dictionary<EntityBoardColumn, List<EntityBoardCard>>();
        string? previousColumnSortKey = null;

        foreach (var importedColumn in importModel.Columns)
        {
            var columnSortKey = SortKeyGenerator.Between(previousColumnSortKey, null);
            var createdColumn = new EntityBoardColumn
            {
                Board = board,
                Title = importedColumn.Name.Trim(),
                SortKey = columnSortKey,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            columnRepository.Add(createdColumn);
            createdColumns.Add(createdColumn);
            previousColumnSortKey = columnSortKey;

            var createdCards = new List<EntityBoardCard>(importedColumn.Cards.Count);
            string? previousCardSortKey = null;
            foreach (var importedCard in importedColumn.Cards)
            {
                var cardSortKey = SortKeyGenerator.Between(previousCardSortKey, null);
                var createdCard = new EntityBoardCard
                {
                    BoardColumn = createdColumn,
                    CardType = systemCardType,
                    Title = importedCard.Name.Trim(),
                    Description = importedCard.Description,
                    SortKey = cardSortKey,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                ReplaceCardTags(createdCard, importedCard.TagNames, tagsByNormalisedName, board, now);

                cardRepository.Add(createdCard);
                createdCards.Add(createdCard);
                previousCardSortKey = cardSortKey;
            }

            createdCardsByColumn.Add(createdColumn, createdCards);
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
            board.CreatedAtUtc,
            board.UpdatedAtUtc,
            BoardMemberRole.Owner.ToString(),
            columnDtos));
    }

    private static EntityTag CreateTag(EntityBoard board, string name, string? hexColor, DateTime now)
    {
        var stylePropertiesJson = string.IsNullOrWhiteSpace(hexColor)
            ? TagStyleSchemaValidator.BuildDefaultStylePropertiesJson()
            : BuildSolidTagStyleProperties(hexColor.Trim());

        return new EntityTag
        {
            Board = board,
            Name = name,
            NormalisedName = NormaliseTagName(name),
            StyleName = TagStyleSchemaValidator.SolidStyleName,
            StylePropertiesJson = stylePropertiesJson,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private void ReplaceCardTags(
        EntityBoardCard card,
        IReadOnlyList<string> importedTagNames,
        Dictionary<string, EntityTag> tagsByNormalisedName,
        EntityBoard board,
        DateTime now)
    {
        card.CardTags.Clear();

        var seenCardTags = new HashSet<string>(StringComparer.Ordinal);
        foreach (var importedTagName in importedTagNames)
        {
            var canonicalTagName = importedTagName.Trim();
            var normalisedTagName = NormaliseTagName(canonicalTagName);
            if (!seenCardTags.Add(normalisedTagName))
            {
                continue;
            }

            if (!tagsByNormalisedName.TryGetValue(normalisedTagName, out var tag))
            {
                tag = CreateTag(board, canonicalTagName, null, now);
                tagRepository.Add(tag);
                tagsByNormalisedName.Add(normalisedTagName, tag);
            }

            card.CardTags.Add(new EntityCardTag { Tag = tag });
        }
    }

    private static ApiError? ValidateTasksMdImportModel(string boardName, TasksMdBoardImportModel importModel)
    {
        var validationErrors = new List<ValidationError>();

        ValidateBoardName(boardName, "url", validationErrors, "tasksmd URL host is required.");

        for (var columnIndex = 0; columnIndex < importModel.Columns.Count; columnIndex++)
        {
            var column = importModel.Columns[columnIndex];
            var columnName = column.Name.Trim();
            if (columnName.Length == 0)
            {
                validationErrors.Add(new ValidationError($"columns[{columnIndex}].name", "Column name is required."));
            }
            else if (columnName.Length > MaxColumnNameLength)
            {
                validationErrors.Add(new ValidationError($"columns[{columnIndex}].name", "Column name must be 200 characters or fewer."));
            }

            for (var cardIndex = 0; cardIndex < column.Cards.Count; cardIndex++)
            {
                var card = column.Cards[cardIndex];
                var cardName = card.Name.Trim();
                if (cardName.Length == 0)
                {
                    validationErrors.Add(new ValidationError($"columns[{columnIndex}].cards[{cardIndex}].name", "Card title is required."));
                }
                else if (cardName.Length > MaxCardTitleLength)
                {
                    validationErrors.Add(new ValidationError($"columns[{columnIndex}].cards[{cardIndex}].name", "Card title must be 200 characters or fewer."));
                }

                if (card.Description.Length > MaxCardDescriptionLength)
                {
                    validationErrors.Add(new ValidationError(
                        $"columns[{columnIndex}].cards[{cardIndex}].description",
                        $"Card description must be {MaxCardDescriptionLength} characters or fewer."));
                }

                ValidateTagNames(card.TagNames, $"columns[{columnIndex}].cards[{cardIndex}].tagNames", validationErrors);
            }
        }

        ValidateTagNames(importModel.Tags.Select(x => x.Name).ToList(), "tags", validationErrors);

        if (validationErrors.Count == 0)
        {
            return null;
        }

        return ValidationFail(validationErrors);
    }

    private static void ValidateBoardName(
        string boardName,
        string property,
        ICollection<ValidationError> validationErrors,
        string? emptyMessage = null)
    {
        if (string.IsNullOrWhiteSpace(boardName))
        {
            validationErrors.Add(new ValidationError(property, emptyMessage ?? "Board name is required."));
            return;
        }

        if (boardName.Length > MaxBoardNameLength)
        {
            validationErrors.Add(new ValidationError(property, "Board name must be 120 characters or fewer."));
        }
    }

    private static void ValidateTagNames(IReadOnlyList<string> tagNames, string propertyPrefix, ICollection<ValidationError> validationErrors)
    {
        for (var tagIndex = 0; tagIndex < tagNames.Count; tagIndex++)
        {
            var tagNameValidation = ValidateTagName(tagNames[tagIndex], $"{propertyPrefix}[{tagIndex}]");
            if (tagNameValidation.Error is not null)
            {
                validationErrors.Add(tagNameValidation.Error);
            }
        }
    }

    private static TagNameValidationResult ValidateTagName(string? rawTagName, string property)
    {
        if (string.IsNullOrWhiteSpace(rawTagName))
        {
            return new TagNameValidationResult(string.Empty, string.Empty, new ValidationError(property, "Tag name is required."));
        }

        var canonicalTagName = rawTagName.Trim();
        if (canonicalTagName.Contains(',', StringComparison.Ordinal))
        {
            return new TagNameValidationResult(string.Empty, string.Empty, new ValidationError(property, "Tag name must be a single value."));
        }

        if (canonicalTagName.Length > MaxTagNameLength)
        {
            return new TagNameValidationResult(
                string.Empty,
                string.Empty,
                new ValidationError(property, "Tag name must be 40 characters or fewer."));
        }

        return new TagNameValidationResult(canonicalTagName, NormaliseTagName(canonicalTagName), null);
    }

    private static string BuildSolidTagStyleProperties(string hexColor) =>
        JsonSerializer.Serialize(new
        {
            backgroundColor = hexColor,
            textColorMode = "auto"
        });

    private static string NormaliseTagName(string tagName) =>
        tagName.ToUpperInvariant();

    private static ApiError ValidationFail(IReadOnlyList<ValidationError> validationErrors) =>
        ApiErrors.BadRequest("Validation failed.", validationErrors);

    private sealed record TagNameValidationResult(
        string CanonicalName,
        string NormalisedName,
        ValidationError? Error);
}
