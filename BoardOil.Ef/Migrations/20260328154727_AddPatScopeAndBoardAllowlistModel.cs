using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddPatScopeAndBoardAllowlistModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedBoardIdsCsv",
                table: "PersonalAccessTokens",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BoardAccessMode",
                table: "PersonalAccessTokens",
                type: "TEXT",
                maxLength: 24,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedBoardIdsCsv",
                table: "PersonalAccessTokens");

            migrationBuilder.DropColumn(
                name: "BoardAccessMode",
                table: "PersonalAccessTokens");
        }
    }
}
