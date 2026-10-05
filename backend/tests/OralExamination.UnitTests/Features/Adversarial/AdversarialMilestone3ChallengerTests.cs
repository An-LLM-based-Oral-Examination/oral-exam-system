using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Appeals.Commands.CreateAppeal;
using OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;
using OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// Bộ kiểm thử thực nghiệm đối kháng (Adversarial Edge Case Tests) cho Milestone 3.
/// Được thiết kế và thực thi độc lập bởi Challenger 1 (teamwork_preview_challenger_m3_1).
/// Kiểm chứng toàn diện:
/// 1. Ca biên UpdateCourseConfigurationCommandValidator: buffer 9s/10s/300s/301s, follow-up 0/1/5/6.
/// 2. Ca biên CreateAppealCommandValidator: Reason rỗng/9 chars/10 chars/2000 chars/2001 chars, TicketId Guid.Empty.
/// 3. Ca biên ReviewAppealDecisionCommandValidator: Decision không hợp lệ/APPROVED/REJECTED, ProposedScore -0.5/10.5/0/10.
/// 4. Rà soát logic handler: chặn trùng đơn (PENDING vs IN_REVIEW), gán department_head tự động.
/// </summary>
public class AdversarialMilestone3ChallengerTests
{
    private readonly UpdateCourseConfigurationCommandValidator _courseValidator = new();
    private readonly CreateAppealCommandValidator _createAppealValidator = new();
    private readonly ReviewAppealDecisionCommandValidator _reviewAppealValidator = new();

    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(User Student, User DeptHead, User Admin, StudentExamTicket Ticket, ExamQuestionSubmission Submission)> SeedBaseEntitiesAsync(OralExamDbContext context)
    {
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = $"student.{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "Sinh Viên Đối Kháng",
            StudentCode = "SE179999",
            Role = UserRole.Student,
            IsActive = true
        };

        var deptHead = new User
        {
            Id = Guid.NewGuid(),
            Email = $"depthead.{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "TS. Trưởng Bộ Môn Đối Kháng",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = $"admin.{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "Quản Trị Viên Đối Kháng",
            Role = UserRole.Admin,
            IsActive = true
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
            Name = "Building Cross-Platform Apps with .NET",
            Credits = 3,
            SemesterId = semester.Id,
            TranscriptBufferSeconds = 60,
            MaxFollowUpQuestions = 1,
            HasFollowUp = true,
            IsActive = true
        };

        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Rubric PRN231 Barem 10.0",
            TotalMaxScore = 10.00m,
            IsActive = true
        };

