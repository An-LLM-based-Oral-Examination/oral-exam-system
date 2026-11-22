using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Persistence;

/// <summary>
/// Unit test suite kiểm chứng tính toàn diện (Coverage & Completeness) của PostgresSchemaReconciler
/// đảm bảo tự phục hồi 100% cho toàn bộ 41 thực thể / 38 bảng CSDL PostgreSQL:
/// - Toàn bộ 41 bảng vật lý đều có lệnh CREATE TABLE IF NOT EXISTS đầy đủ.
/// - Các cột trọng yếu (crucial columns) được bổ sung cả trong CREATE TABLE lẫn ALTER TABLE ... ADD COLUMN IF NOT EXISTS.
/// - Các check constraints trọng yếu (ck_ai_evaluations_type, ck_ai_evaluations_xor) đã bao gồm giá trị 'in_class'.
/// - Check constraint ck_users_role bao gồm đầy đủ 6 roles RBAC.
/// - Cơ chế subtransaction PL/pgSQL bảo vệ 100% foreign key và constraint khỏi lỗi abort giao dịch.
/// </summary>
public class PostgresSchemaReconcilerCoverageTests
{
    private static readonly string[] ExpectedAll41Tables =
    [
        "academic_programs",
        "ai_evaluation_details",
        "ai_evaluations",
        "appeal_requests",
        "audit_logs",
        "class_enrollments",
        "classes",
        "course_clos",
        "course_lecturers",
        "course_offerings",
        "courses",
        "dead_letter_queues",
        "exam_anti_cheat_logs",
        "exam_question_submissions",
        "exam_questions",
        "exam_set_questions",
        "exam_sets",
        "exam_structures",
        "grading_jobs",
        "in_class_exam_shifts",
        "in_class_exam_submissions",
        "in_class_shift_participants",
        "in_class_shift_passwords",
        "lecturer_audit_details",
        "lecturer_audits",
        "mock_exam_answers",
        "mock_exam_quotas",
        "mock_exam_sessions",
        "notifications",
        "official_exam_candidates",
        "official_exam_sessions",
        "practice_answers",
        "practice_questions",
        "practice_sessions",
        "real_exam_session_shifts",
        "refresh_tokens",
        "rubric_criteria",
        "rubrics",
        "semesters",
        "student_exam_tickets",
        "system_configs",
        "users"
    ];

    [Fact(DisplayName = "PostgresSchemaReconciler: GetPostMigrationSql chứa CREATE TABLE IF NOT EXISTS cho toàn bộ 41 bảng CSDL")]
    public void GetPostMigrationSql_Should_Contain_CreateTableIfNotExists_ForAll41Entities()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();
        sql.Should().NotBeNullOrWhiteSpace();

        var createdTables = Regex.Matches(sql, @"CREATE\s+TABLE\s+IF\s+NOT\s+EXISTS\s+([a-z0-9_]+)", RegexOptions.IgnoreCase)
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .Distinct()
            .ToHashSet();

        createdTables.Should().HaveCountGreaterOrEqualTo(41);

