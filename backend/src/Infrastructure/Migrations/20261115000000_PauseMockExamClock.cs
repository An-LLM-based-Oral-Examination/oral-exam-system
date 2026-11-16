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
        migrationBuilder.AddColumn<System.DateTime>("paused_at", "mock_exam_sessions", "timestamp with time zone", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("paused_at", "mock_exam_sessions");
    }
}
