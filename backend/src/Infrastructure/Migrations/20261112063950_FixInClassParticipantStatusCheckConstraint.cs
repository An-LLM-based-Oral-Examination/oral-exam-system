using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixInClassParticipantStatusCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_in_class_shift_participants_status",
                table: "in_class_shift_participants");

            migrationBuilder.AddCheckConstraint(
                name: "ck_in_class_shift_participants_status",
                table: "in_class_shift_participants",
                sql: "status IN ('JOINED', 'IN_PROGRESS', 'SUBMITTED', 'GRADED', 'FINALIZED', 'ABSENT')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_in_class_shift_participants_status",
                table: "in_class_shift_participants");

            migrationBuilder.AddCheckConstraint(
                name: "ck_in_class_shift_participants_status",
                table: "in_class_shift_participants",
                sql: "status IN ('JOINED', 'SUBMITTED', 'ABSENT')");
        }
    }
}
