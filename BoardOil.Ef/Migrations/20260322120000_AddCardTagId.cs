using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddCardTagId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardTags_New",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagName = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardTags_New", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardTags_New_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO CardTags_New (CardId, TagName)
                SELECT CardId, TagName
                FROM CardTags;
                """);

            migrationBuilder.DropTable(
                name: "CardTags");

            migrationBuilder.RenameTable(
                name: "CardTags_New",
                newName: "CardTags");

            migrationBuilder.CreateIndex(
                name: "IX_CardTags_CardId_TagName",
                table: "CardTags",
                columns: new[] { "CardId", "TagName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardTags_TagName",
                table: "CardTags",
                column: "TagName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardTags_Old",
                columns: table => new
                {
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagName = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardTags_Old", x => new { x.CardId, x.TagName });
                    table.ForeignKey(
                        name: "FK_CardTags_Old_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO CardTags_Old (CardId, TagName)
                SELECT CardId, TagName
                FROM CardTags;
                """);

            migrationBuilder.DropTable(
                name: "CardTags");

            migrationBuilder.RenameTable(
                name: "CardTags_Old",
                newName: "CardTags");

            migrationBuilder.CreateIndex(
                name: "IX_CardTags_TagName",
                table: "CardTags",
                column: "TagName");
        }
    }
}
