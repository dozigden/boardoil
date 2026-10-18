using BoardOil.Contracts.CardType;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Services.Style;

namespace BoardOil.Api.Mcp;

internal static class McpCardTypeMapper
{
    public static McpStyleMappingResult ParseStyle(McpStyle? style)
    {
        if (style is null)
        {
            return new McpStyleMappingResult(null, [new ValidationError("style", "Style is required.")]);
        }

        return McpStyleMapper.Parse(style);
    }

    public static McpCardTypeSnapshot? ToMcpSnapshot(CardTypeDto cardType)
    {
        var parsedStyle = StyleDefinitionCodec.ParseForWrite(cardType.StyleName, cardType.StylePropertiesJson);
        if (!parsedStyle.IsValid || parsedStyle.Definition is null)
        {
            return null;
        }

        return new McpCardTypeSnapshot(
            cardType.Id,
            cardType.Name,
            cardType.Emoji,
            McpStyleMapper.ToMcp(parsedStyle.Definition),
            cardType.CreatedAtUtc,
            cardType.UpdatedAtUtc);
    }

    public static McpToolError InvalidStyle(CardTypeDto cardType) =>
        new("data_integrity_error", $"Card type {cardType.Id} ('{cardType.Name}') has an invalid style definition.", 500);
}
