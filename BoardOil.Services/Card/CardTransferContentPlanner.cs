using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Services.Card;

public sealed class CardTransferContentPlanner
{
    public CardTransferContentPlanResult CreatePlan(
        string rawPolicy,
        int destinationBoardId,
        EntityBoardCard sourceCard,
        EntityCardType destinationDefaultCardType,
        IReadOnlyList<EntityCardType> destinationCardTypes,
        IReadOnlyList<EntityTag> destinationTags,
        IReadOnlyList<EntitySlick> destinationSlicks)
    {
        var policy = ParsePolicy(rawPolicy);
        if (policy is null)
        {
            return Failure("transferPolicy", "Transfer policy is not valid.");
        }

        if (policy == CardTransferPolicy.DestinationDefaults)
        {
            return Success(new CardTransferContentPlan(
                destinationDefaultCardType,
                [],
                null,
                [],
                [],
                []));
        }

        var cardTypeMatch = FindSingleCardTypeMatch(sourceCard.CardType.Name, destinationCardTypes);
        if (cardTypeMatch.IsAmbiguous)
        {
            return Failure(
                "destinationBoardId",
                $"Destination board contains multiple card types named '{sourceCard.CardType.Name}'.");
        }

        var tagsByName = destinationTags.ToDictionary(x => x.NormalisedName, StringComparer.Ordinal);
        var slicksByName = destinationSlicks.ToDictionary(x => x.NormalisedName, StringComparer.Ordinal);
        if (policy == CardTransferPolicy.KeepMatching)
        {
            var matchingTags = sourceCard.CardTags
                .Select(x => x.Tag)
                .Where(x => tagsByName.ContainsKey(NormaliseName(x.Name)))
                .Select(x => tagsByName[NormaliseName(x.Name)])
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .ToList();
            var matchingSlick = FindSlickMatch(sourceCard.Slick, slicksByName);
            return Success(new CardTransferContentPlan(
                cardTypeMatch.CardType ?? destinationDefaultCardType,
                matchingTags,
                matchingSlick,
                [],
                [],
                []));
        }

        var newCardTypes = new List<EntityCardType>();
        var selectedCardType = cardTypeMatch.CardType;
        if (selectedCardType is null)
        {
            selectedCardType = new EntityCardType
            {
                BoardId = destinationBoardId,
                Name = sourceCard.CardType.Name,
                Emoji = sourceCard.CardType.Emoji,
                StyleName = sourceCard.CardType.StyleName,
                StylePropertiesJson = sourceCard.CardType.StylePropertiesJson,
                IsSystem = false,
            };
            newCardTypes.Add(selectedCardType);
        }

        var selectedTags = new List<EntityTag>();
        var newTags = new List<EntityTag>();
        foreach (var sourceTag in sourceCard.CardTags.Select(x => x.Tag).OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            var normalisedName = NormaliseName(sourceTag.Name);
            if (tagsByName.TryGetValue(normalisedName, out var matchingTag))
            {
                selectedTags.Add(matchingTag);
                continue;
            }

            var copiedTag = new EntityTag
            {
                BoardId = destinationBoardId,
                Name = sourceTag.Name,
                NormalisedName = normalisedName,
                Emoji = sourceTag.Emoji,
                StyleName = sourceTag.StyleName,
                StylePropertiesJson = sourceTag.StylePropertiesJson,
            };
            selectedTags.Add(copiedTag);
            newTags.Add(copiedTag);
            tagsByName[normalisedName] = copiedTag;
        }

        EntitySlick? selectedSlick = null;
        var newSlicks = new List<EntitySlick>();
        if (sourceCard.Slick is not null)
        {
            var normalisedName = NormaliseName(sourceCard.Slick.Name);
            if (!slicksByName.TryGetValue(normalisedName, out selectedSlick))
            {
                selectedSlick = new EntitySlick
                {
                    BoardId = destinationBoardId,
                    Name = sourceCard.Slick.Name,
                    NormalisedName = normalisedName,
                    StyleName = sourceCard.Slick.StyleName,
                    StylePropertiesJson = sourceCard.Slick.StylePropertiesJson,
                };
                newSlicks.Add(selectedSlick);
            }
        }

        return Success(new CardTransferContentPlan(
            selectedCardType,
            selectedTags,
            selectedSlick,
            newCardTypes,
            newTags,
            newSlicks));
    }

    private static CardTransferPolicy? ParsePolicy(string? rawPolicy)
    {
        if (string.Equals(rawPolicy?.Trim(), CardTransferPolicies.DestinationDefaults, StringComparison.OrdinalIgnoreCase))
        {
            return CardTransferPolicy.DestinationDefaults;
        }

        if (string.Equals(rawPolicy?.Trim(), CardTransferPolicies.KeepMatching, StringComparison.OrdinalIgnoreCase))
        {
            return CardTransferPolicy.KeepMatching;
        }

        if (string.Equals(rawPolicy?.Trim(), CardTransferPolicies.CopyMissing, StringComparison.OrdinalIgnoreCase))
        {
            return CardTransferPolicy.CopyMissing;
        }

        return null;
    }

    private static CardTypeMatch FindSingleCardTypeMatch(
        string sourceName,
        IReadOnlyList<EntityCardType> destinationCardTypes)
    {
        var normalisedName = NormaliseName(sourceName);
        var matches = destinationCardTypes
            .Where(x => NormaliseName(x.Name) == normalisedName)
            .Take(2)
            .ToList();
        return new CardTypeMatch(
            matches.Count == 1 ? matches[0] : null,
            matches.Count > 1);
    }

    private static EntitySlick? FindSlickMatch(
        EntitySlick? sourceSlick,
        IReadOnlyDictionary<string, EntitySlick> destinationSlicksByName)
    {
        if (sourceSlick is null)
        {
            return null;
        }

        return destinationSlicksByName.GetValueOrDefault(NormaliseName(sourceSlick.Name));
    }

    private static string NormaliseName(string name) =>
        name.Trim().ToUpperInvariant();

    private static CardTransferContentPlanResult Success(CardTransferContentPlan plan) =>
        new(plan, null);

    private static CardTransferContentPlanResult Failure(string property, string message) =>
        new(null, ApiErrors.ValidationFailed([new ValidationError(property, message)]));

    private enum CardTransferPolicy
    {
        DestinationDefaults,
        KeepMatching,
        CopyMissing,
    }

    private sealed record CardTypeMatch(EntityCardType? CardType, bool IsAmbiguous);
}

public sealed record CardTransferContentPlan(
    EntityCardType CardType,
    IReadOnlyList<EntityTag> Tags,
    EntitySlick? Slick,
    IReadOnlyList<EntityCardType> NewCardTypes,
    IReadOnlyList<EntityTag> NewTags,
    IReadOnlyList<EntitySlick> NewSlicks)
{
    public bool CreatedDefinitions =>
        NewCardTypes.Count > 0 || NewTags.Count > 0 || NewSlicks.Count > 0;
}

public sealed record CardTransferContentPlanResult(
    CardTransferContentPlan? Plan,
    ApiError? Error);
