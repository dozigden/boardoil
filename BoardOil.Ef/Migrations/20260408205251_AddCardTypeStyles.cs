using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddCardTypeStyles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StyleName",
                table: "CardTypes",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "solid");

            migrationBuilder.AddColumn<string>(
                name: "StylePropertiesJson",
                table: "CardTypes",
                type: "TEXT",
                nullable: false,
                defaultValue: "{\"backgroundColor\":\"#FFFFFF\",\"textColorMode\":\"auto\"}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StyleName",
                table: "CardTypes");

            migrationBuilder.DropColumn(
                name: "StylePropertiesJson",
                table: "CardTypes");
        }
    }
}
