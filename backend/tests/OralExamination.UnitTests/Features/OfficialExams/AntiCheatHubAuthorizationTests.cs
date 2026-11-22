using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.API.Hubs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class AntiCheatHubAuthorizationTests
{
    private readonly Mock<IGroupManager> _groupManagerMock;

    public AntiCheatHubAuthorizationTests()
    {
        _groupManagerMock = new Mock<IGroupManager>();
        _groupManagerMock
            .Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _groupManagerMock
            .Setup(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private AntiCheatHub CreateHubWithCaller(OralExamDbContext context, Guid userId, string connectionId, string? role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        if (role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthScheme"));
        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        contextMock.Setup(c => c.User).Returns(principal);
        contextMock.Setup(c => c.ConnectionAborted).Returns(CancellationToken.None);

        return new AntiCheatHub(context)
        {
            Context = contextMock.Object,
            Groups = _groupManagerMock.Object
        };
    }

    private static async Task<(RealExamSessionShift Shift, User Proctor, User Examiner, User DeptHead, User Officer)> SeedShiftWithStaffAsync(
        OralExamDbContext context)
    {
        var proctor = new User { Id = Guid.NewGuid(), Email = "proctor@hub.test", FullName = "Proctor", Role = UserRole.Proctor, IsActive = true };
        var examiner = new User { Id = Guid.NewGuid(), Email = "examiner@hub.test", FullName = "Examiner", Role = UserRole.Lecturer, IsActive = true };
        var deptHead = new User { Id = Guid.NewGuid(), Email = "dh@hub.test", FullName = "Dept Head", Role = UserRole.DepartmentHead, IsActive = true };
        var officer = new User { Id = Guid.NewGuid(), Email = "eo@hub.test", FullName = "Officer", Role = UserRole.ExaminationOfficer, IsActive = true };

        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "NET", DepartmentHeadUserId = deptHead.Id, IsActive = true };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Title = "Hub Exam",
            ExamDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = "in_progress"
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca Hub",
            RoomLab = "Lab 101",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(50),
            ProctorUserId = proctor.Id,
            ExaminerUserId = examiner.Id,
            Status = "in_progress"
        };

        context.Users.AddRange(proctor, examiner, deptHead, officer);
        context.Courses.Add(course);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        await context.SaveChangesAsync();
        return (shift, proctor, examiner, deptHead, officer);
    }

    [Theory(DisplayName = "ANTICHEAT-HUB-01: Admin, Student và caller không có role bị chặn FORBIDDEN trên JoinShift")]
    [InlineData("admin")]
    [InlineData("student")]
    [InlineData(null)]
    public async Task JoinShift_UnauthorizedRoles_ThrowsForbidden_AndNeverAddsToGroup(string? role)
    {
        await using var context = CreateDbContext();
        var (shift, _, _, _, _) = await SeedShiftWithStaffAsync(context);
        var caller = new User
        {
            Id = Guid.NewGuid(),
            Email = $"caller.{role ?? "none"}@hub.test",
            FullName = "Caller",
            Role = role ?? UserRole.Student,
            IsActive = true
        };
        context.Users.Add(caller);
        await context.SaveChangesAsync();

        var hub = CreateHubWithCaller(context, caller.Id, $"conn-{role ?? "none"}", role);

        var act = async () => await hub.JoinShift(shift.Id.ToString());

        var ex = await act.Should().ThrowAsync<HubException>();
        ex.WithMessage("*FORBIDDEN*");

        _groupManagerMock.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory(DisplayName = "ANTICHEAT-HUB-02: Proctor, ExaminationOfficer, DepartmentHead và Lecturer phụ trách ca join shift thành công")]
    [InlineData("proctor")]
    [InlineData("examination_officer")]
    [InlineData("department_head")]
    [InlineData("lecturer")]
    public async Task JoinShift_AuthorizedRoles_AddsConnectionToShiftGroup(string role)
    {
        await using var context = CreateDbContext();
        var (shift, proctor, examiner, deptHead, officer) = await SeedShiftWithStaffAsync(context);
        var actorId = role switch
        {
            UserRole.Proctor => proctor.Id,
            UserRole.Lecturer => examiner.Id,
            UserRole.DepartmentHead => deptHead.Id,
            _ => officer.Id
        };

        var connId = $"conn-{role}";
        var hub = CreateHubWithCaller(context, actorId, connId, role);

        await hub.JoinShift(shift.Id.ToString());

        _groupManagerMock.Verify(
            g => g.AddToGroupAsync(connId, $"shift_{shift.Id:D}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory(DisplayName = "ANTICHEAT-HUB-03: ShiftId rỗng hoặc sai định dạng Guid ném BAD_REQUEST")]
    [InlineData("")]
    [InlineData("invalid-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task JoinShift_InvalidShiftId_ThrowsBadRequest(string invalidShiftId)
    {
        await using var context = CreateDbContext();
        var hub = CreateHubWithCaller(context, Guid.NewGuid(), "conn-proctor", "proctor");

        var act = async () => await hub.JoinShift(invalidShiftId);

        var ex = await act.Should().ThrowAsync<HubException>();
        ex.WithMessage("*BAD_REQUEST*");
    }

    [Fact(DisplayName = "ANTICHEAT-HUB-04: AntiCheatHub có [Authorize] và LeaveShift rời đúng group")]
    public async Task AntiCheatHub_HasAuthorize_And_LeaveShift_RemovesFromGroup()
    {
        typeof(AntiCheatHub).GetCustomAttribute<AuthorizeAttribute>(inherit: true).Should().NotBeNull();

        await using var context = CreateDbContext();
        var hub = CreateHubWithCaller(context, Guid.NewGuid(), "conn-leave", "proctor");
        var shiftId = Guid.NewGuid();

        await hub.LeaveShift(shiftId.ToString());

        _groupManagerMock.Verify(
            g => g.RemoveFromGroupAsync("conn-leave", $"shift_{shiftId:D}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "ANTICHEAT-HUB-05: Inactive user, non-existent shift, và proctor/lecturer/DH không phụ trách ca bị chặn fail-closed")]
    public async Task JoinShift_EnforcesDbIsActive_ShiftExistence_AndShiftOwnershipBola()
    {
        await using var context = CreateDbContext();
        var (shift, proctor, _, _, officer) = await SeedShiftWithStaffAsync(context);

        var otherProctor = new User { Id = Guid.NewGuid(), Email = "other.proctor@hub.test", FullName = "Other Proctor", Role = UserRole.Proctor, IsActive = true };
        var otherLecturer = new User { Id = Guid.NewGuid(), Email = "other.lecturer@hub.test", FullName = "Other Lecturer", Role = UserRole.Lecturer, IsActive = true };
        var otherDeptHead = new User { Id = Guid.NewGuid(), Email = "other.dh@hub.test", FullName = "Other DH", Role = UserRole.DepartmentHead, IsActive = true };
        context.Users.AddRange(otherProctor, otherLecturer, otherDeptHead);
        await context.SaveChangesAsync();

        // 1. Non-existent shift -> NOT_FOUND
        var officerHub = CreateHubWithCaller(context, officer.Id, "conn-eo", UserRole.ExaminationOfficer);
        (await FluentActions.Awaiting(() => officerHub.JoinShift(Guid.NewGuid().ToString()))
            .Should().ThrowAsync<HubException>()).WithMessage("*NOT_FOUND*");

        // 2. Unassigned proctor/lecturer/DH -> FORBIDDEN
        foreach (var (u, r) in new[] { (otherProctor, UserRole.Proctor), (otherLecturer, UserRole.Lecturer), (otherDeptHead, UserRole.DepartmentHead) })
        {
            var hub = CreateHubWithCaller(context, u.Id, $"conn-other-{r}", r);
            (await FluentActions.Awaiting(() => hub.JoinShift(shift.Id.ToString()))
                .Should().ThrowAsync<HubException>()).WithMessage("*FORBIDDEN*");
        }

        // 3. Inactive assigned proctor -> FORBIDDEN (DB IsActive verified!)
        proctor.IsActive = false;
        await context.SaveChangesAsync();
        var inactiveProctorHub = CreateHubWithCaller(context, proctor.Id, "conn-inactive", UserRole.Proctor);
        (await FluentActions.Awaiting(() => inactiveProctorHub.JoinShift(shift.Id.ToString()))
            .Should().ThrowAsync<HubException>()).WithMessage("*FORBIDDEN*");

        _groupManagerMock.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
