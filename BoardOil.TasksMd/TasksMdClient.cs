using System.Text.Json;
using System.Text.RegularExpressions;

namespace BoardOil.TasksMd;

public sealed partial class TasksMdClient(HttpClient httpClient) : ITasksMdClient
{
    private const string UrlPropertyName = "url";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Dictionary<string, string> TasksMdColorMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["var(--color-alt-1)"] = "#a51d2d",
        ["var(--color-alt-2)"] = "#c64600",
        ["var(--color-alt-3)"] = "#e5a50a",
        ["var(--color-alt-4)"] = "#63452c",
        ["var(--color-alt-5)"] = "#26a269",
        ["var(--color-alt-6)"] = "#613583",
        ["var(--color-alt-7)"] = "#1A5FB4"
    };

    public async Task<TasksMdBoardImportModel> LoadBoardAsync(Uri baseUri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseUri);

        var normalisedBaseUri = NormaliseBaseUri(baseUri);
        var resourceUri = new Uri(normalisedBaseUri, "_api/resource");
        var tagsUri = new Uri(normalisedBaseUri, "_api/tags");
        var sortUri = new Uri(normalisedBaseUri, "_api/sort");

        var resource = await GetResourceAsync(resourceUri, cancellationToken);
        var tags = await GetTagsAsync(tagsUri, cancellationToken);
        var sort = await GetSortAsync(sortUri, cancellationToken);

        return BuildImportModel(resource, tags, sort);
    }

    private static Uri NormaliseBaseUri(Uri baseUri)
    {
        var absolute = baseUri.IsAbsoluteUri
            ? baseUri
            : throw ValidationException("tasksmd URL must be absolute.");

        if (absolute.Scheme is not ("http" or "https"))
        {
            throw ValidationException("tasksmd URL must use http or https.");
        }

        var normalised = absolute.AbsoluteUri;
        if (!normalised.EndsWith("/", StringComparison.Ordinal))
        {
            normalised += "/";
        }

        return new Uri(normalised);
    }

    private async Task<IReadOnlyList<ResourceColumnDto>> GetResourceAsync(Uri uri, CancellationToken cancellationToken)
    {
        var payload = await GetStringAsync(uri, cancellationToken);
        var resource = Deserialize<List<ResourceColumnDto>>(payload, "tasksmd resource payload is invalid JSON.");
        if (resource is null)
        {
            throw ValidationException("tasksmd resource payload is required.");
        }

        return resource;
    }

    private async Task<IReadOnlyList<TagDefinitionDto>> GetTagsAsync(Uri uri, CancellationToken cancellationToken)
    {
        var payload = await GetStringAsync(uri, cancellationToken);

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw ValidationException("tasksmd tags payload must be a JSON object.");
            }

            var tags = new List<TagDefinitionDto>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    throw ValidationException("tasksmd tags payload values must be strings.");
                }

                tags.Add(new TagDefinitionDto(property.Name, property.Value.GetString() ?? string.Empty));
            }

            return tags;
        }
        catch (JsonException)
        {
            throw ValidationException("tasksmd tags payload is invalid JSON.");
        }
    }

    private async Task<IReadOnlyList<SortColumnDto>> GetSortAsync(Uri uri, CancellationToken cancellationToken)
    {
        var payload = await GetStringAsync(uri, cancellationToken);

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw ValidationException("tasksmd sort payload must be a JSON object.");
            }

            var columns = new List<SortColumnDto>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Array)
                {
                    throw ValidationException("tasksmd sort payload values must be arrays.");
                }

                var fileNames = new List<string>();
                foreach (var item in property.Value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                    {
                        throw ValidationException("tasksmd sort file names must be strings.");
                    }

                    var fileName = (item.GetString() ?? string.Empty).Trim();
                    if (fileName.Length == 0)
                    {
                        continue;
                    }

                    fileNames.Add(fileName);
                }

                columns.Add(new SortColumnDto(property.Name, fileNames));
            }

            return columns;
        }
        catch (JsonException)
        {
            throw ValidationException("tasksmd sort payload is invalid JSON.");
        }
    }

    private async Task<string> GetStringAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw ValidationException($"tasksmd endpoint '{uri}' returned {(int)response.StatusCode}.");
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw ValidationException($"Unable to fetch tasksmd data from '{uri}'.");
        }
        catch (TaskCanceledException)
        {
            throw ValidationException($"Timed out fetching tasksmd data from '{uri}'.");
        }
    }

    private static T? Deserialize<T>(string json, string invalidJsonMessage)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            throw ValidationException(invalidJsonMessage);
        }
    }

    private static TasksMdBoardImportModel BuildImportModel(
        IReadOnlyList<ResourceColumnDto> resourceColumns,
        IReadOnlyList<TagDefinitionDto> tagDefinitions,
        IReadOnlyList<SortColumnDto> sortColumns)
    {
        var parsedColumns = resourceColumns
            .Select(ParseResourceColumn)
            .ToList();

        var orderedColumns = OrderBySortNames(
            parsedColumns,
            sortColumns.Select(x => x.ColumnName),
            x => x.Name,
            StringComparer.OrdinalIgnoreCase);

        var sortByColumnName = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var sortColumn in sortColumns)
        {
            if (!sortByColumnName.ContainsKey(sortColumn.ColumnName))
            {
                sortByColumnName.Add(sortColumn.ColumnName, sortColumn.FileNames);
            }
        }

        var allDiscoveredTagNames = new List<string>();
        var seenDiscoveredTagNormalisedNames = new HashSet<string>(StringComparer.Ordinal);

        var importedColumns = new List<TasksMdImportedColumn>(orderedColumns.Count);
        foreach (var column in orderedColumns)
        {
            var fileSortNames = sortByColumnName.GetValueOrDefault(column.Name, Array.Empty<string>());
            var orderedFiles = OrderBySortNames(column.Files, fileSortNames, x => x.Name, StringComparer.OrdinalIgnoreCase);

            var importedCards = new List<TasksMdImportedCard>(orderedFiles.Count);
            foreach (var file in orderedFiles)
            {
                var extraction = ExtractTags(file.Content);
                foreach (var tagName in extraction.TagNames)
                {
                    var normalised = NormaliseName(tagName);
                    if (seenDiscoveredTagNormalisedNames.Add(normalised))
                    {
                        allDiscoveredTagNames.Add(tagName);
                    }
                }

                importedCards.Add(new TasksMdImportedCard(file.Name, extraction.Description, extraction.TagNames));
            }

            importedColumns.Add(new TasksMdImportedColumn(column.Name, importedCards));
        }

        var importedTags = BuildImportedTags(tagDefinitions, allDiscoveredTagNames);
        return new TasksMdBoardImportModel(importedColumns, importedTags);
    }

    private static ParsedResourceColumn ParseResourceColumn(ResourceColumnDto column)
    {
        var canonicalColumnName = CanonicaliseName(column.Name);
        if (canonicalColumnName.Length == 0)
        {
            throw ValidationException("tasksmd resource contains a column without a valid name.");
        }

        var rawFiles = column.Files ?? [];
        var files = new List<ParsedResourceFile>(rawFiles.Count);
        foreach (var file in rawFiles)
        {
            var canonicalFileName = CanonicaliseName(file.Name);
            if (canonicalFileName.Length == 0)
            {
                throw ValidationException($"tasksmd column '{canonicalColumnName}' contains a file without a valid name.");
            }

            files.Add(new ParsedResourceFile(canonicalFileName, file.Content ?? string.Empty));
        }

        return new ParsedResourceColumn(canonicalColumnName, files);
    }

    private static IReadOnlyList<TasksMdImportedTag> BuildImportedTags(
        IReadOnlyList<TagDefinitionDto> tagDefinitions,
        IReadOnlyList<string> discoveredTagNames)
    {
        var orderedTags = new List<TasksMdImportedTag>();
        var byNormalisedName = new Dictionary<string, TasksMdImportedTag>(StringComparer.Ordinal);

        foreach (var tagDefinition in tagDefinitions)
        {
            var canonicalName = CanonicaliseName(tagDefinition.Name);
            if (canonicalName.Length == 0)
            {
                continue;
            }

            var normalisedName = NormaliseName(canonicalName);
            if (byNormalisedName.ContainsKey(normalisedName))
            {
                continue;
            }

            var mappedColor = MapColor(tagDefinition.ColorValue);
            var imported = new TasksMdImportedTag(canonicalName, mappedColor);
            byNormalisedName.Add(normalisedName, imported);
            orderedTags.Add(imported);
        }

        foreach (var discoveredTagName in discoveredTagNames)
        {
            var canonicalName = CanonicaliseName(discoveredTagName);
            if (canonicalName.Length == 0)
            {
                continue;
            }

            var normalisedName = NormaliseName(canonicalName);
            if (byNormalisedName.ContainsKey(normalisedName))
            {
                continue;
            }

            var imported = new TasksMdImportedTag(canonicalName, null);
            byNormalisedName.Add(normalisedName, imported);
            orderedTags.Add(imported);
        }

        return orderedTags;
    }

    private static string? MapColor(string? rawColor)
    {
        if (string.IsNullOrWhiteSpace(rawColor))
        {
            return null;
        }

        var canonical = rawColor.Trim().TrimEnd(';').Trim();

        var altVariableMatch = AltVariableColorRegex().Match(canonical);
        if (altVariableMatch.Success)
        {
            var altIndex = altVariableMatch.Groups["index"].Value;
            var variableKey = $"var(--color-alt-{altIndex})";
            if (TasksMdColorMap.TryGetValue(variableKey, out var mappedFromVariable))
            {
                return mappedFromVariable;
            }
        }

        if (TasksMdColorMap.TryGetValue(canonical, out var mapped))
        {
            return mapped;
        }

        if (!HexColorRegex().IsMatch(canonical))
        {
            return null;
        }

        return canonical;
    }

    private static IReadOnlyList<TItem> OrderBySortNames<TItem>(
        IReadOnlyList<TItem> source,
        IEnumerable<string> orderedNames,
        Func<TItem, string> nameSelector,
        IEqualityComparer<string> comparer)
    {
        var remaining = source.ToList();
        var ordered = new List<TItem>(source.Count);

        foreach (var rawName in orderedNames)
        {
            var targetName = CanonicaliseName(rawName);
            if (targetName.Length == 0)
            {
                continue;
            }

            var matchIndex = FindIndex(remaining, item => comparer.Equals(nameSelector(item), targetName));
            if (matchIndex < 0)
            {
                continue;
            }

            ordered.Add(remaining[matchIndex]);
            remaining.RemoveAt(matchIndex);
        }

        ordered.AddRange(remaining);
        return ordered;
    }

    private static int FindIndex<TItem>(IReadOnlyList<TItem> items, Func<TItem, bool> predicate)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (predicate(items[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static TagExtractionResult ExtractTags(string content)
    {
        var discoveredTagNames = new List<string>();
        var discoveredNormalisedNames = new HashSet<string>(StringComparer.Ordinal);

        var withoutTokens = TagTokenRegex().Replace(content, match =>
        {
            var rawTagName = match.Groups["name"].Value;
            var canonicalTagName = CanonicaliseName(rawTagName);
            if (canonicalTagName.Length == 0)
            {
                return string.Empty;
            }

            var normalised = NormaliseName(canonicalTagName);
            if (discoveredNormalisedNames.Add(normalised))
            {
                discoveredTagNames.Add(canonicalTagName);
            }

            return string.Empty;
        });

        return new TagExtractionResult(NormaliseDescription(withoutTokens), discoveredTagNames);
    }

    private static string CanonicaliseName(string? rawName) =>
        rawName?.Trim() ?? string.Empty;

    private static string NormaliseName(string name) =>
        name.ToUpperInvariant();

    private static string NormaliseDescription(string rawDescription)
    {
        var withNormalisedLineEndings = rawDescription
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        var trimmedLineEndings = string.Join(
            "\n",
            withNormalisedLineEndings
                .Split('\n')
                .Select(x => x.TrimEnd()));

        var collapsedBlankLines = MultiBlankLineRegex().Replace(trimmedLineEndings, "\n\n");
        return collapsedBlankLines.Trim();
    }

    private static TasksMdClientException ValidationException(string message) =>
        new(message, [new TasksMdClientValidationError(UrlPropertyName, message)]);

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled)]
    private static partial Regex HexColorRegex();

    [GeneratedRegex("^var\\(\\s*--color-alt-(?<index>[1-7])\\s*\\)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AltVariableColorRegex();

    [GeneratedRegex("\\[\\s*tag\\s*:(?<name>[^\\]\\r\\n]+?)\\s*\\]", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex TagTokenRegex();

    [GeneratedRegex("\\n{3,}", RegexOptions.Compiled)]
    private static partial Regex MultiBlankLineRegex();

    private sealed record ResourceColumnDto(string? Name, List<ResourceFileDto>? Files);

    private sealed record ResourceFileDto(string? Name, string? Content);

    private sealed record ParsedResourceColumn(string Name, IReadOnlyList<ParsedResourceFile> Files);

    private sealed record ParsedResourceFile(string Name, string Content);

    private sealed record TagDefinitionDto(string Name, string? ColorValue);

    private sealed record SortColumnDto(string ColumnName, IReadOnlyList<string> FileNames);

    private sealed record TagExtractionResult(string Description, IReadOnlyList<string> TagNames);
}
