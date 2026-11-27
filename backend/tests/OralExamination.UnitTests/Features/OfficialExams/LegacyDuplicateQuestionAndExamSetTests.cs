using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Features.OfficialExams.Commands.ApproveExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.CreateExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.HandOverExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitExamSetForReview;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public class LegacyDuplicateQuestionAndExamSetTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "EXAMSET-01: Tạo bộ đề mới ở trạng thái DRAFT và gửi duyệt SUBMITTED_FOR_REVIEW")]
    public async Task Create_And_Submit_ExamSet_Should_Succeed()
    {
        await using var context = CreateDbContext();

        var courseId = Guid.NewGuid();
        var structureId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();

        context.Users.Add(new User { Id = lecturerId, Email = "lecturer@examsets.test", FullName = "Lecturer", Role = UserRole.Lecturer, IsActive = true });
        context.Courses.Add(new Course { Id = courseId, Code = "PRN231", Name = "Cross-Platform", IsActive = true });
        context.CourseLecturers.Add(new CourseLecturer
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            LecturerUserId = lecturerId,
            AssignedByUserId = lecturerId,
            IsActive = true
        });
        context.ExamStructures.Add(new ExamStructure { Id = structureId, CourseId = courseId, Name = "Structure 1", TotalQuestions = 2, IsActive = true });

        var q1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = courseId, RubricId = Guid.NewGuid(), Title = "Q1", Content = "Content 1", ApprovalStatus = ApprovalStatus.Draft, IsActive = true };
        var q2 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = courseId, RubricId = Guid.NewGuid(), Title = "Q2", Content = "Content 2", ApprovalStatus = ApprovalStatus.Draft, IsActive = true };
        context.ExamQuestions.AddRange(q1, q2);
        await context.SaveChangesAsync();

        // 1. Create ExamSet
        var createHandler = new CreateExamSetCommandHandler(context);
        var createResult = await createHandler.Handle(new CreateExamSetCommand(
            structureId,
            courseId,
            null,
            "PRN231-SET01",
            [q1.Id, q2.Id],
            lecturerId,
            UserRole.Lecturer), CancellationToken.None);

        createResult.IsSuccess.Should().BeTrue();
        createResult.Value.Status.Should().Be(ExamSetStatus.Draft);
        createResult.Value.Questions.Should().HaveCount(2);

        // 2. Submit for review
        var submitHandler = new SubmitExamSetForReviewCommandHandler(context);
        var submitResult = await submitHandler.Handle(new SubmitExamSetForReviewCommand(
            createResult.Value.Id,
            lecturerId,
            UserRole.Lecturer), CancellationToken.None);

        submitResult.IsSuccess.Should().BeTrue();

        var updatedSet = await context.ExamSets
            .Include(es => es.ExamSetQuestions)
                .ThenInclude(esq => esq.ExamQuestion)
            .FirstOrDefaultAsync(es => es.Id == createResult.Value.Id);

        updatedSet.Should().NotBeNull();
        updatedSet!.Status.Should().Be(ExamSetStatus.SubmittedForReview);
        updatedSet.SubmittedAt.Should().NotBeNull();
        updatedSet.ExamSetQuestions.All(esq => esq.ExamQuestion.ApprovalStatus == ApprovalStatus.SubmittedForReview).Should().BeTrue();
    }

    [Fact(DisplayName = "EXAMSET-02: Phê duyệt OFFICIAL cascade duyệt câu hỏi và cắm cờ câu hỏi trùng")]
    public async Task Approve_ExamSet_Should_Cascade_Approval_And_Flag_Duplicates()
    {
        await using var context = CreateDbContext();

        var courseId = Guid.NewGuid();
        var structureId = Guid.NewGuid();
        var headId = Guid.NewGuid();

        context.Users.Add(new User { Id = headId, Email = "head@examsets.test", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true });
        context.Courses.Add(new Course { Id = courseId, Code = "CSD201", Name = "Data Structures", IsActive = true, DepartmentHeadUserId = headId });
        context.ExamStructures.Add(new ExamStructure { Id = structureId, CourseId = courseId, Name = "Structure 1", IsActive = true });

        var originalApproverId = Guid.NewGuid();
        var originalApprovedAt = DateTime.UtcNow.AddDays(-1);
        var uniqueRubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Name = "Rubric Dijkstra 10đ",
            TotalMaxScore = 10.00m,
            IsActive = true
        };
        uniqueRubric.Criteria.Add(new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = uniqueRubric.Id,
            CriterionName = "Nguyên lý thuật toán",
            MaxScore = 5.00m,
            Weight = 0.50m,
            OrderIndex = 1
        });
        uniqueRubric.Criteria.Add(new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = uniqueRubric.Id,
            CriterionName = "Độ phức tạp & minh họa",
            MaxScore = 5.00m,
            Weight = 0.50m,
            OrderIndex = 2
        });
        context.Rubrics.Add(uniqueRubric);

        // Câu hỏi dùng chung (đã APPROVED từ trước — không bị ghi đè ApprovedBy/ApprovedAt, sẽ bị cắm cờ trùng lặp)
        var sharedQuestion = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            RubricId = uniqueRubric.Id,
            Title = "Câu hỏi chung AVL Tree",
            Content = "Nêu nguyên lý cân bằng cây AVL.",
            SampleAnswer = "Cây AVL duy trì hệ số cân bằng giữa cây con trái và phải trong khoảng [-1, 1] bằng phép xoay đơn và xoay kép.",
            ApprovalStatus = ApprovalStatus.Approved,
            ApprovedBy = originalApproverId,
            ApprovedAt = originalApprovedAt,
            IsActive = true
        };
        var uniqueQuestion = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            RubricId = uniqueRubric.Id,
            Title = "Câu hỏi riêng Dijkstra",
            Content = "Thuật toán tìm đường đi ngắn nhất.",
            SampleAnswer = "Thuật toán Dijkstra sử dụng hàng đợi ưu tiên chọn đỉnh có khoảng cách tạm thời nhỏ nhất và cập nhật cạnh kề với trọng số không âm.",
            ApprovalStatus = ApprovalStatus.Draft,
            IsActive = false
        };
        context.ExamQuestions.AddRange(sharedQuestion, uniqueQuestion);

        // Bộ đề cũ đã OFFICIAL chứa sharedQuestion
        var existingSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            StructureId = structureId,
            SetCode = "CSD201-SET01",
            Status = ExamSetStatus.Official,
            IsActive = true
        };
        existingSet.ExamSetQuestions.Add(new ExamSetQuestion
        {
            Id = Guid.NewGuid(),
            ExamSetId = existingSet.Id,
            ExamQuestionId = sharedQuestion.Id,
            OrderIndex = 1
        });
        context.ExamSets.Add(existingSet);

        // Bộ đề mới SUBMITTED_FOR_REVIEW cũng chứa sharedQuestion
        var newSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            StructureId = structureId,
            SetCode = "CSD201-SET02",
            Status = ExamSetStatus.SubmittedForReview,
            IsActive = true
        };
        newSet.ExamSetQuestions.Add(new ExamSetQuestion
        {
            Id = Guid.NewGuid(),
            ExamSetId = newSet.Id,
            ExamQuestionId = sharedQuestion.Id,
            OrderIndex = 1
        });
        newSet.ExamSetQuestions.Add(new ExamSetQuestion
        {
            Id = Guid.NewGuid(),
            ExamSetId = newSet.Id,
            ExamQuestionId = uniqueQuestion.Id,
            OrderIndex = 2
        });
        context.ExamSets.Add(newSet);
        await context.SaveChangesAsync();

        // Approve new set
        var approveHandler = new ApproveExamSetCommandHandler(context);
        var approveResult = await approveHandler.Handle(new ApproveExamSetCommand(
            newSet.Id,
            "Đã kiểm tra barem và duyệt chính thức.",
            headId,
            UserRole.DepartmentHead), CancellationToken.None);

        approveResult.IsSuccess.Should().BeTrue();
        approveResult.Value.Status.Should().Be(ExamSetStatus.Official);
        approveResult.Value.QuestionsCascadedCount.Should().Be(1);

        // Phát hiện trùng lặp
        approveResult.Value.HasDuplicates.Should().BeTrue();
        approveResult.Value.DuplicateQuestionCount.Should().Be(1);
        approveResult.Value.DuplicateWarnings.Should().HaveCount(1);
        approveResult.Value.DuplicateWarnings[0].Should().Contain("Câu hỏi chung AVL Tree");
        approveResult.Value.DuplicateWarnings[0].Should().Contain("CSD201-SET01");

        // Câu hỏi đã APPROVED trước đó không bị ghi đè ApprovedBy/ApprovedAt
        var reloadedShared = await context.ExamQuestions.FindAsync(sharedQuestion.Id);
        reloadedShared!.ApprovedBy.Should().Be(originalApproverId);
        reloadedShared.ApprovedAt.Should().Be(originalApprovedAt);

        // Câu hỏi uniqueQuestion được cascade thành APPROVED
        var reloadedUnique = await context.ExamQuestions.FindAsync(uniqueQuestion.Id);
        reloadedUnique!.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        reloadedUnique.ApprovedBy.Should().Be(headId);
        reloadedUnique.ApprovedAt.Should().NotBeNull();
        reloadedUnique.ApprovedAt.Should().Be((await context.ExamSets.FindAsync(newSet.Id))!.ApprovedAt);
    }

    [Fact(DisplayName = "EXAMSET-03: Bàn giao bộ đề chính thức sang phòng Khảo thí (HANDED_OVER)")]
    public async Task HandOver_ExamSet_Should_Transition_To_HandedOver()
    {
        await using var context = CreateDbContext();

        var headId = Guid.NewGuid();
        var examSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            StructureId = Guid.NewGuid(),
            SetCode = "PRN231-OFFICIAL-01",
            Status = ExamSetStatus.Official,
            IsActive = true
        };
        context.ExamSets.Add(examSet);
        context.Users.Add(new User { Id = headId, Email = "head@examsets.test", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true });
        context.Courses.Add(new Course { Id = examSet.CourseId, Code = "PRN231", Name = "Owned course", DepartmentHeadUserId = headId });
        await context.SaveChangesAsync();

        var handler = new HandOverExamSetCommandHandler(context);
        var result = await handler.Handle(new HandOverExamSetCommand(examSet.Id, headId, UserRole.DepartmentHead), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var reloaded = await context.ExamSets.FindAsync(examSet.Id);
        reloaded!.Status.Should().Be(ExamSetStatus.HandedOver);
        reloaded.HandedOverAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "EXAMSET-04: Từ chối bàn giao bộ đề chưa đạt trạng thái OFFICIAL")]
    public async Task HandOver_Draft_ExamSet_Should_Fail()
    {
        await using var context = CreateDbContext();

        var examSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            StructureId = Guid.NewGuid(),
            SetCode = "DRAFT-SET",
            Status = ExamSetStatus.Draft,
            IsActive = true
        };
        var head = new User { Email = "head@examsets.test", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        context.Users.Add(head);
        context.Courses.Add(new Course { Id = examSet.CourseId, Code = "DRAFT", Name = "Owned course", DepartmentHeadUserId = head.Id });
        context.ExamSets.Add(examSet);
        await context.SaveChangesAsync();

        var handler = new HandOverExamSetCommandHandler(context);
        var result = await handler.Handle(new HandOverExamSetCommand(examSet.Id, head.Id, UserRole.DepartmentHead), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("OFFICIAL");
    }

    [Fact(DisplayName = "EXAMSET-05: Admin bị chặn FORBIDDEN khi tạo hoặc gửi duyệt bộ đề (MF-03/MF-04)")]
    public async Task Create_And_Submit_ExamSet_Admin_IsForbidden()
    {
        await using var context = CreateDbContext();

        var courseId = Guid.NewGuid();
        var structureId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var examSetId = Guid.NewGuid();

        context.Users.Add(new User { Id = adminId, Email = "admin@examsets.test", FullName = "Admin", Role = UserRole.Admin, IsActive = true });
        context.Courses.Add(new Course { Id = courseId, Code = "PRN231", Name = "Cross-Platform", IsActive = true });
        context.ExamStructures.Add(new ExamStructure { Id = structureId, CourseId = courseId, Name = "Structure 1", IsActive = true });
        var q1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = courseId, RubricId = Guid.NewGuid(), Title = "Q1", Content = "Content 1", ApprovalStatus = ApprovalStatus.Draft, IsActive = true };
        context.ExamQuestions.Add(q1);
        context.ExamSets.Add(new ExamSet
        {
            Id = examSetId,
            CourseId = courseId,
            StructureId = structureId,
            SetCode = "SET-ADMIN-TEST",
            Status = ExamSetStatus.Draft,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var createHandler = new CreateExamSetCommandHandler(context);
        var submitHandler = new SubmitExamSetForReviewCommandHandler(context);

        foreach (var badRole in new string?[] { UserRole.Admin, null, string.Empty, "   ", UserRole.Lecturer })
        {
            var createResult = await createHandler.Handle(new CreateExamSetCommand(
                structureId,
                courseId,
                null,
                "PRN231-ADMIN",
                [q1.Id],
                adminId,
                badRole), CancellationToken.None);
            createResult.IsSuccess.Should().BeFalse();
            createResult.Error.Should().Be(OralExamination.Application.Features.OfficialExams.OfficialExamErrors.Forbidden);

            var submitResult = await submitHandler.Handle(
                new SubmitExamSetForReviewCommand(examSetId, adminId, badRole), CancellationToken.None);
            submitResult.IsSuccess.Should().BeFalse();
            submitResult.Error.Should().Be(OralExamination.Application.Features.OfficialExams.OfficialExamErrors.Forbidden);
        }

        // Direct controller invocation must map Forbidden -> 403 ProblemDetails (never 422)
        var senderMock = new Moq.Mock<MediatR.ISender>();
        senderMock
            .Setup(s => s.Send(Moq.It.IsAny<CreateExamSetCommand>(), Moq.It.IsAny<CancellationToken>()))
            .ReturnsAsync(OralExamination.Application.Common.Models.Result<OralExamination.Application.Features.OfficialExams.DTOs.ExamSetDto>.Failure(
                OralExamination.Application.Features.OfficialExams.OfficialExamErrors.Forbidden));
        var controller = new global::API.Controllers.v1.OfficialExamsController(senderMock.Object)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
                    {
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, adminId.ToString()),
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, UserRole.Admin)
                    }, "TestAuth"))
                }
            }
        };

        var actionResult = await controller.CreateExamSet(new OralExamination.Application.Features.OfficialExams.DTOs.CreateExamSetRequest
        {
            StructureId = structureId,
            CourseId = courseId,
            SetCode = "SET-ADMIN-CTRL",
            QuestionIds = [q1.Id]
        }, CancellationToken.None);
        var forbiddenResult = actionResult.Should().BeOfType<Microsoft.AspNetCore.Mvc.ObjectResult>().Subject;
        forbiddenResult.StatusCode.Should().Be(Microsoft.AspNetCore.Http.StatusCodes.Status403Forbidden);
    }
}
