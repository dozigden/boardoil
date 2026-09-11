using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddCardAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    CardId = table.Column<int>(type: "INTEGER", nullable: true),
                    ArchivedCardId = table.Column<int>(type: "INTEGER", nullable: true),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    NormalisedFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ByteLength = table.Column<long>(type: "INTEGER", nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardAttachments", x => x.Id);
                    table.CheckConstraint("CK_CardAttachments_ExactlyOneOwner", "(State IN (0, 1, 2)) AND NOT (CardId IS NOT NULL AND ArchivedCardId IS NOT NULL) AND (State != 1 OR CardId IS NOT NULL OR ArchivedCardId IS NOT NULL) AND (State != 2 OR (CardId IS NULL AND ArchivedCardId IS NULL))");
                    table.ForeignKey(
                        name: "FK_CardAttachments_ArchivedCards_ArchivedCardId",
                        column: x => x.ArchivedCardId,
                        principalTable: "ArchivedCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardAttachments_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardAttachments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TemporaryBoardPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StorageKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemporaryBoardPackages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardAttachments_ArchivedCardId_NormalisedFileName",
                table: "CardAttachments",
                columns: new[] { "ArchivedCardId", "NormalisedFileName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardAttachments_CardId_NormalisedFileName",
                table: "CardAttachments",
                columns: new[] { "CardId", "NormalisedFileName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardAttachments_CreatedByUserId",
                table: "CardAttachments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CardAttachments_State",
                table: "CardAttachments",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_CardAttachments_StorageKey",
                table: "CardAttachments",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryBoardPackages_StorageKey",
                table: "TemporaryBoardPackages",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardAttachments");

            migrationBuilder.DropTable(
                name: "TemporaryBoardPackages");
        }
    }
}
