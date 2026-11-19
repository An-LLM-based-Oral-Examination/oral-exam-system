using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Moq;
using OralExamination.API.Hubs;
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

    private AntiCheatHub CreateHubWithCaller(string connectionId, string? role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
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

        return new AntiCheatHub
        {
            Context = contextMock.Object,
            Groups = _groupManagerMock.Object
        };
    }

    [Theory(DisplayName = "ANTICHEAT-HUB-01: Admin, Student và caller không có role bị chặn FORBIDDEN trên JoinShift")]
    [InlineData("admin")]
    [InlineData("student")]
    [InlineData(null)]
    public async Task JoinShift_UnauthorizedRoles_ThrowsForbidden_AndNeverAddsToGroup(string? role)
    {
        var hub = CreateHubWithCaller($"conn-{role ?? "none"}", role);
        var shiftId = Guid.NewGuid();

        var act = async () => await hub.JoinShift(shiftId.ToString());

        var ex = await act.Should().ThrowAsync<HubException>();
        ex.WithMessage("*FORBIDDEN*");

        _groupManagerMock.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory(DisplayName = "ANTICHEAT-HUB-02: Proctor, ExaminationOfficer, DepartmentHead và Lecturer join shift thành công")]
    [InlineData("proctor")]
    [InlineData("examination_officer")]
    [InlineData("department_head")]
    [InlineData("lecturer")]
    public async Task JoinShift_AuthorizedRoles_AddsConnectionToShiftGroup(string role)
    {
        var connId = $"conn-{role}";
        var hub = CreateHubWithCaller(connId, role);
        var shiftId = Guid.NewGuid();

        await hub.JoinShift(shiftId.ToString());

        _groupManagerMock.Verify(
            g => g.AddToGroupAsync(connId, $"shift_{shiftId:D}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory(DisplayName = "ANTICHEAT-HUB-03: ShiftId rỗng hoặc sai định dạng Guid ném BAD_REQUEST")]
    [InlineData("")]
    [InlineData("invalid-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task JoinShift_InvalidShiftId_ThrowsBadRequest(string invalidShiftId)
    {
        var hub = CreateHubWithCaller("conn-proctor", "proctor");

        var act = async () => await hub.JoinShift(invalidShiftId);

        var ex = await act.Should().ThrowAsync<HubException>();
        ex.WithMessage("*BAD_REQUEST*");
    }

    [Fact(DisplayName = "ANTICHEAT-HUB-04: AntiCheatHub có [Authorize] và LeaveShift rời đúng group")]
    public async Task AntiCheatHub_HasAuthorize_And_LeaveShift_RemovesFromGroup()
    {
        typeof(AntiCheatHub).GetCustomAttribute<AuthorizeAttribute>(inherit: true).Should().NotBeNull();

        var hub = CreateHubWithCaller("conn-leave", "proctor");
        var shiftId = Guid.NewGuid();

        await hub.LeaveShift(shiftId.ToString());

        _groupManagerMock.Verify(
            g => g.RemoveFromGroupAsync("conn-leave", $"shift_{shiftId:D}", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
