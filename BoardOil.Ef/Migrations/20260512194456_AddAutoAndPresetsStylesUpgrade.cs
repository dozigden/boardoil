using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoAndPresetsStylesUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ApplySolidToPresetsUpgrade(migrationBuilder, "Tags");
            ApplySolidToPresetsUpgrade(migrationBuilder, "CardTypes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RevertPresetsToSolid(migrationBuilder, "Tags");
            RevertPresetsToSolid(migrationBuilder, "CardTypes");
        }

        private static void ApplySolidToPresetsUpgrade(MigrationBuilder migrationBuilder, string tableName)
        {
            var presetPalette = new[]
            {
                "#35165A",
                "#9D8ABF",
                "#69C1CE",
                "#E8C07D",
                "#CD474E",
                "#9BBEF8",
                "#F17437",
                "#32CDA0"
            };

            for (var index = 0; index < presetPalette.Length; index++)
            {
                migrationBuilder.Sql($"""
                    UPDATE {tableName}
                    SET
                        StyleName = 'presets',
                        StylePropertiesJson = json_remove(json_set(StylePropertiesJson, '$.presetIndex', {index}), '$.backgroundColor')
                    WHERE
                        StyleName = 'solid'
                        AND json_valid(StylePropertiesJson) = 1
                        AND UPPER(json_extract(StylePropertiesJson, '$.backgroundColor')) = '{presetPalette[index]}';
                    """);
            }
        }

        private static void RevertPresetsToSolid(MigrationBuilder migrationBuilder, string tableName)
        {
            var presetPalette = new[]
            {
                "#35165A",
                "#9D8ABF",
                "#69C1CE",
                "#E8C07D",
                "#CD474E",
                "#9BBEF8",
                "#F17437",
                "#32CDA0"
            };

            for (var index = 0; index < presetPalette.Length; index++)
            {
                migrationBuilder.Sql($"""
                    UPDATE {tableName}
                    SET
                        StyleName = 'solid',
                        StylePropertiesJson = json_set(json_remove(StylePropertiesJson, '$.presetIndex'), '$.backgroundColor', '{presetPalette[index]}')
                    WHERE
                        StyleName = 'presets'
                        AND json_valid(StylePropertiesJson) = 1
                        AND CAST(json_extract(StylePropertiesJson, '$.presetIndex') AS INTEGER) = {index};
                    """);
            }
        }
    }
}
