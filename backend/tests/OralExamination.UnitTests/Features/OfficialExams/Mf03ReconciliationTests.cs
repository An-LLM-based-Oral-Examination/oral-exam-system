using System.Security.Claims;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.Commands.ApproveExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.CreateExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.CurateExamQuestionBank;
using OralExamination.Application.Features.OfficialExams.Commands.DeleteExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitExamSetForReview;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamSet;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries;
using OralExamination.Application.Features.OfficialExams.Queries.GetAvailableExamSetQuestions;
using OralExamination.Application.Features.OfficialExams.Queries.GetBankCurationCandidates;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamSetById;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamSets;
using OralExamination.Application.Features.QuestionBank.Commands.BatchApproveQuestions;
using OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;
using OralExamination.Application.Features.QuestionBank.Commands.CreateRubric;
using OralExamination.Application.Features.QuestionBank.Commands.MutateQuestion;
using OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;
using OralExamination.Application.Features.QuestionBank.Commands.UpdateRubric;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Application.Features.QuestionBank.Queries.GetQuestions;
using OralExamination.Application.Features.QuestionBank.ReviewRequests;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Domain.Exceptions;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public class Mf03ReconciliationTests
{
    private static OralExamDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase($"mf03-reconcile-{Guid.NewGuid():N}")
            .Options);

    private static RubricDraftDto ValidRubric(string name = "Rubric Chấm Thi") =>
        new(name, "Barem 10 điểm", 10.00m,
        [
            new("Hiểu khái niệm", 5.00m, 0.50m, BloomLevel.Understand, "Trình bày đúng bản chất", 1),
            new("Phân tích tình huống", 5.00m, 0.50m, BloomLevel.Analyze, "Lập luận và ví dụ rõ ràng", 2)
        ]);

    private static async Task<(
        OralExamDbContext Db,
        User Head,
        User LecA,
        User LecB,
        User Officer,
        Course Course,
        CourseClo Clo,
        OfficialExamSession Session,
        Guid StructureId,
        Guid AssignmentAId,
        Guid AssignmentBId)> CreateAssignedPreparationAsync(int requiredSetCount = 2, int reusePercent = 30, int totalQuestions = 1)
    {
        var db = CreateDb();
        var head = new User { Id = Guid.NewGuid(), Email = "head@reconcile.local", FullName = "Trưởng BM", Role = UserRole.DepartmentHead, IsActive = true };
        var lecA = new User { Id = Guid.NewGuid(), Email = "leca@reconcile.local", FullName = "Giảng viên A", Role = UserRole.Lecturer, IsActive = true };
        var lecB = new User { Id = Guid.NewGuid(), Email = "lecb@reconcile.local", FullName = "Giảng viên B", Role = UserRole.Lecturer, IsActive = true };
        var officer = new User { Id = Guid.NewGuid(), Email = "officer@reconcile.local", FullName = "Khảo thí", Role = UserRole.ExaminationOfficer, IsActive = true };
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new(2026, 9, 1), EndDate = new(2026, 12, 31) };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "Building Cross-Platform", SemesterId = semester.Id, DepartmentHeadUserId = head.Id, IsActive = true };
        var clo = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO1", Description = "Design REST API", BloomLevel = BloomLevel.Apply, IsActive = true };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = course.Id, SemesterId = semester.Id, Title = "Final FE", ExamDate = new DateOnly(2026, 12, 20), Status = "scheduled" };

        db.Users.AddRange(head, lecA, lecB, officer);
        db.Semesters.Add(semester);
        db.Courses.Add(course);
        db.CourseClos.Add(clo);
        db.CourseLecturers.AddRange(
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, SemesterId = semester.Id, LecturerUserId = lecA.Id, AssignedByUserId = head.Id, IsActive = true },
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, SemesterId = semester.Id, LecturerUserId = lecB.Id, AssignedByUserId = head.Id, IsActive = true });
        db.OfficialExamSessions.Add(session);
        await db.SaveChangesAsync();

        var workflow = new ExamPreparationCommandHandler(db);
        (await workflow.Handle(new(session.Id, officer.Id, officer.Role, PreparationAction.Request,
            Request: new(requiredSetCount, DateTime.UtcNow.AddDays(10), null)), default)).IsSuccess.Should().BeTrue();
        if (reusePercent != 30)
        {
            (await workflow.Handle(new(session.Id, head.Id, head.Role, PreparationAction.UpdateReuse, ReusePercent: reusePercent), default)).IsSuccess.Should().BeTrue();
        }
        (await workflow.Handle(new(session.Id, head.Id, head.Role, PreparationAction.Configure,
            Blueprint: new("Cấu trúc chuẩn", totalQuestions, 30, 0, totalQuestions, 0, [clo.Id])), default)).IsSuccess.Should().BeTrue();

        var structureId = session.ExamStructureId!.Value;
        (await workflow.Handle(new(session.Id, head.Id, head.Role, PreparationAction.Assign,
            Assignment: new(structureId, lecA.Id, Math.Max(1, requiredSetCount / 2), DateTime.UtcNow.AddDays(5))), default)).IsSuccess.Should().BeTrue();
        (await workflow.Handle(new(session.Id, head.Id, head.Role, PreparationAction.Assign,
            Assignment: new(structureId, lecB.Id, Math.Max(1, requiredSetCount / 2), DateTime.UtcNow.AddDays(5))), default)).IsSuccess.Should().BeTrue();

        var assignmentAId = await db.ExamAuthoringAssignments.Where(x => x.LecturerId == lecA.Id).Select(x => x.Id).SingleAsync();
        var assignmentBId = await db.ExamAuthoringAssignments.Where(x => x.LecturerId == lecB.Id).Select(x => x.Id).SingleAsync();

        return (db, head, lecA, lecB, officer, course, clo, session, structureId, assignmentAId, assignmentBId);
    }

    private static async Task<ExamQuestion> SeedBankQuestionAsync(
        OralExamDbContext db,
        Guid courseId,
        Guid cloId,
        Guid submittedBy,
        string title,
        string content)
    {
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Name = $"Rubric {title}",
            TotalMaxScore = 10m,
            IsActive = true,
            Criteria =
            [
                new RubricCriterion { Id = Guid.NewGuid(), CriterionName = "Tiêu chí 1", Description = "Đúng lý thuyết", MaxScore = 5m, Weight = 0.5m, OrderIndex = 1 },
                new RubricCriterion { Id = Guid.NewGuid(), CriterionName = "Tiêu chí 2", Description = "Có ví dụ minh họa", MaxScore = 5m, Weight = 0.5m, OrderIndex = 2 }
            ]
        };
        var question = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            CloId = cloId,
            RubricId = rubric.Id,
            Rubric = rubric,
            Title = title,
            Content = content,
            SampleAnswer = new string('B', 65),
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            Source = QuestionSource.Manual,
            ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.BankReady,
            SubmittedBy = submittedBy,
            IsActive = true
        };
        db.Rubrics.Add(rubric);
        db.ExamQuestions.Add(question);
        await db.SaveChangesAsync();
        return question;
    }

    [Fact(DisplayName = "M2-01: DeptHead tạo/xem/xóa Sample Set trên cấu trúc PR#47, ẩn câu khỏi kho khả dụng và chặn gán vào bộ đề")]
    public async Task SampleSet_DeptHeadLifecycle_OnAssignedStructure_AndExcludedFromPool()
    {
        var (db, head, lecA, _, _, course, clo, session, structureId, assignmentAId, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var sampleQ = await SeedBankQuestionAsync(db, course.Id, clo.Id, lecA.Id, "Câu mẫu", "Nội dung câu hỏi mẫu tham khảo cho cấu trúc.");
        var bankQ = await SeedBankQuestionAsync(db, course.Id, clo.Id, lecA.Id, "Câu thi thật", "Nội dung câu hỏi thi thật bình thường.");

        var createHandler = new CreateExamSetCommandHandler(db);
        var sampleResult = await createHandler.Handle(new CreateExamSetCommand(
            structureId, course.Id, session.Id, "MAU-PRN231", [sampleQ.Id], head.Id, UserRole.DepartmentHead, IsSampleSet: true), default);
        sampleResult.IsSuccess.Should().BeTrue(sampleResult.Error);
        sampleResult.Value.IsSampleSet.Should().BeTrue();

        // DeptHead & Lecturer đều xem được Sample Set qua GetExamSets & GetExamSetById
        var headList = await new GetExamSetsQueryHandler(db).Handle(new(course.Id, session.Id, null, head.Id, head.Role), default);
        headList.IsSuccess.Should().BeTrue();
        headList.Value.Should().ContainSingle(x => x.Id == sampleResult.Value.Id && x.IsSampleSet);

        var lecList = await new GetExamSetsQueryHandler(db).Handle(new(course.Id, session.Id, null, lecA.Id, lecA.Role), default);
        lecList.IsSuccess.Should().BeTrue();
        lecList.Value.Should().ContainSingle(x => x.Id == sampleResult.Value.Id && x.IsSampleSet);

        var headDetail = await new GetExamSetByIdQueryHandler(db).Handle(new(sampleResult.Value.Id, head.Id, head.Role), default);
        headDetail.IsSuccess.Should().BeTrue();

        // Câu trong Sample Set bị ẩn khỏi GetAvailableExamSetQuestions (nhánh assignment-backed)
        var available = await new GetAvailableExamSetQuestionsQueryHandler(db).Handle(
            new(structureId, lecA.Id, lecA.Role, SessionId: session.Id), default);
        available.IsSuccess.Should().BeTrue(available.Error);
        available.Value.Items.Select(x => x.QuestionId).Should().ContainSingle().Which.Should().Be(bankQ.Id);

        // Chặn giảng viên gắn câu thuộc Sample Set vào bộ đề theo phân công (ExamPaperRules.SetQuestions)
        var assignedWithSample = await createHandler.Handle(new CreateExamSetCommand(
            structureId, course.Id, session.Id, "DE-CHINH-01", [sampleQ.Id], lecA.Id, lecA.Role, assignmentAId), default);
        assignedWithSample.IsSuccess.Should().BeFalse();
        assignedWithSample.Error.Should().StartWith("VALIDATION:");

        // DeptHead xóa Sample Set -> câu hỏi được mở lại trong available pool
        var deleted = await new DeleteExamSetCommandHandler(db).Handle(
            new(sampleResult.Value.Id, head.Id, head.Role), default);
        deleted.IsSuccess.Should().BeTrue(deleted.Error);

        var availableAfterDelete = await new GetAvailableExamSetQuestionsQueryHandler(db).Handle(
            new(structureId, lecA.Id, lecA.Role, SessionId: session.Id), default);
        availableAfterDelete.Value.Items.Select(x => x.QuestionId).Should().BeEquivalentTo([sampleQ.Id, bankQ.Id]);
    }

    [Fact(DisplayName = "M2-02: ArchiveDraft lưu trữ mềm bản nháp theo phân công, ghi lịch sử ARCHIVED và giải phóng SetCode/Quota trong khi DeleteExamSet vẫn chặn 409")]
    public async Task ArchiveDraft_SoftArchivesAssignedDraft_RecordsRevision_WhileLegacyDeleteStillBlocks409()
    {
        var (db, _, lecA, _, _, course, clo, session, structureId, assignmentAId, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 1, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var bankQ = await SeedBankQuestionAsync(db, course.Id, clo.Id, lecA.Id, "Câu ngân hàng", "Nội dung câu hỏi phục vụ kiểm thử xóa nháp.");
        var createHandler = new CreateExamSetCommandHandler(db);
        var created = await createHandler.Handle(new CreateExamSetCommand(
            structureId, course.Id, session.Id, "NHAP-01", [bankQ.Id], lecA.Id, lecA.Role, assignmentAId), default);
        created.IsSuccess.Should().BeTrue(created.Error);

        // Legacy DeleteExamSetCommandHandler vẫn chặn 409 CONFLICT với bộ đề theo phân công
        var legacyDelete = await new DeleteExamSetCommandHandler(db).Handle(
            new(created.Value.Id, lecA.Id, lecA.Role), default);
        legacyDelete.IsSuccess.Should().BeFalse();
        legacyDelete.Error.Should().StartWith("CONFLICT:");

        // ArchiveDraft qua ExamPreparationWorkflow thành công
        var workflow = new ExamPreparationCommandHandler(db);
        var archived = await workflow.Handle(new(created.Value.Id, lecA.Id, lecA.Role, PreparationAction.ArchiveDraft), default);
        archived.IsSuccess.Should().BeTrue(archived.Error);

        var storedSet = await db.ExamSets.Include(x => x.ExamSetQuestions).SingleAsync(x => x.Id == created.Value.Id);
        storedSet.IsActive.Should().BeFalse();
        storedSet.ExamSetQuestions.Should().BeEmpty();

        var revisions = await db.ExamSetRevisions.AsNoTracking()
            .Where(r => r.ExamSetId == created.Value.Id)
            .Select(r => r.Action)
            .ToListAsync();
        revisions.Should().Contain("ARCHIVED");

        // Có thể tạo lại bộ đề mới với cùng SetCode và câu hỏi vừa giải phóng
        var recreated = await createHandler.Handle(new CreateExamSetCommand(
            structureId, course.Id, session.Id, "NHAP-01", [bankQ.Id], lecA.Id, lecA.Role, assignmentAId), default);
        recreated.IsSuccess.Should().BeTrue(recreated.Error);
    }

    [Fact(DisplayName = "M2-03: GetAvailableExamSetQuestions đánh dấu vàng ẩn danh cho GV khác và chặn nộp >70% khi MaxUses > 1")]
    public async Task YellowPendingWarning_IsAnonymous_AndBlocksOver70PercentSubmissionWhenMaxUsesGt1()
    {
        var (db, _, lecA, lecB, _, course, clo, session, structureId, assignmentAId, assignmentBId) =
            await CreateAssignedPreparationAsync(requiredSetCount: 4, reusePercent: 50, totalQuestions: 10);
        await using var _ = db;

        var bankQuestions = new List<ExamQuestion>();
        for (var i = 1; i <= 12; i++)
        {
            bankQuestions.Add(await SeedBankQuestionAsync(db, course.Id, clo.Id, lecA.Id, $"Câu {i}", $"Nội dung câu hỏi ngân hàng số {i} phục vụ thi thử."));
        }

        var createHandler = new CreateExamSetCommandHandler(db);
        var submitHandler = new SubmitExamSetForReviewCommandHandler(db);

        var setA = await createHandler.Handle(new CreateExamSetCommand(
            structureId, course.Id, session.Id, "L1-S1", bankQuestions.Take(10).Select(q => q.Id).ToList(), lecA.Id, lecA.Role, assignmentAId), default);
        setA.IsSuccess.Should().BeTrue(setA.Error);
        (await submitHandler.Handle(new(setA.Value.Id, lecA.Id, lecA.Role), default)).IsSuccess.Should().BeTrue();

        // GV B kiểm tra available-questions thấy 10 câu vàng với nhãn ẩn danh, MaxUses = 2
        var availB = await new GetAvailableExamSetQuestionsQueryHandler(db).Handle(
            new(structureId, lecB.Id, lecB.Role, SessionId: session.Id), default);
        availB.IsSuccess.Should().BeTrue(availB.Error);
        availB.Value.YellowThresholdPercent.Should().Be(70);
        availB.Value.YellowCount.Should().Be(10);
        availB.Value.Items.Count(x => x.IsYellowPendingByOtherLecturer).Should().Be(10);
        availB.Value.Items.Where(x => x.IsYellowPendingByOtherLecturer)
            .Select(x => x.YellowWarningLabel)
            .Distinct()
            .Should().ContainSingle().Which.Should().Be("Đã có người chọn, đang chờ duyệt");

        // GV B tạo đề dùng 8 câu vàng + 2 câu sạch = 80% > 70% -> bị chặn khi nộp duyệt
        var eightYellowTwoClean = bankQuestions.Take(8).Concat(bankQuestions.Skip(10).Take(2)).Select(q => q.Id).ToList();
        var setBTooMuchYellow = await createHandler.Handle(new CreateExamSetCommand(
            structureId, course.Id, session.Id, "L2-S1", eightYellowTwoClean, lecB.Id, lecB.Role, assignmentBId), default);
        setBTooMuchYellow.IsSuccess.Should().BeTrue(setBTooMuchYellow.Error);

        var blockedSubmit = await submitHandler.Handle(new(setBTooMuchYellow.Value.Id, lecB.Id, lecB.Role), default);
        blockedSubmit.IsSuccess.Should().BeFalse();
        blockedSubmit.Error.Should().StartWith("UNPROCESSABLE:");
        blockedSubmit.Error.Should().Contain("70%");

        // Thay thành 7 câu vàng + 2 câu sạch + 1 câu mới (70% <= 70%) -> cho phép nộp duyệt
        var freshQ = await SeedBankQuestionAsync(db, course.Id, clo.Id, lecB.Id, "Câu 13", "Nội dung câu hỏi ngân hàng số 13 hoàn toàn mới.");
        var sevenYellowThreeClean = bankQuestions.Take(7).Concat(bankQuestions.Skip(10).Take(2)).Append(freshQ).Select(q => q.Id).ToList();
        var workflow = new ExamPreparationCommandHandler(db);
        (await workflow.Handle(new(setBTooMuchYellow.Value.Id, lecB.Id, lecB.Role, PreparationAction.SaveDraft,
            Draft: new(sevenYellowThreeClean)), default)).IsSuccess.Should().BeTrue();

        var allowedSubmit = await submitHandler.Handle(new(setBTooMuchYellow.Value.Id, lecB.Id, lecB.Role), default);
        allowedSubmit.IsSuccess.Should().BeTrue(allowedSubmit.Error);
    }

    [Fact(DisplayName = "M2-04: Câu tạo tay IsPaperPrivate PR#47 đi qua ApproveExamSet (IsActive=false) -> Lọc kho kỳ sau (kèm SetCode) & đồng bộ Promote tránh trùng")]
    public async Task Pr47PaperPrivateQuestion_BridgesIntoCourseBankCuration_AndPromotePreventsDuplicates()
    {
        var (db, head, lecA, _, _, course, clo, session, structureId, assignmentAId, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        await SeedBankQuestionAsync(db, course.Id, clo.Id, head.Id, "Câu kho kiến trúc REST",
            "Trình bày nguyên tắc thiết kế RESTful API idempotent và phân biệt PUT với PATCH trong ASP.NET Core.");

        var createDraftHandler = new CreateDraftQuestionCommandHandler(db);
        var private1Id = (await createDraftHandler.Handle(new CreateDraftQuestionCommand(
            course.Id, lecA.Id, "Câu tạo tay 1",
            "Trình bày nguyên tắc thiết kế RESTful API idempotent và phân biệt PUT với PATCH trong ASP.NET Core.",
            new string('C', 65),
            ["Idempotency", "PUT vs PATCH"],
            QuestionDifficulty.Medium, BloomLevel.Apply, QuestionSource.Manual, "exam",
            false, null, ValidRubric("R-Priv1"), null, null, null, null, clo.Id, null, lecA.Id, IsPaperPrivate: true), default)).Value;

        var private2Id = (await createDraftHandler.Handle(new CreateDraftQuestionCommand(
            course.Id, lecA.Id, "Câu tạo tay 2",
            "Phân tích cơ chế Optimistic Concurrency với RowVersion và giao dịch Serializable trong PostgreSQL.",
            new string('D', 65),
            ["RowVersion", "Serializable"],
            QuestionDifficulty.Medium, BloomLevel.Apply, QuestionSource.Manual, "exam",
            false, null, ValidRubric("R-Priv2"), null, null, null, null, clo.Id, null, lecA.Id, IsPaperPrivate: true), default)).Value;

        var stored1Before = await db.ExamQuestions.AsNoTracking().SingleAsync(x => x.Id == private1Id);
        stored1Before.BankCurationStatus.Should().Be(BankCurationStatus.PendingCuration);
        stored1Before.IsActive.Should().BeFalse();

        // Gắn vào 2 bộ đề PR#47, nộp duyệt và TBM duyệt
        var createSet = new CreateExamSetCommandHandler(db);
        var set1 = (await createSet.Handle(new(structureId, course.Id, session.Id, "PRN-S1", [private1Id], lecA.Id, lecA.Role, assignmentAId), default)).Value;
        var set2 = (await createSet.Handle(new(structureId, course.Id, session.Id, "PRN-S2", [private2Id], lecA.Id, lecA.Role, assignmentAId), default)).Value;

        var submit = new SubmitExamSetForReviewCommandHandler(db);
        (await submit.Handle(new(set1.Id, lecA.Id, lecA.Role), default)).IsSuccess.Should().BeTrue();
        (await submit.Handle(new(set2.Id, lecA.Id, lecA.Role), default)).IsSuccess.Should().BeTrue();

        var approve = new ApproveExamSetCommandHandler(db);
        (await approve.Handle(new(set1.Id, "Đạt", head.Id, head.Role), default)).IsSuccess.Should().BeTrue();
        (await approve.Handle(new(set2.Id, "Đạt", head.Id, head.Role), default)).IsSuccess.Should().BeTrue();

        // Sau duyệt: câu nguồn chuyển Approved + giữ IsActive = false + PendingCuration để không rò rỉ trước khi lọc kho
        var stored1Approved = await db.ExamQuestions.AsNoTracking().SingleAsync(x => x.Id == private1Id);
        stored1Approved.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        stored1Approved.IsActive.Should().BeFalse();
        stored1Approved.BankCurationStatus.Should().Be(BankCurationStatus.PendingCuration);

        // Tab "Lọc kho kỳ sau" hiển thị cả 2 câu kèm SourceSetCodes chính xác
        var candidatesQuery = new GetBankCurationCandidatesQueryHandler(db);
        var candidates = await candidatesQuery.Handle(new(course.Id, head.Id, head.Role), default);
        candidates.IsSuccess.Should().BeTrue(candidates.Error);
        candidates.Value.TotalPendingCount.Should().Be(2);

        var cand1 = candidates.Value.Items.Single(x => x.QuestionId == private1Id);
        cand1.SourceSetCodes.Should().Contain("PRN-S1");
        cand1.HighestBand.Should().Be("RED_80_PLUS");

        var cand2 = candidates.Value.Items.Single(x => x.QuestionId == private2Id);
        cand2.SourceSetCodes.Should().Contain("PRN-S2");
        cand2.HighestBand.Should().Be("CLEAR");

        // Promote câu 2 từ drawer (PaperBankPromotionDialog) -> đánh dấu nguồn CuratedIntoBank (không xuất hiện lại ở Lọc kho kỳ sau)
        var copy2Id = set2.Questions.Single().QuestionId;
        var promote = await new PromotePaperQuestionsCommandHandler(db).Handle(
            new(set2.Id, [copy2Id], head.Id, head.Role), default);
        promote.IsSuccess.Should().BeTrue(promote.Error);

        var afterPromote = await candidatesQuery.Handle(new(course.Id, head.Id, head.Role), default);
        afterPromote.Value.Items.Should().ContainSingle(x => x.QuestionId == private1Id);

        // TBM loại câu 1 qua màn Lọc kho kỳ sau
        var curate = await new CurateExamQuestionBankCommandHandler(db).Handle(
            new(course.Id, [private1Id], BankCurationStatus.ExcludedFromBank, "Trùng ngân hàng", head.Id, head.Role), default);
        curate.IsSuccess.Should().BeTrue(curate.Error);

        var afterExclude = await candidatesQuery.Handle(new(course.Id, head.Id, head.Role), default);
        afterExclude.Value.TotalPendingCount.Should().Be(0);
    }

    [Fact(DisplayName = "M2-05: GetQuestionsQueryHandler với BankReadyOnly=true loại bỏ PendingCuration, ExcludedFromBank và IsActive=false nhưng giữ nguyên Question Studio khi null")]
    public async Task GetQuestions_BankReadyOnly_ExcludesNonReadyAndInactiveForDeptHead()
    {
        var (db, head, lecA, _, _, course, clo, _, _, _, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var readyQ = await SeedBankQuestionAsync(db, course.Id, clo.Id, lecA.Id, "Ready Active", "Nội dung câu hỏi sẵn sàng trong kho.");
        var curatedQ = await SeedBankQuestionAsync(db, course.Id, clo.Id, lecA.Id, "Curated Active", "Nội dung câu hỏi đã nạp kho.");
        curatedQ.BankCurationStatus = BankCurationStatus.CuratedIntoBank;

        var inactiveReadyQ = await SeedBankQuestionAsync(db, course.Id, clo.Id, lecA.Id, "Inactive Ready", "Nội dung câu hỏi đã bị vô hiệu hóa.");
        inactiveReadyQ.IsActive = false;

        var pendingQ = await SeedBankQuestionAsync(db, course.Id, clo.Id, lecA.Id, "Pending Curation", "Nội dung câu hỏi tạo tay chờ lọc.");
        pendingQ.IsActive = false;
        pendingQ.BankCurationStatus = BankCurationStatus.PendingCuration;

        var excludedQ = await SeedBankQuestionAsync(db, course.Id, clo.Id, lecA.Id, "Excluded Question", "Nội dung câu hỏi đã bị loại khỏi kho.");
        excludedQ.IsActive = false;
        excludedQ.BankCurationStatus = BankCurationStatus.ExcludedFromBank;

        await db.SaveChangesAsync();

        var handler = new GetQuestionsQueryHandler(db);

        // Question Studio (BankReadyOnly = null) vẫn trả về đủ 5 câu cho Trưởng BM
        var studioResult = await handler.Handle(new GetQuestionsQuery(
            CourseId: course.Id, UsageScope: "exam", ApprovalStatus: ApprovalStatus.Approved,
            ActorId: head.Id, ActorRole: UserRole.DepartmentHead), default);
        studioResult.IsSuccess.Should().BeTrue(studioResult.Error);
        studioResult.Value.Items.Select(x => x.Id).Should().BeEquivalentTo(
            [readyQ.Id, curatedQ.Id, inactiveReadyQ.Id, pendingQ.Id, excludedQ.Id]);

        // Sample Set picker (BankReadyOnly = true) chỉ trả về câu Active + (BankReady | CuratedIntoBank)
        var bankReadyResult = await handler.Handle(new GetQuestionsQuery(
            CourseId: course.Id, UsageScope: "exam", ApprovalStatus: ApprovalStatus.Approved,
            ActorId: head.Id, ActorRole: UserRole.DepartmentHead, BankReadyOnly: true), default);
        bankReadyResult.IsSuccess.Should().BeTrue(bankReadyResult.Error);
        bankReadyResult.Value.Items.Select(x => x.Id).Should().BeEquivalentTo([readyQ.Id, curatedQ.Id]);
    }

    [Fact(DisplayName = "M2-06: ExamSetOverlapEnricher cảnh báo RED_APPROVED ẩn danh cho bộ đề NEEDS_REVISION của giảng viên khi trùng với bộ đề OFFICIAL")]
    public async Task OverlapEnricher_NeedsRevisionSet_ReportsRedApprovedAnonymouslyForLecturer()
    {
        var (db, head, lecA, lecB, _, course, clo, session, structureId, assignmentAId, assignmentBId) =
            await CreateAssignedPreparationAsync(requiredSetCount: 4, reusePercent: 50, totalQuestions: 2);
        await using var _ = db;

        var sharedQ = await SeedBankQuestionAsync(db, course.Id, clo.Id, lecA.Id, "Shared Q", "Nội dung câu hỏi dùng chung giữa 2 giảng viên.");
        var qA = await SeedBankQuestionAsync(db, course.Id, clo.Id, lecA.Id, "Unique A", "Nội dung câu hỏi riêng của giảng viên A.");
        var qB = await SeedBankQuestionAsync(db, course.Id, clo.Id, lecB.Id, "Unique B", "Nội dung câu hỏi riêng của giảng viên B.");
        var createHandler = new CreateExamSetCommandHandler(db);
        var submitHandler = new SubmitExamSetForReviewCommandHandler(db);

        var setA = (await createHandler.Handle(new CreateExamSetCommand(
            structureId, course.Id, session.Id, "L1-OFF", [sharedQ.Id, qA.Id], lecA.Id, lecA.Role, assignmentAId), default)).Value;
        var setB = (await createHandler.Handle(new CreateExamSetCommand(
            structureId, course.Id, session.Id, "L2-REV", [sharedQ.Id, qB.Id], lecB.Id, lecB.Role, assignmentBId), default)).Value;

        var subA = await submitHandler.Handle(new(setA.Id, lecA.Id, lecA.Role), default);
        subA.IsSuccess.Should().BeTrue(subA.Error);
        var subB = await submitHandler.Handle(new(setB.Id, lecB.Id, lecB.Role), default);
        subB.IsSuccess.Should().BeTrue(subB.Error);

        var appA = await new ApproveExamSetCommandHandler(db).Handle(new(setA.Id, "Đạt", head.Id, head.Role), default);
        appA.IsSuccess.Should().BeTrue(appA.Error);
        var reqB = await new ExamPreparationCommandHandler(db).Handle(
            new(setB.Id, head.Id, head.Role, PreparationAction.RequestChanges, Review: new("Cần sửa câu trùng")), default);
        reqB.IsSuccess.Should().BeTrue(reqB.Error);

        var lecBSets = await new GetExamSetsQueryHandler(db).Handle(new(course.Id, session.Id, null, lecB.Id, lecB.Role), default);
        lecBSets.IsSuccess.Should().BeTrue(lecBSets.Error);
        var revSet = lecBSets.Value.Should().ContainSingle(x => x.Id == setB.Id).Subject;
        revSet.Status.Should().Be(ExamSetStatus.NeedsRevision);
        revSet.OverlapSeverity.Should().Be("RED_APPROVED");
        revSet.RedOverlapQuestionCount.Should().Be(1);
        var sharedQuestionDto = revSet.Questions.Single(q => q.SourceQuestionId == sharedQ.Id);
        sharedQuestionDto.OverlapSeverity.Should().Be("RED_APPROVED");
        sharedQuestionDto.DuplicateInSets.Should().BeEmpty();
    }

    [Fact(DisplayName = "M2-07: PromotePaperQuestionsCommandHandler chặn CONFLICT khi câu nguồn đã bị loại EXCLUDED_FROM_BANK")]
    public async Task PromotePaperQuestions_ExcludedFromBankSource_ReturnsConflict()
    {
        var (db, head, lecA, _, _, course, clo, session, structureId, assignmentAId, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var privateId = (await new CreateDraftQuestionCommandHandler(db).Handle(new CreateDraftQuestionCommand(
            course.Id, lecA.Id, "Câu tạo tay bị loại",
            "Phân tích kiến trúc hướng sự kiện và hàng đợi thông điệp trong hệ thống phân tán.",
            new string('E', 65),
            ["Event-driven", "Message queue"],
            QuestionDifficulty.Medium, BloomLevel.Analyze, QuestionSource.Manual, "exam",
            false, null, ValidRubric("R-Excl"), null, null, null, null, clo.Id, null, lecA.Id, IsPaperPrivate: true), default)).Value;

        var examSet = (await new CreateExamSetCommandHandler(db).Handle(new(
            structureId, course.Id, session.Id, "PRN-EXCL", [privateId], lecA.Id, lecA.Role, assignmentAId), default)).Value;
        (await new SubmitExamSetForReviewCommandHandler(db).Handle(new(examSet.Id, lecA.Id, lecA.Role), default)).IsSuccess.Should().BeTrue();
        (await new ApproveExamSetCommandHandler(db).Handle(new(examSet.Id, "Đạt", head.Id, head.Role), default)).IsSuccess.Should().BeTrue();

        var curateResult = await new CurateExamQuestionBankCommandHandler(db).Handle(
            new(course.Id, [privateId], BankCurationStatus.ExcludedFromBank, "Không phù hợp kho chung", head.Id, head.Role), default);
        curateResult.IsSuccess.Should().BeTrue(curateResult.Error);

        var copyId = examSet.Questions.Single().QuestionId;
        var promoteResult = await new PromotePaperQuestionsCommandHandler(db).Handle(
            new(examSet.Id, [copyId], head.Id, head.Role), default);
        promoteResult.IsSuccess.Should().BeFalse();
        promoteResult.Error.Should().StartWith("CONFLICT:");

        var createdCopies = await db.ExamQuestions.AsNoTracking()
            .Where(q => q.OriginQuestionId == privateId && q.IsActive)
            .ToListAsync();
        createdCopies.Should().BeEmpty();
    }

    [Fact(DisplayName = "M2-08: GetBankCurationCandidatesQueryHandler bỏ qua câu hỏi ngân hàng IsActive=false khi đối chiếu độ tương đồng")]
    public async Task BankCurationCandidates_IgnoresInactiveBankQuestions_InSimilarityMatching()
    {
        var (db, head, lecA, _, _, course, clo, session, structureId, assignmentAId, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        const string identicalContent = "Trình bày cơ chế tối ưu hóa truy vấn PostgreSQL bằng Partial Index và GIN Index.";

        var inactiveBankReady = await SeedBankQuestionAsync(db, course.Id, clo.Id, head.Id, "Câu kho đã tắt", identicalContent);
        inactiveBankReady.IsActive = false;

        var inactiveCurated = await SeedBankQuestionAsync(db, course.Id, clo.Id, head.Id, "Câu đã nạp nhưng đã tắt", identicalContent);
        inactiveCurated.BankCurationStatus = BankCurationStatus.CuratedIntoBank;
        inactiveCurated.IsActive = false;
        await db.SaveChangesAsync();

        var privateId = (await new CreateDraftQuestionCommandHandler(db).Handle(new CreateDraftQuestionCommand(
            course.Id, lecA.Id, "Câu tạo tay mới",
            identicalContent,
            new string('F', 65),
            ["Partial Index", "GIN Index"],
            QuestionDifficulty.Medium, BloomLevel.Apply, QuestionSource.Manual, "exam",
            false, null, ValidRubric("R-Sim"), null, null, null, null, clo.Id, null, lecA.Id, IsPaperPrivate: true), default)).Value;

        var examSet = (await new CreateExamSetCommandHandler(db).Handle(new(
            structureId, course.Id, session.Id, "PRN-SIM", [privateId], lecA.Id, lecA.Role, assignmentAId), default)).Value;
        (await new SubmitExamSetForReviewCommandHandler(db).Handle(new(examSet.Id, lecA.Id, lecA.Role), default)).IsSuccess.Should().BeTrue();
        (await new ApproveExamSetCommandHandler(db).Handle(new(examSet.Id, "Đạt", head.Id, head.Role), default)).IsSuccess.Should().BeTrue();

        var candidates = await new GetBankCurationCandidatesQueryHandler(db).Handle(new(course.Id, head.Id, head.Role), default);
        candidates.IsSuccess.Should().BeTrue(candidates.Error);
        var item = candidates.Value.Items.Should().ContainSingle(x => x.QuestionId == privateId).Subject;
        item.HighestBand.Should().Be("CLEAR");
        item.MaxSimilarityPercent.Should().Be(0);
        item.Matches.Should().BeEmpty();
    }

    [Fact(DisplayName = "R7-01: PromotePaperQuestions không tạo 2 dòng trùng trong Question Studio và chặn bật lại câu nguồn đã nạp kho")]
    public async Task PromotePaperQuestions_HidesSupersededOriginFromStudio_AndBlocksOriginReactivation()
    {
        var (db, head, lecA, _, _, course, clo, session, structureId, assignmentAId, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var privateId = (await new CreateDraftQuestionCommandHandler(db).Handle(new CreateDraftQuestionCommand(
            course.Id, lecA.Id, "Câu tạo tay nạp kho",
            "Trình bày sự khác biệt giữa Optimistic Concurrency và Pessimistic Locking trong PostgreSQL.",
            new string('G', 65),
            ["Optimistic", "Pessimistic"],
            QuestionDifficulty.Medium, BloomLevel.Analyze, QuestionSource.Manual, "exam",
            false, null, ValidRubric("R-Prom"), null, null, null, null, clo.Id, null, lecA.Id, IsPaperPrivate: true), default)).Value;

        var examSet = (await new CreateExamSetCommandHandler(db).Handle(new(
            structureId, course.Id, session.Id, "PRN-PROM", [privateId], lecA.Id, lecA.Role, assignmentAId), default)).Value;
        (await new SubmitExamSetForReviewCommandHandler(db).Handle(new(examSet.Id, lecA.Id, lecA.Role), default)).IsSuccess.Should().BeTrue();
        (await new ApproveExamSetCommandHandler(db).Handle(new(examSet.Id, "Đạt", head.Id, head.Role), default)).IsSuccess.Should().BeTrue();

        var copyId = examSet.Questions.Single().QuestionId;
        var promoteHandler = new PromotePaperQuestionsCommandHandler(db);
        var firstPromote = await promoteHandler.Handle(new(examSet.Id, [copyId], head.Id, head.Role), default);
        firstPromote.IsSuccess.Should().BeTrue(firstPromote.Error);

        var bankQuestion = await db.ExamQuestions.AsNoTracking()
            .SingleAsync(q => q.OriginQuestionId == privateId && !q.IsPaperCopy);

        // Question Studio (BankReadyOnly = null) chỉ hiển thị duy nhất bản ngân hàng chính thức, không nhân đôi với câu nguồn
        var studio = await new GetQuestionsQueryHandler(db).Handle(new GetQuestionsQuery(
            CourseId: course.Id, UsageScope: "exam", ApprovalStatus: ApprovalStatus.Approved,
            ActorId: head.Id, ActorRole: UserRole.DepartmentHead), default);
        studio.IsSuccess.Should().BeTrue(studio.Error);
        studio.Value.Items.Select(x => x.Id).Should().ContainSingle().Which.Should().Be(bankQuestion.Id);

        // Chặn Trưởng BM bật lại IsActive=true trên câu nguồn đã bị thay thế bởi bản ngân hàng
        var reactivateOrigin = await new OralExamination.Application.Features.QuestionBank.Commands.MutateQuestion.MutateQuestionCommandHandler(db)
            .Handle(new(privateId, head.Id, head.Role, OralExamination.Application.Features.QuestionBank.Commands.MutateQuestion.QuestionMutationKind.SetActive, Active: true), default);
        reactivateOrigin.IsSuccess.Should().BeFalse();
        reactivateOrigin.Error.Should().StartWith("CONFLICT:");

        // Gọi Promote lần 2 (idempotent) vẫn trả đúng BankQuestionId của bản ngân hàng active
        var secondPromote = await promoteHandler.Handle(new(examSet.Id, [copyId], head.Id, head.Role), default);
        secondPromote.IsSuccess.Should().BeTrue(secondPromote.Error);
        var json = System.Text.Json.JsonSerializer.Serialize(secondPromote.Value);
        json.Should().Contain(bankQuestion.Id.ToString());
        json.Should().Contain("\"AlreadyAdded\":true");
    }

    [Fact(DisplayName = "BE-M1-01: GetExamPreparation loại trừ IsSampleSet khỏi Status, ApprovedSetCount và HandedOverSetCount")]
    public async Task GetExamPreparation_ExcludesSampleSetsFromCountsAndStatus()
    {
        var (db, head, _, _, _, course, clo, session, structureId, _, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var sampleQ = await SeedBankQuestionAsync(db, course.Id, clo.Id, head.Id, "Sample Q", "Sample question content for structure blueprint.");
        var sampleSet = new ExamSet
        {
            StructureId = structureId,
            CourseId = course.Id,
            SessionId = null,
            SetCode = "SAMPLE-01",
            Status = ExamSetStatus.HandedOver,
            IsSampleSet = true,
            IsActive = true,
            CreatedBy = head.Id
        };
        sampleSet.ExamSetQuestions.Add(new ExamSetQuestion
        {
            ExamSetId = sampleSet.Id,
            ExamQuestionId = sampleQ.Id,
            SourceQuestionId = sampleQ.Id,
            IsBankQuestion = true,
            OrderIndex = 1
        });
        db.ExamSets.Add(sampleSet);
        await db.SaveChangesAsync();

        var prepResult = await new GetExamPreparationQueryHandler(db)
            .Handle(new(session.Id, head.Id, UserRole.DepartmentHead), default);
        prepResult.IsSuccess.Should().BeTrue(prepResult.Error);
        var json = System.Text.Json.JsonSerializer.Serialize(prepResult.Value);
        json.Should().Contain("\"ApprovedSetCount\":0");
        json.Should().Contain("\"HandedOverSetCount\":0");
        json.Should().Contain("\"Status\":\"IN_PROGRESS\"");
    }

    [Fact(DisplayName = "BE-M1-02 & BE-M1-07(b): MutateQuestion chặn Update/SoftDelete câu IsPaperPrivate đã gắn qua SourceQuestionId và chặn dùng Rubric copy")]
    public async Task MutateQuestion_BlocksUpdateAndSoftDelete_WhenUsedViaSourceQuestionId_AndExcludesPaperCopyRubrics()
    {
        var (db, _, lecA, _, _, course, clo, session, structureId, assignmentAId, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var privateId = (await new CreateDraftQuestionCommandHandler(db).Handle(new CreateDraftQuestionCommand(
            course.Id, lecA.Id, "Private Q",
            "Trình bày sự khác biệt giữa Optimistic Lock và Pessimistic Lock trong Entity Framework Core.",
            new string('A', 65), ["Lock", "EF Core"],
            QuestionDifficulty.Medium, BloomLevel.Apply, QuestionSource.Manual, "exam",
            false, null, ValidRubric("R-Priv"), null, null, null, null, clo.Id, null, lecA.Id, IsPaperPrivate: true), default)).Value;

        var createdSet = (await new CreateExamSetCommandHandler(db).Handle(new(
            structureId, course.Id, session.Id, "SET-USE-01", [privateId], lecA.Id, lecA.Role, assignmentAId), default)).Value;

        var copyQuestion = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == createdSet.Questions.Single().QuestionId);
        copyQuestion.IsPaperCopy.Should().BeTrue();
        var setLink = await db.ExamSetQuestions.AsNoTracking().SingleAsync(esq => esq.ExamSetId == createdSet.Id);
        setLink.ExamQuestionId.Should().NotBe(privateId);
        setLink.SourceQuestionId.Should().Be(privateId);

        var mutateHandler = new MutateQuestionCommandHandler(db);

        // Even while SET-USE-01 is still Draft, SoftDelete is blocked via SourceQuestionId
        var deleteResult = await mutateHandler.Handle(new(privateId, lecA.Id, lecA.Role, QuestionMutationKind.SoftDelete), default);
        deleteResult.IsSuccess.Should().BeFalse();
        deleteResult.Error.Should().StartWith("CONFLICT:");

        // Once SET-USE-01 is submitted for review, Update via SourceQuestionId is also blocked
        (await new SubmitExamSetForReviewCommandHandler(db).Handle(new(createdSet.Id, lecA.Id, lecA.Role), default))
            .IsSuccess.Should().BeTrue();
        var updateResult = await mutateHandler.Handle(new(privateId, lecA.Id, lecA.Role, QuestionMutationKind.Update,
            Draft: new UpdateQuestionDraftDto("New Title", "New valid question content for update test.", new string('B', 65), ["K1"],
                QuestionDifficulty.Medium, BloomLevel.Apply, ValidRubric("R-New"), clo.Id)), default);
        updateResult.IsSuccess.Should().BeFalse();
        updateResult.Error.Should().StartWith("CONFLICT:");

        // ValidateDraftAsync chặn dùng lại Rubric thuộc bản sao IsPaperCopy
        var otherDraftId = (await new CreateDraftQuestionCommandHandler(db).Handle(new CreateDraftQuestionCommand(
            course.Id, lecA.Id, "Other Draft",
            "Other valid question content before trying copy rubric.",
            new string('D', 65), ["K1"],
            QuestionDifficulty.Medium, BloomLevel.Apply, QuestionSource.Manual, "exam",
            false, null, ValidRubric("R-Other"), null, null, null, null, clo.Id, null, lecA.Id, IsPaperPrivate: false), default)).Value;
        var updateWithCopyRubric = await mutateHandler.Handle(new(otherDraftId, lecA.Id, lecA.Role, QuestionMutationKind.Update,
            Draft: new UpdateQuestionDraftDto("Title", "Other valid content updated.", new string('C', 65), ["K1"],
                QuestionDifficulty.Medium, BloomLevel.Apply, null, clo.Id, RubricId: copyQuestion.RubricId)), default);
        updateWithCopyRubric.IsSuccess.Should().BeFalse();
        updateWithCopyRubric.Error.Should().StartWith("VALIDATION:").And.Contain("Rubric");
    }

    [Fact(DisplayName = "BE-M1-04 & BE-M1-08: ArchiveDraft/SaveDraft trả 409 với owner khi !IsActive, 403 với foreign author, và thu hồi IsPaperCopy mồ côi")]
    public async Task ExamPreparationWorkflow_ArchiveAndSaveDraft_ReturnConflictOrForbidden_AndRetireOrphanPaperCopies()
    {
        var (db, head, lecA, lecB, _, course, clo, session, structureId, assignmentAId, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var q1 = (await SeedBankQuestionAsync(db, course.Id, clo.Id, head.Id, "Bank Q1", "Question one content for archival.")).Id;
        var q2 = (await SeedBankQuestionAsync(db, course.Id, clo.Id, head.Id, "Bank Q2", "Question two content for replacement.")).Id;

        var set = (await new CreateExamSetCommandHandler(db).Handle(new(
            structureId, course.Id, session.Id, "SET-ARCH-01", [q1], lecA.Id, lecA.Role, assignmentAId), default)).Value;
        var firstCopyId = set.Questions.Single().QuestionId;

        // Attached copy still blocks arbitrary direct mutation via OneWayLockException
        var attachedCopy = await db.ExamQuestions.SingleAsync(q => q.Id == firstCopyId);
        attachedCopy.Content = "Direct tamper";
        await db.Invoking(x => x.SaveChangesAsync()).Should().ThrowAsync<OneWayLockException>();
        db.Entry(attachedCopy).Reload();

        var flow = new ExamPreparationCommandHandler(db);

        // SaveDraft replaces q1 with q2 -> firstCopyId is retired (IsActive=false, IsDeleted=true)
        var saveRes = await flow.Handle(new(set.Id, lecA.Id, lecA.Role, PreparationAction.SaveDraft, Draft: new([q2])), default);
        saveRes.IsSuccess.Should().BeTrue(saveRes.Error);
        var retiredFirstCopy = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == firstCopyId);
        retiredFirstCopy.IsActive.Should().BeFalse();
        retiredFirstCopy.IsDeleted.Should().BeTrue();

        var secondCopyId = await db.ExamSetQuestions.Where(x => x.ExamSetId == set.Id).Select(x => x.ExamQuestionId).SingleAsync();

        // Non-owner gets 403 Forbidden on SaveDraft / ArchiveDraft
        var foreignArchive = await flow.Handle(new(set.Id, lecB.Id, lecB.Role, PreparationAction.ArchiveDraft), default);
        foreignArchive.IsSuccess.Should().BeFalse();
        foreignArchive.Error.Should().Be(OfficialExamErrors.Forbidden);

        // Owner ArchiveDraft soft-archives set AND retires secondCopyId
        var archiveRes = await flow.Handle(new(set.Id, lecA.Id, lecA.Role, PreparationAction.ArchiveDraft), default);
        archiveRes.IsSuccess.Should().BeTrue(archiveRes.Error);
        var retiredSecondCopy = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == secondCopyId);
        retiredSecondCopy.IsActive.Should().BeFalse();
        retiredSecondCopy.IsDeleted.Should().BeTrue();

        // Owner acting again on inactive set receives 409 CONFLICT, while non-owner still receives 403 Forbidden
        var ownerRetryArchive = await flow.Handle(new(set.Id, lecA.Id, lecA.Role, PreparationAction.ArchiveDraft), default);
        ownerRetryArchive.IsSuccess.Should().BeFalse();
        ownerRetryArchive.Error.Should().StartWith("CONFLICT:");

        var ownerRetrySave = await flow.Handle(new(set.Id, lecA.Id, lecA.Role, PreparationAction.SaveDraft, Draft: new([q1])), default);
        ownerRetrySave.IsSuccess.Should().BeFalse();
        ownerRetrySave.Error.Should().StartWith("CONFLICT:");

        var foreignOnInactive = await flow.Handle(new(set.Id, lecB.Id, lecB.Role, PreparationAction.SaveDraft, Draft: new([q1])), default);
        foreignOnInactive.IsSuccess.Should().BeFalse();
        foreignOnInactive.Error.Should().Be(OfficialExamErrors.Forbidden);
    }

    [Fact(DisplayName = "BE-M1-05 & BE-M1-06: GetExamSets/ById trả SourceQuestionId fallback, kèm Sample Set cấp structure; AvailableQuestions chuẩn hóa Bloom & ẩn mẫu legacy")]
    public async Task ExamSetQueries_ProjectOriginFallback_IncludeStructureSampleSet_AndNormalizeBloomFilter()
    {
        var (db, head, lecA, _, _, course, clo, session, structureId, assignmentAId, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var sampleBankQ = await SeedBankQuestionAsync(db, course.Id, clo.Id, head.Id, "Sample Origin Q", "Sample origin question text.");
        var availableLowerBloomQ = await SeedBankQuestionAsync(db, course.Id, clo.Id, head.Id, "Lower Bloom Q", "Lowercase bloom level question text.");
        availableLowerBloomQ.BloomLevel = "apply"; // lowercase mismatch vs CourseClo/Structure "Apply"
        await db.SaveChangesAsync();

        // Structure-level Sample Set (SessionId == null) referencing sampleBankQ via OriginQuestionId only (SourceQuestionId == null)
        var legacySampleCopy = new ExamQuestion
        {
            CourseId = course.Id,
            CloId = clo.Id,
            StructureId = structureId,
            RubricId = sampleBankQ.RubricId,
            Title = sampleBankQ.Title,
            Content = sampleBankQ.Content,
            SampleAnswer = sampleBankQ.SampleAnswer,
            Difficulty = sampleBankQ.Difficulty,
            BloomLevel = sampleBankQ.BloomLevel,
            SubmittedBy = head.Id,
            IsPaperCopy = true,
            OriginQuestionId = sampleBankQ.Id,
            BankCurationStatus = BankCurationStatus.ExcludedFromBank
        };
        var sampleSet = new ExamSet
        {
            StructureId = structureId,
            CourseId = course.Id,
            SessionId = null,
            SetCode = "SAMPLE-STRUCT",
            Status = ExamSetStatus.Official,
            IsSampleSet = true,
            IsActive = true,
            CreatedBy = head.Id
        };
        sampleSet.ExamSetQuestions.Add(new ExamSetQuestion
        {
            ExamSetId = sampleSet.Id,
            ExamQuestionId = legacySampleCopy.Id,
            ExamQuestion = legacySampleCopy,
            SourceQuestionId = null,
            IsBankQuestion = true,
            OrderIndex = 1
        });
        db.ExamQuestions.Add(legacySampleCopy);
        db.ExamSets.Add(sampleSet);
        await db.SaveChangesAsync();

        // GetExamSets filtered by SessionId includes structure-level Sample Set and falls back SourceQuestionId to OriginQuestionId
        var setsRes = await new GetExamSetsQueryHandler(db)
            .Handle(new GetExamSetsQuery(CourseId: course.Id, SessionId: session.Id, ActorUserId: head.Id, ActorRole: head.Role), default);
        setsRes.IsSuccess.Should().BeTrue(setsRes.Error);
        var dto = setsRes.Value.Should().ContainSingle(x => x.Id == sampleSet.Id).Subject;
        dto.Questions.Should().ContainSingle().Which.SourceQuestionId.Should().Be(sampleBankQ.Id);

        // GetExamSetById projects SourceQuestionId fallback
        var byIdRes = await new GetExamSetByIdQueryHandler(db)
            .Handle(new GetExamSetByIdQuery(sampleSet.Id, head.Id, head.Role), default);
        byIdRes.IsSuccess.Should().BeTrue(byIdRes.Error);
        byIdRes.Value.Questions.Should().ContainSingle().Which.SourceQuestionId.Should().Be(sampleBankQ.Id);

        // GetAvailableExamSetQuestions: case-insensitive BloomLevel filter ("Apply" vs "apply") & legacy OriginQuestionId sample exclusion
        var availRes = await new GetAvailableExamSetQuestionsQueryHandler(db)
            .Handle(new GetAvailableExamSetQuestionsQuery(
                structureId, lecA.Id, lecA.Role, CloId: clo.Id, BloomLevel: "Apply", SessionId: session.Id), default);
        availRes.IsSuccess.Should().BeTrue(availRes.Error);
        availRes.Value.Items.Select(x => x.QuestionId).Should().Contain(availableLowerBloomQ.Id);
        availRes.Value.Items.Select(x => x.QuestionId).Should().NotContain(sampleBankQ.Id);
    }

    [Fact(DisplayName = "BE-M1-07(a)(c): BatchApproveQuestions gắn cờ IsPaperPrivate đúng trạng thái và QuestionsController.ReviewDecision trả 422 cho INVARIANT/VALIDATION")]
    public async Task BatchApprove_WiresIsPaperPrivate_AndQuestionsControllerMapsInvariantTo422()
    {
        var (db, head, _, _, _, course, clo, _, structureId, _, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var batchHandler = new BatchApproveQuestionsCommandHandler(db);
        var res = await batchHandler.Handle(new BatchApproveQuestionsCommand(
            course.Id,
            head.Id,
            [
                new ApprovedQuestionItemDto(
                    "Private Batch Q", "Content for private batch approved exam question.",
                    QuestionDifficulty.Medium, BloomLevel.Apply, "exam", QuestionSource.Manual,
                    new string('M', 60), ["Key1"], false, null, ValidRubric("R-PrivBatch"),
                    CloId: clo.Id, StructureId: structureId, IsPaperPrivate: true),
                new ApprovedQuestionItemDto(
                    "Public Batch Q", "Content for public bank ready exam question.",
                    QuestionDifficulty.Medium, BloomLevel.Apply, "exam", QuestionSource.Manual,
                    new string('N', 60), ["Key2"], false, null, ValidRubric("R-PubBatch"),
                    CloId: clo.Id, StructureId: structureId, IsPaperPrivate: false)
            ]), default);
        res.IsSuccess.Should().BeTrue(res.Error);
        res.Value.TotalApproved.Should().Be(2);

        var privQ = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Title == "Private Batch Q");
        privQ.BankCurationStatus.Should().Be(BankCurationStatus.PendingCuration);
        privQ.IsActive.Should().BeFalse();

        var pubQ = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Title == "Public Batch Q");
        pubQ.BankCurationStatus.Should().Be(BankCurationStatus.BankReady);
        pubQ.IsActive.Should().BeTrue();

        var senderMock = new Mock<ISender>();
        var controller = new QuestionsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, head.Id.ToString()),
                        new Claim(ClaimTypes.Role, UserRole.DepartmentHead)
                    ], "Test"))
                }
            }
        };

        senderMock.Setup(s => s.Send(It.IsAny<ReviewQuestionDecisionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<QuestionReviewDecisionResponseDto>.Failure("INVARIANT: Tổng điểm rubric phải bằng 10.0."));
        var invResp = await controller.ReviewDecision(pubQ.Id, new QuestionReviewDecisionRequestDto("APPROVED"));
        invResp.Should().BeOfType<UnprocessableEntityObjectResult>().Which.StatusCode.Should().Be(422);

        senderMock.Setup(s => s.Send(It.IsAny<ReviewQuestionDecisionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<QuestionReviewDecisionResponseDto>.Failure("NOT_FOUND: Không tìm thấy câu hỏi."));
        var nfResp = await controller.ReviewDecision(pubQ.Id, new QuestionReviewDecisionRequestDto("APPROVED"));
        nfResp.Should().BeOfType<NotFoundObjectResult>().Which.StatusCode.Should().Be(404);
    }

    [Fact(DisplayName = "M1-R2-01: BatchApproveQuestions kiểm định + chuẩn hóa BloomLevel cho cả câu hỏi lẫn tiêu chí rubric, và QuestionsController trả 422")]
    public async Task BatchApprove_ValidatesAndCanonicalizesBloomLevel_ForQuestionAndCriteria()
    {
        var (db, head, _, _, _, course, clo, _, structureId, _, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var batchHandler = new BatchApproveQuestionsCommandHandler(db);

        // 1. Reject invalid >20-char question BloomLevel
        var badQuestionBloom = await batchHandler.Handle(new BatchApproveQuestionsCommand(
            course.Id,
            head.Id,
            [
                new ApprovedQuestionItemDto(
                    "Invalid Q Bloom", "Content for invalid question bloom level check.",
                    QuestionDifficulty.Medium, "INVALID_QUESTION_BLOOM_25", "exam", QuestionSource.Manual,
                    new string('M', 60), ["Key1"], false, null, ValidRubric("R-BadQ"),
                    CloId: clo.Id, StructureId: structureId)
            ]), default);
        badQuestionBloom.IsSuccess.Should().BeFalse();
        badQuestionBloom.Error.Should().StartWith("INVARIANT:");

        // 2. Reject invalid >20-char criterion BloomLevel
        var badCritRubric = new RubricDraftDto("R-BadCrit", null, 10.00m,
        [
            new("Criterion 1", 5.00m, 0.50m, "INVALID_CRITERION_BLOOM_TOO_LONG", "Desc 1", 1),
            new("Criterion 2", 5.00m, 0.50m, BloomLevel.Understand, "Desc 2", 2)
        ]);
        var badCritBloom = await batchHandler.Handle(new BatchApproveQuestionsCommand(
            course.Id,
            head.Id,
            [
                new ApprovedQuestionItemDto(
                    "Invalid Crit Bloom", "Content for invalid criterion bloom level check.",
                    QuestionDifficulty.Medium, BloomLevel.Apply, "practice", QuestionSource.Manual,
                    new string('N', 60), ["Key2"], false, null, badCritRubric,
                    CloId: clo.Id)
            ]), default);
        badCritBloom.IsSuccess.Should().BeFalse();
        badCritBloom.Error.Should().StartWith("INVARIANT:");

        // Nothing persisted on failures
        (await db.ExamQuestions.CountAsync()).Should().Be(0);
        (await db.PracticeQuestions.CountAsync()).Should().Be(0);

        // 3. Canonicalize lowercase / mixed-case valid BloomLevel on both scopes + blank criterion -> null
        var mixedCritRubric = new RubricDraftDto("R-Mixed", null, 10.00m,
        [
            new("Criterion 1", 5.00m, 0.50m, "  analyze ", "Desc 1", 1),
            new("Criterion 2", 5.00m, 0.50m, "   ", "Desc 2", 2)
        ]);
        var validMixed = await batchHandler.Handle(new BatchApproveQuestionsCommand(
            course.Id,
            head.Id,
            [
                new ApprovedQuestionItemDto(
                    "Canonical Exam Q", "Content for canonical bloom on exam scope.",
                    QuestionDifficulty.Medium, "  apply ", "exam", QuestionSource.Manual,
                    new string('E', 60), ["KeyE"], false, null, mixedCritRubric,
                    CloId: clo.Id, StructureId: structureId),
                new ApprovedQuestionItemDto(
                    "Canonical Practice Q", "Content for canonical bloom on practice scope.",
                    QuestionDifficulty.Easy, "evaluate", "practice", QuestionSource.Manual,
                    new string('P', 60), ["KeyP"], false, null, mixedCritRubric,
                    CloId: clo.Id)
            ]), default);
        validMixed.IsSuccess.Should().BeTrue(validMixed.Error);

        var eq = await db.ExamQuestions.Include(q => q.Rubric).ThenInclude(r => r.Criteria)
            .AsNoTracking().SingleAsync(q => q.Title == "Canonical Exam Q");
        eq.BloomLevel.Should().Be(BloomLevel.Apply);
        eq.Rubric.Criteria.OrderBy(c => c.OrderIndex).Select(c => c.BloomLevel)
            .Should().Equal(BloomLevel.Analyze, null);

        var pq = await db.PracticeQuestions.Include(q => q.Rubric).ThenInclude(r => r.Criteria)
            .AsNoTracking().SingleAsync(q => q.Title == "Canonical Practice Q");
        pq.BloomLevel.Should().Be(BloomLevel.Evaluate);
        pq.Rubric.Criteria.OrderBy(c => c.OrderIndex).Select(c => c.BloomLevel)
            .Should().Equal(BloomLevel.Analyze, null);

        // 4. QuestionsController.BatchApprove maps both INVARIANT: and VALIDATION: to HTTP 422
        var senderMock = new Mock<ISender>();
        var controller = new QuestionsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, head.Id.ToString()),
                        new Claim(ClaimTypes.Role, UserRole.DepartmentHead)
                    ], "Test"))
                }
            }
        };
        senderMock.Setup(s => s.Send(It.IsAny<BatchApproveQuestionsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(badCritBloom);
        var invResp = await controller.BatchApprove(new BatchApproveQuestionsRequestDto(course.Id, []));
        invResp.Should().BeOfType<UnprocessableEntityObjectResult>().Which.StatusCode.Should().Be(422);

        senderMock.Setup(s => s.Send(It.IsAny<BatchApproveQuestionsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BatchApproveQuestionsResponseDto>.Failure("VALIDATION: BloomLevel không hợp lệ."));
        var valResp = await controller.BatchApprove(new BatchApproveQuestionsRequestDto(course.Id, []));
        valResp.Should().BeOfType<UnprocessableEntityObjectResult>().Which.StatusCode.Should().Be(422);
    }

    [Theory(DisplayName = "M1-R2-02: ReviewQuestionDecision giữ IsActive=false khi duyệt ExamQuestion PendingCuration/ExcludedFromBank và bật IsActive=true cho BankReady/CuratedIntoBank")]
    [InlineData(BankCurationStatus.PendingCuration, false)]
    [InlineData(BankCurationStatus.ExcludedFromBank, false)]
    [InlineData(BankCurationStatus.BankReady, true)]
    [InlineData(BankCurationStatus.CuratedIntoBank, true)]
    public async Task ReviewQuestionDecision_PreservesInactiveState_ForPendingOrExcludedBankCuration(
        string bankCurationStatus,
        bool expectedIsActive)
    {
        var (db, head, lecA, _, _, course, clo, _, _, _, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var rubric = new Rubric
        {
            CourseId = course.Id,
            Name = "Rubric Review",
            TotalMaxScore = 10.00m,
            IsActive = true,
            Criteria =
            {
                new RubricCriterion { CriterionName = "C1", MaxScore = 5m, Weight = 0.5m, BloomLevel = BloomLevel.Apply, OrderIndex = 1 },
                new RubricCriterion { CriterionName = "C2", MaxScore = 5m, Weight = 0.5m, BloomLevel = BloomLevel.Analyze, OrderIndex = 2 }
            }
        };
        var question = new ExamQuestion
        {
            CourseId = course.Id,
            CloId = clo.Id,
            Rubric = rubric,
            Title = $"Review {bankCurationStatus}",
            Content = "Explain how ReviewQuestionDecision respects BankCurationStatus.",
            SampleAnswer = new string('S', 65),
            KeyPoints = "[\"K1\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            Source = QuestionSource.Manual,
            ApprovalStatus = ApprovalStatus.SubmittedForReview,
            SubmittedBy = lecA.Id,
            BankCurationStatus = bankCurationStatus,
            IsActive = false
        };
        db.Rubrics.Add(rubric);
        db.ExamQuestions.Add(question);
        await db.SaveChangesAsync();

        var res = await new ReviewQuestionDecisionCommandHandler(db).Handle(
            new ReviewQuestionDecisionCommand(question.Id, head.Id, ApprovalStatus.Approved, "Approved", "exam"),
            default);
        res.IsSuccess.Should().BeTrue(res.Error);

        var reloaded = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == question.Id);
        reloaded.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        reloaded.BankCurationStatus.Should().Be(bankCurationStatus);
        reloaded.IsActive.Should().Be(expectedIsActive);
    }

    [Fact(DisplayName = "M1-R2-04: ProtectExamPapersAsync chặn soft-retire IsPaperCopy khi ExamQuestionSubmission mới Added trong cùng SaveChanges UoW")]
    public async Task ProtectExamPapersAsync_BlocksSoftRetire_WhenSubmissionAddedInSameUnitOfWork()
    {
        var (db, head, _, _, _, course, clo, _, structureId, _, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var rubric = new Rubric
        {
            CourseId = course.Id,
            Name = "Rubric Copy",
            TotalMaxScore = 10.00m,
            IsActive = true,
            Criteria =
            {
                new RubricCriterion { CriterionName = "C1", MaxScore = 5m, Weight = 0.5m, OrderIndex = 1 },
                new RubricCriterion { CriterionName = "C2", MaxScore = 5m, Weight = 0.5m, OrderIndex = 2 }
            }
        };
        var paperCopy = new ExamQuestion
        {
            CourseId = course.Id,
            CloId = clo.Id,
            StructureId = structureId,
            Rubric = rubric,
            Title = "Orphan Copy With Pending Submission",
            Content = "Paper copy referenced by same-UoW added submission.",
            SampleAnswer = new string('Q', 60),
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            SubmittedBy = head.Id,
            IsPaperCopy = true,
            IsActive = true,
            IsDeleted = false
        };
        db.Rubrics.Add(rubric);
        db.ExamQuestions.Add(paperCopy);
        await db.SaveChangesAsync();

        // Same UoW: soft-retire paperCopy while adding an ExamQuestionSubmission referencing it
        paperCopy.IsActive = false;
        paperCopy.IsDeleted = true;
        db.ExamQuestionSubmissions.Add(new ExamQuestionSubmission
        {
            TicketId = Guid.NewGuid(),
            ExamQuestionId = paperCopy.Id
        });

        await db.Invoking(x => x.SaveChangesAsync()).Should().ThrowAsync<OneWayLockException>();
    }

    [Fact(DisplayName = "M1-R3-01: BatchApproveQuestions kiểm định Title/Content/Source/Difficulty, từ chối phần tử null & rollback nguyên tử 0 bản ghi")]
    public async Task BatchApprove_ValidatesTitleContentSourceDifficulty_AndRollsBackAtomically()
    {
        var (db, head, _, _, _, course, clo, _, structureId, _, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var handler = new BatchApproveQuestionsCommandHandler(db);

        ApprovedQuestionItemDto ValidItem(string title = "Valid Question") => new(
            title,
            "Valid question content for atomic validation.",
            QuestionDifficulty.Medium,
            BloomLevel.Apply,
            "exam",
            QuestionSource.Manual,
            new string('M', 60),
            ["Key1"],
            false,
            null,
            ValidRubric("R-Valid"),
            CloId: clo.Id,
            StructureId: structureId);

        // 1. Null item in batch
        var nullItemRes = await handler.Handle(
            new BatchApproveQuestionsCommand(course.Id, head.Id, [ValidItem(), null!]),
            default);
        nullItemRes.IsSuccess.Should().BeFalse();
        nullItemRes.Error.Should().StartWith("INVARIANT:");

        // 2. Null, empty, and >300-char Title
        foreach (var badTitle in new string?[] { null, "   ", new string('T', 301) })
        {
            var badTitleRes = await handler.Handle(
                new BatchApproveQuestionsCommand(course.Id, head.Id, [ValidItem(), ValidItem() with { Title = badTitle! }]),
                default);
            badTitleRes.IsSuccess.Should().BeFalse();
            badTitleRes.Error.Should().StartWith("INVARIANT:");
        }

        // 3. Null and empty Content
        foreach (var badContent in new string?[] { null, "   " })
        {
            var badContentRes = await handler.Handle(
                new BatchApproveQuestionsCommand(course.Id, head.Id, [ValidItem(), ValidItem() with { Content = badContent! }]),
                default);
            badContentRes.IsSuccess.Should().BeFalse();
            badContentRes.Error.Should().StartWith("INVARIANT:");
        }

        // 4. >50-char Source
        var badSourceRes = await handler.Handle(
            new BatchApproveQuestionsCommand(course.Id, head.Id, [ValidItem(), ValidItem() with { Source = new string('S', 51) }]),
            default);
        badSourceRes.IsSuccess.Should().BeFalse();
        badSourceRes.Error.Should().StartWith("INVARIANT:");

        // 5. Invalid Difficulty (including >20 chars and "progressive")
        foreach (var badDiff in new[] { "INVALID_DIFFICULTY_25_CH", QuestionDifficulty.Progressive })
        {
            var badDiffRes = await handler.Handle(
                new BatchApproveQuestionsCommand(course.Id, head.Id, [ValidItem(), ValidItem() with { Difficulty = badDiff }]),
                default);
            badDiffRes.IsSuccess.Should().BeFalse();
            badDiffRes.Error.Should().StartWith("INVARIANT:");
        }

        // Atomic 0-write guarantee across all mixed-batch failures
        (await db.Rubrics.CountAsync()).Should().Be(0);
        (await db.ExamQuestions.CountAsync()).Should().Be(0);
        (await db.PracticeQuestions.CountAsync()).Should().Be(0);

        // 6. Valid canonicalization: padded/uppercase Difficulty -> lowercase, blank Difficulty -> "medium", blank Source -> "flm_api", 300-char trimmed Title accepted
        var maxTitle = new string('Q', 300);
        var okRes = await handler.Handle(new BatchApproveQuestionsCommand(
            course.Id,
            head.Id,
            [
                ValidItem() with
                {
                    Title = $"  {maxTitle}  ",
                    Content = "  Trimmed valid content  ",
                    Difficulty = "  HARD ",
                    Source = "  manual  ",
                    UsageScope = "exam"
                },
                ValidItem("Practice Defaulted") with
                {
                    Difficulty = "   ",
                    Source = "   ",
                    UsageScope = "practice",
                    StructureId = null
                }
            ]), default);
        okRes.IsSuccess.Should().BeTrue(okRes.Error);

        var savedEq = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Title == maxTitle);
        savedEq.Difficulty.Should().Be(QuestionDifficulty.Hard);
        savedEq.Source.Should().Be(QuestionSource.Manual);
        savedEq.Content.Should().Be("Trimmed valid content");

        var savedPq = await db.PracticeQuestions.AsNoTracking().SingleAsync(q => q.Title == "Practice Defaulted");
        savedPq.Difficulty.Should().Be(QuestionDifficulty.Medium);
        savedPq.Source.Should().Be(QuestionSource.FlmApi);
    }

    [Theory(DisplayName = "M1-R3-02: MutateQuestion SetActive=true trả CONFLICT (409) cho ExamQuestion APPROVED có BankCurationStatus PendingCuration/ExcludedFromBank")]
    [InlineData(BankCurationStatus.PendingCuration, false)]
    [InlineData(BankCurationStatus.ExcludedFromBank, false)]
    [InlineData(BankCurationStatus.BankReady, true)]
    [InlineData(BankCurationStatus.CuratedIntoBank, true)]
    public async Task MutateQuestion_SetActiveTrue_BlocksPendingOrExcludedExamQuestionsWithConflict(
        string bankCurationStatus,
        bool shouldAllowActivate)
    {
        var (db, head, lecA, _, _, course, clo, _, _, _, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var rubric = new Rubric
        {
            CourseId = course.Id,
            Name = "Rubric SetActive",
            TotalMaxScore = 10.00m,
            IsActive = true,
            Criteria =
            {
                new RubricCriterion { CriterionName = "C1", MaxScore = 5m, Weight = 0.5m, BloomLevel = BloomLevel.Apply, OrderIndex = 1 },
                new RubricCriterion { CriterionName = "C2", MaxScore = 5m, Weight = 0.5m, BloomLevel = BloomLevel.Analyze, OrderIndex = 2 }
            }
        };
        var question = new ExamQuestion
        {
            CourseId = course.Id,
            CloId = clo.Id,
            Rubric = rubric,
            Title = $"SetActive {bankCurationStatus}",
            Content = "Explain how SetActive respects BankCurationStatus.",
            SampleAnswer = new string('S', 65),
            KeyPoints = "[\"K1\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            Source = QuestionSource.Manual,
            ApprovalStatus = ApprovalStatus.Approved,
            SubmittedBy = lecA.Id,
            ApprovedBy = head.Id,
            BankCurationStatus = bankCurationStatus,
            IsActive = false
        };
        db.Rubrics.Add(rubric);
        db.ExamQuestions.Add(question);
        await db.SaveChangesAsync();

        var handler = new MutateQuestionCommandHandler(db);
        var activateRes = await handler.Handle(
            new MutateQuestionCommand(question.Id, head.Id, UserRole.DepartmentHead, QuestionMutationKind.SetActive, Active: true),
            default);

        if (shouldAllowActivate)
        {
            activateRes.IsSuccess.Should().BeTrue(activateRes.Error);
            var reloaded = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == question.Id);
            reloaded.IsActive.Should().BeTrue();
        }
        else
        {
            activateRes.IsSuccess.Should().BeFalse();
            activateRes.Error.Should().StartWith("CONFLICT:");
            var reloaded = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == question.Id);
            reloaded.IsActive.Should().BeFalse();

            // Deactivating (Active: false) stays allowed without activating into shared bank
            var deactivateRes = await handler.Handle(
                new MutateQuestionCommand(question.Id, head.Id, UserRole.DepartmentHead, QuestionMutationKind.SetActive, Active: false),
                default);
            deactivateRes.IsSuccess.Should().BeTrue(deactivateRes.Error);

            // QuestionsController maps CONFLICT: from MutateQuestionCommand to HTTP 409
            var senderMock = new Mock<ISender>();
            senderMock.Setup(s => s.Send(It.IsAny<MutateQuestionCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(activateRes);
            var controller = new QuestionsController(senderMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, head.Id.ToString()),
                            new Claim(ClaimTypes.Role, UserRole.DepartmentHead)
                        ], "Test"))
                    }
                }
            };
            var action = (await controller.SetQuestionActive(question.Id, new SetQuestionActiveRequest(true), default))
                .Should().BeOfType<ObjectResult>().Subject;
            action.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        }
    }

    [Fact(DisplayName = "M1-R4-01: QuestionReviewRequestService.Decide giữ IsActive=false khi duyệt ExamQuestion ở trạng thái PendingCuration hoặc ExcludedFromBank")]
    public async Task QuestionReviewRequestService_Decide_KeepsInactiveOnPendingOrExcludedExamQuestions()
    {
        var (db, head, lecA, _, _, course, clo, _, structureId, _, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        Rubric MakeRubric(string name) => new()
        {
            CourseId = course.Id,
            Name = name,
            TotalMaxScore = 10.00m,
            IsActive = true,
            Criteria =
            {
                new RubricCriterion { CriterionName = "C1", MaxScore = 5m, Weight = 0.5m, BloomLevel = BloomLevel.Understand, OrderIndex = 1 },
                new RubricCriterion { CriterionName = "C2", MaxScore = 5m, Weight = 0.5m, BloomLevel = BloomLevel.Analyze, OrderIndex = 2 }
            }
        };

        var eqPending = new ExamQuestion
        {
            CourseId = course.Id,
            CloId = clo.Id,
            StructureId = structureId,
            Rubric = MakeRubric("R-Pending"),
            Title = "Pending Curation ExamQuestion",
            Content = "Explain pending curation visibility.",
            SampleAnswer = new string('P', 60),
            KeyPoints = "[\"K1\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            Source = QuestionSource.Manual,
            ApprovalStatus = ApprovalStatus.SubmittedForReview,
            SubmittedBy = lecA.Id,
            BankCurationStatus = BankCurationStatus.PendingCuration,
            IsActive = false
        };
        var eqExcluded = new ExamQuestion
        {
            CourseId = course.Id,
            CloId = clo.Id,
            StructureId = structureId,
            Rubric = MakeRubric("R-Excluded"),
            Title = "Excluded ExamQuestion",
            Content = "Explain excluded curation visibility.",
            SampleAnswer = new string('E', 60),
            KeyPoints = "[\"K1\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            Source = QuestionSource.Manual,
            ApprovalStatus = ApprovalStatus.SubmittedForReview,
            SubmittedBy = lecA.Id,
            BankCurationStatus = BankCurationStatus.ExcludedFromBank,
            IsActive = false
        };
        var eqBankReady = new ExamQuestion
        {
            CourseId = course.Id,
            CloId = clo.Id,
            StructureId = structureId,
            Rubric = MakeRubric("R-Ready"),
            Title = "BankReady ExamQuestion",
            Content = "Explain bank ready activation.",
            SampleAnswer = new string('B', 60),
            KeyPoints = "[\"K1\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            Source = QuestionSource.Manual,
            ApprovalStatus = ApprovalStatus.SubmittedForReview,
            SubmittedBy = lecA.Id,
            BankCurationStatus = BankCurationStatus.BankReady,
            IsActive = false
        };
        var pq = new PracticeQuestion
        {
            CourseId = course.Id,
            CloId = clo.Id,
            Rubric = MakeRubric("R-Practice"),
            Title = "Practice Question",
            Content = "Explain practice activation.",
            SampleAnswer = new string('Q', 60),
            KeyPoints = "[\"K1\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            Source = QuestionSource.Manual,
            ApprovalStatus = ApprovalStatus.SubmittedForReview,
            SubmittedBy = lecA.Id,
            IsActive = false
        };

        db.ExamQuestions.AddRange(eqPending, eqExcluded, eqBankReady);
        db.PracticeQuestions.Add(pq);

        var itemPending = new QuestionReviewRequestItem { Id = Guid.NewGuid(), QuestionId = eqPending.Id, UsageScope = "exam", Decision = "PENDING" };
        var itemExcluded = new QuestionReviewRequestItem { Id = Guid.NewGuid(), QuestionId = eqExcluded.Id, UsageScope = "exam", Decision = "PENDING" };
        var itemBankReady = new QuestionReviewRequestItem { Id = Guid.NewGuid(), QuestionId = eqBankReady.Id, UsageScope = "exam", Decision = "PENDING" };
        var itemPractice = new QuestionReviewRequestItem { Id = Guid.NewGuid(), QuestionId = pq.Id, UsageScope = "practice", Decision = "PENDING" };
        var req = new QuestionReviewRequest
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SubmittedBy = lecA.Id,
            Version = 1,
            Items = [itemPending, itemExcluded, itemBankReady, itemPractice]
        };
        db.QuestionReviewRequests.Add(req);
        await db.SaveChangesAsync();

        var service = new QuestionReviewRequestService(db);
        var res = await service.Decide(
            head.Id,
            req.Id,
            new ReviewRequestDecision(
                [itemPending.Id, itemExcluded.Id, itemBankReady.Id, itemPractice.Id],
                ApprovalStatus.Approved,
                "Approved via batch review request",
                1),
            default);

        res.IsSuccess.Should().BeTrue(res.Error);

        var savedPending = await db.ExamQuestions.AsNoTracking().SingleAsync(x => x.Id == eqPending.Id);
        var savedExcluded = await db.ExamQuestions.AsNoTracking().SingleAsync(x => x.Id == eqExcluded.Id);
        var savedReady = await db.ExamQuestions.AsNoTracking().SingleAsync(x => x.Id == eqBankReady.Id);
        var savedPractice = await db.PracticeQuestions.AsNoTracking().SingleAsync(x => x.Id == pq.Id);

        savedPending.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        savedPending.IsActive.Should().BeFalse();

        savedExcluded.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        savedExcluded.IsActive.Should().BeFalse();

        savedReady.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        savedReady.IsActive.Should().BeTrue();

        savedPractice.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        savedPractice.IsActive.Should().BeTrue();
    }

    [Fact(DisplayName = "M1-R4-02: MutateQuestionCommandHandler xử lý an toàn ExamQuestion có SubmittedBy == null (không NRE HTTP 500)")]
    public async Task MutateQuestion_NullSubmittedByExamQuestion_DoesNotThrowNre()
    {
        var (db, head, lecA, _, _, course, clo, _, _, _, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        Rubric MakeRubric(string name) => new()
        {
            CourseId = course.Id,
            Name = name,
            TotalMaxScore = 10.00m,
            IsActive = true,
            Criteria =
            {
                new RubricCriterion { CriterionName = "C1", MaxScore = 5m, Weight = 0.5m, BloomLevel = BloomLevel.Apply, OrderIndex = 1 },
                new RubricCriterion { CriterionName = "C2", MaxScore = 5m, Weight = 0.5m, BloomLevel = BloomLevel.Analyze, OrderIndex = 2 }
            }
        };

        var draftNullOwner = new ExamQuestion
        {
            CourseId = course.Id,
            CloId = clo.Id,
            Rubric = MakeRubric("R-NullOwner-Draft"),
            Title = "System Imported Draft",
            Content = "Initial content",
            SampleAnswer = new string('A', 60),
            KeyPoints = "[\"K1\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            Source = QuestionSource.Manual,
            ApprovalStatus = ApprovalStatus.Draft,
            SubmittedBy = null,
            BankCurationStatus = BankCurationStatus.BankReady,
            IsActive = false
        };
        var approvedNullOwner = new ExamQuestion
        {
            CourseId = course.Id,
            CloId = clo.Id,
            Rubric = MakeRubric("R-NullOwner-Approved"),
            Title = "System Imported Approved",
            Content = "Initial content",
            SampleAnswer = new string('B', 60),
            KeyPoints = "[\"K1\"]",
            Difficulty = QuestionDifficulty.Hard,
            BloomLevel = BloomLevel.Analyze,
            Source = QuestionSource.Manual,
            ApprovalStatus = ApprovalStatus.Approved,
            SubmittedBy = null,
            BankCurationStatus = BankCurationStatus.BankReady,
            IsActive = false
        };

        db.ExamQuestions.AddRange(draftNullOwner, approvedNullOwner);
        await db.SaveChangesAsync();

        var handler = new MutateQuestionCommandHandler(db);

        // Lecturer must receive FORBIDDEN: (not NullReferenceException)
        var lecForbidden = await handler.Handle(
            new MutateQuestionCommand(approvedNullOwner.Id, lecA.Id, UserRole.Lecturer, QuestionMutationKind.SetActive, Active: true),
            default);
        lecForbidden.IsSuccess.Should().BeFalse();
        lecForbidden.Error.Should().StartWith("FORBIDDEN:");

        // DepartmentHead SetActive succeeds
        var headActivate = await handler.Handle(
            new MutateQuestionCommand(approvedNullOwner.Id, head.Id, UserRole.DepartmentHead, QuestionMutationKind.SetActive, Active: true),
            default);
        headActivate.IsSuccess.Should().BeTrue(headActivate.Error);
        (await db.ExamQuestions.AsNoTracking().SingleAsync(x => x.Id == approvedNullOwner.Id)).IsActive.Should().BeTrue();

        // DepartmentHead Update succeeds even when Source is null
        var headUpdate = await handler.Handle(
            new MutateQuestionCommand(
                draftNullOwner.Id,
                head.Id,
                UserRole.DepartmentHead,
                QuestionMutationKind.Update,
                Draft: new UpdateQuestionDraftDto(
                    "Updated System Draft",
                    "Updated question content",
                    new string('U', 60),
                    ["K1"],
                    QuestionDifficulty.Easy,
                    BloomLevel.Understand,
                    ValidRubric("Updated Rubric"),
                    clo.Id)),
            default);
        headUpdate.IsSuccess.Should().BeTrue(headUpdate.Error);

        // DepartmentHead SoftDelete succeeds
        var headDelete = await handler.Handle(
            new MutateQuestionCommand(draftNullOwner.Id, head.Id, UserRole.DepartmentHead, QuestionMutationKind.SoftDelete),
            default);
        headDelete.IsSuccess.Should().BeTrue(headDelete.Error);
        (await db.ExamQuestions.AsNoTracking().SingleAsync(x => x.Id == draftNullOwner.Id)).IsDeleted.Should().BeTrue();
    }

    [Fact(DisplayName = "M1-R4-03: CreateDraftQuestion Validator & Handler từ chối 'progressive', Source > 50, ImageUrl > 1000 & chuẩn hóa biên hợp lệ")]
    public async Task CreateDraftQuestion_RejectsProgressiveDifficultyAndOver50Source_InValidatorAndHandler()
    {
        var (db, _, lecA, _, _, course, clo, _, structureId, _, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var validator = new CreateDraftQuestionCommandValidator();
        var handler = new CreateDraftQuestionCommandHandler(db);

        CreateDraftQuestionCommand MakeCmd(
            string difficulty = QuestionDifficulty.Medium,
            string source = QuestionSource.Manual,
            string? imageUrl = null) => new(
            course.Id,
            lecA.Id,
            "Draft Title",
            "Draft Content",
            new string('A', 60),
            ["Key1"],
            difficulty,
            BloomLevel.Apply,
            source,
            "exam",
            false,
            null,
            ValidRubric(),
            CloId: clo.Id,
            StructureId: structureId,
            ActorUserId: lecA.Id,
            ImageUrl: imageUrl);

        // 1. Progressive difficulty rejected by both validator and handler
        var progressiveCmd = MakeCmd(difficulty: QuestionDifficulty.Progressive);
        validator.Validate(progressiveCmd).IsValid.Should().BeFalse();
        var progressiveRes = await handler.Handle(progressiveCmd, default);
        progressiveRes.IsSuccess.Should().BeFalse();
        progressiveRes.Error.Should().StartWith("VALIDATION:");

        // 2. Source > 50 trimmed chars rejected by both validator and handler
        var over50SourceCmd = MakeCmd(source: new string('S', 51));
        validator.Validate(over50SourceCmd).IsValid.Should().BeFalse();
        var over50SourceRes = await handler.Handle(over50SourceCmd, default);
        over50SourceRes.IsSuccess.Should().BeFalse();
        over50SourceRes.Error.Should().StartWith("VALIDATION:");

        // 3. ImageUrl > 1000 chars rejected by both validator and handler
        var over1000ImgCmd = MakeCmd(imageUrl: "https://cdn.example.com/" + new string('x', 990) + ".png");
        validator.Validate(over1000ImgCmd).IsValid.Should().BeFalse();
        var over1000ImgRes = await handler.Handle(over1000ImgCmd, default);
        over1000ImgRes.IsSuccess.Should().BeFalse();
        over1000ImgRes.Error.Should().StartWith("VALIDATION:");

        (await db.ExamQuestions.CountAsync()).Should().Be(0);

        // 4. Blank Source defaults to "manual", 50-char padded Source trims to 50, padded uppercase Difficulty canonicalizes
        var blankSourceCmd = MakeCmd(difficulty: "  HARD ", source: "   ");
        validator.Validate(blankSourceCmd).IsValid.Should().BeTrue();
        var blankSourceRes = await handler.Handle(blankSourceCmd, default);
        blankSourceRes.IsSuccess.Should().BeTrue(blankSourceRes.Error);

        var max50Source = new string('M', 50);
        var maxSourceCmd = MakeCmd(source: $"  {max50Source}  ");
        validator.Validate(maxSourceCmd).IsValid.Should().BeTrue();
        var maxSourceRes = await handler.Handle(maxSourceCmd, default);
        maxSourceRes.IsSuccess.Should().BeTrue(maxSourceRes.Error);

        var saved1 = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == blankSourceRes.Value);
        saved1.Difficulty.Should().Be(QuestionDifficulty.Hard);
        saved1.Source.Should().Be(QuestionSource.Manual);

        var saved2 = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == maxSourceRes.Value);
        saved2.Source.Should().Be(max50Source);
    }

    [Fact(DisplayName = "M1-R4-04: CreateRubric, UpdateRubric, MutateQuestion(Update) và CreateDraftQuestion chặn phần tử null trong Criteria không ném NRE")]
    public async Task RubricAndQuestionHandlers_RejectNullCriterionElement_WithoutNullReferenceException()
    {
        var (db, _, lecA, _, _, course, clo, _, structureId, _, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var existingRubric = new Rubric
        {
            CourseId = course.Id,
            Name = "Existing Rubric",
            TotalMaxScore = 10.00m,
            IsActive = true,
            Criteria =
            {
                new RubricCriterion { CriterionName = "C1", MaxScore = 5m, Weight = 0.5m, BloomLevel = BloomLevel.Understand, OrderIndex = 1 },
                new RubricCriterion { CriterionName = "C2", MaxScore = 5m, Weight = 0.5m, BloomLevel = BloomLevel.Analyze, OrderIndex = 2 }
            }
        };
        var draftQuestion = new ExamQuestion
        {
            CourseId = course.Id,
            CloId = clo.Id,
            StructureId = structureId,
            Rubric = existingRubric,
            Title = "Draft Q",
            Content = "Draft Content",
            SampleAnswer = new string('D', 60),
            KeyPoints = "[\"K1\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            Source = QuestionSource.Manual,
            ApprovalStatus = ApprovalStatus.Draft,
            SubmittedBy = lecA.Id,
            IsActive = false
        };
        db.Rubrics.Add(existingRubric);
        db.ExamQuestions.Add(draftQuestion);
        await db.SaveChangesAsync();

        var validCriterion = new RubricCriterionDraftDto("Valid", 5.00m, 0.5m, BloomLevel.Apply, "Desc", 1);
        List<RubricCriterionDraftDto> criteriaWithNull = [validCriterion, null!];

        var createRubricRes = await new CreateRubricCommandHandler(db).Handle(
            new CreateRubricCommand(lecA.Id, course.Id, "New Rubric", null, criteriaWithNull), default);
        createRubricRes.IsSuccess.Should().BeFalse();
        createRubricRes.Error.Should().StartWith("VALIDATION:");

        var updateRubricRes = await new UpdateRubricCommandHandler(db).Handle(
            new UpdateRubricCommand(lecA.Id, existingRubric.Id, "Renamed Rubric", null, criteriaWithNull), default);
        updateRubricRes.IsSuccess.Should().BeFalse();
        updateRubricRes.Error.Should().StartWith("VALIDATION:");

        var mutateRes = await new MutateQuestionCommandHandler(db).Handle(
            new MutateQuestionCommand(
                draftQuestion.Id,
                lecA.Id,
                UserRole.Lecturer,
                QuestionMutationKind.Update,
                Draft: new UpdateQuestionDraftDto(
                    "Updated Title",
                    "Updated Content",
                    new string('U', 60),
                    ["K1"],
                    QuestionDifficulty.Medium,
                    BloomLevel.Apply,
                    new RubricDraftDto("Updated Rubric", null, 10.00m, criteriaWithNull),
                    clo.Id)),
            default);
        mutateRes.IsSuccess.Should().BeFalse();
        mutateRes.Error.Should().StartWith("VALIDATION:");

        var createDraftRes = await new CreateDraftQuestionCommandHandler(db).Handle(
            new CreateDraftQuestionCommand(
                course.Id,
                lecA.Id,
                "Another Draft",
                "Another Content",
                new string('C', 60),
                ["K1"],
                QuestionDifficulty.Medium,
                BloomLevel.Apply,
                QuestionSource.Manual,
                "exam",
                false,
                null,
                new RubricDraftDto("Bad Rubric", null, 10.00m, criteriaWithNull),
                CloId: clo.Id,
                StructureId: structureId,
                ActorUserId: lecA.Id),
            default);
        createDraftRes.IsSuccess.Should().BeFalse();
    }

    [Fact(DisplayName = "M1-R4-05: UpdateExamSetCommandHandler (ExamSetInlineQuestionHelper) trả UNPROCESSABLE: khi InlineManualQuestions hoặc Rubric.Criteria chứa null")]
    public async Task ExamSetInlineQuestionHelper_RejectsNullInlineQuestionOrNullCriterion_WithUnprocessable()
    {
        var (db, _, lecA, _, _, course, clo, session, structureId, _, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var bankQ = await SeedBankQuestionAsync(db, course.Id, clo.Id, lecA.Id, "Base Bank Q", "Base Bank Q Content");
        var examSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = structureId,
            SessionId = session.Id,
            AssignmentId = null,
            SetCode = "SET-INLINE-01",
            Status = ExamSetStatus.Draft,
            CreatedBy = lecA.Id,
            IsActive = true,
            IsSampleSet = false
        };
        db.ExamSets.Add(examSet);
        db.ExamSetQuestions.Add(new ExamSetQuestion
        {
            Id = Guid.NewGuid(),
            ExamSetId = examSet.Id,
            ExamQuestionId = bankQ.Id,
            OrderIndex = 1
        });
        await db.SaveChangesAsync();

        var updateHandler = new UpdateExamSetCommandHandler(db);

        var nullItemRes = await updateHandler.Handle(
            new UpdateExamSetCommand(
                examSet.Id,
                null,
                [],
                lecA.Id,
                lecA.Role,
                [null!]),
            default);
        nullItemRes.IsSuccess.Should().BeFalse();
        nullItemRes.Error.Should().StartWith("UNPROCESSABLE:");

        var inlineWithNullCriterion = new CreateInlineExamQuestionDto
        {
            Title = "Inline Question",
            Content = "Inline Content",
            SampleAnswer = new string('I', 60),
            KeyPoints = ["K1"],
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            CloId = clo.Id,
            Rubric = new RubricDraftDto(
                "Inline Rubric",
                null,
                10.00m,
                [new RubricCriterionDraftDto("C1", 5.00m, 0.5m, BloomLevel.Apply, "D1", 1), null!])
        };

        var nullCritRes = await updateHandler.Handle(
            new UpdateExamSetCommand(
                examSet.Id,
                null,
                [],
                lecA.Id,
                lecA.Role,
                [inlineWithNullCriterion]),
            default);
        nullCritRes.IsSuccess.Should().BeFalse();
        nullCritRes.Error.Should().StartWith("UNPROCESSABLE:");
    }

    [Fact(DisplayName = "M1-R4-06: BatchApproveQuestions từ chối ImageUrl khi Source=null (default flm_api) và ImageUrl > 1000 ký tự")]
    public async Task BatchApprove_EnforcesNormalizedSourceForImageUrl_AndMax1000Length()
    {
        var (db, head, _, _, _, course, clo, _, structureId, _, _) =
            await CreateAssignedPreparationAsync(requiredSetCount: 2, reusePercent: 30, totalQuestions: 1);
        await using var _ = db;

        var handler = new BatchApproveQuestionsCommandHandler(db);

        ApprovedQuestionItemDto MakeItem(string? source, string? imageUrl) => new(
            "Image Edge Question",
            "Question content with image validation.",
            QuestionDifficulty.Medium,
            BloomLevel.Apply,
            "exam",
            source!,
            new string('M', 60),
            ["Key1"],
            false,
            null,
            ValidRubric("R-Img"),
            CloId: clo.Id,
            StructureId: structureId,
            ImageUrl: imageUrl);

        // 1. Source == null defaults to flm_api, so ImageUrl must be rejected
        var nullSourceImgRes = await handler.Handle(
            new BatchApproveQuestionsCommand(course.Id, head.Id, [MakeItem(null, "/uploads/questions/q1.png")]),
            default);
        nullSourceImgRes.IsSuccess.Should().BeFalse();
        nullSourceImgRes.Error.Should().StartWith("INVARIANT:");

        // 2. Source == "manual" with ImageUrl > 1000 chars rejected
        var over1000ImgRes = await handler.Handle(
            new BatchApproveQuestionsCommand(
                course.Id,
                head.Id,
                [MakeItem(QuestionSource.Manual, "https://cdn.example.com/" + new string('a', 990) + ".png")]),
            default);
        over1000ImgRes.IsSuccess.Should().BeFalse();
        over1000ImgRes.Error.Should().StartWith("INVARIANT:");

        (await db.ExamQuestions.CountAsync()).Should().Be(0);

        // 3. Source == "manual" with valid local ImageUrl succeeds
        var validImgRes = await handler.Handle(
            new BatchApproveQuestionsCommand(course.Id, head.Id, [MakeItem(QuestionSource.Manual, "/uploads/questions/q1.png")]),
            default);
        validImgRes.IsSuccess.Should().BeTrue(validImgRes.Error);
        (await db.ExamQuestions.CountAsync()).Should().Be(1);
    }
}
