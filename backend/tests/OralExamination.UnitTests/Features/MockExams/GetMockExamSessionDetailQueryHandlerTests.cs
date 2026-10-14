using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamSessionDetail;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.MockExams;

public class GetMockExamSessionDetailQueryHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "1. Trả về chi tiết bài thi thử kèm đầy đủ Scorecard và tiêu chí Rubric CLO")]
    public async Task Handle_Should_Return_Full_Scorecard_When_Session_Exists()
    {
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            Credits = 3
        };
        context.Courses.Add(course);

        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric", TotalMaxScore = 10.0m };
        var crit1 = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Tiêu chuẩn CLO1", MaxScore = 10.0m, Weight = 1.0m, OrderIndex = 1 };
        context.Rubrics.Add(rubric);
        context.RubricCriteria.Add(crit1);

        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Câu 1",
            Content = "Nội dung câu 1",
            Difficulty = "medium",
            BloomLevel = "Apply"
        };
        context.PracticeQuestions.Add(question);

        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.Id,
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-30),
            ExpiresAt = DateTime.UtcNow,
            SubmittedAt = DateTime.UtcNow.AddMinutes(-2),
            Status = "graded",
            TotalScore = 8.5m
        };

        var answer = new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            AnswerText = "Transcript câu trả lời",
            Status = "graded",
            TimeTakenSeconds = 60
        };

        var eval = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerType = "practice",
            MockExamAnswerId = answer.Id,
            TotalScore = 8.5m,
            Feedback = "Rất tốt",
            ConfidenceScore = 0.95m,
            Details = new List<AiEvaluationDetail>
            {
                new() { Id = Guid.NewGuid(), CriterionId = crit1.Id, Score = 8.5m, Comment = "Đạt chuẩn" }
            }
        };

        answer.AiEvaluations.Add(eval);
        session.Answers.Add(answer);
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new GetMockExamSessionDetailQueryHandler(context);
        var query = new GetMockExamSessionDetailQuery(session.Id, studentId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var scorecard = result.Value;
        scorecard.Should().NotBeNull();
        scorecard.SessionId.Should().Be(session.Id);
        scorecard.CourseCode.Should().Be("SWD392");
        scorecard.TotalScore.Should().Be(8.5m);
        scorecard.Classification.Should().Be("Giỏi");
        scorecard.Questions.Should().HaveCount(1);

        var qScore = scorecard.Questions[0];
        qScore.Title.Should().Be("Câu 1");
        qScore.Transcript.Should().Be("Transcript câu trả lời");
        qScore.Score.Should().Be(8.5m);
        qScore.CriteriaScores.Should().HaveCount(1);
        qScore.CriteriaScores[0].CriterionName.Should().Be("Tiêu chuẩn CLO1");
        qScore.CriteriaScores[0].Score.Should().Be(8.5m);
    }

    [Fact(DisplayName = "2. Báo lỗi khi ca thi thử không tồn tại")]
    public async Task Handle_Should_Fail_When_Session_Not_Found()
    {
        using var context = CreateDbContext();
        var handler = new GetMockExamSessionDetailQueryHandler(context);
        var query = new GetMockExamSessionDetailQuery(Guid.NewGuid(), Guid.NewGuid());

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Không tìm thấy thông tin ca thi thử");
    }

    [Fact(DisplayName = "3. Chặn truy cập khi sinh viên không sở hữu ca thi thử")]
    public async Task Handle_Should_Fail_When_Student_Is_Not_Owner()
    {
        using var context = CreateDbContext();
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Architecture" };
        context.Courses.Add(course);
        var ownerId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();

        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = ownerId,
            CourseId = course.Id,
            ExamSetId = Guid.NewGuid(),
            Status = "graded"
        };
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new GetMockExamSessionDetailQueryHandler(context);
        var query = new GetMockExamSessionDetailQuery(session.Id, otherStudentId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không có quyền truy cập");
    }

    [Fact(DisplayName = "4. GetMockExamSessionDetailQueryValidator bắt lỗi khi thiếu SessionId hoặc StudentId")]
    public void Validator_Should_Enforce_Parameters()
    {
        var validator = new GetMockExamSessionDetailQueryValidator();
        var invalid = new GetMockExamSessionDetailQuery(Guid.Empty, Guid.Empty);

        var res = validator.Validate(invalid);
        res.IsValid.Should().BeFalse();
        res.Errors.Should().Contain(e => e.PropertyName == nameof(GetMockExamSessionDetailQuery.SessionId));
        res.Errors.Should().Contain(e => e.PropertyName == nameof(GetMockExamSessionDetailQuery.StudentId));
    }
}
