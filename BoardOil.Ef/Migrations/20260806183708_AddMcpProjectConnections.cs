using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpProjectConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "McpProjectConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PublicId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ClientAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    AllowedScopesCsv = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedByUserName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    RevokedByUserName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "McpProjectConnections");
        }
    }
}
