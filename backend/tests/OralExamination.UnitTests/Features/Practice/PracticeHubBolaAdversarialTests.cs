using System;
using System.Collections.Generic;
using System.Linq;
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
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Practice;

/// <summary>
/// Adversarial unit test suite for PracticeHub BOLA (Broken Object Level Authorization) hardening.
/// Verifies student-only role enforcement, session ownership validation, fail-closed anti-enumeration,
/// claim parsing variations (NameIdentifier / sub), and group lifecycle.
/// </summary>
public sealed class PracticeHubBolaAdversarialTests : IDisposable
{
    private readonly OralExamDbContext _dbContext;
    private readonly Mock<IGroupManager> _groupManagerMock;

    public PracticeHubBolaAdversarialTests()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: $"PracticeHubBola_{Guid.NewGuid():N}")
            .Options;

        _dbContext = new OralExamDbContext(options);
        _groupManagerMock = new Mock<IGroupManager>();

        _groupManagerMock
            .Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _groupManagerMock
            .Setup(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private PracticeHub CreateHubWithCaller(
        string connectionId,
        Guid? userId,
        string? role,
        bool useSubClaim = false,
        string? customUserIdClaimValue = null)
    {
        var claims = new List<Claim>();

        if (role != null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (customUserIdClaimValue != null)
        {
            claims.Add(useSubClaim
                ? new Claim("sub", customUserIdClaimValue)
                : new Claim(ClaimTypes.NameIdentifier, customUserIdClaimValue));
        }
        else if (userId.HasValue)
        {
            claims.Add(useSubClaim
                ? new Claim("sub", userId.Value.ToString())
                : new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, "TestAuthScheme");
        var principal = new ClaimsPrincipal(identity);

        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        contextMock.Setup(c => c.User).Returns(principal);
        contextMock.Setup(c => c.ConnectionAborted).Returns(CancellationToken.None);

        return new PracticeHub(_dbContext)
        {
            Context = contextMock.Object,
            Groups = _groupManagerMock.Object
        };
    }

    private async Task<PracticeSession> SeedPracticeSessionAsync(Guid studentId)
    {
        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = Guid.NewGuid(),
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };

        _dbContext.PracticeSessions.Add(session);
        await _dbContext.SaveChangesAsync();
        return session;
    }

    #region 1. Cross-Student Ownership BOLA Tests

    [Fact(DisplayName = "BOLA-01: Sinh viên B cố tình join phiên luyện tập của Sinh viên A -> Phải ném FORBIDDEN và không thêm vào group")]
    public async Task JoinSession_CrossStudentAccess_Throws_FORBIDDEN()
    {
        // Arrange
        var ownerStudentId = Guid.NewGuid();
        var attackerStudentId = Guid.NewGuid();
        var session = await SeedPracticeSessionAsync(ownerStudentId);

        var hub = CreateHubWithCaller("conn-attacker", attackerStudentId, "student");

        // Act
        var act = async () => await hub.JoinSession(session.Id.ToString());

        // Assert
        var exception = await act.Should().ThrowAsync<HubException>();
        exception.WithMessage("*FORBIDDEN*Bạn không có quyền tham gia phiên luyện tập của sinh viên khác*");

        _groupManagerMock.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Kẻ tấn công không được thêm vào group SignalR của nạn nhân.");
    }

    #endregion

    #region 2. Elevated Roles BOLA Bypass Elimination Tests

    [Theory(DisplayName = "BOLA-02: Các vai trò đặc quyền (Admin, Giảng viên, Trưởng BM, Khảo thí, Giám thị) cố ý join phiên -> Phải bị từ chối FORBIDDEN")]
    [InlineData("admin")]
    [InlineData("lecturer")]
    [InlineData("department_head")]
    [InlineData("examination_officer")]
    [InlineData("proctor")]
    public async Task JoinSession_ElevatedRoles_Throws_FORBIDDEN(string elevatedRole)
    {
        // Arrange
        var ownerStudentId = Guid.NewGuid();
        var elevatedUserId = Guid.NewGuid();
        var session = await SeedPracticeSessionAsync(ownerStudentId);

        var hub = CreateHubWithCaller($"conn-{elevatedRole}", elevatedUserId, elevatedRole);

        // Act
        var act = async () => await hub.JoinSession(session.Id.ToString());

        // Assert
        var exception = await act.Should().ThrowAsync<HubException>();
        exception.WithMessage("*FORBIDDEN*Chỉ sinh viên mới có quyền tham gia phiên luyện tập*");

        _groupManagerMock.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            $"Vai trò {elevatedRole} không được phép nghe lén phiên luyện tập của sinh viên.");
    }

    #endregion

    #region 3. Role and Authentication State Tests

    [Fact(DisplayName = "BOLA-03: Người dùng không có role hoặc role không phải student -> Phải bị từ chối FORBIDDEN")]
    public async Task JoinSession_NonStudentRole_Throws_FORBIDDEN()
    {
        // Arrange
        var ownerStudentId = Guid.NewGuid();
        var session = await SeedPracticeSessionAsync(ownerStudentId);

        var hub = CreateHubWithCaller("conn-guest", Guid.NewGuid(), "guest");

        // Act
        var act = async () => await hub.JoinSession(session.Id.ToString());

        // Assert
        var exception = await act.Should().ThrowAsync<HubException>();
        exception.WithMessage("*FORBIDDEN*Chỉ sinh viên mới có quyền tham gia phiên luyện tập*");

        _groupManagerMock.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName = "BOLA-04: Người dùng không có bất kỳ role claim nào -> Phải bị từ chối FORBIDDEN")]
    public async Task JoinSession_NoRoleClaim_Throws_FORBIDDEN()
    {
        // Arrange
        var ownerStudentId = Guid.NewGuid();
        var session = await SeedPracticeSessionAsync(ownerStudentId);

        var hub = CreateHubWithCaller("conn-norole", Guid.NewGuid(), role: null);

        // Act
        var act = async () => await hub.JoinSession(session.Id.ToString());

        // Assert
        var exception = await act.Should().ThrowAsync<HubException>();
        exception.WithMessage("*FORBIDDEN*Chỉ sinh viên mới có quyền tham gia phiên luyện tập*");

        _groupManagerMock.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region 4. User Identity Extraction and Tampering Tests

    [Fact(DisplayName = "BOLA-05: Caller có role student nhưng thiếu claim NameIdentifier và sub -> Phải ném FORBIDDEN")]
    public async Task JoinSession_MissingUserIdClaims_Throws_FORBIDDEN()
    {
        // Arrange
        var ownerStudentId = Guid.NewGuid();
        var session = await SeedPracticeSessionAsync(ownerStudentId);

        var hub = CreateHubWithCaller("conn-no-id", userId: null, role: "student");

        // Act
        var act = async () => await hub.JoinSession(session.Id.ToString());

        // Assert
        var exception = await act.Should().ThrowAsync<HubException>();
        exception.WithMessage("*FORBIDDEN*Không xác định được danh tính sinh viên*");

        _groupManagerMock.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory(DisplayName = "BOLA-06: Caller có claim userId định dạng sai hoặc Guid rỗng -> Phải ném FORBIDDEN")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("not-a-valid-guid")]
    [InlineData("   ")]
    public async Task JoinSession_InvalidUserIdClaimValue_Throws_FORBIDDEN(string invalidUserIdClaim)
    {
        // Arrange
        var ownerStudentId = Guid.NewGuid();
        var session = await SeedPracticeSessionAsync(ownerStudentId);

        var hub = CreateHubWithCaller(
            "conn-invalid-id",
            userId: null,
            role: "student",
            customUserIdClaimValue: invalidUserIdClaim);

        // Act
        var act = async () => await hub.JoinSession(session.Id.ToString());

        // Assert
        var exception = await act.Should().ThrowAsync<HubException>();
        exception.WithMessage("*FORBIDDEN*Không xác định được danh tính sinh viên*");

        _groupManagerMock.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region 5. Session ID Validation & Anti-Enumeration Tests

    [Theory(DisplayName = "BOLA-07: Truyền mã sessionId sai định dạng hoặc Guid rỗng -> Phải ném BAD_REQUEST")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("")]
    [InlineData("    ")]
    public async Task JoinSession_InvalidSessionId_Throws_BAD_REQUEST(string invalidSessionId)
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var hub = CreateHubWithCaller("conn-1", studentId, "student");

        // Act
        var act = async () => await hub.JoinSession(invalidSessionId);

        // Assert
        var exception = await act.Should().ThrowAsync<HubException>();
        exception.WithMessage("*BAD_REQUEST*Mã phiên luyện tập không hợp lệ*");

        _groupManagerMock.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName = "BOLA-08: Phiên luyện tập không tồn tại -> Phải ném FORBIDDEN (fail-closed, chống rò rỉ ID enumeration)")]
    public async Task JoinSession_NonExistentSession_Throws_FORBIDDEN()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var nonExistentSessionId = Guid.NewGuid();
        var hub = CreateHubWithCaller("conn-1", studentId, "student");

        // Act
        var act = async () => await hub.JoinSession(nonExistentSessionId.ToString());

        // Assert
        var exception = await act.Should().ThrowAsync<HubException>();
        exception.WithMessage("*FORBIDDEN*Phiên luyện tập không tồn tại hoặc không thuộc về sinh viên này*");

        _groupManagerMock.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Phiên không tồn tại tuyệt đối không được thêm vào group SignalR.");
    }

    #endregion

    #region 6. Authorized Owner Success Path Tests

    [Fact(DisplayName = "BOLA-09: Sinh viên chính chủ dùng NameIdentifier join phiên của mình -> Thành công thêm vào group session_{id}")]
    public async Task JoinSession_AuthorizedOwner_WithNameIdentifier_Succeeds()
    {
        // Arrange
        var ownerStudentId = Guid.NewGuid();
        var session = await SeedPracticeSessionAsync(ownerStudentId);

        var hub = CreateHubWithCaller("conn-owner-nameid", ownerStudentId, "student", useSubClaim: false);

        // Act
        await hub.JoinSession(session.Id.ToString());

        // Assert
        _groupManagerMock.Verify(
            g => g.AddToGroupAsync("conn-owner-nameid", $"session_{session.Id:D}", It.IsAny<CancellationToken>()),
            Times.Once,
            "Chính chủ phiên phải được tham gia vào đúng group SignalR tương ứng.");
    }

    [Fact(DisplayName = "BOLA-10: Sinh viên chính chủ dùng claim sub (OAuth/JWT standard) join phiên của mình -> Thành công")]
    public async Task JoinSession_AuthorizedOwner_WithSubClaim_Succeeds()
    {
        // Arrange
        var ownerStudentId = Guid.NewGuid();
        var session = await SeedPracticeSessionAsync(ownerStudentId);

        var hub = CreateHubWithCaller("conn-owner-sub", ownerStudentId, "student", useSubClaim: true);

        // Act
        await hub.JoinSession(session.Id.ToString());

        // Assert
        _groupManagerMock.Verify(
            g => g.AddToGroupAsync("conn-owner-sub", $"session_{session.Id:D}", It.IsAny<CancellationToken>()),
            Times.Once,
            "Chính chủ định danh qua claim 'sub' phải được kết nối thành công.");
    }

    #endregion

    #region 7. Class-Level Security & Architecture Reflection Tests

    [Fact(DisplayName = "BOLA-11: Lớp PracticeHub phải được đánh dấu thuộc tính [Authorize(Roles = 'student')]")]
    public void PracticeHub_Class_Has_Authorize_Student_Attribute()
    {
        // Arrange
        var hubType = typeof(PracticeHub);

        // Act
        var authAttr = hubType.GetCustomAttribute<AuthorizeAttribute>(inherit: true);

        // Assert
        authAttr.Should().NotBeNull("PracticeHub bắt buộc phải có thuộc tính [Authorize] ở cấp độ class.");
        authAttr!.Roles.Should().Be("student", "PracticeHub bắt buộc khóa cứng chỉ cho phép duy nhất vai trò 'student'.");
    }

    [Fact(DisplayName = "BOLA-12: Lớp PracticeHub phải là sealed class và kế thừa từ Hub<IPracticeClient>")]
    public void PracticeHub_Class_MustBeSealed_And_Inherit_HubClientInterface()
    {
        // Arrange
        var hubType = typeof(PracticeHub);

        // Assert
        hubType.IsSealed.Should().BeTrue("PracticeHub phải là sealed class để tối ưu hóa hiệu năng và bảo mật.");
        typeof(Hub<IPracticeClient>).IsAssignableFrom(hubType)
            .Should().BeTrue("PracticeHub phải kế thừa từ Hub<IPracticeClient>.");
    }

    #endregion

    #region 8. LeaveSession Group Removal Tests

    [Fact(DisplayName = "BOLA-13: LeaveSession với GUID hợp lệ -> Rời khỏi group đúng định dạng session_{id:D}")]
    public async Task LeaveSession_ValidGuid_RemovesFromGroup()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var hub = CreateHubWithCaller("conn-leave", studentId, "student");

        // Act
        await hub.LeaveSession(sessionId.ToString());

        // Assert
        _groupManagerMock.Verify(
            g => g.RemoveFromGroupAsync("conn-leave", $"session_{sessionId:D}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "BOLA-14: LeaveSession với chuỗi thô -> Rời khỏi group session_{rawString}")]
    public async Task LeaveSession_RawString_RemovesFromStringGroup()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var hub = CreateHubWithCaller("conn-leave-raw", studentId, "student");

        // Act
        await hub.LeaveSession("legacy-session-key");

        // Assert
        _groupManagerMock.Verify(
            g => g.RemoveFromGroupAsync("conn-leave-raw", "session_legacy-session-key", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
