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
}
