using System.Text;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Services.Card;

public readonly record struct ChecklistCounts(int Completed, int Total);

/// <summary>
/// Counts BoardOil checklist markers, without building a Markdown document. Block state
/// exists only to distinguish checklist lines from code/HTML and list continuations.
/// Compatibility fixtures also run against the editor's parser.
/// </summary>
public static class CardChecklistCounter
{
    private static readonly HashSet<string> HtmlBlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "base", "basefont", "blockquote", "body", "caption", "center",
        "col", "colgroup", "dd", "details", "dialog", "dir", "div", "dl", "dt", "fieldset", "figcaption",
        "figure", "footer", "form", "frame", "frameset", "h1", "h2", "h3", "h4", "h5", "h6", "head",
        "header", "hr", "html", "iframe", "legend", "li", "link", "main", "menu", "menuitem", "meta",
        "nav", "noframes", "ol", "optgroup", "option", "p", "param", "search", "section", "summary",
        "table", "tbody", "td", "tfoot", "th", "thead", "title", "tr", "track", "ul"
    };

    public static void Refresh(EntityBoardCard card)
    {
        var counts = Count(card.Description);
        card.CompletedChecklistItemCount = counts.Completed;
        card.TotalChecklistItemCount = counts.Total;
    }

    public static ChecklistCounts Count(string description)
    {
        if (!description.Contains('['))
        {
            return default;
        }

        var completed = 0;
        var total = 0;
        var root = new BlockContext(-1, 0);
        var lists = new List<BlockContext>();
        var quoteDepth = 0;

        foreach (var sourceLine in description.AsSpan().EnumerateLines())
        {
            var line = sourceLine;
            if (line.Contains('\t')) { line = ExpandTabs(line); }
            // Apply the editor's bare-checkbox normalisation before block handling,
            // so bare and bulleted items also share list-boundary behaviour.
            var bareIndent = LeadingWhitespace(line);
            var bareContent = line[bareIndent..];
            if (bareContent.Length >= 3 && bareContent[0] == '[' && bareContent[2] == ']'
                && bareContent[1] is ' ' or 'x' or 'X')
            {
                line = string.Concat(line[..bareIndent], "- ", bareContent);
            }
            var nextQuoteDepth = 0;
            var previousContext = root;
            if (lists.Count > 0) { previousContext = lists[^1]; }
            var opaque = previousContext.FenceLength > 0 || previousContext.IndentedCode
                || previousContext.HtmlEnd is not null || previousContext.HtmlUntilBlank;
            // Quote-looking content inside code/HTML cannot open a new quote block.
            while (!opaque || nextQuoteDepth < quoteDepth)
            {
                var leading = LeadingWhitespace(line);
                var quoteIndent = leading;
                if (nextQuoteDepth == 0) { quoteIndent -= previousContext.ContentIndent; }
                if (leading == line.Length || line[leading] != '>' || quoteIndent > 3
                    || line[..leading].Contains('\t'))
                {
                    break;
                }

                nextQuoteDepth++;
                line = line[(leading + 1)..];
                if (!line.IsEmpty && line[0] == ' ')
                {
                    line = line[1..];
                }
            }

            if (nextQuoteDepth != quoteDepth)
            {
                root = new BlockContext(-1, 0);
                lists.Clear();
                quoteDepth = nextQuoteDepth;
            }

            var indentation = LeadingWhitespace(line);
            if (indentation < line.Length)
            {
                while (lists.Count > 0 && (indentation <= lists[^1].ItemIndent
                    || (!lists[^1].IsChecklistItem && indentation < lists[^1].ContentIndent)))
                {
                    lists.RemoveAt(lists.Count - 1);
                }
            }

            var context = root;
            if (lists.Count > 0)
            {
                context = lists[^1];
            }

            // TaskItem's continuation parser removes two source characters rather
            // than expanding indentation first. A lone tab therefore cannot indent
            // a child checklist item, although a tab plus another whitespace character can.
            var sourceIndent = LeadingWhitespace(sourceLine);
            if (quoteDepth == 0 && context.IsChecklistItem && sourceLine[..sourceIndent].Contains('\t')
                && sourceIndent < context.ContentIndent && sourceIndent < sourceLine.Length)
            {
                continue;
            }

            // An under-indented checklist continuation must not lose non-whitespace text.
            if (indentation < context.ContentIndent && indentation < line.Length) { continue; }
            var content = line[Math.Min(context.ContentIndent, indentation)..];
            var indent = LeadingWhitespace(content);
            var trimmed = content[indent..];

            if (context.FenceLength > 0)
            {
                if (indent < 4 && FenceLength(trimmed, context.FenceCharacter) >= context.FenceLength
                    && trimmed[FenceLength(trimmed, context.FenceCharacter)..].Trim().IsEmpty)
                {
                    context.FenceLength = 0;
                }
                continue;
            }

            if (context.HtmlEnd is not null)
            {
                if (trimmed.Contains(context.HtmlEnd, StringComparison.OrdinalIgnoreCase))
                {
                    context.HtmlEnd = null;
                }
                continue;
            }

            if (trimmed.IsEmpty)
            {
                context.Paragraph = false;
                context.HtmlUntilBlank = false;
                context.BlankLine = true;
                root.BlankLine = true;
                continue;
            }

            if (context.HtmlUntilBlank)
            {
                continue;
            }

            if (context.IndentedCode && indent >= 4)
            {
                continue;
            }
            context.IndentedCode = false;

            var checklistItemState = ReadChecklistItem(trimmed);
            if (indent >= 4 && ((context.Paragraph && (context == root || context.IsChecklistItem)) || checklistItemState is null))
            {
                context.IndentedCode = !context.Paragraph || (context != root && !context.IsChecklistItem);
                continue;
            }

            if (indent < 4 && StartFence(trimmed, context))
            {
                context.PendingListMarker = '\0';
                context.Paragraph = false;
                continue;
            }

            if (indent < 4 && StartHtml(trimmed, context))
            {
                context.PendingListMarker = '\0';
                context.Paragraph = false;
                continue;
            }

            var markerLength = ListMarkerLength(trimmed);
            if (context.PendingListMarker != '\0')
            {
                if (markerLength > 0 && trimmed[0] == context.PendingListMarker)
                {
                    if (checklistItemState is bool pendingChecked)
                    {
                        context.PendingTotal++;
                        if (pendingChecked) { context.PendingCompleted++; }
                        lists.Add(new BlockContext(indentation, indentation + markerLength));
                        continue;
                    }
                    // A list beginning with an empty marker is handled by the editor's
                    // fallback: it only becomes a checklist if it also has plain items.
                    total += context.PendingTotal;
                    completed += context.PendingCompleted;
                    context.PendingListMarker = '\0';
                }
                else if (markerLength > 0 || context.BlankLine || IsHeading(trimmed)
                    || IsThematicBreak(trimmed) || trimmed.StartsWith("<"))
                {
                    context.PendingListMarker = '\0';
                }
            }
            context.BlankLine = false;

            if (markerLength > 0 && trimmed[0] is '-' or '*' or '+')
            {
                var item = trimmed[markerLength..];
                if (item.Length == 3 && item[0] == '[' && item[2] == ']' && item[1] is ' ' or 'x' or 'X')
                {
                    context.PendingListMarker = trimmed[0];
                    context.PendingTotal = 1;
                    context.PendingCompleted = item[1] == ' ' ? 0 : 1;
                }
            }

            if (checklistItemState is bool isChecked)
            {
                total++;
                if (isChecked)
                {
                    completed++;
                }
                context.Paragraph = false;
                // Tiptap removes exactly two characters of indentation below a checklist item,
                // including its non-standard treatment of standalone indented tasks.
                lists.Add(new BlockContext(indentation, indentation + 2) { IsChecklistItem = true });
                continue;
            }

            if (indent < 4 && markerLength > 0)
            {
                context.Paragraph = false;
                var itemContext = new BlockContext(indentation, indentation + markerLength) { Paragraph = true };
                lists.Add(itemContext);
                // The editor parses initial bullet-item content as blocks. Ordered
                // items use a different fallback, covered by the shared fixtures.
                if (trimmed[0] is '-' or '*' or '+')
                {
                    itemContext.Paragraph = false;
                    var item = trimmed[markerLength..];
                    var itemQuoteDepth = 0;
                    while (item.StartsWith(">"))
                    {
                        itemQuoteDepth++;
                        item = item[1..];
                        if (item.StartsWith(" ")) { item = item[1..]; }
                    }
                    if (itemQuoteDepth > 0)
                    {
                        quoteDepth += itemQuoteDepth;
                        root = new BlockContext(-1, 0);
                        lists.Clear();
                        itemContext = root;
                    }
                    if (!item.IsEmpty)
                    {
                        if (StartFence(item, itemContext) || StartHtml(item, itemContext))
                        {
                            itemContext.Paragraph = false;
                        }
                        else if (ReadChecklistItem(item) is bool itemChecked)
                        {
                            total++;
                            if (itemChecked) { completed++; }
                            lists.Add(new BlockContext(0, 2) { IsChecklistItem = true });
                        }
                        else { itemContext.Paragraph = !IsHeading(item) && !IsThematicBreak(item); }
                    }
                }
                continue;
            }

            context.Paragraph = !IsHeading(trimmed) && !IsThematicBreak(trimmed);
        }

        return new ChecklistCounts(completed, total);
    }

    private static bool? ReadChecklistItem(ReadOnlySpan<char> text)
    {
        // The editor's fallback parser also recognises an escaped list bullet.
        if (text.StartsWith("\\- ") || text.StartsWith("\\* ") || text.StartsWith("\\+ "))
        {
            text = text[1..];
        }

        if (text.Length > 1 && text[0] is '-' or '*' or '+' && char.IsWhiteSpace(text[1]))
        {
            text = text[1..].TrimStart();
        }
        else
        {
            return null;
        }

        if (text.Length < 4 || text[0] != '[' || text[2] != ']' || !char.IsWhiteSpace(text[3]))
        {
            return null;
        }
        return text[1] switch { ' ' => false, 'x' or 'X' => true, _ => null };
    }

    private static int LeadingWhitespace(ReadOnlySpan<char> text)
    {
        var length = 0;
        while (length < text.Length && char.IsWhiteSpace(text[length])) { length++; }
        return length;
    }

    private static string ExpandTabs(ReadOnlySpan<char> text)
    {
        var expanded = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (character == '\t') { expanded.Append(' ', 4 - expanded.Length % 4); }
            else { expanded.Append(character); }
        }
        return expanded.ToString();
    }

    private static int ListMarkerLength(ReadOnlySpan<char> text)
    {
        var end = 0;
        if (text[0] is '-' or '*' or '+') { end = 1; }
        else
        {
            while (end < text.Length && char.IsAsciiDigit(text[end])) { end++; }
            if (end == 0 || end > 9 || end == text.Length || text[end] is not ('.' or ')')) { return 0; }
            end++;
        }
        if (end == text.Length || !char.IsWhiteSpace(text[end])) { return 0; }
        return end + LeadingWhitespace(text[end..]);
    }

    private static int FenceLength(ReadOnlySpan<char> text, char character)
    {
        var length = 0;
        while (length < text.Length && text[length] == character) { length++; }
        return length;
    }

    private static bool StartFence(ReadOnlySpan<char> text, BlockContext context)
    {
        if (text[0] is not ('`' or '~')) { return false; }
        var length = FenceLength(text, text[0]);
        if (length < 3 || (text[0] == '`' && text[length..].Contains('`'))) { return false; }
        context.FenceCharacter = text[0];
        context.FenceLength = length;
        return true;
    }

    private static bool StartHtml(ReadOnlySpan<char> text, BlockContext context)
    {
        if (text[0] != '<') { return false; }
        string? end = null;
        if (text.StartsWith("<!--")) { end = "-->"; }
        else if (text.StartsWith("<![CDATA[")) { end = "]]>"; }
        else if (text.StartsWith("<?")) { end = "?>"; }
        else if (text.StartsWith("<!")) { end = ">"; }
        else
        {
            foreach (var tag in new[] { "pre", "script", "style", "textarea" })
            {
                if (text.StartsWith($"<{tag}", StringComparison.OrdinalIgnoreCase)
                    && text.Length > tag.Length + 1 && (char.IsWhiteSpace(text[tag.Length + 1]) || text[tag.Length + 1] == '>'))
                {
                    end = $"</{tag}>";
                    break;
                }
            }
        }

        if (end is not null)
        {
            if (!text.Contains(end, StringComparison.OrdinalIgnoreCase)) { context.HtmlEnd = end; }
            return true;
        }

        var tagStart = 1;
        if (text.Length > 1 && text[1] == '/') { tagStart++; }
        var tagEnd = tagStart;
        while (tagEnd < text.Length && (char.IsAsciiLetterOrDigit(text[tagEnd]) || text[tagEnd] == '-')) { tagEnd++; }
        if (tagEnd == tagStart) { return false; }

        // Block tags can interrupt a paragraph and may have attributes on later lines.
        // Other standalone tags only start HTML blocks outside an existing paragraph.
        var blockTag = HtmlBlockTags.Contains(text[tagStart..tagEnd].ToString())
            && (tagEnd == text.Length || char.IsWhiteSpace(text[tagEnd]) || text[tagEnd] is '/' or '>');
        var tagBoundary = tagEnd < text.Length && (char.IsWhiteSpace(text[tagEnd]) || text[tagEnd] is '/' or '>');
        if (blockTag || (!context.Paragraph && tagBoundary && text.TrimEnd().EndsWith(">")))
        {
            context.HtmlUntilBlank = true;
            return true;
        }
        return false;
    }

    private static bool IsHeading(ReadOnlySpan<char> text)
    {
        var hashes = FenceLength(text, '#');
        return hashes is > 0 and <= 6 && (hashes == text.Length || char.IsWhiteSpace(text[hashes]));
    }

    private static bool IsThematicBreak(ReadOnlySpan<char> text)
    {
        if (text[0] is not ('-' or '*' or '_')) { return false; }
        var count = 0;
        foreach (var character in text)
        {
            if (character == text[0]) { count++; }
            else if (!char.IsWhiteSpace(character)) { return false; }
        }
        return count >= 3;
    }

    private sealed class BlockContext(int itemIndent, int contentIndent)
    {
        public int ItemIndent { get; } = itemIndent;
        public int ContentIndent { get; } = contentIndent;
        public bool IsChecklistItem { get; init; }
        public bool Paragraph { get; set; }
        public bool IndentedCode { get; set; }
        public char FenceCharacter { get; set; }
        public int FenceLength { get; set; }
        public string? HtmlEnd { get; set; }
        public bool HtmlUntilBlank { get; set; }
        public bool BlankLine { get; set; }
        public char PendingListMarker { get; set; }
        public int PendingCompleted { get; set; }
        public int PendingTotal { get; set; }
    }
}
