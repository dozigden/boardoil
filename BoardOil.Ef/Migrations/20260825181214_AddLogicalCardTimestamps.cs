using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddLogicalCardTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CardCreatedUtc",
                table: "Cards",
                type: "TEXT",
                nullable: false,
                defaultValue: default(DateTime));

            migrationBuilder.AddColumn<DateTime>(
                name: "CardUpdatedUtc",
                table: "Cards",
                type: "TEXT",
                nullable: false,
                defaultValue: default(DateTime));

            migrationBuilder.Sql(
                """
                UPDATE "Cards"
                SET "CardCreatedUtc" = "CreatedAtUtc",
                    "CardUpdatedUtc" = "UpdatedAtUtc";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CardCreatedUtc",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "CardUpdatedUtc",
                table: "Cards");
        }
    }
}
