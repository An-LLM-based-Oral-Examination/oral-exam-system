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
            migrationBuilder.DropCheckConstraint(
                name: "ck_semesters_dates",
                table: "semesters");

            migrationBuilder.DropCheckConstraint(
                name: "ck_courses_credits",
                table: "courses");

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
            migrationBuilder.DropCheckConstraint(
                name: "ck_semesters_dates",
                table: "semesters");

            migrationBuilder.DropCheckConstraint(
                name: "ck_courses_credits",
                table: "courses");

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
