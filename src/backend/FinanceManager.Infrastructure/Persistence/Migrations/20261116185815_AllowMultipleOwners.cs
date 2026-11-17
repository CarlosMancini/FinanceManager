using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Owners_ProvisioningSlot",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Owners_ProvisioningSlot",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<byte[]>(
                name: "NormalizedEmailHash",
                table: "AspNetUsers",
                type: "binary(32)",
                nullable: true,
                computedColumnSql: "CONVERT(binary(32), HASHBYTES('SHA2_256', [NormalizedEmail]))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "UX_Owners_NormalizedEmailHash",
                table: "AspNetUsers",
                column: "NormalizedEmailHash",
                unique: true,
                filter: "[NormalizedEmail] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF (SELECT COUNT_BIG(*) FROM [AspNetUsers]) > 1 THROW 51000, 'Não é possível reverter para titular único com múltiplos usuários. Nenhum dado foi excluído.', 1;");

            migrationBuilder.DropIndex(
                name: "UX_Owners_NormalizedEmailHash",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NormalizedEmailHash",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "UX_Owners_ProvisioningSlot",
                table: "AspNetUsers",
                column: "ProvisioningSlot",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Owners_ProvisioningSlot",
                table: "AspNetUsers",
                sql: "[ProvisioningSlot] = 1");
        }
    }
}
