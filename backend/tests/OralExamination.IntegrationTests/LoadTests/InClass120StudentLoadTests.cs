using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Common.Security;
using OralExamination.Application.Features.InClassShifts;
using OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.SubmitInClassShiftAnswer;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Persistence.Interceptors;
using OralExamination.IntegrationTests.Support;
using Xunit;
using Xunit.Abstractions;

namespace OralExamination.IntegrationTests.LoadTests;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class InClass120StudentLoadTests(PostgresFixture fixture, ITestOutputHelper output)
{
    private const int TotalStudentCount = 120;
    private const int ClassCount = 3;
    private const int StudentsPerClass = 40;
    private const int QuestionCount = 15;

    [Fact]
    public async Task Simulate120Students_Across3Classes_JoinAndSubmitConcurrently()
    {
        // ----------------------------------------------------------------------
        // Scaffolding: Setup PostgreSQL 16 container database & seed data
        // ----------------------------------------------------------------------
        await using var database = await fixture.CreateDatabaseAsync();
        var pooledConnectionString = CreatePooledConnectionString(database.ConnectionString);

        var seedData = await SeedLoadTestEnvironmentAsync(database);
        var recordingQueue = new RecordingQueue();

        Log("================================================================================");
        Log("FA26SE166 — MF-05 In-Class 120-Student Concurrent Load Testing Harness");
        Log("================================================================================");
        Log($"Total Students : {TotalStudentCount}");
        Log($"Total Classes  : {ClassCount} ({StudentsPerClass} students/class)");
        Log($"Total Shifts   : {seedData.Shifts.Count} ({StudentsPerClass} students/shift)");
        Log($"Question Pool  : {QuestionCount} questions (5 Easy, 5 Medium, 5 Hard)");
        Log("--------------------------------------------------------------------------------");

        using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        // This benchmark measures steady-state throughput, not the first request's EF/JIT startup.
        // Exercise the real insert/commit/enqueue path on a separate shift and report its cold cost.
        // Do not use an idempotent resubmit as warmup: it skips the mutation being measured.
        await WarmUpRequestPathsAsync(database, pooledConnectionString, seedData.StudentShiftPairs[0], testCts.Token);

        // ----------------------------------------------------------------------
        // Flow 1: 120 Concurrent Joins (40 students per shift across 3 shifts)
        // ----------------------------------------------------------------------
        var joinReadySignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allJoinsReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyJoinCount = 0;
        var joinLatencies = new ConcurrentBag<double>();
        var studentAssignments = new ConcurrentDictionary<Guid, (Guid ShiftId, Guid AssignedQuestionId)>();

        var joinTasks = seedData.StudentShiftPairs.Select(pair => Task.Run(async () =>
        {
            await using var context = CreateContext(pooledConnectionString);
            var handler = new EnterInClassShiftCommandHandler(
                context,
                new EnterInClassShiftCommandValidator(),
                recordingQueue);

            if (Interlocked.Increment(ref readyJoinCount) == TotalStudentCount)
                allJoinsReady.SetResult();
            await joinReadySignal.Task.WaitAsync(testCts.Token);

            var stopwatch = Stopwatch.StartNew();
            var result = await handler.Handle(
                new EnterInClassShiftCommand(pair.Student.Id, pair.Shift.Id, pair.OtpCode),
                testCts.Token);
            stopwatch.Stop();

            joinLatencies.Add(stopwatch.Elapsed.TotalMilliseconds);

            if (result.IsSuccess && result.Value.AssignedQuestionId.HasValue)
            {
                studentAssignments.TryAdd(pair.Student.Id, (pair.Shift.Id, result.Value.AssignedQuestionId.Value));
            }

            return result;
        })).ToArray();

        // Release all 120 join requests concurrently (thundering herd)
        await allJoinsReady.Task.WaitAsync(testCts.Token);
        joinReadySignal.SetResult();
        var joinResults = await Task.WhenAll(joinTasks);

        // Verify Join Success
        joinResults.Should().OnlyContain(r => r.IsSuccess,
            "all 120 students must successfully join without error or HTTP 5xx equivalent");
        studentAssignments.Should().HaveCount(TotalStudentCount,
            "each of the 120 students must receive an assigned question");

        var joinMetrics = LatencyMetrics.FromSamples(joinLatencies);
        Log($"JOIN METRICS (120 Concurrent Joins):");
        Log($"  Min Latency : {joinMetrics.MinMs:F1} ms");
        Log($"  Max Latency : {joinMetrics.MaxMs:F1} ms");
        Log($"  P50 Latency : {joinMetrics.P50Ms:F1} ms");
        Log($"  P95 Latency : {joinMetrics.P95Ms:F1} ms");
        Log($"  Avg Latency : {joinMetrics.AvgMs:F1} ms");
        Log("--------------------------------------------------------------------------------");

        // Verify Anti-Collision Question Allocation per Shift
        await using var verifyContext = database.CreateContext();
        var participants = await verifyContext.InClassShiftParticipants
            .AsNoTracking()
            .Where(p => seedData.Shifts.Select(s => s.Id).Contains(p.ShiftId))
            .ToListAsync(testCts.Token);

        participants.Should().HaveCount(TotalStudentCount);

        Log("ANTI-COLLISION QUESTION ALLOCATION BALANCE:");
        foreach (var shift in seedData.Shifts)
        {
            var shiftParticipants = participants.Where(p => p.ShiftId == shift.Id).ToList();
            var questionUsage = seedData.QuestionIds.ToDictionary(
                qId => qId,
                qId => shiftParticipants.Count(p => p.AssignedQuestionId == qId));

            var minUsage = questionUsage.Values.Min();
            var maxUsage = questionUsage.Values.Max();
            var delta = maxUsage - minUsage;

            Log($"  Shift '{shift.ShiftName}' ({shiftParticipants.Count} SV, {QuestionCount} Qs): Min={minUsage}, Max={maxUsage}, Delta={delta}");

            delta.Should().BeLessThanOrEqualTo(2,
                $"Anti-Collision algorithm must maintain question allocation balance (max - min <= 2) in shift '{shift.ShiftName}'");
        }
        Log("--------------------------------------------------------------------------------");

        // ----------------------------------------------------------------------
        // Flow 2: 120 concurrent new submissions; steady-state p95 remains below 500ms.
        // ----------------------------------------------------------------------
        var submitReadySignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allSubmitsReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readySubmitCount = 0;
        var submitLatencies = new ConcurrentBag<double>();

        var submitTasks = seedData.StudentShiftPairs.Select(pair => Task.Run(async () =>
        {
            var (shiftId, assignedQuestionId) = studentAssignments[pair.Student.Id];

            await using var context = CreateContext(pooledConnectionString);
            var handler = new SubmitInClassShiftAnswerCommandHandler(
                context,
                recordingQueue,
                new SubmitInClassShiftAnswerCommandValidator());

            var transcriptText = $"Sinh viên {pair.Student.FullName} nộp bài vấn đáp câu hỏi {assignedQuestionId:N} cho ca thi {shiftId:N}.";

            if (Interlocked.Increment(ref readySubmitCount) == TotalStudentCount)
                allSubmitsReady.SetResult();
            await submitReadySignal.Task.WaitAsync(testCts.Token);

            var stopwatch = Stopwatch.StartNew();
            var result = await handler.Handle(
                new SubmitInClassShiftAnswerCommand(pair.Student.Id, shiftId, assignedQuestionId, transcriptText),
                testCts.Token);
            stopwatch.Stop();

            submitLatencies.Add(stopwatch.Elapsed.TotalMilliseconds);
            return result;
        })).ToArray();

        // Release all 120 submit requests concurrently
        await allSubmitsReady.Task.WaitAsync(testCts.Token);
        submitReadySignal.SetResult();
        var submitResults = await Task.WhenAll(submitTasks);

        // Verify Submit Success
        submitResults.Should().OnlyContain(r => r.IsSuccess,
            "all 120 students must successfully submit their answers");

        var submitMetrics = LatencyMetrics.FromSamples(submitLatencies);
        submitLatencies.Should().HaveCount(TotalStudentCount, "every measured request must contribute a latency sample");
        Log($"SUBMIT METRICS (120 Concurrent Submissions, Steady State):");
        Log($"  Min Latency : {submitMetrics.MinMs:F1} ms");
        Log($"  Max Latency : {submitMetrics.MaxMs:F1} ms");
        Log($"  P50 Latency : {submitMetrics.P50Ms:F1} ms");
        Log($"  P95 Latency : {submitMetrics.P95Ms:F1} ms");
        Log($"  Avg Latency : {submitMetrics.AvgMs:F1} ms");
        Log("--------------------------------------------------------------------------------");

        // ----------------------------------------------------------------------
        // Verify 0 Lost Updates & Data Integrity
        // ----------------------------------------------------------------------
        await using var finalVerifyContext = database.CreateContext();

        var totalSubmissions = await finalVerifyContext.InClassExamSubmissions
            .AsNoTracking()
            .CountAsync(s => seedData.Shifts.Select(shift => shift.Id).Contains(s.ShiftId), testCts.Token);

        var submittedParticipants = await finalVerifyContext.InClassShiftParticipants
            .AsNoTracking()
            .CountAsync(p => seedData.Shifts.Select(shift => shift.Id).Contains(p.ShiftId) && p.Status == "SUBMITTED", testCts.Token);

        totalSubmissions.Should().Be(TotalStudentCount,
            "exactly 120 submissions must be persisted into InClassExamSubmissions");
        submittedParticipants.Should().Be(TotalStudentCount,
            "all 120 participants must transition to 'SUBMITTED' status with 0 lost updates");
        recordingQueue.Tasks.Should().HaveCount(TotalStudentCount,
            "exactly 120 grading tasks must be enqueued into the background queue");

        Log("DATA INTEGRITY & ZERO LOST UPDATES:");
        Log($"  Total Submissions Created : {totalSubmissions} / {TotalStudentCount}");
        Log($"  Participants 'SUBMITTED'  : {submittedParticipants} / {TotalStudentCount}");
        Log($"  Grading Tasks Enqueued    : {recordingQueue.Tasks.Count} / {TotalStudentCount}");
        Log("  0 Lost Updates            : VERIFIED");
        Log("================================================================================");

        // Verify committed state even when performance fails; never hide data checks behind the SLA.
        submitMetrics.P95Ms.Should().BeLessThan(500,
            "steady-state Persist-First Submit p95 must remain under 500ms for all 120 concurrent new submissions");
    }

