using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class ClearSyntheticClientPasswordHashes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // UserIdentityType.Client is persisted as 1.
            migrationBuilder.Sql(
                """
                UPDATE "Users"
                SET "PasswordHash" = NULL
                WHERE "IdentityType" = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Synthetic password hashes were random, unused credentials and cannot be reconstructed.
        }
    }
}
