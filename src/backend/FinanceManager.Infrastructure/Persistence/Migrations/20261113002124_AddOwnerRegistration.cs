using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF (SELECT COUNT_BIG(*) FROM [AspNetUsers]) > 1 THROW 51000, 'Mais de um titular impede a migração.', 1; UPDATE [AspNetUsers] SET [ProvisioningSlot] = 1 WHERE [ProvisioningSlot] IS NULL OR [ProvisioningSlot] <> 1;");
            migrationBuilder.DropIndex(
                name: "EmailIndex",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "UX_Owners_ProvisioningSlot",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<byte>(
                name: "ProvisioningSlot",
                table: "AspNetUsers",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0,
                oldClrType: typeof(byte),
                oldType: "tinyint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BirthDate",
                table: "AspNetUsers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RegistrationCompletedAtUtc",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RegistrationAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationAudits", x => x.Id);
                    table.CheckConstraint("CK_RegistrationAudits_Action", "[Action] IN (N'Registered', N'RegistrationCompleted')");
                    table.ForeignKey(
                        name: "FK_RegistrationAudits_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationAuthorizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    VerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationAuthorizations", x => x.Id);
                    table.CheckConstraint("CK_RegistrationAuthorizations_Expiry", "[ExpiresAtUtc] = DATEADD(minute, 10, [VerifiedAtUtc])");
                    table.CheckConstraint("CK_RegistrationAuthorizations_Revocation", "[RevokedAtUtc] IS NULL OR [RevokedAtUtc] >= [VerifiedAtUtc]");
                    table.ForeignKey(
                        name: "FK_RegistrationAuthorizations_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Owners_ProvisioningSlot",
                table: "AspNetUsers",
                column: "ProvisioningSlot",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Owners_ProvisioningSlot",
                table: "AspNetUsers",
                sql: "[ProvisioningSlot] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Owners_RegistrationProfile",
                table: "AspNetUsers",
                sql: "[RegistrationCompletedAtUtc] IS NULL OR ([FullName] IS NOT NULL AND LEN(LTRIM(RTRIM([FullName]))) > 0 AND [BirthDate] IS NOT NULL AND [Email] IS NOT NULL AND LEN(LTRIM(RTRIM([Email]))) > 0 AND [NormalizedEmail] IS NOT NULL AND LEN(LTRIM(RTRIM([NormalizedEmail]))) > 0 AND [PhoneNumber] IS NOT NULL AND LEN(LTRIM(RTRIM([PhoneNumber]))) > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationAudits_OwnerId",
                table: "RegistrationAudits",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationAuthorizations_OwnerId",
                table: "RegistrationAuthorizations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationAuthorizations_TokenHash",
                table: "RegistrationAuthorizations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrationAudits");

            migrationBuilder.DropTable(
                name: "RegistrationAuthorizations");

            migrationBuilder.DropIndex(
                name: "UX_Owners_ProvisioningSlot",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Owners_ProvisioningSlot",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Owners_RegistrationProfile",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Nickname",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RegistrationCompletedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<byte>(
                name: "ProvisioningSlot",
                table: "AspNetUsers",
                type: "tinyint",
                nullable: true,
                oldClrType: typeof(byte),
                oldType: "tinyint");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                table: "AspNetUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "AspNetUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UX_Owners_ProvisioningSlot",
                table: "AspNetUsers",
                column: "ProvisioningSlot",
                unique: true,
                filter: "[ProvisioningSlot] IS NOT NULL");
        }
    }
}
