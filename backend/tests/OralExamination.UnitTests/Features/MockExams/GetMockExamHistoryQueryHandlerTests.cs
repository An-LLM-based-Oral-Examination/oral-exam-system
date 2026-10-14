using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamHistory;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.MockExams;

public class GetMockExamHistoryQueryHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "1. Lấy danh sách lịch sử thi thử phân trang thành công cho sinh viên")]
    public async Task Handle_Should_Return_Paginated_History_For_Student()
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

        for (int i = 0; i < 5; i++)
        {
            var session = new MockExamSession
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                CourseId = course.Id,
                ExamSetId = Guid.NewGuid(),
                StartedAt = DateTime.UtcNow.AddDays(-i),
                ExpiresAt = DateTime.UtcNow.AddDays(-i).AddMinutes(30),
                Status = "graded",
                TotalScore = 8.0m + (i * 0.2m)
            };
            session.Answers.Add(new MockExamAnswer
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                QuestionId = Guid.NewGuid(),
                Status = "graded",
                AnswerText = "Transcript"
            });
            context.MockExamSessions.Add(session);
        }
        await context.SaveChangesAsync();

        var handler = new GetMockExamHistoryQueryHandler(context);
        var query = new GetMockExamHistoryQuery(studentId, PageIndex: 1, PageSize: 3);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        response.Should().NotBeNull();
        response.TotalCount.Should().Be(5);
        response.TotalPages.Should().Be(2);
        response.PageIndex.Should().Be(1);
        response.PageSize.Should().Be(3);
        response.Items.Should().HaveCount(3);
        response.Items[0].CourseCode.Should().Be("SWD392");
    }

    [Fact(DisplayName = "2. Lọc danh sách lịch sử thi thử theo môn học courseId")]
    public async Task Handle_Should_Filter_By_CourseId_When_Provided()
    {
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var course1 = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Software Architecture" };
        var course2 = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "Building Web APIs" };
        context.Courses.AddRange(course1, course2);

        var s1 = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course1.Id,
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddDays(-1),
            Status = "graded",
            TotalScore = 8.5m
        };
        var s2 = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course2.Id,
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            Status = "graded",
            TotalScore = 7.0m
        };
        context.MockExamSessions.AddRange(s1, s2);
        await context.SaveChangesAsync();

        var handler = new GetMockExamHistoryQueryHandler(context);
        var query = new GetMockExamHistoryQuery(studentId, CourseId: course1.Id, PageIndex: 1, PageSize: 10);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].CourseCode.Should().Be("SWD392");
    }

    [Fact(DisplayName = "3. Cô lập dữ liệu lịch sử giữa các sinh viên")]
    public async Task Handle_Should_Isolate_History_Per_Student()
    {
        using var context = CreateDbContext();
        var studentA = Guid.NewGuid();
        var studentB = Guid.NewGuid();
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Architecture" };
        context.Courses.Add(course);

        context.MockExamSessions.Add(new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentA,
            CourseId = course.Id,
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            Status = "graded"
        });
        context.MockExamSessions.Add(new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentB,
            CourseId = course.Id,
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            Status = "graded"
        });
        await context.SaveChangesAsync();

        var handler = new GetMockExamHistoryQueryHandler(context);
        var query = new GetMockExamHistoryQuery(studentA, PageIndex: 1, PageSize: 10);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].SessionId.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "4. GetMockExamHistoryQueryValidator bắt lỗi tham số rỗng hoặc không hợp lệ")]
    public void Validator_Should_Enforce_Parameters()
    {
        var validator = new GetMockExamHistoryQueryValidator();
        var invalid = new GetMockExamHistoryQuery(Guid.Empty, PageIndex: 0, PageSize: 150);

        var res = validator.Validate(invalid);

        res.IsValid.Should().BeFalse();
        res.Errors.Should().Contain(e => e.PropertyName == nameof(GetMockExamHistoryQuery.StudentId));
        res.Errors.Should().Contain(e => e.PropertyName == nameof(GetMockExamHistoryQuery.PageIndex));
        res.Errors.Should().Contain(e => e.PropertyName == nameof(GetMockExamHistoryQuery.PageSize));
    }
}
