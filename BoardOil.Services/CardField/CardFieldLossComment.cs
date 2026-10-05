using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Services.CardField;

internal static class CardFieldLossComment
{
    public static EntityCardComment? Add(EntityBoardCard card, IReadOnlyList<LostCardFieldValue> losses,
        string reason = "card type change")
    {
        if (losses.Count == 0) { return null; }

        // Indented code preserves literal names/values without Markdown escaping expanding the record.
        // Ten fields of 40-character names and 250-character values fit below 4,000 characters.
        var text = $"Fields removed during {reason}:\n\n"
            + string.Join("\n", losses.Select(loss => $"    {loss.DisplayName}: {loss.Value}"));
        var now = DateTime.UtcNow;
        var comment = new EntityCardComment
        {
            Card = card, Text = text, IsSystem = true, PostedAtUtc = now
        };
        card.Comments.Add(comment);
        card.CardUpdatedUtc = now;
        return comment;
    }
}
