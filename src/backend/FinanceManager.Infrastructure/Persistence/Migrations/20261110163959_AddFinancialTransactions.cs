using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_FinancialAccounts_OwnerId_Id",
                table: "FinancialAccounts",
                columns: new[] { "OwnerId", "Id" });

            migrationBuilder.CreateTable(
                name: "FinancialTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    CreationRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreationFingerprint = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CanceledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialTransactions", x => x.Id);
                    table.UniqueConstraint("AK_FinancialTransactions_OwnerId_Id", x => new { x.OwnerId, x.Id });
                    table.CheckConstraint("CK_FinancialTransactions_Amount", "[Amount] >= 0.01 AND [Amount] <= 99999999999999999.99");
                    table.CheckConstraint("CK_FinancialTransactions_Cancellation", "([Status] = N'Canceled' AND [CanceledAtUtc] IS NOT NULL) OR ([Status] = N'Active' AND [CanceledAtUtc] IS NULL)");
                    table.CheckConstraint("CK_FinancialTransactions_Currency", "[Currency] = N'BRL'");
                    table.CheckConstraint("CK_FinancialTransactions_Kind", "[Kind] IN (N'Income', N'Expense')");
                    table.CheckConstraint("CK_FinancialTransactions_Source", "[Source] = N'Manual'");
                    table.CheckConstraint("CK_FinancialTransactions_Status", "[Status] IN (N'Active', N'Canceled')");
                    table.ForeignKey(
                        name: "FK_FinancialTransactions_FinancialAccounts_OwnerId_AccountId",
                        columns: x => new { x.OwnerId, x.AccountId },
                        principalTable: "FinancialAccounts",
                        principalColumns: new[] { "OwnerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancialTransactionAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialTransactionAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialTransactionAudits_FinancialTransactions_OwnerId_TransactionId",
                        columns: x => new { x.OwnerId, x.TransactionId },
                        principalTable: "FinancialTransactions",
                        principalColumns: new[] { "OwnerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactionAudits_OwnerId_TransactionId",
                table: "FinancialTransactionAudits",
                columns: new[] { "OwnerId", "TransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactions_OwnerId_AccountId",
                table: "FinancialTransactions",
                columns: new[] { "OwnerId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactions_OwnerId_TransactionDate_Id",
                table: "FinancialTransactions",
                columns: new[] { "OwnerId", "TransactionDate", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "UX_FinancialTransactions_OwnerId_CreationRequestId",
                table: "FinancialTransactions",
                columns: new[] { "OwnerId", "CreationRequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancialTransactionAudits");

            migrationBuilder.DropTable(
                name: "FinancialTransactions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_FinancialAccounts_OwnerId_Id",
                table: "FinancialAccounts");
        }
    }
}