    private async Task WarmUpRequestPathsAsync(TestDatabase database, string connectionString,
        StudentShiftPair pair, CancellationToken cancellationToken)
    {
        var secret = TotpPasswordHelper.GenerateSecretKey(32);
        var warmupShift = new InClassExamShift
        {
            ClassId = pair.Shift.ClassId,
            LecturerId = pair.Shift.LecturerId,
            ShiftName = "Warmup_NotMeasured",
            StartTime = DateTime.UtcNow.AddMinutes(-1),
            EndTime = DateTime.UtcNow.AddHours(2),
            Status = "IN_PROGRESS",
            QuestionPoolMode = InClassShiftPoolConfig.AllCourseBank,
            TotalQuestionsPerStudent = 1,
            TotpSecretKey = secret
        };
        await using (var seed = database.CreateContext())
        {
            seed.InClassExamShifts.Add(warmupShift);
            await seed.SaveChangesAsync(cancellationToken);
        }

        var warmupQueue = new RecordingQueue();
        Guid assignedQuestionId;
        await using (var context = CreateContext(connectionString))
        {
            var handler = new EnterInClassShiftCommandHandler(context,
                new EnterInClassShiftCommandValidator(), warmupQueue);
            var stopwatch = Stopwatch.StartNew();
            var result = await handler.Handle(new EnterInClassShiftCommand(pair.Student.Id,
                warmupShift.Id, TotpPasswordHelper.GenerateCode(secret)), cancellationToken);
            stopwatch.Stop();
            result.IsSuccess.Should().BeTrue("the cold join must succeed before measuring load");
            result.Value.AssignedQuestionId.Should().HaveValue();
            assignedQuestionId = result.Value.AssignedQuestionId!.Value;
            Log($"COLD JOIN (separate shift, not a load percentile): {stopwatch.Elapsed.TotalMilliseconds:F1} ms");
        }
        await using (var context = CreateContext(connectionString))
        {
            var handler = new SubmitInClassShiftAnswerCommandHandler(context, warmupQueue,
                new SubmitInClassShiftAnswerCommandValidator());
            var stopwatch = Stopwatch.StartNew();
            var result = await handler.Handle(new SubmitInClassShiftAnswerCommand(pair.Student.Id,
                warmupShift.Id, assignedQuestionId, "Warmup of the real new-submission persistence path."), cancellationToken);
            stopwatch.Stop();
            result.IsSuccess.Should().BeTrue("warmup must insert a new answer, not take an idempotent shortcut");
            Log($"COLD SUBMIT (separate shift, not a load percentile): {stopwatch.Elapsed.TotalMilliseconds:F1} ms");
        }
        await using var verify = database.CreateContext();
        (await verify.InClassExamSubmissions.AsNoTracking().CountAsync(
            s => s.ShiftId == warmupShift.Id && s.StudentId == pair.Student.Id, cancellationToken)).Should().Be(1);
        (await verify.InClassShiftParticipants.AsNoTracking().SingleAsync(
            p => p.ShiftId == warmupShift.Id && p.StudentId == pair.Student.Id, cancellationToken))
            .Status.Should().Be("SUBMITTED");
        warmupQueue.Tasks.Should().ContainSingle();
        warmupQueue.Tasks.Single().SessionId.Should().Be(warmupShift.Id);
    }

