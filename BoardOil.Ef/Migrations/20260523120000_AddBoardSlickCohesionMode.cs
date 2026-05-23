using BoardOil.Ef;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BoardOilDbContext))]
    [Migration("20260523120000_AddBoardSlickCohesionMode")]
    public partial class AddBoardSlickCohesionMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SlickCohesionModeEnabled",
                table: "Boards",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlickCohesionModeEnabled",
                table: "Boards");
        }
    }
}
