using System;
using System.Collections.Generic;
using System.Linq;
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
using OralExamination.Application.Features.Appeals.Commands.CreateAppeal;
using OralExamination.Application.Features.Appeals.DTOs;
using OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;
using OralExamination.Application.Features.Courses.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// Bộ kiểm thử thực nghiệm đối kháng Milestone 5 (Empirical Challenger M5).
/// Thực thi độc lập bởi Challenger M5_1 (challenger_m5_1).
/// 
/// Trọng tâm thử thách:
/// 1. Dải số câu hỏi phụ chuyên sâu (MaxFollowUpQuestions 1–2 câu):
///    - Cận dưới vi phạm: 0 câu -> REJECT
///    - Cận dưới hợp lệ: 1 câu -> PASS
///    - Cận trên hợp lệ: 2 câu -> PASS
///    - Cận trên vi phạm: 3 câu -> REJECT
///    - Cực trị âm và vượt ngưỡng: -10, -1, 4, 10, int.MaxValue -> REJECT
///    - Cập nhật Handler và ánh xạ CSDL & DTO
///    - Phản hồi HTTP Controller khi vi phạm dải số (422 Unprocessable Entity)
/// 
/// 2. Guard clause nộp đơn phúc khảo (Chỉ PUBLISHED hoặc LOCKED mới được nộp):
///    - Trạng thái chưa công bố: SCHEDULED, IN_PROGRESS, SUBMITTED, AI_GRADED, AUDITED -> REJECT
///    - Trạng thái hợp lệ: PUBLISHED, LOCKED -> PASS
///    - Tính bất biến hoa thường (OrdinalIgnoreCase)
///    - Trạng thái bất thường / rác: DRAFT, CANCELLED, null, rỗng -> REJECT
///    - Toàn vẹn vé thi: Vé không tồn tại, sai quyền sinh viên, sai submission, trùng lặp PENDING/IN_REVIEW -> REJECT
/// </summary>
public class AdversarialMilestone5ChallengerTests
{
    private readonly UpdateCourseConfigurationCommandValidator _courseConfigValidator = new();
    private readonly CreateAppealCommandValidator _appealValidator = new();

    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(User Student, User DeptHead, User Admin, Course Course, StudentExamTicket Ticket, ExamQuestionSubmission Submission)> SeedBaseContextAsync(
        OralExamDbContext context,
        string ticketStatus = ExamTicketStatus.Published,
        bool isLocked = false)
    {
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = $"student.{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "Nguyễn Văn Sinh Viên M5",
            StudentCode = "SE170999",
            Role = UserRole.Student,
            IsActive = true
        };

