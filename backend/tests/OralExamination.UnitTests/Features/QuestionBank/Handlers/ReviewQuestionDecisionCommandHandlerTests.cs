using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.QuestionBank.Handlers;

public class ReviewQuestionDecisionCommandHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(User Reviewer, Course Course)> SeedReviewerAndCourseAsync(
        OralExamDbContext context,
        string role = "department_head",
        bool isActive = true)
    {
        var reviewer = new User
        {
            Id = Guid.NewGuid(),
            Email = "head.reviewer@fpt.edu.vn",
            FullName = "Trưởng Bộ Môn Thẩm Định",
            Role = role,
            IsActive = isActive,
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

        context.Users.Add(reviewer);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        return (reviewer, course);
    }

    private ExamQuestion CreateExamQuestion(
        Guid courseId,
        string sampleAnswer = "Đây là câu trả lời mẫu đạt chuẩn tối thiểu 50 ký tự cho câu hỏi thi vấn đáp phần mềm.",
        decimal rubricTotal = 10.00m,
        int criteriaCount = 2,
        string approvalStatus = ApprovalStatus.SubmittedForReview)
    {
        var rubric = new Rubric
        {
            CourseId = courseId,
            Name = "Barem Rubric Chuẩn 10đ",
            TotalMaxScore = rubricTotal,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        decimal scorePerCriterion = rubricTotal / criteriaCount;
        for (int i = 1; i <= criteriaCount; i++)
        {
            rubric.Criteria.Add(new RubricCriterion
            {
                CriterionName = $"Tiêu chí {i}",
                MaxScore = scorePerCriterion,
                Weight = 100m / criteriaCount,
                BloomLevel = "understand",
                OrderIndex = i,
                CreatedAt = DateTime.UtcNow
            });
        }

        return new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Rubric = rubric,
            Title = "Câu hỏi thi thẩm định PRN231",
            Content = "Phân tích nguyên lý Dependency Inversion trong kiến trúc 4 tầng Clean Architecture.",
            SampleAnswer = sampleAnswer,
            KeyPoints = "[\"Inversion of Control\", \"Abstractions\", \"Decoupling\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Analyze,
            Source = "manual",
            ApprovalStatus = approvalStatus,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    [Fact(DisplayName = "1. Handle xử lý phê duyệt (APPROVED) thành công: cập nhật ApprovedBy và trạng thái APPROVED")]
    public async Task Handle_Should_Approve_Question_When_Decision_Is_APPROVED_And_Criteria_Valid()
    {
        using var context = CreateDbContext();
        var (reviewer, course) = await SeedReviewerAndCourseAsync(context, role: "department_head");
        var handler = new ReviewQuestionDecisionCommandHandler(context);

        var question = CreateExamQuestion(course.Id);
        context.ExamQuestions.Add(question);
        await context.SaveChangesAsync();

        var command = new ReviewQuestionDecisionCommand(
            QuestionId: question.Id,
            ReviewerId: reviewer.Id,
            Decision: ApprovalStatus.Approved,
            ReviewNotes: "Bộ Môn đồng ý phê duyệt câu hỏi đưa vào kho đề thi chính thức.",
            TargetUsageScope: "official_exam"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        result.Value.ReviewedBy.Should().Be(reviewer.Id);

        // Kiểm tra trong DB
        var dbQuestion = await context.ExamQuestions.FindAsync(question.Id);
        dbQuestion.Should().NotBeNull();
        dbQuestion!.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        dbQuestion.ApprovedBy.Should().Be(reviewer.Id);
        dbQuestion.ReviewNotes.Should().Be(command.ReviewNotes);
        dbQuestion.IsActive.Should().BeTrue();
    }

    [Fact(DisplayName = "2. Handle xử lý yêu cầu chỉnh sửa (NEEDS_REVISION): cập nhật trạng thái và ghi chú thẩm định")]
    public async Task Handle_Should_Set_Status_To_NEEDS_REVISION_When_Decision_Is_NEEDS_REVISION()
    {
        using var context = CreateDbContext();
        var (reviewer, course) = await SeedReviewerAndCourseAsync(context, role: "department_head");
        var handler = new ReviewQuestionDecisionCommandHandler(context);

        var question = CreateExamQuestion(course.Id);
        context.ExamQuestions.Add(question);
        await context.SaveChangesAsync();

        var command = new ReviewQuestionDecisionCommand(
            QuestionId: question.Id,
            ReviewerId: reviewer.Id,
            Decision: ApprovalStatus.NeedsRevision,
            ReviewNotes: "Yêu cầu Giảng viên bổ sung thêm ví dụ thực tế vào câu trả lời mẫu.",
            TargetUsageScope: null
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ApprovalStatus.Should().Be(ApprovalStatus.NeedsRevision);

        var dbQuestion = await context.ExamQuestions.FindAsync(question.Id);
        dbQuestion.Should().NotBeNull();
        dbQuestion!.ApprovalStatus.Should().Be(ApprovalStatus.NeedsRevision);
        dbQuestion.ReviewNotes.Should().Be(command.ReviewNotes);
    }

    [Fact(DisplayName = "3. Handle xử lý từ chối (REJECTED): đổi trạng thái sang REJECTED và de-activate câu hỏi")]
    public async Task Handle_Should_Set_Status_To_REJECTED_And_Deactivate_Question_When_Decision_Is_REJECTED()
    {
        using var context = CreateDbContext();
        var (reviewer, course) = await SeedReviewerAndCourseAsync(context, role: "department_head");
        var handler = new ReviewQuestionDecisionCommandHandler(context);

        var question = CreateExamQuestion(course.Id);
        context.ExamQuestions.Add(question);
        await context.SaveChangesAsync();

        var command = new ReviewQuestionDecisionCommand(
            QuestionId: question.Id,
            ReviewerId: reviewer.Id,
            Decision: ApprovalStatus.Rejected,
            ReviewNotes: "Câu hỏi trùng lặp với đề thi học kỳ trước, từ chối không phê duyệt.",
            TargetUsageScope: null
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ApprovalStatus.Should().Be(ApprovalStatus.Rejected);

        var dbQuestion = await context.ExamQuestions.FindAsync(question.Id);
        dbQuestion.Should().NotBeNull();
        dbQuestion!.ApprovalStatus.Should().Be(ApprovalStatus.Rejected);
        dbQuestion.IsActive.Should().BeFalse("Câu hỏi bị từ chối phải bị vô hiệu hóa khỏi kho thi");
    }

    [Fact(DisplayName = "4. Handle trả về Failure khi User thẩm định không phải Trưởng Bộ Môn hay Admin (ví dụ: lecturer)")]
    public async Task Handle_Should_Return_Failure_When_Reviewer_Is_Not_DepartmentHead_Or_Admin()
    {
        using var context = CreateDbContext();
        var (lecturer, course) = await SeedReviewerAndCourseAsync(context, role: "lecturer");
        var handler = new ReviewQuestionDecisionCommandHandler(context);

        var question = CreateExamQuestion(course.Id);
        context.ExamQuestions.Add(question);
        await context.SaveChangesAsync();

        var command = new ReviewQuestionDecisionCommand(
            QuestionId: question.Id,
            ReviewerId: lecturer.Id,
            Decision: ApprovalStatus.Approved,
            ReviewNotes: "Giảng viên tự duyệt đề của mình",
            TargetUsageScope: null
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Chỉ Trưởng Bộ Môn ('department_head') hoặc Quản trị viên mới có quyền");
    }

    [Fact(DisplayName = "5. Handle trả về Failure khi không tìm thấy câu hỏi")]
    public async Task Handle_Should_Return_Failure_When_Question_Not_Found()
    {
        using var context = CreateDbContext();
        var (reviewer, _) = await SeedReviewerAndCourseAsync(context, role: "department_head");
        var handler = new ReviewQuestionDecisionCommandHandler(context);

        var command = new ReviewQuestionDecisionCommand(
            QuestionId: Guid.NewGuid(),
            ReviewerId: reviewer.Id,
            Decision: ApprovalStatus.Approved,
            ReviewNotes: "Duyệt câu hỏi không tồn tại",
            TargetUsageScope: null
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Không tìm thấy câu hỏi tương ứng.");
    }

    [Fact(DisplayName = "6. HARD VERIFICATION GATE: Chặn phê duyệt (APPROVED) khi Model Answer < 50 ký tự")]
    public async Task Handle_Should_Return_Failure_When_Approving_Question_With_Short_Sample_Answer()
    {
        using var context = CreateDbContext();
        var (reviewer, course) = await SeedReviewerAndCourseAsync(context, role: "department_head");
        var handler = new ReviewQuestionDecisionCommandHandler(context);

        var question = CreateExamQuestion(course.Id, sampleAnswer: "Câu trả lời quá ngắn dưới 50 ký tự.");
        context.ExamQuestions.Add(question);
        await context.SaveChangesAsync();

        var command = new ReviewQuestionDecisionCommand(
            QuestionId: question.Id,
            ReviewerId: reviewer.Id,
            Decision: ApprovalStatus.Approved,
            ReviewNotes: "Cố tình phê duyệt câu hỏi thiếu model answer",
            TargetUsageScope: null
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Model Answer < 50 ký tự");
    }

    [Fact(DisplayName = "7. HARD VERIFICATION GATE: Chặn phê duyệt (APPROVED) khi Barem Rubric lệch 10.00 điểm")]
    public async Task Handle_Should_Return_Failure_When_Approving_Question_With_Rubric_Not_10_00()
    {
        using var context = CreateDbContext();
        var (reviewer, course) = await SeedReviewerAndCourseAsync(context, role: "department_head");
        var handler = new ReviewQuestionDecisionCommandHandler(context);

        var question = CreateExamQuestion(course.Id, rubricTotal: 9.00m);
        context.ExamQuestions.Add(question);
        await context.SaveChangesAsync();

        var command = new ReviewQuestionDecisionCommand(
            QuestionId: question.Id,
            ReviewerId: reviewer.Id,
            Decision: ApprovalStatus.Approved,
            ReviewNotes: "Cố tình phê duyệt câu hỏi lệch điểm barem",
            TargetUsageScope: null
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("lệch 10.00đ");
    }
}
