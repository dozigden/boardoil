using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomFieldValuesJson",
                table: "Cards",
                type: "TEXT",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CardTypes_BoardId_Id",
                table: "CardTypes",
                columns: new[] { "BoardId", "Id" });

            migrationBuilder.CreateTable(
                name: "CustomFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BoardId = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    NormalisedCode = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    NormalisedName = table.Column<string>(type: "TEXT", nullable: false),
                    Emoji = table.Column<string>(type: "TEXT", nullable: true),
                    DataType = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Nullable = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShowOnCard = table.Column<bool>(type: "INTEGER", nullable: false),
                    AppliesToAllCardTypes = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFields", x => x.Id);
                    table.UniqueConstraint("AK_CustomFields_BoardId_Id", x => new { x.BoardId, x.Id });
                    table.ForeignKey(
                        name: "FK_CustomFields_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomFieldCardTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BoardId = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomFieldId = table.Column<int>(type: "INTEGER", nullable: false),
                    CardTypeId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldCardTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomFieldCardTypes_CardTypes_BoardId_CardTypeId",
                        columns: x => new { x.BoardId, x.CardTypeId },
                        principalTable: "CardTypes",
                        principalColumns: new[] { "BoardId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomFieldCardTypes_CustomFields_BoardId_CustomFieldId",
                        columns: x => new { x.BoardId, x.CustomFieldId },
                        principalTable: "CustomFields",
                        principalColumns: new[] { "BoardId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldCardTypes_BoardId_CardTypeId",
                table: "CustomFieldCardTypes",
                columns: new[] { "BoardId", "CardTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldCardTypes_BoardId_CustomFieldId",
                table: "CustomFieldCardTypes",
                columns: new[] { "BoardId", "CustomFieldId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldCardTypes_CustomFieldId_CardTypeId",
                table: "CustomFieldCardTypes",
                columns: new[] { "CustomFieldId", "CardTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomFields_BoardId_NormalisedCode",
                table: "CustomFields",
                columns: new[] { "BoardId", "NormalisedCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomFields_BoardId_NormalisedName",
                table: "CustomFields",
                columns: new[] { "BoardId", "NormalisedName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomFieldCardTypes");

            migrationBuilder.DropTable(
                name: "CustomFields");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CardTypes_BoardId_Id",
                table: "CardTypes");

            migrationBuilder.DropColumn(
                name: "CustomFieldValuesJson",
                table: "Cards");
        }
    }
}
