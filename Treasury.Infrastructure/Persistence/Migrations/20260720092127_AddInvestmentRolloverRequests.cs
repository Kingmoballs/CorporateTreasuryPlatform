using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentRolloverRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovalPolicies_OperationType",
                table: "ApprovalPolicies");

            migrationBuilder.CreateTable(
                name: "InvestmentRolloverRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalInvestmentPlacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalInvestmentReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OriginalInstitutionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OriginalMaturityDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OriginalPrincipalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossInterestAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossMaturityAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WithholdingTaxRatePercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    WithholdingTaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetInterestAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetMaturityProceeds = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RolloverOption = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RolloverPrincipalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CashPayoutAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CashPayoutAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    NewInvestmentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NewInstitutionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NewAnnualInterestRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    NewDayCountBasis = table.Column<int>(type: "integer", nullable: false),
                    NewStartDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NewMaturityDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NewTenorDays = table.Column<int>(type: "integer", nullable: false),
                    NewExpectedInterestAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NewExpectedMaturityAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RequestIdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExecutionIdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RequiredApprovalCount = table.Column<int>(type: "integer", nullable: false),
                    ApprovalCount = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RejectedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NewInvestmentPlacementId = table.Column<Guid>(type: "uuid", nullable: true),
                    CashPayoutTreasuryTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExecutedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentRolloverRequests", x => x.Id);
                    table.CheckConstraint("CK_InvestmentRolloverRequests_Amounts", "\"OriginalPrincipalAmount\" > 0 AND \"GrossInterestAmount\" >= 0 AND \"WithholdingTaxAmount\" >= 0 AND \"WithholdingTaxAmount\" <= \"GrossInterestAmount\" AND \"NetInterestAmount\" >= 0 AND \"NetMaturityProceeds\" > 0 AND \"RolloverPrincipalAmount\" > 0 AND \"CashPayoutAmount\" >= 0 AND \"NewExpectedInterestAmount\" >= 0 AND \"NewExpectedMaturityAmount\" >= \"RolloverPrincipalAmount\"");
                    table.CheckConstraint("CK_InvestmentRolloverRequests_Approvals", "\"RequiredApprovalCount\" BETWEEN 1 AND 5 AND \"ApprovalCount\" >= 0 AND \"ApprovalCount\" <= \"RequiredApprovalCount\"");
                    table.CheckConstraint("CK_InvestmentRolloverRequests_Arithmetic", "\"GrossMaturityAmount\" = \"OriginalPrincipalAmount\" + \"GrossInterestAmount\" AND \"NetInterestAmount\" = \"GrossInterestAmount\" - \"WithholdingTaxAmount\" AND \"NetMaturityProceeds\" = \"OriginalPrincipalAmount\" + \"NetInterestAmount\" AND \"NewExpectedMaturityAmount\" = \"RolloverPrincipalAmount\" + \"NewExpectedInterestAmount\"");
                    table.CheckConstraint("CK_InvestmentRolloverRequests_Currency", "char_length(\"Currency\") = 3");
                    table.CheckConstraint("CK_InvestmentRolloverRequests_Dates", "\"NewStartDateUtc\" >= \"OriginalMaturityDateUtc\" AND \"NewMaturityDateUtc\" > \"NewStartDateUtc\" AND \"NewTenorDays\" > 0");
                    table.CheckConstraint("CK_InvestmentRolloverRequests_DayCount", "\"NewDayCountBasis\" IN (360, 365)");
                    table.CheckConstraint("CK_InvestmentRolloverRequests_Expiry", "\"ExpiresAtUtc\" > \"RequestedAtUtc\"");
                    table.CheckConstraint("CK_InvestmentRolloverRequests_Option", "\"RolloverOption\" IN ('PrincipalOnly','PrincipalAndNetInterest')");
                    table.CheckConstraint("CK_InvestmentRolloverRequests_OptionAmounts", "(\"RolloverOption\" = 'PrincipalOnly' AND \"RolloverPrincipalAmount\" = \"OriginalPrincipalAmount\" AND \"CashPayoutAmount\" = \"NetInterestAmount\") OR (\"RolloverOption\" = 'PrincipalAndNetInterest' AND \"RolloverPrincipalAmount\" = \"NetMaturityProceeds\" AND \"CashPayoutAmount\" = 0)");
                    table.CheckConstraint("CK_InvestmentRolloverRequests_PayoutAccount", "(\"CashPayoutAmount\" = 0 AND \"CashPayoutAccountId\" IS NULL) OR (\"CashPayoutAmount\" > 0 AND \"CashPayoutAccountId\" IS NOT NULL)");
                    table.CheckConstraint("CK_InvestmentRolloverRequests_Rates", "\"WithholdingTaxRatePercentage\" BETWEEN 0 AND 100 AND \"NewAnnualInterestRate\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_InvestmentRolloverRequests_Status", "\"Status\" IN ('Pending','Approved','Rejected','Executed','Expired')");
                    table.ForeignKey(
                        name: "FK_InvestmentRolloverRequests_Accounts_CashPayoutAccountId",
                        column: x => x.CashPayoutAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestmentRolloverRequests_InvestmentPlacements_NewInvestme~",
                        column: x => x.NewInvestmentPlacementId,
                        principalTable: "InvestmentPlacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestmentRolloverRequests_InvestmentPlacements_OriginalInv~",
                        column: x => x.OriginalInvestmentPlacementId,
                        principalTable: "InvestmentPlacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestmentRolloverRequests_TreasuryTransactions_CashPayoutT~",
                        column: x => x.CashPayoutTreasuryTransactionId,
                        principalTable: "TreasuryTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestmentRolloverRequests_Users_ExecutedByUserId",
                        column: x => x.ExecutedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestmentRolloverRequests_Users_RejectedByUserId",
                        column: x => x.RejectedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestmentRolloverRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvestmentRolloverDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestmentRolloverRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentRolloverDecisions", x => x.Id);
                    table.CheckConstraint("CK_InvestmentRolloverDecisions_Decision", "\"Decision\" IN ('Approved','Rejected')");
                    table.ForeignKey(
                        name: "FK_InvestmentRolloverDecisions_InvestmentRolloverRequests_Inve~",
                        column: x => x.InvestmentRolloverRequestId,
                        principalTable: "InvestmentRolloverRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestmentRolloverDecisions_Users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentEarlyRedemptionRequest','InvestmentRolloverRequest','System')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovalPolicies_OperationType",
                table: "ApprovalPolicies",
                sql: "\"OperationType\" IN ('InternalTransfer', 'CashPayment', 'TransactionReversal', 'InvestmentPlacement', 'InvestmentEarlyRedemption', 'InvestmentRollover')");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentRolloverDecisions_ApproverUserId",
                table: "InvestmentRolloverDecisions",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentRolloverDecisions_InvestmentRolloverRequestId_App~",
                table: "InvestmentRolloverDecisions",
                columns: new[] { "InvestmentRolloverRequestId", "ApproverUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentRolloverRequests_CashPayoutAccountId",
                table: "InvestmentRolloverRequests",
                column: "CashPayoutAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentRolloverRequests_CashPayoutTreasuryTransactionId",
                table: "InvestmentRolloverRequests",
                column: "CashPayoutTreasuryTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentRolloverRequests_ExecutedByUserId",
                table: "InvestmentRolloverRequests",
                column: "ExecutedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentRolloverRequests_ExecutionIdempotencyKey",
                table: "InvestmentRolloverRequests",
                column: "ExecutionIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentRolloverRequests_NewInvestmentPlacementId",
                table: "InvestmentRolloverRequests",
                column: "NewInvestmentPlacementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentRolloverRequests_OriginalInvestmentPlacementId",
                table: "InvestmentRolloverRequests",
                column: "OriginalInvestmentPlacementId",
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Approved')");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentRolloverRequests_RejectedByUserId",
                table: "InvestmentRolloverRequests",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentRolloverRequests_RequestedByUserId",
                table: "InvestmentRolloverRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentRolloverRequests_RequestIdempotencyKey",
                table: "InvestmentRolloverRequests",
                column: "RequestIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentRolloverRequests_Status_ExpiresAtUtc",
                table: "InvestmentRolloverRequests",
                columns: new[] { "Status", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvestmentRolloverDecisions");

            migrationBuilder.DropTable(
                name: "InvestmentRolloverRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovalPolicies_OperationType",
                table: "ApprovalPolicies");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentEarlyRedemptionRequest','System')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovalPolicies_OperationType",
                table: "ApprovalPolicies",
                sql: "\"OperationType\" IN ('InternalTransfer', 'CashPayment', 'TransactionReversal', 'InvestmentPlacement', 'InvestmentEarlyRedemption')");
        }
    }
}
