using BoardOil.Contracts.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BoardOil.Services.Style;

public static class StyleDefinitionCodec
{
    public const string AutoStyleName = "auto";
    public const string PresetsStyleName = "presets";
    public const string SolidStyleName = "solid";
    public const string GradientStyleName = "gradient";
    public const int PresetCount = 12;
    public const int DefaultPresetIndex = 2;

    private static readonly Regex HexColourRegex = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);
    private static readonly string[] DefaultSolidColours =
    [
        "#35165A",
        "#9D8ABF",
        "#69C1CE",
        "#E8C07D",
        "#CD474E",
        "#9BBEF8",
        "#F17437",
        "#32CDA0"
    ];

    public static StyleDefinitionParseResult ParseForWrite(
        string? styleName,
        string? stylePropertiesJson,
        string styleNameProperty = "styleName",
        string stylePropertiesProperty = "stylePropertiesJson") =>
        Parse(styleName, stylePropertiesJson, false, styleNameProperty, stylePropertiesProperty);

    public static StyleDefinitionParseResult ParseCompatible(
        string? styleName,
        string? stylePropertiesJson,
        string styleNameProperty = "styleName",
        string stylePropertiesProperty = "stylePropertiesJson") =>
        Parse(styleName, stylePropertiesJson, true, styleNameProperty, stylePropertiesProperty);

    public static string Serialise(StyleDefinition definition)
    {
        return definition switch
        {
            AutoStyleDefinition => "{}",
            PresetStyleDefinition preset => JsonSerializer.Serialize(new
            {
                presetIndex = preset.PresetIndex
            }),
            SolidStyleDefinition solid => SerialiseSolid(solid),
            GradientStyleDefinition gradient => SerialiseGradient(gradient),
            _ => throw new ArgumentOutOfRangeException(nameof(definition))
        };
    }

    public static string GetStyleName(StyleDefinition definition) =>
        definition.Kind switch
        {
            StyleKind.Auto => AutoStyleName,
            StyleKind.Presets => PresetsStyleName,
            StyleKind.Solid => SolidStyleName,
            StyleKind.Gradient => GradientStyleName,
            _ => throw new ArgumentOutOfRangeException(nameof(definition))
        };

    public static StyleDefinition CreateDefault(string? styleName = SolidStyleName)
    {
        var styleKind = NormaliseStyleKind(styleName) ?? StyleKind.Solid;
        return CreateDefault(styleKind);
    }

    public static StyleDefinition CreateDefault(StyleKind styleKind)
    {
        if (styleKind == StyleKind.Auto)
        {
            return new AutoStyleDefinition();
        }

        if (styleKind == StyleKind.Presets)
        {
            return new PresetStyleDefinition(DefaultPresetIndex);
        }

        var colour = PickDefaultSolidColour();
        var manualOptions = new StyleManualOptions(
            StyleTextColourMode.Auto,
            null,
            StyleBorderMode.Auto,
            null);
        if (styleKind == StyleKind.Gradient)
        {
            return new GradientStyleDefinition(colour, colour, manualOptions);
        }

        return new SolidStyleDefinition(colour, manualOptions);
    }

    public static StyleKind? NormaliseStyleKind(string? styleName)
    {
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return null;
        }

        return styleName.Trim().ToLowerInvariant() switch
        {
            AutoStyleName => StyleKind.Auto,
            PresetsStyleName => StyleKind.Presets,
            SolidStyleName => StyleKind.Solid,
            GradientStyleName => StyleKind.Gradient,
            _ => null
        };
    }

    private static StyleDefinitionParseResult Parse(
        string? styleName,
        string? stylePropertiesJson,
        bool allowLegacyDefaults,
        string styleNameProperty,
        string stylePropertiesProperty)
    {
        var styleKind = NormaliseStyleKind(styleName);
        if (styleKind is null)
        {
            return Invalid(
                styleNameProperty,
                "Style name must be 'solid', 'gradient', 'auto', or 'presets'.");
        }

        if (string.IsNullOrWhiteSpace(stylePropertiesJson))
        {
            return Invalid(stylePropertiesProperty, "Style properties must be valid JSON object text.");
        }

        try
        {
            using var document = JsonDocument.Parse(stylePropertiesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Invalid(stylePropertiesProperty, "Style properties must be valid JSON object text.");
            }

            return ParseProperties(styleKind.Value, document.RootElement, allowLegacyDefaults, stylePropertiesProperty);
        }
        catch (JsonException)
        {
            return Invalid(stylePropertiesProperty, "Style properties must be valid JSON object text.");
        }
        catch (ArgumentException)
        {
            return Invalid(stylePropertiesProperty, "Style properties must be valid JSON object text.");
        }
    }

    private static StyleDefinitionParseResult ParseProperties(
        StyleKind styleKind,
        JsonElement properties,
        bool allowLegacyDefaults,
        string propertyName)
    {
        if (styleKind == StyleKind.Auto)
        {
            return Valid(new AutoStyleDefinition());
        }

        if (styleKind == StyleKind.Presets)
        {
            return ParsePreset(properties, allowLegacyDefaults, propertyName);
        }

        var manualOptionsResult = ParseManualOptions(properties, allowLegacyDefaults, propertyName);
        if (manualOptionsResult.Error is not null)
        {
            return Invalid(manualOptionsResult.Error);
        }

        if (styleKind == StyleKind.Solid)
        {
            var backgroundColour = ReadHexColour(properties, "backgroundColor");
            if (backgroundColour is null)
            {
                return Invalid(propertyName, "Solid style backgroundColor must be a six-digit hex colour.");
            }

            return Valid(new SolidStyleDefinition(backgroundColour, manualOptionsResult.Options!));
        }

        var leftColour = ReadHexColour(properties, "leftColor");
        var rightColour = ReadHexColour(properties, "rightColor");
        if (leftColour is null || rightColour is null)
        {
            return Invalid(propertyName, "Gradient style leftColor and rightColor must be six-digit hex colours.");
        }

        return Valid(new GradientStyleDefinition(leftColour, rightColour, manualOptionsResult.Options!));
    }

    private static StyleDefinitionParseResult ParsePreset(
        JsonElement properties,
        bool allowLegacyValues,
        string propertyName)
    {
        if (!properties.TryGetProperty("presetIndex", out var presetIndexElement))
        {
            return Invalid(propertyName, $"Preset style presetIndex must be an integer from 0 to {PresetCount - 1}.");
        }

        var presetIndex = ReadPresetIndex(presetIndexElement, allowLegacyValues);
        if (presetIndex is null || presetIndex < 0 || presetIndex >= PresetCount)
        {
            return Invalid(propertyName, $"Preset style presetIndex must be an integer from 0 to {PresetCount - 1}.");
        }

        return Valid(new PresetStyleDefinition(presetIndex.Value));
    }

    private static int? ReadPresetIndex(JsonElement element, bool allowLegacyValues)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var numericIndex))
        {
            return numericIndex;
        }

        if (allowLegacyValues
            && element.ValueKind == JsonValueKind.String
            && int.TryParse(
                element.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var stringIndex))
        {
            return stringIndex;
        }

        return null;
    }

    private static ManualOptionsParseResult ParseManualOptions(
        JsonElement properties,
        bool allowLegacyDefaults,
        string propertyName)
    {
        var textColourMode = ReadTextColourMode(properties);
        if (textColourMode is null)
        {
            return InvalidManual(propertyName, "Manual styles require textColorMode to be 'auto' or 'custom'.");
        }

        var borderMode = ReadBorderMode(properties);
        if (borderMode is null && allowLegacyDefaults && !properties.TryGetProperty("borderMode", out _))
        {
            borderMode = StyleBorderMode.Auto;
        }

        if (borderMode is null)
        {
            return InvalidManual(propertyName, "Manual styles require borderMode to be 'auto', 'custom', or 'none'.");
        }

        string? textColour = null;
        if (textColourMode == StyleTextColourMode.Custom)
        {
            textColour = ReadHexColour(properties, "textColor");
            if (textColour is null)
            {
                return InvalidManual(propertyName, "Custom textColor must be a six-digit hex colour.");
            }
        }

        string? borderColour = null;
        if (borderMode == StyleBorderMode.Custom)
        {
            borderColour = ReadHexColour(properties, "borderColor");
            if (borderColour is null)
            {
                return InvalidManual(propertyName, "Custom borderColor must be a six-digit hex colour.");
            }
        }

        return new ManualOptionsParseResult(
            new StyleManualOptions(textColourMode.Value, textColour, borderMode.Value, borderColour),
            null);
    }

    private static StyleTextColourMode? ReadTextColourMode(JsonElement properties)
    {
        if (!properties.TryGetProperty("textColorMode", out var element)
            || element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return element.GetString() switch
        {
            "auto" => StyleTextColourMode.Auto,
            "custom" => StyleTextColourMode.Custom,
            _ => null
        };
    }

    private static StyleBorderMode? ReadBorderMode(JsonElement properties)
    {
        if (!properties.TryGetProperty("borderMode", out var element)
            || element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return element.GetString() switch
        {
            "auto" => StyleBorderMode.Auto,
            "custom" => StyleBorderMode.Custom,
            "none" => StyleBorderMode.None,
            _ => null
        };
    }

    private static string? ReadHexColour(JsonElement properties, string propertyName)
    {
        if (!properties.TryGetProperty(propertyName, out var element)
            || element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var colour = element.GetString()?.Trim();
        if (colour is null || !HexColourRegex.IsMatch(colour))
        {
            return null;
        }

        return colour.ToUpperInvariant();
    }

    private static string SerialiseSolid(SolidStyleDefinition style)
    {
        var properties = BuildManualProperties(style.ManualOptions);
        properties.Insert(0, new KeyValuePair<string, object?>("backgroundColor", style.BackgroundColour));
        return JsonSerializer.Serialize(properties.ToDictionary());
    }

    private static string SerialiseGradient(GradientStyleDefinition style)
    {
        var properties = BuildManualProperties(style.ManualOptions);
        properties.Insert(0, new KeyValuePair<string, object?>("rightColor", style.RightColour));
        properties.Insert(0, new KeyValuePair<string, object?>("leftColor", style.LeftColour));
        return JsonSerializer.Serialize(properties.ToDictionary());
    }

    private static List<KeyValuePair<string, object?>> BuildManualProperties(StyleManualOptions options)
    {
        var properties = new List<KeyValuePair<string, object?>>
        {
            new("textColorMode", options.TextColourMode == StyleTextColourMode.Custom ? "custom" : "auto"),
            new("borderMode", ToBorderModeValue(options.BorderMode))
        };
        if (options.BorderMode == StyleBorderMode.Custom)
        {
            properties.Add(new KeyValuePair<string, object?>("borderColor", options.BorderColour));
        }

        if (options.TextColourMode == StyleTextColourMode.Custom)
        {
            properties.Add(new KeyValuePair<string, object?>("textColor", options.TextColour));
        }

        return properties;
    }

    private static string ToBorderModeValue(StyleBorderMode borderMode) =>
        borderMode switch
        {
            StyleBorderMode.Auto => "auto",
            StyleBorderMode.Custom => "custom",
            StyleBorderMode.None => "none",
            _ => throw new ArgumentOutOfRangeException(nameof(borderMode))
        };

    private static string PickDefaultSolidColour() =>
        DefaultSolidColours[RandomNumberGenerator.GetInt32(DefaultSolidColours.Length)];

    private static StyleDefinitionParseResult Valid(StyleDefinition definition) =>
        new(definition, []);

    private static StyleDefinitionParseResult Invalid(string property, string message) =>
        new(null, [new ValidationError(property, message)]);

    private static StyleDefinitionParseResult Invalid(ValidationError error) =>
        new(null, [error]);

    private static ManualOptionsParseResult InvalidManual(string property, string message) =>
        new(null, new ValidationError(property, message));

    private sealed record ManualOptionsParseResult(
        StyleManualOptions? Options,
        ValidationError? Error);
}

public sealed record StyleDefinitionParseResult(
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
