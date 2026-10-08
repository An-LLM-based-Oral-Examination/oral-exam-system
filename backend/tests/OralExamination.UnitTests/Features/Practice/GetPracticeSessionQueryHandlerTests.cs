using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Practice.Queries.GetPracticeSession;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Practice;

public class GetPracticeSessionQueryHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "GetPracticeSessionQuery: Trả về chi tiết phiên luyện tập kèm câu hỏi và câu trả lời")]
    public async Task Handle_Should_Return_Session_Detail_When_Found()
    {
        using var context = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Web APIs",
            Credits = 3,
            TranscriptBufferSeconds = 60,
            IsActive = true
        };

        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Barem PRN231",
            TotalMaxScore = 10.0m
        };

        var crit = new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "Kiến thức lý thuyết",
            MaxScore = 10.0m
        };
        rubric.Criteria.Add(crit);

        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Câu hỏi Clean Architecture",
            Content = "Clean Architecture gồm những tầng nào?",
            SampleAnswer = "Domain, Application, Infrastructure, Presentation.",
            Rubric = rubric,
            IsActive = true
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student@fpt.edu.vn",
            FullName = "Nguyễn Văn Sinh Viên",
            Role = "student"
        };

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            PracticeMode = "per_question",
            Status = "in_progress",
            Course = course,
            Student = student
        };

        var answer = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = student.Id,
            AnswerText = "Gồm Domain, Application, Infrastructure và API.",
            Status = "pending"
        };

        context.Courses.Add(course);
        context.Rubrics.Add(rubric);
        context.PracticeQuestions.Add(question);
        context.Users.Add(student);
        context.PracticeSessions.Add(session);
        context.PracticeAnswers.Add(answer);
        await context.SaveChangesAsync();

        var handler = new GetPracticeSessionQueryHandler(context);
        var query = new GetPracticeSessionQuery(session.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SessionId.Should().Be(session.Id);
        result.Value.CourseCode.Should().Be("PRN231");
        result.Value.Questions.Should().HaveCount(1);
        result.Value.Answers.Should().HaveCount(1);
        result.Value.Answers[0].AnswerText.Should().Be("Gồm Domain, Application, Infrastructure và API.");
    }

    [Fact(DisplayName = "GetPracticeSessionQuery: Trả về Failure khi SessionId không tồn tại")]
    public async Task Handle_Should_Return_Failure_When_Session_Not_Found()
    {
        using var context = CreateDbContext();
        var handler = new GetPracticeSessionQueryHandler(context);
        var query = new GetPracticeSessionQuery(Guid.NewGuid());

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không tồn tại");
    }
}
