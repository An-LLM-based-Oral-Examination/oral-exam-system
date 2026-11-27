using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.ApproveExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.CreateExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.CreateExamStructure;
using OralExamination.Application.Features.OfficialExams.Commands.CurateExamQuestionBank;
using OralExamination.Application.Features.OfficialExams.Commands.HandOverExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.ReturnExamSetForRevision;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitExamSetForReview;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamStructure;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetAvailableExamSetQuestions;
using OralExamination.Application.Features.OfficialExams.Queries.GetBankCurationCandidates;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamSets;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamStructures;
using OralExamination.Application.Features.QuestionBank.Commands.CreateRubric;
using OralExamination.Application.Features.QuestionBank.Commands.DeleteRubric;
using OralExamination.Application.Features.QuestionBank.Commands.UpdateRubric;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public class Pr4Mf03SampleSetsOverlapAndCurationTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: $"pr4-mf03-{Guid.NewGuid():N}")
            .Options;

        return new OralExamDbContext(options);
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, string role) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        ], "TestAuth"));

    private static List<RubricCriterionDraftDto> ValidCriteria() =>
    [
        new("Hiểu khái niệm cốt lõi", 5.00m, 0.50m, BloomLevel.Understand, "Trình bày chính xác định nghĩa và bản chất", 1),
        new("Phân tích & ví dụ thực tế", 5.00m, 0.50m, BloomLevel.Analyze, "Đưa ví dụ và phân tích trade-off rõ ràng", 2)
    ];

    [Fact(DisplayName = "PR4-01: ExamStructures M3 lưu và trả RequiredSetCount & SlotDefinitions đồng thời chặn dữ liệu sai biên")]
    public async Task ExamStructures_M3_PersistsRequiredSetCountAndSlotDefinitions_AndRejectsInvalidInputs()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-m3@fpt.edu.vn", FullName = "Trưởng BM", Role = UserRole.DepartmentHead, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Software Architecture", DepartmentHeadUserId = head.Id, IsActive = true };
        db.Users.Add(head);
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var createHandler = new CreateExamStructureCommandHandler(db);
        var updateHandler = new UpdateExamStructureCommandHandler(db);
        var getHandler = new GetExamStructuresQueryHandler(db);

        // Chặn RequiredSetCount ngoài biên 1..20 và SlotDefinitions không phải JSON array
        var badCountZero = await createHandler.Handle(new CreateExamStructureCommand(
            head.Id, course.Id, "Bad 0", 5, 30, "{}", true, 0, "[]"), CancellationToken.None);
        badCountZero.IsSuccess.Should().BeFalse();
        badCountZero.Error.Should().StartWith("VALIDATION:");

        var badCountTooHigh = await createHandler.Handle(new CreateExamStructureCommand(
            head.Id, course.Id, "Bad 21", 5, 30, "{}", true, 21, "[]"), CancellationToken.None);
        badCountTooHigh.IsSuccess.Should().BeFalse();
        badCountTooHigh.Error.Should().StartWith("VALIDATION:");

        var badSlotsObject = await createHandler.Handle(new CreateExamStructureCommand(
            head.Id, course.Id, "Bad Slots", 5, 30, "{}", true, 4, "{\"slot\":1}"), CancellationToken.None);
        badSlotsObject.IsSuccess.Should().BeFalse();
        badSlotsObject.Error.Should().StartWith("VALIDATION:");

        var badSlotsMalformed = await createHandler.Handle(new CreateExamStructureCommand(
            head.Id, course.Id, "Bad JSON", 5, 30, "{}", true, 4, "[not-json"), CancellationToken.None);
        badSlotsMalformed.IsSuccess.Should().BeFalse();
        badSlotsMalformed.Error.Should().StartWith("VALIDATION:");

        var slotsJson = "[{\"slotIndex\":1,\"cloCode\":\"CLO1\",\"bloomLevel\":\"Understand\",\"difficulty\":\"easy\"}]";
        var created = await createHandler.Handle(new CreateExamStructureCommand(
            head.Id, course.Id, "Cấu trúc M3 FA26", 5, 30, "{\"Understand\":3,\"Apply\":2}", true, 6, slotsJson), CancellationToken.None);

        created.IsSuccess.Should().BeTrue(because: created.Error);
        created.Value.RequiredSetCount.Should().Be(6);
        created.Value.SlotDefinitions.Should().Be(slotsJson);

        var updatedSlotsJson = "[{\"slotIndex\":1,\"cloCode\":\"CLO1\",\"bloomLevel\":\"Apply\",\"difficulty\":\"medium\"}]";
        var updated = await updateHandler.Handle(new UpdateExamStructureCommand(
            head.Id, created.Value.Id, "Cấu trúc M3 Cập Nhật", 6, 40, "{\"Understand\":3,\"Apply\":3}", true, 8, updatedSlotsJson), CancellationToken.None);

        updated.IsSuccess.Should().BeTrue(because: updated.Error);
        updated.Value.RequiredSetCount.Should().Be(8);
        updated.Value.SlotDefinitions.Should().Be(updatedSlotsJson);

        var fetched = await getHandler.Handle(new GetExamStructuresQuery(course.Id), CancellationToken.None);
        fetched.IsSuccess.Should().BeTrue();
        fetched.Value.Should().ContainSingle(s =>
            s.Id == created.Value.Id
            && s.RequiredSetCount == 8
            && s.SlotDefinitions == updatedSlotsJson);
    }

    [Fact(DisplayName = "PR4-02: Đề mẫu (IsSampleSet) chỉ TBM tạo tối đa 1 bộ/cấu trúc, ẩn khỏi available-questions và chặn ghép vào đề thi thật")]
    public async Task SampleSet_OnlyDepartmentHeadCanCreateOne_HiddenFromPool_AndBlockedInOfficialSets()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-sample@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lec = new User { Id = Guid.NewGuid(), Email = "lec-sample@fpt.edu.vn", FullName = "Lecturer", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "Struct Sample", RequiredSetCount = 3, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R1", TotalMaxScore = 10.00m, IsActive = true };
        var qSample = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Sample Q", Content = "Sample content", ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };
        var qRegular = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Regular Q", Content = "Regular content", ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };

        db.Users.AddRange(head, lec);
        db.Courses.Add(course);
        db.CourseLecturers.Add(new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec.Id, AssignedByUserId = head.Id, IsActive = true });
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.AddRange(qSample, qRegular);
        await db.SaveChangesAsync();

        var createHandler = new CreateExamSetCommandHandler(db);

        // GV không được tạo đề mẫu -> FORBIDDEN
        var lecSampleAttempt = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SAMPLE-LEC", [qSample.Id], lec.Id, UserRole.Lecturer, IsSampleSet: true), CancellationToken.None);
        lecSampleAttempt.IsSuccess.Should().BeFalse();
        lecSampleAttempt.Error.Should().StartWith("FORBIDDEN:");

        // TBM tạo đề mẫu thành công
        var headSample = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "PRN231-SAMPLE", [qSample.Id], head.Id, UserRole.DepartmentHead, IsSampleSet: true), CancellationToken.None);
        headSample.IsSuccess.Should().BeTrue(because: headSample.Error);
        headSample.Value.IsSampleSet.Should().BeTrue();

        // Không được tạo đề mẫu thứ 2 cho cùng cấu trúc -> CONFLICT
        var secondSample = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "PRN231-SAMPLE-2", [qRegular.Id], head.Id, UserRole.DepartmentHead, IsSampleSet: true), CancellationToken.None);
        secondSample.IsSuccess.Should().BeFalse();
        secondSample.Error.Should().StartWith("CONFLICT:");

        // Không thể gửi duyệt hoặc bàn giao đề mẫu
        var submitSample = await new SubmitExamSetForReviewCommandHandler(db).Handle(
            new SubmitExamSetForReviewCommand(headSample.Value.Id, head.Id, UserRole.DepartmentHead), CancellationToken.None);
        submitSample.IsSuccess.Should().BeFalse();
        submitSample.Error.Should().StartWith("UNPROCESSABLE:");

        var storedSample = await db.ExamSets.FirstAsync(es => es.Id == headSample.Value.Id);
        storedSample.Status = ExamSetStatus.Official;
        await db.SaveChangesAsync();

        var handOverSample = await new HandOverExamSetCommandHandler(db).Handle(
            new HandOverExamSetCommand(headSample.Value.Id, head.Id, UserRole.DepartmentHead), CancellationToken.None);
        handOverSample.IsSuccess.Should().BeFalse();
        handOverSample.Error.Should().StartWith("UNPROCESSABLE:");

        // Câu thuộc đề mẫu bị ẩn hoàn toàn khỏi available-questions
        var available = await new GetAvailableExamSetQuestionsQueryHandler(db).Handle(
            new GetAvailableExamSetQuestionsQuery(structure.Id, lec.Id, UserRole.Lecturer), CancellationToken.None);
        available.IsSuccess.Should().BeTrue();
        available.Value.Items.Select(i => i.QuestionId).Should().ContainSingle().Which.Should().Be(qRegular.Id);

        // Chặn ghép câu thuộc đề mẫu vào bộ đề thi thật (422 UNPROCESSABLE)
        var officialWithSampleQ = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "PRN231-SET01", [qSample.Id], lec.Id, UserRole.Lecturer), CancellationToken.None);
        officialWithSampleQ.IsSuccess.Should().BeFalse();
        officialWithSampleQ.Error.Should().StartWith("UNPROCESSABLE:");
    }

    [Fact(DisplayName = "PR4-03: Loại trừ nội bộ GV giữa các đề và câu tạo tay inline lưu PENDING_CURATION riêng tư")]
    public async Task Lecturer_SelfExclusion_And_InlineManualQuestion_PendingCuration_PrivateToAuthor()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-inline@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lec1 = new User { Id = Guid.NewGuid(), Email = "lec1-inline@fpt.edu.vn", FullName = "GV 1", Role = UserRole.Lecturer, IsActive = true };
        var lec2 = new User { Id = Guid.NewGuid(), Email = "lec2-inline@fpt.edu.vn", FullName = "GV 2", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWR302", Name = "Requirements", DepartmentHeadUserId = head.Id, IsActive = true };
        var clo = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO1", Description = "Req", BloomLevel = BloomLevel.Understand, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "Struct Inline", RequiredSetCount = 2, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R-Base", TotalMaxScore = 10.00m, IsActive = true };
        var qBank1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, CloId = clo.Id, RubricId = rubric.Id, Title = "Bank Q1", Content = "Bank content 1", ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };
        var qBank2 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, CloId = clo.Id, RubricId = rubric.Id, Title = "Bank Q2", Content = "Bank content 2", ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };

        db.Users.AddRange(head, lec1, lec2);
        db.Courses.Add(course);
        db.CourseClos.Add(clo);
        db.CourseLecturers.AddRange(
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec1.Id, AssignedByUserId = head.Id, IsActive = true },
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec2.Id, AssignedByUserId = head.Id, IsActive = true });
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.AddRange(qBank1, qBank2);
        await db.SaveChangesAsync();

        var createHandler = new CreateExamSetCommandHandler(db);

        // Chặn inline manual question có tổng barem != 10.00 hoặc ModelAnswer < 50 ký tự
        var badInlineRubric = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SET-BAD-RUBRIC", [], lec1.Id, UserRole.Lecturer, false,
            [
                new CreateInlineExamQuestionDto
                {
                    Title = "Inline Bad Rubric",
                    Content = "Mô tả bài toán phân rã yêu cầu phi chức năng?",
                    SampleAnswer = new string('A', 60),
                    Difficulty = QuestionDifficulty.Medium,
                    BloomLevel = BloomLevel.Apply,
                    Rubric = new RubricDraftDto("Bad", null, 8m,
                    [
                        new("C1", 4m, 0.5m, BloomLevel.Understand, "D1", 1),
                        new("C2", 4m, 0.5m, BloomLevel.Apply, "D2", 2)
                    ])
                }
            ]), CancellationToken.None);
        badInlineRubric.IsSuccess.Should().BeFalse();
        badInlineRubric.Error.Should().StartWith("UNPROCESSABLE:");

        var badInlineSampleAnswer = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SET-BAD-ANS", [], lec1.Id, UserRole.Lecturer, false,
            [
                new CreateInlineExamQuestionDto
                {
                    Title = "Inline Short Answer",
                    Content = "Mô tả bài toán phân rã yêu cầu phi chức năng?",
                    SampleAnswer = "Quá ngắn dưới 50 ký tự",
                    Rubric = new RubricDraftDto("Good", null, 10m, ValidCriteria())
                }
            ]), CancellationToken.None);
        badInlineSampleAnswer.IsSuccess.Should().BeFalse();
        badInlineSampleAnswer.Error.Should().StartWith("UNPROCESSABLE:");

        // GV1 tạo Bộ đề 1 gồm qBank1 + 1 câu tạo tay inline hợp lệ
        var set1 = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SWR302-L1-S1", [qBank1.Id], lec1.Id, UserRole.Lecturer, false,
            [
                new CreateInlineExamQuestionDto
                {
                    Title = "Câu tạo tay riêng của GV1",
                    Content = "Phân tích sự khác biệt giữa yêu cầu chức năng và phi chức năng trong hệ thống y tế.",
                    SampleAnswer = "Yêu cầu chức năng mô tả hệ thống làm gì, còn yêu cầu phi chức năng quy định chất lượng vận hành như bảo mật và hiệu năng.",
                    KeyPoints = ["Functional", "Non-Functional"],
                    Difficulty = QuestionDifficulty.Hard,
                    BloomLevel = BloomLevel.Analyze,
                    CloId = clo.Id,
                    Rubric = new RubricDraftDto("Rubric Inline GV1", "Barem 10đ", 10.00m, ValidCriteria())
                }
            ]), CancellationToken.None);

        set1.IsSuccess.Should().BeTrue(because: set1.Error);
        set1.Value.Questions.Should().HaveCount(2);

        var inlineQId = set1.Value.Questions.Single(q => q.QuestionId != qBank1.Id).QuestionId;
        var storedInline = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == inlineQId);
        storedInline.Source.Should().Be(QuestionSource.Manual);
        storedInline.ApprovalStatus.Should().Be(ApprovalStatus.Draft);
        storedInline.IsActive.Should().BeFalse();
        storedInline.BankCurationStatus.Should().Be(BankCurationStatus.PendingCuration);
        storedInline.SubmittedBy.Should().Be(lec1.Id);
        storedInline.StructureId.Should().Be(structure.Id);

        // Loại trừ nội bộ GV1: qBank1 biến mất khỏi available-questions của GV1 và bị chặn khi GV1 soạn Bộ đề 2
        var availLec1 = await new GetAvailableExamSetQuestionsQueryHandler(db).Handle(
            new GetAvailableExamSetQuestionsQuery(structure.Id, lec1.Id, UserRole.Lecturer), CancellationToken.None);
        availLec1.IsSuccess.Should().BeTrue();
        availLec1.Value.Items.Select(i => i.QuestionId).Should().ContainSingle().Which.Should().Be(qBank2.Id);

        var set2SelfReuse = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SWR302-L1-S2", [qBank1.Id], lec1.Id, UserRole.Lecturer), CancellationToken.None);
        set2SelfReuse.IsSuccess.Should().BeFalse();
        set2SelfReuse.Error.Should().StartWith("UNPROCESSABLE:");

        // Câu tạo tay PENDING_CURATION của GV1 là riêng tư -> GV2 không thấy trên available-questions và bị chặn nếu cố gắn ID
        var availLec2 = await new GetAvailableExamSetQuestionsQueryHandler(db).Handle(
            new GetAvailableExamSetQuestionsQuery(structure.Id, lec2.Id, UserRole.Lecturer), CancellationToken.None);
        availLec2.IsSuccess.Should().BeTrue();
        availLec2.Value.Items.Select(i => i.QuestionId).Should().NotContain(inlineQId);
        availLec2.Value.Items.Select(i => i.QuestionId).Should().BeEquivalentTo([qBank1.Id, qBank2.Id]);

        var lec2StealInline = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SWR302-L2-STEAL", [inlineQId], lec2.Id, UserRole.Lecturer), CancellationToken.None);
        lec2StealInline.IsSuccess.Should().BeFalse();
        lec2StealInline.Error.Should().StartWith("UNPROCESSABLE:");
    }

    [Fact(DisplayName = "PR4-04 & PR4-05: Nhãn vàng ẩn danh, chặn nộp >70% (422), cho nộp <=70%, ẩn câu sau duyệt, cảnh báo đỏ TBM & trả về chỉnh sửa")]
    public async Task YellowOverlap70Threshold_PostApprovalHiding_RedWarningForHead_AndReturnForRevision()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-ov@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lecA = new User { Id = Guid.NewGuid(), Email = "leca-ov@fpt.edu.vn", FullName = "GV A", Role = UserRole.Lecturer, IsActive = true };
        var lecB = new User { Id = Guid.NewGuid(), Email = "lecb-ov@fpt.edu.vn", FullName = "GV B", Role = UserRole.Lecturer, IsActive = true };
        var lecC = new User { Id = Guid.NewGuid(), Email = "lecc-ov@fpt.edu.vn", FullName = "GV C", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWE201c", Name = "Intro SE", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "Struct Overlap", TotalQuestions = 10, RequiredSetCount = 4, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R-Ov", TotalMaxScore = 10.00m, IsActive = true };

        db.Users.AddRange(head, lecA, lecB, lecC);
        db.Courses.Add(course);
        db.CourseLecturers.AddRange(
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lecA.Id, AssignedByUserId = head.Id, IsActive = true },
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lecB.Id, AssignedByUserId = head.Id, IsActive = true },
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lecC.Id, AssignedByUserId = head.Id, IsActive = true });
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);

        // Tạo 13 câu hỏi ngân hàng APPROVED & BANK_READY
        var questions = Enumerable.Range(1, 13).Select(i => new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = $"Q{i}",
            Content = $"Nội dung câu hỏi kiểm thử số {i}",
            ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.BankReady,
            IsActive = true
        }).ToList();
        db.ExamQuestions.AddRange(questions);
        await db.SaveChangesAsync();

        var createHandler = new CreateExamSetCommandHandler(db);
        var submitHandler = new SubmitExamSetForReviewCommandHandler(db);
        var availHandler = new GetAvailableExamSetQuestionsQueryHandler(db);
        var getSetsHandler = new GetExamSetsQueryHandler(db);
        var approveHandler = new ApproveExamSetCommandHandler(db);
        var returnHandler = new ReturnExamSetForRevisionCommandHandler(db);

        // 1. GV A tạo và nộp duyệt SET-A gồm q[0..9] (10 câu)
        var setAIds = questions.Take(10).Select(q => q.Id).ToList();
        var setA = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SWE-SET-A", setAIds, lecA.Id, UserRole.Lecturer), CancellationToken.None);
        setA.IsSuccess.Should().BeTrue();

        var submitA = await submitHandler.Handle(new SubmitExamSetForReviewCommand(
            setA.Value.Id, lecA.Id, UserRole.Lecturer), CancellationToken.None);
        submitA.IsSuccess.Should().BeTrue();

        // 2. GV B mở màn chọn câu -> 10 câu của SET-A gắn nhãn vàng ẩn danh, 3 câu còn lại bình thường
        var availForB = await availHandler.Handle(
            new GetAvailableExamSetQuestionsQuery(structure.Id, lecB.Id, UserRole.Lecturer), CancellationToken.None);
        availForB.IsSuccess.Should().BeTrue();
        availForB.Value.TotalAvailable.Should().Be(13);
        availForB.Value.YellowCount.Should().Be(10);
        availForB.Value.Items.Where(i => setAIds.Contains(i.QuestionId))
            .Should().OnlyContain(i => i.IsYellowPendingByOtherLecturer && i.YellowWarningLabel == "Đã có người chọn, đang chờ duyệt");

        // 3. GV B tạo bộ đề trùng 8/10 = 80% (> 70%) -> BỊ CHẶN NỘP (422 UNPROCESSABLE)
        var setBHighIds = questions.Take(8).Concat(questions.Skip(10).Take(2)).Select(q => q.Id).ToList();
        var setBHigh = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SWE-SET-B-80", setBHighIds, lecB.Id, UserRole.Lecturer), CancellationToken.None);
        setBHigh.IsSuccess.Should().BeTrue();

        var submitBHigh = await submitHandler.Handle(new SubmitExamSetForReviewCommand(
            setBHigh.Value.Id, lecB.Id, UserRole.Lecturer), CancellationToken.None);
        submitBHigh.IsSuccess.Should().BeFalse();
        submitBHigh.Error.Should().StartWith("UNPROCESSABLE:");
        submitBHigh.Error.Should().Contain("70%");

        // 4. GV C tạo bộ đề trùng đúng 7/10 = 70% (<= 70%) -> ĐƯỢC PHÉP NỘP DUYỆT
        var setCOkIds = questions.Take(7).Concat(questions.Skip(10).Take(3)).Select(q => q.Id).ToList();
        var setCOk = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SWE-SET-C-70", setCOkIds, lecC.Id, UserRole.Lecturer), CancellationToken.None);
        setCOk.IsSuccess.Should().BeTrue();

        var submitCOk = await submitHandler.Handle(new SubmitExamSetForReviewCommand(
            setCOk.Value.Id, lecC.Id, UserRole.Lecturer), CancellationToken.None);
        submitCOk.IsSuccess.Should().BeTrue(because: submitCOk.Error);

        // GV C gọi GetExamSets: thấy nhãn vàng ẩn danh, KHÔNG được lộ SetCode "SWE-SET-A"
        var lecCSets = await getSetsHandler.Handle(
            new GetExamSetsQuery(course.Id, null, null, lecC.Id, UserRole.Lecturer), CancellationToken.None);
        lecCSets.IsSuccess.Should().BeTrue();
        var lecCSetDto = lecCSets.Value.Should().ContainSingle(s => s.Id == setCOk.Value.Id).Subject;
        lecCSetDto.OverlapSeverity.Should().Be("YELLOW_PENDING");
        lecCSetDto.YellowOverlapQuestionCount.Should().Be(7);
        lecCSetDto.YellowOverlapRatio.Should().Be(0.7000m);
        lecCSetDto.Questions.Should().OnlyContain(q => q.DuplicateInSets.Count == 0);

        // 5. TBM duyệt SET-A thành OFFICIAL -> 10 câu trong SET-A ẩn ngay khỏi available-questions của đợt thi này
        var approveA = await approveHandler.Handle(new ApproveExamSetCommand(
            setA.Value.Id, "Đạt chuẩn duyệt Đề 1", head.Id, UserRole.DepartmentHead), CancellationToken.None);
        approveA.IsSuccess.Should().BeTrue();

        var availAfterApprove = await availHandler.Handle(
            new GetAvailableExamSetQuestionsQuery(structure.Id, lecB.Id, UserRole.Lecturer), CancellationToken.None);
        availAfterApprove.IsSuccess.Should().BeTrue();
        availAfterApprove.Value.Items.Select(i => i.QuestionId).Should().NotIntersectWith(setAIds);

        // Chặn tạo bộ đề mới dùng câu đã duyệt trong SET-A cùng cấu trúc
        var reuseApproved = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SWE-SET-REUSE", [questions[0].Id], lecB.Id, UserRole.Lecturer), CancellationToken.None);
        reuseApproved.IsSuccess.Should().BeFalse();
        reuseApproved.Error.Should().StartWith("UNPROCESSABLE:");

        // 6. TBM gọi GetExamSets -> SET-C-70 hiện cảnh báo ĐỎ kèm mã bộ đề trùng SWE-SET-A
        var headSets = await getSetsHandler.Handle(
            new GetExamSetsQuery(course.Id, null, null, head.Id, UserRole.DepartmentHead), CancellationToken.None);
        headSets.IsSuccess.Should().BeTrue();
        var setCForHead = headSets.Value.Single(s => s.Id == setCOk.Value.Id);
        setCForHead.OverlapSeverity.Should().Be("RED_APPROVED");
        setCForHead.RedOverlapQuestionCount.Should().Be(7);
        setCForHead.Questions.Where(q => setAIds.Contains(q.QuestionId))
            .Should().OnlyContain(q =>
                q.IsDuplicate
                && q.OverlapSeverity == "RED_APPROVED"
                && q.DuplicateInSets.Contains("SWE-SET-A"));

        // 7. TBM gọi ReturnExamSetForRevision: chặn lý do < 5 ký tự, thành công chuyển về REJECTED + lưu AuditLog
        var shortReturn = await returnHandler.Handle(new ReturnExamSetForRevisionCommand(
            setCOk.Value.Id, "Ngắn", head.Id, UserRole.DepartmentHead), CancellationToken.None);
        shortReturn.IsSuccess.Should().BeFalse();
        shortReturn.Error.Should().StartWith("UNPROCESSABLE:");

        var validReturn = await returnHandler.Handle(new ReturnExamSetForRevisionCommand(
            setCOk.Value.Id, "Trùng 7 câu với bộ đề SWE-SET-A đã duyệt, vui lòng thay câu mới.", head.Id, UserRole.DepartmentHead), CancellationToken.None);
        validReturn.IsSuccess.Should().BeTrue(because: validReturn.Error);
        validReturn.Value.Status.Should().Be(ExamSetStatus.Rejected);

        var reloadedSetC = await db.ExamSets.AsNoTracking().SingleAsync(es => es.Id == setCOk.Value.Id);
        reloadedSetC.Status.Should().Be(ExamSetStatus.Rejected);
        reloadedSetC.ReviewNotes.Should().Contain("SWE-SET-A");
        db.AuditLogs.Any(a => a.Action == "RETURN_EXAM_SET_FOR_REVISION" && a.EntityId == setCOk.Value.Id).Should().BeTrue();
    }

    [Fact(DisplayName = "PR4-06: Lọc kho kỳ sau (Trigram 70-79% Vàng / >=80% Đỏ), quyết định CURATED_INTO_BANK vs EXCLUDED_FROM_BANK bảo toàn lịch sử & Controller HTTP mapping")]
    public async Task BankCuration_ClassifiesYellowAndRedSimilarity_AndCuratesWithoutHardDelete()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-cur@fpt.edu.vn", FullName = "Head Curation", Role = UserRole.DepartmentHead, IsActive = true };
        var lec = new User { Id = Guid.NewGuid(), Email = "lec-cur@fpt.edu.vn", FullName = "GV Curation", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN222", Name = "Advanced .NET", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "Struct Cur", IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R-Cur", TotalMaxScore = 10.00m, IsActive = true };

        var bank1 = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Clean Architecture Dependency Rule",
            Content = "Trình bày nguyên lý phụ thuộc và cách tổ chức 4 tầng Domain Application Infrastructure API trong dự án ASP NET Core",
            ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.BankReady,
            IsActive = true
        };
        var bank2 = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "PostgreSQL ESR Indexing",
            Content = "Phân tích quy tắc Equality Sort Range khi thiết kế chỉ mục tổng hợp trong hệ quản trị cơ sở dữ liệu quan hệ",
            ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.BankReady,
            IsActive = true
        };

        var redCand = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Clean Architecture Dependency Rule",
            Content = "Trình bày nguyên lý phụ thuộc và cách tổ chức 4 tầng Domain Application Infrastructure API trong ASP NET Core 8",
            ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.PendingCuration,
            SubmittedBy = lec.Id,
            IsActive = true
        };
        var yellowCand = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "PostgreSQL ESR Indexing",
            Content = "Phân tích quy tắc Equality Sort Range khi thiết kế chỉ mục tổng hợp trong cơ sở báo cáo thương mại",
            ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.PendingCuration,
            SubmittedBy = lec.Id,
            IsActive = true
        };
        var clearCand = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Cấu trúc dữ liệu cây nhị phân tìm kiếm AVL",
            Content = "và thao tác xoay đơn xoay kép cân bằng chiều cao",
            ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.PendingCuration,
            SubmittedBy = lec.Id,
            IsActive = false
        };

        var historicalSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = structure.Id,
            CourseId = course.Id,
            CreatedBy = lec.Id,
            SetCode = "PRN222-HIST-01",
            Status = ExamSetStatus.HandedOver,
            IsActive = true
        };
        historicalSet.ExamSetQuestions.Add(new ExamSetQuestion
        {
            Id = Guid.NewGuid(),
            ExamSetId = historicalSet.Id,
            ExamQuestionId = redCand.Id,
            OrderIndex = 1
        });

        db.Users.AddRange(head, lec);
        db.Courses.Add(course);
        db.CourseLecturers.Add(new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec.Id, AssignedByUserId = head.Id, IsActive = true });
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.AddRange(bank1, bank2, redCand, yellowCand, clearCand);
        db.ExamSets.Add(historicalSet);
        await db.SaveChangesAsync();

        // 1. Lấy danh sách ứng viên lọc kho -> phân loại chuẩn RED_80_PLUS, YELLOW_70_79, CLEAR
        var queryHandler = new GetBankCurationCandidatesQueryHandler(db);
        var candidates = await queryHandler.Handle(
            new GetBankCurationCandidatesQuery(course.Id, head.Id, UserRole.DepartmentHead), CancellationToken.None);

        candidates.IsSuccess.Should().BeTrue(because: candidates.Error);
        candidates.Value.TotalPendingCount.Should().Be(3);
        candidates.Value.RedCount.Should().Be(1);
        candidates.Value.YellowCount.Should().Be(1);
        candidates.Value.ClearCount.Should().Be(1);

        var redDto = candidates.Value.Items.Single(i => i.QuestionId == redCand.Id);
        redDto.HighestBand.Should().Be(TrigramSimilarityCalculator.BandRed80Plus);
        redDto.MaxSimilarityScore.Should().BeGreaterThanOrEqualTo(0.80m);
        redDto.SourceSetCodes.Should().Contain("PRN222-HIST-01");
        redDto.Matches.Should().Contain(m => m.ComparedQuestionId == bank1.Id && m.IsFromExistingBank && m.Band == TrigramSimilarityCalculator.BandRed80Plus);

        var yellowDto = candidates.Value.Items.Single(i => i.QuestionId == yellowCand.Id);
        yellowDto.HighestBand.Should().Be(TrigramSimilarityCalculator.BandYellow70To79);
        yellowDto.MaxSimilarityScore.Should().BeGreaterThanOrEqualTo(0.70m).And.BeLessThan(0.80m);
        yellowDto.Matches.Should().Contain(m => m.ComparedQuestionId == bank2.Id && m.IsFromExistingBank && m.Band == TrigramSimilarityCalculator.BandYellow70To79);

        var clearDto = candidates.Value.Items.Single(i => i.QuestionId == clearCand.Id);
        clearDto.HighestBand.Should().Be(TrigramSimilarityCalculator.BandClear);
        clearDto.Matches.Should().BeEmpty();

        // 2. Quyết định nạp kho CURATED_INTO_BANK cho clearCand và loại khỏi kho EXCLUDED_FROM_BANK cho redCand
        var decideHandler = new CurateExamQuestionBankCommandHandler(db);

        var curateIntoBank = await decideHandler.Handle(new CurateExamQuestionBankCommand(
            course.Id, [clearCand.Id], BankCurationStatus.CuratedIntoBank, "Câu mới rõ ràng, nạp kho kỳ sau", head.Id, UserRole.DepartmentHead), CancellationToken.None);
        curateIntoBank.IsSuccess.Should().BeTrue(because: curateIntoBank.Error);

        var excludeFromBank = await decideHandler.Handle(new CurateExamQuestionBankCommand(
            course.Id, [redCand.Id], BankCurationStatus.ExcludedFromBank, "Trùng trên 90% với câu có sẵn", head.Id, UserRole.DepartmentHead), CancellationToken.None);
        excludeFromBank.IsSuccess.Should().BeTrue(because: excludeFromBank.Error);

        var reloadedClear = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == clearCand.Id);
        reloadedClear.BankCurationStatus.Should().Be(BankCurationStatus.CuratedIntoBank);
        reloadedClear.CuratedBy.Should().Be(head.Id);
        reloadedClear.CuratedAt.Should().NotBeNull();
        reloadedClear.IsActive.Should().BeTrue();

        var reloadedRed = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == redCand.Id);
        reloadedRed.BankCurationStatus.Should().Be(BankCurationStatus.ExcludedFromBank);
        reloadedRed.CuratedBy.Should().Be(head.Id);
        reloadedRed.CuratedAt.Should().NotBeNull();
        reloadedRed.IsDeleted.Should().BeFalse("câu bị loại khỏi kho phải được giữ bản ghi mềm để bảo toàn lịch sử bài thi/phúc khảo");
        db.ExamSetQuestions.Any(esq => esq.ExamSetId == historicalSet.Id && esq.ExamQuestionId == redCand.Id).Should().BeTrue();

        // Câu EXCLUDED_FROM_BANK không thể đưa vào bộ đề mới, còn CURATED_INTO_BANK xuất hiện lại trong pool
        var nextTermStructure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "Struct Next Term", IsActive = true };
        db.ExamStructures.Add(nextTermStructure);
        await db.SaveChangesAsync();

        var nextTermAvailable = await new GetAvailableExamSetQuestionsQueryHandler(db).Handle(
            new GetAvailableExamSetQuestionsQuery(nextTermStructure.Id, lec.Id, UserRole.Lecturer), CancellationToken.None);
        nextTermAvailable.IsSuccess.Should().BeTrue();
        nextTermAvailable.Value.Items.Select(i => i.QuestionId).Should().Contain(clearCand.Id);
        nextTermAvailable.Value.Items.Select(i => i.QuestionId).Should().NotContain(redCand.Id);

        var createWithExcluded = await new CreateExamSetCommandHandler(db).Handle(new CreateExamSetCommand(
            nextTermStructure.Id, course.Id, null, "PRN222-EXCL", [redCand.Id], lec.Id, UserRole.Lecturer), CancellationToken.None);
        createWithExcluded.IsSuccess.Should().BeFalse();
        createWithExcluded.Error.Should().StartWith("UNPROCESSABLE:");

        // 3. Kiểm tra mapping HTTP RFC 7807 trên OfficialExamCurationController
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<ReturnExamSetForRevisionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ReturnExamSetForRevisionResultDto>.Failure("CONFLICT: Bộ đề không ở trạng thái chờ duyệt."));
        senderMock.Setup(s => s.Send(It.IsAny<CurateExamQuestionBankCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CurateExamQuestionBankResultDto>.Failure("UNPROCESSABLE: Quyết định không hợp lệ."));

        var controller = new OfficialExamCurationController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(head.Id, UserRole.DepartmentHead) }
            }
        };

        (await controller.ReturnExamSetForRevision(historicalSet.Id, new ReturnExamSetForRevisionRequest { ReviewNotes = "Lý do hợp lệ" }))
            .Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        (await controller.DecideBankCuration(course.Id, new CurateExamQuestionBankRequest { QuestionIds = [yellowCand.Id], Decision = "INVALID" }))
            .Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact(DisplayName = "PR4-07: PUT và DELETE /api/v1/rubrics/{id} khóa tổng 10.00 (422), chặn xóa/sửa khi tham chiếu (409) và xóa mềm (200)")]
    public async Task Rubrics_PutAndDelete_EnforceTenPointInvariant_ReferenceConflict_AndSoftDelete()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-rub@fpt.edu.vn", FullName = "Head Rubric", Role = UserRole.DepartmentHead, IsActive = true };
        var lec = new User { Id = Guid.NewGuid(), Email = "lec-rub@fpt.edu.vn", FullName = "GV Rubric", Role = UserRole.Lecturer, IsActive = true };
        var foreignLec = new User { Id = Guid.NewGuid(), Email = "foreign-rub@fpt.edu.vn", FullName = "GV Khác", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRJ301", Name = "Java Web", DepartmentHeadUserId = head.Id, IsActive = true };
        var otherCourse = new Course { Id = Guid.NewGuid(), Code = "ENT104", Name = "Other", IsActive = true };

        db.Users.AddRange(head, lec, foreignLec);
        db.Courses.AddRange(course, otherCourse);
        db.CourseLecturers.AddRange(
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec.Id, AssignedByUserId = head.Id, IsActive = true },
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = otherCourse.Id, LecturerUserId = foreignLec.Id, AssignedByUserId = head.Id, IsActive = true });
        await db.SaveChangesAsync();

        var createHandler = new CreateRubricCommandHandler(db);
        var updateHandler = new UpdateRubricCommandHandler(db);
        var deleteHandler = new DeleteRubricCommandHandler(db);

        var createdUnused = await createHandler.Handle(new CreateRubricCommand(
            lec.Id, course.Id, "Rubric Chưa Dùng", "Mô tả gốc", ValidCriteria()), CancellationToken.None);
        createdUnused.IsSuccess.Should().BeTrue();

        var createdReferenced = await createHandler.Handle(new CreateRubricCommand(
            head.Id, course.Id, "Rubric Đang Dùng", "Gắn câu hỏi thi", ValidCriteria()), CancellationToken.None);
        createdReferenced.IsSuccess.Should().BeTrue();

        var linkedExamQ = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = createdReferenced.Value.Id,
            Title = "Câu hỏi gắn rubric",
            Content = "Nội dung câu hỏi",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        db.ExamQuestions.Add(linkedExamQ);
        await db.SaveChangesAsync();

        // 1. UpdateRubric chặn tổng != 10.00 (INVARIANT -> 422) và chặn GV không thuộc môn (FORBIDDEN -> 403)
        var badTotalUpdate = await updateHandler.Handle(new UpdateRubricCommand(
            lec.Id, createdUnused.Value.Id, "Rubric Sai Tổng", null,
            [
                new("Tiêu chí 1", 4.00m, 0.40m, BloomLevel.Understand, "D1", 1),
                new("Tiêu chí 2", 4.00m, 0.40m, BloomLevel.Apply, "D2", 2)
            ]), CancellationToken.None);
        badTotalUpdate.IsSuccess.Should().BeFalse();
        badTotalUpdate.Error.Should().StartWith("INVARIANT:");

        var foreignUpdate = await updateHandler.Handle(new UpdateRubricCommand(
            foreignLec.Id, createdUnused.Value.Id, "Hack Update", null, ValidCriteria()), CancellationToken.None);
        foreignUpdate.IsSuccess.Should().BeFalse();
        foreignUpdate.Error.Should().StartWith("FORBIDDEN:");

        // 2. UpdateRubric hợp lệ cập nhật tên và đồng bộ tiêu chí con
        var validUpdate = await updateHandler.Handle(new UpdateRubricCommand(
            lec.Id, createdUnused.Value.Id, "Rubric Đã Cập Nhật", "Mô tả mới",
            [
                new("Tiêu chí A", 3.00m, 0.30m, BloomLevel.Remember, "M1", 1),
                new("Tiêu chí B", 3.00m, 0.30m, BloomLevel.Understand, "M2", 2),
                new("Tiêu chí C", 4.00m, 0.40m, BloomLevel.Apply, "M3", 3)
            ]), CancellationToken.None);
        validUpdate.IsSuccess.Should().BeTrue(because: validUpdate.Error);
        validUpdate.Value.Name.Should().Be("Rubric Đã Cập Nhật");
        validUpdate.Value.CriteriaCount.Should().Be(3);
        validUpdate.Value.TotalMaxScore.Should().Be(10.00m);

        // 3. Chặn UpdateRubric khi đã có ExamQuestionSubmission gắn bài thi thật -> CONFLICT (409)
        db.ExamQuestionSubmissions.Add(new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = Guid.NewGuid(),
            ExamQuestionId = linkedExamQ.Id,
            GradingStatus = "graded"
        });
        await db.SaveChangesAsync();

        var updateSubmittedRubric = await updateHandler.Handle(new UpdateRubricCommand(
            head.Id, createdReferenced.Value.Id, "Cố sửa rubric đã thi", null, ValidCriteria()), CancellationToken.None);
        updateSubmittedRubric.IsSuccess.Should().BeFalse();
        updateSubmittedRubric.Error.Should().StartWith("CONFLICT:");

        // 4. DeleteRubric chặn xóa rubric đang được tham chiếu -> CONFLICT (409)
        var deleteReferenced = await deleteHandler.Handle(new DeleteRubricCommand(
            head.Id, createdReferenced.Value.Id), CancellationToken.None);
        deleteReferenced.IsSuccess.Should().BeFalse();
        deleteReferenced.Error.Should().StartWith("CONFLICT:");

        // 5. DeleteRubric xóa mềm (IsActive = false) khi chưa được tham chiếu -> Success
        var deleteUnused = await deleteHandler.Handle(new DeleteRubricCommand(
            lec.Id, createdUnused.Value.Id), CancellationToken.None);
        deleteUnused.IsSuccess.Should().BeTrue(because: deleteUnused.Error);

        var reloadedDeleted = await db.Rubrics.AsNoTracking().SingleAsync(r => r.Id == createdUnused.Value.Id);
        reloadedDeleted.IsActive.Should().BeFalse();
        db.AuditLogs.Any(a => a.Action == "DELETE_RUBRIC" && a.EntityId == createdUnused.Value.Id).Should().BeTrue();

        // 6. Kiểm tra RubricsController ánh xạ 422 / 409 / 200
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<UpdateRubricCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CreateRubricResponseDto>.Failure("INVARIANT: Tổng điểm phải bằng 10.00."));
        senderMock.Setup(s => s.Send(It.Is<DeleteRubricCommand>(c => c.RubricId == createdReferenced.Value.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("CONFLICT: Đang được tham chiếu."));
        senderMock.Setup(s => s.Send(It.Is<DeleteRubricCommand>(c => c.RubricId == createdUnused.Value.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var controller = new RubricsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(lec.Id, UserRole.Lecturer) }
            }
        };

        (await controller.UpdateRubric(createdUnused.Value.Id, new UpdateRubricRequestDto("Bad", null, ValidCriteria())))
            .Should().BeOfType<UnprocessableEntityObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        (await controller.DeleteRubric(createdReferenced.Value.Id))
            .Should().BeOfType<ConflictObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        (await controller.DeleteRubric(createdUnused.Value.Id))
            .Should().BeOfType<OkObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "PR4-RT-01: Khóa bypass ẩn câu sau duyệt khi nộp DRAFT, loại trừ nội bộ GV khi nộp lại, và GetExamSets hiển thị Đề mẫu + RED_APPROVED ẩn danh cho GV")]
    public async Task SubmitExamSet_BlocksPostApprovalBypassAndSelfReuse_AndGetExamSetsShowsSampleAndAnonymousRedForLecturer()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-rt1@fpt.edu.vn", FullName = "Head RT1", Role = UserRole.DepartmentHead, IsActive = true };
        var lecA = new User { Id = Guid.NewGuid(), Email = "leca-rt1@fpt.edu.vn", FullName = "GV A", Role = UserRole.Lecturer, IsActive = true };
        var lecB = new User { Id = Guid.NewGuid(), Email = "lecb-rt1@fpt.edu.vn", FullName = "GV B", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Architecture", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "Struct RT1", TotalQuestions = 2, RequiredSetCount = 3, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R-RT1", TotalMaxScore = 10.00m, IsActive = true };

        var qSample = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Sample RT1", Content = "Sample", ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };
        var q1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q1 RT1", Content = "Content 1", ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };
        var q2 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q2 RT1", Content = "Content 2", ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };
        var q3 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q3 RT1", Content = "Content 3", ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };

        db.Users.AddRange(head, lecA, lecB);
        db.Courses.Add(course);
        db.CourseLecturers.AddRange(
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lecA.Id, AssignedByUserId = head.Id, IsActive = true },
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lecB.Id, AssignedByUserId = head.Id, IsActive = true });
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.AddRange(qSample, q1, q2, q3);
        await db.SaveChangesAsync();

        var createHandler = new CreateExamSetCommandHandler(db);
        var submitHandler = new SubmitExamSetForReviewCommandHandler(db);
        var approveHandler = new ApproveExamSetCommandHandler(db);
        var getSetsHandler = new GetExamSetsQueryHandler(db);

        // TBM tạo bộ Đề mẫu active
        var sampleSet = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SWD-SAMPLE", [qSample.Id], head.Id, UserRole.DepartmentHead, IsSampleSet: true), CancellationToken.None);
        sampleSet.IsSuccess.Should().BeTrue(because: sampleSet.Error);

        // GV A và GV B cùng tạo DRAFT chứa q1 trước khi có bộ đề nào được duyệt
        var setA = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SWD-L1-A", [q1.Id, q2.Id], lecA.Id, UserRole.Lecturer), CancellationToken.None);
        setA.IsSuccess.Should().BeTrue(because: setA.Error);

        var setBDraft = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SWD-L2-DRAFT", [q1.Id, q3.Id], lecB.Id, UserRole.Lecturer), CancellationToken.None);
        setBDraft.IsSuccess.Should().BeTrue(because: setBDraft.Error);

        // GV A nộp và TBM duyệt setA thành OFFICIAL
        (await submitHandler.Handle(new SubmitExamSetForReviewCommand(setA.Value.Id, lecA.Id, UserRole.Lecturer), CancellationToken.None))
            .IsSuccess.Should().BeTrue();
        (await approveHandler.Handle(new ApproveExamSetCommand(setA.Value.Id, "Duyệt Đề A", head.Id, UserRole.DepartmentHead), CancellationToken.None))
            .IsSuccess.Should().BeTrue();

        // 1. GV B gọi GetExamSets -> thấy Đề mẫu active + bộ đề DRAFT của mình bị gắn RED_APPROVED ẩn danh (DuplicateInSets rỗng)
        var lecBSets = await getSetsHandler.Handle(
            new GetExamSetsQuery(course.Id, null, null, lecB.Id, UserRole.Lecturer), CancellationToken.None);
        lecBSets.IsSuccess.Should().BeTrue();
        lecBSets.Value.Should().Contain(s => s.Id == sampleSet.Value.Id && s.IsSampleSet);
        var lecBDraftDto = lecBSets.Value.Single(s => s.Id == setBDraft.Value.Id);
        lecBDraftDto.OverlapSeverity.Should().Be("RED_APPROVED");
        lecBDraftDto.RedOverlapQuestionCount.Should().Be(1);
        lecBDraftDto.Questions.Single(q => q.QuestionId == q1.Id).OverlapSeverity.Should().Be("RED_APPROVED");
        lecBDraftDto.Questions.Should().OnlyContain(q => q.DuplicateInSets.Count == 0);

        // 2. GV B cố nộp DRAFT cũ chứa q1 sau khi đề GV A đã OFFICIAL -> bị chặn 422 UNPROCESSABLE
        var submitBypass = await submitHandler.Handle(
            new SubmitExamSetForReviewCommand(setBDraft.Value.Id, lecB.Id, UserRole.Lecturer), CancellationToken.None);
        submitBypass.IsSuccess.Should().BeFalse();
        submitBypass.Error.Should().StartWith("UNPROCESSABLE:");

        // 3. Loại trừ nội bộ GV khi nộp lại: bộ đề REJECTED của GV B có chứa q3 trùng với bộ đề DRAFT khác của chính GV B -> bị chặn UNPROCESSABLE
        var rejectedSelfSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = structure.Id,
            CourseId = course.Id,
            CreatedBy = lecB.Id,
            SetCode = "SWD-L2-REJ-SELF",
            Status = ExamSetStatus.Rejected,
            IsSampleSet = false,
            IsActive = true
        };
        rejectedSelfSet.ExamSetQuestions.Add(new ExamSetQuestion
        {
            Id = Guid.NewGuid(),
            ExamSetId = rejectedSelfSet.Id,
            ExamQuestionId = q3.Id,
            OrderIndex = 1
        });
        db.ExamSets.Add(rejectedSelfSet);
        await db.SaveChangesAsync();

        var resubmitSelfOverlap = await submitHandler.Handle(
            new SubmitExamSetForReviewCommand(rejectedSelfSet.Id, lecB.Id, UserRole.Lecturer), CancellationToken.None);
        resubmitSelfOverlap.IsSuccess.Should().BeFalse();
        resubmitSelfOverlap.Error.Should().StartWith("UNPROCESSABLE:");
    }

    [Fact(DisplayName = "PR4-RT-02: Luồng CreateExamSet -> Submit -> ApproveExamSet giữ nguyên PENDING_CURATION, chống zombie DRAFT khi lọc kho & đồng bộ làm tròn Trigram")]
    public async Task InlineManualQuestion_PreservesPendingCurationAcrossApproval_BlocksUnapprovedCuration_AndMatchesTrigramBandRounding()
    {
        // Kiểm tra làm tròn nhất quán: 0.7996 -> 80.0% phải xếp RED_80_PLUS (không được hiện 80.0% mà band vàng)
        TrigramSimilarityCalculator.ToSimilarityPercent(0.7996m).Should().Be(80.0m);
        TrigramSimilarityCalculator.ClassifyBand(0.7996m).Should().Be(TrigramSimilarityCalculator.BandRed80Plus);
        TrigramSimilarityCalculator.ToSimilarityPercent(0.6996m).Should().Be(70.0m);
        TrigramSimilarityCalculator.ClassifyBand(0.6996m).Should().Be(TrigramSimilarityCalculator.BandYellow70To79);

        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-rt2@fpt.edu.vn", FullName = "Head RT2", Role = UserRole.DepartmentHead, IsActive = true };
        var lec = new User { Id = Guid.NewGuid(), Email = "lec-rt2@fpt.edu.vn", FullName = "GV RT2", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN222", Name = "PRN222", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "Struct RT2", TotalQuestions = 1, RequiredSetCount = 2, IsActive = true };

        db.Users.AddRange(head, lec);
        db.Courses.Add(course);
        db.CourseLecturers.Add(new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec.Id, AssignedByUserId = head.Id, IsActive = true });
        db.ExamStructures.Add(structure);
        await db.SaveChangesAsync();

        var createHandler = new CreateExamSetCommandHandler(db);
        var submitHandler = new SubmitExamSetForReviewCommandHandler(db);
        var approveHandler = new ApproveExamSetCommandHandler(db);
        var candidatesHandler = new GetBankCurationCandidatesQueryHandler(db);
        var curateHandler = new CurateExamQuestionBankCommandHandler(db);

        // Tạo bộ đề có câu tạo tay inline
        var created = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "PRN222-INLINE-01", [], lec.Id, UserRole.Lecturer, false,
            [
                new CreateInlineExamQuestionDto
                {
                    Title = "Kiến trúc CQRS MediatR",
                    Content = "Giải thích lợi ích tách Command và Query trong MediatR cho hệ thống chấm thi vấn đáp.",
                    SampleAnswer = "CQRS tách luồng ghi dữ liệu và luồng đọc giúp tối ưu hóa AsNoTracking, bảo vệ tính toàn vẹn giao dịch và dễ kiểm thử độc lập.",
                    Difficulty = QuestionDifficulty.Medium,
                    BloomLevel = BloomLevel.Analyze,
                    Rubric = new RubricDraftDto("Rubric Inline RT2", null, 10.00m, ValidCriteria())
                }
            ]), CancellationToken.None);
        created.IsSuccess.Should().BeTrue(because: created.Error);

        var inlineQuestionId = created.Value.Questions.Single().QuestionId;

        // Khi câu còn DRAFT (chưa duyệt) -> KHÔNG xuất hiện trong hàng đợi lọc kho & bị chặn CURATED_INTO_BANK (CONFLICT)
        var beforeApproveCandidates = await candidatesHandler.Handle(
            new GetBankCurationCandidatesQuery(course.Id, head.Id, UserRole.DepartmentHead), CancellationToken.None);
        beforeApproveCandidates.IsSuccess.Should().BeTrue();
        beforeApproveCandidates.Value.TotalPendingCount.Should().Be(0);

        var curateDraftAttempt = await curateHandler.Handle(new CurateExamQuestionBankCommand(
            course.Id, [inlineQuestionId], BankCurationStatus.CuratedIntoBank, "Thử nạp câu chưa duyệt", head.Id, UserRole.DepartmentHead), CancellationToken.None);
        curateDraftAttempt.IsSuccess.Should().BeFalse();
        curateDraftAttempt.Error.Should().StartWith("CONFLICT:");

        // Submit -> ApproveExamSet: câu chuyển APPROVED nhưng vẫn giữ nguyên BankCurationStatus == PENDING_CURATION
        (await submitHandler.Handle(new SubmitExamSetForReviewCommand(created.Value.Id, lec.Id, UserRole.Lecturer), CancellationToken.None))
            .IsSuccess.Should().BeTrue();
        (await approveHandler.Handle(new ApproveExamSetCommand(created.Value.Id, "Duyệt đề inline", head.Id, UserRole.DepartmentHead), CancellationToken.None))
            .IsSuccess.Should().BeTrue();

        var afterApproveQuestion = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == inlineQuestionId);
        afterApproveQuestion.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        afterApproveQuestion.BankCurationStatus.Should().Be(BankCurationStatus.PendingCuration);

        // Sau khi duyệt -> xuất hiện trong hàng đợi lọc kho và nạp kho thành công kèm QuestionIds trong AuditLog
        var afterApproveCandidates = await candidatesHandler.Handle(
            new GetBankCurationCandidatesQuery(course.Id, head.Id, UserRole.DepartmentHead), CancellationToken.None);
        afterApproveCandidates.IsSuccess.Should().BeTrue();
        afterApproveCandidates.Value.Items.Should().ContainSingle(i => i.QuestionId == inlineQuestionId);

        var curateApproved = await curateHandler.Handle(new CurateExamQuestionBankCommand(
            course.Id, [inlineQuestionId], BankCurationStatus.CuratedIntoBank, "Nạp kho kỳ sau", head.Id, UserRole.DepartmentHead), CancellationToken.None);
        curateApproved.IsSuccess.Should().BeTrue(because: curateApproved.Error);

        var auditEntry = await db.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(a => a.Action == "CURATE_EXAM_QUESTION_BANK" && a.EntityId == course.Id);
        auditEntry.Should().NotBeNull();
        auditEntry!.NewValues.Should().Contain(inlineQuestionId.ToString());
    }

    [Fact(DisplayName = "PR4-RT-03: ApproveExamSet chặn IsSampleSet, DRAFT/REJECTED, bộ đề rỗng, câu EXCLUDED_FROM_BANK/SampleSet & Controller map RFC 7807")]
    public async Task ApproveExamSet_EnforcesAllM3Guards_AndControllerMapsRfc7807StatusCodes()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-rt3@fpt.edu.vn", FullName = "Head RT3", Role = UserRole.DepartmentHead, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWT301", Name = "Software Testing", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "Struct RT3", IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R-RT3", TotalMaxScore = 10.00m, IsActive = true };

        var qValid = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Valid Q", Content = "Content", ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };
        var qExcluded = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Excluded Q", Content = "Content", ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.ExcludedFromBank, IsActive = true };
        var qInSample = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Sample Q", Content = "Content", ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };

        var sampleSet = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, SetCode = "SWT-SAMPLE", Status = ExamSetStatus.Draft, IsSampleSet = true, IsActive = true };
        sampleSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = sampleSet.Id, ExamQuestionId = qInSample.Id, OrderIndex = 1 });

        var draftSet = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, SetCode = "SWT-DRAFT", Status = ExamSetStatus.Draft, IsActive = true };
        draftSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = draftSet.Id, ExamQuestionId = qValid.Id, OrderIndex = 1 });

        var rejectedSet = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, SetCode = "SWT-REJ", Status = ExamSetStatus.Rejected, IsActive = true };
        rejectedSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = rejectedSet.Id, ExamQuestionId = qValid.Id, OrderIndex = 1 });

        var emptySet = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, SetCode = "SWT-EMPTY", Status = ExamSetStatus.SubmittedForReview, IsActive = true };

        var excludedSet = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, SetCode = "SWT-EXCL", Status = ExamSetStatus.SubmittedForReview, IsActive = true };
        excludedSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = excludedSet.Id, ExamQuestionId = qExcluded.Id, OrderIndex = 1 });

        var sampleCollisionSet = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, SetCode = "SWT-SAMPCOL", Status = ExamSetStatus.SubmittedForReview, IsActive = true };
        sampleCollisionSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = sampleCollisionSet.Id, ExamQuestionId = qInSample.Id, OrderIndex = 1 });

        db.Users.Add(head);
        db.Courses.Add(course);
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.AddRange(qValid, qExcluded, qInSample);
        db.ExamSets.AddRange(sampleSet, draftSet, rejectedSet, emptySet, excludedSet, sampleCollisionSet);
        await db.SaveChangesAsync();

        var handler = new ApproveExamSetCommandHandler(db);

        (await handler.Handle(new ApproveExamSetCommand(sampleSet.Id, null, head.Id, UserRole.DepartmentHead), CancellationToken.None))
            .Error.Should().StartWith("UNPROCESSABLE:");
        (await handler.Handle(new ApproveExamSetCommand(draftSet.Id, null, head.Id, UserRole.DepartmentHead), CancellationToken.None))
            .Error.Should().StartWith("CONFLICT:");
        (await handler.Handle(new ApproveExamSetCommand(rejectedSet.Id, null, head.Id, UserRole.DepartmentHead), CancellationToken.None))
            .Error.Should().StartWith("CONFLICT:");
        (await handler.Handle(new ApproveExamSetCommand(emptySet.Id, null, head.Id, UserRole.DepartmentHead), CancellationToken.None))
            .Error.Should().StartWith("UNPROCESSABLE:");
        (await handler.Handle(new ApproveExamSetCommand(excludedSet.Id, null, head.Id, UserRole.DepartmentHead), CancellationToken.None))
            .Error.Should().StartWith("CONFLICT:");
        (await handler.Handle(new ApproveExamSetCommand(sampleCollisionSet.Id, null, head.Id, UserRole.DepartmentHead), CancellationToken.None))
            .Error.Should().StartWith("CONFLICT:");

        // Kiểm tra OfficialExamsController.ApproveExamSet ánh xạ đủ 403 / 404 / 409 / 422 chuẩn RFC 7807
        var senderMock = new Mock<ISender>();
        var controller = new OfficialExamsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(head.Id, UserRole.DepartmentHead) }
            }
        };

        var cases = new[]
        {
            (OfficialExamErrors.Forbidden, StatusCodes.Status403Forbidden),
            (OfficialExamErrors.ExamSetNotFound, StatusCodes.Status404NotFound),
            (OfficialExamErrors.ExamSetAlreadyApproved, StatusCodes.Status409Conflict),
            (OfficialExamErrors.ExamSetAlreadyHandedOver, StatusCodes.Status409Conflict),
            ("CONFLICT: Bộ đề chưa ở trạng thái chờ duyệt.", StatusCodes.Status409Conflict),
            ("UNPROCESSABLE: Bộ đề mẫu không đi qua quy trình phê duyệt.", StatusCodes.Status422UnprocessableEntity)
        };

        foreach (var (error, expectedStatus) in cases)
        {
            senderMock.Setup(s => s.Send(It.IsAny<ApproveExamSetCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ApproveExamSetResultDto>.Failure(error));

            var action = await controller.ApproveExamSet(draftSet.Id, null, CancellationToken.None);
            var objectResult = action.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(expectedStatus);
            objectResult.Value.Should().BeOfType<ProblemDetails>().Which.Status.Should().Be(expectedStatus);
        }
    }

    [Fact(DisplayName = "PR4-RT-04: UpdateRubric chặn đề HANDED_OVER, chặn GV sửa câu APPROVED, chặn MaxScore 3 chữ số thập phân & RubricsController trả ProblemDetails")]
    public async Task UpdateRubric_EnforcesHandedOverApprovedLecturerPracticeAndPrecisionGuards_AndControllerReturnsProblemDetails()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-rt4@fpt.edu.vn", FullName = "Head RT4", Role = UserRole.DepartmentHead, IsActive = true };
        var lec = new User { Id = Guid.NewGuid(), Email = "lec-rt4@fpt.edu.vn", FullName = "GV RT4", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "Struct RT4", IsActive = true };

        var rubricPrecision = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R-Prec", TotalMaxScore = 10.00m, IsActive = true };
        var rubricHandedOver = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R-Hand", TotalMaxScore = 10.00m, IsActive = true };
        var rubricApprovedOnly = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R-App", TotalMaxScore = 10.00m, IsActive = true };
        var rubricPracticeUsed = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R-Prac", TotalMaxScore = 10.00m, IsActive = true };

        var qHanded = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubricHandedOver.Id, Title = "Q Handed", Content = "Content", ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var handedSet = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, SetCode = "PRN-HAND-01", Status = ExamSetStatus.HandedOver, IsActive = true };
        handedSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = handedSet.Id, ExamQuestionId = qHanded.Id, OrderIndex = 1 });

        var qApproved = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubricApprovedOnly.Id, Title = "Q App", Content = "Content", ApprovalStatus = ApprovalStatus.Approved, IsActive = true };

        var pqUsed = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubricPracticeUsed.Id, Title = "PQ Used", Content = "Content", ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var practiceAns = new PracticeAnswer { Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), QuestionId = pqUsed.Id, StudentId = Guid.NewGuid(), AnswerText = "Câu trả lời luyện tập" };

        db.Users.AddRange(head, lec);
        db.Courses.Add(course);
        db.CourseLecturers.Add(new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec.Id, AssignedByUserId = head.Id, IsActive = true });
        db.ExamStructures.Add(structure);
        db.Rubrics.AddRange(rubricPrecision, rubricHandedOver, rubricApprovedOnly, rubricPracticeUsed);
        db.ExamQuestions.AddRange(qHanded, qApproved);
        db.ExamSets.Add(handedSet);
        db.PracticeQuestions.Add(pqUsed);
        db.PracticeAnswers.Add(practiceAns);
        await db.SaveChangesAsync();

        var handler = new UpdateRubricCommandHandler(db);

        // 1. MaxScore 3 chữ số thập phân (3.333 + 3.333 + 3.334 = 10.000) -> INVARIANT (422)
        var threeDecimals = await handler.Handle(new UpdateRubricCommand(
            head.Id, rubricPrecision.Id, "Bad Precision", null,
            [
                new("C1", 3.333m, 0.33m, BloomLevel.Understand, "D1", 1),
                new("C2", 3.333m, 0.33m, BloomLevel.Apply, "D2", 2),
                new("C3", 3.334m, 0.34m, BloomLevel.Analyze, "D3", 3)
            ]), CancellationToken.None);
        threeDecimals.IsSuccess.Should().BeFalse();
        threeDecimals.Error.Should().StartWith("INVARIANT:");

        // 2. BloomLevel không hợp lệ -> VALIDATION (422)
        var badBloom = await handler.Handle(new UpdateRubricCommand(
            head.Id, rubricPrecision.Id, "Bad Bloom", null,
            [
                new("C1", 5.00m, 0.50m, "SuperBloomLevel", "D1", 1),
                new("C2", 5.00m, 0.50m, BloomLevel.Apply, "D2", 2)
            ]), CancellationToken.None);
        badBloom.IsSuccess.Should().BeFalse();
        badBloom.Error.Should().StartWith("VALIDATION:");

        // 3. Rubric gắn bộ đề HANDED_OVER -> chặn cả TBM (CONFLICT 409)
        var handedConflict = await handler.Handle(new UpdateRubricCommand(
            head.Id, rubricHandedOver.Id, "Cố sửa đề đã bàn giao", null, ValidCriteria()), CancellationToken.None);
        handedConflict.IsSuccess.Should().BeFalse();
        handedConflict.Error.Should().StartWith("CONFLICT:");

        // 4. Rubric gắn PracticeQuestion đã có bài làm -> chặn cả TBM (CONFLICT 409)
        var practiceConflict = await handler.Handle(new UpdateRubricCommand(
            head.Id, rubricPracticeUsed.Id, "Cố sửa rubric đã có bài luyện tập", null, ValidCriteria()), CancellationToken.None);
        practiceConflict.IsSuccess.Should().BeFalse();
        practiceConflict.Error.Should().StartWith("CONFLICT:");

        // 5. Rubric gắn câu APPROVED (chưa vào bộ đề OFFICIAL/chưa có bài làm): GV bị chặn CONFLICT, TBM vẫn sửa được
        var lecEditApproved = await handler.Handle(new UpdateRubricCommand(
            lec.Id, rubricApprovedOnly.Id, "GV lách sửa barem", null, ValidCriteria()), CancellationToken.None);
        lecEditApproved.IsSuccess.Should().BeFalse();
        lecEditApproved.Error.Should().StartWith("CONFLICT:");

        var headEditApproved = await handler.Handle(new UpdateRubricCommand(
            head.Id, rubricApprovedOnly.Id, "TBM hiệu chỉnh barem trước khi phát sinh bài làm", null, ValidCriteria()), CancellationToken.None);
        headEditApproved.IsSuccess.Should().BeTrue(because: headEditApproved.Error);

        // 6. RubricsController trả chuẩn RFC 7807 ProblemDetails
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<UpdateRubricCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CreateRubricResponseDto>.Failure("CONFLICT: Rubric đã khóa."));
        var controller = new RubricsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(lec.Id, UserRole.Lecturer) }
            }
        };
        var conflictAction = await controller.UpdateRubric(rubricHandedOver.Id, new UpdateRubricRequestDto("X", null, ValidCriteria()));
        conflictAction.Should().BeOfType<ConflictObjectResult>()
            .Which.Value.Should().BeOfType<ProblemDetails>()
            .Which.Status.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact(DisplayName = "PR4-RT-05: CreateExamSet chặn BOLA Draft GV khác, SessionId khác môn, inline SampleAnswer null; GetExamStructures chặn BOLA & UpdateExamStructure đồng bộ Bloom cũ")]
    public async Task CreateExamSet_AndExamStructures_EnforceBolaSessionCourseSampleAnswerAndEffectiveBloom()
    {
        await using var db = CreateDbContext();

        var headA = new User { Id = Guid.NewGuid(), Email = "heada-rt5@fpt.edu.vn", FullName = "Head A", Role = UserRole.DepartmentHead, IsActive = true };
        var headB = new User { Id = Guid.NewGuid(), Email = "headb-rt5@fpt.edu.vn", FullName = "Head B", Role = UserRole.DepartmentHead, IsActive = true };
        var lecA1 = new User { Id = Guid.NewGuid(), Email = "leca1-rt5@fpt.edu.vn", FullName = "GV A1", Role = UserRole.Lecturer, IsActive = true };
        var lecA2 = new User { Id = Guid.NewGuid(), Email = "leca2-rt5@fpt.edu.vn", FullName = "GV A2", Role = UserRole.Lecturer, IsActive = true };
        var officer = new User { Id = Guid.NewGuid(), Email = "eo-rt5@fpt.edu.vn", FullName = "Khảo thí", Role = UserRole.ExaminationOfficer, IsActive = true };

        var courseA = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", DepartmentHeadUserId = headA.Id, IsActive = true };
        var courseB = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "SWD392", DepartmentHeadUserId = headB.Id, IsActive = true };

        var structA = new ExamStructure { Id = Guid.NewGuid(), CourseId = courseA.Id, CreatedBy = headA.Id, Name = "Struct A", TotalQuestions = 6, DurationMinutes = 30, BloomDistribution = "{\"Understand\":3,\"Apply\":3}", IsActive = true };
        var structB = new ExamStructure { Id = Guid.NewGuid(), CourseId = courseB.Id, CreatedBy = headB.Id, Name = "Struct B", TotalQuestions = 5, DurationMinutes = 30, BloomDistribution = "{}", IsActive = true };
        var sessionB = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = courseB.Id, ExamStructureId = structB.Id, Title = "FE Course B", ExamDate = new DateOnly(2026, 12, 1), Status = "scheduled" };

        var rubricA = new Rubric { Id = Guid.NewGuid(), CourseId = courseA.Id, Name = "R-A", TotalMaxScore = 10.00m, IsActive = true };
        var lecA1Draft = new ExamQuestion { Id = Guid.NewGuid(), CourseId = courseA.Id, RubricId = rubricA.Id, Title = "Draft A1", Content = "Private draft", ApprovalStatus = ApprovalStatus.Draft, BankCurationStatus = BankCurationStatus.BankReady, SubmittedBy = lecA1.Id, IsActive = false };
        var validApproved = new ExamQuestion { Id = Guid.NewGuid(), CourseId = courseA.Id, RubricId = rubricA.Id, Title = "App A", Content = "Approved", ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };

        db.Users.AddRange(headA, headB, lecA1, lecA2, officer);
        db.Courses.AddRange(courseA, courseB);
        db.CourseLecturers.AddRange(
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = courseA.Id, LecturerUserId = lecA1.Id, AssignedByUserId = headA.Id, IsActive = true },
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = courseA.Id, LecturerUserId = lecA2.Id, AssignedByUserId = headA.Id, IsActive = true });
        db.ExamStructures.AddRange(structA, structB);
        db.OfficialExamSessions.Add(sessionB);
        db.Rubrics.Add(rubricA);
        db.ExamQuestions.AddRange(lecA1Draft, validApproved);
        await db.SaveChangesAsync();

        var createSetHandler = new CreateExamSetCommandHandler(db);

        // 1. BOLA Draft: lecA2 KHÔNG được chọn câu Draft do lecA1 soạn -> FORBIDDEN
        var draftBola = await createSetHandler.Handle(new CreateExamSetCommand(
            structA.Id, courseA.Id, null, "SET-BOLA-DRAFT", [lecA1Draft.Id], lecA2.Id, UserRole.Lecturer), CancellationToken.None);
        draftBola.IsSuccess.Should().BeFalse();
        draftBola.Error.Should().StartWith("FORBIDDEN:");

        // 2. SessionId xuyên môn (sessionB thuộc courseB) -> UNPROCESSABLE
        var crossSession = await createSetHandler.Handle(new CreateExamSetCommand(
            structA.Id, courseA.Id, sessionB.Id, "SET-CROSS-SESS", [validApproved.Id], lecA1.Id, UserRole.Lecturer), CancellationToken.None);
        crossSession.IsSuccess.Should().BeFalse();
        crossSession.Error.Should().StartWith("UNPROCESSABLE:");

        // 3. Inline manual question với SampleAnswer == null -> UNPROCESSABLE (Invariant #7)
        var nullSampleAnswer = await createSetHandler.Handle(new CreateExamSetCommand(
            structA.Id, courseA.Id, null, "SET-NULL-ANS", [], lecA1.Id, UserRole.Lecturer, false,
            [
                new CreateInlineExamQuestionDto
                {
                    Title = "Inline Thiếu Model Answer",
                    Content = "Nội dung câu hỏi hợp lệ nhưng thiếu câu trả lời mẫu.",
                    SampleAnswer = null,
                    Rubric = new RubricDraftDto("R-Valid", null, 10.00m, ValidCriteria())
                }
            ]), CancellationToken.None);
        nullSampleAnswer.IsSuccess.Should().BeFalse();
        nullSampleAnswer.Error.Should().StartWith("UNPROCESSABLE:");

        // 4. UpdateExamStructure giảm TotalQuestions xuống 4 mà bỏ trống BloomDistribution (cũ tổng = 6) -> VALIDATION
        var updateStructHandler = new UpdateExamStructureCommandHandler(db);
        var bloomOverflow = await updateStructHandler.Handle(new UpdateExamStructureCommand(
            headA.Id, structA.Id, "Struct A Shrunk", TotalQuestions: 4, DurationMinutes: 30, BloomDistribution: null, IsActive: true), CancellationToken.None);
        bloomOverflow.IsSuccess.Should().BeFalse();
        bloomOverflow.Error.Should().StartWith("VALIDATION:");

        // 5. GetExamStructures BOLA scoping: headA & lecA1 chỉ thấy structA; officer thấy cả structA & structB
        var getStructHandler = new GetExamStructuresQueryHandler(db);
        var headAStructs = await getStructHandler.Handle(new GetExamStructuresQuery(null, null, true, headA.Id, UserRole.DepartmentHead), CancellationToken.None);
        headAStructs.IsSuccess.Should().BeTrue();
        headAStructs.Value.Should().ContainSingle(s => s.Id == structA.Id);

        var lecA1Structs = await getStructHandler.Handle(new GetExamStructuresQuery(null, null, true, lecA1.Id, UserRole.Lecturer), CancellationToken.None);
        lecA1Structs.IsSuccess.Should().BeTrue();
        lecA1Structs.Value.Should().ContainSingle(s => s.Id == structA.Id);

        var officerStructs = await getStructHandler.Handle(new GetExamStructuresQuery(null, null, true, officer.Id, UserRole.ExaminationOfficer), CancellationToken.None);
        officerStructs.IsSuccess.Should().BeTrue();
        officerStructs.Value.Should().HaveCount(2);
    }
}
