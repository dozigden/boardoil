using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddArchivedCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchivedCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BoardId = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalCardId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NormalisedTitle = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 20000, nullable: false),
                    CardTypeName = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CardTypeEmoji = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    OriginalColumnName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TagNamesCsv = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OriginalCreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OriginalUpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivedCards_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedCards_BoardId_ArchivedAtUtc_Id",
                table: "ArchivedCards",
                columns: new[] { "BoardId", "ArchivedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedCards_BoardId_NormalisedTitle",
                table: "ArchivedCards",
                columns: new[] { "BoardId", "NormalisedTitle" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedCards_OriginalCardId",
                table: "ArchivedCards",
                column: "OriginalCardId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivedCards");
        }
    }
}
