using BoardOil.Ef;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BoardOilDbContext))]
    [Migration("20260530200000_NormaliseLegacyStylePropertiesJsonPayloads")]
    public partial class NormaliseLegacyStylePropertiesJsonPayloads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ApplyStylePayloadNormalisation(migrationBuilder, "Tags");
            ApplyStylePayloadNormalisation(migrationBuilder, "CardTypes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op: this upgrade canonicalises historical JSON payload shape.
        }

        private static void ApplyStylePayloadNormalisation(MigrationBuilder migrationBuilder, string tableName)
        {
            NormaliseAutoPayload(migrationBuilder, tableName);
            NormalisePresetsPayload(migrationBuilder, tableName);
            NormaliseSolidPayload(migrationBuilder, tableName);
            NormaliseGradientPayload(migrationBuilder, tableName);
        }

        private static void NormaliseAutoPayload(MigrationBuilder migrationBuilder, string tableName)
        {
            migrationBuilder.Sql($"""
                UPDATE "{tableName}"
                SET
                    "StyleName" = 'auto',
                    "StylePropertiesJson" = json_object()
                WHERE lower("StyleName") = 'auto';
                """);
        }

        private static void NormalisePresetsPayload(MigrationBuilder migrationBuilder, string tableName)
        {
            migrationBuilder.Sql($"""
                UPDATE "{tableName}"
                SET
                    "StyleName" = 'presets',
                    "StylePropertiesJson" = json_object(
                        'presetIndex',
                        CASE
                            WHEN
                                json_valid("StylePropertiesJson") = 1
                                AND json_type("StylePropertiesJson", '$') = 'object'
                                AND json_type("StylePropertiesJson", '$.presetIndex') IN ('integer', 'real')
                                AND CAST(json_extract("StylePropertiesJson", '$.presetIndex') AS INTEGER) BETWEEN 0 AND 7
                                THEN CAST(json_extract("StylePropertiesJson", '$.presetIndex') AS INTEGER)
                            WHEN
                                json_valid("StylePropertiesJson") = 1
                                AND json_type("StylePropertiesJson", '$') = 'object'
                                AND json_type("StylePropertiesJson", '$.presetIndex') = 'text'
                                AND LENGTH(trim(json_extract("StylePropertiesJson", '$.presetIndex'))) > 0
                                AND (
                                    trim(json_extract("StylePropertiesJson", '$.presetIndex')) NOT GLOB '*[^0-9]*'
                                    OR (
                                        substr(trim(json_extract("StylePropertiesJson", '$.presetIndex')), 1, 1) IN ('+', '-')
                                        AND length(trim(json_extract("StylePropertiesJson", '$.presetIndex'))) > 1
                                        AND substr(trim(json_extract("StylePropertiesJson", '$.presetIndex')), 2) NOT GLOB '*[^0-9]*'
                                    )
                                )
                                AND CAST(trim(json_extract("StylePropertiesJson", '$.presetIndex')) AS INTEGER) BETWEEN 0 AND 7
                                THEN CAST(trim(json_extract("StylePropertiesJson", '$.presetIndex')) AS INTEGER)
                            ELSE 2
                        END
                    )
                WHERE lower("StyleName") = 'presets';
                """);
        }

        private static void NormaliseSolidPayload(MigrationBuilder migrationBuilder, string tableName)
        {
            migrationBuilder.Sql($"""
                UPDATE "{tableName}"
                SET
                    "StyleName" = 'solid',
                    "StylePropertiesJson" = (
                        WITH payload AS (
                            SELECT
                                CASE
                                    WHEN
                                        json_valid("StylePropertiesJson") = 1
                                        AND json_type("StylePropertiesJson", '$') = 'object'
                                        THEN trim(COALESCE(
                                            json_extract("StylePropertiesJson", '$.backgroundColor'),
                                            json_extract("StylePropertiesJson", '$.leftColor'),
                                            json_extract("StylePropertiesJson", '$.rightColor'),
                                            ''
                                        ))
                                    ELSE ''
                                END AS rawBackgroundColor,
                                CASE
                                    WHEN
                                        json_valid("StylePropertiesJson") = 1
                                        AND json_type("StylePropertiesJson", '$') = 'object'
                                        THEN lower(trim(COALESCE(json_extract("StylePropertiesJson", '$.textColorMode'), '')))
                                    ELSE ''
                                END AS rawTextColorMode,
                                CASE
                                    WHEN
                                        json_valid("StylePropertiesJson") = 1
                                        AND json_type("StylePropertiesJson", '$') = 'object'
                                        THEN lower(trim(COALESCE(json_extract("StylePropertiesJson", '$.borderMode'), '')))
                                    ELSE ''
                                END AS rawBorderMode,
                                CASE
                                    WHEN
                                        json_valid("StylePropertiesJson") = 1
                                        AND json_type("StylePropertiesJson", '$') = 'object'
                                        THEN trim(COALESCE(json_extract("StylePropertiesJson", '$.textColor'), ''))
                                    ELSE ''
                                END AS rawTextColor,
                                CASE
                                    WHEN
                                        json_valid("StylePropertiesJson") = 1
                                        AND json_type("StylePropertiesJson", '$') = 'object'
                                        THEN trim(COALESCE(json_extract("StylePropertiesJson", '$.borderColor'), ''))
                                    ELSE ''
                                END AS rawBorderColor
                        ),
                        normalised AS (
                            SELECT
                                CASE
                                    WHEN
                                        length(rawBackgroundColor) = 7
                                        AND upper(rawBackgroundColor) GLOB '#[0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F]'
                                        THEN upper(rawBackgroundColor)
                                    ELSE '#69C1CE'
                                END AS backgroundColor,
                                CASE
                                    WHEN rawTextColorMode = 'custom' THEN 'custom'
                                    ELSE 'auto'
                                END AS textColorMode,
                                CASE
                                    WHEN rawBorderMode = 'custom' THEN 'custom'
                                    WHEN rawBorderMode = 'none' THEN 'none'
                                    ELSE 'auto'
                                END AS borderMode,
                                CASE
                                    WHEN
                                        length(rawTextColor) = 7
                                        AND upper(rawTextColor) GLOB '#[0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F]'
                                        THEN upper(rawTextColor)
                                    ELSE '#111827'
                                END AS textColor,
                                CASE
                                    WHEN
                                        length(rawBorderColor) = 7
                                        AND upper(rawBorderColor) GLOB '#[0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F]'
                                        THEN upper(rawBorderColor)
                                    ELSE '#D8CDEC'
                                END AS borderColor
                            FROM payload
                        )
                        SELECT
                            CASE
                                WHEN textColorMode = 'custom' AND borderMode = 'custom'
                                    THEN json_object(
                                        'backgroundColor', backgroundColor,
                                        'textColorMode', textColorMode,
                                        'borderMode', borderMode,
                                        'textColor', textColor,
                                        'borderColor', borderColor
                                    )
                                WHEN textColorMode = 'custom'
                                    THEN json_object(
                                        'backgroundColor', backgroundColor,
                                        'textColorMode', textColorMode,
                                        'borderMode', borderMode,
                                        'textColor', textColor
                                    )
                                WHEN borderMode = 'custom'
                                    THEN json_object(
                                        'backgroundColor', backgroundColor,
                                        'textColorMode', textColorMode,
                                        'borderMode', borderMode,
                                        'borderColor', borderColor
                                    )
                                ELSE json_object(
                                    'backgroundColor', backgroundColor,
                                    'textColorMode', textColorMode,
                                    'borderMode', borderMode
                                )
                            END
                        FROM normalised
                    )
                WHERE lower("StyleName") = 'solid';
                """);
        }

        private static void NormaliseGradientPayload(MigrationBuilder migrationBuilder, string tableName)
        {
            migrationBuilder.Sql($"""
                UPDATE "{tableName}"
                SET
                    "StyleName" = 'gradient',
                    "StylePropertiesJson" = (
                        WITH payload AS (
                            SELECT
                                CASE
                                    WHEN
                                        json_valid("StylePropertiesJson") = 1
                                        AND json_type("StylePropertiesJson", '$') = 'object'
                                        THEN trim(COALESCE(json_extract("StylePropertiesJson", '$.backgroundColor'), ''))
                                    ELSE ''
                                END AS rawBackgroundColor,
                                CASE
                                    WHEN
                                        json_valid("StylePropertiesJson") = 1
                                        AND json_type("StylePropertiesJson", '$') = 'object'
                                        THEN trim(COALESCE(json_extract("StylePropertiesJson", '$.leftColor'), ''))
                                    ELSE ''
                                END AS rawLeftColor,
                                CASE
                                    WHEN
                                        json_valid("StylePropertiesJson") = 1
                                        AND json_type("StylePropertiesJson", '$') = 'object'
                                        THEN trim(COALESCE(json_extract("StylePropertiesJson", '$.rightColor'), ''))
                                    ELSE ''
                                END AS rawRightColor,
                                CASE
                                    WHEN
                                        json_valid("StylePropertiesJson") = 1
                                        AND json_type("StylePropertiesJson", '$') = 'object'
                                        THEN lower(trim(COALESCE(json_extract("StylePropertiesJson", '$.textColorMode'), '')))
                                    ELSE ''
                                END AS rawTextColorMode,
                                CASE
                                    WHEN
                                        json_valid("StylePropertiesJson") = 1
                                        AND json_type("StylePropertiesJson", '$') = 'object'
                                        THEN lower(trim(COALESCE(json_extract("StylePropertiesJson", '$.borderMode'), '')))
                                    ELSE ''
                                END AS rawBorderMode,
                                CASE
                                    WHEN
                                        json_valid("StylePropertiesJson") = 1
                                        AND json_type("StylePropertiesJson", '$') = 'object'
                                        THEN trim(COALESCE(json_extract("StylePropertiesJson", '$.textColor'), ''))
                                    ELSE ''
                                END AS rawTextColor,
                                CASE
                                    WHEN
                                        json_valid("StylePropertiesJson") = 1
                                        AND json_type("StylePropertiesJson", '$') = 'object'
                                        THEN trim(COALESCE(json_extract("StylePropertiesJson", '$.borderColor'), ''))
                                    ELSE ''
                                END AS rawBorderColor
                        ),
                        normalised AS (
                            SELECT
                                CASE
                                    WHEN
                                        length(rawBackgroundColor) = 7
                                        AND upper(rawBackgroundColor) GLOB '#[0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F]'
                                        THEN upper(rawBackgroundColor)
                                    ELSE NULL
                                END AS fallbackColor,
                                CASE
                                    WHEN
                                        length(rawLeftColor) = 7
                                        AND upper(rawLeftColor) GLOB '#[0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F]'
                                        THEN upper(rawLeftColor)
                                    ELSE NULL
                                END AS leftColorCandidate,
                                CASE
                                    WHEN
                                        length(rawRightColor) = 7
                                        AND upper(rawRightColor) GLOB '#[0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F]'
                                        THEN upper(rawRightColor)
                                    ELSE NULL
                                END AS rightColorCandidate,
                                CASE
                                    WHEN rawTextColorMode = 'custom' THEN 'custom'
                                    ELSE 'auto'
                                END AS textColorMode,
                                CASE
                                    WHEN rawBorderMode = 'custom' THEN 'custom'
                                    WHEN rawBorderMode = 'none' THEN 'none'
                                    ELSE 'auto'
                                END AS borderMode,
                                CASE
                                    WHEN
                                        length(rawTextColor) = 7
                                        AND upper(rawTextColor) GLOB '#[0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F]'
                                        THEN upper(rawTextColor)
                                    ELSE '#111827'
                                END AS textColor,
                                CASE
                                    WHEN
                                        length(rawBorderColor) = 7
                                        AND upper(rawBorderColor) GLOB '#[0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F]'
                                        THEN upper(rawBorderColor)
                                    ELSE '#D8CDEC'
                                END AS borderColor
                            FROM payload
                        ),
                        resolved AS (
                            SELECT
                                CASE
                                    WHEN leftColorCandidate IS NOT NULL THEN leftColorCandidate
                                    WHEN rightColorCandidate IS NOT NULL THEN rightColorCandidate
                                    WHEN fallbackColor IS NOT NULL THEN fallbackColor
                                    ELSE '#69C1CE'
                                END AS leftColor,
                                CASE
                                    WHEN rightColorCandidate IS NOT NULL THEN rightColorCandidate
                                    WHEN leftColorCandidate IS NOT NULL THEN leftColorCandidate
                                    WHEN fallbackColor IS NOT NULL THEN fallbackColor
                                    ELSE '#69C1CE'
                                END AS rightColor,
                                textColorMode,
                                borderMode,
                                textColor,
                                borderColor
                            FROM normalised
                        )
                        SELECT
                            CASE
                                WHEN textColorMode = 'custom' AND borderMode = 'custom'
                                    THEN json_object(
                                        'leftColor', leftColor,
                                        'rightColor', rightColor,
                                        'textColorMode', textColorMode,
                                        'borderMode', borderMode,
                                        'textColor', textColor,
                                        'borderColor', borderColor
                                    )
                                WHEN textColorMode = 'custom'
                                    THEN json_object(
                                        'leftColor', leftColor,
                                        'rightColor', rightColor,
                                        'textColorMode', textColorMode,
                                        'borderMode', borderMode,
                                        'textColor', textColor
                                    )
                                WHEN borderMode = 'custom'
                                    THEN json_object(
                                        'leftColor', leftColor,
                                        'rightColor', rightColor,
                                        'textColorMode', textColorMode,
                                        'borderMode', borderMode,
                                        'borderColor', borderColor
                                    )
                                ELSE json_object(
                                    'leftColor', leftColor,
                                    'rightColor', rightColor,
                                    'textColorMode', textColorMode,
                                    'borderMode', borderMode
                                )
                            END
                        FROM resolved
                    )
                WHERE lower("StyleName") = 'gradient';
                """);
        }
    }
}