        var question = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Câu hỏi Clean Architecture",
            Content = "Nêu vai trò tầng Application trong Clean Architecture .NET 8.",
            Difficulty = "medium",
            BloomLevel = BloomLevel.Understand,
            IsActive = true
        };

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            ExamStructureId = Guid.NewGuid(),
            Title = "Kỳ thi Vấn đáp PRN231 FA26",
            ExamDate = new DateOnly(2026, 10, 15),
            Status = "grading"
        };

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1",
            RoomLab = "Lab 402",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            Status = "completed"
        };

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 10,
            IpAddress = "192.168.1.110",
            Status = ExamTicketStatus.Published,
            IsLocked = false
        };

        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = question.Id,
            TranscriptWhisper = "Application chứa use cases, commands, queries và DTOs.",
            AiScore = 7.00m,
            FinalScore = 7.00m,
            GradingStatus = "audited"
        };

        context.Users.AddRange(student, deptHead, admin);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.Rubrics.Add(rubric);
        context.ExamQuestions.Add(question);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.Add(submission);

        await context.SaveChangesAsync();

        return (student, deptHead, admin, ticket, submission);
    }

    #region 1. Ca biên UpdateCourseConfigurationCommandValidator

    [Fact(DisplayName = "ADV-01: Validator PHẢI chặn TranscriptBufferSeconds = 9 giây (cận dưới vi phạm)")]
    public void Validator_Must_Fail_When_Buffer_Is_9_Seconds()
    {
        var command = new UpdateCourseConfigurationCommand(
            CourseId: Guid.NewGuid(),
            TranscriptBufferSeconds: 9,
            MaxFollowUpQuestions: 2,
            HasFollowUp: true
        );

        var result = _courseValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateCourseConfigurationCommand.TranscriptBufferSeconds));
    }

    [Fact(DisplayName = "ADV-02: Validator PHẢI chấp thuận TranscriptBufferSeconds = 10 giây (cận dưới hợp lệ)")]
    public void Validator_Must_Pass_When_Buffer_Is_10_Seconds()
    {
        var command = new UpdateCourseConfigurationCommand(
            CourseId: Guid.NewGuid(),
            TranscriptBufferSeconds: 10,
            MaxFollowUpQuestions: 2,
            HasFollowUp: true
        );

        var result = _courseValidator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "ADV-03: Validator PHẢI chấp thuận TranscriptBufferSeconds = 300 giây (cận trên hợp lệ)")]
    public void Validator_Must_Pass_When_Buffer_Is_300_Seconds()
    {
        var command = new UpdateCourseConfigurationCommand(
            CourseId: Guid.NewGuid(),
            TranscriptBufferSeconds: 300,
            MaxFollowUpQuestions: 2,
            HasFollowUp: true
        );

        var result = _courseValidator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "ADV-04: Validator PHẢI chặn TranscriptBufferSeconds = 301 giây (cận trên vi phạm)")]
    public void Validator_Must_Fail_When_Buffer_Is_301_Seconds()
    {
        var command = new UpdateCourseConfigurationCommand(
            CourseId: Guid.NewGuid(),
            TranscriptBufferSeconds: 301,
            MaxFollowUpQuestions: 2,
            HasFollowUp: true
        );

        var result = _courseValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateCourseConfigurationCommand.TranscriptBufferSeconds));
    }

    [Fact(DisplayName = "ADV-05: Validator PHẢI chặn MaxFollowUpQuestions = 0 (cận dưới vi phạm)")]
    public void Validator_Must_Fail_When_MaxFollowUp_Is_0()
    {
        var command = new UpdateCourseConfigurationCommand(
            CourseId: Guid.NewGuid(),
            TranscriptBufferSeconds: 60,
            MaxFollowUpQuestions: 0,
            HasFollowUp: true
        );

        var result = _courseValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateCourseConfigurationCommand.MaxFollowUpQuestions));
    }

    [Fact(DisplayName = "ADV-06: Validator PHẢI chấp thuận MaxFollowUpQuestions = 1 (cận dưới hợp lệ)")]
    public void Validator_Must_Pass_When_MaxFollowUp_Is_1()
    {
        var command = new UpdateCourseConfigurationCommand(
            CourseId: Guid.NewGuid(),
            TranscriptBufferSeconds: 60,
            MaxFollowUpQuestions: 1,
            HasFollowUp: true
        );

        var result = _courseValidator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "ADV-07: Validator PHẢI chấp thuận MaxFollowUpQuestions = 2 (cận trên hợp lệ)")]
    public void Validator_Must_Pass_When_MaxFollowUp_Is_2()
    {
        var command = new UpdateCourseConfigurationCommand(
            CourseId: Guid.NewGuid(),
            TranscriptBufferSeconds: 60,
            MaxFollowUpQuestions: 2,
            HasFollowUp: true
        );

        var result = _courseValidator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "ADV-08: Validator PHẢI chặn MaxFollowUpQuestions = 6 (cận trên vi phạm)")]
    public void Validator_Must_Fail_When_MaxFollowUp_Is_6()
    {
        var command = new UpdateCourseConfigurationCommand(
            CourseId: Guid.NewGuid(),
            TranscriptBufferSeconds: 60,
            MaxFollowUpQuestions: 6,
            HasFollowUp: true
        );

        var result = _courseValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateCourseConfigurationCommand.MaxFollowUpQuestions));
    }

    [Theory(DisplayName = "ADV-09: Validator PHẢI chặn các giá trị cực trị số âm hoặc số nguyên lớn")]
    [InlineData(-100, 3)]
    [InlineData(60, -5)]
    [InlineData(int.MinValue, 2)]
    [InlineData(60, int.MaxValue)]
    public void Validator_Must_Fail_When_Extreme_Integers_Provided(int buffer, int followUp)
    {
        var command = new UpdateCourseConfigurationCommand(
            CourseId: Guid.NewGuid(),
            TranscriptBufferSeconds: buffer,
            MaxFollowUpQuestions: followUp,
            HasFollowUp: false
        );

        var result = _courseValidator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region 2. Ca biên CreateAppealCommandValidator

    [Theory(DisplayName = "ADV-10: Validator PHẢI chặn Reason rỗng, null hoặc khoảng trắng")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("          ")]
    public void Validator_Must_Fail_When_Reason_Is_Empty_Or_Whitespace(string? reason)
    {
        var command = new CreateAppealCommand(
            StudentId: Guid.NewGuid(),
            TicketId: Guid.NewGuid(),
            SubmissionId: null,
            Reason: reason!
        );

        var result = _createAppealValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAppealCommand.Reason));
    }

    [Fact(DisplayName = "ADV-11: Validator PHẢI chặn Reason đúng 9 ký tự (cận dưới vi phạm)")]
    public void Validator_Must_Fail_When_Reason_Has_9_Chars()
    {
        var command = new CreateAppealCommand(
            StudentId: Guid.NewGuid(),
            TicketId: Guid.NewGuid(),
            SubmissionId: null,
            Reason: "123456789" // 9 chars
        );

        var result = _createAppealValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAppealCommand.Reason));
    }

    [Fact(DisplayName = "ADV-12: Validator PHẢI chấp thuận Reason đúng 10 ký tự (cận dưới hợp lệ)")]
    public void Validator_Must_Pass_When_Reason_Has_10_Chars()
    {
        var command = new CreateAppealCommand(
            StudentId: Guid.NewGuid(),
            TicketId: Guid.NewGuid(),
            SubmissionId: null,
            Reason: "1234567890" // 10 chars
        );

        var result = _createAppealValidator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "ADV-13: Validator PHẢI chấp thuận Reason đúng 2000 ký tự (cận trên hợp lệ)")]
    public void Validator_Must_Pass_When_Reason_Has_2000_Chars()
    {
        var command = new CreateAppealCommand(
            StudentId: Guid.NewGuid(),
            TicketId: Guid.NewGuid(),
            SubmissionId: null,
            Reason: new string('K', 2000)
        );

        var result = _createAppealValidator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "ADV-14: Validator PHẢI chặn Reason đúng 2001 ký tự (cận trên vi phạm)")]
    public void Validator_Must_Fail_When_Reason_Has_2001_Chars()
    {
        var command = new CreateAppealCommand(
            StudentId: Guid.NewGuid(),
            TicketId: Guid.NewGuid(),
            SubmissionId: null,
            Reason: new string('K', 2001)
        );

        var result = _createAppealValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAppealCommand.Reason));
    }

    [Fact(DisplayName = "ADV-15: Validator PHẢI chặn TicketId rỗng (Guid.Empty)")]
    public void Validator_Must_Fail_When_TicketId_Is_Empty()
    {
        var command = new CreateAppealCommand(
            StudentId: Guid.NewGuid(),
            TicketId: Guid.Empty,
            SubmissionId: null,
            Reason: "Lý do phúc khảo hợp lệ trên 10 ký tự."
        );

        var result = _createAppealValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAppealCommand.TicketId));
    }

    [Fact(DisplayName = "ADV-16: Validator PHẢI chặn StudentId rỗng (Guid.Empty)")]
    public void Validator_Must_Fail_When_StudentId_Is_Empty()
    {
        var command = new CreateAppealCommand(
            StudentId: Guid.Empty,
            TicketId: Guid.NewGuid(),
            SubmissionId: null,
            Reason: "Lý do phúc khảo hợp lệ trên 10 ký tự."
        );

        var result = _createAppealValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAppealCommand.StudentId));
    }

    #endregion

    #region 3. Ca biên ReviewAppealDecisionCommandValidator

    [Theory(DisplayName = "ADV-17: Validator PHẢI chặn Decision không phải APPROVED hoặc REJECTED")]
    [InlineData("INVALID_STATUS")]
    [InlineData("PENDING")]
    [InlineData("IN_REVIEW")]
    [InlineData("CANCELLED")]
    [InlineData("approved")] // Lowercase vi phạm quy chuẩn hoa
    [InlineData("rejected")]
    [InlineData("")]
    public void Validator_Must_Fail_When_Decision_Is_Invalid(string decision)
    {
        var command = new ReviewAppealDecisionCommand(
            AppealId: Guid.NewGuid(),
            ReviewerId: Guid.NewGuid(),
            Decision: decision,
            ProposedScore: 8.0m,
            ReviewNotes: "Ghi chú hợp lệ trên 10 ký tự."
        );

        var result = _reviewAppealValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ReviewAppealDecisionCommand.Decision));
    }

    [Theory(DisplayName = "ADV-18: Validator PHẢI chấp thuận Decision APPROVED hoặc REJECTED chuẩn")]
    [InlineData("APPROVED", 8.0)]
    [InlineData("REJECTED", null)]
    public void Validator_Must_Pass_When_Decision_Is_Valid(string decision, double? score)
    {
        var command = new ReviewAppealDecisionCommand(
            AppealId: Guid.NewGuid(),
            ReviewerId: Guid.NewGuid(),
            Decision: decision,
            ProposedScore: score.HasValue ? (decimal)score.Value : null,
            ReviewNotes: "Ghi chú hợp lệ trên 10 ký tự."
        );

        var result = _reviewAppealValidator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "ADV-19: Validator PHẢI chặn ProposedScore âm (-0.5) khi Decision là APPROVED")]
    public void Validator_Must_Fail_When_Approved_And_ProposedScore_Is_Negative()
    {
        var command = new ReviewAppealDecisionCommand(
            AppealId: Guid.NewGuid(),
            ReviewerId: Guid.NewGuid(),
            Decision: AppealStatus.Approved,
            ProposedScore: -0.5m,
            ReviewNotes: "Ghi chú thẩm định nâng điểm nhưng điểm âm."
        );

        var result = _reviewAppealValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ReviewAppealDecisionCommand.ProposedScore));
    }

    [Fact(DisplayName = "ADV-20: Validator PHẢI chặn ProposedScore vượt quá 10.0 (10.5) khi Decision là APPROVED")]
    public void Validator_Must_Fail_When_Approved_And_ProposedScore_Exceeds_10()
    {
        var command = new ReviewAppealDecisionCommand(
            AppealId: Guid.NewGuid(),
            ReviewerId: Guid.NewGuid(),
            Decision: AppealStatus.Approved,
            ProposedScore: 10.5m,
            ReviewNotes: "Ghi chú thẩm định nâng điểm nhưng điểm vượt quá 10."
        );

        var result = _reviewAppealValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ReviewAppealDecisionCommand.ProposedScore));
    }

    [Theory(DisplayName = "ADV-21: Validator PHẢI chấp thuận ProposedScore tại đúng ranh giới 0.00 và 10.00 khi APPROVED")]
    [InlineData(0.00)]
    [InlineData(10.00)]
    public void Validator_Must_Pass_When_Approved_And_ProposedScore_At_Boundaries(double score)
    {
        var command = new ReviewAppealDecisionCommand(
            AppealId: Guid.NewGuid(),
            ReviewerId: Guid.NewGuid(),
            Decision: AppealStatus.Approved,
            ProposedScore: (decimal)score,
            ReviewNotes: "Ghi chú thẩm định nâng điểm chính xác tại ranh giới."
        );

        var result = _reviewAppealValidator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region 4. Rà soát logic handler: chặn trùng đơn & gán department_head tự động

    [Fact(DisplayName = "ADV-22: Handler tự động gán Trưởng Bộ Môn (department_head) khi có user role department_head active")]
    public async Task Handler_Must_Auto_Assign_Active_DepartmentHead()
    {
        using var context = CreateDbContext();
        var (student, deptHead, _, ticket, submission) = await SeedBaseEntitiesAsync(context);

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: ticket.Id,
            SubmissionId: submission.Id,
            Reason: "Kính mong thầy cô chấm phúc khảo lại câu hỏi này."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AssignedTo.Should().Be(deptHead.Id);
        result.Value.AssignedToName.Should().Be(deptHead.FullName);
    }

    [Fact(DisplayName = "ADV-23: Handler tự động fallback sang admin khi không có department_head active")]
    public async Task Handler_Must_Fallback_To_Admin_When_DepartmentHead_Inactive()
    {
        using var context = CreateDbContext();
        var (student, deptHead, admin, ticket, submission) = await SeedBaseEntitiesAsync(context);

        // Vô hiệu hóa deptHead
        deptHead.IsActive = false;
        await context.SaveChangesAsync();

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: ticket.Id,
            SubmissionId: submission.Id,
            Reason: "Kính mong phúc khảo khi trưởng bộ môn đang nghỉ phép/inactive."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AssignedTo.Should().Be(admin.Id);
        result.Value.AssignedToName.Should().Be(admin.FullName);
    }

    [Fact(DisplayName = "ADV-24: Handler PHẢI chặn nộp trùng đơn khi đơn cũ đang có trạng thái PENDING")]
    public async Task Handler_Must_Block_Duplicate_Appeal_When_Existing_Is_Pending()
    {
        using var context = CreateDbContext();
        var (student, deptHead, _, ticket, submission) = await SeedBaseEntitiesAsync(context);

        var existingAppeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = Guid.NewGuid(),
            SubmissionId = submission.Id,
            Reason = "Đơn phúc khảo lần đầu đang chờ xử lý.",
            Status = AppealStatus.Pending,
            AssignedTo = deptHead.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.AppealRequests.Add(existingAppeal);
        await context.SaveChangesAsync();

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: ticket.Id,
            SubmissionId: submission.Id,
            Reason: "Cố tình nộp tiếp đơn phúc khảo thứ 2 khi đơn 1 đang PENDING."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("đã tồn tại và đang chờ xử lý");
    }

    [Fact(DisplayName = "ADV-25 [STRESS/VULNERABILITY]: Kiểm tra hành vi Handler khi đơn cũ đang có trạng thái IN_REVIEW")]
    public async Task Handler_Empirical_Observation_When_Existing_Appeal_Is_InReview()
    {
        using var context = CreateDbContext();
        var (student, deptHead, _, ticket, submission) = await SeedBaseEntitiesAsync(context);

        // Giả lập đơn phúc khảo trước đó đã được Trưởng Bộ Môn tiếp nhận và chuyển sang trạng thái IN_REVIEW
        var inReviewAppeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = Guid.NewGuid(),
            SubmissionId = submission.Id,
            Reason = "Đơn phúc khảo đang được thẩm định (IN_REVIEW).",
            Status = AppealStatus.InReview,
            AssignedTo = deptHead.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.AppealRequests.Add(inReviewAppeal);
        await context.SaveChangesAsync();

        var handler = new CreateAppealCommandHandler(context);
        var duplicateCommand = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: ticket.Id,
            SubmissionId: submission.Id,
            Reason: "Sinh viên nộp trùng đơn thứ hai trong khi đơn trước đang IN_REVIEW."
        );

        var result = await handler.Handle(duplicateCommand, CancellationToken.None);

        // Thực nghiệm kiểm chứng:
        // Hiện tại CreateAppealCommandHandler.cs dòng 54-58 chỉ kiểm tra:
        // a.Status == AppealStatus.Pending.
        // Do đó khi đơn cũ ở trạng thái IN_REVIEW, hasPendingAppeal trả về false,
        // dẫn đến kết quả IsSuccess = true (CHO PHÉP NỘP TRÙNG LẶP).
        // Đây là bằng chứng thực nghiệm cho thấy handler CHƯA chặn IN_REVIEW!
        var totalAppealsForThisSubmission = await context.AppealRequests
            .CountAsync(a => a.TicketId == ticket.Id && a.SubmissionId == submission.Id);

        // Kiểm chứng sau khi khắc phục: Handler PHẢI chặn nộp trùng đơn khi đơn cũ đang IN_REVIEW
        result.IsSuccess.Should().BeFalse("Handler PHẢI chặn nộp trùng đơn khi đơn cũ đang IN_REVIEW.");
        result.Error.Should().Contain("đang chờ xử lý");
        totalAppealsForThisSubmission.Should().Be(1, "Chỉ được phép có duy nhất 1 bản ghi đơn phúc khảo trong CSDL.");
    }

    #endregion
}
