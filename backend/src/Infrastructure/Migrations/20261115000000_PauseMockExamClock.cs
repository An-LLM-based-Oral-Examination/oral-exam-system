using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OralExamination.Infrastructure.Persistence;

namespace OralExamination.Infrastructure.Migrations;

[DbContext(typeof(OralExamDbContext))]
[Migration("20261115000000_PauseMockExamClock")]
public sealed class PauseMockExamClock : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE mock_exam_sessions ADD COLUMN IF NOT EXISTS paused_at timestamp with time zone NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE mock_exam_sessions DROP COLUMN IF EXISTS paused_at;");
    }
}
