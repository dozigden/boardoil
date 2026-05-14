using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddCardCommentPostedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CardComments_CardId_CreatedAtUtc_Id",
                table: "CardComments");

            migrationBuilder.AddColumn<DateTime>(
                name: "PostedAtUtc",
                table: "CardComments",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql("""
                UPDATE "CardComments"
                SET "PostedAtUtc" = "CreatedAtUtc";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CardComments_CardId_PostedAtUtc_Id",
                table: "CardComments",
                columns: new[] { "CardId", "PostedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CardComments_CardId_PostedAtUtc_Id",
                table: "CardComments");

            migrationBuilder.DropColumn(
                name: "PostedAtUtc",
                table: "CardComments");

            migrationBuilder.CreateIndex(
                name: "IX_CardComments_CardId_CreatedAtUtc_Id",
                table: "CardComments",
                columns: new[] { "CardId", "CreatedAtUtc", "Id" });
        }
    }
}
