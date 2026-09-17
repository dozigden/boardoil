using System.Text.Json.Serialization;
using BoardOil.Contracts.Card;

namespace BoardOil.Mcp.Contracts;

public sealed record McpToolDefinition(
    string Name,
    string Description,
    string InputSchemaJson,
    string OutputSchemaJson,
    string? RequiredScope,
    int DiscoveryOrder,
    McpToolBehaviour Behaviour);

public sealed record McpToolBehaviour(
    bool ReadOnly,
    bool Destructive,
    bool Idempotent);

public static class McpToolBehaviours
{
    public static McpToolBehaviour ReadOnly { get; } = new(true, false, true);
    public static McpToolBehaviour NonDestructive { get; } = new(false, false, false);
    public static McpToolBehaviour IdempotentNonDestructive { get; } = new(false, false, true);
    public static McpToolBehaviour Destructive { get; } = new(false, true, false);
    public static McpToolBehaviour IdempotentDestructive { get; } = new(false, true, true);
}

public sealed record McpToolError(
    string Code,
    string Message,
    int StatusCode,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? ValidationErrors = null);

public sealed record McpToolResult<T>(
    bool Success,
    T? Data,
    McpToolError? Error);

public sealed record McpBoardSnapshot(
    int Id,
    string Name,
    string Description,
    DateTime UpdatedAtUtc,
    IReadOnlyList<McpColumnSnapshot> Columns);

public sealed record McpBoardSummary(
    int Id,
    string Name,
    string Description,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? CurrentUserRole);

public sealed record McpColumnSnapshot(
    int Id,
    string Title,
    string SortKey,
    IReadOnlyList<McpBoardCardSnapshot> Cards);

public sealed record McpBoardCardSnapshot(
    int Id,
    int ColumnId,
    int CardTypeId,
    string CardTypeName,
    string? CardTypeEmoji,
    string Title,
    string SortKey,
    IReadOnlyList<McpCardTagSnapshot> Tags,
    IReadOnlyList<string> TagNames,
    DateTime CardCreatedUtc,
    DateTime CardUpdatedUtc,
    int? AssignedUserId,
    string? AssignedUserDisplayName,
    int? SlickId,
    McpCardSlickSnapshot? Slick,
    string? ExternalUrl);

public sealed record McpCardSnapshot(
    int Id,
    int ColumnId,
    int CardTypeId,
    string CardTypeName,
    string? CardTypeEmoji,
    string Title,
    string Description,
    string SortKey,
    IReadOnlyList<McpCardTagSnapshot> Tags,
    IReadOnlyList<string> TagNames,
    DateTime CardCreatedUtc,
    DateTime CardUpdatedUtc,
    int? AssignedUserId,
    string? AssignedUserDisplayName,
    int? SlickId,
    McpCardSlickSnapshot? Slick,
    IReadOnlyList<McpCardCommentSnapshot> Comments,
    string? ExternalUrl)
{
    // Loaded by card_get only. Omit on mutation responses rather than imply an empty attachment list.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CardAttachmentDto>? Attachments { get; init; }
}

public sealed record McpCardCommentSnapshot(
    int Id,
    int CardId,
    int? AuthorUserId,
    string Text,
    DateTime PostedAtUtc,
    string? AuthorDisplayName,
    string? AuthorImageRelativePath);

public sealed record McpArchivedCardSnapshot(
    int Id,
    int ColumnId,
    int CardTypeId,
    string CardTypeName,
    string? CardTypeEmoji,
    string Title,
    string Description,
    string SortKey,
    IReadOnlyList<McpCardTagSnapshot> Tags,
    IReadOnlyList<string> TagNames,
    DateTime CardCreatedUtc,
    DateTime CardUpdatedUtc,
    int? AssignedUserId,
    string? AssignedUserDisplayName,
    int? SlickId,
    McpCardSlickSnapshot? Slick,
    IReadOnlyList<McpArchivedCardCommentSnapshot> Comments,
    string? ExternalUrl,
    DateTime ArchivedAtUtc,
    IReadOnlyList<CardAttachmentDto> Attachments);

public sealed record McpArchivedCardCommentSnapshot(
    string Text,
    DateTime PostedAtUtc,
    int? AuthorUserId,
    string? AuthorDisplayName,
    string? AuthorImageRelativePath);

public sealed record McpArchivedCardSearchResult(
    IReadOnlyList<McpArchivedCardSearchSummary> Cards,
    int TotalCount,
    int Offset,
    int Limit);

public sealed record McpArchivedCardSearchSummary(
    int Id,
    string Title,
    IReadOnlyList<string> TagNames,
    DateTime ArchivedAtUtc);

