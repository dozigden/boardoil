using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddCardAssignedUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedUserId",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_AssignedUserId",
                table: "Cards",
                column: "AssignedUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Users_AssignedUserId",
                table: "Cards",
                column: "AssignedUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Users_AssignedUserId",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_AssignedUserId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "AssignedUserId",
                table: "Cards");
        }
    }
}
