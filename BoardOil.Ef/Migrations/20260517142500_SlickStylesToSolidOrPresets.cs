using BoardOil.Ef;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    [DbContext(typeof(BoardOilDbContext))]
    [Migration("20260517142500_SlickStylesToSolidOrPresets")]
    public partial class SlickStylesToSolidOrPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Slicks
                SET
                    StyleName = 'presets',
                    StylePropertiesJson = '{"presetIndex":2}',
                    UpdatedAtUtc = CURRENT_TIMESTAMP
                WHERE StyleName = 'auto';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Slicks
                SET
                    StyleName = 'auto',
                    StylePropertiesJson = '{}',
                    UpdatedAtUtc = CURRENT_TIMESTAMP
                WHERE StyleName = 'presets';
                """);
        }
    }
}
