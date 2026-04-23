using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddUserEmailAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Users",
                type: "TEXT",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalisedEmail",
                table: "Users",
                type: "TEXT",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                WITH base_values AS (
                    SELECT
                        "Id",
                        "UserName",
                        lower("UserName" || '@localhost') AS "BaseNormalisedEmail",
                        ROW_NUMBER() OVER (
                            PARTITION BY lower("UserName" || '@localhost')
                            ORDER BY "Id"
                        ) AS "DuplicateIndex"
                    FROM "Users"
                )
                UPDATE "Users"
                SET
                    "Email" = CASE
                        WHEN (
                            SELECT "DuplicateIndex"
                            FROM base_values
                            WHERE base_values."Id" = "Users"."Id"
                        ) = 1 THEN (
                            SELECT "UserName" || '@localhost'
                            FROM base_values
                            WHERE base_values."Id" = "Users"."Id"
                        )
                        ELSE (
                            SELECT "UserName" || '+' || "Id" || '@localhost'
                            FROM base_values
                            WHERE base_values."Id" = "Users"."Id"
                        )
                    END,
                    "NormalisedEmail" = CASE
                        WHEN (
                            SELECT "DuplicateIndex"
                            FROM base_values
                            WHERE base_values."Id" = "Users"."Id"
                        ) = 1 THEN (
                            SELECT "BaseNormalisedEmail"
                            FROM base_values
                            WHERE base_values."Id" = "Users"."Id"
                        )
                        ELSE (
                            SELECT lower("UserName" || '+' || "Id" || '@localhost')
                            FROM base_values
                            WHERE base_values."Id" = "Users"."Id"
                        )
                    END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalisedEmail",
                table: "Users",
                column: "NormalisedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_NormalisedEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NormalisedEmail",
                table: "Users");
        }
    }
}
