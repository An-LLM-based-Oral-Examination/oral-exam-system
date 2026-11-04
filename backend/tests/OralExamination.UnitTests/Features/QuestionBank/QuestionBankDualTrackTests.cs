using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamSession;
using OralExamination.Application.Features.QuestionBank.Commands.BatchApproveQuestions;
using OralExamination.Application.Features.QuestionBank.Commands.BatchSubmitQuestionsForReview;
using OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;
using OralExamination.Application.Features.QuestionBank.Commands.CreateRubric;
using OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Application.Features.QuestionBank.Queries.GetQuestionById;
using OralExamination.Application.Features.QuestionBank.Queries.GetQuestions;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.QuestionBank;

public sealed class QuestionBankDualTrackTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: $"DualTrackTests_{Guid.NewGuid():N}")
            .Options;

        return new OralExamDbContext(options);
    }

    private static async Task<(User Lecturer, Course Course, CourseClo Clo, ExamStructure Structure)> SeedEntitiesAsync(
        OralExamDbContext context,
        string role = UserRole.Lecturer)
    {
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = $"lecturer.{Guid.NewGuid():N}@fpt.edu.vn",
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

        var clo = new CourseClo
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CloCode = "CLO1",
            Description = "Áp dụng Clean Architecture trong phát triển ứng dụng .NET 8",
            BloomLevel = BloomLevel.Understand,
            OrderIndex = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CreatedBy = lecturer.Id,
            Name = "Ma Trận Đề Thi Thật Cuối Kỳ PRN231",
            TotalQuestions = 5,
            DurationMinutes = 30,
            BloomDistribution = "{\"understand\": 2, \"analyze\": 3}",
            SlotDefinitions = "[]",
            RequiredSetCount = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(lecturer);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.CourseClos.Add(clo);
        context.ExamStructures.Add(structure);
        await context.SaveChangesAsync();

        return (lecturer, course, clo, structure);
    }

    private static CreateDraftQuestionCommand CreateCommand(
        Guid courseId,
        Guid lecturerId,
        string usageScope = "practice",
        Guid? cloId = null,
        Guid? structureId = null,
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
            Rubric: rubric,
            CloId: cloId,
            StructureId: structureId
        );
    }

    [Fact(DisplayName = "1. R8 Tách biệt: Scope 'practice' CHỈ tạo PracticeQuestion, TUYỆT ĐỐI KHÔNG thêm vào ExamQuestions")]
    public async Task Handle_ScopePractice_ShouldOnlyCreatePracticeQuestion_AndNeverExamQuestion()
    {
        using var context = CreateDbContext();
        var (lecturer, course, _, _) = await SeedEntitiesAsync(context);
        var handler = new CreateDraftQuestionCommandHandler(context);

        var command = CreateCommand(course.Id, lecturer.Id, usageScope: "practice");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        var practiceCount = await context.PracticeQuestions.CountAsync();
        var examCount = await context.ExamQuestions.CountAsync();

        practiceCount.Should().Be(1, "vì scope là 'practice' chỉ được tạo PracticeQuestion");
        examCount.Should().Be(0, "vì scope là 'practice' TUYỆT ĐỐI KHÔNG được thêm vào ExamQuestions");

        var createdPq = await context.PracticeQuestions.FirstAsync();
        createdPq.Id.Should().Be(result.Value);
        createdPq.CourseId.Should().Be(course.Id);
        createdPq.Title.Should().Be(command.Title);
    }

    [Theory(DisplayName = "2. R8 Tách biệt: Scope 'exam' hoặc 'official_exam' CHỈ tạo ExamQuestion, TUYỆT ĐỐI KHÔNG thêm vào PracticeQuestions")]
    [InlineData("exam")]
    [InlineData("official_exam")]
    public async Task Handle_ScopeExam_ShouldOnlyCreateExamQuestion_AndNeverPracticeQuestion(string scope)
    {
        using var context = CreateDbContext();
        var (lecturer, course, _, _) = await SeedEntitiesAsync(context);
        var handler = new CreateDraftQuestionCommandHandler(context);

        var command = CreateCommand(course.Id, lecturer.Id, usageScope: scope);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        var examCount = await context.ExamQuestions.CountAsync();
        var practiceCount = await context.PracticeQuestions.CountAsync();

        examCount.Should().Be(1, $"vì scope là '{scope}' chỉ được tạo ExamQuestion");
        practiceCount.Should().Be(0, $"vì scope là '{scope}' TUYỆT ĐỐI KHÔNG được thêm vào PracticeQuestions");

        var createdEq = await context.ExamQuestions.FirstAsync();
        createdEq.Id.Should().Be(result.Value);
        createdEq.CourseId.Should().Be(course.Id);
        createdEq.ApprovalStatus.Should().Be(ApprovalStatus.Draft);
    }

    [Theory(DisplayName = "3. R8 Tách biệt: Scope 'both', 'all', hoặc 'shared' tạo cả ExamQuestion và PracticeQuestion")]
    [InlineData("both")]
    [InlineData("all")]
    [InlineData("shared")]
    public async Task Handle_ScopeBoth_ShouldCreateBothExamAndPracticeQuestions(string scope)
    {
        using var context = CreateDbContext();
        var (lecturer, course, _, _) = await SeedEntitiesAsync(context);
        var handler = new CreateDraftQuestionCommandHandler(context);

        var command = CreateCommand(course.Id, lecturer.Id, usageScope: scope);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        var examCount = await context.ExamQuestions.CountAsync();
        var practiceCount = await context.PracticeQuestions.CountAsync();

        examCount.Should().Be(1, "scope both/shared phải tạo ExamQuestion");
        practiceCount.Should().Be(1, "scope both/shared phải tạo PracticeQuestion");
    }

    [Fact(DisplayName = "4. R8 CloId: Scope 'practice' với CloId gán chính xác CloId cho PracticeQuestion và ánh xạ navigation")]
    public async Task Handle_ScopePractice_WithCloId_ShouldAssignCloIdToPracticeQuestion()
    {
        using var context = CreateDbContext();
        var (lecturer, course, clo, _) = await SeedEntitiesAsync(context);
        var handler = new CreateDraftQuestionCommandHandler(context);

        var command = CreateCommand(course.Id, lecturer.Id, usageScope: "practice", cloId: clo.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var createdPq = await context.PracticeQuestions
            .Include(p => p.Clo)
            .FirstOrDefaultAsync(p => p.Id == result.Value);

        createdPq.Should().NotBeNull();
        createdPq!.CloId.Should().Be(clo.Id);
        createdPq.Clo.Should().NotBeNull();
        createdPq.Clo!.CloCode.Should().Be("CLO1");
    }

    [Fact(DisplayName = "5. R8 CloId & StructureId: Scope 'exam' với CloId và StructureId gán đầy đủ cho ExamQuestion")]
    public async Task Handle_ScopeExam_WithCloIdAndStructureId_ShouldAssignBothToExamQuestion()
    {
        using var context = CreateDbContext();
        var (lecturer, course, clo, structure) = await SeedEntitiesAsync(context);
        var handler = new CreateDraftQuestionCommandHandler(context);

        var command = CreateCommand(
            course.Id,
            lecturer.Id,
            usageScope: "exam",
            cloId: clo.Id,
            structureId: structure.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var createdEq = await context.ExamQuestions
            .Include(e => e.Clo)
            .Include(e => e.Structure)
            .FirstOrDefaultAsync(e => e.Id == result.Value);

        createdEq.Should().NotBeNull();
        createdEq!.CloId.Should().Be(clo.Id);
        createdEq.Clo.Should().NotBeNull();
        createdEq.Clo!.CloCode.Should().Be("CLO1");
        createdEq.StructureId.Should().Be(structure.Id);
        createdEq.Structure.Should().NotBeNull();
        createdEq.Structure!.Name.Should().Be("Ma Trận Đề Thi Thật Cuối Kỳ PRN231");
    }

    [Fact(DisplayName = "6. R8 Domain & DbContext: ExamSet.IsSampleSet mặc định là false và lưu/truy vấn true thành công")]
    public async Task ExamSet_IsSampleSet_DefaultsToFalse_AndCanBePersisted()
    {
        using var context = CreateDbContext();
        var (lecturer, course, _, structure) = await SeedEntitiesAsync(context);

        var defaultSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = structure.Id,
            SetCode = "SET_DEFAULT",
            CreatedBy = lecturer.Id,
            Status = ExamSetStatus.Draft,
            GeneratedAt = DateTime.UtcNow
        };

        defaultSet.IsSampleSet.Should().BeFalse("IsSampleSet phải có giá trị mặc định là false");

        var sampleSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = structure.Id,
            SetCode = "SET_SAMPLE",
            CreatedBy = lecturer.Id,
            Status = ExamSetStatus.Official,
            IsSampleSet = true,
            GeneratedAt = DateTime.UtcNow
        };

        context.ExamSets.AddRange(defaultSet, sampleSet);
        await context.SaveChangesAsync();

        var reloadedSample = await context.ExamSets
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sampleSet.Id);

        reloadedSample.Should().NotBeNull();
        reloadedSample!.IsSampleSet.Should().BeTrue();

        var reloadedDefault = await context.ExamSets
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == defaultSet.Id);

        reloadedDefault.Should().NotBeNull();
        reloadedDefault!.IsSampleSet.Should().BeFalse();
    }

    [Fact(DisplayName = "7. R8 Scorecard DTO: SubmitMockExamSessionCommandHandler ánh xạ CloCode khi câu hỏi có Clo liên kết")]
    public async Task SubmitMockExamSession_ShouldPopulateCloCode_WhenPracticeQuestionHasClo()
    {
        using var context = CreateDbContext();
        var (lecturer, course, clo, _) = await SeedEntitiesAsync(context);

        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Barem Kiến trúc phần mềm",
            TotalMaxScore = 10.0m
        };
        var crit1 = new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "Kiến thức Clean Architecture",
            MaxScore = 5.0m,
            Weight = 0.5m,
            OrderIndex = 1
        };
        var crit2 = new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "Phân tích Trade-off",
            MaxScore = 5.0m,
            Weight = 0.5m,
            OrderIndex = 2
        };
        context.Rubrics.Add(rubric);
        context.RubricCriteria.AddRange(crit1, crit2);

        var questionWithClo = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CloId = clo.Id,
            RubricId = rubric.Id,
            Title = "Câu hỏi có CLO",
            Content = "Trình bày 4 tầng Clean Architecture",
            SampleAnswer = "Domain, Application, Infrastructure, Presentation đạt chuẩn 50 ký tự.",
            Difficulty = "medium",
            BloomLevel = "Understand",
            IsActive = true
        };

        var questionWithoutClo = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CloId = null,
            RubricId = rubric.Id,
            Title = "Câu hỏi không có CLO",
            Content = "Nêu khái niệm Dependency Inversion Principle",
            SampleAnswer = "High level modules should not depend on low level modules đạt chuẩn 50 ký tự.",
            Difficulty = "easy",
            BloomLevel = "Remember",
            IsActive = true
        };

        context.PracticeQuestions.AddRange(questionWithClo, questionWithoutClo);

        var studentId = Guid.NewGuid();
        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.Id,
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-20),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = "in_progress"
        };

        var ans1 = new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = questionWithClo.Id,
            AnswerText = "Domain, Application, Infrastructure và Web API.",
            Status = "answered",
            AnsweredAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var ans2 = new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = questionWithoutClo.Id,
            AnswerText = "DIP đảo ngược chiều phụ thuộc để giảm coupling.",
            Status = "answered",
            AnsweredAt = DateTime.UtcNow.AddMinutes(-5)
        };

        session.Answers.Add(ans1);
        session.Answers.Add(ans2);
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult
            {
                Score = 8.5m,
                ConfidenceScore = 0.95m,
                Feedback = "Đạt yêu cầu.",
                Details = new List<GradingDetailDto>
                {
                    new() { CriterionId = crit1.Id.ToString(), Score = 4.5m, Comment = "Đầy đủ" },
                    new() { CriterionId = crit2.Id.ToString(), Score = 4.0m, Comment = "Tốt" }
                }
            });

        var handler = new SubmitMockExamSessionCommandHandler(context, aiMock.Object);
        var command = new SubmitMockExamSessionCommand(session.Id, studentId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var scorecard = result.Value;
        scorecard.Should().NotBeNull();
        scorecard.Questions.Should().HaveCount(2);

        var scoredQ1 = scorecard.Questions.First(q => q.QuestionId == questionWithClo.Id);
        scoredQ1.CloCode.Should().Be("CLO1", "vì câu hỏi questionWithClo liên kết với CLO1");

        var scoredQ2 = scorecard.Questions.First(q => q.QuestionId == questionWithoutClo.Id);
        scoredQ2.CloCode.Should().BeNull("vì câu hỏi questionWithoutClo không có CloId");
    }

    [Fact(DisplayName = "8. MF-03 Invariant: Barem Rubric bắt buộc có tổng điểm chính xác 10.00 điểm (HTTP 422)")]
    public async Task Handle_Should_Reject_When_Rubric_Total_Not_10_00()
    {
        using var context = CreateDbContext();
        var (lecturer, course, _, _) = await SeedEntitiesAsync(context);
        var handler = new CreateDraftQuestionCommandHandler(context);

        var command = CreateCommand(course.Id, lecturer.Id, totalRubricScore: 9.50m);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("10.00 điểm");
    }

    [Fact(DisplayName = "9. MF-03 Invariant: Model Answer khi được cung cấp bắt buộc phải từ 50 ký tự trở lên")]
    public async Task Handle_Should_Reject_When_ModelAnswer_Is_Under_50_Characters()
    {
        using var context = CreateDbContext();
        var (lecturer, course, _, _) = await SeedEntitiesAsync(context);
        var handler = new CreateDraftQuestionCommandHandler(context);

        var command = CreateCommand(course.Id, lecturer.Id, sampleAnswer: "Dưới 50 ký tự.");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("50 ký tự");
    }

    [Fact(DisplayName = "10. R8 Toàn trình vòng đời Kho Luyện tập độc lập (practice): Tạo DRAFT -> Gửi duyệt -> Truy vấn -> Trưởng BM phê duyệt APPROVED & kích hoạt IsActive")]
    public async Task PracticeQuestion_FullLifecycle_Draft_Submit_Query_Approve_Should_Succeed()
    {
        using var context = CreateDbContext();
        var (lecturer, course, clo, _) = await SeedEntitiesAsync(context, role: UserRole.Lecturer);

        var deptHead = new User
        {
            Id = Guid.NewGuid(),
            Email = $"depthead.{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "Trưởng Bộ Môn Kiểm Thử",
            Role = UserRole.DepartmentHead,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(deptHead);
        await context.SaveChangesAsync();

        // 1. Giảng viên gọi CreateDraftQuestionCommand với UsageScope = "practice"
        var createHandler = new CreateDraftQuestionCommandHandler(context);
        var createCmd = CreateCommand(course.Id, lecturer.Id, usageScope: "practice", cloId: clo.Id);
        var createResult = await createHandler.Handle(createCmd, CancellationToken.None);

        createResult.IsSuccess.Should().BeTrue();
        var questionId = createResult.Value;

        var examCount = await context.ExamQuestions.CountAsync(q => q.Id == questionId);
        examCount.Should().Be(0, "câu hỏi scope 'practice' không được nằm trong ExamQuestions");

        var draftPq = await context.PracticeQuestions.FirstOrDefaultAsync(q => q.Id == questionId);
        draftPq.Should().NotBeNull();
        draftPq!.ApprovalStatus.Should().Be(ApprovalStatus.Draft);
        draftPq.IsActive.Should().BeFalse("câu hỏi mới tạo ở trạng thái DRAFT chưa được kích hoạt");

        // 2. Giảng viên gọi BatchSubmitQuestionsForReviewCommand
        var submitHandler = new BatchSubmitQuestionsForReviewCommandHandler(context);
        var submitCmd = new BatchSubmitQuestionsForReviewCommand(
            CourseId: course.Id,
            LecturerId: lecturer.Id,
            QuestionIds: new List<Guid> { questionId },
            SubmissionNotes: "Đệ trình câu hỏi kho luyện tập R8 lên Trưởng Bộ Môn duyệt.");
        var submitResult = await submitHandler.Handle(submitCmd, CancellationToken.None);

        submitResult.IsSuccess.Should().BeTrue();
        submitResult.Value.SubmittedCount.Should().Be(1);
        submitResult.Value.ApprovalStatus.Should().Be(ApprovalStatus.SubmittedForReview);

        var submittedPq = await context.PracticeQuestions.AsNoTracking().FirstAsync(q => q.Id == questionId);
        submittedPq.ApprovalStatus.Should().Be(ApprovalStatus.SubmittedForReview);
        submittedPq.IsActive.Should().BeFalse();

        // 3. Gọi GetQuestionsQuery (lọc SUBMITTED_FOR_REVIEW) và GetQuestionByIdQuery
        var getListHandler = new GetQuestionsQueryHandler(context);
        var listResult = await getListHandler.Handle(
            new GetQuestionsQuery(CourseId: course.Id, ApprovalStatus: ApprovalStatus.SubmittedForReview),
            CancellationToken.None);

        listResult.IsSuccess.Should().BeTrue();
        listResult.Value.Items.Should().ContainSingle(q => q.Id == questionId && q.ApprovalStatus == ApprovalStatus.SubmittedForReview);

        var getByIdHandler = new GetQuestionByIdQueryHandler(context);
        var detailResult = await getByIdHandler.Handle(new GetQuestionByIdQuery(questionId), CancellationToken.None);

        detailResult.IsSuccess.Should().BeTrue();
        detailResult.Value.Id.Should().Be(questionId);
        detailResult.Value.ApprovalStatus.Should().Be(ApprovalStatus.SubmittedForReview);
        detailResult.Value.IsActive.Should().BeFalse();

        // 4. Trưởng Bộ Môn gọi ReviewQuestionDecisionCommand với Decision = ApprovalStatus.Approved
        var reviewHandler = new ReviewQuestionDecisionCommandHandler(context);
        var reviewCmd = new ReviewQuestionDecisionCommand(
            QuestionId: questionId,
            ReviewerId: deptHead.Id,
            Decision: ApprovalStatus.Approved,
            ReviewNotes: "Đạt chuẩn CLO và Barem 10.0, đồng ý đưa vào kho luyện tập.",
            TargetUsageScope: "practice");
        var reviewResult = await reviewHandler.Handle(reviewCmd, CancellationToken.None);

        reviewResult.IsSuccess.Should().BeTrue();
        reviewResult.Value.QuestionId.Should().Be(questionId);
        reviewResult.Value.ApprovalStatus.Should().Be(ApprovalStatus.Approved);

        var approvedPq = await context.PracticeQuestions.AsNoTracking().FirstAsync(q => q.Id == questionId);
        approvedPq.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        approvedPq.IsActive.Should().BeTrue("sau khi Trưởng Bộ Môn phê duyệt, câu hỏi luyện tập phải được kích hoạt IsActive = true");
        approvedPq.ApprovedBy.Should().Be(deptHead.Id);

        var finalExamCount = await context.ExamQuestions.CountAsync(q => q.Id == questionId);
        finalExamCount.Should().Be(0, "phê duyệt câu hỏi chỉ thuộc kho practice không được tạo bản ghi bên ExamQuestions");
    }

    [Fact(DisplayName = "11. MF-03 & R8: CreateRubric chuẩn hóa Weight, BatchApproveQuestions gán CloId/StructureId và BatchSubmit cho phép DepartmentHead")]
    public async Task CreateRubric_WeightNormalization_BatchApprove_CloAndStructure_And_BatchSubmit_DepartmentHead_Should_Succeed()
    {
        using var context = CreateDbContext();
        var (_, course, clo, structure) = await SeedEntitiesAsync(context, role: UserRole.DepartmentHead);
        var deptHead = await context.Users.FirstAsync(u => u.Role == UserRole.DepartmentHead);

        // 1. CreateRubricCommandHandler chuẩn hóa Weight > 1.00m (vd 60m -> 0.60m) và Weight <= 0m (vd 0m -> MaxScore/10 = 0.40m)
        var createRubricHandler = new CreateRubricCommandHandler(context);
        var rubricRes = await createRubricHandler.Handle(new CreateRubricCommand(
            deptHead.Id,
            course.Id,
            "Barem Chuẩn Hóa Trọng Số",
            "Kiểm thử chuẩn hóa Weight",
            new List<RubricCriterionDraftDto>
            {
                new("Tiêu chí 1", 6.00m, 60m, "Understand", "Mô tả 1", 1),
                new("Tiêu chí 2", 4.00m, 0m, "Analyze", "Mô tả 2", 2)
            }), CancellationToken.None);

        rubricRes.IsSuccess.Should().BeTrue();
        rubricRes.Value.Criteria.Should().HaveCount(2);
        rubricRes.Value.Criteria[0].Weight.Should().Be(0.60m);
        rubricRes.Value.Criteria[1].Weight.Should().Be(0.40m);

        // 2. BatchApproveQuestionsCommandHandler gán CloId/StructureId cho cả scope 'practice' và 'exam'
        var batchApproveHandler = new BatchApproveQuestionsCommandHandler(context);
        var rubricDraft = new RubricDraftDto(
            "Barem Batch Approve",
            "Mô tả",
            10.00m,
            new List<RubricCriterionDraftDto>
            {
                new("TC 1", 5.00m, 0.50m, "Understand", "Mô tả 1", 1),
                new("TC 2", 5.00m, 0.50m, "Apply", "Mô tả 2", 2)
            });

        var approveRes = await batchApproveHandler.Handle(new BatchApproveQuestionsCommand(
            course.Id,
            deptHead.Id,
            new List<ApprovedQuestionItemDto>
            {
                new(
                    Title: "Câu hỏi Luyện tập gắn CLO",
                    Content: "Nội dung câu hỏi luyện tập gắn CLO",
                    Difficulty: "easy",
                    BloomLevel: "Understand",
                    UsageScope: "practice",
                    Source: "manual",
                    ModelAnswer: "Câu trả lời mẫu cho câu hỏi luyện tập đạt độ dài tối thiểu trên 50 ký tự chuẩn.",
                    KeyPoints: new List<string> { "Ý 1" },
                    HasFollowUp: true,
                    FollowUpPrompt: "Follow-up?",
                    Rubric: rubricDraft,
                    CloId: clo.Id),
                new(
                    Title: "Câu hỏi Thi thật gắn CLO và Structure",
                    Content: "Nội dung câu hỏi thi thật gắn CLO và Structure",
                    Difficulty: "hard",
                    BloomLevel: "Analyze",
                    UsageScope: "exam",
                    Source: "manual",
                    ModelAnswer: "Câu trả lời mẫu cho câu hỏi thi thật đạt độ dài tối thiểu trên 50 ký tự chuẩn.",
                    KeyPoints: new List<string> { "Ý 2" },
                    HasFollowUp: false,
                    FollowUpPrompt: null,
                    Rubric: rubricDraft,
                    CloId: clo.Id,
                    StructureId: structure.Id)
            }), CancellationToken.None);

        approveRes.IsSuccess.Should().BeTrue();
        approveRes.Value.TotalApproved.Should().Be(2);
        approveRes.Value.ApprovedQuestions.Should().HaveCount(2);

        var practiceApprovedId = approveRes.Value.ApprovedQuestions[0].QuestionId;
        var examApprovedId = approveRes.Value.ApprovedQuestions[1].QuestionId;

        var savedPq = await context.PracticeQuestions.FirstOrDefaultAsync(p => p.Id == practiceApprovedId);
        savedPq.Should().NotBeNull();
        savedPq!.CloId.Should().Be(clo.Id);

        var savedEq = await context.ExamQuestions.FirstOrDefaultAsync(e => e.Id == examApprovedId);
        savedEq.Should().NotBeNull();
        savedEq!.CloId.Should().Be(clo.Id);
        savedEq.StructureId.Should().Be(structure.Id);

        // 3. BatchSubmitQuestionsForReviewCommandHandler cho phép cả 'department_head' đệ trình câu hỏi
        var createDraftHandler = new CreateDraftQuestionCommandHandler(context);
        var draftIdRes = await createDraftHandler.Handle(
            CreateCommand(course.Id, deptHead.Id, usageScope: "exam", cloId: clo.Id, structureId: structure.Id),
            CancellationToken.None);
        draftIdRes.IsSuccess.Should().BeTrue();

        var batchSubmitHandler = new BatchSubmitQuestionsForReviewCommandHandler(context);
        var submitByHeadRes = await batchSubmitHandler.Handle(new BatchSubmitQuestionsForReviewCommand(
            course.Id,
            deptHead.Id,
            new List<Guid> { draftIdRes.Value },
            "Trưởng Bộ Môn tự đệ trình câu hỏi vào quy trình rà soát."), CancellationToken.None);

        submitByHeadRes.IsSuccess.Should().BeTrue();
        submitByHeadRes.Value.SubmittedCount.Should().Be(1);
    }
}
