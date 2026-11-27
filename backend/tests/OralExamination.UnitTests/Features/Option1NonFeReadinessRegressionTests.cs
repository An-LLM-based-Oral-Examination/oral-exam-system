using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using OralExamination.API.Configuration;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.Queries.GetAppealEvidence;
using OralExamination.Application.Features.OfficialExams.Commands.AssignExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.OverrideExamScore;
using OralExamination.Application.Features.OfficialExams.Commands.PublishGrades;
using OralExamination.Application.Features.OfficialExams.Commands.ReturnExamSetForRevision;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitExamSetForReview;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamSet;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.Queries.GetAuditEvidence;
using OralExamination.Application.Features.OfficialExams.Queries.GetBankCurationCandidates;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamSets;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamAudioPlaybackUrl;
using OralExamination.Application.Features.Practice.Queries.GetPracticeRuntimeConfig;
using OralExamination.Application.Features.Proctor.Commands.SuspendExamTicket;
using OralExamination.Application.Features.QuestionBank.Commands.MutateQuestion;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Application.Features.QuestionBank.Queries.GetQuestions;
using OralExamination.Application.Features.Student.Queries.GetOfficialExamGrade;
using OralExamination.Application.Features.Student.Queries.GetStudentDashboard;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Services;
using Xunit;

namespace OralExamination.UnitTests.Features;

