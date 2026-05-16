using System.Text;
using System.Text.Json;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Contracts;
using BoardOil.Services.Card;
using BoardOil.Services.Tag;

namespace BoardOil.Services.Board.Import;

public sealed class BoardPackageImportPlanner
{
    public BoardPackageImportPlanResult BuildBoardPackageImportPlan(
        string boardName,
        string boardDescription,
        BoardPackageBoardDto boardPayload,
        BoardPackageArchiveDto? archivePayload)
    {
        var validationErrors = new List<ValidationError>();

        ValidateBoardName(boardName, "name", validationErrors);
        ValidateBoardDescription(boardDescription, "description", validationErrors);

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
        var systemCardTypeName = CardTypeDefaults.SystemTypeName;
        var systemCardTypeNormalisedName = BoardPackageImportNormalisation.NormaliseName(CardTypeDefaults.SystemTypeName);
        string? systemCardTypeEmoji = null;
        var systemCardTypeStyleName = CardTypeDefaults.DefaultStyleName;
        var systemCardTypeStylePropertiesJson = CardTypeDefaults.DefaultStylePropertiesJson;
        var hasSystemCardType = false;
        var knownCardTypeNames = new HashSet<string>(StringComparer.Ordinal);

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

                var styleValidation = ResolveCardTypeStyle(importedCardType.StyleName, importedCardType.StylePropertiesJson, cardTypePropertyPrefix);
                if (styleValidation.Error is not null)
                {
                    validationErrors.Add(styleValidation.Error);
                    continue;
                }

                if (!knownCardTypeNames.Add(cardTypeNameValidation.NormalisedName))
                {
                    validationErrors.Add(new ValidationError(
                        $"{cardTypePropertyPrefix}.name",
                        $"Card type '{cardTypeNameValidation.CanonicalName}' is duplicated when compared case-insensitively."));
                    continue;
                }

                if (importedCardType.IsSystem)
                {
                    if (hasSystemCardType)
                    {
                        validationErrors.Add(new ValidationError(
                            $"{cardTypePropertyPrefix}.isSystem",
                            "Only one card type can be marked as a system card type."));
                        continue;
                    }

                    hasSystemCardType = true;
                    systemCardTypeName = cardTypeNameValidation.CanonicalName;
                    systemCardTypeNormalisedName = cardTypeNameValidation.NormalisedName;
                    systemCardTypeEmoji = emojiValidation.CanonicalEmoji;
                    systemCardTypeStyleName = styleValidation.StyleName;
                    systemCardTypeStylePropertiesJson = styleValidation.StylePropertiesJson;
                    continue;
                }

                plannedCardTypes.Add(new CardTypeImportDefinition(
                    cardTypeNameValidation.CanonicalName,
                    cardTypeNameValidation.NormalisedName,
                    emojiValidation.CanonicalEmoji,
                    styleValidation.StyleName,
                    styleValidation.StylePropertiesJson));
            }
        }

        var plannedTagDefinitionsByNormalisedName = new Dictionary<string, TagImportDefinition>(StringComparer.Ordinal);

        if (packageTags is not null)
        {
            for (var tagIndex = 0; tagIndex < packageTags.Count; tagIndex++)
            {
                var importedTag = packageTags[tagIndex];
                var tagPropertyPrefix = $"board.tags[{tagIndex}]";
                var errorCountBeforeTag = validationErrors.Count;

                if (importedTag is null)
                {
                    validationErrors.Add(new ValidationError(tagPropertyPrefix, "Tag entry is required."));
                    continue;
                }

                var tagNameValidation = ValidateTagName(importedTag.Name, $"{tagPropertyPrefix}.name");
                if (tagNameValidation.Error is not null)
                {
                    validationErrors.Add(tagNameValidation.Error);
                }

                var normalisedStyleName = TagStyleSchemaValidator.NormaliseStyleName(importedTag.StyleName);
                if (normalisedStyleName is null)
                {
                    validationErrors.Add(new ValidationError(
                        $"{tagPropertyPrefix}.styleName",
                        "Style name must be 'solid', 'gradient', 'auto', or 'presets'."));
                }

                if (string.IsNullOrWhiteSpace(importedTag.StylePropertiesJson))
                {
                    validationErrors.Add(new ValidationError(
                        $"{tagPropertyPrefix}.stylePropertiesJson",
                        "Style properties must be valid JSON object."));
                }
                else if (!TagStyleSchemaValidator.IsValidJsonObject(importedTag.StylePropertiesJson))
                {
                    validationErrors.Add(new ValidationError(
                        $"{tagPropertyPrefix}.stylePropertiesJson",
                        "Style properties must be valid JSON object."));
                }

                var emojiValidation = TagEmojiValidator.ValidateAndNormalise(importedTag.Emoji, $"{tagPropertyPrefix}.emoji");
                if (emojiValidation.Error is not null)
                {
                    validationErrors.Add(emojiValidation.Error);
                }

                if (validationErrors.Count > errorCountBeforeTag)
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
                        normalisedStyleName!,
                        importedTag.StylePropertiesJson!,
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
                var errorCountBeforeColumn = validationErrors.Count;

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
                else if (columnTitle.Length > BoardPackageImportLimits.MaxColumnNameLength)
                {
                    validationErrors.Add(new ValidationError(
                        $"{columnPropertyPrefix}.title",
                        $"Column title must be {BoardPackageImportLimits.MaxColumnNameLength} characters or fewer."));
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
                    var errorCountBeforeCard = validationErrors.Count;
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
                    else if (cardTitle.Length > BoardPackageImportLimits.MaxCardTitleLength)
                    {
                        validationErrors.Add(new ValidationError(
                            $"{cardPropertyPrefix}.title",
                            $"Card title must be {BoardPackageImportLimits.MaxCardTitleLength} characters or fewer."));
                    }

                    var cardDescription = importedCard.Description ?? string.Empty;
                    if (cardDescription.Length > BoardPackageImportLimits.MaxCardDescriptionLength)
                    {
                        validationErrors.Add(new ValidationError(
                            $"{cardPropertyPrefix}.description",
                            $"Card description must be {BoardPackageImportLimits.MaxCardDescriptionLength} characters or fewer."));
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
                    var plannedComments = ValidateAndCanonicaliseCardComments(
                        importedCard.Comments,
                        $"{cardPropertyPrefix}.comments",
                        validationErrors);

                    if (validationErrors.Count > errorCountBeforeCard)
                    {
                        continue;
                    }

                    plannedCards.Add(new CardImportDefinition(
                        cardTitle,
                        cardDescription,
                        cardTypeValidation.NormalisedName,
                        canonicalTagNames,
                        BoardPackageImportNormalisation.ResolveNormalisedEmailOrNull(importedCard.AssignedUserEmail),
                        plannedComments));
                }

                if (validationErrors.Count > errorCountBeforeColumn)
                {
                    continue;
                }

                plannedColumns.Add(new ColumnImportDefinition(columnTitle, plannedCards));
            }
        }

        var plannedArchivedCards = new List<ArchivedCardImportDefinition>();
        if (archivePayload is not null)
        {
            if (archivePayload.Cards is null)
            {
                validationErrors.Add(new ValidationError("archive.cards", "Archive cards are required."));
            }
            else
            {
                for (var archivedCardIndex = 0; archivedCardIndex < archivePayload.Cards.Count; archivedCardIndex++)
                {
                    var archivedCard = archivePayload.Cards[archivedCardIndex];
                    var archivedCardPropertyPrefix = $"archive.cards[{archivedCardIndex}]";
                    var errorCountBeforeArchivedCard = validationErrors.Count;

                    if (archivedCard is null)
                    {
                        validationErrors.Add(new ValidationError(archivedCardPropertyPrefix, "Archived card entry is required."));
                        continue;
                    }

                    var title = archivedCard.Title?.Trim() ?? string.Empty;
                    if (title.Length == 0)
                    {
                        validationErrors.Add(new ValidationError($"{archivedCardPropertyPrefix}.title", "Archived card title is required."));
                    }
                    else if (title.Length > BoardPackageImportLimits.MaxArchiveTitleLength)
                    {
                        validationErrors.Add(new ValidationError(
                            $"{archivedCardPropertyPrefix}.title",
                            $"Archived card title must be {BoardPackageImportLimits.MaxArchiveTitleLength} characters or fewer."));
                    }

                    var snapshotJson = archivedCard.SnapshotJson?.Trim() ?? string.Empty;
                    if (snapshotJson.Length == 0)
                    {
                        validationErrors.Add(new ValidationError($"{archivedCardPropertyPrefix}.snapshotJson", "Archived card snapshot JSON is required."));
                    }
                    else if (Encoding.UTF8.GetByteCount(snapshotJson) > BoardPackageImportLimits.MaxArchiveSnapshotJsonBytes)
                    {
                        validationErrors.Add(new ValidationError(
                            $"{archivedCardPropertyPrefix}.snapshotJson",
                            $"Archived card snapshot JSON must be {BoardPackageImportLimits.MaxArchiveSnapshotJsonBytes} bytes or fewer."));
                    }

                    if (archivedCard.ArchivedAtUtc == default)
                    {
                        validationErrors.Add(new ValidationError($"{archivedCardPropertyPrefix}.archivedAtUtc", "Archived at time is required."));
                    }

                    var canonicalTagNames = ValidateAndCanonicaliseCardTagNames(
                        archivedCard.TagNames,
                        $"{archivedCardPropertyPrefix}.tagNames",
                        validationErrors);
                    var searchTagsJson = JsonSerializer.Serialize<IReadOnlyList<string>>(canonicalTagNames);
                    if (searchTagsJson.Length > BoardPackageImportLimits.MaxArchiveSearchTagsJsonLength)
                    {
                        validationErrors.Add(new ValidationError(
                            $"{archivedCardPropertyPrefix}.tagNames",
                            "Archived card tags exceed the supported search payload size."));
                    }

                    var searchTextNormalised = BoardPackageImportNormalisation.BuildArchiveSearchText(title, canonicalTagNames);
                    if (searchTextNormalised.Length > BoardPackageImportLimits.MaxArchiveSearchTextNormalisedLength)
                    {
                        validationErrors.Add(new ValidationError(
                            $"{archivedCardPropertyPrefix}.tagNames",
                            "Archived card title and tags exceed the supported search payload size."));
                    }

                    if (validationErrors.Count > errorCountBeforeArchivedCard)
                    {
                        continue;
                    }

                    plannedArchivedCards.Add(new ArchivedCardImportDefinition(
                        archivedCard.OriginalCardId,
                        title,
                        canonicalTagNames,
                        archivedCard.ArchivedAtUtc,
                        snapshotJson));
                }
            }
        }

        if (validationErrors.Count > 0)
        {
            return new BoardPackageImportPlanResult(null, ApiErrors.ValidationFailed(validationErrors));
        }

        return new BoardPackageImportPlanResult(
            new BoardPackageImportPlan(
                boardName,
                boardDescription,
                systemCardTypeName,
                systemCardTypeNormalisedName,
                systemCardTypeEmoji,
                systemCardTypeStyleName,
                systemCardTypeStylePropertiesJson,
                plannedCardTypes,
                plannedTagDefinitionsByNormalisedName.Values.ToList(),
                plannedColumns,
                plannedArchivedCards),
            null);
    }

    private static void ValidateBoardName(string boardName, string property, ICollection<ValidationError> validationErrors)
    {
        if (string.IsNullOrWhiteSpace(boardName))
        {
            validationErrors.Add(new ValidationError(property, "Board name is required."));
            return;
        }

        if (boardName.Length > BoardPackageImportLimits.MaxBoardNameLength)
        {
            validationErrors.Add(new ValidationError(property, "Board name must be 120 characters or fewer."));
        }
    }

    private static void ValidateBoardDescription(string boardDescription, string property, ICollection<ValidationError> validationErrors)
    {
        if (boardDescription.Length > BoardPackageImportLimits.MaxBoardDescriptionLength)
        {
            validationErrors.Add(new ValidationError(property, $"Board description must be {BoardPackageImportLimits.MaxBoardDescriptionLength} characters or fewer."));
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

    private static IReadOnlyList<CommentImportDefinition> ValidateAndCanonicaliseCardComments(
        IReadOnlyList<BoardPackageCommentDto>? comments,
        string propertyPrefix,
        ICollection<ValidationError> validationErrors)
    {
        if (comments is null)
        {
            return [];
        }

        var canonicalComments = new List<CommentImportDefinition>(comments.Count);
        for (var commentIndex = 0; commentIndex < comments.Count; commentIndex++)
        {
            var importedComment = comments[commentIndex];
            var commentPropertyPrefix = $"{propertyPrefix}[{commentIndex}]";
            var errorCountBeforeComment = validationErrors.Count;

            if (importedComment is null)
            {
                validationErrors.Add(new ValidationError(commentPropertyPrefix, "Comment entry is required."));
                continue;
            }

            var canonicalText = importedComment.Text?.Trim() ?? string.Empty;
            if (canonicalText.Length == 0)
            {
                validationErrors.Add(new ValidationError($"{commentPropertyPrefix}.text", "Comment text is required."));
            }
            else if (canonicalText.Length > BoardPackageImportLimits.MaxCardCommentLength)
            {
                validationErrors.Add(new ValidationError(
                    $"{commentPropertyPrefix}.text",
                    $"Comment text must be {BoardPackageImportLimits.MaxCardCommentLength} characters or fewer."));
            }

            if (importedComment.PostedAtUtc == default)
            {
                validationErrors.Add(new ValidationError($"{commentPropertyPrefix}.postedAtUtc", "Comment posted time is required."));
            }

            if (validationErrors.Count > errorCountBeforeComment)
            {
                continue;
            }

            canonicalComments.Add(new CommentImportDefinition(
                canonicalText,
                importedComment.PostedAtUtc,
                BoardPackageImportNormalisation.ResolveNormalisedEmailOrNull(importedComment.AuthorEmail)));
        }

        return canonicalComments;
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

        if (canonicalTagName.Length > BoardPackageImportLimits.MaxTagNameLength)
        {
            return new TagNameValidationResult(
                string.Empty,
                string.Empty,
                new ValidationError(property, "Tag name must be 40 characters or fewer."));
        }

        return new TagNameValidationResult(canonicalTagName, BoardPackageImportNormalisation.NormaliseTagName(canonicalTagName), null);
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
        if (canonicalCardTypeName.Length > BoardPackageImportLimits.MaxCardTypeNameLength)
        {
            return new CardTypeNameValidationResult(
                string.Empty,
                string.Empty,
                new ValidationError(property, "Card type name must be 40 characters or fewer."));
        }

        return new CardTypeNameValidationResult(
            canonicalCardTypeName,
            BoardPackageImportNormalisation.NormaliseName(canonicalCardTypeName),
            null);
    }

    private static CardTypeStyleResolution ResolveCardTypeStyle(string? styleName, string? stylePropertiesJson, string propertyPrefix)
    {
        var resolvedStyleName = string.IsNullOrWhiteSpace(styleName)
            ? CardTypeDefaults.DefaultStyleName
            : styleName.Trim();
        var normalisedStyleName = TagStyleSchemaValidator.NormaliseStyleName(resolvedStyleName);
        if (normalisedStyleName is null)
        {
            return new CardTypeStyleResolution(
                string.Empty,
                string.Empty,
                new ValidationError($"{propertyPrefix}.styleName", "Style name must be 'solid', 'gradient', 'auto', or 'presets'."));
        }

        var resolvedStylePropertiesJson = string.IsNullOrWhiteSpace(stylePropertiesJson)
            ? TagStyleSchemaValidator.BuildDefaultStylePropertiesJson(normalisedStyleName)
            : stylePropertiesJson.Trim();
        if (!TagStyleSchemaValidator.IsValidJsonObject(resolvedStylePropertiesJson))
        {
            return new CardTypeStyleResolution(
                string.Empty,
                string.Empty,
                new ValidationError($"{propertyPrefix}.stylePropertiesJson", "Style properties must be valid JSON object."));
        }

        return new CardTypeStyleResolution(normalisedStyleName, resolvedStylePropertiesJson, null);
    }
}
