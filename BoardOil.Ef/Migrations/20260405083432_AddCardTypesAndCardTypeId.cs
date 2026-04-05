using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddCardTypesAndCardTypeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BoardId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Emoji = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardTypes_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "CardTypes" ("BoardId", "Name", "Emoji", "IsSystem", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT b."Id", 'Story', NULL, 1, b."CreatedAtUtc", b."UpdatedAtUtc"
                FROM "Boards" b
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "CardTypes" ct
                    WHERE ct."BoardId" = b."Id"
                      AND ct."IsSystem" = 1
                );
                """);

            migrationBuilder.AddColumn<int>(
                name: "CardTypeId",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Cards" AS c
                SET "CardTypeId" = (
                    SELECT ct."Id"
                    FROM "Columns" col
                    INNER JOIN "CardTypes" ct ON ct."BoardId" = col."BoardId"
                    WHERE col."Id" = c."BoardColumnId"
                      AND ct."IsSystem" = 1
                    ORDER BY ct."Id"
                    LIMIT 1
                )
                WHERE c."CardTypeId" IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "CardTypeId",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardTypes_BoardId",
                table: "CardTypes",
                column: "BoardId",
                unique: true,
                filter: "\"IsSystem\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_CardTypeId",
                table: "Cards",
                column: "CardTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_CardTypes_CardTypeId",
                table: "Cards",
                column: "CardTypeId",
                principalTable: "CardTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_CardTypes_CardTypeId",
                table: "Cards");

            migrationBuilder.DropTable(
                name: "CardTypes");

            migrationBuilder.DropIndex(
                name: "IX_Cards_CardTypeId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "CardTypeId",
                table: "Cards");
        }
    }
}
