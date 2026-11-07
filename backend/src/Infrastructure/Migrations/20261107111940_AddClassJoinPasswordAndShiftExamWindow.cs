using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClassJoinPasswordAndShiftExamWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_in_class_exam_shifts_status",
                table: "in_class_exam_shifts");

            migrationBuilder.AddColumn<int>(
                name: "exam_duration_minutes",
                table: "in_class_exam_shifts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "join_password_plain",
                table: "classes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "classes",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "join_password_plain",
                value: null);

            migrationBuilder.AddCheckConstraint(
                name: "ck_in_class_exam_shifts_exam_duration",
                table: "in_class_exam_shifts",
                sql: "exam_duration_minutes IS NULL OR (exam_duration_minutes >= 1 AND exam_duration_minutes <= 60)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_in_class_exam_shifts_status",
                table: "in_class_exam_shifts",
                sql: "status IN ('SCHEDULED', 'IN_PROGRESS', 'FINALIZED', 'CLOSED')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_in_class_exam_shifts_exam_duration",
                table: "in_class_exam_shifts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_in_class_exam_shifts_status",
                table: "in_class_exam_shifts");

            migrationBuilder.DropColumn(
                name: "exam_duration_minutes",
                table: "in_class_exam_shifts");

            migrationBuilder.DropColumn(
                name: "join_password_plain",
                table: "classes");

            migrationBuilder.AddCheckConstraint(
                name: "ck_in_class_exam_shifts_status",
                table: "in_class_exam_shifts",
                sql: "status IN ('SCHEDULED', 'IN_PROGRESS', 'FINALIZED', 'CANCELLED')");
        }
    }
}
