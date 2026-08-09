using BoardOil.Contracts.Common;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BoardOil.Services.Tag;

public static class TagStyleSchemaValidator
{
    public const string SolidStyleName = "solid";
    public const string GradientStyleName = "gradient";
    public const string AutoStyleName = "auto";
    public const string PresetsStyleName = "presets";
    public const int PresetCount = 12;
    public const int DefaultPresetIndex = 2;

    private static readonly Regex HexColorRegex = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);
    private static readonly string[] PresetPalette =
    [
        "#35165A", // Brand
        "#9D8ABF", // Brand Mid
        "#69C1CE", // Secondary
        "#E8C07D", // Warning
        "#CD474E", // Danger
        "#9BBEF8", // Info
        "#F17437", // Energy
        "#32CDA0"  // Success
    ];

    public static bool IsValidJsonObject(string stylePropertiesJson)
    {
        if (string.IsNullOrWhiteSpace(stylePropertiesJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(stylePropertiesJson);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static string BuildDefaultStylePropertiesJson(string styleName = SolidStyleName)
    {
        var normalisedStyleName = NormaliseStyleName(styleName) ?? SolidStyleName;
        if (normalisedStyleName == PresetsStyleName)
        {
            return JsonSerializer.Serialize(new
            {
                presetIndex = DefaultPresetIndex,
                textColorMode = "auto"
            });
        }

        if (normalisedStyleName == AutoStyleName)
        {
            return "{}";
        }

        return JsonSerializer.Serialize(new
        {
            backgroundColor = PickDefaultTagColor(),
            textColorMode = "auto",
            borderMode = "auto"
        });
    }

    public static string? NormaliseStyleName(string? styleName)
    {
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return null;
        }

        var normalised = styleName.Trim().ToLowerInvariant();
        return normalised is SolidStyleName or GradientStyleName or AutoStyleName or PresetsStyleName
            ? normalised
            : null;
    }

    public static bool TryResolvePresetIndex(string? colorHex, out int presetIndex)
    {
        presetIndex = -1;
        if (!IsHexColor(colorHex ?? string.Empty))
        {
            return false;
        }

        var normalised = colorHex!.Trim().ToUpperInvariant();
        for (var index = 0; index < PresetPalette.Length; index++)
        {
            if (PresetPalette[index] == normalised)
            {
                presetIndex = index;
                return true;
            }
        }

        return false;
    }

    private static bool IsHexColor(string value) =>
        HexColorRegex.IsMatch(value);

    private static string PickDefaultTagColor() =>
        PresetPalette[RandomNumberGenerator.GetInt32(PresetPalette.Length)];
}
