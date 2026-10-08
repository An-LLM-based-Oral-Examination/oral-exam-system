using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAudioUrlFromPractice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_mock_exam_answers_exam_questions_exam_question_id",
                table: "mock_exam_answers");

            migrationBuilder.DropColumn(
                name: "audio_url",
                table: "practice_answers");

            migrationBuilder.RenameColumn(
                name: "exam_question_id",
                table: "mock_exam_answers",
                newName: "question_id");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "student_exam_tickets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "SCHEDULED",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "checked_in");

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                table: "official_exam_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "exam_input_mode",
                table: "official_exam_sessions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "VoiceAndTextInput");

            migrationBuilder.AddColumn<bool>(
                name: "has_follow_up",
                table: "official_exam_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "max_follow_up_questions",
                table: "official_exam_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "transcript_buffer_seconds",
                table: "official_exam_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<bool>(
                name: "has_follow_up",
                table: "mock_exam_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "approval_status",
                table: "exam_questions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "DRAFT");

            migrationBuilder.AddColumn<string>(
                name: "review_notes",
                table: "exam_questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "exam_questions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "manual");

            migrationBuilder.AddColumn<Guid>(
                name: "submitted_by",
                table: "exam_questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "exam_input_mode",
                table: "courses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "VoiceAndTextInput");

            migrationBuilder.AddColumn<int>(
                name: "max_follow_up_questions",
                table: "courses",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "transcript_buffer_seconds",
                table: "courses",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<string>(
                name: "cot_trace",
                table: "ai_evaluations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "appeal_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    assigned_to = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    original_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    proposed_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    review_notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appeal_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_appeal_requests_exam_question_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "exam_question_submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_appeal_requests_official_exam_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "official_exam_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_appeal_requests_student_exam_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "student_exam_tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_appeal_requests_users_assigned_to",
                        column: x => x.assigned_to,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_appeal_requests_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_appeal_requests_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_official_exam_sessions_creator",
                table: "official_exam_sessions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_exam_questions_approval_status",
                table: "exam_questions",
                column: "approval_status");

            migrationBuilder.CreateIndex(
                name: "ix_exam_questions_submitted",
                table: "exam_questions",
                column: "submitted_by");

            migrationBuilder.CreateIndex(
                name: "ix_appeal_requests_assigned",
                table: "appeal_requests",
                columns: new[] { "assigned_to", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_appeal_requests_reviewed_by",
                table: "appeal_requests",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "ix_appeal_requests_session",
                table: "appeal_requests",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_appeal_requests_status",
                table: "appeal_requests",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_appeal_requests_student",
                table: "appeal_requests",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_appeal_requests_submission_id",
                table: "appeal_requests",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "ix_appeal_requests_ticket",
                table: "appeal_requests",
                column: "ticket_id");

            migrationBuilder.AddForeignKey(
                name: "FK_exam_questions_users_submitted_by",
                table: "exam_questions",
                column: "submitted_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_mock_exam_answers_practice_questions_question_id",
                table: "mock_exam_answers",
                column: "question_id",
                principalTable: "practice_questions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_official_exam_sessions_users_created_by",
                table: "official_exam_sessions",
                column: "created_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exam_questions_users_submitted_by",
                table: "exam_questions");

            migrationBuilder.DropForeignKey(
                name: "FK_mock_exam_answers_practice_questions_question_id",
                table: "mock_exam_answers");

            migrationBuilder.DropForeignKey(
                name: "FK_official_exam_sessions_users_created_by",
                table: "official_exam_sessions");

            migrationBuilder.DropTable(
                name: "appeal_requests");

            migrationBuilder.DropIndex(
                name: "ix_official_exam_sessions_creator",
                table: "official_exam_sessions");

            migrationBuilder.DropIndex(
                name: "ix_exam_questions_approval_status",
                table: "exam_questions");

            migrationBuilder.DropIndex(
                name: "ix_exam_questions_submitted",
                table: "exam_questions");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "official_exam_sessions");

            migrationBuilder.DropColumn(
                name: "exam_input_mode",
                table: "official_exam_sessions");

            migrationBuilder.DropColumn(
                name: "has_follow_up",
                table: "official_exam_sessions");

            migrationBuilder.DropColumn(
                name: "max_follow_up_questions",
                table: "official_exam_sessions");

            migrationBuilder.DropColumn(
                name: "transcript_buffer_seconds",
                table: "official_exam_sessions");

            migrationBuilder.DropColumn(
                name: "has_follow_up",
                table: "mock_exam_sessions");

            migrationBuilder.DropColumn(
                name: "approval_status",
                table: "exam_questions");

            migrationBuilder.DropColumn(
                name: "review_notes",
                table: "exam_questions");

            migrationBuilder.DropColumn(
                name: "source",
                table: "exam_questions");

            migrationBuilder.DropColumn(
                name: "submitted_by",
                table: "exam_questions");

            migrationBuilder.DropColumn(
                name: "exam_input_mode",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "max_follow_up_questions",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "transcript_buffer_seconds",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "cot_trace",
                table: "ai_evaluations");

            migrationBuilder.RenameColumn(
                name: "question_id",
                table: "mock_exam_answers",
                newName: "exam_question_id");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "student_exam_tickets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "checked_in",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "SCHEDULED");

            migrationBuilder.AddColumn<string>(
                name: "audio_url",
                table: "practice_answers",
                type: "text",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_mock_exam_answers_exam_questions_exam_question_id",
                table: "mock_exam_answers",
                column: "exam_question_id",
                principalTable: "exam_questions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
