using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardOil.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentTransferTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttachmentDownloadTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SecretHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ActorUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    BoardId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttachmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    CardId = table.Column<int>(type: "INTEGER", nullable: true),
                    ArchivedCardId = table.Column<int>(type: "INTEGER", nullable: true),
                    CardNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonalAccessTokenId = table.Column<int>(type: "INTEGER", nullable: true),
                    OAuthTokenId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    OAuthAuthorizationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttachmentDownloadTickets", x => x.Id);
                    table.CheckConstraint("CK_AttachmentDownloadTickets_Credential", "(PersonalAccessTokenId IS NOT NULL AND OAuthTokenId IS NULL AND OAuthAuthorizationId IS NULL) OR (PersonalAccessTokenId IS NULL AND OAuthTokenId IS NOT NULL AND OAuthAuthorizationId IS NOT NULL)");
                    table.CheckConstraint("CK_AttachmentDownloadTickets_Owner", "(CardId IS NOT NULL AND ArchivedCardId IS NULL) OR (CardId IS NULL AND ArchivedCardId IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AttachmentDownloadTickets_CardAttachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "CardAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttachmentTransferAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TicketId = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    BoardId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttachmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Operation = table.Column<int>(type: "INTEGER", nullable: false),
                    CredentialType = table.Column<int>(type: "INTEGER", nullable: false),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttachmentTransferAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttachmentUploadTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SecretHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ActorUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    BoardId = table.Column<int>(type: "INTEGER", nullable: false),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    CardNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    AttachmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    DeclaredByteLength = table.Column<long>(type: "INTEGER", nullable: false),
                    PersonalAccessTokenId = table.Column<int>(type: "INTEGER", nullable: true),
                    OAuthTokenId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    OAuthAuthorizationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FailedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttachmentUploadTickets", x => x.Id);
                    table.CheckConstraint("CK_AttachmentUploadTickets_ByteLength", "DeclaredByteLength >= 0");
                    table.CheckConstraint("CK_AttachmentUploadTickets_Credential", "(PersonalAccessTokenId IS NOT NULL AND OAuthTokenId IS NULL AND OAuthAuthorizationId IS NULL) OR (PersonalAccessTokenId IS NULL AND OAuthTokenId IS NOT NULL AND OAuthAuthorizationId IS NOT NULL)");
                    table.CheckConstraint("CK_AttachmentUploadTickets_State", "State IN (0, 1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_AttachmentUploadTickets_CardAttachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "CardAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentDownloadTickets_AttachmentId",
                table: "AttachmentDownloadTickets",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentDownloadTickets_ExpiresAtUtc",
                table: "AttachmentDownloadTickets",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentTransferAudits_OccurredAtUtc",
                table: "AttachmentTransferAudits",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentTransferAudits_Operation_TicketId",
                table: "AttachmentTransferAudits",
                columns: new[] { "Operation", "TicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentTransferAudits_Outcome_OccurredAtUtc",
                table: "AttachmentTransferAudits",
                columns: new[] { "Outcome", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentTransferAudits_TicketId",
                table: "AttachmentTransferAudits",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentUploadTickets_AttachmentId",
                table: "AttachmentUploadTickets",
                column: "AttachmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentUploadTickets_ExpiresAtUtc",
                table: "AttachmentUploadTickets",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentUploadTickets_State",
                table: "AttachmentUploadTickets",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttachmentDownloadTickets");

            migrationBuilder.DropTable(
                name: "AttachmentTransferAudits");

            migrationBuilder.DropTable(
                name: "AttachmentUploadTickets");
        }
    }
}
