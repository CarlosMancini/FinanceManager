using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowProfileUpdateAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_RegistrationAudits_Action",
                table: "RegistrationAudits");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RegistrationAudits_Action",
                table: "RegistrationAudits",
                sql: "[Action] IN (N'Registered', N'RegistrationCompleted', N'ProfileUpdated')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [RegistrationAudits] WHERE [Action] = N'ProfileUpdated') THROW 51000, 'Nao e possivel reverter: existem auditorias de atualizacao cadastral.', 1;");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RegistrationAudits_Action",
                table: "RegistrationAudits");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RegistrationAudits_Action",
                table: "RegistrationAudits",
                sql: "[Action] IN (N'Registered', N'RegistrationCompleted')");
        }
    }
}
