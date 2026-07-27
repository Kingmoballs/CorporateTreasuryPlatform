using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricalTransactionImportStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Accounts_OrganizationId_Id",
                table: "Accounts",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.CreateTable(
                name: "HistoricalTransactionImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TotalRowCount = table.Column<int>(type: "integer", nullable: false),
                    ValidRowCount = table.Column<int>(type: "integer", nullable: false),
                    InvalidRowCount = table.Column<int>(type: "integer", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalTransactionImportBatches", x => x.Id);
                    table.UniqueConstraint("AK_HistoricalTransactionImportBatches_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_HistoricalImportBatches_Counts", "\"TotalRowCount\" > 0 AND \"ValidRowCount\" >= 0 AND \"InvalidRowCount\" >= 0 AND \"ValidRowCount\" + \"InvalidRowCount\" = \"TotalRowCount\"");
                    table.CheckConstraint("CK_HistoricalImportBatches_Hash", "char_length(\"FileHash\") = 64");
                    table.CheckConstraint("CK_HistoricalImportBatches_Mode", "\"Mode\" IN ('HistoricalTransactions','CutoverOpeningBalances')");
                    table.CheckConstraint("CK_HistoricalImportBatches_Status", "\"Status\" IN ('Validated','ValidationFailed')");
                    table.ForeignKey(
                        name: "FK_HistoricalTransactionImportBatches_Organizations_Organizati~",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalTransactionImportBatches_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalTransactionImportRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    LegalEntityCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessUnitCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BusinessUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValueDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TransactionType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CounterpartyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RawDataJson = table.Column<string>(type: "jsonb", nullable: false),
                    ValidationErrorsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalTransactionImportRows", x => x.Id);
                    table.CheckConstraint("CK_HistoricalImportRows_Currency", "\"Currency\" IS NULL OR char_length(\"Currency\") = 3");
                    table.CheckConstraint("CK_HistoricalImportRows_Direction", "\"Direction\" IS NULL OR \"Direction\" IN ('Credit','Debit')");
                    table.CheckConstraint("CK_HistoricalImportRows_Hash", "char_length(\"Fingerprint\") = 64");
                    table.CheckConstraint("CK_HistoricalImportRows_RowNumber", "\"RowNumber\" > 1");
                    table.ForeignKey(
                        name: "FK_HistoricalTransactionImportRows_Accounts_OrganizationId_Acc~",
                        columns: x => new { x.OrganizationId, x.AccountId },
                        principalTable: "Accounts",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalTransactionImportRows_BusinessUnits_OrganizationI~",
                        columns: x => new { x.OrganizationId, x.LegalEntityId, x.BusinessUnitId },
                        principalTable: "BusinessUnits",
                        principalColumns: new[] { "OrganizationId", "LegalEntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalTransactionImportRows_HistoricalTransactionImport~",
                        columns: x => new { x.OrganizationId, x.BatchId },
                        principalTable: "HistoricalTransactionImportBatches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistoricalTransactionImportRows_LegalEntities_OrganizationI~",
                        columns: x => new { x.OrganizationId, x.LegalEntityId },
                        principalTable: "LegalEntities",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalTransactionImportRows_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Organization','OrganizationApplication','LegalEntity','BusinessUnit','OrganizationMembership','UserInvitation','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','HistoricalTransactionImportBatch','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentRolloverRequest','Counterparty','InvestmentLimit','CreditFacility','CreditFacilityDrawdown','CreditFacilityRepayment','CreditFacilityInterestAccrualSnapshot','System')");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionImportBatches_OrganizationId_ImportKey",
                table: "HistoricalTransactionImportBatches",
                columns: new[] { "OrganizationId", "ImportKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionImportBatches_OrganizationId_Mode_File~",
                table: "HistoricalTransactionImportBatches",
                columns: new[] { "OrganizationId", "Mode", "FileHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionImportBatches_OrganizationId_Status_Up~",
                table: "HistoricalTransactionImportBatches",
                columns: new[] { "OrganizationId", "Status", "UploadedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionImportBatches_UploadedByUserId",
                table: "HistoricalTransactionImportBatches",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionImportRows_OrganizationId_AccountId",
                table: "HistoricalTransactionImportRows",
                columns: new[] { "OrganizationId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionImportRows_OrganizationId_BatchId_RowN~",
                table: "HistoricalTransactionImportRows",
                columns: new[] { "OrganizationId", "BatchId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionImportRows_OrganizationId_Fingerprint",
                table: "HistoricalTransactionImportRows",
                columns: new[] { "OrganizationId", "Fingerprint" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTransactionImportRows_OrganizationId_LegalEntityI~",
                table: "HistoricalTransactionImportRows",
                columns: new[] { "OrganizationId", "LegalEntityId", "BusinessUnitId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricalTransactionImportRows");

            migrationBuilder.DropTable(
                name: "HistoricalTransactionImportBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Accounts_OrganizationId_Id",
                table: "Accounts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Organization','OrganizationApplication','LegalEntity','BusinessUnit','OrganizationMembership','UserInvitation','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentRolloverRequest','Counterparty','InvestmentLimit','CreditFacility','CreditFacilityDrawdown','CreditFacilityRepayment','CreditFacilityInterestAccrualSnapshot','System')");
        }
    }
}
