using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_OrganizationMemberships_OrganizationId_UserId_Id",
                table: "OrganizationMemberships",
                columns: new[] { "OrganizationId", "UserId", "Id" });

            migrationBuilder.CreateTable(
                name: "AuthenticationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationMembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthenticationSessions", x => x.Id);
                    table.CheckConstraint("CK_AuthenticationSessions_Activity", "\"LastActivityAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_AuthenticationSessions_Expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_AuthenticationSessions_OrganizationMemberships_Organization~",
                        columns: x => new { x.OrganizationId, x.UserId, x.OrganizationMembershipId },
                        principalTable: "OrganizationMemberships",
                        principalColumns: new[] { "OrganizationId", "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuthenticationSessions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuthenticationSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuthenticationRefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthenticationSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthenticationRefreshTokens", x => x.Id);
                    table.CheckConstraint("CK_AuthenticationRefreshTokens_Expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_AuthenticationRefreshTokens_Replacement", "\"ReplacedByTokenId\" IS NULL OR \"ConsumedAtUtc\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_AuthenticationRefreshTokens_AuthenticationRefreshTokens_Rep~",
                        column: x => x.ReplacedByTokenId,
                        principalTable: "AuthenticationRefreshTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuthenticationRefreshTokens_AuthenticationSessions_Authenti~",
                        column: x => x.AuthenticationSessionId,
                        principalTable: "AuthenticationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationRefreshTokens_AuthenticationSessionId_Expires~",
                table: "AuthenticationRefreshTokens",
                columns: new[] { "AuthenticationSessionId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationRefreshTokens_ReplacedByTokenId",
                table: "AuthenticationRefreshTokens",
                column: "ReplacedByTokenId",
                unique: true,
                filter: "\"ReplacedByTokenId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationRefreshTokens_TokenHash",
                table: "AuthenticationRefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSessions_ExpiresAtUtc",
                table: "AuthenticationSessions",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSessions_OrganizationId_UserId_OrganizationMe~",
                table: "AuthenticationSessions",
                columns: new[] { "OrganizationId", "UserId", "OrganizationMembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSessions_OrganizationMembershipId",
                table: "AuthenticationSessions",
                column: "OrganizationMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSessions_UserId_OrganizationId",
                table: "AuthenticationSessions",
                columns: new[] { "UserId", "OrganizationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthenticationRefreshTokens");

            migrationBuilder.DropTable(
                name: "AuthenticationSessions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_OrganizationMemberships_OrganizationId_UserId_Id",
                table: "OrganizationMemberships");
        }
    }
}
