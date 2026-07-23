using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthenticationSecurityEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthenticationMethod",
                table: "AuthenticationSessions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "password");

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "AuthenticationSessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "AuthenticationSessions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuthenticationSecurityEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthenticationSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IdentifierHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthenticationSecurityEvents", x => x.Id);
                    table.CheckConstraint("CK_AuthenticationSecurityEvents_Outcome", "\"Outcome\" IN ('succeeded','failed','blocked')");
                    table.ForeignKey(
                        name: "FK_AuthenticationSecurityEvents_AuthenticationSessions_Authent~",
                        column: x => x.AuthenticationSessionId,
                        principalTable: "AuthenticationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuthenticationSecurityEvents_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuthenticationSecurityEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSecurityEvents_AuthenticationSessionId",
                table: "AuthenticationSecurityEvents",
                column: "AuthenticationSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSecurityEvents_EventType",
                table: "AuthenticationSecurityEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSecurityEvents_IdentifierHash",
                table: "AuthenticationSecurityEvents",
                column: "IdentifierHash");

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSecurityEvents_OrganizationId_OccurredAtUtc",
                table: "AuthenticationSecurityEvents",
                columns: new[] { "OrganizationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSecurityEvents_UserId_OccurredAtUtc",
                table: "AuthenticationSecurityEvents",
                columns: new[] { "UserId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthenticationSecurityEvents");

            migrationBuilder.DropColumn(
                name: "AuthenticationMethod",
                table: "AuthenticationSessions");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "AuthenticationSessions");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "AuthenticationSessions");
        }
    }
}
