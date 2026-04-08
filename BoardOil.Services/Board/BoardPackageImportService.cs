using System.IO.Compression;
using System.Text.Json;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.CardType;
using BoardOil.Persistence.Abstractions.Column;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Persistence.Abstractions.Tag;
using BoardOil.Services.Card;
using BoardOil.Services.Ordering;
using BoardOil.Services.Tag;

namespace BoardOil.Services.Board;

public sealed class BoardPackageImportService(
    IBoardRepository boardRepository,
    IColumnRepository columnRepository,
    ICardRepository cardRepository,
    ICardTypeRepository cardTypeRepository,
    ITagRepository tagRepository,
    IDbContextScopeFactory scopeFactory) : IBoardPackageImportService
{
    private const int MaxBoardNameLength = 120;
    private const int MaxColumnNameLength = 200;
    private const int MaxCardTitleLength = 200;
    private const int MaxCardDescriptionLength = 20_000;
    private const int MaxTagNameLength = 40;
    private const int MaxCardTypeNameLength = 40;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ApiResult<BoardDto>> ImportBoardPackageAsync(ImportBoardPackageRequest request, int actorUserId)
    {
        if (request.PackageContent is null || request.PackageContent.Length == 0)
        {
            return ValidationFail([new ValidationError("file", "Board package ZIP file is required.")]);
        }

        var readPackageResult = TryReadBoardPackage(request.PackageContent);
        if (readPackageResult.Error is not null)
        {
            return readPackageResult.Error;
        }

        var boardName = ResolveImportedBoardName(request.Name, readPackageResult.BoardPayload!.Name);
        var planResult = BuildBoardPackageImportPlan(boardName, readPackageResult.BoardPayload!);
        if (planResult.Error is not null)
        {
            return planResult.Error;
        }

        return await PersistBoardPackageImportAsync(planResult.Plan!, actorUserId);
    }

    private async Task<ApiResult<BoardDto>> PersistBoardPackageImportAsync(BoardPackageImportPlan importPlan, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        var now = DateTime.UtcNow;
        var board = new EntityBoard
        {
            Name = importPlan.BoardName,
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
        systemCardType.StyleName = importPlan.SystemCardTypeStyleName;
        systemCardType.StylePropertiesJson = importPlan.SystemCardTypeStylePropertiesJson;
        var cardTypesByNormalisedName = new Dictionary<string, EntityCardType>(StringComparer.Ordinal)
        {
            [NormaliseName(CardTypeDefaults.SystemTypeName)] = systemCardType
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
                CreatedAtUtc = now,
                UpdatedAtUtc = now
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
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            tagRepository.Add(createdTag);
            tagsByNormalisedName.Add(tagDefinition.NormalisedName, createdTag);
        }

        var createdColumns = new List<EntityBoardColumn>(importPlan.Columns.Count);
        var createdCardsByColumn = new Dictionary<EntityBoardColumn, List<EntityBoardCard>>();
        string? previousColumnSortKey = null;

        foreach (var importedColumn in importPlan.Columns)
        {
            var columnSortKey = SortKeyGenerator.Between(previousColumnSortKey, null);
            var createdColumn = new EntityBoardColumn
            {
                Board = board,
                Title = importedColumn.Title,
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
                    CardType = cardTypesByNormalisedName[importedCard.CardTypeNormalisedName],
                    Title = importedCard.Title,
                    Description = importedCard.Description,
                    SortKey = cardSortKey,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                foreach (var importedTagName in importedCard.TagNames)
                {
                    var normalisedTagName = NormaliseTagName(importedTagName);
                    if (!tagsByNormalisedName.TryGetValue(normalisedTagName, out var tag))
                    {
                        tag = new EntityTag
                        {
                            Board = board,
                            Name = importedTagName,
                            NormalisedName = normalisedTagName,
                            StyleName = TagStyleSchemaValidator.SolidStyleName,
                            StylePropertiesJson = TagStyleSchemaValidator.BuildDefaultStylePropertiesJson(),
                            Emoji = null,
                            CreatedAtUtc = now,
                            UpdatedAtUtc = now
                        };

                        tagRepository.Add(tag);
                        tagsByNormalisedName.Add(normalisedTagName, tag);
                    }

                    createdCard.CardTags.Add(new EntityCardTag { Tag = tag });
                }

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
            board.CreatedAtUtc,
            board.UpdatedAtUtc,
            BoardMemberRole.Owner.ToString(),
            columnDtos));
    }

    private static ReadBoardPackageResult TryReadBoardPackage(byte[] packageContent)
    {
        try
        {
            using var stream = new MemoryStream(packageContent);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var manifestEntry = archive.GetEntry(BoardPackageContract.ManifestPath);
            if (manifestEntry is null)
            {
                return new ReadBoardPackageResult(
                    null,
                    null,
                    ValidationFail([new ValidationError("manifest", $"Board package is missing '{BoardPackageContract.ManifestPath}'.")]));
            }

            BoardPackageManifestDto? manifest;
            using (var manifestReader = new StreamReader(manifestEntry.Open()))
            {
                var manifestJson = manifestReader.ReadToEnd();
                manifest = JsonSerializer.Deserialize<BoardPackageManifestDto>(manifestJson, JsonOptions);
            }

            if (manifest is null)
            {
                return new ReadBoardPackageResult(
                    null,
                    null,
                    ValidationFail([new ValidationError("manifest", "Board package manifest is invalid JSON.")]));
            }

            if (manifest.Entries is null)
            {
                return new ReadBoardPackageResult(
                    null,
                    null,
                    ValidationFail([new ValidationError("manifest.entries", "Manifest entries are required.")]));
            }

            var manifestValidationError = BoardPackageContract.ValidateManifest(manifest);
            if (manifestValidationError is not null)
            {
                return new ReadBoardPackageResult(
                    manifest,
                    null,
                    manifestValidationError);
            }

            var boardEntry = archive.GetEntry(BoardPackageContract.BoardEntryPath);
            if (boardEntry is null)
            {
                return new ReadBoardPackageResult(
                    null,
                    null,
                    ValidationFail([new ValidationError("board", $"Board package is missing '{BoardPackageContract.BoardEntryPath}'.")]));
            }

            BoardPackageBoardDto? boardPayload;
            using (var boardReader = new StreamReader(boardEntry.Open()))
            {
                var boardJson = boardReader.ReadToEnd();
                var parseBoardPayloadResult = TryParseBoardPayload(manifest.SchemaVersion, boardJson);
                if (parseBoardPayloadResult.Error is not null)
                {
                    return new ReadBoardPackageResult(
                        manifest,
                        null,
                        parseBoardPayloadResult.Error);
                }

                boardPayload = parseBoardPayloadResult.BoardPayload;
            }

            if (boardPayload is null)
            {
                return new ReadBoardPackageResult(
                    null,
                    null,
                    ValidationFail([new ValidationError("board", "Board payload is invalid JSON.")]));
            }

            return new ReadBoardPackageResult(manifest, boardPayload, null);
        }
        catch (InvalidDataException)
        {
            return new ReadBoardPackageResult(
                null,
                null,
                ValidationFail([new ValidationError("file", "Uploaded file is not a valid ZIP archive.")]));
        }
        catch (JsonException)
        {
            return new ReadBoardPackageResult(
                null,
                null,
                ValidationFail([new ValidationError("file", "Board package JSON content is invalid.")]));
        }
    }

    private static ParseBoardPayloadResult TryParseBoardPayload(int schemaVersion, string boardJson)
    {
        switch (schemaVersion)
        {
            case 1:
            {
                var boardPayload = JsonSerializer.Deserialize<BoardPackageBoardDto>(boardJson, JsonOptions);
                return new ParseBoardPayloadResult(boardPayload, null);
            }
            default:
                return new ParseBoardPayloadResult(
                    null,
                    ValidationFail([new ValidationError(
                        "manifest.schemaVersion",
                        $"Schema version '{schemaVersion}' does not have an import payload handler configured.")]));
        }
    }

    private static BuildImportPlanResult BuildBoardPackageImportPlan(string boardName, BoardPackageBoardDto boardPayload)
    {
        var validationErrors = new List<ValidationError>();

        ValidateBoardName(boardName, "name", validationErrors);

        var packageCardTypes = boardPayload.CardTypes;
        if (packageCardTypes is null)
        {
            validationErrors.Add(new ValidationError("board.cardTypes", "Board card types are required."));
        }

        var packageTags = boardPayload.Tags;
        if (packageTags is null)
        {
            validationErrors.Add(new ValidationError("board.tags", "Board tags are required."));
        }

        var packageColumns = boardPayload.Columns;
        if (packageColumns is null)
        {
            validationErrors.Add(new ValidationError("board.columns", "Board columns are required."));
        }

        var plannedCardTypes = new List<CardTypeImportDefinition>();
        var systemCardTypeStyleName = CardTypeDefaults.DefaultStyleName;
        var systemCardTypeStylePropertiesJson = CardTypeDefaults.DefaultStylePropertiesJson;
        var knownCardTypeNames = new HashSet<string>(StringComparer.Ordinal)
        {
            NormaliseName(CardTypeDefaults.SystemTypeName)
        };

        if (packageCardTypes is not null)
        {
            for (var cardTypeIndex = 0; cardTypeIndex < packageCardTypes.Count; cardTypeIndex++)
            {
                var importedCardType = packageCardTypes[cardTypeIndex];
                var cardTypePropertyPrefix = $"board.cardTypes[{cardTypeIndex}]";

                if (importedCardType is null)
                {
                    validationErrors.Add(new ValidationError(cardTypePropertyPrefix, "Card type entry is required."));
                    continue;
                }

                var cardTypeNameValidation = ValidateCardTypeName(importedCardType.Name, $"{cardTypePropertyPrefix}.name");
                if (cardTypeNameValidation.Error is not null)
                {
                    validationErrors.Add(cardTypeNameValidation.Error);
                    continue;
                }

                var emojiValidation = TagEmojiValidator.ValidateAndNormalise(importedCardType.Emoji, $"{cardTypePropertyPrefix}.emoji");
                if (emojiValidation.Error is not null)
                {
                    validationErrors.Add(emojiValidation.Error);
                    continue;
                }

                var isSystemType = string.Equals(
                    cardTypeNameValidation.NormalisedName,
                    NormaliseName(CardTypeDefaults.SystemTypeName),
                    StringComparison.Ordinal);

                if (isSystemType)
                {
                    if (!importedCardType.IsSystem)
                    {
                        validationErrors.Add(new ValidationError(
                            $"{cardTypePropertyPrefix}.name",
                            $"Card type '{cardTypeNameValidation.CanonicalName}' is reserved for the system card type."));
                    }

                    systemCardTypeStyleName = ResolveCardTypeStyleName(importedCardType.StyleName);
                    systemCardTypeStylePropertiesJson = ResolveCardTypeStylePropertiesJson(importedCardType.StylePropertiesJson);
                    continue;
                }

                if (importedCardType.IsSystem)
                {
                    validationErrors.Add(new ValidationError(
                        $"{cardTypePropertyPrefix}.isSystem",
                        $"Only '{CardTypeDefaults.SystemTypeName}' can be marked as a system card type."));
                    continue;
                }

                if (!knownCardTypeNames.Add(cardTypeNameValidation.NormalisedName))
                {
                    validationErrors.Add(new ValidationError(
                        $"{cardTypePropertyPrefix}.name",
                        $"Card type '{cardTypeNameValidation.CanonicalName}' is duplicated when compared case-insensitively."));
                    continue;
                }

                plannedCardTypes.Add(new CardTypeImportDefinition(
                    cardTypeNameValidation.CanonicalName,
                    cardTypeNameValidation.NormalisedName,
                    emojiValidation.CanonicalEmoji,
                    ResolveCardTypeStyleName(importedCardType.StyleName),
                    ResolveCardTypeStylePropertiesJson(importedCardType.StylePropertiesJson)));
            }
        }

        var plannedTagDefinitionsByNormalisedName = new Dictionary<string, TagImportDefinition>(StringComparer.Ordinal);

        if (packageTags is not null)
        {
            for (var tagIndex = 0; tagIndex < packageTags.Count; tagIndex++)
            {
                var importedTag = packageTags[tagIndex];
                var tagPropertyPrefix = $"board.tags[{tagIndex}]";

                if (importedTag is null)
                {
                    validationErrors.Add(new ValidationError(tagPropertyPrefix, "Tag entry is required."));
                    continue;
                }

                var tagNameValidation = ValidateTagName(importedTag.Name, $"{tagPropertyPrefix}.name");
                if (tagNameValidation.Error is not null)
                {
                    validationErrors.Add(tagNameValidation.Error);
                    continue;
                }

                var normalisedStyleName = TagStyleSchemaValidator.NormaliseStyleName(importedTag.StyleName);
                if (normalisedStyleName is null)
                {
                    validationErrors.Add(new ValidationError(
                        $"{tagPropertyPrefix}.styleName",
                        "Style name must be 'solid' or 'gradient'."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(importedTag.StylePropertiesJson))
                {
                    validationErrors.Add(new ValidationError(
                        $"{tagPropertyPrefix}.stylePropertiesJson",
                        "Style properties must be valid JSON."));
                    continue;
                }

                var styleErrors = TagStyleSchemaValidator.Validate(normalisedStyleName, importedTag.StylePropertiesJson)
                    .Select(styleError => new ValidationError(
                        $"{tagPropertyPrefix}.{styleError.Property}",
                        styleError.Message));
                validationErrors.AddRange(styleErrors);

                var emojiValidation = TagEmojiValidator.ValidateAndNormalise(importedTag.Emoji, $"{tagPropertyPrefix}.emoji");
                if (emojiValidation.Error is not null)
                {
                    validationErrors.Add(emojiValidation.Error);
                }

                if (validationErrors.Any(x => x.Property.StartsWith(tagPropertyPrefix, StringComparison.Ordinal)))
                {
                    continue;
                }

                if (plannedTagDefinitionsByNormalisedName.ContainsKey(tagNameValidation.NormalisedName))
                {
                    validationErrors.Add(new ValidationError(
                        $"{tagPropertyPrefix}.name",
                        $"Tag '{tagNameValidation.CanonicalName}' collides with another tag by case-insensitive name."));
                    continue;
                }

                plannedTagDefinitionsByNormalisedName.Add(
                    tagNameValidation.NormalisedName,
                    new TagImportDefinition(
                        tagNameValidation.CanonicalName,
                        tagNameValidation.NormalisedName,
                        normalisedStyleName,
                        importedTag.StylePropertiesJson,
                        emojiValidation.CanonicalEmoji));
            }
        }

        var plannedColumns = new List<ColumnImportDefinition>();

        if (packageColumns is not null)
        {
            for (var columnIndex = 0; columnIndex < packageColumns.Count; columnIndex++)
            {
                var importedColumn = packageColumns[columnIndex];
                var columnPropertyPrefix = $"board.columns[{columnIndex}]";
                if (importedColumn is null)
                {
                    validationErrors.Add(new ValidationError(columnPropertyPrefix, "Column entry is required."));
                    continue;
                }

                var columnTitle = importedColumn.Title?.Trim() ?? string.Empty;
                if (columnTitle.Length == 0)
                {
                    validationErrors.Add(new ValidationError($"{columnPropertyPrefix}.title", "Column title is required."));
                }
                else if (columnTitle.Length > MaxColumnNameLength)
                {
                    validationErrors.Add(new ValidationError(
                        $"{columnPropertyPrefix}.title",
                        $"Column title must be {MaxColumnNameLength} characters or fewer."));
                }

                if (importedColumn.Cards is null)
                {
                    validationErrors.Add(new ValidationError($"{columnPropertyPrefix}.cards", "Column cards are required."));
                    continue;
                }

                var plannedCards = new List<CardImportDefinition>(importedColumn.Cards.Count);

                for (var cardIndex = 0; cardIndex < importedColumn.Cards.Count; cardIndex++)
                {
                    var importedCard = importedColumn.Cards[cardIndex];
                    var cardPropertyPrefix = $"{columnPropertyPrefix}.cards[{cardIndex}]";
                    if (importedCard is null)
                    {
                        validationErrors.Add(new ValidationError(cardPropertyPrefix, "Card entry is required."));
                        continue;
                    }

                    var cardTitle = importedCard.Title?.Trim() ?? string.Empty;
                    if (cardTitle.Length == 0)
                    {
                        validationErrors.Add(new ValidationError($"{cardPropertyPrefix}.title", "Card title is required."));
                    }
                    else if (cardTitle.Length > MaxCardTitleLength)
                    {
                        validationErrors.Add(new ValidationError(
                            $"{cardPropertyPrefix}.title",
                            $"Card title must be {MaxCardTitleLength} characters or fewer."));
                    }

                    var cardDescription = importedCard.Description ?? string.Empty;
                    if (cardDescription.Length > MaxCardDescriptionLength)
                    {
                        validationErrors.Add(new ValidationError(
                            $"{cardPropertyPrefix}.description",
                            $"Card description must be {MaxCardDescriptionLength} characters or fewer."));
                    }

                    var cardTypeValidation = ValidateCardTypeName(importedCard.CardTypeName, $"{cardPropertyPrefix}.cardTypeName");
                    if (cardTypeValidation.Error is not null)
                    {
                        validationErrors.Add(cardTypeValidation.Error);
                    }
                    else if (!knownCardTypeNames.Contains(cardTypeValidation.NormalisedName))
                    {
                        validationErrors.Add(new ValidationError(
                            $"{cardPropertyPrefix}.cardTypeName",
                            $"Card type '{cardTypeValidation.CanonicalName}' does not exist in the package card type list."));
                    }

                    var canonicalTagNames = ValidateAndCanonicaliseCardTagNames(importedCard.TagNames, $"{cardPropertyPrefix}.tagNames", validationErrors);

                    if (validationErrors.Any(x => x.Property.StartsWith(cardPropertyPrefix, StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    plannedCards.Add(new CardImportDefinition(
                        cardTitle,
                        cardDescription,
                        cardTypeValidation.NormalisedName,
                        canonicalTagNames));
                }

                if (validationErrors.Any(x => x.Property.StartsWith(columnPropertyPrefix, StringComparison.Ordinal)))
                {
                    continue;
                }

                plannedColumns.Add(new ColumnImportDefinition(columnTitle, plannedCards));
            }
        }

        if (validationErrors.Count > 0)
        {
            return new BuildImportPlanResult(null, ValidationFail(validationErrors));
        }

        return new BuildImportPlanResult(
            new BoardPackageImportPlan(
                boardName,
                systemCardTypeStyleName,
                systemCardTypeStylePropertiesJson,
                plannedCardTypes,
                plannedTagDefinitionsByNormalisedName.Values.ToList(),
                plannedColumns),
            null);
    }

    private static string ResolveImportedBoardName(string? requestBoardName, string? importedBoardName)
    {
        var sourceName = string.IsNullOrWhiteSpace(requestBoardName)
            ? importedBoardName
            : requestBoardName;

        return sourceName?.Trim() ?? string.Empty;
    }

    private static void ValidateBoardName(string boardName, string property, ICollection<ValidationError> validationErrors)
    {
        if (string.IsNullOrWhiteSpace(boardName))
        {
            validationErrors.Add(new ValidationError(property, "Board name is required."));
            return;
        }

        if (boardName.Length > MaxBoardNameLength)
        {
            validationErrors.Add(new ValidationError(property, "Board name must be 120 characters or fewer."));
        }
    }

    private static IReadOnlyList<string> ValidateAndCanonicaliseCardTagNames(
        IReadOnlyList<string>? tagNames,
        string propertyPrefix,
        ICollection<ValidationError> validationErrors)
    {
        if (tagNames is null)
        {
            validationErrors.Add(new ValidationError(propertyPrefix, "Tag names are required."));
            return [];
        }

        var canonicalTagNames = new List<string>(tagNames.Count);
        var seenTagNames = new HashSet<string>(StringComparer.Ordinal);

        for (var tagIndex = 0; tagIndex < tagNames.Count; tagIndex++)
        {
            var tagNameValidation = ValidateTagName(tagNames[tagIndex], $"{propertyPrefix}[{tagIndex}]");
            if (tagNameValidation.Error is not null)
            {
                validationErrors.Add(tagNameValidation.Error);
                continue;
            }

            if (!seenTagNames.Add(tagNameValidation.NormalisedName))
            {
                continue;
            }

            canonicalTagNames.Add(tagNameValidation.CanonicalName);
        }

        return canonicalTagNames;
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

    private static CardTypeNameValidationResult ValidateCardTypeName(string? rawCardTypeName, string property)
    {
        if (string.IsNullOrWhiteSpace(rawCardTypeName))
        {
            return new CardTypeNameValidationResult(
                string.Empty,
                string.Empty,
                new ValidationError(property, "Card type name is required."));
        }

        var canonicalCardTypeName = rawCardTypeName.Trim();
        if (canonicalCardTypeName.Length > MaxCardTypeNameLength)
        {
            return new CardTypeNameValidationResult(
                string.Empty,
                string.Empty,
                new ValidationError(property, "Card type name must be 40 characters or fewer."));
        }

        return new CardTypeNameValidationResult(
            canonicalCardTypeName,
            NormaliseName(canonicalCardTypeName),
            null);
    }

    private static string NormaliseTagName(string tagName) =>
        tagName.ToUpperInvariant();

    private static string NormaliseName(string value) =>
        value.ToUpperInvariant();

    private static string ResolveCardTypeStyleName(string? styleName)
    {
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return CardTypeDefaults.DefaultStyleName;
        }

        return styleName.Trim();
    }

    private static string ResolveCardTypeStylePropertiesJson(string? stylePropertiesJson)
    {
        if (string.IsNullOrWhiteSpace(stylePropertiesJson))
        {
            return CardTypeDefaults.DefaultStylePropertiesJson;
        }

        return stylePropertiesJson.Trim();
    }

    private static ApiError ValidationFail(IReadOnlyList<ValidationError> validationErrors) =>
        ApiErrors.BadRequest("Validation failed.", validationErrors);

    private sealed record ReadBoardPackageResult(
        BoardPackageManifestDto? Manifest,
        BoardPackageBoardDto? BoardPayload,
        ApiError? Error);

    private sealed record ParseBoardPayloadResult(
        BoardPackageBoardDto? BoardPayload,
        ApiError? Error);

    private sealed record BuildImportPlanResult(
        BoardPackageImportPlan? Plan,
        ApiError? Error);

    private sealed record BoardPackageImportPlan(
        string BoardName,
        string SystemCardTypeStyleName,
        string SystemCardTypeStylePropertiesJson,
        IReadOnlyList<CardTypeImportDefinition> CardTypes,
        IReadOnlyList<TagImportDefinition> TagDefinitions,
        IReadOnlyList<ColumnImportDefinition> Columns);

    private sealed record CardTypeImportDefinition(
        string Name,
        string NormalisedName,
        string? Emoji,
        string StyleName,
        string StylePropertiesJson);

    private sealed record TagImportDefinition(
        string Name,
        string NormalisedName,
        string StyleName,
        string StylePropertiesJson,
        string? Emoji);

    private sealed record ColumnImportDefinition(
        string Title,
        IReadOnlyList<CardImportDefinition> Cards);

    private sealed record CardImportDefinition(
        string Title,
        string Description,
        string CardTypeNormalisedName,
        IReadOnlyList<string> TagNames);

    private sealed record TagNameValidationResult(
        string CanonicalName,
        string NormalisedName,
        ValidationError? Error);

    private sealed record CardTypeNameValidationResult(
        string CanonicalName,
        string NormalisedName,
        ValidationError? Error);
}