public sealed record McpCardTagSnapshot(
    int Id,
    string Name,
    string StyleName,
    string StylePropertiesJson,
    string? Emoji);

public sealed record McpCardSlickSnapshot(
    int Id,
    string Name,
    string StyleName,
    string StylePropertiesJson);

public sealed record BoardGetInput
{
    public int? Id { get; init; }
}

public sealed record BoardListInput;

public sealed record BoardListOutput(
    IReadOnlyList<McpBoardSummary> Boards);

public sealed record IdentityGetInput;

public sealed record IdentityGetOutput(
    McpIdentityUser User,
    McpAuthenticationContext Authentication);

public sealed record McpIdentityUser(
    int Id,
    string UserName,
    string DisplayName,
    string Role);

public sealed record McpAuthenticationContext(
    string Type,
    IReadOnlyList<string> Scopes);

public sealed record CardGetInput
{
    public int? BoardId { get; init; }
    public int? Id { get; init; }
    public bool Archived { get; init; }
}

public sealed record CardOptionsGetInput
{
    public int? Id { get; init; }
}

public sealed record CardAttachmentDeleteInput
{
    public int? BoardId { get; init; }
    public int? CardId { get; init; }
    public int? Id { get; init; }
}

public sealed record CardAttachmentDeleteOutput(int Id, string Outcome);

public sealed record CardAttachmentDownloadInput
{
    public int? BoardId { get; init; }
    public int? Id { get; init; }
}

public sealed record CardAttachmentDownloadOutput(string Url, string Method, IReadOnlyDictionary<string, string> Headers, DateTime ExpiresAtUtc);

public sealed record CardAttachmentUploadInput
{
    public int? BoardId { get; init; }
    public int? CardId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string? ContentType { get; init; }
    public long? ByteLength { get; init; }
}

public sealed record CardAttachmentUploadOutput(string Url, string Method, IReadOnlyDictionary<string, string> Headers,
    long ByteLength, string MarkdownSnippet, DateTime ExpiresAtUtc);

public sealed record CardOptionsGetOutput(
    int Id,
    IReadOnlyList<McpCardOptionColumn> Columns,
    IReadOnlyList<McpCardOptionMember> Members,
    IReadOnlyList<McpCardOptionCardType> CardTypes,
    int DefaultCardTypeId,
    IReadOnlyList<McpCardOptionTag> Tags,
    IReadOnlyList<McpCardOptionSlick> Slicks);

public sealed record McpCardOptionColumn(
    int Id,
    string Title);

public sealed record McpCardOptionMember(
    int UserId,
    string UserName,
    string DisplayName,
    string Role);

public sealed record McpCardOptionCardType(
    int Id,
    string Name,
    string? Emoji);

public sealed record McpCardOptionTag(
    int Id,
    string Name,
    string? Emoji,
    McpStyle Style);

public sealed record McpCardOptionSlick(
    int Id,
    string Name,
    McpStyle Style);

public sealed record CardCreateInput
{
    public int? BoardId { get; init; }
    public int? ColumnId { get; init; }
    public int? CardTypeId { get; init; }
    public int? AssignedUserId { get; init; }
    public string? SlickName { get; init; }
    public string? ExternalUrl { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string>? TagNames { get; init; }
}

public sealed record CardUpdateInput
{
    private int? _assignedUserId;
    private bool _assignedUserIdSpecified;
    private string? _slickName;
    private bool _slickNameSpecified;
    private string? _externalUrl;
    private bool _externalUrlSpecified;

    public int? BoardId { get; init; }
    public int? Id { get; init; }
    public int? ColumnId { get; init; }
    public int? CardTypeId { get; init; }
    public int? AssignedUserId
    {
        get => _assignedUserId;
        init
        {
            _assignedUserId = value;
            _assignedUserIdSpecified = true;
        }
    }
    public string? SlickName
    {
        get => _slickName;
        init
        {
            _slickName = value;
            _slickNameSpecified = true;
        }
    }
    public string? ExternalUrl
    {
        get => _externalUrl;
        init
        {
            _externalUrl = value;
            _externalUrlSpecified = true;
        }
    }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> TagNames { get; init; } = [];

    [JsonIgnore]
    public bool AssignedUserIdSpecified => _assignedUserIdSpecified;

    [JsonIgnore]
    public bool SlickNameSpecified => _slickNameSpecified;

