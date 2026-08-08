using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceMcpProjectConnectionsWithOAuthConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "McpProjectConnections");

            migrationBuilder.CreateTable(
                name: "OAuthConnectionGrants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OAuthConnectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenIddictApplicationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OpenIddictAuthorizationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OAuthClientId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OAuthClientDisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Resource = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    ApprovedScopesCsv = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    RevokedByUserName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    RevocationReason = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthConnectionGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OAuthConnectionGrants_Users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OAuthConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    NormalisedName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveGrantId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    RevokedByUserName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OAuthConnections_OAuthConnectionGrants_ActiveGrantId",
                        column: x => x.ActiveGrantId,
                        principalTable: "OAuthConnectionGrants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OAuthConnections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OAuthConnections_Users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OAuthConnectionGrants_OAuthConnectionId",
                table: "OAuthConnectionGrants",
                column: "OAuthConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthConnectionGrants_OpenIddictApplicationId",
                table: "OAuthConnectionGrants",
                column: "OpenIddictApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthConnectionGrants_OpenIddictAuthorizationId",
                table: "OAuthConnectionGrants",
                column: "OpenIddictAuthorizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OAuthConnectionGrants_RevokedAtUtc",
                table: "OAuthConnectionGrants",
                column: "RevokedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthConnectionGrants_RevokedByUserId",
                table: "OAuthConnectionGrants",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthConnections_ActiveGrantId",
                table: "OAuthConnections",
                column: "ActiveGrantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OAuthConnections_UserId_ResourceType_NormalisedName",
                table: "OAuthConnections",
                columns: new[] { "UserId", "ResourceType", "NormalisedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OAuthConnections_RevokedAtUtc",
                table: "OAuthConnections",
                column: "RevokedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthConnections_RevokedByUserId",
                table: "OAuthConnections",
                column: "RevokedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_OAuthConnectionGrants_OAuthConnections_OAuthConnectionId",
                table: "OAuthConnectionGrants",
                column: "OAuthConnectionId",
                principalTable: "OAuthConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"OAuthConnections\" SET \"ActiveGrantId\" = NULL;");

            migrationBuilder.DropTable(
                name: "OAuthConnectionGrants");

            migrationBuilder.DropTable(
                name: "OAuthConnections");

            migrationBuilder.CreateTable(
                name: "McpProjectConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    RevokedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    AllowedScopesCsv = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    PublicId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedByUserName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpProjectConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpProjectConnections_Users_ClientAccountId",
                        column: x => x.ClientAccountId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_McpProjectConnections_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_McpProjectConnections_Users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_McpProjectConnections_ClientAccountId",
                table: "McpProjectConnections",
                column: "ClientAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_McpProjectConnections_CreatedByUserId",
                table: "McpProjectConnections",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_McpProjectConnections_PublicId",
                table: "McpProjectConnections",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpProjectConnections_RevokedAtUtc",
                table: "McpProjectConnections",
                column: "RevokedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_McpProjectConnections_RevokedByUserId",
                table: "McpProjectConnections",
                column: "RevokedByUserId");
        }
    }
}
