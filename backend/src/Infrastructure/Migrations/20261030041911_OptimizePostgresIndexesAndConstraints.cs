using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizePostgresIndexesAndConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ai_evaluations_type",
                table: "ai_evaluations");

            migrationBuilder.AddColumn<string>(
                name: "selected_question_ids",
                table: "practice_sessions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "prompt_text",
                table: "practice_answers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "initial_voice_transcript",
                table: "mock_exam_answers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "submission_id",
                table: "lecturer_audit_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_practice_answers_status_submitted",
                table: "practice_answers",
                columns: new[] { "status", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_mock_exam_sessions_status_expires",
                table: "mock_exam_sessions",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_lecturer_audit_details_submission",
                table: "lecturer_audit_details",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_user_action_entity",
                table: "audit_logs",
                columns: new[] { "user_id", "action", "entity_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_evaluations_type",
                table: "ai_evaluations",
                sql: "answer_type IN ('practice', 'mock_exam', 'exam')");

            migrationBuilder.AddForeignKey(
                name: "FK_lecturer_audit_details_exam_question_submissions_submission~",
                table: "lecturer_audit_details",
                column: "submission_id",
                principalTable: "exam_question_submissions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_lecturer_audit_details_exam_question_submissions_submission~",
                table: "lecturer_audit_details");

            migrationBuilder.DropIndex(
                name: "ix_practice_answers_status_submitted",
                table: "practice_answers");

            migrationBuilder.DropIndex(
                name: "ix_mock_exam_sessions_status_expires",
                table: "mock_exam_sessions");

            migrationBuilder.DropIndex(
                name: "ix_lecturer_audit_details_submission",
                table: "lecturer_audit_details");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_user_action_entity",
                table: "audit_logs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ai_evaluations_type",
                table: "ai_evaluations");

            migrationBuilder.DropColumn(
                name: "selected_question_ids",
                table: "practice_sessions");

            migrationBuilder.DropColumn(
                name: "prompt_text",
                table: "practice_answers");

            migrationBuilder.DropColumn(
                name: "initial_voice_transcript",
                table: "mock_exam_answers");

            migrationBuilder.DropColumn(
                name: "submission_id",
                table: "lecturer_audit_details");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_evaluations_type",
                table: "ai_evaluations",
                sql: "answer_type IN ('practice', 'exam')");
        }
    }
}
