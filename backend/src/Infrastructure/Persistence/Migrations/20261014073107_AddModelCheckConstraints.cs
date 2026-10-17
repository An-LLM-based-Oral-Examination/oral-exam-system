using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModelCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "student_acknowledgement_status",
                table: "student_exam_tickets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PENDING");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_role",
                table: "users",
                sql: "role IN ('admin', 'department_head', 'lecturer', 'proctor', 'student')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tickets_ack_status",
                table: "student_exam_tickets",
                sql: "student_acknowledgement_status IN ('PENDING', 'ACKNOWLEDGED', 'APPEALED')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tickets_seat",
                table: "student_exam_tickets",
                sql: "seat_number >= 1 AND seat_number <= 40");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tickets_status",
                table: "student_exam_tickets",
                sql: "status IN ('SCHEDULED', 'IN_PROGRESS', 'SUBMITTED', 'AI_GRADED', 'AUDITED', 'PUBLISHED', 'LOCKED')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_semesters_dates",
                table: "semesters",
                sql: "end_date >= start_date");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rubrics_total_score",
                table: "rubrics",
                sql: "total_max_score = 10.00");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rubric_criteria_score",
                table: "rubric_criteria",
                sql: "max_score > 0 AND max_score <= 10.00");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rubric_criteria_weight",
                table: "rubric_criteria",
                sql: "weight > 0 AND weight <= 1.00");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shifts_status",
                table: "real_exam_session_shifts",
                sql: "status IN ('scheduled', 'in_progress', 'completed', 'cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shifts_time",
                table: "real_exam_session_shifts",
                sql: "end_time > start_time");

            migrationBuilder.AddCheckConstraint(
                name: "ck_practice_sessions_mode",
                table: "practice_sessions",
                sql: "practice_mode IN ('per_question', 'full_session')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_practice_sessions_status",
                table: "practice_sessions",
                sql: "status IN ('in_progress', 'completed', 'abandoned', 'timed_out')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_practice_questions_bloom",
                table: "practice_questions",
                sql: "bloom_level IN ('Remember', 'Understand', 'Apply', 'Analyze', 'Evaluate', 'Create')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_practice_questions_diff",
                table: "practice_questions",
                sql: "difficulty IN ('easy', 'medium', 'hard')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_practice_answers_status",
                table: "practice_answers",
                sql: "status IN ('pending', 'grading', 'graded', 'failed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_official_sessions_input_mode",
                table: "official_exam_sessions",
                sql: "exam_input_mode IN ('VoiceOnly', 'VoiceWithTranscriptEdit')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_official_sessions_max_follow_up",
                table: "official_exam_sessions",
                sql: "max_follow_up_questions >= 1 AND max_follow_up_questions <= 5");

            migrationBuilder.AddCheckConstraint(
                name: "ck_official_sessions_status",
                table: "official_exam_sessions",
                sql: "status IN ('scheduled', 'in_progress', 'grading', 'auditing', 'concluded')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_official_sessions_transcript_buffer",
                table: "official_exam_sessions",
                sql: "transcript_buffer_seconds >= 10 AND transcript_buffer_seconds <= 300");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mock_sessions_status",
                table: "mock_exam_sessions",
                sql: "status IN ('in_progress', 'submitted', 'graded', 'abandoned')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mock_exam_quotas_count",
                table: "mock_exam_quotas",
                sql: "used_count >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mock_answers_status",
                table: "mock_exam_answers",
                sql: "status IN ('pending', 'answered', 'graded', 'failed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_lecturer_audits_scores",
                table: "lecturer_audits",
                sql: "original_ai_score >= 0 AND audited_score >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_details_scores",
                table: "lecturer_audit_details",
                sql: "ai_score >= 0 AND audited_score >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exam_struct_duration",
                table: "exam_structures",
                sql: "duration_minutes > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exam_struct_questions",
                table: "exam_structures",
                sql: "total_questions > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exam_questions_bloom",
                table: "exam_questions",
                sql: "bloom_level IN ('Remember', 'Understand', 'Apply', 'Analyze', 'Evaluate', 'Create')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exam_questions_diff",
                table: "exam_questions",
                sql: "difficulty IN ('easy', 'medium', 'hard')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_submissions_grading_status",
                table: "exam_question_submissions",
                sql: "grading_status IN ('pending', 'transcribing', 'transcribed', 'ai_graded', 'audited', 'failed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dlq_status",
                table: "dead_letter_queues",
                sql: "status IN ('pending', 'resolved', 'abandoned')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_courses_credits",
                table: "courses",
                sql: "credits > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_courses_input_mode",
                table: "courses",
                sql: "exam_input_mode IN ('VoiceOnly', 'VoiceWithTranscriptEdit')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_courses_max_follow_up",
                table: "courses",
                sql: "max_follow_up_questions >= 1 AND max_follow_up_questions <= 5");

            migrationBuilder.AddCheckConstraint(
                name: "ck_courses_mock_quota",
                table: "courses",
                sql: "max_mock_exams_per_day >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_courses_transcript_buffer",
                table: "courses",
                sql: "transcript_buffer_seconds >= 10 AND transcript_buffer_seconds <= 300");

            migrationBuilder.AddCheckConstraint(
                name: "ck_classes_max_students",
                table: "classes",
                sql: "max_students > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_class_enrollment_status",
                table: "class_enrollments",
                sql: "status IN ('enrolled', 'dropped', 'completed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_appeal_requests_scores",
                table: "appeal_requests",
                sql: "(original_score IS NULL OR (original_score >= 0 AND original_score <= 10.00)) AND (proposed_score IS NULL OR (proposed_score >= 0 AND proposed_score <= 10.00))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_appeal_requests_status",
                table: "appeal_requests",
                sql: "status IN ('PENDING', 'IN_REVIEW', 'APPROVED', 'REJECTED', 'CANCELLED')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_evaluations_score",
                table: "ai_evaluations",
                sql: "total_score >= 0 AND total_score <= 10.00");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_evaluations_type",
                table: "ai_evaluations",
                sql: "answer_type IN ('practice', 'exam')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_eval_detail_score",
                table: "ai_evaluation_details",
                sql: "score >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_role",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tickets_ack_status",
                table: "student_exam_tickets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tickets_seat",
                table: "student_exam_tickets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tickets_status",
                table: "student_exam_tickets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_semesters_dates",
                table: "semesters");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rubrics_total_score",
                table: "rubrics");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rubric_criteria_score",
                table: "rubric_criteria");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rubric_criteria_weight",
                table: "rubric_criteria");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shifts_status",
                table: "real_exam_session_shifts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shifts_time",
                table: "real_exam_session_shifts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_practice_sessions_mode",
                table: "practice_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_practice_sessions_status",
                table: "practice_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_practice_questions_bloom",
                table: "practice_questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_practice_questions_diff",
                table: "practice_questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_practice_answers_status",
                table: "practice_answers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_official_sessions_input_mode",
                table: "official_exam_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_official_sessions_max_follow_up",
                table: "official_exam_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_official_sessions_status",
                table: "official_exam_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_official_sessions_transcript_buffer",
                table: "official_exam_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_mock_sessions_status",
                table: "mock_exam_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_mock_exam_quotas_count",
                table: "mock_exam_quotas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_mock_answers_status",
                table: "mock_exam_answers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_lecturer_audits_scores",
                table: "lecturer_audits");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_details_scores",
                table: "lecturer_audit_details");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exam_struct_duration",
                table: "exam_structures");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exam_struct_questions",
                table: "exam_structures");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exam_questions_bloom",
                table: "exam_questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exam_questions_diff",
                table: "exam_questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_submissions_grading_status",
                table: "exam_question_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_dlq_status",
                table: "dead_letter_queues");

            migrationBuilder.DropCheckConstraint(
                name: "ck_courses_credits",
                table: "courses");

            migrationBuilder.DropCheckConstraint(
                name: "ck_courses_input_mode",
                table: "courses");

            migrationBuilder.DropCheckConstraint(
                name: "ck_courses_max_follow_up",
                table: "courses");

            migrationBuilder.DropCheckConstraint(
                name: "ck_courses_mock_quota",
                table: "courses");

            migrationBuilder.DropCheckConstraint(
                name: "ck_courses_transcript_buffer",
                table: "courses");

            migrationBuilder.DropCheckConstraint(
                name: "ck_classes_max_students",
                table: "classes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_class_enrollment_status",
                table: "class_enrollments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_appeal_requests_scores",
                table: "appeal_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_appeal_requests_status",
                table: "appeal_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ai_evaluations_score",
                table: "ai_evaluations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ai_evaluations_type",
                table: "ai_evaluations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ai_eval_detail_score",
                table: "ai_evaluation_details");

            migrationBuilder.DropColumn(
                name: "student_acknowledgement_status",
                table: "student_exam_tickets");
        }
    }
}
