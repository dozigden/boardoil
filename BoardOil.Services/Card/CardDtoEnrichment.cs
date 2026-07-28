using BoardOil.Contracts.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Image;

namespace BoardOil.Services.Card;

internal static class CardDtoEnrichment
{
    public static async Task<CardDto> EnrichAssignedUserImageAsync(CardDto card, IImageRepository imageRepository)
    {
        if (card.AssignedUserId is null)
        {
            return card.WithAssignedUserImageRelativePath(null);
        }

        var image = await imageRepository.GetLatestForEntityAsync(ImageEntityType.UserProfile, card.AssignedUserId.Value);
        return card.WithAssignedUserImageRelativePath(image?.RelativePath);
    }

    public static async Task<IReadOnlyList<CardDto>> EnrichAssignedUserImagesAsync(
        IReadOnlyList<CardDto> cards,
        IImageRepository imageRepository)
    {
        var assignedUserIds = cards
            .Where(x => x.AssignedUserId is not null)
            .Select(x => x.AssignedUserId!.Value)
            .Distinct()
            .ToArray();
        if (assignedUserIds.Length == 0)
        {
            return cards;
        }

        var images = await imageRepository.GetLatestForEntitiesAsync(ImageEntityType.UserProfile, assignedUserIds);
        var imagePathsByUserId = images.ToDictionary(x => x.EntityId, x => x.RelativePath);
        return cards
            .Select(card => card.WithAssignedUserImageRelativePath(
                card.AssignedUserId is int assignedUserId
                    ? imagePathsByUserId.GetValueOrDefault(assignedUserId)
                    : null))
            .ToList();
    }
}
