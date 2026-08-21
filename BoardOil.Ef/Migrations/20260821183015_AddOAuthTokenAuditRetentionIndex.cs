using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddOAuthTokenAuditRetentionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OAuthTokenAudits_OAuthClientId_OccurredAtUtc",
                table: "OAuthTokenAudits",
                columns: new[] { "OAuthClientId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OAuthTokenAudits_OAuthClientId_OccurredAtUtc",
                table: "OAuthTokenAudits");
        }
    }
}