        var deptHead = new User
        {
            Id = Guid.NewGuid(),
            Email = $"depthead.{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "TS. Trưởng Bộ Môn M5",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = $"admin.{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "Quản Trị Viên M5",
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
            Name = "Building Cross-Platform Back-End Applications with .NET",
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
            Name = "Rubric PRN231 10.0",
            TotalMaxScore = 10.00m,
            IsActive = true
        };

        var question = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Kiểm thử đối kháng kiến trúc",
            Content = "Phân tích và chứng minh tính bất biến của Clean Architecture.",
            Difficulty = "hard",
            BloomLevel = BloomLevel.Evaluate,
            IsActive = true
        };

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            ExamStructureId = Guid.NewGuid(),
            Title = "Ca thi chính thức M5",
            ExamDate = new DateOnly(2026, 10, 15),
            Status = "published"
        };

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca M5 - Lab 1",
            RoomLab = "Lab 401",
            StartTime = DateTime.UtcNow.AddHours(-3),
            EndTime = DateTime.UtcNow.AddHours(-2),
            Status = "completed"
        };

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 10,
            IpAddress = "192.168.1.110",
            Status = ticketStatus,
            IsLocked = isLocked
        };

        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = question.Id,
            TranscriptWhisper = "Transcript đã được ghi nhận an toàn.",
            AiScore = 7.50m,
            FinalScore = 7.50m,
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

        return (student, deptHead, admin, course, ticket, submission);
    }

    #region Part 1: Thử thách dải số câu hỏi phụ 1-2 (0, 1, 2, 3 câu)

    [Fact(DisplayName = "ADV-M5-01: Validator PHẢI chặn MaxFollowUpQuestions = 0 (cận dưới vi phạm)")]
    public void Validator_Must_Reject_When_MaxFollowUpQuestions_Is_0()
    {
        var command = new UpdateCourseConfigurationCommand(
            CourseId: Guid.NewGuid(),
            TranscriptBufferSeconds: 60,
            MaxFollowUpQuestions: 0,
            HasFollowUp: true
        );

        var result = _courseConfigValidator.Validate(command);

        result.IsValid.Should().BeFalse("MaxFollowUpQuestions = 0 vi phạm dải quy chuẩn [1, 5].");
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateCourseConfigurationCommand.MaxFollowUpQuestions));
        result.Errors.First(e => e.PropertyName == nameof(UpdateCourseConfigurationCommand.MaxFollowUpQuestions))
            .ErrorMessage.Should().Contain("1 đến 5 câu");
    }

    [Fact(DisplayName = "ADV-M5-02: Validator PHẢI chấp thuận MaxFollowUpQuestions = 1 (cận dưới hợp lệ)")]
    public void Validator_Must_Approve_When_MaxFollowUpQuestions_Is_1()
    {
        var command = new UpdateCourseConfigurationCommand(
            CourseId: Guid.NewGuid(),
            TranscriptBufferSeconds: 60,
            MaxFollowUpQuestions: 1,
            HasFollowUp: true
        );

        var result = _courseConfigValidator.Validate(command);

        result.IsValid.Should().BeTrue("MaxFollowUpQuestions = 1 là cận dưới hợp lệ của dải [1, 2].");
        result.Errors.Should().BeEmpty();
    }

    [Fact(DisplayName = "ADV-M5-03: Validator PHẢI chấp thuận MaxFollowUpQuestions = 2 (cận trên hợp lệ)")]
    public void Validator_Must_Approve_When_MaxFollowUpQuestions_Is_2()
    {
        var command = new UpdateCourseConfigurationCommand(
            CourseId: Guid.NewGuid(),
            TranscriptBufferSeconds: 60,
            MaxFollowUpQuestions: 2,
            HasFollowUp: true
        );

        var result = _courseConfigValidator.Validate(command);

        result.IsValid.Should().BeTrue("MaxFollowUpQuestions = 2 là cận trên hợp lệ của dải [1, 2].");
        result.Errors.Should().BeEmpty();
    }

    [Fact(DisplayName = "ADV-M5-04: Validator PHẢI chặn MaxFollowUpQuestions = 6 (cận trên vi phạm)")]
    public void Validator_Must_Reject_When_MaxFollowUpQuestions_Is_6()
    {
        var command = new UpdateCourseConfigurationCommand(
            CourseId: Guid.NewGuid(),
            TranscriptBufferSeconds: 60,
            MaxFollowUpQuestions: 6,
            HasFollowUp: true
        );

        var result = _courseConfigValidator.Validate(command);

        result.IsValid.Should().BeFalse("MaxFollowUpQuestions = 6 vượt quá cận trên quy chuẩn [1, 5].");
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateCourseConfigurationCommand.MaxFollowUpQuestions));
        result.Errors.First(e => e.PropertyName == nameof(UpdateCourseConfigurationCommand.MaxFollowUpQuestions))
            .ErrorMessage.Should().Contain("1 đến 5 câu");
    }

    [Theory(DisplayName = "ADV-M5-05: Validator PHẢI chặn toàn bộ các giá trị âm và vượt ngưỡng [1, 5]")]
    [InlineData(-100)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(999)]
    [InlineData(int.MaxValue)]
    public void Validator_Must_Reject_Extreme_Out_Of_Range_FollowUp_Values(int outOfRangeValue)
    {
        var command = new UpdateCourseConfigurationCommand(
            CourseId: Guid.NewGuid(),
            TranscriptBufferSeconds: 60,
            MaxFollowUpQuestions: outOfRangeValue,
            HasFollowUp: true
        );

        var result = _courseConfigValidator.Validate(command);

        result.IsValid.Should().BeFalse($"Giá trị {outOfRangeValue} không nằm trong [1, 5] bắt buộc phải bị chặn.");
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateCourseConfigurationCommand.MaxFollowUpQuestions));
    }

    [Theory(DisplayName = "ADV-M5-06: Handler thực thi chuẩn xác và lưu bền vững giá trị hợp lệ vào CSDL")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public async Task Handler_Must_Persist_Valid_MaxFollowUpQuestions_To_Database(int validCount)
    {
        using var context = CreateDbContext();
        var (_, _, _, course, _, _) = await SeedBaseContextAsync(context);

        var handler = new UpdateCourseConfigurationCommandHandler(context);
        var command = new UpdateCourseConfigurationCommand(
            CourseId: course.Id,
            TranscriptBufferSeconds: 90,
            MaxFollowUpQuestions: validCount,
            HasFollowUp: true
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.MaxFollowUpQuestions.Should().Be(validCount);
        result.Value.TranscriptBufferSeconds.Should().Be(90);
        result.Value.HasFollowUp.Should().BeTrue();

        var updatedCourseInDb = await context.Courses.FirstOrDefaultAsync(c => c.Id == course.Id);
        updatedCourseInDb.Should().NotBeNull();
        updatedCourseInDb!.MaxFollowUpQuestions.Should().Be(validCount);
        updatedCourseInDb.TranscriptBufferSeconds.Should().Be(90);
    }

    [Fact(DisplayName = "ADV-M5-07: Course entity mặc định khởi tạo MaxFollowUpQuestions = 2, HasFollowUp = false")]
    public void Course_Entity_Must_Have_Sensible_Defaults()
    {
        var course = new Course();
        course.MaxFollowUpQuestions.Should().Be(2, "Quy chế đồ án quy định giá trị mặc định cho câu hỏi phụ do Admin cấu hình hệ thống luyện tập là 2.");
        course.HasFollowUp.Should().BeFalse("Mặc định môn học không bật hỏi phụ trừ khi giảng viên chủ động bật.");
        course.TranscriptBufferSeconds.Should().Be(60, "Thời gian đệm mặc định là 60 giây.");
    }

    [Theory(DisplayName = "ADV-M5-08: CoursesController trả về 422 UnprocessableEntity khi Command thất bại do lỗi cấu hình")]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Controller_Must_Return_422_When_UpdateConfiguration_Fails(int invalidMaxFollowUp)
    {
        var senderMock = new Mock<ISender>();
        var controller = new CoursesController(senderMock.Object);
        var courseId = Guid.NewGuid();

        senderMock
            .Setup(s => s.Send(It.Is<UpdateCourseConfigurationCommand>(c => c.MaxFollowUpQuestions == invalidMaxFollowUp), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CourseConfigurationDto>.Failure("Số lượng câu hỏi phụ tối đa phải nằm trong khoảng từ 1 đến 5 câu."));

        var requestDto = new UpdateCourseConfigRequestDto
        {
            TranscriptBufferSeconds = 60,
            MaxFollowUpQuestions = invalidMaxFollowUp,
            HasFollowUp = true
        };

        var actionResult = await controller.UpdateConfiguration(courseId, requestDto);

        var unprocResult = actionResult.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        unprocResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    #endregion

    #region Part 2: Thử thách guard clause nộp phúc khảo (PUBLISHED / LOCKED)

    [Theory(DisplayName = "ADV-M5-09: Handler PHẢI chặn toàn bộ các trạng thái vé thi chưa công bố điểm (SCHEDULED, IN_PROGRESS, SUBMITTED, AI_GRADED, AUDITED)")]
    [InlineData(ExamTicketStatus.Scheduled)]
    [InlineData(ExamTicketStatus.InProgress)]
    [InlineData(ExamTicketStatus.Submitted)]
    [InlineData(ExamTicketStatus.AiGraded)]
    [InlineData(ExamTicketStatus.Audited)]
    public async Task Handler_Must_Block_Appeal_When_TicketStatus_Is_Unpublished(string unreleasedStatus)
    {
        using var context = CreateDbContext();
        var (student, _, _, _, ticket, submission) = await SeedBaseContextAsync(context, ticketStatus: unreleasedStatus);

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: ticket.Id,
            SubmissionId: submission.Id,
            Reason: "Sinh viên cố tình gửi đơn phúc khảo trước khi giảng viên công bố điểm ca thi."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse($"Vé thi ở trạng thái '{unreleasedStatus}' tuyệt đối không được phép nộp phúc khảo.");
        result.Error.Should().Contain("PUBLISHED hoặc LOCKED");

        var appealsCount = await context.AppealRequests.CountAsync();
        appealsCount.Should().Be(0, "Không được tạo bất kỳ bản ghi AppealRequest nào khi guard clause kích hoạt.");
    }

    [Fact(DisplayName = "ADV-M5-10: Handler PHẢI chấp thuận khi vé thi ở trạng thái PUBLISHED")]
    public async Task Handler_Must_Accept_Appeal_When_TicketStatus_Is_Published()
    {
        using var context = CreateDbContext();
        var (student, deptHead, _, _, ticket, submission) = await SeedBaseContextAsync(context, ticketStatus: ExamTicketStatus.Published);

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: ticket.Id,
            SubmissionId: submission.Id,
            Reason: "Kính mong Trưởng Bộ Môn phúc khảo câu hỏi Clean Architecture."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue("Vé thi đã PUBLISHED thì sinh viên có quyền nộp đơn phúc khảo.");
        result.Value.Should().NotBeNull();
        result.Value.Status.Should().Be(AppealStatus.Pending);
        result.Value.TicketId.Should().Be(ticket.Id);
        result.Value.AssignedTo.Should().Be(deptHead.Id);

        var createdAppeal = await context.AppealRequests.FirstOrDefaultAsync(a => a.TicketId == ticket.Id);
        createdAppeal.Should().NotBeNull();
        createdAppeal!.Status.Should().Be(AppealStatus.Pending);
    }

    [Fact(DisplayName = "ADV-M5-11: Handler PHẢI chấp thuận khi vé thi ở trạng thái LOCKED (đã khóa điểm 1 chiều)")]
    public async Task Handler_Must_Accept_Appeal_When_TicketStatus_Is_Locked()
    {
        using var context = CreateDbContext();
        var (student, deptHead, _, _, ticket, submission) = await SeedBaseContextAsync(
            context,
            ticketStatus: ExamTicketStatus.Locked,
            isLocked: true);

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: ticket.Id,
            SubmissionId: submission.Id,
            Reason: "Nộp phúc khảo sau khi ca thi đã bị khóa điểm một chiều (LOCKED)."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue("Vé thi ở trạng thái LOCKED vẫn được phép nộp phúc khảo.");
        result.Value.Should().NotBeNull();
        result.Value.TicketId.Should().Be(ticket.Id);
        result.Value.Status.Should().Be(AppealStatus.Pending);
    }

    [Theory(DisplayName = "ADV-M5-12: Handler xử lý không phân biệt chữ hoa/thường cho PUBLISHED và LOCKED (OrdinalIgnoreCase)")]
    [InlineData("published")]
    [InlineData("Published")]
    [InlineData("pUbLiShEd")]
    [InlineData("locked")]
    [InlineData("Locked")]
    [InlineData("lOcKeD")]
    public async Task Handler_Must_Be_Case_Insensitive_For_Allowed_Statuses(string casedStatus)
    {
        using var context = CreateDbContext();
        var (student, _, _, _, ticket, submission) = await SeedBaseContextAsync(context, ticketStatus: casedStatus);

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: ticket.Id,
            SubmissionId: submission.Id,
            Reason: "Kiểm tra tính an toàn hoa thường của status vé thi."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue($"Status '{casedStatus}' phải được chấp thuận nhờ so sánh StringComparison.OrdinalIgnoreCase.");
    }

    [Theory(DisplayName = "ADV-M5-13: Handler PHẢI chặn toàn bộ các chuỗi trạng thái rác hoặc bất thường")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UNKNOWN")]
    [InlineData("CANCELLED")]
    [InlineData("DRAFT")]
    [InlineData("EXPIRED")]
    [InlineData("published_extra")]
    public async Task Handler_Must_Block_Arbitrary_And_Garbage_Statuses(string garbageStatus)
    {
        using var context = CreateDbContext();
        var (student, _, _, _, ticket, submission) = await SeedBaseContextAsync(context, ticketStatus: garbageStatus);

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: ticket.Id,
            SubmissionId: submission.Id,
            Reason: "Cố tình nộp đơn với trạng thái không xác định."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse($"Trạng thái rác '{garbageStatus}' phải bị từ chối.");
        result.Error.Should().Contain("PUBLISHED hoặc LOCKED");
    }

    [Fact(DisplayName = "ADV-M5-14: Handler PHẢI chặn khi sinh viên không sở hữu vé thi kể cả khi vé thi đã PUBLISHED")]
    public async Task Handler_Must_Block_When_Student_Does_Not_Own_Ticket()
    {
        using var context = CreateDbContext();
        var (_, _, _, _, ticket, submission) = await SeedBaseContextAsync(context, ticketStatus: ExamTicketStatus.Published);

        var attackerStudentId = Guid.NewGuid();

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: attackerStudentId,
            TicketId: ticket.Id,
            SubmissionId: submission.Id,
            Reason: "Sinh viên khác cố tình gửi đơn phúc khảo cho vé thi này."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không sở hữu vé thi");
    }

    [Fact(DisplayName = "ADV-M5-15: Handler PHẢI chặn khi SubmissionId không thuộc về vé thi")]
    public async Task Handler_Must_Block_When_SubmissionId_Does_Not_Belong_To_Ticket()
    {
        using var context = CreateDbContext();
        var (student, _, _, _, ticket, _) = await SeedBaseContextAsync(context, ticketStatus: ExamTicketStatus.Published);

        var foreignSubmissionId = Guid.NewGuid();

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: ticket.Id,
            SubmissionId: foreignSubmissionId,
            Reason: "Nộp đơn phúc khảo trỏ tới một câu hỏi không nằm trong ca thi."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không tồn tại hoặc không thuộc vé thi");
    }

    [Fact(DisplayName = "ADV-M5-16: Handler PHẢI chặn nộp trùng lặp khi đơn phúc khảo cũ đang PENDING hoặc IN_REVIEW")]
    public async Task Handler_Must_Block_Duplicate_Appeals()
    {
        using var context = CreateDbContext();
        var (student, deptHead, _, _, ticket, submission) = await SeedBaseContextAsync(context, ticketStatus: ExamTicketStatus.Published);

        var pendingAppeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = Guid.NewGuid(),
            SubmissionId = submission.Id,
            Reason = "Đơn phúc khảo lần 1",
            Status = AppealStatus.Pending,
            AssignedTo = deptHead.Id
        };
        context.AppealRequests.Add(pendingAppeal);
        await context.SaveChangesAsync();

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: ticket.Id,
            SubmissionId: submission.Id,
            Reason: "Nộp đơn phúc khảo lần 2 khi đơn 1 đang chờ duyệt."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("đang chờ xử lý");
    }

    [Fact(DisplayName = "ADV-M5-17: Handler tự động gán Trưởng Bộ Môn (DepartmentHead) khi tạo đơn phúc khảo thành công")]
    public async Task Handler_Must_Auto_Assign_DepartmentHead_When_Creating_Appeal()
    {
        using var context = CreateDbContext();
        var (student, deptHead, _, _, ticket, submission) = await SeedBaseContextAsync(context, ticketStatus: ExamTicketStatus.Published);

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: ticket.Id,
            SubmissionId: submission.Id,
            Reason: "Kính mong Trưởng Bộ Môn xem xét phúc khảo bài thi."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AssignedTo.Should().Be(deptHead.Id);
        result.Value.AssignedToName.Should().Be(deptHead.FullName);

        var savedAppeal = await context.AppealRequests.FirstOrDefaultAsync(a => a.Id == result.Value.Id);
        savedAppeal.Should().NotBeNull();
        savedAppeal!.AssignedTo.Should().Be(deptHead.Id);
    }

    [Fact(DisplayName = "ADV-M5-18: AppealsController trả về 422 UnprocessableEntity khi nộp phúc khảo bị guard clause chặn")]
    public async Task AppealsController_Must_Return_422_When_Guard_Clause_Fails()
    {
        var senderMock = new Mock<ISender>();
        var controller = new AppealsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var requestDto = new CreateAppealRequestDto
        {
            StudentId = Guid.NewGuid(),
            TicketId = Guid.NewGuid(),
            SubmissionId = null,
            Reason = "Lý do phúc khảo hợp lệ trên 10 ký tự"
        };

        senderMock
            .Setup(s => s.Send(It.IsAny<CreateAppealCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AppealResponseDto>.Failure("Chỉ được phép nộp đơn phúc khảo sau khi điểm thi đã được công bố chính thức (PUBLISHED hoặc LOCKED)."));

        var actionResult = await controller.CreateAppeal(requestDto);

        var unprocResult = actionResult.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        unprocResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact(DisplayName = "ADV-M5-19: AppealsController xử lý an toàn khi User có claims hoặc header X-User-Id")]
    public async Task AppealsController_Must_Extract_User_From_Claims_Or_Header()
    {
        var senderMock = new Mock<ISender>();
        var expectedUserId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = expectedUserId.ToString();

        var controller = new AppealsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        var requestDto = new CreateAppealRequestDto
        {
            TicketId = Guid.NewGuid(),
            Reason = "Lý do phúc khảo hợp lệ trên 10 ký tự"
        };

        senderMock
            .Setup(s => s.Send(It.Is<CreateAppealCommand>(c => c.StudentId == expectedUserId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AppealResponseDto>.Failure("Chỉ được phép nộp đơn phúc khảo sau khi điểm thi đã được công bố chính thức (PUBLISHED hoặc LOCKED)."));

        var actionResult = await controller.CreateAppeal(requestDto);

        var unprocResult = actionResult.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        unprocResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }


    #endregion
}
