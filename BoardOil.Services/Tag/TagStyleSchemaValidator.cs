using BoardOil.Contracts.Contracts;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BoardOil.Services.Tag;

public static class TagStyleSchemaValidator
{
    public const string SolidStyleName = "solid";
    public const string GradientStyleName = "gradient";

    private static readonly Regex HexColorRegex = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);
    private static readonly string[] DefaultTagPalette =
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

    public static IReadOnlyList<ValidationError> Validate(string styleName, string stylePropertiesJson)
    {
        var errors = new List<ValidationError>();
        var normalisedStyleName = NormaliseStyleName(styleName);
        if (normalisedStyleName is null)
        {
            errors.Add(new ValidationError("styleName", "Style name must be 'solid' or 'gradient'."));
            return errors;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(stylePropertiesJson);
        }
        catch (JsonException)
        {
            errors.Add(new ValidationError("stylePropertiesJson", "Style properties must be valid JSON."));
            return errors;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new ValidationError("stylePropertiesJson", "Style properties must be a JSON object."));
                return errors;
            }

            if (normalisedStyleName == SolidStyleName)
            {
                ValidateSolid(document.RootElement, errors);
            }
            else
            {
                ValidateGradient(document.RootElement, errors);
            }

            ValidateTextColor(document.RootElement, errors);
            return errors;
        }
    }

    public static string BuildDefaultStylePropertiesJson() =>
        JsonSerializer.Serialize(new
        {
            backgroundColor = PickDefaultTagColor(),
            textColorMode = "auto"
        });

    public static string? NormaliseStyleName(string? styleName)
    {
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return null;
        }

        var normalised = styleName.Trim().ToLowerInvariant();
        return normalised is SolidStyleName or GradientStyleName
            ? normalised
            : null;
    }

    private static void ValidateSolid(JsonElement root, ICollection<ValidationError> errors)
    {
        if (!TryGetStringProperty(root, "backgroundColor", out var backgroundColor))
        {
            errors.Add(new ValidationError("stylePropertiesJson", "Solid style requires 'backgroundColor'."));
            return;
        }

        if (!IsHexColor(backgroundColor))
        {
            errors.Add(new ValidationError("stylePropertiesJson", "'backgroundColor' must be a #RRGGBB value."));
        }
    }

    private static void ValidateGradient(JsonElement root, ICollection<ValidationError> errors)
    {
        if (!TryGetStringProperty(root, "leftColor", out var leftColor))
        {
            errors.Add(new ValidationError("stylePropertiesJson", "Gradient style requires 'leftColor'."));
        }
        else if (!IsHexColor(leftColor))
        {
            errors.Add(new ValidationError("stylePropertiesJson", "'leftColor' must be a #RRGGBB value."));
        }

        if (!TryGetStringProperty(root, "rightColor", out var rightColor))
        {
            errors.Add(new ValidationError("stylePropertiesJson", "Gradient style requires 'rightColor'."));
        }
        else if (!IsHexColor(rightColor))
        {
            errors.Add(new ValidationError("stylePropertiesJson", "'rightColor' must be a #RRGGBB value."));
        }
    }

    private static void ValidateTextColor(JsonElement root, ICollection<ValidationError> errors)
    {
        if (!TryGetStringProperty(root, "textColorMode", out var textColorMode))
        {
            errors.Add(new ValidationError("stylePropertiesJson", "Style properties require 'textColorMode'."));
            return;
        }

        var mode = textColorMode.Trim().ToLowerInvariant();
        if (mode is not ("auto" or "custom"))
        {
            errors.Add(new ValidationError("stylePropertiesJson", "'textColorMode' must be 'auto' or 'custom'."));
            return;
        }

        if (mode != "custom")
        {
            return;
        }

        if (!TryGetStringProperty(root, "textColor", out var textColor))
        {
            errors.Add(new ValidationError("stylePropertiesJson", "Custom text color mode requires 'textColor'."));
            return;
        }

        if (!IsHexColor(textColor))
        {
            errors.Add(new ValidationError("stylePropertiesJson", "'textColor' must be a #RRGGBB value."));
        }
    }

    private static bool TryGetStringProperty(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var stringValue = property.GetString();
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return false;
        }

        value = stringValue.Trim();
        return true;
    }

    private static bool IsHexColor(string value) =>
        HexColorRegex.IsMatch(value);

    private static string PickDefaultTagColor() =>
        DefaultTagPalette[RandomNumberGenerator.GetInt32(DefaultTagPalette.Length)];
}
