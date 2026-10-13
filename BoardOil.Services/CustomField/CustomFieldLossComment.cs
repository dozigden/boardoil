using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Services.CustomField;

internal static class CustomFieldLossComment
{
    public static EntityCardComment? Add(EntityBoardCard card, IReadOnlyList<LostCustomFieldValue> losses,
        string sourceTypeName, string destinationTypeName, string reason = "card type change")
    {
        if (losses.Count == 0) { return null; }

        // Indented code preserves literal names/values without Markdown escaping expanding the record.
        // Ten fields of 40-character names and 250-character values fit below 4,000 characters.
        var typeChange = $"Card type: {sourceTypeName} → {destinationTypeName}".ReplaceLineEndings("\n    ");
        var text = $"Fields removed during {reason}:\n\n"
            + $"    {typeChange}\n\n"
            + string.Join("\n", losses.Select(loss => $"    {loss.Name}: {loss.Value}"));
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
