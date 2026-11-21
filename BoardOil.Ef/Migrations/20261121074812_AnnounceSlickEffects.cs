using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AnnounceSlickEffects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasUnreadAnnouncement",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Release housekeeping preserves the account's last edit time.
            migrationBuilder.Sql("""UPDATE "Users" SET "HasUnreadAnnouncement" = 1 WHERE "IdentityType" = 0""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasUnreadAnnouncement",
                table: "Users");
        }
    }
}
