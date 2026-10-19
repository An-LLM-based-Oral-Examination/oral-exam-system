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

    [Fact(DisplayName = "GetPracticeSessionQuery: Ưu tiên đọc TranscriptBufferSeconds từ SystemConfigs của Admin")]
    public async Task Handle_Should_Prioritize_SystemConfig_TranscriptBufferSeconds()
    {
        using var context = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWR302",
            Name = "Software Requirements",
            Credits = 3,
            TranscriptBufferSeconds = 60,
            IsActive = true
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.buffer@fpt.edu.vn",
            FullName = "Nguyễn Văn Buffer",
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

        context.Courses.Add(course);
        context.Users.Add(student);
        context.PracticeSessions.Add(session);
        context.SystemConfigs.Add(new SystemConfig
        {
            Key = "TranscriptBufferSeconds",
            Value = "120",
            Description = "Thời gian đệm chỉnh sửa transcript MF-01"
        });
        await context.SaveChangesAsync();

        var handler = new GetPracticeSessionQueryHandler(context);
        var result = await handler.Handle(new GetPracticeSessionQuery(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TranscriptBufferSeconds.Should().Be(120);
    }

    [Fact(DisplayName = "GetPracticeSessionQuery: Chỉ trả về các câu hỏi đã phát sinh trong phiên thay vì toàn bộ câu hỏi của môn học")]
    public async Task Handle_Should_Only_Return_Session_Questions_Not_All_Course_Questions()
    {
        using var context = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture and Design",
            Credits = 3,
            TranscriptBufferSeconds = 60,
            IsActive = true
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.swd@fpt.edu.vn",
            FullName = "Nguyễn Văn SWD",
            Role = "student"
        };

        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Barem SWD392",
            TotalMaxScore = 10.0m
        };

        var q1 = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Rubric = rubric,
            Title = "Câu 1",
            Content = "Câu hỏi số 1 thuộc phiên",
            Difficulty = "easy",
            IsActive = true
        };
        var q2 = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Rubric = rubric,
            Title = "Câu 2",
            Content = "Câu hỏi số 2 ngoài phiên",
            Difficulty = "hard",
            IsActive = true
        };

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            PracticeMode = "per_question",
            SelectedDifficulties = "[\"easy\"]",
            Status = "in_progress",
            Course = course,
            Student = student
        };

        var answer = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = q1.Id,
            StudentId = student.Id,
            AnswerText = "Câu trả lời cho câu 1",
            Status = "graded",
            SubmittedAt = DateTime.UtcNow
        };

        context.Courses.Add(course);
        context.Rubrics.Add(rubric);
        context.Users.Add(student);
        context.PracticeQuestions.AddRange(q1, q2);
        context.PracticeSessions.Add(session);
        context.PracticeAnswers.Add(answer);
        await context.SaveChangesAsync();

        var handler = new GetPracticeSessionQueryHandler(context);
        var result = await handler.Handle(new GetPracticeSessionQuery(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Questions.Should().HaveCount(1);
        result.Value.Questions[0].QuestionId.Should().Be(q1.Id);
    }

    [Fact(DisplayName = "GetPracticeSessionQuery: Student A không thể xem phiên luyện tập của Student B (BOLA Protection)")]
    public async Task Handle_Should_Return_Failure_When_Student_Attempts_To_Access_Other_Student_Session()
    {
        using var context = CreateDbContext();
        var studentAId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Web APIs",
            Credits = 3,
            TranscriptBufferSeconds = 60,
            IsActive = true
        };

        var studentB = new User
        {
            Id = studentBId,
            Email = "studentB@fpt.edu.vn",
            FullName = "Nguyễn Văn B",
            Role = "student"
        };

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentBId,
            PracticeMode = "per_question",
            Status = "in_progress",
            Course = course,
            Student = studentB
        };

        context.Courses.Add(course);
        context.Users.Add(studentB);
        context.PracticeSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new GetPracticeSessionQueryHandler(context);
        var query = new GetPracticeSessionQuery(session.Id, RequestingUserId: studentAId, IsElevatedRole: false);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Bạn không có quyền truy cập phiên luyện tập này.");
    }

    [Fact(DisplayName = "GetPracticeSessionQuery: Student A xem được phiên luyện tập chính chủ của mình")]
    public async Task Handle_Should_Return_Success_When_Student_Accesses_Own_Session()
    {
        using var context = CreateDbContext();
        var studentAId = Guid.NewGuid();

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Web APIs",
            Credits = 3,
            TranscriptBufferSeconds = 60,
            IsActive = true
        };

        var studentA = new User
        {
            Id = studentAId,
            Email = "studentA@fpt.edu.vn",
            FullName = "Nguyễn Văn A",
            Role = "student"
        };

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentAId,
            PracticeMode = "per_question",
            Status = "in_progress",
            Course = course,
            Student = studentA
        };

        context.Courses.Add(course);
        context.Users.Add(studentA);
        context.PracticeSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new GetPracticeSessionQueryHandler(context);
        var query = new GetPracticeSessionQuery(session.Id, RequestingUserId: studentAId, IsElevatedRole: false);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SessionId.Should().Be(session.Id);
        result.Value.StudentId.Should().Be(studentAId);
    }

    [Fact(DisplayName = "GetPracticeSessionQuery: Elevated Role (lecturer/head/admin) xem được phiên luyện tập của bất kỳ sinh viên nào")]
    public async Task Handle_Should_Return_Success_When_Elevated_Role_Accesses_Any_Session()
    {
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Web APIs",
            Credits = 3,
            TranscriptBufferSeconds = 60,
            IsActive = true
        };

        var student = new User
        {
            Id = studentId,
            Email = "student@fpt.edu.vn",
            FullName = "Nguyễn Văn Sinh Viên",
            Role = "student"
        };

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentId,
            PracticeMode = "per_question",
            Status = "in_progress",
            Course = course,
            Student = student
        };

        context.Courses.Add(course);
        context.Users.Add(student);
        context.PracticeSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new GetPracticeSessionQueryHandler(context);
        var query = new GetPracticeSessionQuery(session.Id, RequestingUserId: lecturerId, IsElevatedRole: true);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SessionId.Should().Be(session.Id);
        result.Value.StudentId.Should().Be(studentId);
    }
}
