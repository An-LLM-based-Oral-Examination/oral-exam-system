using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.CreateOfficialExamSession;
using OralExamination.Application.Features.OfficialExams.Commands.OpenExamShift;
using OralExamination.Application.Features.OfficialExams.Commands.PublishExamShift;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

public class SixRolesRbacTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "RBAC-01: Hệ thống hỗ trợ đầy đủ 6 vai trò chuẩn hóa")]
    public void System_Must_Support_All_Six_Standard_Roles()
    {
        var roles = UserRole.All;

        roles.Should().HaveCount(6);
        roles.Should().Contain(UserRole.Admin);
        roles.Should().Contain(UserRole.DepartmentHead);
        roles.Should().Contain(UserRole.ExaminationOfficer);
        roles.Should().Contain(UserRole.Lecturer);
        roles.Should().Contain(UserRole.Proctor);
        roles.Should().Contain(UserRole.Student);
        UserRole.ExaminationOfficer.Should().Be("examination_officer");
    }

    [Fact(DisplayName = "RBAC-02: Check constraint ck_users_role trong DbContext chứa đủ 6 vai trò")]
    public void Users_Table_Check_Constraint_Must_Include_All_Six_Roles()
    {
        using var context = CreateDbContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var userType = model.FindEntityType(typeof(User))!;
        var roleCheck = userType.GetCheckConstraints().FirstOrDefault(c => c.Name == "ck_users_role");

        roleCheck.Should().NotBeNull("Bảng users phải có check constraint ck_users_role");
        roleCheck!.Sql.Should().Contain("'examination_officer'");
        roleCheck.Sql.Should().Contain("'admin'");
        roleCheck.Sql.Should().Contain("'department_head'");
        roleCheck.Sql.Should().Contain("'lecturer'");
        roleCheck.Sql.Should().Contain("'proctor'");
        roleCheck.Sql.Should().Contain("'student'");
    }

    [Fact(DisplayName = "RBAC-03: Cán bộ khảo thí (examination_officer) được phép tạo kỳ thi thật")]
    public async Task ExaminationOfficer_Can_Create_OfficialExamSession()
    {
        await using var context = CreateDbContext();

        var examOfficer = new User
        {
            Id = Guid.NewGuid(),
            Email = "khaothi@fe.edu.vn",
            FullName = "Cán bộ Khảo Thí",
            Role = UserRole.ExaminationOfficer,
            IsActive = true
        };
        context.Users.Add(examOfficer);

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Cross-Platform Apps",
            IsActive = true
        };
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        var handler = new CreateOfficialExamSessionCommandHandler(context, new CreateOfficialExamSessionCommandValidator());
        var command = new CreateOfficialExamSessionCommand(
            examOfficer.Id,
            course.Id,
            null,
            "Kỳ Thi Vấn Đáp PRN231 - FA26",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            true,
            2,
            "VoiceOnly",
            60);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Kỳ Thi Vấn Đáp PRN231 - FA26");
        result.Value.CourseId.Should().Be(course.Id);
    }

    [Fact(DisplayName = "RBAC-04: Cán bộ khảo thí được phép mở ca thi phòng Lab và chỉ định giám thị")]
    public async Task ExaminationOfficer_Can_Open_ExamShift()
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
            Email = "giamthi@fe.edu.vn",
            FullName = "Giám thị 01",
            Role = UserRole.Proctor,
            IsActive = true
        };
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "CSD201",
            Name = "Data Structures and Algorithms",
            IsActive = true
        };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Title = "Kỳ thi CSD201",
            ExamDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            Status = "scheduled",
            CreatedBy = examOfficer.Id
        };

        context.Users.AddRange(examOfficer, proctor);
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
            proctor.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShiftName.Should().Be("Ca 1 - Phòng Lab AL-L201");
        result.Value.RoomLab.Should().Be("AL-L201");
    }

    [Fact(DisplayName = "RBAC-05: Vai trò không được phép mở ca thi sẽ bị từ chối (Forbidden)")]
    public async Task UnauthorizedRole_Cannot_Open_ExamShift()
    {
        await using var context = CreateDbContext();

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student@fe.edu.vn",
            FullName = "Sinh viên A",
            Role = UserRole.Student,
            IsActive = true
        };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Title = "Kỳ thi",
            ExamDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            Status = "scheduled",
            CreatedBy = Guid.NewGuid()
        };

        context.Users.Add(student);
        context.OfficialExamSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new OpenExamShiftCommandHandler(context, new OpenExamShiftCommandValidator());
        var command = new OpenExamShiftCommand(
            student.Id,
            session.Id,
            "Ca 1",
            "AL-L201",
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(4),
            Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.Forbidden);
    }
}
