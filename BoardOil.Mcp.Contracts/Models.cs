using System.Text.Json.Serialization;

namespace BoardOil.Mcp.Contracts;

public sealed record McpToolDefinition(
    string Name,
    string Description,
    string InputSchemaJson,
    string OutputSchemaJson,
    string? RequiredScope);

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
    string? ExternalUrl);

public sealed record McpCardCommentSnapshot(
    int Id,
    int CardId,
    int? AuthorUserId,
    string Text,
    DateTime PostedAtUtc,
    string? AuthorDisplayName,
    string? AuthorImageRelativePath);

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
}

public sealed record CardOptionsGetInput
{
    public int? Id { get; init; }
}

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
    string Name,
    string? Emoji);

public sealed record McpCardOptionSlick(
    string Name);

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

public sealed record TagUpdateInput
{
    private string? _name;
    private string? _emoji;
    private McpTagStyle? _style;

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
    public McpTagStyle? Style
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

[JsonPolymorphic(TypeDiscriminatorPropertyName = "styleName")]
[JsonDerivedType(typeof(McpAutoTagStyle), "auto")]
[JsonDerivedType(typeof(McpPresetTagStyle), "presets")]
[JsonDerivedType(typeof(McpSolidTagStyle), "solid")]
[JsonDerivedType(typeof(McpGradientTagStyle), "gradient")]
public abstract record McpTagStyle;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record McpAutoTagStyle : McpTagStyle;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record McpPresetTagStyle : McpTagStyle
{
    public int? PresetIndex { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record McpSolidTagStyle : McpTagStyle
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
public sealed record McpGradientTagStyle : McpTagStyle
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
    McpTagStyle Style,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record TagMutationOutput(
    McpTagSnapshot Tag,
    string Outcome);

public sealed record CardMutationOutput(
    McpCardSnapshot? Card,
    string Outcome);

public sealed record CardCommentMutationOutput(
    McpCardCommentSnapshot? Comment,
    string Outcome);
