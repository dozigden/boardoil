using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class RepairTagNameNormalisation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use the same .NET casing as current tag writes, including for databases
            // whose historical backfill used SQLite's ASCII-only UPPER.
            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE "__TagNameRepair" (
                    "TagId" INTEGER NOT NULL PRIMARY KEY,
                    "SurvivorId" INTEGER NOT NULL,
                    "NormalisedName" TEXT NOT NULL
                );

                INSERT INTO "__TagNameRepair" ("TagId", "SurvivorId", "NormalisedName")
                SELECT "Id",
                    FIRST_VALUE("Id") OVER (
                        PARTITION BY "BoardId", bo_upper_invariant("Name")
                        ORDER BY CASE WHEN "NormalisedName" = bo_upper_invariant("Name") THEN 0 ELSE 1 END, "Id"
                    ),
                    bo_upper_invariant("Name")
                FROM "Tags";

                -- Keep an existing survivor link when a card already has both tags.
                DELETE FROM "CardTags"
                WHERE "Id" IN (
                    SELECT "Id" FROM (
                        SELECT ct."Id", ROW_NUMBER() OVER (
                            PARTITION BY ct."CardId", repair."SurvivorId"
                            ORDER BY CASE WHEN ct."TagId" = repair."SurvivorId" THEN 0 ELSE 1 END, ct."Id"
                        ) AS "LinkRank"
                        FROM "CardTags" ct
                        INNER JOIN "__TagNameRepair" repair ON repair."TagId" = ct."TagId"
                    ) WHERE "LinkRank" > 1
                );

                UPDATE "CardTags"
                SET "TagId" = (SELECT "SurvivorId" FROM "__TagNameRepair" WHERE "TagId" = "CardTags"."TagId")
                WHERE "TagId" IN (SELECT "TagId" FROM "__TagNameRepair" WHERE "TagId" <> "SurvivorId");

                DELETE FROM "Tags"
                WHERE "Id" IN (SELECT "TagId" FROM "__TagNameRepair" WHERE "TagId" <> "SurvivorId");

                -- This repairs derived values and references, not user edits; retain timestamps.
                UPDATE "Tags"
                SET "NormalisedName" = (SELECT "NormalisedName" FROM "__TagNameRepair" WHERE "TagId" = "Tags"."Id")
                WHERE "NormalisedName" <> (SELECT "NormalisedName" FROM "__TagNameRepair" WHERE "TagId" = "Tags"."Id");

                DROP TABLE "__TagNameRepair";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Duplicate tags and links cannot be reconstructed after merging.
        }
    }
}
