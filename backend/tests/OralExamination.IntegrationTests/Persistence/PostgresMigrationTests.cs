using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class PostgresMigrationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task LegacyUpdateDbAlreadyApplied_UpgradeRepairsMissingSchema_AndPreservesData()
    {
        await using var database = await fixture.CreateDatabaseAsync(migrate: false);
        await using (var legacy = database.CreateContext())
        {
            await legacy.GetService<IMigrator>().MigrateAsync("20261108141117_UpdateDB");
        }

        await database.ExecuteAsync("CREATE TABLE upgrade_probe (value integer); INSERT INTO upgrade_probe VALUES (42)");
        await using (var current = database.CreateContext())
        {
            await current.Database.MigrateAsync();
            await current.Database.MigrateAsync();
            (await current.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        }

        (await database.ScalarAsync<int>("SELECT value FROM upgrade_probe")).Should().Be(42);
        (await database.ScalarAsync<long>("SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='classes' AND column_name='join_code'"))
            .Should().Be(1);
        (await database.ScalarAsync<long>("SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='courses' AND column_name='mock_exam_duration_minutes'"))
            .Should().Be(1);
    }

    [Fact]
    public async Task EmptyDatabase_AllProductionMigrationsApply_AndSecondApplyIsIdempotent()
    {
        await using var database = await fixture.CreateDatabaseAsync(migrate: false);
        await using var context = database.CreateContext();
        var expected = context.Database.GetMigrations().ToArray();
        expected.Should().NotBeEmpty();

        await context.Database.MigrateAsync();
        (await context.Database.GetAppliedMigrationsAsync()).Should().Equal(expected);
        await context.Database.MigrateAsync();

        (await context.Database.GetAppliedMigrationsAsync()).Should().Equal(expected);
        (await context.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await database.ScalarAsync<string>("SHOW server_version")).Should().StartWith("16.");
    }

    [Fact]
    public async Task SeparateTestDatabases_DoNotShareRecords()
    {
        await using var first = await fixture.CreateDatabaseAsync(migrate: false);
        await using var second = await fixture.CreateDatabaseAsync(migrate: false);
        await first.ExecuteAsync("CREATE TABLE isolation_probe (value integer); INSERT INTO isolation_probe VALUES (42)");

        (await first.ScalarAsync<long>("SELECT count(*) FROM isolation_probe")).Should().Be(1);
        (await second.ScalarAsync<long>("SELECT count(*) FROM information_schema.tables WHERE table_name = 'isolation_probe'"))
            .Should().Be(0);
    }

    [Fact]
    public async Task AppealWorkflowMigration_MapsLegacyStates_AndEnforcesNewConstraint()
    {
        await using var database = await fixture.CreateDatabaseAsync(migrate: false);
        await using (var legacy = database.CreateContext())
        {
            await legacy.GetService<IMigrator>().MigrateAsync("20261114030000_AddGradePublicationTime");
        }

        (await database.ScalarAsync<string>("SELECT status FROM appeal_requests LIMIT 1"))
            .Should().Be("PENDING");

        await using (var workflowMigrated = database.CreateContext())
        {
            await workflowMigrated.GetService<IMigrator>().MigrateAsync("20261114040000_EnforceAppealFiveStepWorkflow");
        }

        (await database.ScalarAsync<string>("SELECT status FROM appeal_requests LIMIT 1"))
            .Should().Be("SUBMITTED_TO_EXAM_OFFICE");

        await using (var current = database.CreateContext())
        {
            await current.Database.MigrateAsync();
        }

        (await database.ScalarAsync<int>("SELECT character_maximum_length FROM information_schema.columns WHERE table_name='appeal_requests' AND column_name='status'"))
            .Should().Be(40);

        Guid appealId;
        await using (var seedContext = database.CreateContext())
        {
            var official = await TestData.OfficialAsync(seedContext);
            var appeal = new Domain.Entities.AppealRequest
            {
                TicketId = official.Ticket.Id,
                StudentId = official.Academic.Student.Id,
                SessionId = official.Ticket.Shift.SessionId,
                Reason = "Migration constraint verification",
                Status = "SUBMITTED_TO_EXAM_OFFICE",
                AssignedTo = official.Academic.Lecturer.Id
            };
            seedContext.AppealRequests.Add(appeal);
            await seedContext.SaveChangesAsync();
            appealId = appeal.Id;
        }

        var invalidWrite = async () => await database.ExecuteAsync(
            $"UPDATE appeal_requests SET status='PENDING' WHERE id='{appealId}'");
        (await invalidWrite.Should().ThrowAsync<PostgresException>()).Which.SqlState
            .Should().Be(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task M1SchemaSeedAndIndexHardening_DeduplicatesAiEvaluationsAcrossAllFourFlows_AndCreatesUniqueIndexes()
    {
        await using var database = await fixture.CreateDatabaseAsync(migrate: false);
        await using (var pre = database.CreateContext())
        {
            await pre.GetService<IMigrator>().MigrateAsync("20261116153826_AddSemesterIdToOfficialExamSessions");
        }

        // Simulate legacy schema where uq_ai_evaluations_exam_submission did not yet exist so exam_submission_id dedup is also exercised
        await database.ExecuteAsync("DROP INDEX IF EXISTS uq_ai_evaluations_exam_submission;");

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var lecturerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var semesterId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();
        var criterionId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var practiceQuestionId = Guid.NewGuid();
        var practiceSessionId = Guid.NewGuid();
        var practiceAnswerId = Guid.NewGuid();
        var examStructureId = Guid.NewGuid();
        var examSetId = Guid.NewGuid();
        var mockSessionId = Guid.NewGuid();
        var mockAnswerId = Guid.NewGuid();
        var inClassShiftId = Guid.NewGuid();
        var inClassSubmissionId = Guid.NewGuid();
        var officialSessionId = Guid.NewGuid();
        var officialShiftId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var examQuestionId = Guid.NewGuid();
        var examSubmissionId = Guid.NewGuid();

        var baseTime = new DateTime(2026, 11, 17, 4, 0, 0, DateTimeKind.Utc);
        var paOlderId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var paTieSmallerId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var paTieWinnerId = Guid.Parse("10000000-0000-0000-0000-000000000003");
        var maOlderId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var maWinnerId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var icsOlderId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var icsWinnerId = Guid.Parse("30000000-0000-0000-0000-000000000002");
        var esOlderId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        var esWinnerId = Guid.Parse("40000000-0000-0000-0000-000000000002");

        await using (var conn = new NpgsqlConnection(database.ConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO users (id, email, full_name, role, is_active)
                VALUES (@lecturerId, @lecturerEmail, 'Dedup Lecturer', 'lecturer', true),
                       (@studentId, @studentEmail, 'Dedup Student', 'student', true);

                INSERT INTO semesters (id, code, name, start_date, end_date, is_active)
                VALUES (@semesterId, @semCode, 'Dedup Semester', DATE '2026-01-01', DATE '2026-12-31', true);

                INSERT INTO courses (id, code, name, semester_id, is_active)
                VALUES (@courseId, @courseCode, 'Dedup Course', @semesterId, true);

                INSERT INTO rubrics (id, course_id, name, total_max_score, is_active)
                VALUES (@rubricId, @courseId, 'Dedup Rubric', 10.00, true);

                INSERT INTO rubric_criteria (id, rubric_id, criterion_name, max_score, weight, order_index)
                VALUES (@criterionId, @rubricId, 'Criterion 1', 10.00, 1.00, 1);

                INSERT INTO classes (id, code, name, join_code, course_id, semester_id, lecturer_id, is_active)
                VALUES (@classId, @classCode, 'Dedup Class', @joinCode, @courseId, @semesterId, @lecturerId, true);

                INSERT INTO practice_questions (id, course_id, rubric_id, title, content, difficulty, bloom_level, is_active)
                VALUES (@practiceQuestionId, @courseId, @rubricId, 'Dedup Q', 'Explain deduplication.', 'medium', 'Understand', true);

                INSERT INTO practice_sessions (id, student_id, course_id, practice_mode, status)
                VALUES (@practiceSessionId, @studentId, @courseId, 'per_question', 'in_progress');

                INSERT INTO practice_answers (id, session_id, question_id, student_id, answer_text, order_index, status)
                VALUES (@practiceAnswerId, @practiceSessionId, @practiceQuestionId, @studentId, 'Practice ans', 1, 'graded');

                INSERT INTO exam_structures (id, course_id, created_by, name, total_questions, duration_minutes)
                VALUES (@examStructureId, @courseId, @lecturerId, 'Dedup Struct', 1, 30);

                INSERT INTO exam_sets (id, structure_id, course_id, set_code, status)
                VALUES (@examSetId, @examStructureId, @courseId, @setCode, 'HANDED_OVER');

                INSERT INTO mock_exam_sessions (id, student_id, exam_set_id, course_id, expires_at, status)
                VALUES (@mockSessionId, @studentId, @examSetId, @courseId, @baseTime + INTERVAL '30 minutes', 'graded');

                INSERT INTO mock_exam_answers (id, session_id, question_id, order_index, status)
                VALUES (@mockAnswerId, @mockSessionId, @practiceQuestionId, 1, 'graded');

                INSERT INTO in_class_exam_shifts (id, class_id, lecturer_id, shift_name, start_time, end_time, totp_secret_key, status)
                VALUES (@inClassShiftId, @classId, @lecturerId, 'Dedup Shift', @baseTime, @baseTime + INTERVAL '1 hour', 'SECRET123', 'IN_PROGRESS');

                INSERT INTO in_class_exam_submissions (id, shift_id, student_id, question_id, transcript, status)
                VALUES (@inClassSubmissionId, @inClassShiftId, @studentId, @practiceQuestionId, 'In-class ans', 'AI_GRADED');

                INSERT INTO official_exam_sessions (id, course_id, exam_structure_id, title, exam_date, semester_id, status)
                VALUES (@officialSessionId, @courseId, @examStructureId, 'Dedup Official', DATE '2026-11-17', @semesterId, 'grading');

                INSERT INTO real_exam_session_shifts (id, session_id, shift_name, room_lab, start_time, end_time, status)
                VALUES (@officialShiftId, @officialSessionId, 'Shift 1', 'Lab 101', @baseTime, @baseTime + INTERVAL '1 hour', 'completed');

                INSERT INTO student_exam_tickets (id, shift_id, student_id, exam_set_id, seat_number, status)
                VALUES (@ticketId, @officialShiftId, @studentId, @examSetId, 1, 'AI_GRADED');

                INSERT INTO exam_questions (id, course_id, rubric_id, title, content, difficulty, bloom_level, is_active)
                VALUES (@examQuestionId, @courseId, @rubricId, 'Exam Q', 'Explain indexing.', 'medium', 'Apply', true);

                INSERT INTO exam_question_submissions (id, ticket_id, exam_question_id, grading_status)
                VALUES (@examSubmissionId, @ticketId, @examQuestionId, 'ai_graded');

                INSERT INTO ai_evaluations (id, answer_type, student_id, practice_answer_id, mock_exam_answer_id, in_class_submission_id, exam_submission_id, total_score, feedback, evaluated_at)
                VALUES
                    (@paOlderId,      'practice',  @studentId, @practiceAnswerId, NULL,          NULL,                 NULL,              4.00, 'pa-older',       @baseTime - INTERVAL '10 minutes'),
                    (@paTieSmallerId, 'practice',  @studentId, @practiceAnswerId, NULL,          NULL,                 NULL,              6.00, 'pa-tie-smaller', @baseTime),
                    (@paTieWinnerId,  'practice',  @studentId, @practiceAnswerId, NULL,          NULL,                 NULL,              9.00, 'pa-tie-winner',  @baseTime),
                    (@maOlderId,      'mock_exam', @studentId, NULL,              @mockAnswerId, NULL,                 NULL,              5.00, 'ma-older',       @baseTime - INTERVAL '5 minutes'),
                    (@maWinnerId,     'mock_exam', @studentId, NULL,              @mockAnswerId, NULL,                 NULL,              8.50, 'ma-winner',      @baseTime),
                    (@icsOlderId,     'in_class',  @studentId, NULL,              NULL,          @inClassSubmissionId, NULL,              4.50, 'ics-older',      @baseTime - INTERVAL '5 minutes'),
                    (@icsWinnerId,    'in_class',  @studentId, NULL,              NULL,          @inClassSubmissionId, NULL,              9.50, 'ics-winner',     @baseTime),
                    (@esOlderId,      'exam',      @studentId, NULL,              NULL,          NULL,                 @examSubmissionId, 3.00, 'es-older',       @baseTime - INTERVAL '5 minutes'),
                    (@esWinnerId,     'exam',      @studentId, NULL,              NULL,          NULL,                 @examSubmissionId, 8.00, 'es-winner',      @baseTime);

                INSERT INTO ai_evaluation_details (id, evaluation_id, criterion_id, score, comment)
                VALUES
                    (gen_random_uuid(), @paOlderId,      @criterionId, 4.00, 'detail-pa-older'),
                    (gen_random_uuid(), @paTieSmallerId, @criterionId, 6.00, 'detail-pa-tie-smaller'),
                    (gen_random_uuid(), @paTieWinnerId,  @criterionId, 9.00, 'detail-pa-winner'),
                    (gen_random_uuid(), @maOlderId,      @criterionId, 5.00, 'detail-ma-older'),
                    (gen_random_uuid(), @maWinnerId,     @criterionId, 8.50, 'detail-ma-winner'),
                    (gen_random_uuid(), @icsOlderId,     @criterionId, 4.50, 'detail-ics-older'),
                    (gen_random_uuid(), @icsWinnerId,    @criterionId, 9.50, 'detail-ics-winner'),
                    (gen_random_uuid(), @esOlderId,      @criterionId, 3.00, 'detail-es-older'),
                    (gen_random_uuid(), @esWinnerId,     @criterionId, 8.00, 'detail-es-winner');
                """;
            cmd.Parameters.AddWithValue("lecturerId", lecturerId);
            cmd.Parameters.AddWithValue("lecturerEmail", $"lect-{suffix}@fpt.edu.vn");
            cmd.Parameters.AddWithValue("studentId", studentId);
            cmd.Parameters.AddWithValue("studentEmail", $"stud-{suffix}@fpt.edu.vn");
            cmd.Parameters.AddWithValue("semesterId", semesterId);
            cmd.Parameters.AddWithValue("semCode", $"SM{suffix[..6]}");
            cmd.Parameters.AddWithValue("courseId", courseId);
            cmd.Parameters.AddWithValue("courseCode", $"CS{suffix[..6]}");
            cmd.Parameters.AddWithValue("rubricId", rubricId);
            cmd.Parameters.AddWithValue("criterionId", criterionId);
            cmd.Parameters.AddWithValue("classId", classId);
            cmd.Parameters.AddWithValue("classCode", $"CL-{suffix}");
            cmd.Parameters.AddWithValue("joinCode", $"JN{suffix[..6]}");
            cmd.Parameters.AddWithValue("practiceQuestionId", practiceQuestionId);
            cmd.Parameters.AddWithValue("practiceSessionId", practiceSessionId);
            cmd.Parameters.AddWithValue("practiceAnswerId", practiceAnswerId);
            cmd.Parameters.AddWithValue("examStructureId", examStructureId);
            cmd.Parameters.AddWithValue("examSetId", examSetId);
            cmd.Parameters.AddWithValue("setCode", $"SET-{suffix}");
            cmd.Parameters.AddWithValue("mockSessionId", mockSessionId);
            cmd.Parameters.AddWithValue("mockAnswerId", mockAnswerId);
            cmd.Parameters.AddWithValue("inClassShiftId", inClassShiftId);
            cmd.Parameters.AddWithValue("inClassSubmissionId", inClassSubmissionId);
            cmd.Parameters.AddWithValue("officialSessionId", officialSessionId);
            cmd.Parameters.AddWithValue("officialShiftId", officialShiftId);
            cmd.Parameters.AddWithValue("ticketId", ticketId);
            cmd.Parameters.AddWithValue("examQuestionId", examQuestionId);
            cmd.Parameters.AddWithValue("examSubmissionId", examSubmissionId);
            cmd.Parameters.AddWithValue("baseTime", baseTime);
            cmd.Parameters.AddWithValue("paOlderId", paOlderId);
            cmd.Parameters.AddWithValue("paTieSmallerId", paTieSmallerId);
            cmd.Parameters.AddWithValue("paTieWinnerId", paTieWinnerId);
            cmd.Parameters.AddWithValue("maOlderId", maOlderId);
            cmd.Parameters.AddWithValue("maWinnerId", maWinnerId);
            cmd.Parameters.AddWithValue("icsOlderId", icsOlderId);
            cmd.Parameters.AddWithValue("icsWinnerId", icsWinnerId);
            cmd.Parameters.AddWithValue("esOlderId", esOlderId);
            cmd.Parameters.AddWithValue("esWinnerId", esWinnerId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Migrate to latest twice to verify deduplication + idempotency
        await using (var current = database.CreateContext())
        {
            await current.Database.MigrateAsync();
            await current.Database.MigrateAsync();
            (await current.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        }

        await using var verify = database.CreateContext();
        var surviving = await verify.AiEvaluations
            .AsNoTracking()
            .Where(e => e.StudentId == studentId)
            .OrderBy(e => e.AnswerType)
            .ToListAsync();

        surviving.Should().HaveCount(4);
        surviving.Single(e => e.PracticeAnswerId == practiceAnswerId).Should().Match<Domain.Entities.AiEvaluation>(e =>
            e.Id == paTieWinnerId && e.TotalScore == 9.00m && e.Feedback == "pa-tie-winner");
        surviving.Single(e => e.MockExamAnswerId == mockAnswerId).Should().Match<Domain.Entities.AiEvaluation>(e =>
            e.Id == maWinnerId && e.TotalScore == 8.50m && e.Feedback == "ma-winner");
        surviving.Single(e => e.InClassSubmissionId == inClassSubmissionId).Should().Match<Domain.Entities.AiEvaluation>(e =>
            e.Id == icsWinnerId && e.TotalScore == 9.50m && e.Feedback == "ics-winner");
        surviving.Single(e => e.ExamSubmissionId == examSubmissionId).Should().Match<Domain.Entities.AiEvaluation>(e =>
            e.Id == esWinnerId && e.TotalScore == 8.00m && e.Feedback == "es-winner");

        var survivingIds = surviving.Select(e => e.Id).ToArray();
        var allDetailsForStudent = await verify.AiEvaluationDetails
            .AsNoTracking()
            .Where(d => d.CriterionId == criterionId)
            .ToListAsync();
        allDetailsForStudent.Should().HaveCount(4);
        allDetailsForStudent.Select(d => d.EvaluationId).Should().BeEquivalentTo(survivingIds);

        var indexDefs = new Dictionary<string, string>();
        foreach (var indexName in new[]
        {
            "uq_ai_evaluations_practice_answer",
            "uq_ai_evaluations_mock_exam_answer",
            "uq_ai_evaluations_in_class_submission",
            "uq_ai_evaluations_exam_submission"
        })
        {
            var indexDef = await database.ScalarAsync<string>(
                $"SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'ai_evaluations' AND indexname = '{indexName}'");
            indexDefs[indexName] = indexDef;
            indexDef.Should().Contain("CREATE UNIQUE INDEX");
            indexDef.Should().Contain("IS NOT NULL");
        }
    }
}