    private void Log(string message)
    {
        output.WriteLine(message);
        Console.WriteLine(message);
    }

    private static string CreatePooledConnectionString(string baseConnectionString)
    {
        return new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Pooling = true,
            MinPoolSize = 5,
            MaxPoolSize = 80,
            ConnectionIdleLifetime = 15,
            ConnectionPruningInterval = 5
        }.ConnectionString;
    }

    private static OralExamDbContext CreateContext(string connectionString)
    {
        return new OralExamDbContext(new DbContextOptionsBuilder<OralExamDbContext>()
            .UseNpgsql(connectionString, options => options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .AddInterceptors(new OneWayLockInterceptor())
            .Options);
    }

    private static async Task<SeededEnvironment> SeedLoadTestEnvironmentAsync(TestDatabase database)
    {
        await using var context = database.CreateContext();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var course = await context.Courses
            .Include(c => c.Semester)
            .FirstOrDefaultAsync(c => c.Code == "PRN231");

        if (course is null)
        {
            var semester = new Semester
            {
                Code = "FA26" + suffix,
                Name = "Fall 2026",
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 12, 31)
            };
            course = new Course
            {
                Code = "PRN231",
                Name = "Building Cross-Platform Applications",
                Semester = semester
            };
            context.Add(course);
            await context.SaveChangesAsync();
        }

        var lecturer = await context.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Lecturer);
        if (lecturer is null)
        {
            lecturer = new User
            {
                Email = $"lecturer_{suffix}@fpt.edu.vn",
                FullName = "Lecturer User",
                Role = UserRole.Lecturer
            };
            context.Users.Add(lecturer);
            await context.SaveChangesAsync();
        }

        var clo = await context.CourseClos.FirstOrDefaultAsync(c => c.CourseId == course.Id && c.IsActive);
        if (clo is null)
        {
            clo = new CourseClo
            {
                CourseId = course.Id,
                CloCode = "CLO1",
                Description = "Understand cross-platform architecture",
                BloomLevel = BloomLevel.Understand,
                OrderIndex = 1,
                IsActive = true
            };
            context.CourseClos.Add(clo);
            await context.SaveChangesAsync();
        }

        var rubric = await context.Rubrics.FirstOrDefaultAsync(r => r.CourseId == course.Id);
        if (rubric is null)
        {
            rubric = new Rubric
            {
                CourseId = course.Id,
                Name = "Standard InClass Rubric"
            };
            context.Rubrics.Add(rubric);
            await context.SaveChangesAsync();
        }

        // Clean up any default seeded practice questions for PRN231 in this isolated test database
        var existingQuestions = await context.PracticeQuestions
            .Where(q => q.CourseId == course.Id)
            .ToListAsync();
        if (existingQuestions.Count > 0)
        {
            context.PracticeQuestions.RemoveRange(existingQuestions);
            await context.SaveChangesAsync();
        }

        // 3 Classes
        var classes = new[]
        {
            new Class
            {
                Code = $"PRN231_SE1801_{suffix}",
                Name = "Class_A",
                JoinCode = $"JA_{suffix}",
                CourseId = course.Id,
                SemesterId = course.SemesterId,
                LecturerId = lecturer.Id,
                MaxStudents = 50
            },
            new Class
            {
                Code = $"PRN231_SE1802_{suffix}",
                Name = "Class_B",
                JoinCode = $"JB_{suffix}",
                CourseId = course.Id,
                SemesterId = course.SemesterId,
                LecturerId = lecturer.Id,
                MaxStudents = 50
            },
            new Class
            {
                Code = $"PRN231_SE1803_{suffix}",
                Name = "Class_C",
                JoinCode = $"JC_{suffix}",
                CourseId = course.Id,
                SemesterId = course.SemesterId,
                LecturerId = lecturer.Id,
                MaxStudents = 50
            }
        };
        context.AddRange(classes);

        // 3 InClassExamShifts
        var shiftsWithSecrets = classes.Select((c, idx) =>
        {
            var secret = TotpPasswordHelper.GenerateSecretKey(32);
            var shift = new InClassExamShift
            {
                ShiftName = $"Shift_{(char)('A' + idx)}",
                Class = c,
                LecturerId = lecturer.Id,
                StartTime = DateTime.UtcNow.AddMinutes(-10),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "IN_PROGRESS",
                QuestionPoolMode = InClassShiftPoolConfig.AllCourseBank,
                TotalQuestionsPerStudent = 1,
                TotpSecretKey = secret
            };
            var otp = TotpPasswordHelper.GenerateCode(secret);
            return (Shift: shift, Secret: secret, Otp: otp);
        }).ToArray();

        context.AddRange(shiftsWithSecrets.Select(s => s.Shift));

        // 15 PracticeQuestions (5 Easy, 5 Medium, 5 Hard)
        var questions = new List<PracticeQuestion>();
        for (int i = 1; i <= 5; i++)
        {
            questions.Add(new PracticeQuestion
            {
                CourseId = course.Id,
                CloId = clo.Id,
                RubricId = rubric.Id,
                Title = $"PRN231 Easy Question {i}",
                Content = $"Explain easy concept {i} in PRN231.",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Remember,
                ApprovalStatus = ApprovalStatus.Approved,
                IsActive = true
            });
        }
        for (int i = 1; i <= 5; i++)
        {
            questions.Add(new PracticeQuestion
            {
                CourseId = course.Id,
                CloId = clo.Id,
                RubricId = rubric.Id,
                Title = $"PRN231 Medium Question {i}",
                Content = $"Explain medium concept {i} in PRN231.",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Understand,
                ApprovalStatus = ApprovalStatus.Approved,
                IsActive = true
            });
        }
        for (int i = 1; i <= 5; i++)
        {
            questions.Add(new PracticeQuestion
            {
                CourseId = course.Id,
                CloId = clo.Id,
                RubricId = rubric.Id,
                Title = $"PRN231 Hard Question {i}",
                Content = $"Explain advanced architecture and hard concept {i} in PRN231.",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Apply,
                ApprovalStatus = ApprovalStatus.Approved,
                IsActive = true
            });
        }
        context.AddRange(questions);

        // 120 Students (40 enrolled per class)
        var studentPairs = new List<StudentShiftPair>(TotalStudentCount);
        for (int i = 1; i <= TotalStudentCount; i++)
        {
            var student = new User
            {
                Email = $"student_{suffix}_{i:D3}@fpt.edu.vn",
                FullName = $"Student {i:D3}",
                Username = $"student_{suffix}_{i:D3}",
                StudentCode = $"SE{180000 + i}",
                Role = UserRole.Student
            };
            context.Add(student);

            var classIndex = (i - 1) / StudentsPerClass; // 0, 1, or 2
            var targetClass = classes[classIndex];
            var shiftInfo = shiftsWithSecrets[classIndex];

            context.Add(new ClassEnrollment
            {
                Class = targetClass,
                Student = student,
                Status = "enrolled"
            });

            studentPairs.Add(new StudentShiftPair(student, shiftInfo.Shift, shiftInfo.Otp));
        }

        await context.SaveChangesAsync();

        return new SeededEnvironment(
            shiftsWithSecrets.Select(s => s.Shift).ToList(),
            studentPairs,
            questions.Select(q => q.Id).ToList()
        );
    }

    private sealed record StudentShiftPair(User Student, InClassExamShift Shift, string OtpCode);

    private sealed record SeededEnvironment(
        IReadOnlyList<InClassExamShift> Shifts,
        IReadOnlyList<StudentShiftPair> StudentShiftPairs,
        IReadOnlyList<Guid> QuestionIds);

    private sealed record LatencyMetrics(double MinMs, double MaxMs, double P50Ms, double P95Ms, double AvgMs)
    {
        public static LatencyMetrics FromSamples(IReadOnlyCollection<double> samples)
        {
            if (samples.Count == 0) return new LatencyMetrics(0, 0, 0, 0, 0);
            var sorted = samples.OrderBy(s => s).ToList();
            var min = sorted[0];
            var max = sorted[^1];
            var avg = sorted.Average();
            var p50 = ComputePercentile(sorted, 0.50);
            var p95 = ComputePercentile(sorted, 0.95);
            return new LatencyMetrics(min, max, p50, p95, avg);
        }

        private static double ComputePercentile(IReadOnlyList<double> sorted, double percentile)
        {
            if (sorted.Count == 0) return 0;
            var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
            return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
        }
    }

    private sealed class RecordingQueue : IGradingQueueChannel
    {
        public int Count => Tasks.Count;
        public int Capacity => 1000;
        public ConcurrentQueue<GradingTask> Tasks { get; } = new();

        public ValueTask EnqueueAsync(GradingTask task, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Tasks.Enqueue(task);
            return ValueTask.CompletedTask;
        }

        public IAsyncEnumerable<GradingTask> ReadAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
