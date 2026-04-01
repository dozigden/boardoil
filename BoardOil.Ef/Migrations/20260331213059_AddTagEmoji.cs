using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddTagEmoji : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Emoji",
                table: "Tags",
                type: "TEXT",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Emoji",
                table: "Tags");
        }
    }
}
