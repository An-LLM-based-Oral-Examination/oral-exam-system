using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OralExamination.Infrastructure.Persistence;

namespace OralExamination.Infrastructure.Migrations;

[DbContext(typeof(OralExamDbContext))]
[Migration("20261114030000_AddGradePublicationTime")]
public sealed class AddGradePublicationTime : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>("grades_published_at", "student_exam_tickets", type: "timestamp with time zone", nullable: true);
        // Option A: one fresh window for legacy published grades, once at migration.
        // IsLocked alone is NOT proof of publication.
        migrationBuilder.Sql("UPDATE student_exam_tickets SET grades_published_at = CURRENT_TIMESTAMP WHERE grades_published_at IS NULL AND upper(status) IN ('PUBLISHED', 'LOCKED');");
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn("grades_published_at", "student_exam_tickets");
}
