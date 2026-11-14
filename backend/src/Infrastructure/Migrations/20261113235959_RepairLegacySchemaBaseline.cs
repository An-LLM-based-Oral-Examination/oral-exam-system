using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OralExamination.Infrastructure.Persistence;

namespace OralExamination.Infrastructure.Migrations;

[DbContext(typeof(OralExamDbContext))]
[Migration("20261113235959_RepairLegacySchemaBaseline")]
public sealed class RepairLegacySchemaBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Apply a frozen baseline even when the legacy UpdateDB ID is already recorded.
        // Run before the explicit CLO/appeal migrations, which own their newer constraints.
        migrationBuilder.Sql(UpdateDB.FrozenSchemaSql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Repairs existing shared schema; dropping it would destroy pre-existing data.
        throw new NotSupportedException("Legacy schema repair cannot be safely reversed.");
    }
}
