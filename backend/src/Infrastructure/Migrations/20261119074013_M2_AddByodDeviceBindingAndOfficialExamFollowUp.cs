using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M2_AddByodDeviceBindingAndOfficialExamFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_ticket_question",
                table: "exam_question_submissions");

            migrationBuilder.AddColumn<string>(
                name: "device_id",
                table: "student_exam_tickets",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "device_locked_at",
                table: "student_exam_tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "device_session_token_hash",
                table: "student_exam_tickets",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "device_unlock_count",
                table: "student_exam_tickets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_device_unlocked_at",
                table: "student_exam_tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "last_device_unlocked_by",
                table: "student_exam_tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certificate_hash",
                table: "real_exam_session_shifts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "certificate_signed_at",
                table: "real_exam_session_shifts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certificate_thumbprint",
                table: "real_exam_session_shifts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "audio_object_key",
                table: "exam_question_submissions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "audio_verified_at",
                table: "exam_question_submissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE exam_question_submissions AS s
                SET audio_object_key = COALESCE(s.audio_object_key, t.audio_object_key),
                    audio_verified_at = COALESCE(s.audio_verified_at, t.audio_verified_at)
                FROM student_exam_tickets AS t
                WHERE s.ticket_id = t.id
                  AND (s.audio_object_key IS NULL OR s.audio_verified_at IS NULL)
                  AND (t.audio_object_key IS NOT NULL OR t.audio_verified_at IS NOT NULL);
                """);

            migrationBuilder.AddColumn<int>(
                name: "follow_up_index",
                table: "exam_question_submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "follow_up_prompt",
                table: "exam_question_submissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "generated_follow_up_prompt",
                table: "exam_question_submissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "initial_voice_transcript",
                table: "exam_question_submissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_follow_up",
                table: "exam_question_submissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_submission_id",
                table: "exam_question_submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "transcript_edit_ratio",
                table: "exam_question_submissions",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "client_event_id",
                table: "exam_anti_cheat_logs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_exam_tickets_device_id",
                table: "student_exam_tickets",
                column: "device_id",
                filter: "device_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_student_exam_tickets_last_unlocked_by",
                table: "student_exam_tickets",
                column: "last_device_unlocked_by");

            migrationBuilder.CreateIndex(
                name: "uq_tickets_shift_device",
                table: "student_exam_tickets",
                columns: new[] { "shift_id", "device_id" },
                unique: true,
                filter: "device_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tickets_device_binding_consistency",
                table: "student_exam_tickets",
                sql: "(device_id IS NULL AND device_session_token_hash IS NULL) OR (device_id IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tickets_device_unlock_count",
                table: "student_exam_tickets",
                sql: "device_unlock_count >= 0");

            migrationBuilder.CreateIndex(
                name: "ix_real_exam_shifts_certificate_hash",
                table: "real_exam_session_shifts",
                column: "certificate_hash");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shifts_certificate_hash_len",
                table: "real_exam_session_shifts",
                sql: "certificate_hash IS NULL OR length(certificate_hash) = 64");

            migrationBuilder.CreateIndex(
                name: "ix_exam_question_submissions_parent",
                table: "exam_question_submissions",
                column: "parent_submission_id");

            migrationBuilder.CreateIndex(
                name: "uq_exam_submissions_ticket_parent",
                table: "exam_question_submissions",
                columns: new[] { "ticket_id", "parent_submission_id" },
                unique: true,
                filter: "is_follow_up = true AND parent_submission_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_exam_submissions_ticket_question_turn",
                table: "exam_question_submissions",
                columns: new[] { "ticket_id", "exam_question_id", "follow_up_index" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_exam_question_submissions_follow_up_consistency",
                table: "exam_question_submissions",
                sql: "(is_follow_up = false AND parent_submission_id IS NULL AND follow_up_index = 0 AND follow_up_prompt IS NULL) OR (is_follow_up = true AND parent_submission_id IS NOT NULL AND follow_up_index >= 1 AND follow_up_index <= 5 AND follow_up_prompt IS NOT NULL AND btrim(follow_up_prompt) <> '')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exam_question_submissions_follow_up_index",
                table: "exam_question_submissions",
                sql: "follow_up_index >= 0 AND follow_up_index <= 5");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exam_question_submissions_transcript_edit_ratio",
                table: "exam_question_submissions",
                sql: "transcript_edit_ratio IS NULL OR (transcript_edit_ratio >= 0 AND transcript_edit_ratio <= 1)");

            migrationBuilder.CreateIndex(
                name: "ix_exam_anti_cheat_logs_shift_occurred",
                table: "exam_anti_cheat_logs",
                columns: new[] { "shift_id", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_exam_anti_cheat_logs_ticket_occurred",
                table: "exam_anti_cheat_logs",
                columns: new[] { "ticket_id", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "uq_exam_anti_cheat_logs_ticket_client_event",
                table: "exam_anti_cheat_logs",
                columns: new[] { "ticket_id", "client_event_id" },
                unique: true,
                filter: "client_event_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_exam_question_submissions_parent_submission_id",
                table: "exam_question_submissions",
                column: "parent_submission_id",
                principalTable: "exam_question_submissions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_student_exam_tickets_users_last_device_unlocked_by",
                table: "student_exam_tickets",
                column: "last_device_unlocked_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exam_question_submissions_parent_submission_id",
                table: "exam_question_submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_student_exam_tickets_users_last_device_unlocked_by",
                table: "student_exam_tickets");

            migrationBuilder.DropIndex(
                name: "ix_student_exam_tickets_device_id",
                table: "student_exam_tickets");

            migrationBuilder.DropIndex(
                name: "ix_student_exam_tickets_last_unlocked_by",
                table: "student_exam_tickets");

            migrationBuilder.DropIndex(
                name: "uq_tickets_shift_device",
                table: "student_exam_tickets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tickets_device_binding_consistency",
                table: "student_exam_tickets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tickets_device_unlock_count",
                table: "student_exam_tickets");

            migrationBuilder.DropIndex(
                name: "ix_real_exam_shifts_certificate_hash",
                table: "real_exam_session_shifts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shifts_certificate_hash_len",
                table: "real_exam_session_shifts");

            migrationBuilder.DropIndex(
                name: "ix_exam_question_submissions_parent",
                table: "exam_question_submissions");

            migrationBuilder.DropIndex(
                name: "uq_exam_submissions_ticket_parent",
                table: "exam_question_submissions");

            migrationBuilder.DropIndex(
                name: "uq_exam_submissions_ticket_question_turn",
                table: "exam_question_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exam_question_submissions_follow_up_consistency",
                table: "exam_question_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exam_question_submissions_follow_up_index",
                table: "exam_question_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exam_question_submissions_transcript_edit_ratio",
                table: "exam_question_submissions");

            migrationBuilder.DropIndex(
                name: "ix_exam_anti_cheat_logs_shift_occurred",
                table: "exam_anti_cheat_logs");

            migrationBuilder.DropIndex(
                name: "ix_exam_anti_cheat_logs_ticket_occurred",
                table: "exam_anti_cheat_logs");

            migrationBuilder.DropIndex(
                name: "uq_exam_anti_cheat_logs_ticket_client_event",
                table: "exam_anti_cheat_logs");

            migrationBuilder.DropColumn(
                name: "device_id",
                table: "student_exam_tickets");

            migrationBuilder.DropColumn(
                name: "device_locked_at",
                table: "student_exam_tickets");

            migrationBuilder.DropColumn(
                name: "device_session_token_hash",
                table: "student_exam_tickets");

            migrationBuilder.DropColumn(
                name: "device_unlock_count",
                table: "student_exam_tickets");

            migrationBuilder.DropColumn(
                name: "last_device_unlocked_at",
                table: "student_exam_tickets");

            migrationBuilder.DropColumn(
                name: "last_device_unlocked_by",
                table: "student_exam_tickets");

            migrationBuilder.DropColumn(
                name: "certificate_hash",
                table: "real_exam_session_shifts");

            migrationBuilder.DropColumn(
                name: "certificate_signed_at",
                table: "real_exam_session_shifts");

            migrationBuilder.DropColumn(
                name: "certificate_thumbprint",
                table: "real_exam_session_shifts");

            migrationBuilder.DropColumn(
                name: "audio_object_key",
                table: "exam_question_submissions");

            migrationBuilder.DropColumn(
                name: "audio_verified_at",
                table: "exam_question_submissions");

            migrationBuilder.DropColumn(
                name: "follow_up_index",
                table: "exam_question_submissions");

            migrationBuilder.DropColumn(
                name: "follow_up_prompt",
                table: "exam_question_submissions");

            migrationBuilder.DropColumn(
                name: "generated_follow_up_prompt",
                table: "exam_question_submissions");

            migrationBuilder.DropColumn(
                name: "initial_voice_transcript",
                table: "exam_question_submissions");

            migrationBuilder.DropColumn(
                name: "is_follow_up",
                table: "exam_question_submissions");

            migrationBuilder.DropColumn(
                name: "parent_submission_id",
                table: "exam_question_submissions");

            migrationBuilder.DropColumn(
                name: "transcript_edit_ratio",
                table: "exam_question_submissions");

            migrationBuilder.DropColumn(
                name: "client_event_id",
                table: "exam_anti_cheat_logs");

            migrationBuilder.CreateIndex(
                name: "uq_ticket_question",
                table: "exam_question_submissions",
                columns: new[] { "ticket_id", "exam_question_id" },
                unique: true);
        }
    }
}