    [JsonIgnore]
    public bool ExternalUrlSpecified => _externalUrlSpecified;
}

public sealed record CardSearchInput
{
    public int? BoardId { get; init; }
    public string Query { get; init; } = string.Empty;
    public int Offset { get; init; }
    public int Limit { get; init; } = 20;
    public bool Archived { get; init; }
}

public sealed record CardMoveInput
{
    public int? BoardId { get; init; }
    public int? Id { get; init; }
    public int? ColumnId { get; init; }
    public int? AfterId { get; init; }
}

public sealed record CardDeleteInput
{
    public int? BoardId { get; init; }
    public int? Id { get; init; }
}

public sealed record CardCommentCreateInput
{
    public int? BoardId { get; init; }
    public int? Id { get; init; }
    public string Text { get; init; } = string.Empty;
}

public sealed record SlickCreateInput
{
    public int? BoardId { get; init; }
    public string Name { get; init; } = string.Empty;
    public McpStyle? Style { get; init; }
}

public sealed record SlickUpdateInput
{
    private string? _name;
    private McpStyle? _style;

    public int? BoardId { get; init; }
    public int? Id { get; init; }
    public string? Name
    {
        get => _name;
        init
        {
            _name = value;
            NameSpecified = true;
        }
    }
    public McpStyle? Style
    {
        get => _style;
        init
        {
            _style = value;
            StyleSpecified = true;
        }
    }

    [JsonIgnore]
    public bool NameSpecified { get; private init; }

    [JsonIgnore]
    public bool StyleSpecified { get; private init; }
}

public sealed record SlickDeleteInput
{
    public int? BoardId { get; init; }
    public int? Id { get; init; }
}

public sealed record McpSlickSnapshot(
    int Id,
    string Name,
    McpStyle Style,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record SlickMutationOutput(
    McpSlickSnapshot Slick,
    string Outcome);

public sealed record SlickDeleteOutput(
    string Outcome);

public sealed record TagCreateInput
{
    private string? _emoji;
    private McpStyle? _style;

    public int? BoardId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Emoji
    {
        get => _emoji;
        init
        {
            _emoji = value;
            EmojiSpecified = true;
        }
    }
    public McpStyle? Style
    {
        get => _style;
        init
        {
            _style = value;
            StyleSpecified = true;
        }
    }

    [JsonIgnore]
    public bool EmojiSpecified { get; private init; }

    [JsonIgnore]
    public bool StyleSpecified { get; private init; }
}

public sealed record TagUpdateInput
{
    private string? _name;
    private string? _emoji;
    private McpStyle? _style;

    public int? BoardId { get; init; }
    public string CurrentTagName { get; init; } = string.Empty;
    public string? Name
    {
        get => _name;
        init
        {
            _name = value;
            NameSpecified = true;
        }
    }
    public string? Emoji
    {
        get => _emoji;
        init
        {
            _emoji = value;
            EmojiSpecified = true;
        }
    }
    public McpStyle? Style
    {
        get => _style;
        init
        {
            _style = value;
            StyleSpecified = true;
        }
    }

    [JsonIgnore]
    public bool NameSpecified { get; private init; }

    [JsonIgnore]
    public bool EmojiSpecified { get; private init; }

    [JsonIgnore]
    public bool StyleSpecified { get; private init; }
}

public sealed record TagDeleteInput
{
    public int? BoardId { get; init; }
    public int? Id { get; init; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "styleName")]
[JsonDerivedType(typeof(McpAutoStyle), "auto")]
[JsonDerivedType(typeof(McpPresetStyle), "presets")]
[JsonDerivedType(typeof(McpSolidStyle), "solid")]
[JsonDerivedType(typeof(McpGradientStyle), "gradient")]
public abstract record McpStyle;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record McpAutoStyle : McpStyle;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record McpPresetStyle : McpStyle
{
    public int? PresetIndex { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record McpSolidStyle : McpStyle
{
    public string? BackgroundColor { get; init; }
    public string? TextColorMode { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TextColor { get; init; }

    public string? BorderMode { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BorderColor { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record McpGradientStyle : McpStyle
{
    public string? LeftColor { get; init; }
    public string? RightColor { get; init; }
    public string? TextColorMode { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TextColor { get; init; }

    public string? BorderMode { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BorderColor { get; init; }
}

public sealed record McpTagSnapshot(
    int Id,
    string Name,
    string? Emoji,
    McpStyle Style,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record TagMutationOutput(
    McpTagSnapshot Tag,
    string Outcome);

public sealed record TagDeleteOutput(
    string Outcome);

public sealed record CardMutationOutput(
    int Id,
    string Outcome);

public sealed record CardCommentMutationOutput(
    int Id,
    string Outcome);
