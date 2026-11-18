using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficialShiftCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE real_exam_session_shifts ADD COLUMN IF NOT EXISTS max_seats integer NOT NULL DEFAULT 24;");

            migrationBuilder.Sql(
                """
                UPDATE real_exam_session_shifts AS shift
                SET max_seats = GREATEST(
                    24,
                    COALESCE((
                        SELECT MAX(ticket.seat_number)
                        FROM student_exam_tickets AS ticket
                        WHERE ticket.shift_id = shift.id
                    ), 0));
                """);

            migrationBuilder.Sql("UPDATE real_exam_session_shifts SET max_seats = 24 WHERE id = '00000000-0000-0000-0000-000000000023'::uuid;");

            migrationBuilder.Sql("ALTER TABLE real_exam_session_shifts DROP CONSTRAINT IF EXISTS ck_shifts_max_seats;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shifts_max_seats",
                table: "real_exam_session_shifts",
                sql: "max_seats >= 1 AND max_seats <= 40");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE real_exam_session_shifts DROP CONSTRAINT IF EXISTS ck_shifts_max_seats;");
            migrationBuilder.Sql("ALTER TABLE real_exam_session_shifts DROP COLUMN IF EXISTS max_seats;");
        }
    }
}
