using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations;

public partial class UseSingleUniqueUserEmail : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Native column operations keep email repair and schema enforcement
        // in one transaction, without a table rebuild that disables foreign keys.
        migrationBuilder.Sql(
            """
            CREATE TEMP TABLE "__UserEmailRepair" ("Plan" TEXT NOT NULL);
            INSERT INTO "__UserEmailRepair"
            SELECT bo_email_repair_plan_v1(json_group_array(json_object('Id', "Id", 'Email', "Email")))
            FROM "Users";

            ALTER TABLE "Users" ADD COLUMN "__RepairedEmail" TEXT COLLATE BO_EMAIL NOT NULL DEFAULT '';
            UPDATE "Users"
            SET "__RepairedEmail" = (
                SELECT json_extract(value, '$.NewEmail')
                FROM json_each((SELECT "Plan" FROM "__UserEmailRepair"))
                WHERE json_extract(value, '$.Id') = "Users"."Id"
            );

            DROP INDEX "IX_Users_NormalisedEmail";
            ALTER TABLE "Users" DROP COLUMN "NormalisedEmail";
            ALTER TABLE "Users" DROP COLUMN "Email";
            ALTER TABLE "Users" RENAME COLUMN "__RepairedEmail" TO "Email";
            CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");
            DROP TABLE "__UserEmailRepair";
            """);
        // User-edit timestamps and ID-based relationships are preserved by this repair.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Restore the old schema with correct derived values, retaining repaired addresses.
        migrationBuilder.Sql(
            """
            DROP INDEX "IX_Users_Email";
            ALTER TABLE "Users" ADD COLUMN "NormalisedEmail" TEXT NOT NULL DEFAULT '';
            UPDATE "Users" SET "NormalisedEmail" = bo_normalise_email("Email");
            ALTER TABLE "Users" ADD COLUMN "__OriginalEmail" TEXT NOT NULL DEFAULT '';
            UPDATE "Users" SET "__OriginalEmail" = "Email";
            ALTER TABLE "Users" DROP COLUMN "Email";
            ALTER TABLE "Users" RENAME COLUMN "__OriginalEmail" TO "Email";
            CREATE UNIQUE INDEX "IX_Users_NormalisedEmail" ON "Users" ("NormalisedEmail");
            """);
    }
}
