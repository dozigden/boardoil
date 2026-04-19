using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class RefineArchivedCardsJsonEnvelope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ArchivedCards_BoardId_NormalisedTitle",
                table: "ArchivedCards");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "ArchivedCards",
                newName: "SearchTitle");

            migrationBuilder.AddColumn<string>(
                name: "SearchTagsJson",
                table: "ArchivedCards",
                type: "TEXT",
                maxLength: 65535,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SearchTextNormalised",
                table: "ArchivedCards",
                type: "TEXT",
                maxLength: 65535,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SnapshotJson",
                table: "ArchivedCards",
                type: "TEXT",
                maxLength: 524288,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE "ArchivedCards"
                SET
                    "SearchTagsJson" = '[]',
                    "SearchTextNormalised" = UPPER(COALESCE("SearchTitle", '')),
                    "SnapshotJson" = json_object(
                        'schema', 'archived-card',
                        'version', 1,
                        'capturedAtUtc', "ArchivedAtUtc",
                        'payload', json_object(
                            'boardId', "BoardId",
                            'originalCardId', "OriginalCardId",
                            'legacyCardTitle', "SearchTitle",
                            'legacyDescription', "Description",
                            'legacyCardTypeName', "CardTypeName",
                            'legacyCardTypeEmoji', "CardTypeEmoji",
                            'legacyOriginalColumnName', "OriginalColumnName",
                            'legacyTagNamesCsv', "TagNamesCsv",
                            'legacyOriginalCreatedAtUtc', "OriginalCreatedAtUtc",
                            'legacyOriginalUpdatedAtUtc', "OriginalUpdatedAtUtc"
                        )
                    );
                """);

            migrationBuilder.DropColumn(
                name: "CardTypeEmoji",
                table: "ArchivedCards");

            migrationBuilder.DropColumn(
                name: "CardTypeName",
                table: "ArchivedCards");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ArchivedCards");

            migrationBuilder.DropColumn(
                name: "NormalisedTitle",
                table: "ArchivedCards");

            migrationBuilder.DropColumn(
                name: "OriginalColumnName",
                table: "ArchivedCards");

            migrationBuilder.DropColumn(
                name: "OriginalCreatedAtUtc",
                table: "ArchivedCards");

            migrationBuilder.DropColumn(
                name: "OriginalUpdatedAtUtc",
                table: "ArchivedCards");

            migrationBuilder.DropColumn(
                name: "TagNamesCsv",
                table: "ArchivedCards");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SearchTagsJson",
                table: "ArchivedCards");

            migrationBuilder.DropColumn(
                name: "SearchTextNormalised",
                table: "ArchivedCards");

            migrationBuilder.DropColumn(
                name: "SnapshotJson",
                table: "ArchivedCards");

            migrationBuilder.RenameColumn(
                name: "SearchTitle",
                table: "ArchivedCards",
                newName: "Title");

            migrationBuilder.AddColumn<string>(
                name: "CardTypeEmoji",
                table: "ArchivedCards",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardTypeName",
                table: "ArchivedCards",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ArchivedCards",
                type: "TEXT",
                maxLength: 20000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalisedTitle",
                table: "ArchivedCards",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OriginalColumnName",
                table: "ArchivedCards",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "OriginalCreatedAtUtc",
                table: "ArchivedCards",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "OriginalUpdatedAtUtc",
                table: "ArchivedCards",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "TagNamesCsv",
                table: "ArchivedCards",
                type: "TEXT",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedCards_BoardId_NormalisedTitle",
                table: "ArchivedCards",
                columns: new[] { "BoardId", "NormalisedTitle" });
        }
    }
}
