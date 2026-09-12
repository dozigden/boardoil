using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentThumbnails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ThumbnailStorageKey",
                table: "CardAttachments",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardAttachments_ThumbnailStorageKey",
                table: "CardAttachments",
                column: "ThumbnailStorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CardAttachments_ThumbnailStorageKey",
                table: "CardAttachments");

            migrationBuilder.DropColumn(
                name: "ThumbnailStorageKey",
                table: "CardAttachments");
        }
    }
}
