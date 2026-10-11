using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.Practice.Commands.NextQuestion;
using OralExamination.Application.Features.Practice.Commands.SubmitPracticeAnswer;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Practice;

public class PracticeSessionInactivityTimeoutTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(Course Course, User Student, PracticeSession Session)> SeedSessionAsync(
        OralExamDbContext context,
        DateTime lastActivityAt,
        int timeoutMinutes = 10)
    {
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWT301",
            Name = "Software Testing",
            TranscriptBufferSeconds = 60,
            IsActive = true
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.timeout@fpt.edu.vn",
            FullName = "Nguyễn Văn Timeout",
            Role = UserRole.Student,
            IsActive = true
        };

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            StartedAt = DateTime.UtcNow.AddMinutes(-30),
            LastActivityAt = lastActivityAt,
            Status = "in_progress",
            PracticeMode = "per_question",
            SelectedDifficulties = JsonSerializer.Serialize(new List<string> { "easy", "medium" })
        };

        context.Courses.Add(course);
        context.Users.Add(student);
        context.PracticeSessions.Add(session);

        context.SystemConfigs.Add(new SystemConfig
        {
            Id = Guid.NewGuid(),
            Key = "SessionInactivityTimeoutMinutes",
            Value = timeoutMinutes.ToString(),
            Description = "Timeout luyện tập không tương tác (phút)"
        });

        await context.SaveChangesAsync();
        return (course, student, session);
    }

    private async Task SeedQuestionsAsync(OralExamDbContext context, Guid courseId)
    {
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Name = "Rubric chung",
            TotalMaxScore = 10.0m
        };
        context.Rubrics.Add(rubric);

        for (int i = 1; i <= 3; i++)
        {
            context.PracticeQuestions.Add(new PracticeQuestion
            {
                Id = Guid.NewGuid(),
                CourseId = courseId,
                RubricId = rubric.Id,
                Title = $"Câu easy #{i}",
                Content = $"Nội dung câu #{i}",
                Difficulty = "easy",
                IsActive = true
            });
        }
        await context.SaveChangesAsync();
    }

    [Fact(DisplayName = "1. NextQuestion: Khi không tương tác quá 10 phút, tự động chuyển status = completed và trả về lỗi timeout")]
    public async Task NextQuestion_WhenInactiveForMoreThan10Minutes_ShouldMarkSessionCompletedAndReturnTimeoutFailure()
    {
        // Arrange: LastActivityAt cách đây 12 phút (quá 10 phút)
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, DateTime.UtcNow.AddMinutes(-12), timeoutMinutes: 10);
        await SeedQuestionsAsync(context, course.Id);

        var handler = new NextQuestionCommandHandler(context);
        var command = new NextQuestionCommand(session.Id, student.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không có tương tác trong hơn 10 phút");

        // Kiểm tra trong DB: Session đã được cập nhật completed an toàn
        var updatedSession = await context.PracticeSessions.FindAsync(session.Id);
        updatedSession.Should().NotBeNull();
        updatedSession!.Status.Should().Be("completed");
        updatedSession.CompletedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "2. SubmitPracticeAnswer: Khi không tương tác quá 10 phút, tự động chuyển status = completed và trả về lỗi timeout")]
    public async Task SubmitPracticeAnswer_WhenInactiveForMoreThan10Minutes_ShouldMarkSessionCompletedAndReturnTimeoutFailure()
    {
        // Arrange: LastActivityAt cách đây 15 phút
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, DateTime.UtcNow.AddMinutes(-15), timeoutMinutes: 10);
        await SeedQuestionsAsync(context, course.Id);

        var question = await context.PracticeQuestions.FirstAsync();
        var queueMock = new Mock<IGradingQueueChannel>();

        var handler = new SubmitPracticeAnswerCommandHandler(context, queueMock.Object);
        var command = new SubmitPracticeAnswerCommand(
            SessionId: session.Id,
            QuestionId: question.Id,
            StudentId: student.Id,
            AnswerText: "Trả lời bài thi sau khi bỏ quên",
            IsFollowUp: false,
            ParentAnswerId: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không có tương tác trong hơn 10 phút");

        var updatedSession = await context.PracticeSessions.FindAsync(session.Id);
        updatedSession!.Status.Should().Be("completed");
        updatedSession.CompletedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "3. Inactivity Timeout bảo toàn 100% dữ liệu các câu trả lời và điểm số đã hoàn thành trước đó")]
    public async Task InactivityTimeout_ShouldPreserveCompletedAnswersAndScores()
    {
        // Arrange: Đã có 2 câu trả lời có điểm trước khi session bị timeout
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, DateTime.UtcNow.AddMinutes(-20), timeoutMinutes: 10);
        await SeedQuestionsAsync(context, course.Id);

        var questions = await context.PracticeQuestions.Take(2).ToListAsync();

        var answer1 = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = questions[0].Id,
            StudentId = student.Id,
            AnswerText = "Câu trả lời số 1 chuẩn",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-25),
            IsFollowUp = false,
            Status = "evaluated"
        };
        var eval1 = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerType = "practice",
            PracticeAnswerId = answer1.Id,
            TotalScore = 8.5m,
            Feedback = "Rất tốt",
            EvaluatedAt = DateTime.UtcNow.AddMinutes(-24)
        };

        var answer2 = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = questions[1].Id,
            StudentId = student.Id,
            AnswerText = "Câu trả lời số 2 chi tiết",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-22),
            IsFollowUp = false,
            Status = "evaluated"
        };
        var eval2 = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerType = "practice",
            PracticeAnswerId = answer2.Id,
            TotalScore = 9.0m,
            Feedback = "Xuất sắc",
            EvaluatedAt = DateTime.UtcNow.AddMinutes(-21)
        };

        context.PracticeAnswers.AddRange(answer1, answer2);
        context.AiEvaluations.AddRange(eval1, eval2);
        await context.SaveChangesAsync();

        var handler = new NextQuestionCommandHandler(context);
        var command = new NextQuestionCommand(session.Id, student.Id);

        // Act: Kích hoạt lazy validation timeout
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: Gọi bị từ chối do timeout nhưng dữ liệu câu trả lời và đánh giá vẫn nguyên vẹn
        result.IsSuccess.Should().BeFalse();

        var preservedAnswers = await context.PracticeAnswers
            .Where(a => a.SessionId == session.Id)
            .OrderBy(a => a.SubmittedAt)
            .ToListAsync();

        preservedAnswers.Should().HaveCount(2);
        preservedAnswers[0].AnswerText.Should().Be("Câu trả lời số 1 chuẩn");
        preservedAnswers[1].AnswerText.Should().Be("Câu trả lời số 2 chi tiết");

        var preservedEvals = await context.AiEvaluations
            .Where(e => e.PracticeAnswerId == answer1.Id || e.PracticeAnswerId == answer2.Id)
            .OrderBy(e => e.EvaluatedAt)
            .ToListAsync();

        preservedEvals.Should().HaveCount(2);
        preservedEvals[0].TotalScore.Should().Be(8.5m);
        preservedEvals[0].Feedback.Should().Be("Rất tốt");
        preservedEvals[1].TotalScore.Should().Be(9.0m);
        preservedEvals[1].Feedback.Should().Be("Xuất sắc");
    }

    [Fact(DisplayName = "4. Cập nhật chính xác LastActivityAt khi NextQuestion và SubmitPracticeAnswer tương tác hợp lệ")]
    public async Task Interaction_ShouldUpdateLastActivityAtOnSuccessfulNextQuestionAndSubmitAnswer()
    {
        // Arrange: Session tương tác cách đây 3 phút (< 10 phút, còn hợp lệ)
        using var context = CreateDbContext();
        var oldTime = DateTime.UtcNow.AddMinutes(-3);
        var (course, student, session) = await SeedSessionAsync(context, oldTime, timeoutMinutes: 10);
        await SeedQuestionsAsync(context, course.Id);

        var nextHandler = new NextQuestionCommandHandler(context);
        var nextCommand = new NextQuestionCommand(session.Id, student.Id);

        // Act 1: Lấy câu hỏi tiếp theo
        var nextResult = await nextHandler.Handle(nextCommand, CancellationToken.None);

        // Assert 1: LastActivityAt đã được cập nhật mới hơn oldTime
        nextResult.IsSuccess.Should().BeTrue();
        var sessionAfterNext = await context.PracticeSessions.FindAsync(session.Id);
        sessionAfterNext!.LastActivityAt.Should().BeAfter(oldTime);

        var timeAfterNext = sessionAfterNext.LastActivityAt;

        // Act 2: Nộp câu trả lời sau đó
        var queueMock = new Mock<IGradingQueueChannel>();
        var submitHandler = new SubmitPracticeAnswerCommandHandler(context, queueMock.Object);
        var submitCommand = new SubmitPracticeAnswerCommand(
            SessionId: session.Id,
            QuestionId: nextResult.Value.Question!.Id,
            StudentId: student.Id,
            AnswerText: "Sinh viên trả lời trong thời gian hợp lệ",
            IsFollowUp: false,
            ParentAnswerId: null
        );

        var submitResult = await submitHandler.Handle(submitCommand, CancellationToken.None);

        // Assert 2: Nộp thành công và LastActivityAt tiếp tục được cập nhật
        submitResult.IsSuccess.Should().BeTrue();
        var sessionAfterSubmit = await context.PracticeSessions.FindAsync(session.Id);
        sessionAfterSubmit!.LastActivityAt.Should().BeOnOrAfter(timeAfterNext);
    }
}
