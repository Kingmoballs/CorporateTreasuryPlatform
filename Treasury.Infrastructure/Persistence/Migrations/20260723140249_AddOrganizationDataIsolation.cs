using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationDataIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TreasuryTransactions_IdempotencyKey",
                table: "TreasuryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_TreasuryTransactions_Reference",
                table: "TreasuryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentRequests_IdempotencyKey",
                table: "PaymentRequests");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentRolloverRequests_ExecutionIdempotencyKey",
                table: "InvestmentRolloverRequests");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentRolloverRequests_RequestIdempotencyKey",
                table: "InvestmentRolloverRequests");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentPlacements_ActivationIdempotencyKey",
                table: "InvestmentPlacements");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentPlacements_RedemptionIdempotencyKey",
                table: "InvestmentPlacements");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentPlacements_Reference",
                table: "InvestmentPlacements");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentEarlyRedemptionRequests_ExecutionIdempotencyKey",
                table: "InvestmentEarlyRedemptionRequests");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentEarlyRedemptionRequests_RequestIdempotencyKey",
                table: "InvestmentEarlyRedemptionRequests");

            migrationBuilder.DropIndex(
                name: "IX_FxRates_FromCurrency_ToCurrency_RateDateUtc",
                table: "FxRates");

            migrationBuilder.DropIndex(
                name: "IX_CreditFacilityRepayments_IdempotencyKey",
                table: "CreditFacilityRepayments");

            migrationBuilder.DropIndex(
                name: "IX_CreditFacilityRepayments_Reference",
                table: "CreditFacilityRepayments");

            migrationBuilder.DropIndex(
                name: "IX_CreditFacilityDrawdowns_IdempotencyKey",
                table: "CreditFacilityDrawdowns");

            migrationBuilder.DropIndex(
                name: "IX_CreditFacilityDrawdowns_Reference",
                table: "CreditFacilityDrawdowns");

            migrationBuilder.DropIndex(
                name: "IX_CreditFacilities_ActivationIdempotencyKey",
                table: "CreditFacilities");

            migrationBuilder.DropIndex(
                name: "IX_CreditFacilities_Reference",
                table: "CreditFacilities");

            migrationBuilder.DropIndex(
                name: "IX_Counterparties_Code",
                table: "Counterparties");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalPolicies_OperationType_Currency",
                table: "ApprovalPolicies");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_AccountNumber",
                table: "Accounts");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "TreasuryTransactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "TreasuryAlerts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "TransferRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ReversalRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "PaymentRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "LedgerEntries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "InvestmentRolloverRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "InvestmentRolloverDecisions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "InvestmentPlacements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "InvestmentLimits",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "InvestmentEarlyRedemptionRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "InvestmentEarlyRedemptionDecisions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "InvestmentAccrualSnapshots",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "FxRates",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "CreditFacilityRepayments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "CreditFacilityInterestAccrualSnapshots",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "CreditFacilityDrawdowns",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "CreditFacilities",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Counterparties",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "CashFlowForecastItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "BankStatementLines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "BankStatementImports",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "AuditLogs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ApprovalPolicies",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ApprovalDecisions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Accounts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            /*
             * The foundation migration intentionally did
             * not assign existing financial data. Resolve
             * the compatibility organization by its stable
             * code, creating it only when the application
             * has not run its seeder yet. All new tenant
             * columns are backfilled before their foreign
             * keys are created.
             */
            migrationBuilder.Sql(
                """
                INSERT INTO "Organizations"
                (
                    "Id",
                    "Code",
                    "Name",
                    "Slug",
                    "CountryCode",
                    "BaseCurrency",
                    "IsActive",
                    "CreatedAtUtc",
                    "UpdatedAtUtc",
                    "ConcurrencyToken"
                )
                SELECT
                    gen_random_uuid(),
                    'DEFAULT',
                    'Default Organization',
                    'default-organization',
                    'NG',
                    'NGN',
                    TRUE,
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP,
                    gen_random_uuid()
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM "Organizations"
                    WHERE "Code" = 'DEFAULT'
                );

                DO $$
                DECLARE
                    default_organization_id uuid;
                BEGIN
                    SELECT "Id"
                    INTO STRICT default_organization_id
                    FROM "Organizations"
                    WHERE "Code" = 'DEFAULT';

                    UPDATE "TreasuryTransactions"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "TreasuryAlerts"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "TransferRequests"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "ReversalRequests"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "PaymentRequests"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "LedgerEntries"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "InvestmentRolloverRequests"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "InvestmentRolloverDecisions"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "InvestmentPlacements"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "InvestmentLimits"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "InvestmentEarlyRedemptionRequests"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "InvestmentEarlyRedemptionDecisions"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "InvestmentAccrualSnapshots"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "FxRates"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "CreditFacilityRepayments"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "CreditFacilityInterestAccrualSnapshots"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "CreditFacilityDrawdowns"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "CreditFacilities"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "Counterparties"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "CashFlowForecastItems"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "BankStatementLines"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "BankStatementImports"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "AuditLogs"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "ApprovalPolicies"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "ApprovalDecisions"
                    SET "OrganizationId" =
                        default_organization_id;

                    UPDATE "Accounts"
                    SET "OrganizationId" =
                        default_organization_id;
                END $$;

                ALTER TABLE "TreasuryTransactions"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "TreasuryAlerts"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "TransferRequests"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "ReversalRequests"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "PaymentRequests"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "LedgerEntries"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "InvestmentRolloverRequests"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "InvestmentRolloverDecisions"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "InvestmentPlacements"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "InvestmentLimits"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "InvestmentEarlyRedemptionRequests"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "InvestmentEarlyRedemptionDecisions"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "InvestmentAccrualSnapshots"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "FxRates"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "CreditFacilityRepayments"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "CreditFacilityInterestAccrualSnapshots"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "CreditFacilityDrawdowns"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "CreditFacilities"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "Counterparties"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "CashFlowForecastItems"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "BankStatementLines"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "BankStatementImports"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "AuditLogs"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "ApprovalPolicies"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "ApprovalDecisions"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;

                ALTER TABLE "Accounts"
                    ALTER COLUMN "OrganizationId"
                    DROP DEFAULT;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_OrganizationId_IdempotencyKey",
                table: "TreasuryTransactions",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_OrganizationId_Reference",
                table: "TreasuryTransactions",
                columns: new[] { "OrganizationId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryAlerts_OrganizationId",
                table: "TreasuryAlerts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferRequests_OrganizationId",
                table: "TransferRequests",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReversalRequests_OrganizationId",
                table: "ReversalRequests",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_OrganizationId_IdempotencyKey",
                table: "PaymentRequests",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_OrganizationId",
                table: "LedgerEntries",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentRolloverRequests_OrganizationId_ExecutionIdempote~",
                table: "InvestmentRolloverRequests",
                columns: new[] { "OrganizationId", "ExecutionIdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentRolloverRequests_OrganizationId_RequestIdempotenc~",
                table: "InvestmentRolloverRequests",
                columns: new[] { "OrganizationId", "RequestIdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentRolloverDecisions_OrganizationId",
                table: "InvestmentRolloverDecisions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_OrganizationId_ActivationIdempotencyKey",
                table: "InvestmentPlacements",
                columns: new[] { "OrganizationId", "ActivationIdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_OrganizationId_RedemptionIdempotencyKey",
                table: "InvestmentPlacements",
                columns: new[] { "OrganizationId", "RedemptionIdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_OrganizationId_Reference",
                table: "InvestmentPlacements",
                columns: new[] { "OrganizationId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentLimits_OrganizationId",
                table: "InvestmentLimits",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentEarlyRedemptionRequests_OrganizationId_ExecutionI~",
                table: "InvestmentEarlyRedemptionRequests",
                columns: new[] { "OrganizationId", "ExecutionIdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentEarlyRedemptionRequests_OrganizationId_RequestIde~",
                table: "InvestmentEarlyRedemptionRequests",
                columns: new[] { "OrganizationId", "RequestIdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentEarlyRedemptionDecisions_OrganizationId",
                table: "InvestmentEarlyRedemptionDecisions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentAccrualSnapshots_OrganizationId",
                table: "InvestmentAccrualSnapshots",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRates_OrganizationId_FromCurrency_ToCurrency_RateDateUtc",
                table: "FxRates",
                columns: new[] { "OrganizationId", "FromCurrency", "ToCurrency", "RateDateUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityRepayments_OrganizationId_IdempotencyKey",
                table: "CreditFacilityRepayments",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityRepayments_OrganizationId_Reference",
                table: "CreditFacilityRepayments",
                columns: new[] { "OrganizationId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityInterestAccrualSnapshots_OrganizationId",
                table: "CreditFacilityInterestAccrualSnapshots",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityDrawdowns_OrganizationId_IdempotencyKey",
                table: "CreditFacilityDrawdowns",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityDrawdowns_OrganizationId_Reference",
                table: "CreditFacilityDrawdowns",
                columns: new[] { "OrganizationId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilities_OrganizationId_ActivationIdempotencyKey",
                table: "CreditFacilities",
                columns: new[] { "OrganizationId", "ActivationIdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilities_OrganizationId_Reference",
                table: "CreditFacilities",
                columns: new[] { "OrganizationId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Counterparties_OrganizationId_Code",
                table: "Counterparties",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowForecastItems_OrganizationId",
                table: "CashFlowForecastItems",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_OrganizationId",
                table: "BankStatementLines",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementImports_OrganizationId",
                table: "BankStatementImports",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OrganizationId",
                table: "AuditLogs",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_OrganizationId_OperationType_Currency",
                table: "ApprovalPolicies",
                columns: new[] { "OrganizationId", "OperationType", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_OrganizationId",
                table: "ApprovalDecisions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_OrganizationId_AccountNumber",
                table: "Accounts",
                columns: new[] { "OrganizationId", "AccountNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Organizations_OrganizationId",
                table: "Accounts",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalDecisions_Organizations_OrganizationId",
                table: "ApprovalDecisions",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalPolicies_Organizations_OrganizationId",
                table: "ApprovalPolicies",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Organizations_OrganizationId",
                table: "AuditLogs",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankStatementImports_Organizations_OrganizationId",
                table: "BankStatementImports",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankStatementLines_Organizations_OrganizationId",
                table: "BankStatementLines",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashFlowForecastItems_Organizations_OrganizationId",
                table: "CashFlowForecastItems",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Counterparties_Organizations_OrganizationId",
                table: "Counterparties",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CreditFacilities_Organizations_OrganizationId",
                table: "CreditFacilities",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CreditFacilityDrawdowns_Organizations_OrganizationId",
                table: "CreditFacilityDrawdowns",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CreditFacilityInterestAccrualSnapshots_Organizations_Organi~",
                table: "CreditFacilityInterestAccrualSnapshots",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CreditFacilityRepayments_Organizations_OrganizationId",
                table: "CreditFacilityRepayments",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FxRates_Organizations_OrganizationId",
                table: "FxRates",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvestmentAccrualSnapshots_Organizations_OrganizationId",
                table: "InvestmentAccrualSnapshots",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvestmentEarlyRedemptionDecisions_Organizations_Organizati~",
                table: "InvestmentEarlyRedemptionDecisions",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvestmentEarlyRedemptionRequests_Organizations_Organizatio~",
                table: "InvestmentEarlyRedemptionRequests",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvestmentLimits_Organizations_OrganizationId",
                table: "InvestmentLimits",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvestmentPlacements_Organizations_OrganizationId",
                table: "InvestmentPlacements",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvestmentRolloverDecisions_Organizations_OrganizationId",
                table: "InvestmentRolloverDecisions",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvestmentRolloverRequests_Organizations_OrganizationId",
                table: "InvestmentRolloverRequests",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerEntries_Organizations_OrganizationId",
                table: "LedgerEntries",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentRequests_Organizations_OrganizationId",
                table: "PaymentRequests",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReversalRequests_Organizations_OrganizationId",
                table: "ReversalRequests",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TransferRequests_Organizations_OrganizationId",
                table: "TransferRequests",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TreasuryAlerts_Organizations_OrganizationId",
                table: "TreasuryAlerts",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TreasuryTransactions_Organizations_OrganizationId",
                table: "TreasuryTransactions",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Organizations_OrganizationId",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalDecisions_Organizations_OrganizationId",
                table: "ApprovalDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalPolicies_Organizations_OrganizationId",
                table: "ApprovalPolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Organizations_OrganizationId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_BankStatementImports_Organizations_OrganizationId",
                table: "BankStatementImports");

            migrationBuilder.DropForeignKey(
                name: "FK_BankStatementLines_Organizations_OrganizationId",
                table: "BankStatementLines");

            migrationBuilder.DropForeignKey(
                name: "FK_CashFlowForecastItems_Organizations_OrganizationId",
                table: "CashFlowForecastItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Counterparties_Organizations_OrganizationId",
                table: "Counterparties");

            migrationBuilder.DropForeignKey(
                name: "FK_CreditFacilities_Organizations_OrganizationId",
                table: "CreditFacilities");

            migrationBuilder.DropForeignKey(
                name: "FK_CreditFacilityDrawdowns_Organizations_OrganizationId",
                table: "CreditFacilityDrawdowns");

            migrationBuilder.DropForeignKey(
                name: "FK_CreditFacilityInterestAccrualSnapshots_Organizations_Organi~",
                table: "CreditFacilityInterestAccrualSnapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_CreditFacilityRepayments_Organizations_OrganizationId",
                table: "CreditFacilityRepayments");

            migrationBuilder.DropForeignKey(
                name: "FK_FxRates_Organizations_OrganizationId",
                table: "FxRates");

            migrationBuilder.DropForeignKey(
                name: "FK_InvestmentAccrualSnapshots_Organizations_OrganizationId",
                table: "InvestmentAccrualSnapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_InvestmentEarlyRedemptionDecisions_Organizations_Organizati~",
                table: "InvestmentEarlyRedemptionDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_InvestmentEarlyRedemptionRequests_Organizations_Organizatio~",
                table: "InvestmentEarlyRedemptionRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_InvestmentLimits_Organizations_OrganizationId",
                table: "InvestmentLimits");

            migrationBuilder.DropForeignKey(
                name: "FK_InvestmentPlacements_Organizations_OrganizationId",
                table: "InvestmentPlacements");

            migrationBuilder.DropForeignKey(
                name: "FK_InvestmentRolloverDecisions_Organizations_OrganizationId",
                table: "InvestmentRolloverDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_InvestmentRolloverRequests_Organizations_OrganizationId",
                table: "InvestmentRolloverRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_LedgerEntries_Organizations_OrganizationId",
                table: "LedgerEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentRequests_Organizations_OrganizationId",
                table: "PaymentRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ReversalRequests_Organizations_OrganizationId",
                table: "ReversalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_TransferRequests_Organizations_OrganizationId",
                table: "TransferRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_TreasuryAlerts_Organizations_OrganizationId",
                table: "TreasuryAlerts");

            migrationBuilder.DropForeignKey(
                name: "FK_TreasuryTransactions_Organizations_OrganizationId",
                table: "TreasuryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_TreasuryTransactions_OrganizationId_IdempotencyKey",
                table: "TreasuryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_TreasuryTransactions_OrganizationId_Reference",
                table: "TreasuryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_TreasuryAlerts_OrganizationId",
                table: "TreasuryAlerts");

            migrationBuilder.DropIndex(
                name: "IX_TransferRequests_OrganizationId",
                table: "TransferRequests");

            migrationBuilder.DropIndex(
                name: "IX_ReversalRequests_OrganizationId",
                table: "ReversalRequests");

            migrationBuilder.DropIndex(
                name: "IX_PaymentRequests_OrganizationId_IdempotencyKey",
                table: "PaymentRequests");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_OrganizationId",
                table: "LedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentRolloverRequests_OrganizationId_ExecutionIdempote~",
                table: "InvestmentRolloverRequests");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentRolloverRequests_OrganizationId_RequestIdempotenc~",
                table: "InvestmentRolloverRequests");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentRolloverDecisions_OrganizationId",
                table: "InvestmentRolloverDecisions");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentPlacements_OrganizationId_ActivationIdempotencyKey",
                table: "InvestmentPlacements");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentPlacements_OrganizationId_RedemptionIdempotencyKey",
                table: "InvestmentPlacements");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentPlacements_OrganizationId_Reference",
                table: "InvestmentPlacements");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentLimits_OrganizationId",
                table: "InvestmentLimits");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentEarlyRedemptionRequests_OrganizationId_ExecutionI~",
                table: "InvestmentEarlyRedemptionRequests");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentEarlyRedemptionRequests_OrganizationId_RequestIde~",
                table: "InvestmentEarlyRedemptionRequests");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentEarlyRedemptionDecisions_OrganizationId",
                table: "InvestmentEarlyRedemptionDecisions");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentAccrualSnapshots_OrganizationId",
                table: "InvestmentAccrualSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_FxRates_OrganizationId_FromCurrency_ToCurrency_RateDateUtc",
                table: "FxRates");

            migrationBuilder.DropIndex(
                name: "IX_CreditFacilityRepayments_OrganizationId_IdempotencyKey",
                table: "CreditFacilityRepayments");

            migrationBuilder.DropIndex(
                name: "IX_CreditFacilityRepayments_OrganizationId_Reference",
                table: "CreditFacilityRepayments");

            migrationBuilder.DropIndex(
                name: "IX_CreditFacilityInterestAccrualSnapshots_OrganizationId",
                table: "CreditFacilityInterestAccrualSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_CreditFacilityDrawdowns_OrganizationId_IdempotencyKey",
                table: "CreditFacilityDrawdowns");

            migrationBuilder.DropIndex(
                name: "IX_CreditFacilityDrawdowns_OrganizationId_Reference",
                table: "CreditFacilityDrawdowns");

            migrationBuilder.DropIndex(
                name: "IX_CreditFacilities_OrganizationId_ActivationIdempotencyKey",
                table: "CreditFacilities");

            migrationBuilder.DropIndex(
                name: "IX_CreditFacilities_OrganizationId_Reference",
                table: "CreditFacilities");

            migrationBuilder.DropIndex(
                name: "IX_Counterparties_OrganizationId_Code",
                table: "Counterparties");

            migrationBuilder.DropIndex(
                name: "IX_CashFlowForecastItems_OrganizationId",
                table: "CashFlowForecastItems");

            migrationBuilder.DropIndex(
                name: "IX_BankStatementLines_OrganizationId",
                table: "BankStatementLines");

            migrationBuilder.DropIndex(
                name: "IX_BankStatementImports_OrganizationId",
                table: "BankStatementImports");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_OrganizationId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalPolicies_OrganizationId_OperationType_Currency",
                table: "ApprovalPolicies");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalDecisions_OrganizationId",
                table: "ApprovalDecisions");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_OrganizationId_AccountNumber",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "TreasuryTransactions");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "TreasuryAlerts");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ReversalRequests");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "InvestmentRolloverRequests");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "InvestmentRolloverDecisions");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "InvestmentLimits");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "InvestmentEarlyRedemptionRequests");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "InvestmentEarlyRedemptionDecisions");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "InvestmentAccrualSnapshots");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "FxRates");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "CreditFacilityRepayments");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "CreditFacilityInterestAccrualSnapshots");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "CreditFacilityDrawdowns");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "CreditFacilities");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Counterparties");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "CashFlowForecastItems");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "BankStatementLines");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "BankStatementImports");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ApprovalPolicies");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ApprovalDecisions");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Accounts");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_IdempotencyKey",
                table: "TreasuryTransactions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_Reference",
                table: "TreasuryTransactions",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_IdempotencyKey",
                table: "PaymentRequests",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentRolloverRequests_ExecutionIdempotencyKey",
                table: "InvestmentRolloverRequests",
                column: "ExecutionIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentRolloverRequests_RequestIdempotencyKey",
                table: "InvestmentRolloverRequests",
                column: "RequestIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_ActivationIdempotencyKey",
                table: "InvestmentPlacements",
                column: "ActivationIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_RedemptionIdempotencyKey",
                table: "InvestmentPlacements",
                column: "RedemptionIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_Reference",
                table: "InvestmentPlacements",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentEarlyRedemptionRequests_ExecutionIdempotencyKey",
                table: "InvestmentEarlyRedemptionRequests",
                column: "ExecutionIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentEarlyRedemptionRequests_RequestIdempotencyKey",
                table: "InvestmentEarlyRedemptionRequests",
                column: "RequestIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FxRates_FromCurrency_ToCurrency_RateDateUtc",
                table: "FxRates",
                columns: new[] { "FromCurrency", "ToCurrency", "RateDateUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityRepayments_IdempotencyKey",
                table: "CreditFacilityRepayments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityRepayments_Reference",
                table: "CreditFacilityRepayments",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityDrawdowns_IdempotencyKey",
                table: "CreditFacilityDrawdowns",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityDrawdowns_Reference",
                table: "CreditFacilityDrawdowns",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilities_ActivationIdempotencyKey",
                table: "CreditFacilities",
                column: "ActivationIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilities_Reference",
                table: "CreditFacilities",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Counterparties_Code",
                table: "Counterparties",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_OperationType_Currency",
                table: "ApprovalPolicies",
                columns: new[] { "OperationType", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountNumber",
                table: "Accounts",
                column: "AccountNumber",
                unique: true);
        }
    }
}