        foreach (var expectedTable in ExpectedAll41Tables)
        {
            createdTables.Should().Contain(
                expectedTable,
                because: $"Bảng '{expectedTable}' bắt buộc phải được khởi tạo an toàn bằng CREATE TABLE IF NOT EXISTS trong Reconciler");
        }
    }

    [Fact(DisplayName = "PostgresSchemaReconciler: Bảng ai_evaluations có đầy đủ student_id, in_class_submission_id, follow_up_prompt")]
    public void GetPostMigrationSql_Should_Contain_CrucialColumns_For_AiEvaluations()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        // 1. Kiểm tra trong CREATE TABLE ai_evaluations
        var createTableMatch = Regex.Match(
            sql,
            @"CREATE\s+TABLE\s+IF\s+NOT\s+EXISTS\s+ai_evaluations\s*\((.*?)\);",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        createTableMatch.Success.Should().BeTrue("Khối CREATE TABLE ai_evaluations phải tồn tại");
        var ddlContent = createTableMatch.Groups[1].Value;

        ddlContent.Should().Contain("student_id", "ai_evaluations phải có cột student_id");
        ddlContent.Should().Contain("in_class_submission_id", "ai_evaluations phải có cột in_class_submission_id cho MF-05");
        ddlContent.Should().Contain("follow_up_prompt", "ai_evaluations phải có cột follow_up_prompt");

        // 2. Kiểm tra trong ALTER TABLE ai_evaluations ADD COLUMN IF NOT EXISTS (tự bù đắp cho DB đã tồn tại)
        sql.Should().MatchRegex(
            @"ALTER\s+TABLE\s+ai_evaluations\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+student_id\s+uuid",
            "Phải có lệnh bù đắp cột student_id trên bảng ai_evaluations cũ");

        sql.Should().MatchRegex(
            @"ALTER\s+TABLE\s+ai_evaluations\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+in_class_submission_id\s+uuid",
            "Phải có lệnh bù đắp cột in_class_submission_id trên bảng ai_evaluations cũ");

        sql.Should().MatchRegex(
            @"ALTER\s+TABLE\s+ai_evaluations\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+follow_up_prompt\s+text",
            "Phải có lệnh bù đắp cột follow_up_prompt trên bảng ai_evaluations cũ");
    }

    [Fact(DisplayName = "PostgresSchemaReconciler: Bảng practice_sessions & practice_answers có đầy đủ selected_question_ids, total_score, order_index, prompt_text")]
    public void GetPostMigrationSql_Should_Contain_CrucialColumns_For_PracticeSessionsAndAnswers()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        // practice_sessions: selected_question_ids (jsonb NOT NULL DEFAULT '[]'::jsonb), total_score
        sql.Should().MatchRegex(
            @"ALTER\s+TABLE\s+practice_sessions\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+selected_question_ids\s+jsonb",
            "Phải bù cột selected_question_ids cho practice_sessions");
        sql.Should().MatchRegex(
            @"ALTER\s+TABLE\s+practice_sessions\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+total_score",
            "Phải bù cột total_score cho practice_sessions");

        // practice_answers: order_index, prompt_text
        sql.Should().MatchRegex(
            @"ALTER\s+TABLE\s+practice_answers\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+order_index\s+integer",
            "Phải bù cột order_index cho practice_answers");
        sql.Should().MatchRegex(
            @"ALTER\s+TABLE\s+practice_answers\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+prompt_text\s+text",
            "Phải bù cột prompt_text cho practice_answers");
    }

    [Fact(DisplayName = "PostgresSchemaReconciler: Bảng mock_exam_answers có đầy đủ initial_voice_transcript, is_follow_up, parent_answer_id, order_index")]
    public void GetPostMigrationSql_Should_Contain_CrucialColumns_For_MockExamAnswers()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        sql.Should().MatchRegex(
            @"ALTER\s+TABLE\s+mock_exam_answers\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+initial_voice_transcript\s+text",
            "Phải bù cột initial_voice_transcript cho mock_exam_answers");

        sql.Should().MatchRegex(
            @"ALTER\s+TABLE\s+mock_exam_answers\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+is_follow_up\s+boolean",
            "Phải bù cột is_follow_up cho mock_exam_answers");

        sql.Should().MatchRegex(
            @"ALTER\s+TABLE\s+mock_exam_answers\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+parent_answer_id\s+uuid",
            "Phải bù cột parent_answer_id cho mock_exam_answers");

        sql.Should().MatchRegex(
            @"ALTER\s+TABLE\s+mock_exam_answers\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+prompt_text\s+text",
            "Phải bù cột prompt_text cho mock_exam_answers");

        sql.Should().MatchRegex(
            @"ALTER\s+TABLE\s+mock_exam_answers\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+order_index\s+integer",
            "Phải bù cột order_index cho mock_exam_answers");
    }

    [Fact(DisplayName = "PostgresSchemaReconciler: Bảng exam_sets, exam_structures và lecturer_audit_details có đủ các cột R1-R8")]
    public void GetPostMigrationSql_Should_Contain_CrucialColumns_For_ExamSetsAndStructures()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        // exam_sets
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_sets\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+session_id\s+uuid");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_sets\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+created_by\s+uuid");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_sets\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+handed_over_at");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_sets\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+approved_at");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_sets\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+approved_by\s+uuid");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_sets\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+submitted_at");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_sets\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+status");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_sets\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+review_notes");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_sets\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+is_sample_set\s+boolean");

        // exam_structures
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_structures\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+slot_definitions\s+jsonb");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_structures\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+required_set_count\s+integer");

        // lecturer_audit_details
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+lecturer_audit_details\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+submission_id\s+uuid");
    }

    [Fact(DisplayName = "PostgresSchemaReconciler: Bảng classes có đầy đủ join_password_hash, join_code và không còn join_password_plain")]
    public void GetPostMigrationSql_Should_Contain_CrucialColumns_For_Classes()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        sql.Should().NotContain("join_password_plain");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+classes\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+join_password_hash\s+character\s+varying");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+classes\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+join_code\s+character\s+varying");
    }

    [Fact(DisplayName = "PostgresSchemaReconciler: Bảng practice_answers, in_class_exam_submissions, appeal_requests và ai_evaluations đồng bộ AZ-B07/AZ-B10")]
    public void GetPostMigrationSql_Should_Contain_ProcessingStartedAt_AppealVarchar40_And_AiEvalPartialUniqueIndexes()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+practice_answers\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+processing_started_at\s+timestamp\s+with\s+time\s+zone");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+in_class_exam_submissions\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+processing_started_at\s+timestamp\s+with\s+time\s+zone");
        sql.Should().Contain("status character varying(40) NOT NULL");
        sql.Should().Contain("uq_ai_evaluations_practice_answer");
        sql.Should().Contain("uq_ai_evaluations_mock_exam_answer");
        sql.Should().Contain("uq_ai_evaluations_in_class_submission");
    }

    [Fact(DisplayName = "PostgresSchemaReconciler: Ràng buộc ck_ai_evaluations_type bao gồm giá trị 'in_class' cho MF-05")]
    public void GetPostMigrationSql_Should_Contain_CheckConstraint_AiEvaluationsType_With_InClass()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        sql.Should().Contain("ck_ai_evaluations_type", "Ràng buộc ck_ai_evaluations_type phải được khai báo");
        sql.Should().MatchRegex(
            @"CHECK\s*\(\s*answer_type\s+IN\s*\(\s*'practice'\s*,\s*'mock_exam'\s*,\s*'exam'\s*,\s*'in_class'\s*\)\s*\)",
            "ck_ai_evaluations_type phải chấp nhận 'practice', 'mock_exam', 'exam', và 'in_class'");
    }

    [Fact(DisplayName = "PostgresSchemaReconciler: Ràng buộc ck_ai_evaluations_xor xử lý đúng XOR cho 'in_class' và in_class_submission_id")]
    public void GetPostMigrationSql_Should_Contain_CheckConstraint_AiEvaluationsXor_With_InClass()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        sql.Should().Contain("ck_ai_evaluations_xor", "Ràng buộc ck_ai_evaluations_xor phải được khai báo");
        sql.Should().Contain("in_class_submission_id IS NOT NULL", "ck_ai_evaluations_xor phải yêu cầu in_class_submission_id khi answer_type = 'in_class'");
        sql.Should().Contain("answer_type = 'in_class'", "ck_ai_evaluations_xor phải có nhánh cho in_class");
    }

    [Fact(DisplayName = "PostgresSchemaReconciler: Ràng buộc ck_users_role chứa đầy đủ 6 roles RBAC")]
    public void GetPostMigrationSql_Should_Contain_CheckConstraint_UsersRole_With_AllSixRoles()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        var expectedRoles = new[] { "admin", "department_head", "examination_officer", "lecturer", "proctor", "student" };
        sql.Should().Contain("ck_users_role");

        foreach (var role in expectedRoles)
        {
            sql.Should().Contain($"'{role}'", because: $"Role '{role}' bắt buộc phải nằm trong check constraint ck_users_role");
        }
    }

    [Fact(DisplayName = "PostgresSchemaReconciler: PreMigrationSql được bọc an toàn trong DO $reconcile_pre$ và subtransactions")]
    public void GetPreMigrationSql_Should_BeIdempotent_And_ProtectConstraintsWithSubtransactions()
    {
        var sql = PostgresSchemaReconciler.GetPreMigrationSql();

        sql.Should().StartWith("DO $reconcile_pre$");
        sql.Should().Contain("IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'users')");
        sql.Should().Contain("BEGIN", "Các khối Foreign Key phải được bọc trong subtransaction");
        sql.Should().Contain("EXCEPTION WHEN OTHERS THEN NULL;", "Lỗi constraint trên bảng mồ côi phải được swallow an toàn");
    }

    [Fact(DisplayName = "PostgresSchemaReconciler: PostMigrationSql đồng bộ bảng __EFMigrationsHistory với ON CONFLICT DO NOTHING")]
    public void GetPostMigrationSql_Should_Sync_EfMigrationsHistory_WithAllKnownMigrations()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        sql.Should().Contain("INSERT INTO \"__EFMigrationsHistory\"");
        sql.Should().Contain("ON CONFLICT (\"MigrationId\") DO NOTHING");
        sql.Should().Contain("20261022125559_InitialDB");
        sql.Should().Contain("20261108141117_UpdateDB");
    }

    [Fact(DisplayName = "PostgresSchemaReconciler: GetPostMigrationSql phải có đúng 2 thẻ $reconcile_post$ và không lặp END")]
    public void GetPostMigrationSql_Should_HaveMatchingDollarQuotes_AndNoDuplicateEndTags()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();
        var dollarTags = Regex.Matches(sql, @"\$reconcile_post\$");
        dollarTags.Should().HaveCount(2, "Khối DO $reconcile_post$ chỉ được có đúng 1 thẻ mở và 1 thẻ đóng");

        var duplicateEndMatch = Regex.Match(sql, @"END\s+\$reconcile_post\$;\s+END\s+\$reconcile_post\$;");
        duplicateEndMatch.Success.Should().BeFalse("Không được xuất hiện duplicate 'END $reconcile_post$;'");
    }

    [Fact(DisplayName = "PostgresSchemaReconciler: Phải chuẩn hóa ai_evaluations trước khi tạo ràng buộc ck_ai_evaluations_xor")]
    public void GetPostMigrationSql_Should_SanitizeAiEvaluationsBeforeAddingXorConstraint()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();
        sql.Should().Contain("UPDATE ai_evaluations SET answer_type = ''mock_exam''");

        var updateIndex = sql.IndexOf("UPDATE ai_evaluations SET answer_type = ''mock_exam''", StringComparison.Ordinal);
        var alterIndex = sql.IndexOf("ADD CONSTRAINT ck_ai_evaluations_xor", StringComparison.Ordinal);
        updateIndex.Should().BeLessThan(alterIndex, "Câu lệnh chuẩn hóa UPDATE phải nằm TRƯỚC câu lệnh ADD CONSTRAINT ck_ai_evaluations_xor");
    }

    [Fact(DisplayName = "PostgresSchemaReconciler: Phủ đầy đủ M1 + M2 MF-04 (official_exam_candidates, FE/RE, BYOD device binding, Follow-up 3NF, PAdES certificate, client_event_id)")]
    public void GetPostMigrationSql_Should_Contain_OfficialExamWave2_M1AndM2_SchemaInvariants()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        // M1: official_exam_candidates & official_exam_sessions.exam_attempt_type
        sql.Should().Contain("CREATE TABLE IF NOT EXISTS official_exam_candidates");
        sql.Should().Contain("uq_official_exam_candidates_session_student");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+official_exam_sessions\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+exam_attempt_type\s+character\s+varying\(10\)\s+NOT\s+NULL\s+DEFAULT\s+'FE'");
        sql.Should().Contain("ck_official_sessions_attempt_type");
        sql.Should().Contain("uq_official_exam_sessions_semester_course_attempt");
        sql.Should().Contain("ck_shifts_distinct_proctor_examiner");

        // M2: PAdES certificate columns on real_exam_session_shifts
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+real_exam_session_shifts\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+certificate_hash\s+character\s+varying\(64\)");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+real_exam_session_shifts\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+certificate_thumbprint\s+character\s+varying\(64\)");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+real_exam_session_shifts\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+certificate_signed_at");
        sql.Should().Contain("ck_shifts_certificate_hash_len");
        sql.Should().Contain("ix_real_exam_shifts_certificate_hash");

        // M2: BYOD device binding on student_exam_tickets (hashed token, full-shift partial unique)
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+student_exam_tickets\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+device_id\s+character\s+varying\(128\)");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+student_exam_tickets\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+device_session_token_hash\s+character\s+varying\(128\)");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+student_exam_tickets\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+device_unlock_count\s+integer\s+NOT\s+NULL\s+DEFAULT\s+0");
        sql.Should().Contain("uq_tickets_shift_device");
        sql.Should().Contain("ck_tickets_device_binding_consistency");
        sql.Should().Contain("ck_tickets_device_unlock_count");

        // M2: Offline anti-cheat client_event_id
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_anti_cheat_logs\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+client_event_id\s+character\s+varying\(128\)");
        sql.Should().Contain("uq_exam_anti_cheat_logs_ticket_client_event");

        // M2: Follow-up 3NF & edit cap on exam_question_submissions
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_question_submissions\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+is_follow_up\s+boolean\s+NOT\s+NULL\s+DEFAULT\s+FALSE");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_question_submissions\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+follow_up_index\s+integer\s+NOT\s+NULL\s+DEFAULT\s+0");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_question_submissions\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+initial_voice_transcript\s+text");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_question_submissions\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+transcript_edit_ratio\s+numeric\(5,4\)");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_question_submissions\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+audio_object_key\s+character\s+varying\(512\)");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_question_submissions\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+generated_follow_up_prompt\s+text");
        sql.Should().Contain("DROP INDEX IF EXISTS uq_ticket_question");
        sql.Should().Contain("uq_exam_submissions_ticket_question_turn");
        sql.Should().Contain("uq_exam_submissions_ticket_parent");
        sql.Should().Contain("ck_exam_question_submissions_follow_up_consistency");
        sql.Should().Contain("ck_exam_question_submissions_transcript_edit_ratio");

        // practice_questions: approved_at & is_deleted
        var createPracticeQuestionsMatch = Regex.Match(
            sql,
            @"CREATE\s+TABLE\s+IF\s+NOT\s+EXISTS\s+practice_questions\s*\((.*?)\);",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        createPracticeQuestionsMatch.Success.Should().BeTrue();
        createPracticeQuestionsMatch.Groups[1].Value.Should().Contain("is_deleted boolean NOT NULL DEFAULT FALSE");
        createPracticeQuestionsMatch.Groups[1].Value.Should().Contain("approved_at timestamp with time zone NULL");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+practice_questions\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+is_deleted\s+boolean\s+NOT\s+NULL\s+DEFAULT\s+FALSE");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+practice_questions\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+approved_at\s+timestamp\s+with\s+time\s+zone");

        // M3: MF-03 ExamQuestion bank curation & pg_trgm partial GIN indexes + exam_sets ESR index
        sql.Should().Contain("CREATE EXTENSION IF NOT EXISTS pg_trgm");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_questions\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+bank_curation_status\s+character\s+varying\(30\)\s+NOT\s+NULL\s+DEFAULT\s+'BANK_READY'");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_questions\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+curated_by\s+uuid");
        sql.Should().MatchRegex(@"ALTER\s+TABLE\s+exam_questions\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+curated_at");
        sql.Should().Contain("ck_exam_questions_bank_curation_status");
        sql.Should().Contain("FK_exam_questions_users_curated_by");
        sql.Should().Contain("ix_exam_questions_course_curation_pool");
        sql.Should().Contain("ix_exam_questions_content_trgm");
        sql.Should().Contain("ix_exam_questions_title_trgm");
        sql.Should().Contain("ix_exam_sets_structure_status_active");
    }
}
