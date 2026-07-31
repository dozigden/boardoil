using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardScopedCardIdFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ArchivedCards_OriginalCardId",
                table: "ArchivedCards");

            migrationBuilder.AddColumn<int>(
                name: "BoardCardId",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BoardId",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BoardCardIdSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BoardId = table.Column<int>(type: "INTEGER", nullable: false),
                    NextCardId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardCardIdSequences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardCardIdSequences_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                UPDATE "Cards"
                SET "BoardId" = (
                        SELECT "Columns"."BoardId"
                        FROM "Columns"
                        WHERE "Columns"."Id" = "Cards"."BoardColumnId"
                    ),
                    "BoardCardId" = "Id";

                CREATE TEMPORARY TABLE "BoardCardIdArchiveRemap" (
                    "ArchivedCardId" INTEGER NOT NULL PRIMARY KEY,
                    "NewOriginalCardId" INTEGER NOT NULL
                );

                INSERT INTO "BoardCardIdArchiveRemap" ("ArchivedCardId", "NewOriginalCardId")
                SELECT "ArchiveCandidates"."Id",
                       "BoardHighWaterMarks"."HighWaterMark"
                           + ROW_NUMBER() OVER (
                               PARTITION BY "ArchiveCandidates"."BoardId"
                               ORDER BY "ArchiveCandidates"."Id"
                           )
                FROM "ArchivedCards" AS "ArchiveCandidates"
                INNER JOIN (
                    SELECT "Boards"."Id" AS "BoardId",
                           MAX(
                               COALESCE((
                                   SELECT MAX("Cards"."BoardCardId")
                                   FROM "Cards"
                                   WHERE "Cards"."BoardId" = "Boards"."Id"
                               ), 0),
                               COALESCE((
                                   SELECT MAX("ArchivedCards"."OriginalCardId")
                                   FROM "ArchivedCards"
                                   WHERE "ArchivedCards"."BoardId" = "Boards"."Id"
                                     AND "ArchivedCards"."OriginalCardId" > 0
                               ), 0)
                           ) AS "HighWaterMark"
                    FROM "Boards"
                ) AS "BoardHighWaterMarks"
                    ON "BoardHighWaterMarks"."BoardId" = "ArchiveCandidates"."BoardId"
                WHERE "ArchiveCandidates"."OriginalCardId" <= 0
                   OR EXISTS (
                       SELECT 1
                       FROM "Cards"
                       WHERE "Cards"."BoardId" = "ArchiveCandidates"."BoardId"
                         AND "Cards"."BoardCardId" = "ArchiveCandidates"."OriginalCardId"
                   );

                UPDATE "ArchivedCards"
                SET "OriginalCardId" = (
                    SELECT "BoardCardIdArchiveRemap"."NewOriginalCardId"
                    FROM "BoardCardIdArchiveRemap"
                    WHERE "BoardCardIdArchiveRemap"."ArchivedCardId" = "ArchivedCards"."Id"
                )
                WHERE "Id" IN (
                    SELECT "ArchivedCardId"
                    FROM "BoardCardIdArchiveRemap"
                );

                DROP TABLE "BoardCardIdArchiveRemap";

                INSERT INTO "BoardCardIdSequences" ("BoardId", "NextCardId")
                SELECT "Boards"."Id",
                       MAX(
                           COALESCE((
                               SELECT MAX("Cards"."BoardCardId")
                               FROM "Cards"
                               WHERE "Cards"."BoardId" = "Boards"."Id"
                           ), 0),
                           COALESCE((
                               SELECT MAX("ArchivedCards"."OriginalCardId")
                               FROM "ArchivedCards"
                               WHERE "ArchivedCards"."BoardId" = "Boards"."Id"
                                 AND "ArchivedCards"."OriginalCardId" > 0
                           ), 0)
                       ) + 1
                FROM "Boards";
                """);

            migrationBuilder.AlterColumn<int>(
                name: "BoardCardId",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BoardId",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_BoardId_BoardCardId",
                table: "Cards",
                columns: new[] { "BoardId", "BoardCardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedCards_BoardId_OriginalCardId",
                table: "ArchivedCards",
                columns: new[] { "BoardId", "OriginalCardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoardCardIdSequences_BoardId",
                table: "BoardCardIdSequences",
                column: "BoardId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Boards_BoardId",
                table: "Cards",
                column: "BoardId",
                principalTable: "Boards",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Board-scoped card IDs cannot be migrated back to globally unique card IDs. "
                + "Restore the automatic pre-migration database backup instead.");
        }
    }
}
