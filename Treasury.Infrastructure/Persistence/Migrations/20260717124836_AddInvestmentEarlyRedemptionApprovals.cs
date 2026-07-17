using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentEarlyRedemptionApprovals : Migration
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
                name: "InvestmentEarlyRedemptionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestmentPlacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestmentReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InstitutionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DestinationAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ProposedRedemptionDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PrincipalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossAccruedInterestAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PenaltyRatePercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    PenaltyAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestAfterPenaltyAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WithholdingTaxRatePercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    WithholdingTaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetInterestAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EstimatedRedemptionProceeds = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedProceedsShortfall = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    RedemptionTreasuryTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentEarlyRedemptionRequests", x => x.Id);
                    table.CheckConstraint("CK_EarlyRedemptionRequests_Amounts", "\"GrossAccruedInterestAmount\" >= 0 AND \"PenaltyAmount\" >= 0 AND \"InterestAfterPenaltyAmount\" >= 0 AND \"WithholdingTaxAmount\" >= 0 AND \"NetInterestAmount\" >= 0 AND \"EstimatedRedemptionProceeds\" > 0 AND \"ExpectedProceedsShortfall\" >= 0");
                    table.CheckConstraint("CK_EarlyRedemptionRequests_Approvals", "\"RequiredApprovalCount\" BETWEEN 1 AND 5 AND \"ApprovalCount\" >= 0 AND \"ApprovalCount\" <= \"RequiredApprovalCount\"");
                    table.CheckConstraint("CK_EarlyRedemptionRequests_Currency", "char_length(\"Currency\") = 3");
                    table.CheckConstraint("CK_EarlyRedemptionRequests_Expiry", "\"ExpiresAtUtc\" > \"RequestedAtUtc\"");
                    table.CheckConstraint("CK_EarlyRedemptionRequests_Principal", "\"PrincipalAmount\" > 0");
                    table.CheckConstraint("CK_EarlyRedemptionRequests_Rates", "\"PenaltyRatePercentage\" BETWEEN 0 AND 100 AND \"WithholdingTaxRatePercentage\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_EarlyRedemptionRequests_Status", "\"Status\" IN ('Pending','Approved','Rejected','Executed','Expired')");
                    table.ForeignKey(
                        name: "FK_InvestmentEarlyRedemptionRequests_Accounts_DestinationAccou~",
                        column: x => x.DestinationAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestmentEarlyRedemptionRequests_InvestmentPlacements_Inve~",
                        column: x => x.InvestmentPlacementId,
                        principalTable: "InvestmentPlacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestmentEarlyRedemptionRequests_TreasuryTransactions_Rede~",
                        column: x => x.RedemptionTreasuryTransactionId,
                        principalTable: "TreasuryTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestmentEarlyRedemptionRequests_Users_RejectedByUserId",
                        column: x => x.RejectedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestmentEarlyRedemptionRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvestmentEarlyRedemptionDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestmentEarlyRedemptionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentEarlyRedemptionDecisions", x => x.Id);
                    table.CheckConstraint("CK_EarlyRedemptionDecisions_Decision", "\"Decision\" IN ('Approved','Rejected')");
                    table.ForeignKey(
                        name: "FK_InvestmentEarlyRedemptionDecisions_InvestmentEarlyRedemptio~",
                        column: x => x.InvestmentEarlyRedemptionRequestId,
                        principalTable: "InvestmentEarlyRedemptionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestmentEarlyRedemptionDecisions_Users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentEarlyRedemptionRequest','System')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovalPolicies_OperationType",
                table: "ApprovalPolicies",
                sql: "\"OperationType\" IN ('InternalTransfer', 'CashPayment', 'TransactionReversal', 'InvestmentPlacement', 'InvestmentEarlyRedemption')");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentEarlyRedemptionDecisions_ApproverUserId",
                table: "InvestmentEarlyRedemptionDecisions",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentEarlyRedemptionDecisions_InvestmentEarlyRedemptio~",
                table: "InvestmentEarlyRedemptionDecisions",
                columns: new[] { "InvestmentEarlyRedemptionRequestId", "ApproverUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentEarlyRedemptionRequests_DestinationAccountId",
                table: "InvestmentEarlyRedemptionRequests",
                column: "DestinationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentEarlyRedemptionRequests_ExecutionIdempotencyKey",
                table: "InvestmentEarlyRedemptionRequests",
                column: "ExecutionIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentEarlyRedemptionRequests_InvestmentPlacementId",
                table: "InvestmentEarlyRedemptionRequests",
                column: "InvestmentPlacementId",
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Approved')");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentEarlyRedemptionRequests_RedemptionTreasuryTransac~",
                table: "InvestmentEarlyRedemptionRequests",
                column: "RedemptionTreasuryTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentEarlyRedemptionRequests_RejectedByUserId",
                table: "InvestmentEarlyRedemptionRequests",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentEarlyRedemptionRequests_RequestedByUserId",
                table: "InvestmentEarlyRedemptionRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentEarlyRedemptionRequests_RequestIdempotencyKey",
                table: "InvestmentEarlyRedemptionRequests",
                column: "RequestIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentEarlyRedemptionRequests_Status_ExpiresAtUtc",
                table: "InvestmentEarlyRedemptionRequests",
                columns: new[] { "Status", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvestmentEarlyRedemptionDecisions");

            migrationBuilder.DropTable(
                name: "InvestmentEarlyRedemptionRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovalPolicies_OperationType",
                table: "ApprovalPolicies");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','System')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovalPolicies_OperationType",
                table: "ApprovalPolicies",
                sql: "\"OperationType\" IN ('InternalTransfer', 'CashPayment', 'TransactionReversal', 'InvestmentPlacement')");
        }
    }
}
