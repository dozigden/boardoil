using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddCardTagTagIdForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CardTags_CardId_TagName",
                table: "CardTags");

            migrationBuilder.DropIndex(
                name: "IX_CardTags_TagName",
                table: "CardTags");

            migrationBuilder.AddColumn<int>(
                name: "TagId",
                table: "CardTags",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                """
                INSERT INTO "Tags" ("Name", "NormalisedName", "StyleName", "StylePropertiesJson", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT MIN(ct."TagName"), UPPER(ct."TagName"), 'solid', '{"backgroundColor":"#35165A","textColorMode":"auto"}', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                FROM "CardTags" ct
                LEFT JOIN "Tags" t ON t."NormalisedName" = UPPER(ct."TagName")
                WHERE t."Id" IS NULL
                GROUP BY UPPER(ct."TagName");
                """);

            migrationBuilder.Sql(
                """
                UPDATE "CardTags"
                SET "TagId" = (
                    SELECT t."Id"
                    FROM "Tags" t
                    WHERE t."NormalisedName" = UPPER("CardTags"."TagName")
                    LIMIT 1
                );
                """);

            migrationBuilder.Sql("""DELETE FROM "CardTags" WHERE "TagId" IS NULL;""");

            migrationBuilder.Sql(
                """
                DELETE FROM "CardTags"
                WHERE "Id" NOT IN (
                    SELECT MIN(ct."Id")
                    FROM "CardTags" ct
                    GROUP BY ct."CardId", ct."TagId"
                );
                """);

            migrationBuilder.AlterColumn<int>(
                name: "TagId",
                table: "CardTags",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "TagName",
                table: "CardTags");

            migrationBuilder.CreateIndex(
                name: "IX_CardTags_CardId_TagId",
                table: "CardTags",
                columns: new[] { "CardId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardTags_TagId",
                table: "CardTags",
                column: "TagId");

            migrationBuilder.AddForeignKey(
                name: "FK_CardTags_Tags_TagId",
                table: "CardTags",
                column: "TagId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CardTags_Tags_TagId",
                table: "CardTags");

            migrationBuilder.DropIndex(
                name: "IX_CardTags_CardId_TagId",
                table: "CardTags");

            migrationBuilder.DropIndex(
                name: "IX_CardTags_TagId",
                table: "CardTags");

            migrationBuilder.AddColumn<string>(
                name: "TagName",
                table: "CardTags",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "CardTags"
                SET "TagName" = (
                    SELECT t."Name"
                    FROM "Tags" t
                    WHERE t."Id" = "CardTags"."TagId"
                    LIMIT 1
                );
                """);

            migrationBuilder.Sql("""DELETE FROM "CardTags" WHERE "TagName" IS NULL;""");

            migrationBuilder.AlterColumn<string>(
                name: "TagName",
                table: "CardTags",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "TagId",
                table: "CardTags");

            migrationBuilder.CreateIndex(
                name: "IX_CardTags_CardId_TagName",
                table: "CardTags",
                columns: new[] { "CardId", "TagName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardTags_TagName",
                table: "CardTags",
                column: "TagName");
        }
    }
}
