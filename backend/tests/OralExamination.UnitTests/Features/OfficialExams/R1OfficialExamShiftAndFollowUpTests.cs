using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.ConfigureOfficialExamFollowUp;
using OralExamination.Application.Features.OfficialExams.Commands.OpenExamShift;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public class R1OfficialExamShiftAndFollowUpTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "R1-01: Mở ca thi thành công với cả Proctor và Examiner hợp lệ, gán ExaminerUserId")]
    public async Task OpenExamShift_ValidProctorAndExaminer_Should_Succeed_And_Assign_Examiner()
    {
        await using var context = CreateDbContext();

        var examOfficer = new User
        {
            Id = Guid.NewGuid(),
            Email = "officer@fe.edu.vn",
            FullName = "Cán bộ Khảo thí",
            Role = UserRole.ExaminationOfficer,
            IsActive = true
        };
        var proctor = new User
        {
            Id = Guid.NewGuid(),
            Email = "proctor@fe.edu.vn",
            FullName = "Giám thị Coi thi",
            Role = UserRole.Proctor,
            IsActive = true
        };
        var examiner = new User
        {
            Id = Guid.NewGuid(),
            Email = "examiner@fe.edu.vn",
            FullName = "Giảng viên Chấm thi",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = ".NET Architecture",
            IsActive = true
        };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Title = "Kỳ thi chính thức PRN231",
            ExamDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            Status = "scheduled",
            CreatedBy = examOfficer.Id
        };

        context.Users.AddRange(examOfficer, proctor, examiner);
        context.Courses.Add(course);
        context.OfficialExamSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new OpenExamShiftCommandHandler(context, new OpenExamShiftCommandValidator());
        var command = new OpenExamShiftCommand(
            examOfficer.Id,
            session.Id,
            "Ca 1 - Phòng Lab AL-L201",
            "AL-L201",
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(4),
            proctor.Id,
            examiner.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ExaminerUserId.Should().Be(examiner.Id);
        result.Value.ExaminerName.Should().Be(examiner.FullName);
        result.Value.ProctorUserId.Should().Be(proctor.Id);
        result.Value.ProctorName.Should().Be(proctor.FullName);

        var savedShift = await context.RealExamSessionShifts.FirstOrDefaultAsync(s => s.Id == result.Value.Id);
        savedShift.Should().NotBeNull();
        savedShift!.ExaminerUserId.Should().Be(examiner.Id);
        savedShift.ProctorUserId.Should().Be(proctor.Id);
    }

    [Fact(DisplayName = "R1-02: Báo lỗi validation khi thiếu ExaminerUserId")]
    public async Task OpenExamShift_MissingExaminer_Should_Fail_Validation()
    {
        var validator = new OpenExamShiftCommandValidator();
        var command = new OpenExamShiftCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ca thi",
            "Lab 1",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(2),
            Guid.NewGuid(),
            Guid.Empty);

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.ExaminerUserId));
    }

    [Fact(DisplayName = "R1-03: Báo lỗi validation khi ExaminerUserId trùng ProctorUserId")]
    public async Task OpenExamShift_ExaminerSameAsProctor_Should_Fail_Validation()
    {
        var sameUserId = Guid.NewGuid();
        var validator = new OpenExamShiftCommandValidator();
        var command = new OpenExamShiftCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ca thi",
            "Lab 1",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(2),
            sameUserId,
            sameUserId);

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("không được trùng"));
    }

    [Fact(DisplayName = "R1-04: Cấu hình Follow-up bởi Trưởng bộ môn thành công")]
    public async Task ConfigureOfficialExamFollowUp_DepartmentHead_Should_Update_Config()
    {
        await using var context = CreateDbContext();

        var deptHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "head@fe.edu.vn",
            FullName = "Trưởng Bộ Môn",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Title = "Kỳ thi Lab",
            ExamDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            HasFollowUp = false,
            MaxFollowUpQuestions = 1
        };

        context.Users.Add(deptHead);
        context.OfficialExamSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new ConfigureOfficialExamFollowUpCommandHandler(context, new ConfigureOfficialExamFollowUpCommandValidator());
        var command = new ConfigureOfficialExamFollowUpCommand(deptHead.Id, session.Id, true, 3);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.HasFollowUp.Should().BeTrue();
        result.Value.MaxFollowUpQuestions.Should().Be(3);

        var updated = await context.OfficialExamSessions.FindAsync(session.Id);
        updated!.HasFollowUp.Should().BeTrue();
        updated.MaxFollowUpQuestions.Should().Be(3);
    }

    [Fact(DisplayName = "R1-05: Cán bộ Khảo thí gọi cấu hình Follow-up bị từ chối Forbidden")]
    public async Task ConfigureOfficialExamFollowUp_ExaminationOfficer_Should_Be_Forbidden()
    {
        await using var context = CreateDbContext();

        var officer = new User
        {
            Id = Guid.NewGuid(),
            Email = "officer@fe.edu.vn",
            FullName = "Cán bộ Khảo thí",
            Role = UserRole.ExaminationOfficer,
            IsActive = true
        };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Title = "Kỳ thi Lab",
            ExamDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2))
        };

        context.Users.Add(officer);
        context.OfficialExamSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new ConfigureOfficialExamFollowUpCommandHandler(context, new ConfigureOfficialExamFollowUpCommandValidator());
        var command = new ConfigureOfficialExamFollowUpCommand(officer.Id, session.Id, true, 2);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.Forbidden);
    }

    [Theory(DisplayName = "R1-06: Cấu hình Follow-up số câu ngoài khoảng [1, 5] báo lỗi validation")]
    [InlineData(0)]
    [InlineData(6)]
    public async Task ConfigureOfficialExamFollowUp_QuestionsOutOfRange_Should_Fail_Validation(int invalidMaxQuestions)
    {
        var validator = new ConfigureOfficialExamFollowUpCommandValidator();
        var command = new ConfigureOfficialExamFollowUpCommand(Guid.NewGuid(), Guid.NewGuid(), true, invalidMaxQuestions);

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.MaxFollowUpQuestions));
    }
}
