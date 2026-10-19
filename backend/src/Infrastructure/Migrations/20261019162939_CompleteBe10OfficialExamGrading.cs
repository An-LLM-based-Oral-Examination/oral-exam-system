using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteBe10OfficialExamGrading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ai_evaluations_exam_submission",
                table: "ai_evaluations");

            migrationBuilder.AddColumn<string>(
                name: "audio_hash_sha256",
                table: "student_exam_tickets",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "audio_object_key",
                table: "student_exam_tickets",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "audio_r2_url",
                table: "student_exam_tickets",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "audio_verified_at",
                table: "student_exam_tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "grading_started_at",
                table: "exam_question_submissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_suspicious",
                table: "exam_question_submissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "suspicious_reason",
                table: "exam_question_submissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "correlation_id",
                table: "dead_letter_queues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_attempt_at",
                table: "dead_letter_queues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "next_retry_at",
                table: "dead_letter_queues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "grading_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    locked_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grading_jobs", x => x.id);
                    table.CheckConstraint("ck_grading_jobs_attempt_count", "attempt_count >= 0");
                    table.CheckConstraint("ck_grading_jobs_status", "status IN ('pending', 'processing', 'completed', 'failed')");
                    table.ForeignKey(
                        name: "FK_grading_jobs_exam_question_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "exam_question_submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "student_exam_tickets",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000024"),
                columns: new[] { "audio_hash_sha256", "audio_object_key", "audio_r2_url", "audio_verified_at" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "student_exam_tickets",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000025"),
                columns: new[] { "audio_hash_sha256", "audio_object_key", "audio_r2_url", "audio_verified_at" },
                values: new object[] { null, null, null, null });

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_queues_correlation",
                table: "dead_letter_queues",
                columns: new[] { "task_type", "correlation_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_queues_retry",
                table: "dead_letter_queues",
                columns: new[] { "status", "next_retry_at" });

            migrationBuilder.CreateIndex(
                name: "uq_ai_evaluations_exam_submission",
                table: "ai_evaluations",
                column: "exam_submission_id",
                unique: true,
                filter: "exam_submission_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_grading_jobs_dispatch",
                table: "grading_jobs",
                columns: new[] { "status", "next_attempt_at", "locked_until" });

            migrationBuilder.CreateIndex(
                name: "uq_grading_jobs_submission",
                table: "grading_jobs",
                column: "submission_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grading_jobs");

            migrationBuilder.DropIndex(
                name: "ix_dead_letter_queues_correlation",
                table: "dead_letter_queues");

            migrationBuilder.DropIndex(
                name: "ix_dead_letter_queues_retry",
                table: "dead_letter_queues");

            migrationBuilder.DropIndex(
                name: "uq_ai_evaluations_exam_submission",
                table: "ai_evaluations");

            migrationBuilder.DropColumn(
                name: "audio_hash_sha256",
                table: "student_exam_tickets");

            migrationBuilder.DropColumn(
                name: "audio_object_key",
                table: "student_exam_tickets");

            migrationBuilder.DropColumn(
                name: "audio_r2_url",
                table: "student_exam_tickets");

            migrationBuilder.DropColumn(
                name: "audio_verified_at",
                table: "student_exam_tickets");

            migrationBuilder.DropColumn(
                name: "grading_started_at",
                table: "exam_question_submissions");

            migrationBuilder.DropColumn(
                name: "is_suspicious",
                table: "exam_question_submissions");

            migrationBuilder.DropColumn(
                name: "suspicious_reason",
                table: "exam_question_submissions");

            migrationBuilder.DropColumn(
                name: "correlation_id",
                table: "dead_letter_queues");

            migrationBuilder.DropColumn(
                name: "last_attempt_at",
                table: "dead_letter_queues");

            migrationBuilder.DropColumn(
                name: "next_retry_at",
                table: "dead_letter_queues");

            migrationBuilder.CreateIndex(
                name: "ix_ai_evaluations_exam_submission",
                table: "ai_evaluations",
                column: "exam_submission_id");
        }
    }
}
