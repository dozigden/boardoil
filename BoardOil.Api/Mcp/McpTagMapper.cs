using BoardOil.Contracts.Tag;
using BoardOil.Mcp.Contracts;
using BoardOil.Services.Style;

namespace BoardOil.Api.Mcp;

internal static class McpTagMapper
{
    public static McpTagSnapshot? ToMcpSnapshot(TagDto tag)
    {
        var parsedStyle = StyleDefinitionCodec.ParseCompatible(
            tag.StyleName,
            tag.StylePropertiesJson);
        if (!parsedStyle.IsValid || parsedStyle.Definition is null)
        {
            return null;
        }

        return new McpTagSnapshot(
            tag.Id,
            tag.Name,
            tag.Emoji,
            McpStyleMapper.ToMcp(parsedStyle.Definition),
            tag.CreatedAtUtc,
            tag.UpdatedAtUtc);
    }
}
