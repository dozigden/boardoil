using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardDeletedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Boards",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Boards");
        }
    }
}
