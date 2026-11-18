using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSemesterDatesAndCourseCreditsConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE semesters DROP CONSTRAINT IF EXISTS ck_semesters_dates;");
            migrationBuilder.Sql("ALTER TABLE courses DROP CONSTRAINT IF EXISTS ck_courses_credits;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_semesters_dates",
                table: "semesters",
                sql: "end_date > start_date");

            migrationBuilder.AddCheckConstraint(
                name: "ck_courses_credits",
                table: "courses",
                sql: "credits >= 1 AND credits <= 10");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE semesters DROP CONSTRAINT IF EXISTS ck_semesters_dates;");
            migrationBuilder.Sql("ALTER TABLE courses DROP CONSTRAINT IF EXISTS ck_courses_credits;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_semesters_dates",
                table: "semesters",
                sql: "end_date >= start_date");

            migrationBuilder.AddCheckConstraint(
                name: "ck_courses_credits",
                table: "courses",
                sql: "credits > 0");
        }
    }
}
