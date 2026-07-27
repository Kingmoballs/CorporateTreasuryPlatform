using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountOrganizationDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BusinessUnits_OrganizationId_LegalEntityId",
                table: "BusinessUnits");

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessUnitId",
                table: "Accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LegalEntityId",
                table: "Accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Accounts" AS account
                SET "LegalEntityId" = (
                    SELECT legal_entity."Id"
                    FROM "LegalEntities" AS legal_entity
                    WHERE legal_entity."OrganizationId" =
                        account."OrganizationId"
                    ORDER BY
                        legal_entity."CreatedAtUtc",
                        legal_entity."Id"
                    LIMIT 1
                )
                WHERE account."LegalEntityId" IS NULL
                  AND EXISTS (
                    SELECT 1
                    FROM "LegalEntities" AS legal_entity
                    WHERE legal_entity."OrganizationId" =
                        account."OrganizationId"
                  );
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_BusinessUnits_OrganizationId_LegalEntityId_Id",
                table: "BusinessUnits",
                columns: new[] { "OrganizationId", "LegalEntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_OrganizationId_BusinessUnitId",
                table: "Accounts",
                columns: new[] { "OrganizationId", "BusinessUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_OrganizationId_LegalEntityId",
                table: "Accounts",
                columns: new[] { "OrganizationId", "LegalEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_OrganizationId_LegalEntityId_BusinessUnitId",
                table: "Accounts",
                columns: new[] { "OrganizationId", "LegalEntityId", "BusinessUnitId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_BusinessUnitRequiresLegalEntity",
                table: "Accounts",
                sql: "\"BusinessUnitId\" IS NULL OR \"LegalEntityId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_BusinessUnits_OrganizationId_LegalEntityId_Busines~",
                table: "Accounts",
                columns: new[] { "OrganizationId", "LegalEntityId", "BusinessUnitId" },
                principalTable: "BusinessUnits",
                principalColumns: new[] { "OrganizationId", "LegalEntityId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_LegalEntities_OrganizationId_LegalEntityId",
                table: "Accounts",
                columns: new[] { "OrganizationId", "LegalEntityId" },
                principalTable: "LegalEntities",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_BusinessUnits_OrganizationId_LegalEntityId_Busines~",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_LegalEntities_OrganizationId_LegalEntityId",
                table: "Accounts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_BusinessUnits_OrganizationId_LegalEntityId_Id",
                table: "BusinessUnits");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_OrganizationId_BusinessUnitId",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_OrganizationId_LegalEntityId",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_OrganizationId_LegalEntityId_BusinessUnitId",
                table: "Accounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_BusinessUnitRequiresLegalEntity",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "BusinessUnitId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "LegalEntityId",
                table: "Accounts");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessUnits_OrganizationId_LegalEntityId",
                table: "BusinessUnits",
                columns: new[] { "OrganizationId", "LegalEntityId" });
        }
    }
}
