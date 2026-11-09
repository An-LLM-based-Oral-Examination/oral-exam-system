using Microsoft.EntityFrameworkCore.Migrations;
using OralExamination.Infrastructure.Persistence;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDlqFlowTypeAndRoleSeparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PostgresSchemaReconciler.GetPreMigrationSql());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
