using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangedAtUtc",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                table: "Users",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                table: "AuthenticationSessions",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            /*
             * Preserve sessions that predate security
             * stamps. Future password changes rotate the
             * user's stamp and revoke these sessions.
             */
            migrationBuilder.Sql(
                """
                UPDATE "AuthenticationSessions" AS session
                SET "SecurityStamp" = users."SecurityStamp"
                FROM "Users" AS users
                WHERE session."UserId" = users."Id";
                """);

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                    table.CheckConstraint("CK_PasswordResetTokens_Expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_PasswordResetTokens_FinalState", "NOT (\"ConsumedAtUtc\" IS NOT NULL AND \"RevokedAtUtc\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_ExpiresAtUtc",
                table: "PasswordResetTokens",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_TokenHash",
                table: "PasswordResetTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_UserId",
                table: "PasswordResetTokens",
                column: "UserId",
                unique: true,
                filter: "\"ConsumedAtUtc\" IS NULL AND \"RevokedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasswordResetTokens");

            migrationBuilder.DropColumn(
                name: "PasswordChangedAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "AuthenticationSessions");
        }
    }
}
