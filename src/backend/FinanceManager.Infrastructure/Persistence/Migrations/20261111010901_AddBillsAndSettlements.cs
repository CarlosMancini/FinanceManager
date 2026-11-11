using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillsAndSettlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_FinancialTransactions_OwnerId_CreationRequestId",
                table: "FinancialTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialTransactions_Source",
                table: "FinancialTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "FinancialTransactions",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreationRequestId",
                table: "FinancialTransactions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<byte[]>(
                name: "CreationFingerprint",
                table: "FinancialTransactions",
                type: "binary(32)",
                fixedLength: true,
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "binary(32)",
                oldFixedLength: true,
                oldMaxLength: 32);

            migrationBuilder.CreateTable(
                name: "Bills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CanceledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bills", x => x.Id);
                    table.UniqueConstraint("AK_Bills_OwnerId_Id", x => new { x.OwnerId, x.Id });
                    table.CheckConstraint("CK_Bills_Amount", "[Amount] >= 0.01 AND [Amount] <= 99999999999999999.99");
                    table.CheckConstraint("CK_Bills_Currency", "[Currency] = N'BRL'");
                    table.ForeignKey(
                        name: "FK_Bills_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BillAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillAudits_Bills_OwnerId_BillId",
                        columns: x => new { x.OwnerId, x.BillId },
                        principalTable: "Bills",
                        principalColumns: new[] { "OwnerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BillSettlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReversedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillSettlements", x => x.Id);
                    table.UniqueConstraint("AK_BillSettlements_OwnerId_Id", x => new { x.OwnerId, x.Id });
                    table.CheckConstraint("CK_BillSettlements_Origin", "[Origin] IN (N'LinkedExpense', N'CreatedExpense')");
                    table.ForeignKey(
                        name: "FK_BillSettlements_Bills_OwnerId_BillId",
                        columns: x => new { x.OwnerId, x.BillId },
                        principalTable: "Bills",
                        principalColumns: new[] { "OwnerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BillSettlements_FinancialTransactions_OwnerId_TransactionId",
                        columns: x => new { x.OwnerId, x.TransactionId },
                        principalTable: "FinancialTransactions",
                        principalColumns: new[] { "OwnerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BillSettlementRequests",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    SettlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillSettlementRequests", x => new { x.OwnerId, x.IdempotencyKey });
                    table.ForeignKey(
                        name: "FK_BillSettlementRequests_BillSettlements_OwnerId_SettlementId",
                        columns: x => new { x.OwnerId, x.SettlementId },
                        principalTable: "BillSettlements",
                        principalColumns: new[] { "OwnerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_FinancialTransactions_OwnerId_CreationRequestId",
                table: "FinancialTransactions",
                columns: new[] { "OwnerId", "CreationRequestId" },
                unique: true,
                filter: "[CreationRequestId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialTransactions_CreationRequest",
                table: "FinancialTransactions",
                sql: "([Source] = N'Manual' AND [CreationRequestId] IS NOT NULL AND [CreationFingerprint] IS NOT NULL) OR ([Source] = N'BillSettlement' AND [CreationRequestId] IS NULL AND [CreationFingerprint] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialTransactions_Source",
                table: "FinancialTransactions",
                sql: "[Source] IN (N'Manual', N'BillSettlement')");

            migrationBuilder.CreateIndex(
                name: "IX_BillAudits_OwnerId_BillId",
                table: "BillAudits",
                columns: new[] { "OwnerId", "BillId" });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_OwnerId_DueDate_Id",
                table: "Bills",
                columns: new[] { "OwnerId", "DueDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_BillSettlementRequests_OwnerId_SettlementId",
                table: "BillSettlementRequests",
                columns: new[] { "OwnerId", "SettlementId" });

            migrationBuilder.CreateIndex(
                name: "UX_BillSettlements_OwnerId_BillId_Active",
                table: "BillSettlements",
                columns: new[] { "OwnerId", "BillId" },
                unique: true,
                filter: "[ReversedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_BillSettlements_OwnerId_TransactionId_Active",
                table: "BillSettlements",
                columns: new[] { "OwnerId", "TransactionId" },
                unique: true,
                filter: "[ReversedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillAudits");

            migrationBuilder.DropTable(
                name: "BillSettlementRequests");

            migrationBuilder.DropTable(
                name: "BillSettlements");

            migrationBuilder.DropTable(
                name: "Bills");

            migrationBuilder.DropIndex(
                name: "UX_FinancialTransactions_OwnerId_CreationRequestId",
                table: "FinancialTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialTransactions_CreationRequest",
                table: "FinancialTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialTransactions_Source",
                table: "FinancialTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "FinancialTransactions",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreationRequestId",
                table: "FinancialTransactions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "CreationFingerprint",
                table: "FinancialTransactions",
                type: "binary(32)",
                fixedLength: true,
                maxLength: 32,
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "binary(32)",
                oldFixedLength: true,
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_FinancialTransactions_OwnerId_CreationRequestId",
                table: "FinancialTransactions",
                columns: new[] { "OwnerId", "CreationRequestId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialTransactions_Source",
                table: "FinancialTransactions",
                sql: "[Source] = N'Manual'");
        }
    }
}
