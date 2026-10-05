using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamQuota;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.MockExams;

public class GetMockExamQuotaQueryHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "1. Trả về đầy đủ hạn ngạch K=3 khi sinh viên chưa thi lượt nào trong ngày")]
    public async Task Handle_Should_Return_Full_Quota_When_No_Prior_Exams()
    {
        using var context = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            Credits = 3
        };
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        var studentId = Guid.NewGuid();
        var handler = new GetMockExamQuotaQueryHandler(context);
        var query = new GetMockExamQuotaQuery(course.Id, studentId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.CourseId.Should().Be(course.Id);
        result.Value.StudentId.Should().Be(studentId);
        result.Value.UsedCount.Should().Be(0);
        result.Value.RemainingCount.Should().Be(3);
        result.Value.MaxDailyQuota.Should().Be(3);
        result.Value.CanStartExam.Should().BeTrue();
    }

    [Fact(DisplayName = "2. Trả về số lượt còn lại chính xác khi sinh viên đã thi 2 lượt")]
    public async Task Handle_Should_Return_Remaining_Count_When_Partially_Used()
    {
        using var context = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Cross-Platform Apps",
            Credits = 3
        };
        var studentId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var quota = new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentId,
            QuotaDate = today,
            UsedCount = 2,
            UpdatedAt = DateTime.UtcNow
        };

        context.Courses.Add(course);
        context.MockExamQuotas.Add(quota);
        await context.SaveChangesAsync();

        var handler = new GetMockExamQuotaQueryHandler(context);
        var query = new GetMockExamQuotaQuery(course.Id, studentId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UsedCount.Should().Be(2);
        result.Value.RemainingCount.Should().Be(1);
        result.Value.CanStartExam.Should().BeTrue();
    }

    [Fact(DisplayName = "3. Chặn thi khi sinh viên đã đạt giới hạn tối đa K=3 lượt/ngày")]
    public async Task Handle_Should_Block_Exam_When_Quota_Exhausted()
    {
        using var context = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWP391",
            Name = "Application Development Project",
            Credits = 3
        };
        var studentId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var quota = new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentId,
            QuotaDate = today,
            UsedCount = 3,
            UpdatedAt = DateTime.UtcNow
        };

        context.Courses.Add(course);
        context.MockExamQuotas.Add(quota);
        await context.SaveChangesAsync();

        var handler = new GetMockExamQuotaQueryHandler(context);
        var query = new GetMockExamQuotaQuery(course.Id, studentId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UsedCount.Should().Be(3);
        result.Value.RemainingCount.Should().Be(0);
        result.Value.CanStartExam.Should().BeFalse();
    }

    [Fact(DisplayName = "4. Trả về Failure khi CourseId không tồn tại trong hệ thống")]
    public async Task Handle_Should_Return_Failure_When_Course_Not_Found()
    {
        using var context = CreateDbContext();
        var nonExistentCourseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var handler = new GetMockExamQuotaQueryHandler(context);
        var query = new GetMockExamQuotaQuery(nonExistentCourseId, studentId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Môn học không tồn tại");
    }

    [Fact(DisplayName = "5. Validator báo lỗi khi CourseId hoặc StudentId để trống")]
    public void Validator_Should_Fail_When_Inputs_Are_Empty()
    {
        var validator = new GetMockExamQuotaQueryValidator();

        var invalidCourseQuery = new GetMockExamQuotaQuery(Guid.Empty, Guid.NewGuid());
        var invalidCourseResult = validator.Validate(invalidCourseQuery);
        invalidCourseResult.IsValid.Should().BeFalse();
        invalidCourseResult.Errors.Should().Contain(e => e.PropertyName == "CourseId");

        var invalidStudentQuery = new GetMockExamQuotaQuery(Guid.NewGuid(), Guid.Empty);
        var invalidStudentResult = validator.Validate(invalidStudentQuery);
        invalidStudentResult.IsValid.Should().BeFalse();
        invalidStudentResult.Errors.Should().Contain(e => e.PropertyName == "StudentId");

        var validQuery = new GetMockExamQuotaQuery(Guid.NewGuid(), Guid.NewGuid());
        var validResult = validator.Validate(validQuery);
        validResult.IsValid.Should().BeTrue();
    }
}
