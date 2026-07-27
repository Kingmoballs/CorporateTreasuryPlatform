using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricalImportApprovalAndCommit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_HistoricalImportBatches_Status",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.AddColumn<Guid>(
                name: "PostedTreasuryTransactionId",
                table: "HistoricalTransactionImportRows",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalCount",
                table: "HistoricalTransactionImportBatches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                table: "HistoricalTransactionImportBatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CommittedAtUtc",
                table: "HistoricalTransactionImportBatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommittedByUserId",
                table: "HistoricalTransactionImportBatches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAtUtc",
                table: "HistoricalTransactionImportBatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedByUserId",
                table: "HistoricalTransactionImportBatches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "HistoricalTransactionImportBatches",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredApprovalCount",
                table: "HistoricalTransactionImportBatches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAtUtc",
                table: "HistoricalTransactionImportBatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedByUserId",
                table: "HistoricalTransactionImportBatches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_HistoricalTransactionImportRows_OrganizationId_Id",
                table: "HistoricalTransactionImportRows",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.CreateTable(
                name: "HistoricalTransactionImportDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApproverRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalTransactionImportDecisions", x => x.Id);
                    table.CheckConstraint("CK_HistoricalImportDecisions_Decision", "\"Decision\" IN ('Approved','Rejected')");
                    table.CheckConstraint("CK_HistoricalImportDecisions_Role", "\"ApproverRole\" IN ('Admin','FinanceManager','CFO')");
                    table.ForeignKey(
                        name: "FK_HistoricalTransactionImportDecisions_HistoricalTransactionI~",
                        columns: x => new { x.OrganizationId, x.BatchId },
                        principalTable: "HistoricalTransactionImportBatches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistoricalTransactionImportDecisions_Organizations_Organiza~",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalTransactionImportDecisions_Users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalTransactionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceRowId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValueDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CounterpartyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CommittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CommittedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalTransactionRecords", x => x.Id);
                    table.CheckConstraint("CK_HistoricalTransactionRecords_Amount", "\"Amount\" > 0");
                    table.CheckConstraint("CK_HistoricalTransactionRecords_Currency", "char_length(\"Currency\") = 3");
                    table.CheckConstraint("CK_HistoricalTransactionRecords_Direction", "\"Direction\" IN ('Credit','Debit')");
                    table.ForeignKey(
                        name: "FK_HistoricalTransactionRecords_Accounts_OrganizationId_Accoun~",
                        columns: x => new { x.OrganizationId, x.AccountId },
                        principalTable: "Accounts",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalTransactionRecords_BusinessUnits_OrganizationId_L~",
                        columns: x => new { x.OrganizationId, x.LegalEntityId, x.BusinessUnitId },
                        principalTable: "BusinessUnits",
                        principalColumns: new[] { "OrganizationId", "LegalEntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalTransactionRecords_HistoricalTransactionImportBat~",
                        columns: x => new { x.OrganizationId, x.BatchId },
                        principalTable: "HistoricalTransactionImportBatches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalTransactionRecords_HistoricalTransactionImportRow~",
                        columns: x => new { x.OrganizationId, x.SourceRowId },
                        principalTable: "HistoricalTransactionImportRows",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalTransactionRecords_LegalEntities_OrganizationId_L~",
                        columns: x => new { x.OrganizationId, x.LegalEntityId },
                        principalTable: "LegalEntities",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalTransactionRecords_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalTransactionRecords_Users_CommittedByUserId",
                        column: x => x.CommittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionImportRows_PostedTreasuryTransactionId",
                table: "HistoricalTransactionImportRows",
                column: "PostedTreasuryTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionImportBatches_CommittedByUserId",
                table: "HistoricalTransactionImportBatches",
                column: "CommittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionImportBatches_RejectedByUserId",
                table: "HistoricalTransactionImportBatches",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionImportBatches_SubmittedByUserId",
                table: "HistoricalTransactionImportBatches",
                column: "SubmittedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HistoricalImportBatches_ApprovalCounts",
                table: "HistoricalTransactionImportBatches",
                sql: "\"RequiredApprovalCount\" >= 0 AND \"ApprovalCount\" >= 0 AND \"ApprovalCount\" <= \"RequiredApprovalCount\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HistoricalImportBatches_FinalState",
                table: "HistoricalTransactionImportBatches",
                sql: "(\"Status\" NOT IN ('Approved','Committed') OR \"ApprovedAtUtc\" IS NOT NULL) AND (\"Status\" <> 'Rejected' OR (\"RejectedByUserId\" IS NOT NULL AND \"RejectedAtUtc\" IS NOT NULL AND \"RejectionReason\" IS NOT NULL)) AND (\"Status\" <> 'Committed' OR (\"CommittedByUserId\" IS NOT NULL AND \"CommittedAtUtc\" IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HistoricalImportBatches_ReviewState",
                table: "HistoricalTransactionImportBatches",
                sql: "(\"Status\" IN ('Validated','ValidationFailed') AND \"SubmittedByUserId\" IS NULL AND \"SubmittedAtUtc\" IS NULL) OR (\"Status\" IN ('PendingApproval','Approved','Rejected','Committed') AND \"SubmittedByUserId\" IS NOT NULL AND \"SubmittedAtUtc\" IS NOT NULL AND \"RequiredApprovalCount\" > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HistoricalImportBatches_Status",
                table: "HistoricalTransactionImportBatches",
                sql: "\"Status\" IN ('Validated','ValidationFailed','PendingApproval','Approved','Rejected','Committed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Organization','OrganizationApplication','LegalEntity','BusinessUnit','OrganizationMembership','UserInvitation','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','HistoricalTransactionImportBatch','HistoricalTransactionRecord','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentRolloverRequest','Counterparty','InvestmentLimit','CreditFacility','CreditFacilityDrawdown','CreditFacilityRepayment','CreditFacilityInterestAccrualSnapshot','System')");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionImportDecisions_ApproverUserId",
                table: "HistoricalTransactionImportDecisions",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionImportDecisions_OrganizationId_BatchId~",
                table: "HistoricalTransactionImportDecisions",
                columns: new[] { "OrganizationId", "BatchId", "ApproverUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionRecords_CommittedByUserId",
                table: "HistoricalTransactionRecords",
                column: "CommittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionRecords_OrganizationId_AccountId_Trans~",
                table: "HistoricalTransactionRecords",
                columns: new[] { "OrganizationId", "AccountId", "TransactionDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionRecords_OrganizationId_BatchId",
                table: "HistoricalTransactionRecords",
                columns: new[] { "OrganizationId", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionRecords_OrganizationId_ExternalReferen~",
                table: "HistoricalTransactionRecords",
                columns: new[] { "OrganizationId", "ExternalReference" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionRecords_OrganizationId_LegalEntityId_B~",
                table: "HistoricalTransactionRecords",
                columns: new[] { "OrganizationId", "LegalEntityId", "BusinessUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionRecords_OrganizationId_SourceRowId",
                table: "HistoricalTransactionRecords",
                columns: new[] { "OrganizationId", "SourceRowId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_HistoricalTransactionImportBatches_Users_CommittedByUserId",
                table: "HistoricalTransactionImportBatches",
                column: "CommittedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HistoricalTransactionImportBatches_Users_RejectedByUserId",
                table: "HistoricalTransactionImportBatches",
                column: "RejectedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HistoricalTransactionImportBatches_Users_SubmittedByUserId",
                table: "HistoricalTransactionImportBatches",
                column: "SubmittedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HistoricalTransactionImportRows_TreasuryTransactions_Posted~",
                table: "HistoricalTransactionImportRows",
                column: "PostedTreasuryTransactionId",
                principalTable: "TreasuryTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HistoricalTransactionImportBatches_Users_CommittedByUserId",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_HistoricalTransactionImportBatches_Users_RejectedByUserId",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_HistoricalTransactionImportBatches_Users_SubmittedByUserId",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_HistoricalTransactionImportRows_TreasuryTransactions_Posted~",
                table: "HistoricalTransactionImportRows");

            migrationBuilder.DropTable(
                name: "HistoricalTransactionImportDecisions");

            migrationBuilder.DropTable(
                name: "HistoricalTransactionRecords");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_HistoricalTransactionImportRows_OrganizationId_Id",
                table: "HistoricalTransactionImportRows");

            migrationBuilder.DropIndex(
                name: "IX_HistoricalTransactionImportRows_PostedTreasuryTransactionId",
                table: "HistoricalTransactionImportRows");

            migrationBuilder.DropIndex(
                name: "IX_HistoricalTransactionImportBatches_CommittedByUserId",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_HistoricalTransactionImportBatches_RejectedByUserId",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_HistoricalTransactionImportBatches_SubmittedByUserId",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HistoricalImportBatches_ApprovalCounts",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HistoricalImportBatches_FinalState",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HistoricalImportBatches_ReviewState",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HistoricalImportBatches_Status",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "PostedTreasuryTransactionId",
                table: "HistoricalTransactionImportRows");

            migrationBuilder.DropColumn(
                name: "ApprovalCount",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropColumn(
                name: "CommittedAtUtc",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropColumn(
                name: "CommittedByUserId",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropColumn(
                name: "RejectedAtUtc",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropColumn(
                name: "RequiredApprovalCount",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropColumn(
                name: "SubmittedAtUtc",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                table: "HistoricalTransactionImportBatches");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HistoricalImportBatches_Status",
                table: "HistoricalTransactionImportBatches",
                sql: "\"Status\" IN ('Validated','ValidationFailed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Organization','OrganizationApplication','LegalEntity','BusinessUnit','OrganizationMembership','UserInvitation','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','HistoricalTransactionImportBatch','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentRolloverRequest','Counterparty','InvestmentLimit','CreditFacility','CreditFacilityDrawdown','CreditFacilityRepayment','CreditFacilityInterestAccrualSnapshot','System')");
        }
    }
}
