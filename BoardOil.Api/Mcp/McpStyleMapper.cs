using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Services.Style;

namespace BoardOil.Api.Mcp;

internal static class McpStyleMapper
{
    public static McpStyleMappingResult Parse(McpStyle style)
    {
        var validationErrors = new List<ValidationError>();
        StyleDefinition? definition = style switch
        {
            McpAutoStyle => new AutoStyleDefinition(),
            McpPresetStyle preset => ParsePreset(preset, validationErrors),
            McpSolidStyle solid => ParseSolid(solid, validationErrors),
            McpGradientStyle gradient => ParseGradient(gradient, validationErrors),
            _ => null
        };
        if (definition is null || validationErrors.Count > 0)
        {
            return new McpStyleMappingResult(null, validationErrors);
        }

        var parsed = StyleDefinitionCodec.ParseForWrite(
            StyleDefinitionCodec.GetStyleName(definition),
            StyleDefinitionCodec.Serialise(definition),
            "style.styleName",
            "style");
        if (!parsed.IsValid)
        {
            return new McpStyleMappingResult(null, parsed.ValidationErrors);
        }

        return new McpStyleMappingResult(parsed.Definition, []);
    }

    public static McpStyle ToMcp(StyleDefinition definition) =>
        definition switch
        {
            AutoStyleDefinition => new McpAutoStyle(),
            PresetStyleDefinition preset => new McpPresetStyle { PresetIndex = preset.PresetIndex },
            SolidStyleDefinition solid => ToMcp(solid),
            GradientStyleDefinition gradient => ToMcp(gradient),
            _ => throw new ArgumentOutOfRangeException(nameof(definition))
        };

    private static PresetStyleDefinition? ParsePreset(
        McpPresetStyle style,
        List<ValidationError> validationErrors)
    {
        if (style.PresetIndex is null)
        {
            validationErrors.Add(new ValidationError(
                "style.presetIndex",
                "Preset style presetIndex is required."));
            return null;
        }

        return new PresetStyleDefinition(style.PresetIndex.Value);
    }

    private static SolidStyleDefinition? ParseSolid(
        McpSolidStyle style,
        List<ValidationError> validationErrors)
    {
        if (string.IsNullOrWhiteSpace(style.BackgroundColor))
        {
            validationErrors.Add(new ValidationError(
                "style.backgroundColor",
                "Solid style backgroundColor is required."));
        }

        var manualOptions = ParseManualOptions(
            style.TextColorMode,
            style.TextColor,
            style.BorderMode,
            style.BorderColor,
            validationErrors);
        if (string.IsNullOrWhiteSpace(style.BackgroundColor) || manualOptions is null)
        {
            return null;
        }

        return new SolidStyleDefinition(style.BackgroundColor, manualOptions);
    }

    private static GradientStyleDefinition? ParseGradient(
        McpGradientStyle style,
        List<ValidationError> validationErrors)
    {
        if (string.IsNullOrWhiteSpace(style.LeftColor))
        {
            validationErrors.Add(new ValidationError(
                "style.leftColor",
                "Gradient style leftColor is required."));
        }

        if (string.IsNullOrWhiteSpace(style.RightColor))
        {
            validationErrors.Add(new ValidationError(
                "style.rightColor",
                "Gradient style rightColor is required."));
        }

        var manualOptions = ParseManualOptions(
            style.TextColorMode,
            style.TextColor,
            style.BorderMode,
            style.BorderColor,
            validationErrors);
        if (string.IsNullOrWhiteSpace(style.LeftColor)
            || string.IsNullOrWhiteSpace(style.RightColor)
            || manualOptions is null)
        {
            return null;
        }

        return new GradientStyleDefinition(style.LeftColor, style.RightColor, manualOptions);
    }

