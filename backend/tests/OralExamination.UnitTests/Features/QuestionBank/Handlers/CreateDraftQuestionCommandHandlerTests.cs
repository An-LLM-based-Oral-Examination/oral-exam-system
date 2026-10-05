using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.QuestionBank.Handlers;

public class CreateDraftQuestionCommandHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(User Lecturer, Course Course)> SeedUserAndCourseAsync(OralExamDbContext context, string role = "lecturer")
    {
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer.test@fpt.edu.vn",
            FullName = "Giảng Viên Kiểm Thử",
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Cross-Platform Back-End Applications with .NET",
            Credits = 3,
            SemesterId = semester.Id,
            HasFollowUp = true,
            IsActive = true
        };

        context.Users.Add(lecturer);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        return (lecturer, course);
    }

    private CreateDraftQuestionCommand CreateCommand(
        Guid courseId,
        Guid lecturerId,
        string usageScope = "official_exam",
        string? sampleAnswer = "Đây là câu trả lời mẫu đạt chuẩn tối thiểu 50 ký tự cho câu hỏi thi vấn đáp phần mềm.",
        decimal totalRubricScore = 10.00m,
        int criteriaCount = 2)
    {
        var criteria = new List<RubricCriterionDraftDto>();
        decimal scorePerCriteria = totalRubricScore / criteriaCount;

        for (int i = 1; i <= criteriaCount; i++)
        {
            criteria.Add(new RubricCriterionDraftDto(
                CriterionName: $"Tiêu chí {i}",
                MaxScore: scorePerCriteria,
                Weight: 100m / criteriaCount,
                BloomLevel: "understand",
                Description: $"Mô tả tiêu chí {i}",
                OrderIndex: i
            ));
        }

        var rubric = new RubricDraftDto(
            Name: "Barem Rubric Kiến Trúc",
            Description: "Barem thẩm định Clean Architecture",
            TotalMaxScore: totalRubricScore,
            Criteria: criteria
        );

        return new CreateDraftQuestionCommand(
            CourseId: courseId,
            LecturerId: lecturerId,
            Title: "Giải thích Clean Architecture 4 tầng trong .NET 8",
            Content: "Trình bày sự phân tách giữa Domain, Application, Infrastructure và Presentation layer.",
            SampleAnswer: sampleAnswer,
            KeyPoints: new List<string> { "Domain thuần khiết", "Application CQRS", "Dependency Inversion" },
            Difficulty: QuestionDifficulty.Medium,
            BloomLevel: BloomLevel.Analyze,
            Source: "manual",
            UsageScope: usageScope,
            HasFollowUp: true,
            FollowUpPrompt: "Tại sao Domain không được phụ thuộc trực tiếp vào EF Core?",
            Rubric: rubric
        );
    }

    [Fact(DisplayName = "1. Handle tạo thành công ExamQuestion nháp với trạng thái DRAFT khi dữ liệu hợp lệ")]
    public async Task Handle_Should_Create_Draft_ExamQuestion_When_Valid_Data_Provided()
    {
        using var context = CreateDbContext();
        var (lecturer, course) = await SeedUserAndCourseAsync(context);
        var handler = new CreateDraftQuestionCommandHandler(context);

        var command = CreateCommand(course.Id, lecturer.Id, usageScope: "official_exam");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        var savedQuestion = await context.ExamQuestions
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .FirstOrDefaultAsync(q => q.Id == result.Value);

        savedQuestion.Should().NotBeNull();
        savedQuestion!.CourseId.Should().Be(course.Id);
        savedQuestion.Title.Should().Be(command.Title);
        savedQuestion.ApprovalStatus.Should().Be(ApprovalStatus.Draft);
        savedQuestion.SubmittedBy.Should().Be(lecturer.Id);
        savedQuestion.Source.Should().Be("manual");
        savedQuestion.Rubric.Should().NotBeNull();
        savedQuestion.Rubric.TotalMaxScore.Should().Be(10.00m);
        savedQuestion.Rubric.Criteria.Should().HaveCount(2);
        savedQuestion.Rubric.Criteria.Sum(c => c.MaxScore).Should().Be(10.00m);
    }

    [Fact(DisplayName = "2. Handle tạo thành công PracticeQuestion khi UsageScope là 'practice'")]
    public async Task Handle_Should_Create_PracticeQuestion_When_UsageScope_Is_Practice()
    {
        using var context = CreateDbContext();
        var (lecturer, course) = await SeedUserAndCourseAsync(context);
        var handler = new CreateDraftQuestionCommandHandler(context);

        var command = CreateCommand(course.Id, lecturer.Id, usageScope: "practice");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        var savedQuestion = await context.PracticeQuestions
            .Include(q => q.Rubric)
            .FirstOrDefaultAsync(q => q.Id == result.Value);

        savedQuestion.Should().NotBeNull();
        savedQuestion!.CourseId.Should().Be(course.Id);
        savedQuestion.HasFollowUp.Should().BeTrue();
        savedQuestion.Rubric.Should().NotBeNull();
    }

    [Fact(DisplayName = "3. Handle trả về Failure khi Course không tồn tại trong hệ thống")]
    public async Task Handle_Should_Return_Failure_When_Course_Does_Not_Exist()
    {
        using var context = CreateDbContext();
        var (lecturer, _) = await SeedUserAndCourseAsync(context);
        var handler = new CreateDraftQuestionCommandHandler(context);

        var nonExistentCourseId = Guid.NewGuid();
        var command = CreateCommand(nonExistentCourseId, lecturer.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Môn học không tồn tại.");
    }

    [Fact(DisplayName = "4. Handle trả về Failure khi User không có quyền biên soạn câu hỏi (ví dụ role 'student')")]
    public async Task Handle_Should_Return_Failure_When_User_Is_Student()
    {
        using var context = CreateDbContext();
        var (student, course) = await SeedUserAndCourseAsync(context, role: "student");
        var handler = new CreateDraftQuestionCommandHandler(context);

        var command = CreateCommand(course.Id, student.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Người dùng không có quyền biên soạn câu hỏi.");
    }

    [Fact(DisplayName = "5. Handle trả về Failure khi Barem Rubric có ít hơn 2 tiêu chí con")]
    public async Task Handle_Should_Return_Failure_When_Rubric_Has_Fewer_Than_Two_Criteria()
    {
        using var context = CreateDbContext();
        var (lecturer, course) = await SeedUserAndCourseAsync(context);
        var handler = new CreateDraftQuestionCommandHandler(context);

        var command = CreateCommand(course.Id, lecturer.Id, criteriaCount: 1);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Barem Rubric phải có tối thiểu 2 tiêu chí đánh giá con.");
    }

    [Fact(DisplayName = "6. RÀNG BUỘC BẤT BIẾN: Handle trả về Failure khi tổng điểm Barem Rubric lệch 10.00 điểm")]
    public async Task Handle_Should_Return_Failure_When_Rubric_Total_Score_Is_Not_10_00()
    {
        using var context = CreateDbContext();
        var (lecturer, course) = await SeedUserAndCourseAsync(context);
        var handler = new CreateDraftQuestionCommandHandler(context);

        var command = CreateCommand(course.Id, lecturer.Id, totalRubricScore: 9.00m);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Tổng điểm các tiêu chí con trong Barem Rubric bắt buộc phải bằng chính xác 10.00 điểm.");
    }

    [Fact(DisplayName = "7. Handle trả về Failure khi Model Answer được cung cấp nhưng ngắn hơn 50 ký tự")]
    public async Task Handle_Should_Return_Failure_When_SampleAnswer_Is_Under_50_Characters()
    {
        using var context = CreateDbContext();
        var (lecturer, course) = await SeedUserAndCourseAsync(context);
        var handler = new CreateDraftQuestionCommandHandler(context);

        var command = CreateCommand(course.Id, lecturer.Id, sampleAnswer: "Câu trả lời quá ngắn dưới 50 ký tự.");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Câu trả lời mẫu (Model Answer) khi cung cấp bắt buộc phải từ 50 ký tự trở lên.");
    }
}
