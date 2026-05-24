using System;
using BoardOil.Ef;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BoardOilDbContext))]
    [Migration("20260524143000_AddSystemInfoMessageTable")]
    public partial class AddSystemInfoMessageTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemInfoMessage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Emoji = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    StyleName = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    StylePropertiesJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemInfoMessage", x => x.Id);
                    table.CheckConstraint("CK_SystemInfoMessage_StyleName", "\"StyleName\" IN ('auto', 'presets', 'solid')");
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "SystemInfoMessage" (
                    "Enabled",
                    "Emoji",
                    "Title",
                    "Description",
                    "StyleName",
                    "StylePropertiesJson",
                    "CreatedAtUtc",
                    "UpdatedAtUtc")
                SELECT
                    COALESCE(CAST(json_extract("Value", '$.Enabled') AS INTEGER), 0),
                    json_extract("Value", '$.Emoji'),
                    COALESCE(json_extract("Value", '$.Title'), ''),
                    COALESCE(json_extract("Value", '$.Description'), ''),
                    COALESCE(json_extract("Value", '$.StyleName'), 'presets'),
                    COALESCE(json_extract("Value", '$.StylePropertiesJson'), '{"presetIndex":2}'),
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP
                FROM "AppSettings"
                WHERE "Key" = 'system_info_message_json'
                  AND json_valid("Value") = 1
                LIMIT 1;

                DELETE FROM "AppSettings"
                WHERE "Key" = 'system_info_message_json';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemInfoMessage");
        }
    }
}
