using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.Commands.CompletePracticeSession;
using OralExamination.Application.Features.Practice.Commands.SubmitPracticeAnswer;
using OralExamination.Application.Features.Practice.Commands.SubmitPracticeBatch;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Practice;

public class SubmitPracticeBatchCommandHandlerTests
{
    private static OralExamDbContext CreateDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private sealed class UniqueViolationDbContext(DbContextOptions<OralExamDbContext> options)
        : OralExamDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new DbUpdateException(
                "23505: duplicate key value violates unique constraint \"uq_practice_answers_session_question_main\"",
                new Exception("23505"));
        }
    }

    private static async Task<(Course Course, User Student, PracticeSession Session, List<PracticeQuestion> Questions)> SeedFullSessionAsync(
        OralExamDbContext context,
        string status = "in_progress",
        int questionCount = 3)
    {
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWT301",
            Name = "Software Testing",
            IsActive = true
        };
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "batch.student@fpt.edu.vn",
            FullName = "Nguyễn Văn Batch",
            Role = UserRole.Student,
            IsActive = true
        };
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Barem Batch",
            TotalMaxScore = 10.0m
        };
        rubric.Criteria.Add(new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "Tiêu chí 1",
            MaxScore = 10.0m,
            OrderIndex = 1
        });

        var questions = new List<PracticeQuestion>();
        for (int i = 1; i <= questionCount; i++)
        {
            questions.Add(new PracticeQuestion
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                RubricId = rubric.Id,
                Title = $"Câu {i}",
                Content = $"Nội dung câu {i}",
                Difficulty = "easy",
                ApprovalStatus = ApprovalStatus.Approved,
                IsActive = true,
                IsDeleted = false
            });
        }

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            PracticeMode = "full_session",
            Status = status,
            StartedAt = DateTime.UtcNow.AddMinutes(-1),
            LastActivityAt = DateTime.UtcNow,
            SelectedQuestionIds = questions.Select(q => q.Id).ToList()
        };

        context.Courses.Add(course);
        context.Users.Add(student);
        context.Rubrics.Add(rubric);
        context.PracticeQuestions.AddRange(questions);
        context.PracticeSessions.Add(session);
        await context.SaveChangesAsync();

        return (course, student, session, questions);
    }

    [Fact(DisplayName = "SubmitPracticeBatch: Phân tách NOT_FOUND (404) và FORBIDDEN BOLA (403)")]
    public async Task Handle_SplitsNotFoundAndForbiddenErrors()
    {
        using var context = CreateDbContext();
        var (_, student, session, questions) = await SeedFullSessionAsync(context);
        var queueMock = new Mock<IGradingQueueChannel>();
        var handler = new SubmitPracticeBatchCommandHandler(context, queueMock.Object);

        var notFoundResult = await handler.Handle(
            new SubmitPracticeBatchCommand(Guid.NewGuid(), student.Id, [new(questions[0].Id, "Ans", false, null)]),
            CancellationToken.None);
        var forbiddenResult = await handler.Handle(
            new SubmitPracticeBatchCommand(session.Id, Guid.NewGuid(), [new(questions[0].Id, "Ans", false, null)]),
            CancellationToken.None);

        notFoundResult.IsSuccess.Should().BeFalse();
        notFoundResult.Error.Should().StartWith("NOT_FOUND:");

        forbiddenResult.IsSuccess.Should().BeFalse();
        forbiddenResult.Error.Should().StartWith("FORBIDDEN:");
        forbiddenResult.Error.Should().Contain("không thuộc về sinh viên này");
    }

    [Fact(DisplayName = "SubmitPracticeBatch: Trả về CONFLICT khi phiên đã kết thúc")]
    public async Task Handle_WhenSessionEnded_ReturnsConflict()
    {
        using var context = CreateDbContext();
        var (_, student, session, questions) = await SeedFullSessionAsync(context, status: "completed");
        var queueMock = new Mock<IGradingQueueChannel>();
        var handler = new SubmitPracticeBatchCommandHandler(context, queueMock.Object);

        var result = await handler.Handle(
            new SubmitPracticeBatchCommand(session.Id, student.Id, [new(questions[0].Id, "Ans", false, null)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT:");
    }

    [Fact(DisplayName = "SubmitPracticeBatch: Trả về CONFLICT khi nộp trùng QuestionId đã trả lời trước đó qua /answers")]
    public async Task Handle_WhenQuestionAlreadyAnswered_ReturnsConflict()
    {
        using var context = CreateDbContext();
        var (_, student, session, questions) = await SeedFullSessionAsync(context);
        context.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = questions[0].Id,
            StudentId = student.Id,
            OrderIndex = 1,
            AnswerText = "Đã trả lời trước",
            IsFollowUp = false,
            Status = "graded"
        });
        await context.SaveChangesAsync();

        var queueMock = new Mock<IGradingQueueChannel>();
        var handler = new SubmitPracticeBatchCommandHandler(context, queueMock.Object);

        var result = await handler.Handle(
            new SubmitPracticeBatchCommand(session.Id, student.Id, [new(questions[0].Id, "Nộp lại", false, null)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT:");
        queueMock.Verify(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "SubmitPracticeBatch: Batch một phần sau /answers bỏ qua câu đã nộp, bảo toàn TotalScore == null và gắn StudentId cho AiEvaluation bỏ qua")]
    public async Task Handle_PartialBatchAfterPriorAnswer_SkipsExistingPaddingPreservesTotalScoreAndSetsAiEvaluationStudentId()
    {
        using var context = CreateDbContext();
        var (_, student, session, questions) = await SeedFullSessionAsync(context, questionCount: 3);

        // Câu 1 đã được nộp trước qua /answers
        context.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = questions[0].Id,
            StudentId = student.Id,
            OrderIndex = 1,
            AnswerText = "Câu 1 đã nộp trước",
            IsFollowUp = false,
            Status = "graded"
        });
        await context.SaveChangesAsync();

        var enqueued = new List<GradingTask>();
        var queueMock = new Mock<IGradingQueueChannel>();
        queueMock.Setup(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
            .Callback<GradingTask, CancellationToken>((t, _) => enqueued.Add(t))
            .Returns(ValueTask.CompletedTask);

        var handler = new SubmitPracticeBatchCommandHandler(context, queueMock.Object);

        // Batch chỉ nộp câu 2 (bỏ qua câu 3 -> tự điền skipped; câu 1 đã nộp trước -> không bị chèn trùng và TotalScore giữ null)
        var result = await handler.Handle(
            new SubmitPracticeBatchCommand(session.Id, student.Id, [new(questions[1].Id, "", false, null)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updatedSession = await context.PracticeSessions.FindAsync(session.Id);
        updatedSession!.TotalScore.Should().BeNull("đã có câu trả lời trước đó nên không được ép TotalScore = 0.0m");

        var savedAnswers = await context.PracticeAnswers.Where(a => a.SessionId == session.Id).ToListAsync();
        savedAnswers.Should().HaveCount(3);

        var skippedEvals = await context.AiEvaluations.ToListAsync();
        skippedEvals.Should().HaveCount(2);
        skippedEvals.Should().OnlyContain(e => e.StudentId == student.Id);
    }

    [Fact(DisplayName = "SubmitPracticeBatch: Bắt DbUpdateException (23505) và trả về CONFLICT thay vì HTTP 500")]
    public async Task Handle_WhenDbUpdateException23505Occurs_ReturnsConflict()
    {
        var dbName = Guid.NewGuid().ToString();
        using var seedContext = CreateDbContext(dbName);
        var (_, student, session, questions) = await SeedFullSessionAsync(seedContext, questionCount: 1);

        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        using var throwingContext = new UniqueViolationDbContext(options);

        var queueMock = new Mock<IGradingQueueChannel>();
        var handler = new SubmitPracticeBatchCommandHandler(throwingContext, queueMock.Object);

        var result = await handler.Handle(
            new SubmitPracticeBatchCommand(session.Id, student.Id, [new(questions[0].Id, "Valid answer", false, null)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT:");
    }

    [Fact(DisplayName = "SubmitPracticeAnswer & SubmitPracticeBatch: Từ chối câu hỏi có IsDeleted = true")]
    public async Task SubmitAnswerAndBatch_RejectSoftDeletedQuestion()
    {
        using var context = CreateDbContext();
        var (_, student, session, questions) = await SeedFullSessionAsync(context, questionCount: 2);
        questions[0].IsDeleted = true;
        await context.SaveChangesAsync();

        var queueMock = new Mock<IGradingQueueChannel>();

        var answerRes = await new SubmitPracticeAnswerCommandHandler(context, queueMock.Object)
            .Handle(new SubmitPracticeAnswerCommand(session.Id, questions[0].Id, student.Id, "Ans", false, null), CancellationToken.None);
        answerRes.IsSuccess.Should().BeFalse();
        answerRes.Error.Should().StartWith("VALIDATION:");

        var batchRes = await new SubmitPracticeBatchCommandHandler(context, queueMock.Object)
            .Handle(new SubmitPracticeBatchCommand(session.Id, student.Id, [new(questions[0].Id, "Ans", false, null)]), CancellationToken.None);
        batchRes.IsSuccess.Should().BeFalse();
        batchRes.Error.Should().StartWith("VALIDATION:");
    }

    [Fact(DisplayName = "CompletePracticeSession: Gắn StudentId cho AiEvaluation của câu bị bỏ qua")]
    public async Task CompletePracticeSession_SetsStudentIdOnSkippedAiEvaluation()
    {
        using var context = CreateDbContext();
        var (_, student, session, _) = await SeedFullSessionAsync(context, questionCount: 2);

        var result = await new CompletePracticeSessionCommandHandler(context)
            .Handle(new CompletePracticeSessionCommand(session.Id, student.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var evals = await context.AiEvaluations.ToListAsync();
        evals.Should().HaveCount(2);
        evals.Should().OnlyContain(e => e.StudentId == student.Id);
    }

    [Fact(DisplayName = "SubmitPracticeBatch: Phiên đã completed quá 5 phút trả về CONFLICT (409) thay vì SESSION_TIMEOUT_410")]
    public async Task Handle_WhenCompletedSessionOlderThan5Minutes_ReturnsConflictInsteadOf410()
    {
        using var context = CreateDbContext();
        var (_, student, session, questions) = await SeedFullSessionAsync(context, status: "completed", questionCount: 1);
        session.LastActivityAt = DateTime.UtcNow.AddMinutes(-10);
        await context.SaveChangesAsync();

        var queueMock = new Mock<IGradingQueueChannel>();
        var handler = new SubmitPracticeBatchCommandHandler(context, queueMock.Object);

        var result = await handler.Handle(
            new SubmitPracticeBatchCommand(session.Id, student.Id, [new(questions[0].Id, "Ans", false, null)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT:");
    }

    [Fact(DisplayName = "SubmitPracticeBatch: Từ chối câu hỏi IsDeleted = true ngay cả khi toàn bộ câu hỏi của môn học đều bị xóa mềm")]
    public async Task Handle_WhenAllCourseQuestionsSoftDeleted_StillRejectsSoftDeletedQuestion()
    {
        using var context = CreateDbContext();
        var (_, student, session, questions) = await SeedFullSessionAsync(context, questionCount: 1);
        questions[0].IsDeleted = true;
        await context.SaveChangesAsync();

        var queueMock = new Mock<IGradingQueueChannel>();
        var handler = new SubmitPracticeBatchCommandHandler(context, queueMock.Object);

        var result = await handler.Handle(
            new SubmitPracticeBatchCommand(session.Id, student.Id, [new(questions[0].Id, "Ans", false, null)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("VALIDATION:");
    }

    [Fact(DisplayName = "SubmitPracticeBatch: Câu trước đã có AiEvaluation + batch chỉ gồm câu bỏ qua -> tính và lưu TotalScore trong DB")]
    public async Task Handle_PriorGradedAnswerWithEvaluation_AndSkippedOnlyBatch_ComputesAndPersistsTotalScore()
    {
        using var context = CreateDbContext();
        var (_, student, session, questions) = await SeedFullSessionAsync(context, questionCount: 3);

        var priorAnswer = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = questions[0].Id,
            StudentId = student.Id,
            OrderIndex = 1,
            AnswerText = "Câu 1 đã chấm",
            IsFollowUp = false,
            Status = "graded"
        };
        var priorEval = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerType = "practice",
            StudentId = student.Id,
            PracticeAnswerId = priorAnswer.Id,
            TotalScore = 9.0m,
            ConfidenceScore = 0.95m,
            Feedback = "Tốt",
            EvaluatedAt = DateTime.UtcNow.AddSeconds(-30)
        };
        session.TotalScore = 9.0m;
        context.PracticeAnswers.Add(priorAnswer);
        context.AiEvaluations.Add(priorEval);
        await context.SaveChangesAsync();

        var queueMock = new Mock<IGradingQueueChannel>();
        var handler = new SubmitPracticeBatchCommandHandler(context, queueMock.Object);

        var result = await handler.Handle(
            new SubmitPracticeBatchCommand(session.Id, student.Id, [new(questions[1].Id, "", false, null)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updatedSession = await context.PracticeSessions.FindAsync(session.Id);
        updatedSession!.Status.Should().Be("completed");
        updatedSession.TotalScore.Should().Be(3.00m);
        queueMock.Verify(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
