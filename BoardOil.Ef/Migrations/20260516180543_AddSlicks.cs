using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddSlicks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SlickId",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Slicks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BoardId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    NormalisedName = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    StyleName = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    StylePropertiesJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Slicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Slicks_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_SlickId",
                table: "Cards",
                column: "SlickId");

            migrationBuilder.CreateIndex(
                name: "IX_Slicks_BoardId",
                table: "Slicks",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_Slicks_BoardId_NormalisedName",
                table: "Slicks",
                columns: new[] { "BoardId", "NormalisedName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Slicks_SlickId",
                table: "Cards",
                column: "SlickId",
                principalTable: "Slicks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Slicks_SlickId",
                table: "Cards");

            migrationBuilder.DropTable(
                name: "Slicks");

            migrationBuilder.DropIndex(
                name: "IX_Cards_SlickId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "SlickId",
                table: "Cards");
        }
    }
}
