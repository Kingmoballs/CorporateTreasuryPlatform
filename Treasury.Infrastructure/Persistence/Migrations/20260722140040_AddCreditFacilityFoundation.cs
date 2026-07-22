using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditFacilityFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovalDecisions_OneRequest",
                table: "ApprovalDecisions");

            migrationBuilder.AddColumn<Guid>(
                name: "CreditFacilityId",
                table: "ApprovalDecisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CreditFacilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FacilityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FacilityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LenderCounterpartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    LenderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SettlementAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ApprovedLimitAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OutstandingPrincipalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    AccruedInterestAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    AnnualInterestRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    CommitmentFeeRatePercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false, defaultValue: 0m),
                    ArrangementFeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    DayCountBasis = table.Column<int>(type: "integer", nullable: false, defaultValue: 365),
                    InterestPaymentFrequency = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Monthly"),
                    StartDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MaturityDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Draft"),
                    ExternalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequiredApprovalCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ApprovalCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ActivationRequestedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivationRequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActivationExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActivationIdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ActivationRejectedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivationRejectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActivationRejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ActivatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspendedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SuspendedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspensionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MaturedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditFacilities", x => x.Id);
                    table.CheckConstraint("CK_CreditFacilities_AccruedInterest_NonNegative", "\"AccruedInterestAmount\" >= 0");
                    table.CheckConstraint("CK_CreditFacilities_ApprovalCounts", "\"RequiredApprovalCount\" BETWEEN 0 AND 5 AND \"ApprovalCount\" BETWEEN 0 AND \"RequiredApprovalCount\"");
                    table.CheckConstraint("CK_CreditFacilities_ApprovedLimit_Positive", "\"ApprovedLimitAmount\" > 0");
                    table.CheckConstraint("CK_CreditFacilities_ArrangementFee_NonNegative", "\"ArrangementFeeAmount\" >= 0");
                    table.CheckConstraint("CK_CreditFacilities_CommitmentFeeRate_Range", "\"CommitmentFeeRatePercentage\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_CreditFacilities_Currency", "char_length(\"Currency\") = 3 AND \"Currency\" = upper(\"Currency\")");
                    table.CheckConstraint("CK_CreditFacilities_Dates", "\"MaturityDateUtc\" > \"StartDateUtc\"");
                    table.CheckConstraint("CK_CreditFacilities_DayCountBasis", "\"DayCountBasis\" IN (360, 365)");
                    table.CheckConstraint("CK_CreditFacilities_FacilityType", "\"FacilityType\" IN ('Overdraft','RevolvingCredit','TermLoan')");
                    table.CheckConstraint("CK_CreditFacilities_InterestPaymentFrequency", "\"InterestPaymentFrequency\" IN ('Monthly','Quarterly','SemiAnnual','Annual','AtMaturity')");
                    table.CheckConstraint("CK_CreditFacilities_InterestRate_Range", "\"AnnualInterestRate\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_CreditFacilities_OutstandingPrincipal_Range", "\"OutstandingPrincipalAmount\" >= 0 AND \"OutstandingPrincipalAmount\" <= \"ApprovedLimitAmount\"");
                    table.CheckConstraint("CK_CreditFacilities_Status", "\"Status\" IN ('Draft','PendingActivation','Active','Suspended','Matured','Closed','ActivationRejected','ActivationExpired','Cancelled')");
                    table.ForeignKey(
                        name: "FK_CreditFacilities_Accounts_SettlementAccountId",
                        column: x => x.SettlementAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditFacilities_Counterparties_LenderCounterpartyId",
                        column: x => x.LenderCounterpartyId,
                        principalTable: "Counterparties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditFacilities_Users_ActivatedByUserId",
                        column: x => x.ActivatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditFacilities_Users_ActivationRejectedByUserId",
                        column: x => x.ActivationRejectedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditFacilities_Users_ActivationRequestedByUserId",
                        column: x => x.ActivationRequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditFacilities_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditFacilities_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditFacilities_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditFacilities_Users_SuspendedByUserId",
                        column: x => x.SuspendedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditFacilities_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs",
                sql: "\"Action\" IN ('Created','Updated','Deleted','Approved','Rejected','Resolved','Dismissed','Cancelled','Activated','Matured','Redeemed','Realized','Matched','Reconciled','Ignored','Expired','Imported','LoggedIn','RoleChanged','Suspended','Closed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentRolloverRequest','Counterparty','InvestmentLimit','CreditFacility','System')");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_CreditFacilityId_ApproverUserId",
                table: "ApprovalDecisions",
                columns: new[] { "CreditFacilityId", "ApproverUserId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovalDecisions_OneRequest",
                table: "ApprovalDecisions",
                sql: "num_nonnulls(\"TransferRequestId\", \"PaymentRequestId\", \"ReversalRequestId\", \"InvestmentPlacementId\", \"CreditFacilityId\") = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilities_ActivatedByUserId",
                table: "CreditFacilities",
                column: "ActivatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilities_ActivationIdempotencyKey",
                table: "CreditFacilities",
                column: "ActivationIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilities_ActivationRejectedByUserId",
                table: "CreditFacilities",
                column: "ActivationRejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilities_ActivationRequestedByUserId",
                table: "CreditFacilities",
                column: "ActivationRequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilities_CancelledByUserId",
                table: "CreditFacilities",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilities_ClosedByUserId",
                table: "CreditFacilities",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilities_CreatedByUserId",
                table: "CreditFacilities",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilities_LenderCounterpartyId_Status",
                table: "CreditFacilities",
                columns: new[] { "LenderCounterpartyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilities_Reference",
                table: "CreditFacilities",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilities_SettlementAccountId",
                table: "CreditFacilities",
                column: "SettlementAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilities_Status_MaturityDateUtc",
                table: "CreditFacilities",
                columns: new[] { "Status", "MaturityDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilities_SuspendedByUserId",
                table: "CreditFacilities",
                column: "SuspendedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilities_UpdatedByUserId",
                table: "CreditFacilities",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalDecisions_CreditFacilities_CreditFacilityId",
                table: "ApprovalDecisions",
                column: "CreditFacilityId",
                principalTable: "CreditFacilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalDecisions_CreditFacilities_CreditFacilityId",
                table: "ApprovalDecisions");

            migrationBuilder.DropTable(
                name: "CreditFacilities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalDecisions_CreditFacilityId_ApproverUserId",
                table: "ApprovalDecisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovalDecisions_OneRequest",
                table: "ApprovalDecisions");

            migrationBuilder.DropColumn(
                name: "CreditFacilityId",
                table: "ApprovalDecisions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs",
                sql: "\"Action\" IN ('Created','Updated','Deleted','Approved','Rejected','Resolved','Dismissed','Cancelled','Activated','Matured','Redeemed','Realized','Matched','Reconciled','Ignored','Expired','Imported','LoggedIn','RoleChanged')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentEarlyRedemptionRequest','InvestmentRolloverRequest','Counterparty','InvestmentLimit','System')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovalDecisions_OneRequest",
                table: "ApprovalDecisions",
                sql: "num_nonnulls(\"TransferRequestId\", \"PaymentRequestId\", \"ReversalRequestId\", \"InvestmentPlacementId\") = 1");
        }
    }
}
