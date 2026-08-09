using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemErrorLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ErrorLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Area = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExceptionType = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    StackTrace = table.Column<string>(type: "TEXT", maxLength: 32768, nullable: true),
                    TraceIdentifier = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    RequestMethod = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    RequestPath = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ActorUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ContextJson = table.Column<string>(type: "TEXT", maxLength: 32768, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_OccurredAtUtc",
                table: "ErrorLogs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_Source_Area",
                table: "ErrorLogs",
                columns: new[] { "Source", "Area" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_TraceIdentifier",
                table: "ErrorLogs",
                column: "TraceIdentifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErrorLogs");
        }
    }
}
