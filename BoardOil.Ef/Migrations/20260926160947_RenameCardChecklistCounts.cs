using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class RenameCardChecklistCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TotalTaskCount",
                table: "Cards",
                newName: "TotalChecklistItemCount");

            migrationBuilder.RenameColumn(
                name: "CompletedTaskCount",
                table: "Cards",
                newName: "CompletedChecklistItemCount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TotalChecklistItemCount",
                table: "Cards",
                newName: "TotalTaskCount");

            migrationBuilder.RenameColumn(
                name: "CompletedChecklistItemCount",
                table: "Cards",
                newName: "CompletedTaskCount");
        }
    }
}
