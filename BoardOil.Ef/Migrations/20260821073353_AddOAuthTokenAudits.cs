using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddOAuthTokenAudits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OAuthTokenAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ErrorDescription = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ErrorUri = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    GrantType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    PresentedTokenId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PresentedTokenFingerprint = table.Column<string>(type: "TEXT", maxLength: 71, nullable: true),
                    IssuedRefreshTokenFingerprint = table.Column<string>(type: "TEXT", maxLength: 71, nullable: true),
                    AuthorizationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    OAuthClientId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    OAuthConnectionId = table.Column<int>(type: "INTEGER", nullable: true),
                    OAuthConnectionName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    OwnerUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    OwnerUserName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    OAuthClientDisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Resource = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    TraceIdentifier = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthTokenAudits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OAuthTokenAudits_AuthorizationId",
                table: "OAuthTokenAudits",
                column: "AuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthTokenAudits_IssuedRefreshTokenFingerprint",
                table: "OAuthTokenAudits",
                column: "IssuedRefreshTokenFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthTokenAudits_OAuthConnectionId",
                table: "OAuthTokenAudits",
                column: "OAuthConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthTokenAudits_OccurredAtUtc",
                table: "OAuthTokenAudits",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthTokenAudits_Outcome_OccurredAtUtc",
                table: "OAuthTokenAudits",
                columns: new[] { "Outcome", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OAuthTokenAudits_PresentedTokenFingerprint",
                table: "OAuthTokenAudits",
                column: "PresentedTokenFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthTokenAudits_PresentedTokenId",
                table: "OAuthTokenAudits",
                column: "PresentedTokenId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OAuthTokenAudits");
        }
    }
}
