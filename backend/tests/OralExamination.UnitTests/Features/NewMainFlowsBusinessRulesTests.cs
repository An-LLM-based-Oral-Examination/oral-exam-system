using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;
using OralExamination.Application.Features.MockExams.Commands.StartMockExam;
using OralExamination.Application.Features.Practice.Commands.StartPracticeSession;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features;

public class NewMainFlowsBusinessRulesTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    #region R1: Chuẩn Hóa Follow-up Trong Luyện Tập Tự Do (MF-01)

    [Fact(DisplayName = "R1-01: Admin cấu hình mặc định MaxFollowUpQuestions = 2 cho hệ thống luyện tập")]
    public void Course_MaxFollowUpQuestions_Defaults_To_2()
    {
        var course = new Course();
        course.MaxFollowUpQuestions.Should().Be(2, "Admin cấu hình hệ thống luyện tập mặc định 2 câu follow-up.");
    }

    [Theory(DisplayName = "R1-02: Admin có thể cấu hình linh hoạt số câu follow-up từ 1 đến 5")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Validator_Should_Accept_FollowUp_Between_1_And_5(int count)
    {
        var validator = new UpdateCourseConfigurationCommandValidator();
        var command = new UpdateCourseConfigurationCommand(Guid.NewGuid(), 60, count, true);
        var result = validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "R1-03: Validator chặn cấu hình follow-up ngoài khoảng 1-5")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(10)]
    public void Validator_Should_Reject_FollowUp_Outside_1_And_5(int count)
    {
        var validator = new UpdateCourseConfigurationCommandValidator();
        var command = new UpdateCourseConfigurationCommand(Guid.NewGuid(), 60, count, true);
        var result = validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateCourseConfigurationCommand.MaxFollowUpQuestions));
    }

    [Fact(DisplayName = "R1-04: Sinh viên chọn Per-Question tự động kích hoạt chế độ có Follow-up")]
    public async Task StartPracticeSession_PerQuestion_Creates_PerQuestion_Mode()
    {
        using var context = CreateDbContext();
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Architecture", SemesterId = Guid.NewGuid() };
        var student = new User { Id = Guid.NewGuid(), Email = "student@fpt.edu.vn", FullName = "Student A", Role = "student" };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Basic Rubric" };
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Q1",
            Content = "Content",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        context.Courses.Add(course);
        context.Users.Add(student);
        context.Rubrics.Add(rubric);
        context.PracticeQuestions.Add(question);
        await context.SaveChangesAsync();

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(student.Id, course.Id, Difficulty: "medium", QuestionCount: 1, IsFullSession: false, Topic: null);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var session = await context.PracticeSessions.FirstOrDefaultAsync(s => s.Id == result.Value!.SessionId);
        session.Should().NotBeNull();
        session!.PracticeMode.Should().Be("per_question", "Luyện từng câu luôn hỗ trợ Follow-up");
    }

    [Fact(DisplayName = "R1-05: Sinh viên chọn Full-Session chuyển sang chế độ không Follow-up")]
    public async Task StartPracticeSession_FullSession_Creates_FullSession_Mode()
    {
        using var context = CreateDbContext();
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = ".NET", SemesterId = Guid.NewGuid() };
        var student = new User { Id = Guid.NewGuid(), Email = "student_full@fpt.edu.vn", FullName = "Student B", Role = "student" };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Basic Rubric" };
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Q1",
            Content = "Content",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        context.Courses.Add(course);
        context.Users.Add(student);
        context.Rubrics.Add(rubric);
        context.PracticeQuestions.Add(question);
        await context.SaveChangesAsync();

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(student.Id, course.Id, Difficulty: "medium", QuestionCount: 1, IsFullSession: true, Topic: null);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var session = await context.PracticeSessions.FirstOrDefaultAsync(s => s.Id == result.Value!.SessionId);
        session.Should().NotBeNull();
        session!.PracticeMode.Should().Be("full_session", "Luyện trọn gói không có Follow-up");
    }

    #endregion

    #region R2: Tùy Chọn Follow-up Trong Thi Thử Tính Giờ (MF-02)

    [Fact(DisplayName = "R2-01: MockExamSession lưu trữ cờ HasFollowUp do sinh viên tự chọn trước khi thi")]
    public async Task MockExamSession_Can_Store_Student_FollowUp_Preference()
    {
        using var context = CreateDbContext();
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = ".NET", SemesterId = Guid.NewGuid() };
        var student = new User { Id = Guid.NewGuid(), Email = "mock_stu@fpt.edu.vn", FullName = "Student C", Role = "student" };
        var examStructure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = student.Id, Name = "Structure 1", TotalQuestions = 3 };
        var examSet = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = examStructure.Id, SetCode = "SET-01" };
        context.Courses.Add(course);
        context.Users.Add(student);
        context.ExamStructures.Add(examStructure);
        context.ExamSets.Add(examSet);
        await context.SaveChangesAsync();

        var mockSession = new MockExamSession
        {
            StudentId = student.Id,
            CourseId = course.Id,
            ExamSetId = examSet.Id,
            HasFollowUp = true,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        context.MockExamSessions.Add(mockSession);
        await context.SaveChangesAsync();

        var saved = await context.MockExamSessions.FirstOrDefaultAsync(s => s.Id == mockSession.Id);
        saved.Should().NotBeNull();
        saved!.HasFollowUp.Should().BeTrue("Sinh viên chủ động chọn chế độ Có Follow-up");
    }

    #endregion

    #region R4: Quản Trị Kỳ Thi & Cấu Hình Môn Thi Của Trưởng Bộ Môn (MF-04)

    [Fact(DisplayName = "R4-01: OfficialExamSession hỗ trợ Trưởng Bộ Môn cấu hình Follow-up, InputMode và BufferSeconds")]
    public async Task OfficialExamSession_Supports_DepartmentHead_Configurations()
    {
        using var context = CreateDbContext();
        var deptHead = new User { Id = Guid.NewGuid(), Email = "dept_head@fpt.edu.vn", FullName = "Dept Head", Role = "department_head" };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Architecture", SemesterId = Guid.NewGuid() };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = deptHead.Id, Name = "Structure Main", TotalQuestions = 3 };
        context.Users.Add(deptHead);
        context.Courses.Add(course);
        context.ExamStructures.Add(structure);
        await context.SaveChangesAsync();

        var officialSession = new OfficialExamSession
        {
            CourseId = course.Id,
            ExamStructureId = structure.Id,
            Title = "Kỳ thi Kết thúc môn FA26 - PRN231",
            ExamDate = DateOnly.FromDateTime(DateTime.Today),
            HasFollowUp = true,
            MaxFollowUpQuestions = 2,
            ExamInputMode = ExamInputMode.VoiceOnly,
            TranscriptBufferSeconds = 90,
            CreatedBy = deptHead.Id
        };
        context.OfficialExamSessions.Add(officialSession);
        await context.SaveChangesAsync();

        var savedSession = await context.OfficialExamSessions.Include(s => s.CreatedByUser).FirstOrDefaultAsync(s => s.Id == officialSession.Id);
        savedSession.Should().NotBeNull();
        savedSession!.HasFollowUp.Should().BeTrue();
        savedSession.MaxFollowUpQuestions.Should().Be(2);
        savedSession.ExamInputMode.Should().Be(ExamInputMode.VoiceOnly);
        savedSession.TranscriptBufferSeconds.Should().Be(90);
        savedSession.CreatedByUser.Should().NotBeNull();
        savedSession.CreatedByUser!.Role.Should().Be("department_head");
    }

    #endregion

    #region R8: Chặn Bốc Đề Câu Hỏi Luyện Tập Chưa Phê Duyệt (MF-01 & MF-02)

    [Theory(DisplayName = "R8-01: PracticeQuestion ở trạng thái DRAFT hoặc REJECTED (dù IsActive = true) KHÔNG bị bốc vào phiên Luyện tập")]
    [InlineData(ApprovalStatus.Draft)]
    [InlineData(ApprovalStatus.Rejected)]
    public async Task StartPracticeSession_Should_Not_Pick_Unapproved_PracticeQuestion_Even_When_IsActive_Is_True(string unapprovedStatus)
    {
        using var context = CreateDbContext();
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = ".NET", SemesterId = Guid.NewGuid(), IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "stu_r8_practice@fpt.edu.vn", FullName = "Student R8", Role = "student", IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric R8", TotalMaxScore = 10.0m, IsActive = true };

        var unapprovedQuestion = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = $"Câu hỏi {unapprovedStatus}",
            Content = "Nội dung câu hỏi chưa được phê duyệt",
            Difficulty = "medium",
            ApprovalStatus = unapprovedStatus,
            IsActive = true
        };

        context.Courses.Add(course);
        context.Users.Add(student);
        context.Rubrics.Add(rubric);
        context.PracticeQuestions.Add(unapprovedQuestion);
        await context.SaveChangesAsync();

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "medium",
            QuestionCount: 1,
            IsFullSession: false);

        var resultWhenOnlyUnapproved = await handler.Handle(command, CancellationToken.None);
        resultWhenOnlyUnapproved.IsSuccess.Should().BeFalse(
            $"câu hỏi đang ở trạng thái {unapprovedStatus} tuyệt đối không được phép bốc vào phiên luyện tập dù IsActive = true");

        // Thêm 1 câu APPROVED -> Hệ thống chỉ được phép bốc đúng câu APPROVED
        var approvedQuestion = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Câu hỏi APPROVED",
            Content = "Nội dung câu hỏi đã được Trưởng BM phê duyệt",
            Difficulty = "medium",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        context.PracticeQuestions.Add(approvedQuestion);
        await context.SaveChangesAsync();

        var resultWithApproved = await handler.Handle(command, CancellationToken.None);
        resultWithApproved.IsSuccess.Should().BeTrue();
        resultWithApproved.Value!.Questions.Should().ContainSingle();
        resultWithApproved.Value.Questions[0].QuestionId.Should().Be(approvedQuestion.Id);
    }

    [Theory(DisplayName = "R8-02: PracticeQuestion ở trạng thái DRAFT hoặc REJECTED (dù IsActive = true) KHÔNG bị bốc vào phiên Thi thử")]
    [InlineData(ApprovalStatus.Draft)]
    [InlineData(ApprovalStatus.Rejected)]
    public async Task StartMockExam_Should_Not_Pick_Unapproved_PracticeQuestion_Even_When_IsActive_Is_True(string unapprovedStatus)
    {
        using var context = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            SemesterId = Guid.NewGuid(),
            MaxMockExamsPerDay = 3,
            IsActive = true
        };
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "stu_r8_mock@fpt.edu.vn",
            FullName = "Student Mock R8",
            Role = "student",
            IsActive = true
        };
        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CreatedBy = student.Id,
            Name = "Cấu trúc thi thử 1 câu",
            TotalQuestions = 1,
            DurationMinutes = 15,
            BloomDistribution = "{\"Understand\": 1}",
            IsActive = true
        };
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Rubric Mock R8",
            TotalMaxScore = 10.0m,
            IsActive = true
        };
        var unapprovedQuestion = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = $"Câu hỏi thi thử {unapprovedStatus}",
            Content = "Nội dung câu hỏi chưa được phê duyệt",
            Difficulty = "medium",
            BloomLevel = "Understand",
            ApprovalStatus = unapprovedStatus,
            IsActive = true
        };

        context.Courses.Add(course);
        context.Users.Add(student);
        context.ExamStructures.Add(structure);
        context.Rubrics.Add(rubric);
        context.PracticeQuestions.Add(unapprovedQuestion);
        await context.SaveChangesAsync();

        var handler = new StartMockExamCommandHandler(context);
        var command = new StartMockExamCommand(
            CourseId: course.Id,
            StudentId: student.Id,
            HasFollowUp: false);

        var resultWhenOnlyUnapproved = await handler.Handle(command, CancellationToken.None);
        resultWhenOnlyUnapproved.IsSuccess.Should().BeFalse(
            $"câu hỏi đang ở trạng thái {unapprovedStatus} tuyệt đối không được phép bốc vào đề thi thử dù IsActive = true");

        // Thêm 1 câu APPROVED -> Hệ thống bốc đúng câu APPROVED
        var approvedQuestion = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Câu hỏi thi thử APPROVED",
            Content = "Nội dung câu hỏi đã được Trưởng BM phê duyệt",
            Difficulty = "medium",
            BloomLevel = "Understand",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        context.PracticeQuestions.Add(approvedQuestion);
        await context.SaveChangesAsync();

        var resultWithApproved = await handler.Handle(command, CancellationToken.None);
        resultWithApproved.IsSuccess.Should().BeTrue();
        resultWithApproved.Value!.Questions.Should().ContainSingle();
        resultWithApproved.Value.Questions[0].Id.Should().Be(approvedQuestion.Id);
    }

    #endregion
}
