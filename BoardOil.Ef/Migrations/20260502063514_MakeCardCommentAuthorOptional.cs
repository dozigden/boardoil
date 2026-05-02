using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class MakeCardCommentAuthorOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CardComments_Users_AuthorUserId",
                table: "CardComments");

            migrationBuilder.AlterColumn<int>(
                name: "AuthorUserId",
                table: "CardComments",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_CardComments_Users_AuthorUserId",
                table: "CardComments",
                column: "AuthorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CardComments_Users_AuthorUserId",
                table: "CardComments");

            // Rollback safety: remove rows that rely on nullable author semantics
            // before restoring AuthorUserId to a required foreign key.
            migrationBuilder.Sql("DELETE FROM CardComments WHERE AuthorUserId IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "AuthorUserId",
                table: "CardComments",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CardComments_Users_AuthorUserId",
                table: "CardComments",
                column: "AuthorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
