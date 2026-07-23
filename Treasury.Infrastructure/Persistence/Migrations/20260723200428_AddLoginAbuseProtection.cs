using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginAbuseProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedLoginAttempts",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastFailedLoginAtUtc",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LoginFailureWindowStartedAtUtc",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LoginLockoutEndUtc",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_LoginLockoutEndUtc",
                table: "Users",
                column: "LoginLockoutEndUtc");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_FailedLoginAttempts",
                table: "Users",
                sql: "\"FailedLoginAttempts\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_LoginLockoutEndUtc",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_FailedLoginAttempts",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FailedLoginAttempts",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastFailedLoginAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LoginFailureWindowStartedAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LoginLockoutEndUtc",
                table: "Users");
        }
    }
}