    private static StyleManualOptions? ParseManualOptions(
        string? textColorMode,
        string? textColor,
        string? borderModeValue,
        string? borderColor,
        List<ValidationError> validationErrors)
    {
        StyleTextColourMode? textColourMode = textColorMode switch
        {
            "auto" => StyleTextColourMode.Auto,
            "custom" => StyleTextColourMode.Custom,
            _ => null
        };
        if (textColourMode is null)
        {
            validationErrors.Add(new ValidationError(
                "style.textColorMode",
                "Manual styles require textColorMode to be 'auto' or 'custom'."));
        }
        else if (textColourMode == StyleTextColourMode.Custom && string.IsNullOrWhiteSpace(textColor))
        {
            validationErrors.Add(new ValidationError("style.textColor", "Custom textColor is required."));
        }
        else if (textColourMode == StyleTextColourMode.Auto && textColor is not null)
        {
            validationErrors.Add(new ValidationError(
                "style.textColor",
                "textColor is only valid when textColorMode is 'custom'."));
        }

        StyleBorderMode? borderMode = borderModeValue switch
        {
            "auto" => StyleBorderMode.Auto,
            "custom" => StyleBorderMode.Custom,
            "none" => StyleBorderMode.None,
            _ => null
        };
        if (borderMode is null)
        {
            validationErrors.Add(new ValidationError(
                "style.borderMode",
                "Manual styles require borderMode to be 'auto', 'custom', or 'none'."));
        }
        else if (borderMode == StyleBorderMode.Custom && string.IsNullOrWhiteSpace(borderColor))
        {
            validationErrors.Add(new ValidationError("style.borderColor", "Custom borderColor is required."));
        }
        else if (borderMode != StyleBorderMode.Custom && borderColor is not null)
        {
            validationErrors.Add(new ValidationError(
                "style.borderColor",
                "borderColor is only valid when borderMode is 'custom'."));
        }

        if (textColourMode is null || borderMode is null || validationErrors.Count > 0)
        {
            return null;
        }

        return new StyleManualOptions(
            textColourMode.Value,
            textColor,
            borderMode.Value,
            borderColor);
    }

    private static McpSolidStyle ToMcp(SolidStyleDefinition style) =>
        new()
        {
            BackgroundColor = style.BackgroundColour,
            TextColorMode = ToMcp(style.ManualOptions.TextColourMode),
            TextColor = style.ManualOptions.TextColour,
            BorderMode = ToMcp(style.ManualOptions.BorderMode),
            BorderColor = style.ManualOptions.BorderColour
        };

    private static McpGradientStyle ToMcp(GradientStyleDefinition style) =>
        new()
        {
            LeftColor = style.LeftColour,
            RightColor = style.RightColour,
            TextColorMode = ToMcp(style.ManualOptions.TextColourMode),
            TextColor = style.ManualOptions.TextColour,
            BorderMode = ToMcp(style.ManualOptions.BorderMode),
            BorderColor = style.ManualOptions.BorderColour
        };

    private static string ToMcp(StyleTextColourMode textColourMode) =>
        textColourMode switch
        {
            StyleTextColourMode.Auto => "auto",
            StyleTextColourMode.Custom => "custom",
            _ => throw new ArgumentOutOfRangeException(nameof(textColourMode))
        };

    private static string ToMcp(StyleBorderMode borderMode) =>
        borderMode switch
        {
            StyleBorderMode.Auto => "auto",
            StyleBorderMode.Custom => "custom",
            StyleBorderMode.None => "none",
            _ => throw new ArgumentOutOfRangeException(nameof(borderMode))
        };
}

internal sealed record McpStyleMappingResult(
    StyleDefinition? Definition,
    IReadOnlyList<ValidationError> ValidationErrors)
{
    public bool IsValid => Definition is not null && ValidationErrors.Count == 0;

    public string StyleName => Definition is null
        ? string.Empty
        : StyleDefinitionCodec.GetStyleName(Definition);

    public string StylePropertiesJson => Definition is null
        ? string.Empty
        : StyleDefinitionCodec.Serialise(Definition);
}
