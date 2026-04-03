using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardMembersRbac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BoardMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BoardId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardMembers_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoardMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoardMembers_BoardId_Role",
                table: "BoardMembers",
                columns: new[] { "BoardId", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_BoardMembers_BoardId_UserId",
                table: "BoardMembers",
                columns: new[] { "BoardId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoardMembers_UserId",
                table: "BoardMembers",
                column: "UserId");

            // Rollout safety: preserve existing access by seeding every active user as board owner.
            migrationBuilder.Sql(
                """
                INSERT INTO "BoardMembers" ("BoardId", "UserId", "Role", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT
                    b."Id",
                    u."Id",
                    1,
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP
                FROM "Boards" b
                CROSS JOIN "Users" u
                WHERE u."IsActive" = 1
                    AND NOT EXISTS (
                        SELECT 1
                        FROM "BoardMembers" bm
                        WHERE bm."BoardId" = b."Id"
                            AND bm."UserId" = u."Id"
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoardMembers");
        }
    }
}
