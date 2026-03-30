using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardScopedTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_NormalisedName",
                table: "Tags");

            migrationBuilder.AddColumn<int>(
                name: "BoardId",
                table: "Tags",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                """
                CREATE TABLE "__BoardScopedTagMap" (
                    "OldTagId" INTEGER NOT NULL,
                    "BoardId" INTEGER NOT NULL,
                    "NewTagId" INTEGER NOT NULL
                );
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "Tags" ("BoardId", "Name", "NormalisedName", "StyleName", "StylePropertiesJson", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT usage."BoardId", oldTag."Name", oldTag."NormalisedName", oldTag."StyleName", oldTag."StylePropertiesJson", oldTag."CreatedAtUtc", oldTag."UpdatedAtUtc"
                FROM (
                    SELECT DISTINCT cardTag."TagId" AS "OldTagId", boardColumn."BoardId" AS "BoardId"
                    FROM "CardTags" cardTag
                    INNER JOIN "Cards" card ON card."Id" = cardTag."CardId"
                    INNER JOIN "Columns" boardColumn ON boardColumn."Id" = card."BoardColumnId"
                ) usage
                INNER JOIN "Tags" oldTag ON oldTag."Id" = usage."OldTagId";
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "__BoardScopedTagMap" ("OldTagId", "BoardId", "NewTagId")
                SELECT usage."OldTagId", usage."BoardId", newTag."Id"
                FROM (
                    SELECT DISTINCT cardTag."TagId" AS "OldTagId", boardColumn."BoardId" AS "BoardId"
                    FROM "CardTags" cardTag
                    INNER JOIN "Cards" card ON card."Id" = cardTag."CardId"
                    INNER JOIN "Columns" boardColumn ON boardColumn."Id" = card."BoardColumnId"
                ) usage
                INNER JOIN "Tags" oldTag ON oldTag."Id" = usage."OldTagId"
                INNER JOIN "Tags" newTag ON newTag."BoardId" = usage."BoardId" AND newTag."NormalisedName" = oldTag."NormalisedName";
                """);

            migrationBuilder.Sql(
                """
                UPDATE "CardTags"
                SET "TagId" = (
                    SELECT mapped."NewTagId"
                    FROM "__BoardScopedTagMap" mapped
                    INNER JOIN "Cards" card ON card."Id" = "CardTags"."CardId"
                    INNER JOIN "Columns" boardColumn ON boardColumn."Id" = card."BoardColumnId"
                    WHERE mapped."OldTagId" = "CardTags"."TagId"
                        AND mapped."BoardId" = boardColumn."BoardId"
                    LIMIT 1
                );
                """);

            migrationBuilder.Sql("""DELETE FROM "Tags" WHERE "BoardId" IS NULL;""");

            migrationBuilder.Sql("""DROP TABLE "__BoardScopedTagMap";""");

            migrationBuilder.AlterColumn<int>(
                name: "BoardId",
                table: "Tags",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_BoardId",
                table: "Tags",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_BoardId_NormalisedName",
                table: "Tags",
                columns: new[] { "BoardId", "NormalisedName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_Boards_BoardId",
                table: "Tags",
                column: "BoardId",
                principalTable: "Boards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tags_Boards_BoardId",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Tags_BoardId",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Tags_BoardId_NormalisedName",
                table: "Tags");

            migrationBuilder.AlterColumn<int>(
                name: "BoardId",
                table: "Tags",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.Sql(
                """
                CREATE TABLE "__GlobalTagMap" (
                    "BoardTagId" INTEGER NOT NULL,
                    "GlobalTagId" INTEGER NOT NULL
                );
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "Tags" ("BoardId", "Name", "NormalisedName", "StyleName", "StylePropertiesJson", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT NULL, MIN(tag."Name"), tag."NormalisedName", MIN(tag."StyleName"), MIN(tag."StylePropertiesJson"), MIN(tag."CreatedAtUtc"), MIN(tag."UpdatedAtUtc")
                FROM "Tags" tag
                WHERE tag."BoardId" IS NOT NULL
                GROUP BY tag."NormalisedName";
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "__GlobalTagMap" ("BoardTagId", "GlobalTagId")
                SELECT boardTag."Id", globalTag."Id"
                FROM "Tags" boardTag
                INNER JOIN "Tags" globalTag ON globalTag."BoardId" IS NULL AND globalTag."NormalisedName" = boardTag."NormalisedName"
                WHERE boardTag."BoardId" IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "CardTags"
                SET "TagId" = (
                    SELECT mapped."GlobalTagId"
                    FROM "__GlobalTagMap" mapped
                    WHERE mapped."BoardTagId" = "CardTags"."TagId"
                    LIMIT 1
                );
                """);

            migrationBuilder.Sql("""DELETE FROM "Tags" WHERE "BoardId" IS NOT NULL;""");

            migrationBuilder.Sql("""DROP TABLE "__GlobalTagMap";""");

            migrationBuilder.DropColumn(
                name: "BoardId",
                table: "Tags");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_NormalisedName",
                table: "Tags",
                column: "NormalisedName",
                unique: true);
        }
    }
}
