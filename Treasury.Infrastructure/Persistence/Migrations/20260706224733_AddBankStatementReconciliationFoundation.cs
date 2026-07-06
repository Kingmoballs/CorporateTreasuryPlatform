using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankStatementReconciliationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankStatementImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StatementReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    StatementFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StatementToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OpeningBalance = table.Column<decimal>(type: "numeric", nullable: true),
                    ClosingBalance = table.Column<decimal>(type: "numeric", nullable: true),
                    LineCount = table.Column<int>(type: "integer", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UploadedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementImports", x => x.Id);
                    table.CheckConstraint("CK_BankStatementImports_Currency_Length", "char_length(\"Currency\") = 3");
                    table.CheckConstraint("CK_BankStatementImports_LineCount", "\"LineCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_BankStatementImports_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementImports_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankStatementLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankStatementImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    TransactionDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValueDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BankReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CounterpartyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    BalanceAfterTransaction = table.Column<decimal>(type: "numeric", nullable: true),
                    ReconciliationStatus = table.Column<string>(type: "text", nullable: false),
                    MatchedTreasuryTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReconciledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReconciledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementLines", x => x.Id);
                    table.CheckConstraint("CK_BankStatementLines_Amount_NotZero", "\"Amount\" <> 0");
                    table.CheckConstraint("CK_BankStatementLines_Currency_Length", "char_length(\"Currency\") = 3");
                    table.CheckConstraint("CK_BankStatementLines_LineNumber", "\"LineNumber\" > 0");
                    table.CheckConstraint("CK_BankStatementLines_ReconciliationStatus", "\"ReconciliationStatus\" IN ('Unmatched', 'Matched', 'Reconciled', 'Ignored')");
                    table.ForeignKey(
                        name: "FK_BankStatementLines_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementLines_BankStatementImports_BankStatementImport~",
                        column: x => x.BankStatementImportId,
                        principalTable: "BankStatementImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankStatementLines_TreasuryTransactions_MatchedTreasuryTran~",
                        column: x => x.MatchedTreasuryTransactionId,
                        principalTable: "TreasuryTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementLines_Users_ReconciledByUserId",
                        column: x => x.ReconciledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementImports_AccountId_StatementReference",
                table: "BankStatementImports",
                columns: new[] { "AccountId", "StatementReference" });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementImports_UploadedAtUtc",
                table: "BankStatementImports",
                column: "UploadedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementImports_UploadedByUserId",
                table: "BankStatementImports",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_AccountId_ReconciliationStatus_Transacti~",
                table: "BankStatementLines",
                columns: new[] { "AccountId", "ReconciliationStatus", "TransactionDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_BankStatementImportId_LineNumber",
                table: "BankStatementLines",
                columns: new[] { "BankStatementImportId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_MatchedTreasuryTransactionId",
                table: "BankStatementLines",
                column: "MatchedTreasuryTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_ReconciledByUserId",
                table: "BankStatementLines",
                column: "ReconciledByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankStatementLines");

            migrationBuilder.DropTable(
                name: "BankStatementImports");
        }
    }
}
