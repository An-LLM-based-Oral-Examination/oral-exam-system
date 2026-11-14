using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OralExamination.Infrastructure.Persistence;

namespace OralExamination.Infrastructure.Migrations;

[DbContext(typeof(OralExamDbContext))]
[Migration("20261114020000_RepairDuplicateCourseOfferings")]
public sealed class RepairDuplicateCourseOfferings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            LOCK TABLE course_offerings, course_lecturers, classes IN SHARE ROW EXCLUSIVE MODE;
            CREATE TEMP TABLE offering_canonical ON COMMIT DROP AS
            SELECT id, first_value(id) OVER (
                PARTITION BY course_id, semester_id ORDER BY created_at NULLS LAST, id
            ) AS canonical_id
            FROM course_offerings;
            UPDATE course_lecturers cl SET course_offering_id = m.canonical_id
            FROM offering_canonical m
            WHERE cl.course_offering_id = m.id AND m.id <> m.canonical_id;
            UPDATE classes c SET course_offering_id = m.canonical_id
            FROM offering_canonical m
            WHERE c.course_offering_id = m.id AND m.id <> m.canonical_id;
            UPDATE course_offerings o SET is_enabled = true
            WHERE o.id IN (
                SELECT m.canonical_id FROM offering_canonical m
                JOIN course_offerings source ON source.id = m.id WHERE source.is_enabled = true
            );
            DELETE FROM course_offerings o USING offering_canonical m
            WHERE o.id = m.id AND m.id <> m.canonical_id;
            CREATE UNIQUE INDEX IF NOT EXISTS uq_course_offerings_course_semester
                ON course_offerings (course_id, semester_id);
            """);
    }

    // Data consolidation preserves all assignments/classes but cannot recreate duplicate IDs.
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
