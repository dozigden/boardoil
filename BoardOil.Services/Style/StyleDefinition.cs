namespace BoardOil.Services.Style;

public enum StyleKind
{
    Auto,
    Presets,
    Solid,
    Gradient
}

public enum StyleTextColourMode
{
    Auto,
    Custom
}

public enum StyleBorderMode
{
    Auto,
    Custom,
    None
}

public sealed record StyleManualOptions(
    StyleTextColourMode TextColourMode,
    string? TextColour,
    StyleBorderMode BorderMode,
    string? BorderColour);

public abstract record StyleDefinition(StyleKind Kind);

public sealed record AutoStyleDefinition() : StyleDefinition(StyleKind.Auto);

public sealed record PresetStyleDefinition(int PresetIndex) : StyleDefinition(StyleKind.Presets);

public sealed record SolidStyleDefinition(
    string BackgroundColour,
    StyleManualOptions ManualOptions) : StyleDefinition(StyleKind.Solid);

public sealed record GradientStyleDefinition(
    string LeftColour,
    string RightColour,
    StyleManualOptions ManualOptions) : StyleDefinition(StyleKind.Gradient);
