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
            migrationBuilder.AddColumn<int>(
                name: "max_seats",
                table: "real_exam_session_shifts",
                type: "integer",
                nullable: false,
                defaultValue: 24);

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

            migrationBuilder.UpdateData(
                table: "real_exam_session_shifts",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000023"),
                column: "max_seats",
                value: 24);

            migrationBuilder.AddCheckConstraint(
                name: "ck_shifts_max_seats",
                table: "real_exam_session_shifts",
                sql: "max_seats >= 1 AND max_seats <= 40");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_shifts_max_seats",
                table: "real_exam_session_shifts");

            migrationBuilder.DropColumn(
                name: "max_seats",
                table: "real_exam_session_shifts");
        }
    }
}
