using Microsoft.EntityFrameworkCore.Migrations;
using OralExamination.Infrastructure.Persistence;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PostgresSchemaReconciler.GetPostMigrationSql());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
