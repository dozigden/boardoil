using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class MinimiseOAuthTokenAuditData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OAuthTokenAudits_PresentedTokenId",
                table: "OAuthTokenAudits");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "OAuthTokenAudits");

            migrationBuilder.DropColumn(
                name: "PresentedTokenId",
                table: "OAuthTokenAudits");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "OAuthTokenAudits");

            migrationBuilder.AddColumn<string>(
                name: "RequestedScopes",
                table: "OAuthTokenAudits",
                type: "TEXT",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestedScopes",
                table: "OAuthTokenAudits");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "OAuthTokenAudits",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql(
                """
                UPDATE "OAuthTokenAudits"
                SET "CreatedAtUtc" = "OccurredAtUtc";
                """);

            migrationBuilder.AddColumn<string>(
                name: "PresentedTokenId",
                table: "OAuthTokenAudits",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "OAuthTokenAudits",
                type: "TEXT",
                maxLength: 400,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OAuthTokenAudits_PresentedTokenId",
                table: "OAuthTokenAudits",
                column: "PresentedTokenId");
        }
    }
}