public sealed class Option1NonFeReadinessRegressionTests
{
    private static OralExamDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase($"opt1-{Guid.NewGuid():N}")
            .Options);

    private static User CreateUser(string role, string prefix, string? studentCode = null) => new()
    {
        Id = Guid.NewGuid(),
        Email = $"{prefix}.{Guid.NewGuid():N}@fpt.edu.vn",
        FullName = $"User {prefix}",
        StudentCode = studentCode,
        Role = role,
        IsActive = true
    };

    private static (Semester Semester, Course Course, Rubric Rubric, List<RubricCriterion> Criteria) CreateAcademicGraph(Guid deptHeadId)
    {
        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = $"FA{Guid.NewGuid():N}"[..8],
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            Credits = 3,
            SemesterId = semester.Id,
            DepartmentHeadUserId = deptHeadId,
            IsActive = true
        };
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Standard 10.0 Rubric",
            TotalMaxScore = 10.00m,
            IsActive = true
        };
        var criteria = new List<RubricCriterion>
        {
            new() { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Architecture Clarity", MaxScore = 5.00m, Weight = 0.50m, BloomLevel = BloomLevel.Understand, OrderIndex = 1 },
            new() { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Trade-off Analysis", MaxScore = 5.00m, Weight = 0.50m, BloomLevel = BloomLevel.Analyze, OrderIndex = 2 }
        };
        return (semester, course, rubric, criteria);
    }

    [Fact]
    public async Task Opt1_01_WindowBlurAutoLock_SuspendAndOverrideUnblockPublishGrades_WhilePostPublishRemainsLocked()
    {
        await using var context = CreateDbContext();
        var officer = CreateUser(UserRole.ExaminationOfficer, "eo");
        var deptHead = CreateUser(UserRole.DepartmentHead, "tbm");
        var examiner = CreateUser(UserRole.Lecturer, "examiner");
        var proctor = CreateUser(UserRole.Proctor, "proctor");
        var sv1 = CreateUser(UserRole.Student, "sv1", "SE180101");
        var sv2 = CreateUser(UserRole.Student, "sv2", "SE180102");
        var (semester, course, rubric, criteria) = CreateAcademicGraph(deptHead.Id);

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SemesterId = semester.Id,
            Title = "FE SWD392",
            ExamDate = new DateOnly(2026, 11, 25),
            CreatedBy = officer.Id
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Kip 1",
            RoomLab = "Lab 301",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            ProctorUserId = proctor.Id,
            ExaminerUserId = examiner.Id,
            Status = "completed",
            MaxSeats = 24
        };
        var q1 = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Q1",
            Content = "Explain CQRS",
            SampleAnswer = new string('A', 60),
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        // Cả 2 vé bị Kiosk auto-lock WINDOW_BLUR > 3: Status = LOCKED, IsLocked = false, GradesPublishedAt = null, chưa có LecturerAudit
        var ticket1 = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = sv1.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.Locked,
            IsLocked = false,
            GradesPublishedAt = null
        };
        var ticket2 = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = sv2.Id,
            SeatNumber = 2,
            Status = ExamTicketStatus.Locked,
            IsLocked = false,
            GradesPublishedAt = null
        };
        var sub2 = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket2.Id,
            ExamQuestionId = q1.Id,
            TranscriptWhisper = "Student 2 submitted before blur lock",
            AiScore = 7.0m,
            FinalScore = 7.0m,
            GradingStatus = "ai_graded"
        };

        // Vé 3 đã bị LockExamTicket thủ công (Status = LOCKED, IsLocked = true, chưa công bố) -> PHẢI bị chặn LOCKED:
        var sv3 = CreateUser(UserRole.Student, "sv3", "SE180103");
        var manuallyLockedTicket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = sv3.Id,
            SeatNumber = 3,
            Status = ExamTicketStatus.Locked,
            IsLocked = true,
            GradesPublishedAt = null
        };
        var sub3 = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = manuallyLockedTicket.Id,
            ExamQuestionId = q1.Id,
            TranscriptWhisper = "Locked by lecturer before publish",
            AiScore = 8.0m,
            FinalScore = 8.0m,
            GradingStatus = "ai_graded"
        };

        context.Users.AddRange(officer, deptHead, examiner, proctor, sv1, sv2, sv3);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.Rubrics.Add(rubric);
        context.RubricCriteria.AddRange(criteria);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        context.ExamQuestions.Add(q1);
        context.StudentExamTickets.AddRange(ticket1, ticket2, manuallyLockedTicket);
        context.ExamQuestionSubmissions.AddRange(sub2, sub3);
        await context.SaveChangesAsync();

        // Vé đã LockExamTicket (IsLocked = true, chưa công bố) PHẢI bị chặn bởi cả OverrideExamScore lẫn SuspendExamTicket
        var manualLockOverride = await new OverrideExamScoreCommandHandler(context)
            .Handle(new OverrideExamScoreCommand(examiner.Id, manuallyLockedTicket.Id, 9.0m, "Cố sửa vé đã bị giảng viên khóa cứng trước công bố."), CancellationToken.None);
        manualLockOverride.IsSuccess.Should().BeFalse();
        manualLockOverride.Error.Should().StartWith("LOCKED:");

        var manualLockSuspend = await new SuspendExamTicketCommandHandler(context)
            .Handle(new SuspendExamTicketCommand(officer.Id, manuallyLockedTicket.Id, "Cố đình chỉ vé đã bị khóa cứng trước công bố."), CancellationToken.None);
        manualLockSuspend.IsSuccess.Should().BeFalse();
        manualLockSuspend.Error.Should().StartWith("LOCKED:");

        // Trước khi xử lý: PublishGrades bị chặn vì ticket1 chưa có điểm
        var prePublish = await new PublishGradesCommandHandler(context)
            .Handle(new PublishGradesCommand(shift.Id, officer.Id), CancellationToken.None);
        prePublish.IsSuccess.Should().BeFalse();

        // Lối thoát 1: Khảo thí SuspendExamTicket gán 0đ cho ticket1 bị auto-lock
        var suspendRes = await new SuspendExamTicketCommandHandler(context)
            .Handle(new SuspendExamTicketCommand(officer.Id, ticket1.Id, "Xác nhận vi phạm chuyển cửa sổ hơn 3 lần trong phòng thi.", 4), CancellationToken.None);
        suspendRes.IsSuccess.Should().BeTrue(suspendRes.Error);

        // Lối thoát 2: Giám khảo OverrideExamScore cho ticket2 bị auto-lock (chưa công bố, chưa One-Way Lock)
        var overrideRes = await new OverrideExamScoreCommandHandler(context)
            .Handle(new OverrideExamScoreCommand(examiner.Id, ticket2.Id, 7.50m, "Đã xác minh sự cố hệ điều hành, chấm theo bài thu âm đã nộp."), CancellationToken.None);
        overrideRes.IsSuccess.Should().BeTrue(overrideRes.Error);
        overrideRes.Value.TicketStatus.Should().Be(ExamTicketStatus.Audited);

        // PublishGrades giờ thành công cho cả 3 thí sinh
        var publishRes = await new PublishGradesCommandHandler(context)
            .Handle(new PublishGradesCommand(shift.Id, officer.Id), CancellationToken.None);
        publishRes.IsSuccess.Should().BeTrue(publishRes.Error);
        publishRes.Value.TotalPublished.Should().Be(3);

        // Sau PublishGrades (One-Way Lock): cả OverrideExamScore và SuspendExamTicket ĐỀU bị chặn LOCKED:
        var postOverride = await new OverrideExamScoreCommandHandler(context)
            .Handle(new OverrideExamScoreCommand(examiner.Id, ticket2.Id, 9.00m, "Cố sửa điểm sau khi đã công bố One-Way Lock."), CancellationToken.None);
        postOverride.IsSuccess.Should().BeFalse();
        postOverride.Error.Should().StartWith("LOCKED:");

        var postSuspend = await new SuspendExamTicketCommandHandler(context)
            .Handle(new SuspendExamTicketCommand(officer.Id, ticket2.Id, "Cố đình chỉ sau khi bảng điểm đã công bố One-Way Lock."), CancellationToken.None);
        postSuspend.IsSuccess.Should().BeFalse();
        postSuspend.Error.Should().StartWith("LOCKED:");
    }

    [Fact]
    public async Task Opt1_02_PrePublishLock_NeverLeaksGrade_AndDashboardUsesFollowUpAggregator()
    {
        await using var context = CreateDbContext();
        var officer = CreateUser(UserRole.ExaminationOfficer, "eo");
        var deptHead = CreateUser(UserRole.DepartmentHead, "tbm");
        var student = CreateUser(UserRole.Student, "sv", "SE180201");
        var (semester, course, rubric, criteria) = CreateAcademicGraph(deptHead.Id);

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SemesterId = semester.Id,
            Title = "FE SWD392",
            ExamDate = new DateOnly(2026, 11, 25),
            HasFollowUp = true,
            MaxFollowUpQuestions = 2,
            CreatedBy = officer.Id
        };
        var shiftLockedPrePublish = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Kip PrePublish",
            RoomLab = "Lab 301",
            StartTime = DateTime.UtcNow.AddHours(-3),
            EndTime = DateTime.UtcNow.AddHours(-2),
            Status = "completed",
            MaxSeats = 24
        };
        var shiftPublished = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Kip Published",
            RoomLab = "Lab 302",
            StartTime = DateTime.UtcNow.AddHours(-5),
            EndTime = DateTime.UtcNow.AddHours(-4),
            Status = "completed",
            MaxSeats = 24
        };
        var q1 = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Q1",
            Content = "Explain CAP theorem",
            SampleAnswer = new string('C', 60),
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };

        // Vé 1: bị LockExamTicket (Status = LOCKED, IsLocked = true) nhưng GradesPublishedAt = null -> KHÔNG ĐƯỢC lộ điểm
        var prePublishTicket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftLockedPrePublish.Id,
            StudentId = student.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.Locked,
            IsLocked = true,
            GradesPublishedAt = null
        };
        var preSub = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = prePublishTicket.Id,
            ExamQuestionId = q1.Id,
            AiScore = 8.50m,
            FinalScore = 8.50m,
            GradingStatus = "ai_graded"
        };

        // Vé 2: đã công bố (PUBLISHED + GradesPublishedAt) có 1 câu chính (6.0) + 1 câu follow-up (8.0)
        var publishedTicket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftPublished.Id,
            StudentId = student.Id,
            SeatNumber = 2,
            Status = ExamTicketStatus.Published,
            IsLocked = true,
            GradesPublishedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var mainSub = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = publishedTicket.Id,
            ExamQuestionId = q1.Id,
            IsFollowUp = false,
            AiScore = 6.00m,
            FinalScore = 6.00m,
            GradingStatus = "ai_graded"
        };
        var followUpSub = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = publishedTicket.Id,
            ExamQuestionId = q1.Id,
            IsFollowUp = true,
            ParentSubmissionId = mainSub.Id,
            FollowUpIndex = 1,
            AiScore = 7.00m,
            FinalScore = 7.00m,
            GradingStatus = "ai_graded"
        };
        // Bắt buộc có AiEvaluation + AiEvaluationDetail khác nhau trên 2 tiêu chí:
        // Main = (5.0, 1.0) = 6.0; FollowUp = (2.0, 5.0) = 7.0 -> Merged per-criterion Max = (5.0 + 5.0) = 10.00!
        // Nếu Dashboard quên Include(AiEvaluations -> Details -> Criterion) sẽ rơi fallback ra 7.00 (lệch 3.0 điểm)!
        var mainEval = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerType = "exam",
            ExamSubmissionId = mainSub.Id,
            TotalScore = 6.00m,
            ConfidenceScore = 0.88m,
            EvaluatedAt = DateTime.UtcNow.AddMinutes(-20),
            Details =
            [
                new AiEvaluationDetail { Id = Guid.NewGuid(), CriterionId = criteria[0].Id, Score = 5.00m, Comment = "Strong C1" },
                new AiEvaluationDetail { Id = Guid.NewGuid(), CriterionId = criteria[1].Id, Score = 1.00m, Comment = "Weak C2" }
            ]
        };
        var followUpEval = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerType = "exam",
            ExamSubmissionId = followUpSub.Id,
            TotalScore = 7.00m,
            ConfidenceScore = 0.90m,
            EvaluatedAt = DateTime.UtcNow.AddMinutes(-18),
            Details =
            [
                new AiEvaluationDetail { Id = Guid.NewGuid(), CriterionId = criteria[0].Id, Score = 2.00m, Comment = "FollowUp C1" },
                new AiEvaluationDetail { Id = Guid.NewGuid(), CriterionId = criteria[1].Id, Score = 5.00m, Comment = "Recovered C2" }
            ]
        };

        context.Users.AddRange(officer, deptHead, student);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.Rubrics.Add(rubric);
        context.RubricCriteria.AddRange(criteria);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.AddRange(shiftLockedPrePublish, shiftPublished);
        context.ExamQuestions.Add(q1);
        context.StudentExamTickets.AddRange(prePublishTicket, publishedTicket);
        context.ExamQuestionSubmissions.AddRange(preSub, mainSub, followUpSub);
        context.AiEvaluations.AddRange(mainEval, followUpEval);
        await context.SaveChangesAsync();

        // GetOfficialExamGrade phải chặn vé chưa công bố dù Status == LOCKED
        var gradeBlocked = await new GetOfficialExamGradeQueryHandler(context)
            .Handle(new GetOfficialExamGradeQuery(prePublishTicket.Id, student.Id), CancellationToken.None);
        gradeBlocked.IsSuccess.Should().BeFalse();
        gradeBlocked.Error.Should().StartWith("UNPROCESSABLE:");

        // GetOfficialExamGrade của vé đã công bố phải gộp theo tiêu chí ra 10.00
        var publishedGrade = await new GetOfficialExamGradeQueryHandler(context)
            .Handle(new GetOfficialExamGradeQuery(publishedTicket.Id, student.Id), CancellationToken.None);
        publishedGrade.IsSuccess.Should().BeTrue(publishedGrade.Error);
        publishedGrade.Value.TotalScore.Should().Be(10.00m);

        // Student Dashboard phải ẩn điểm vé chưa công bố và KHỚP CHÍNH XÁC với GetOfficialExamGrade (10.00m, không phải 7.00m)
        var dashboard = await new GetStudentDashboardQueryHandler(context)
            .Handle(new GetStudentDashboardQuery(student.Id), CancellationToken.None);
        dashboard.IsSuccess.Should().BeTrue(dashboard.Error);

        var hiddenItem = dashboard.Value.OfficialExamTickets.Single(t => t.TicketId == prePublishTicket.Id);
        hiddenItem.FinalScore.Should().BeNull();
        hiddenItem.Status.Should().Be(ExamTicketStatus.Locked);

        var publishedItem = dashboard.Value.OfficialExamTickets.Single(t => t.TicketId == publishedTicket.Id);
        publishedItem.FinalScore.Should().Be(publishedGrade.Value.TotalScore);
        publishedItem.FinalScore.Should().Be(10.00m);
    }

    [Fact]
    public async Task Opt1_03_AuditAndAppealEvidence_ExposeAllMainSubmissions_Criteria_Timestamps_R2DegradeAndRefreshUrl()
    {
        await using var context = CreateDbContext();
        var officer = CreateUser(UserRole.ExaminationOfficer, "eo");
        var deptHead = CreateUser(UserRole.DepartmentHead, "tbm");
        var examiner = CreateUser(UserRole.Lecturer, "examiner");
        var appealLecturer = CreateUser(UserRole.Lecturer, "regrader");
        var unrelatedLecturer = CreateUser(UserRole.Lecturer, "stranger");
        var proctor = CreateUser(UserRole.Proctor, "proctor");
        var student = CreateUser(UserRole.Student, "sv", "SE180301");
        var (semester, course, rubric, criteria) = CreateAcademicGraph(deptHead.Id);

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SemesterId = semester.Id,
            Title = "FE Multi-Question",
            ExamDate = new DateOnly(2026, 11, 25),
            CreatedBy = officer.Id
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Kip 1",
            RoomLab = "Lab 303",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            ProctorUserId = proctor.Id,
            ExaminerUserId = examiner.Id,
            Status = "completed",
            MaxSeats = 24
        };
        var q1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Cau 1", Content = "Content 1", SampleAnswer = new string('1', 60), ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var q2 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Cau 2", Content = "Content 2", SampleAnswer = new string('2', 60), ApprovalStatus = ApprovalStatus.Approved, IsActive = true };

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 5,
            Status = ExamTicketStatus.Published,
            IsLocked = true,
            GradesPublishedAt = DateTime.UtcNow.AddMinutes(-30)
        };
        var sub1 = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = q1.Id,
            AudioObjectKey = "official/05_SE180301_q1.webm",
            AudioHashSha256 = "hash-q1",
            TranscriptWhisper = "Answer Q1",
            TimestampsWhisper = "[{\"start\":0,\"end\":5,\"text\":\"Answer Q1\"}]",
            AiScore = 7.0m,
            FinalScore = 7.0m,
            GradingStatus = "ai_graded",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-50)
        };
        var sub2 = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = q2.Id,
            AudioObjectKey = "official/05_SE180301_q2.webm",
            AudioHashSha256 = "hash-q2",
            TranscriptWhisper = "Answer Q2",
            TimestampsWhisper = "[{\"start\":0,\"end\":8,\"text\":\"Answer Q2\"}]",
            AiScore = 8.0m,
            FinalScore = 8.0m,
            GradingStatus = "ai_graded",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-45)
        };
        var eval1 = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerType = "exam",
            ExamSubmissionId = sub1.Id,
            TotalScore = 7.0m,
            ConfidenceScore = 0.85m,
            CotTrace = "CoT Q1",
            EvaluatedAt = DateTime.UtcNow.AddMinutes(-49),
            Details =
            [
                new AiEvaluationDetail { Id = Guid.NewGuid(), CriterionId = criteria[0].Id, Score = 3.5m, Comment = "Good Q1 C1" },
                new AiEvaluationDetail { Id = Guid.NewGuid(), CriterionId = criteria[1].Id, Score = 3.5m, Comment = "Good Q1 C2" }
            ]
        };
        var eval2 = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerType = "exam",
            ExamSubmissionId = sub2.Id,
            TotalScore = 8.0m,
            ConfidenceScore = 0.90m,
            CotTrace = "CoT Q2",
            EvaluatedAt = DateTime.UtcNow.AddMinutes(-44),
            Details =
            [
                new AiEvaluationDetail { Id = Guid.NewGuid(), CriterionId = criteria[0].Id, Score = 4.0m, Comment = "Strong Q2 C1" },
                new AiEvaluationDetail { Id = Guid.NewGuid(), CriterionId = criteria[1].Id, Score = 4.0m, Comment = "Strong Q2 C2" }
            ]
        };
        var audit = new LecturerAudit
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            LecturerId = examiner.Id,
            OriginalAiScore = 7.5m,
            AuditedScore = 8.0m,
            OverrideReason = "Điều chỉnh tiêu chí 2 của câu 2 sau khi nghe băng.",
            IsLocked = true,
            Details =
            [
                // Cố tình có 2 dòng trùng CriterionId (legacy SubmissionId = null + bản mới có SubmissionId = sub2.Id) -> KHÔNG được crash HTTP 500
                new LecturerAuditDetail { Id = Guid.NewGuid(), SubmissionId = null, CriterionId = criteria[1].Id, AiScore = 4.0m, AuditedScore = 4.0m, LecturerComment = "Legacy row" },
                new LecturerAuditDetail { Id = Guid.NewGuid(), SubmissionId = sub2.Id, CriterionId = criteria[1].Id, AiScore = 4.0m, AuditedScore = 4.5m, LecturerComment = "Trả lời đủ trade-off" }
            ]
        };
        var otherDeptHead = CreateUser(UserRole.DepartmentHead, "other-tbm");
        var legacyStudent = CreateUser(UserRole.Student, "sv-legacy", "SE180302");
        var legacySingleMainTicket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = legacyStudent.Id,
            SeatNumber = 6,
            Status = ExamTicketStatus.Published,
            AudioObjectKey = "official/06_SE180302_legacy.webm",
            AudioR2Url = "https://expired.example/old.webm",
            AudioHashSha256 = "hash-legacy"
        };
        var legacyMainSub = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = legacySingleMainTicket.Id,
            ExamQuestionId = q1.Id,
            IsFollowUp = false,
            AudioObjectKey = null,
            AudioR2Url = null,
            GradingStatus = "ai_graded"
        };
        var legacyFollowUpSub = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = legacySingleMainTicket.Id,
            ExamQuestionId = q1.Id,
            IsFollowUp = true,
            ParentSubmissionId = legacyMainSub.Id,
            FollowUpIndex = 1,
            AudioR2Url = "javascript:alert(1)",
            GradingStatus = "ai_graded"
        };
        var appeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = session.Id,
            SubmissionId = sub1.Id,
            Reason = "Đề nghị phúc khảo lại câu 1 giúp em ạ.",
            Status = AppealStatus.AssignedToLecturer,
            AssignedTo = appealLecturer.Id,
            OriginalScore = 8.0m
        };

        context.Users.AddRange(officer, deptHead, otherDeptHead, examiner, appealLecturer, unrelatedLecturer, proctor, student, legacyStudent);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.Rubrics.Add(rubric);
        context.RubricCriteria.AddRange(criteria);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        context.ExamQuestions.AddRange(q1, q2);
        context.StudentExamTickets.AddRange(ticket, legacySingleMainTicket);
        context.ExamQuestionSubmissions.AddRange(sub1, sub2, legacyMainSub, legacyFollowUpSub);
        context.AiEvaluations.AddRange(eval1, eval2);
        context.LecturerAudits.Add(audit);
        context.AppealRequests.Add(appeal);
        await context.SaveChangesAsync();

        // 1. Khi R2 chưa cấu hình (ném InvalidOperationException): Evidence Panel vẫn 200 OK và đủ 2 câu chính + CriterionDetails + TimestampsWhisper (không crash trùng CriterionId)
        var unconfiguredR2 = new Mock<IStorageService>();
        unconfiguredR2
            .Setup(s => s.CreatePresignedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("R2 is not configured"));

        var shiftEvidence = await new GetAuditEvidenceQueryHandler(context, unconfiguredR2.Object)
            .Handle(new GetAuditEvidenceQuery(shift.Id, examiner.Id), CancellationToken.None);
        shiftEvidence.IsSuccess.Should().BeTrue(shiftEvidence.Error);
        var item = shiftEvidence.Value.Single(x => x.TicketId == ticket.Id);
        item.AudioR2Url.Should().BeNull();
        item.TimestampsWhisper.Should().Be(sub2.TimestampsWhisper);
        item.MainSubmissions.Should().HaveCount(2);
        item.MainSubmissions![0].SubmissionId.Should().Be(sub1.Id);
        item.MainSubmissions[0].AudioHashSha256.Should().Be("hash-q1");
        item.MainSubmissions[0].CriterionDetails.Should().HaveCount(2);
        item.MainSubmissions[1].SubmissionId.Should().Be(sub2.Id);
        item.MainSubmissions[1].CriterionDetails!.Single(c => c.CriterionId == criteria[1].Id).AuditedScore.Should().Be(4.5m);

        // Chặn tuyệt đối scheme javascript: trên follow-up trong Evidence Panel
        var legacyEvidence = shiftEvidence.Value.Single(x => x.TicketId == legacySingleMainTicket.Id);
        legacyEvidence.FollowUpItems!.Single().AudioR2Url.Should().BeNull();

        var appealEvidence = await new GetAppealEvidenceQueryHandler(context, unconfiguredR2.Object)
            .Handle(new GetAppealEvidenceQuery(appeal.Id, appealLecturer.Id), CancellationToken.None);
        appealEvidence.IsSuccess.Should().BeTrue(appealEvidence.Error);
        appealEvidence.Value.MainSubmissions.Should().HaveCount(2);

        // 2. Refresh Audio Playback URL: ủy quyền chặt và trả đúng URL cho từng submissionId
        var workingR2 = new Mock<IStorageService>();
        workingR2
            .Setup(s => s.CreatePresignedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, TimeSpan ttl, CancellationToken _) =>
                new StoragePresignedDownloadResult($"https://r2.signed.example/{key}?ttl={(int)ttl.TotalSeconds}", key, DateTime.UtcNow.Add(ttl)));

        var playbackHandler = new GetOfficialExamAudioPlaybackUrlQueryHandler(context, workingR2.Object);

        var q1Playback = await playbackHandler.Handle(new GetOfficialExamAudioPlaybackUrlQuery(appealLecturer.Id, ticket.Id, sub1.Id), CancellationToken.None);
        q1Playback.IsSuccess.Should().BeTrue(q1Playback.Error);
        q1Playback.Value.DownloadUrl.Should().Contain("05_SE180301_q1.webm");
        q1Playback.Value.AudioHashSha256.Should().Be("hash-q1");

        var examinerPlayback = await playbackHandler.Handle(new GetOfficialExamAudioPlaybackUrlQuery(examiner.Id, ticket.Id, sub2.Id), CancellationToken.None);
        examinerPlayback.IsSuccess.Should().BeTrue(examinerPlayback.Error);
        examinerPlayback.Value.DownloadUrl.Should().Contain("05_SE180301_q2.webm");

        // Vé có 1 câu chính + 1 follow-up dùng audio cấp ticket legacy -> vẫn ký lại AudioObjectKey thành công, còn follow-up scheme javascript: bị chặn UNPROCESSABLE
        var legacyPlayback = await playbackHandler.Handle(new GetOfficialExamAudioPlaybackUrlQuery(examiner.Id, legacySingleMainTicket.Id, legacyMainSub.Id), CancellationToken.None);
        legacyPlayback.IsSuccess.Should().BeTrue(legacyPlayback.Error);
        legacyPlayback.Value.DownloadUrl.Should().Contain("06_SE180302_legacy.webm");

        var unsafeSchemePlayback = await playbackHandler.Handle(new GetOfficialExamAudioPlaybackUrlQuery(examiner.Id, legacySingleMainTicket.Id, legacyFollowUpSub.Id), CancellationToken.None);
        unsafeSchemePlayback.IsSuccess.Should().BeFalse();
        unsafeSchemePlayback.Error.Should().StartWith("UNPROCESSABLE:");

        // Cross-ticket submissionId -> NOT_FOUND
        var crossTicketSub = await playbackHandler.Handle(new GetOfficialExamAudioPlaybackUrlQuery(examiner.Id, ticket.Id, legacyMainSub.Id), CancellationToken.None);
        crossTicketSub.IsSuccess.Should().BeFalse();
        crossTicketSub.Error.Should().StartWith("NOT_FOUND:");

        // DepartmentHead khác môn -> FORBIDDEN
        var deniedOtherDeptHead = await playbackHandler.Handle(new GetOfficialExamAudioPlaybackUrlQuery(otherDeptHead.Id, ticket.Id, sub1.Id), CancellationToken.None);
        deniedOtherDeptHead.IsSuccess.Should().BeFalse();
        deniedOtherDeptHead.Error.Should().StartWith("FORBIDDEN:");

        var deniedStudent = await playbackHandler.Handle(new GetOfficialExamAudioPlaybackUrlQuery(student.Id, ticket.Id, sub1.Id), CancellationToken.None);
        deniedStudent.IsSuccess.Should().BeFalse();
        deniedStudent.Error.Should().StartWith("FORBIDDEN:");

        var deniedStranger = await playbackHandler.Handle(new GetOfficialExamAudioPlaybackUrlQuery(unrelatedLecturer.Id, ticket.Id, sub1.Id), CancellationToken.None);
        deniedStranger.IsSuccess.Should().BeFalse();
        deniedStranger.Error.Should().StartWith("FORBIDDEN:");

        var deniedProctor = await playbackHandler.Handle(new GetOfficialExamAudioPlaybackUrlQuery(proctor.Id, ticket.Id, sub1.Id), CancellationToken.None);
        deniedProctor.IsSuccess.Should().BeFalse();
        deniedProctor.Error.Should().StartWith("FORBIDDEN:");
    }

    [Fact]
    public async Task Opt1_04_AssignExamTicket_AcceptsStudentCode_AndRejectsMissingIdentity()
    {
        await using var context = CreateDbContext();
        var officer = CreateUser(UserRole.ExaminationOfficer, "eo");
        var deptHead = CreateUser(UserRole.DepartmentHead, "tbm");
        var student = CreateUser(UserRole.Student, "sv", "SE180401");
        var (semester, course, _, _) = CreateAcademicGraph(deptHead.Id);

        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CreatedBy = deptHead.Id,
            Name = "Struct Assign",
            TotalQuestions = 1,
            IsActive = true
        };
        var handedOverSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = structure.Id,
            SetCode = "SET-ASSIGN-01",
            Status = ExamSetStatus.HandedOver,
            IsActive = true
        };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SemesterId = semester.Id,
            ExamStructureId = structure.Id,
            Title = "FE Assign",
            ExamDate = new DateOnly(2026, 11, 25),
            CreatedBy = officer.Id
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Kip 1",
            RoomLab = "Lab 304",
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(2),
            Status = "scheduled",
            MaxSeats = 24
        };
        var inactiveStudent = CreateUser(UserRole.Student, "sv-off", "SE180402");
        inactiveStudent.IsActive = false;
        var lecturerWithCode = CreateUser(UserRole.Lecturer, "lec-code", "SE180403");
        context.Users.AddRange(officer, deptHead, student, inactiveStudent, lecturerWithCode);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.ExamStructures.Add(structure);
        context.ExamSets.Add(handedOverSet);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        await context.SaveChangesAsync();

        var validator = new AssignExamTicketCommandValidator();
        var handler = new AssignExamTicketCommandHandler(context, validator);

        // Thiếu cả StudentId lẫn StudentCode -> thất bại validation
        var invalidRes = await handler.Handle(
            new AssignExamTicketCommand(officer.Id, shift.Id, Guid.Empty, 1, "192.168.1.10", null),
            CancellationToken.None);
        invalidRes.IsSuccess.Should().BeFalse();

        // StudentCode thuộc user bị khóa (IsActive = false) hoặc role khác student -> từ chối
        var inactiveRes = await handler.Handle(
            new AssignExamTicketCommand(officer.Id, shift.Id, Guid.Empty, 2, "192.168.1.11", "SE180402"),
            CancellationToken.None);
        inactiveRes.IsSuccess.Should().BeFalse();

        var nonStudentRes = await handler.Handle(
            new AssignExamTicketCommand(officer.Id, shift.Id, Guid.Empty, 3, "192.168.1.12", "SE180403"),
            CancellationToken.None);
        nonStudentRes.IsSuccess.Should().BeFalse();

        // Truyền StudentCode chữ thường với StudentId == Guid.Empty -> cấp vé thành công
        var validRes = await handler.Handle(
            new AssignExamTicketCommand(officer.Id, shift.Id, Guid.Empty, 1, "192.168.1.10", "se180401"),
            CancellationToken.None);
        validRes.IsSuccess.Should().BeTrue(validRes.Error);
        validRes.Value.StudentId.Should().Be(student.Id);
        validRes.Value.SeatNumber.Should().Be(1);
    }

    [Theory]
    [InlineData("xlsx")]
    [InlineData("csv")]
    public async Task Opt1_05_ExamRosterFileParser_GenerateTemplate_RoundTripsCleanly(string format)
    {
        var parser = new ExamRosterFileParser();
        var template = parser.GenerateTemplate(format);

        template.Contents.Should().NotBeEmpty();
        template.FileName.Should().EndWith($".{format}");

        await using var stream = new MemoryStream(template.Contents);
        var parsed = await parser.ParseAsync(stream, template.FileName, CancellationToken.None);
        parsed.Errors.Should().BeEmpty();
        parsed.Rows.Should().HaveCountGreaterThanOrEqualTo(1);
        parsed.Rows[0].StudentCode.Should().NotBeNullOrWhiteSpace();
        parsed.Rows[0].SeatNumber.Should().BeGreaterThan(0);
        parsed.Rows[0].IpAddress.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Opt1_06_Mf03_ExamSetYellowOverlapPercent_AndCurationBandAliases()
    {
        await using var context = CreateDbContext();
        var deptHead = CreateUser(UserRole.DepartmentHead, "tbm");
        var lecA = CreateUser(UserRole.Lecturer, "lecA");
        var lecB = CreateUser(UserRole.Lecturer, "lecB");
        var (semester, course, rubric, criteria) = CreateAcademicGraph(deptHead.Id);

        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CreatedBy = deptHead.Id,
            Name = "Struct M3",
            TotalQuestions = 2,
            RequiredSetCount = 4,
            IsActive = true
        };
        var bankQ1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Clean Architecture Dependency Rule", Content = "Explain dependency rule in clean architecture for enterprise dotnet eight applications", SampleAnswer = new string('A', 60), ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };
        var bankQ2 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "PostgreSQL Indexing", Content = "Explain B-Tree versus GIN trigram indexing performance in PostgreSQL 16", SampleAnswer = new string('B', 60), ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };
        var bankQ3 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "SignalR Hubs", Content = "Explain typed SignalR hub reconnection strategies", SampleAnswer = new string('C', 60), ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };

        // Câu tạo tay trùng >= 80% (RED) với bankQ1
        var redPendingQ = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Clean Architecture Dependency Rule",
            Content = "Explain dependency rule in clean architecture for enterprise dotnet eight applications",
            SampleAnswer = new string('R', 60),
            ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.PendingCuration,
            SubmittedBy = lecA.Id,
            IsActive = true
        };

        var setA = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = structure.Id,
            SetCode = "SET-A",
            Status = ExamSetStatus.SubmittedForReview,
            CreatedBy = lecA.Id,
            IsActive = true
        };
        var setB = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = structure.Id,
            SetCode = "SET-B",
            Status = ExamSetStatus.SubmittedForReview,
            CreatedBy = lecB.Id,
            IsActive = true
        };

        context.Users.AddRange(deptHead, lecA, lecB);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.Rubrics.Add(rubric);
        context.RubricCriteria.AddRange(criteria);
        context.ExamStructures.Add(structure);
        context.ExamQuestions.AddRange(bankQ1, bankQ2, bankQ3, redPendingQ);
        context.ExamSets.AddRange(setA, setB);
        context.ExamSetQuestions.AddRange(
            new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = setA.Id, ExamQuestionId = bankQ1.Id, OrderIndex = 1 },
            new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = setA.Id, ExamQuestionId = bankQ2.Id, OrderIndex = 2 },
            new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = setB.Id, ExamQuestionId = bankQ1.Id, OrderIndex = 1 },
            new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = setB.Id, ExamQuestionId = bankQ3.Id, OrderIndex = 2 });
        await context.SaveChangesAsync();

        var setsRes = await new GetExamSetsQueryHandler(context)
            .Handle(new GetExamSetsQuery(course.Id, ActorUserId: deptHead.Id, ActorRole: UserRole.DepartmentHead), CancellationToken.None);
        setsRes.IsSuccess.Should().BeTrue(setsRes.Error);
        var dtoB = setsRes.Value.Single(s => s.Id == setB.Id);
        dtoB.YellowOverlapRatio.Should().Be(0.5000m);
        dtoB.YellowOverlapPercent.Should().Be(50.0m);

        var redByAlias = await new GetBankCurationCandidatesQueryHandler(context)
            .Handle(new GetBankCurationCandidatesQuery(course.Id, deptHead.Id, UserRole.DepartmentHead, "RED"), CancellationToken.None);
        var redByCanonical = await new GetBankCurationCandidatesQueryHandler(context)
            .Handle(new GetBankCurationCandidatesQuery(course.Id, deptHead.Id, UserRole.DepartmentHead, "RED_80_PLUS"), CancellationToken.None);

        redByAlias.IsSuccess.Should().BeTrue(redByAlias.Error);
        redByAlias.Value.Items.Should().ContainSingle(i => i.QuestionId == redPendingQ.Id);
        redByAlias.Value.Items.Select(i => i.QuestionId).Should().BeEquivalentTo(redByCanonical.Value.Items.Select(i => i.QuestionId));
    }

    [Fact]
    public async Task Opt1_07_Mf03_ReturnForRevision_UpdateRetainingInlineQuestion_SubmitReviewSucceeds()
    {
        await using var context = CreateDbContext();
        var deptHead = CreateUser(UserRole.DepartmentHead, "tbm");
        var lecturer = CreateUser(UserRole.Lecturer, "lec");
        var (semester, course, rubric, criteria) = CreateAcademicGraph(deptHead.Id);

        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CreatedBy = deptHead.Id,
            Name = "Struct Inline",
            TotalQuestions = 1,
            IsActive = true
        };
        var inlineQ = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Inline Q1",
            Content = "Explain Outbox Pattern",
            SampleAnswer = new string('O', 65),
            Source = QuestionSource.Manual,
            ApprovalStatus = ApprovalStatus.SubmittedForReview,
            BankCurationStatus = BankCurationStatus.PendingCuration,
            SubmittedBy = lecturer.Id,
            IsActive = true
        };
        var set = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = structure.Id,
            SetCode = "SET-INLINE-01",
            Status = ExamSetStatus.SubmittedForReview,
            CreatedBy = lecturer.Id,
            IsActive = true
        };

        context.Users.AddRange(deptHead, lecturer);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.CourseLecturers.Add(new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lecturer.Id, IsActive = true });
        context.Rubrics.Add(rubric);
        context.RubricCriteria.AddRange(criteria);
        context.ExamStructures.Add(structure);
        context.ExamQuestions.Add(inlineQ);
        context.ExamSets.Add(set);
        context.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = set.Id, ExamQuestionId = inlineQ.Id, OrderIndex = 1 });
        await context.SaveChangesAsync();

        // 1. TBM trả bộ đề về chỉnh sửa -> inlineQ chuyển thành NEEDS_REVISION
        var returnRes = await new ReturnExamSetForRevisionCommandHandler(context)
            .Handle(new ReturnExamSetForRevisionCommand(set.Id, "Cần bổ sung rõ tiêu chí phân tích lỗi.", deptHead.Id, UserRole.DepartmentHead), CancellationToken.None);
        returnRes.IsSuccess.Should().BeTrue(returnRes.Error);
        (await context.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == inlineQ.Id)).ApprovalStatus.Should().Be(ApprovalStatus.NeedsRevision);

        // 2. GV sửa bộ đề (giữ lại inlineQ) -> tự reset inlineQ về DRAFT và bộ đề về DRAFT
        var updateRes = await new UpdateExamSetCommandHandler(context)
            .Handle(new UpdateExamSetCommand(set.Id, "SET-INLINE-01-V2", [inlineQ.Id], lecturer.Id, UserRole.Lecturer), CancellationToken.None);
        updateRes.IsSuccess.Should().BeTrue(updateRes.Error);
        (await context.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == inlineQ.Id)).ApprovalStatus.Should().Be(ApprovalStatus.Draft);

        // 3. GV gửi duyệt lại -> PHẢI thành công (không còn 409 deadlock)
        var resubmitRes = await new SubmitExamSetForReviewCommandHandler(context)
            .Handle(new SubmitExamSetForReviewCommand(set.Id, lecturer.Id, UserRole.Lecturer), CancellationToken.None);
        resubmitRes.IsSuccess.Should().BeTrue(resubmitRes.Error);
    }

    [Fact]
    public async Task Opt1_08_Mf03_GetQuestionsCloFilterAndIsActive_AndMutateQuestionImageAndRubricId()
    {
        await using var context = CreateDbContext();
        var deptHead = CreateUser(UserRole.DepartmentHead, "tbm");
        var lecturer = CreateUser(UserRole.Lecturer, "lec");
        var (semester, course, rubric, criteria) = CreateAcademicGraph(deptHead.Id);

        var clo1 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO1", Description = "Design", BloomLevel = BloomLevel.Understand, OrderIndex = 1, IsActive = true };
        var clo2 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO2", Description = "Evaluate", BloomLevel = BloomLevel.Evaluate, OrderIndex = 2, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = deptHead.Id, Name = "Struct Q", TotalQuestions = 2, IsActive = true };

        var qClo1Inactive = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CloId = clo1.Id,
            RubricId = rubric.Id,
            Title = "Q CLO1 Draft",
            Content = "Draft Content",
            SampleAnswer = new string('D', 60),
            Source = QuestionSource.Manual,
            ApprovalStatus = ApprovalStatus.NeedsRevision,
            ReviewNotes = "Sửa hình minh họa rõ hơn",
            BankCurationStatus = BankCurationStatus.BankReady,
            SubmittedBy = lecturer.Id,
            IsActive = false
        };
        var qClo2Active = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CloId = clo2.Id,
            RubricId = rubric.Id,
            Title = "Q CLO2 Approved",
            Content = "Approved Content",
            SampleAnswer = new string('E', 60),
            Source = QuestionSource.Manual,
            ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.BankReady,
            SubmittedBy = lecturer.Id,
            IsActive = true
        };

        context.Users.AddRange(deptHead, lecturer);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.CourseLecturers.Add(new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lecturer.Id, IsActive = true });
        context.CourseClos.AddRange(clo1, clo2);
        context.Rubrics.Add(rubric);
        context.RubricCriteria.AddRange(criteria);
        context.ExamStructures.Add(structure);
        context.ExamQuestions.AddRange(qClo1Inactive, qClo2Active);
        await context.SaveChangesAsync();

        // 1. GetQuestions lọc theo CloId & chiếu đúng IsActive, CloCode, BankCurationStatus, ReviewNotes
        var filtered = await new GetQuestionsQueryHandler(context)
            .Handle(new GetQuestionsQuery(CourseId: course.Id, UsageScope: "exam", ActorId: deptHead.Id, ActorRole: UserRole.DepartmentHead, CloId: clo1.Id), CancellationToken.None);
        filtered.IsSuccess.Should().BeTrue(filtered.Error);
        var item = filtered.Value.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(qClo1Inactive.Id);
        item.IsActive.Should().BeFalse();
        item.CloCode.Should().Be("CLO1");
        item.BankCurationStatus.Should().Be(BankCurationStatus.BankReady);
        item.ReviewNotes.Should().Be("Sửa hình minh họa rõ hơn");

        // 2. MutateQuestion chặn path traversal, %2e%2e, quote XSS, javascript/data/http và SSRF loopback/private/metadata IP
        var mutateHandler = new MutateQuestionCommandHandler(context);
        var unsafeUrls = new[]
        {
            "/uploads/questions/../../etc/passwd",
            "/uploads/questions/%2e%2e/%2e%2e/appsettings.json",
            "/uploads/questions/a\" onerror=\"alert(1).png",
            "javascript:alert(1)",
            "data:image/svg+xml;base64,PHN2Zz4=",
            "http://example.com/a.png",
            "https://127.0.0.1/internal.png",
            "https://localhost/internal.png",
            "https://169.254.169.254/latest/meta-data/",
            "https://[::ffff:127.0.0.1]/internal.png",
            "https://user:pass@cdn.example.com/a.png"
        };
        foreach (var badUrl in unsafeUrls)
        {
            var blocked = await mutateHandler.Handle(new MutateQuestionCommand(
                qClo1Inactive.Id,
                lecturer.Id,
                UserRole.Lecturer,
                QuestionMutationKind.Update,
                new UpdateQuestionDraftDto(
                    "Updated",
                    "Updated Content",
                    new string('U', 60),
                    ["Point 1"],
                    QuestionDifficulty.Medium,
                    BloomLevel.Apply,
                    Rubric: null,
                    CloId: clo1.Id,
                    ImageUrl: badUrl,
                    RubricId: rubric.Id)),
                CancellationToken.None);
            blocked.IsSuccess.Should().BeFalse($"URL độc hại '{badUrl}' phải bị chặn");
        }

        // Chặn BOLA RubricId & StructureId từ môn học khác + chặn gắn ảnh/ImageDescription lên câu ai_generated
        var otherCourse = new Course { Id = Guid.NewGuid(), Code = "PRN232", Name = "Other", Credits = 3, SemesterId = semester.Id, IsActive = true };
        var otherRubric = new Rubric { Id = Guid.NewGuid(), CourseId = otherCourse.Id, Name = "Other Rubric", TotalMaxScore = 10m, IsActive = true };
        var otherStruct = new ExamStructure { Id = Guid.NewGuid(), CourseId = otherCourse.Id, CreatedBy = deptHead.Id, Name = "Other Struct", TotalQuestions = 2, IsActive = true };
        var aiQuestion = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CloId = clo1.Id,
            RubricId = rubric.Id,
            Title = "AI Draft",
            Content = "AI Content",
            SampleAnswer = new string('A', 60),
            Source = QuestionSource.AiGenerated,
            ApprovalStatus = ApprovalStatus.Draft,
            SubmittedBy = lecturer.Id,
            IsActive = false
        };
        context.Courses.Add(otherCourse);
        context.Rubrics.Add(otherRubric);
        context.ExamStructures.Add(otherStruct);
        context.ExamQuestions.Add(aiQuestion);
        await context.SaveChangesAsync();

        var crossCourseRubric = await mutateHandler.Handle(new MutateQuestionCommand(
            qClo1Inactive.Id, lecturer.Id, UserRole.Lecturer, QuestionMutationKind.Update,
            new UpdateQuestionDraftDto("U", "C", new string('U', 60), ["P"], QuestionDifficulty.Medium, BloomLevel.Apply, null, clo1.Id, RubricId: otherRubric.Id)),
            CancellationToken.None);
        crossCourseRubric.IsSuccess.Should().BeFalse();

        var crossCourseStruct = await mutateHandler.Handle(new MutateQuestionCommand(
            qClo1Inactive.Id, lecturer.Id, UserRole.Lecturer, QuestionMutationKind.Update,
            new UpdateQuestionDraftDto("U", "C", new string('U', 60), ["P"], QuestionDifficulty.Medium, BloomLevel.Apply, null, clo1.Id, StructureId: otherStruct.Id, RubricId: rubric.Id)),
            CancellationToken.None);
        crossCourseStruct.IsSuccess.Should().BeFalse();

        var aiImageDescBlocked = await mutateHandler.Handle(new MutateQuestionCommand(
            aiQuestion.Id, lecturer.Id, UserRole.Lecturer, QuestionMutationKind.Update,
            new UpdateQuestionDraftDto("U", "C", new string('U', 60), ["P"], QuestionDifficulty.Medium, BloomLevel.Apply, null, clo1.Id, ImageDescription: "Should be blocked on AI question", RubricId: rubric.Id)),
            CancellationToken.None);
        aiImageDescBlocked.IsSuccess.Should().BeFalse();

        // 3. MutateQuestion cập nhật ảnh hợp lệ + StructureId + tái sử dụng RubricId (không tạo Rubric trùng)
        var rubricCountBefore = await context.Rubrics.CountAsync();
        var updateOk = await mutateHandler.Handle(new MutateQuestionCommand(
            qClo1Inactive.Id,
            lecturer.Id,
            UserRole.Lecturer,
            QuestionMutationKind.Update,
            new UpdateQuestionDraftDto(
                "Updated With Diagram",
                "Updated Content With Diagram",
                new string('U', 60),
                ["Point 1"],
                QuestionDifficulty.Medium,
                BloomLevel.Apply,
                Rubric: null,
                CloId: clo1.Id,
                ImageUrl: "/uploads/questions/202611/diagram01.png",
                ImageDescription: "Sơ đồ kiến trúc 4 tầng",
                StructureId: structure.Id,
                RubricId: rubric.Id)),
            CancellationToken.None);
        updateOk.IsSuccess.Should().BeTrue(updateOk.Error);
        (await context.Rubrics.CountAsync()).Should().Be(rubricCountBefore);

        var updated = await context.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == qClo1Inactive.Id);
        updated.ImageUrl.Should().Be("/uploads/questions/202611/diagram01.png");
        updated.ImageDescription.Should().Be("Sơ đồ kiến trúc 4 tầng");
        updated.StructureId.Should().Be(structure.Id);
        updated.RubricId.Should().Be(rubric.Id);

        // 4. ClearImage = true xóa sạch ảnh cũ
        var clearRes = await mutateHandler.Handle(new MutateQuestionCommand(
            qClo1Inactive.Id,
            lecturer.Id,
            UserRole.Lecturer,
            QuestionMutationKind.Update,
            new UpdateQuestionDraftDto(
                "Updated No Diagram",
                "Updated Content",
                new string('U', 60),
                ["Point 1"],
                QuestionDifficulty.Medium,
                BloomLevel.Apply,
                Rubric: null,
                CloId: clo1.Id,
                RubricId: rubric.Id,
                ClearImage: true)),
            CancellationToken.None);
        clearRes.IsSuccess.Should().BeTrue(clearRes.Error);
        var cleared = await context.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == qClo1Inactive.Id);
        cleared.ImageUrl.Should().BeNull();
        cleared.ImageDescription.Should().BeNull();
    }

    [Fact]
    public async Task Opt1_09_PracticeRuntimeConfig_CorsExposedHeaders_AndProductionCloudflareModel()
    {
        // 1. Practice Runtime Config reads live SystemConfigs for student
        await using var context = CreateDbContext();
        context.SystemConfigs.AddRange(
            new SystemConfig { Key = "MinMixedPracticeQuestions", Value = "4" },
            new SystemConfig { Key = "MaxMixedPracticeQuestions", Value = "8" },
            new SystemConfig { Key = "MaxPracticeQuestionsPerSession", Value = "9" },
            new SystemConfig { Key = "MaxPracticeFollowUpQuestions", Value = "3" },
            new SystemConfig { Key = "SessionInactivityTimeoutMinutes", Value = "7" },
            new SystemConfig { Key = "TranscriptBufferSeconds", Value = "90" });
        await context.SaveChangesAsync();

        var configRes = await new GetPracticeRuntimeConfigQueryHandler(context)
            .Handle(new GetPracticeRuntimeConfigQuery(), CancellationToken.None);
        configRes.IsSuccess.Should().BeTrue();
        configRes.Value.MinMixedPracticeQuestions.Should().Be(4);
        configRes.Value.MaxMixedPracticeQuestions.Should().Be(8);
        configRes.Value.MaxPracticeQuestionsPerSession.Should().Be(9);
        configRes.Value.MaxPracticeFollowUpQuestions.Should().Be(3);
        configRes.Value.SessionInactivityTimeoutMinutes.Should().Be(7);
        configRes.Value.TranscriptBufferSeconds.Should().Be(90);

        // 2. CORS exposes required download, signature, retry and TTS headers
        var services = new ServiceCollection();
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://oralexam.io.vn"
            })
            .Build();
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);

        services.AddOralExamCors(cfg, env.Object);
        using var provider = services.BuildServiceProvider();
        var corsOptions = provider.GetRequiredService<IOptions<CorsOptions>>().Value;
        var policy = corsOptions.GetPolicy(OralExamCorsExtensions.PolicyName);
        policy.Should().NotBeNull();
        policy!.ExposedHeaders.Should().Contain(new[]
        {
            "Content-Disposition",
            "X-Certificate-Hash",
            "X-Signature-Thumbprint",
            "Retry-After",
            "X-Correlation-ID",
            "X-TTS-Provider",
            "X-TTS-Voice",
            "X-TTS-Rate",
            "X-TTS-Fallback",
            "X-TTS-Cache"
        });

        // 3. appsettings.Production.json uses valid Cloudflare Workers AI Llama model ID (-fp8-fast)
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var prodSettingsPath = Path.Combine(repoRoot, "src", "API", "appsettings.Production.json");
        File.Exists(prodSettingsPath).Should().BeTrue($"expected {prodSettingsPath}");
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(prodSettingsPath));
        doc.RootElement.GetProperty("Cloudflare").GetProperty("LlamaModel").GetString()
            .Should().Be("@cf/meta/llama-3.3-70b-instruct-fp8-fast");
    }
}
