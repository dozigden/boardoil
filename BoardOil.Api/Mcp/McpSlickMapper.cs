using BoardOil.Contracts.Common;
using BoardOil.Contracts.Slick;
using BoardOil.Mcp.Contracts;
using BoardOil.Services.Style;

namespace BoardOil.Api.Mcp;

internal static class McpSlickMapper
{
    public static McpStyleMappingResult ParseStyle(McpStyle style)
    {
        if (style is not McpPresetStyle && style is not McpSolidStyle)
        {
            return new McpStyleMappingResult(
                null,
                [new ValidationError("style.styleName", "Slick style must be 'solid' or 'presets'.")]);
        }

        return McpStyleMapper.Parse(style);
    }

    public static McpSlickSnapshot? ToMcpSnapshot(SlickDto slick)
    {
        var style = ToMcpStyle(slick.StyleName, slick.StylePropertiesJson);
        if (style is null)
        {
            return null;
        }

        return new McpSlickSnapshot(
            slick.Id,
            slick.Name,
            style,
            slick.CreatedAtUtc,
            slick.UpdatedAtUtc);
    }

    public static McpCardOptionSlick? ToMcpOption(SlickDto slick)
    {
        var style = ToMcpStyle(slick.StyleName, slick.StylePropertiesJson);
        return style is null
            ? null
            : new McpCardOptionSlick(slick.Id, slick.Name, style);
    }

    private static McpStyle? ToMcpStyle(string styleName, string stylePropertiesJson)
    {
        var parsedStyle = StyleDefinitionCodec.ParseCompatible(styleName, stylePropertiesJson);
        if (!parsedStyle.IsValid || parsedStyle.Definition is null)
        {
            return null;
        }

        var style = McpStyleMapper.ToMcp(parsedStyle.Definition);
        return style switch
        {
            McpPresetStyle => style,
            McpSolidStyle => style,
            _ => null
        };
    }
}
