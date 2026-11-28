using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;

namespace OralExamination.API.Hubs;

[Authorize(Roles = "student")]
public sealed class PracticeHub(IApplicationDbContext context) : Hub<IPracticeClient>
{
    /// <summary>
    /// Cho phép Sinh viên tham gia nhóm phiên luyện tập của chính mình để nhận kết quả chấm điểm realtime.
    /// Khóa cứng BOLA: Chỉ cho phép role student và bắt buộc session.StudentId == currentUserId.
    /// </summary>
    public async Task JoinSession(string sessionId)
    {
        if (!Guid.TryParse(sessionId, out var parsedSessionId) || parsedSessionId == Guid.Empty)
        {
            throw new HubException("BAD_REQUEST: Mã phiên luyện tập không hợp lệ.");
        }

        if (Context?.User?.IsInRole("student") != true)
        {
            throw new HubException("FORBIDDEN: Chỉ sinh viên mới có quyền tham gia phiên luyện tập.");
        }

        var userIdStr = Context?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context?.User?.FindFirstValue("sub");
        if (!Guid.TryParse(userIdStr, out var currentUserId) || currentUserId == Guid.Empty)
        {
            throw new HubException("FORBIDDEN: Không xác định được danh tính sinh viên.");
        }

        var tokenAuthVersionRaw = Context?.User?.FindFirstValue("auth_version");
        int? tokenAuthVersion = null;
        if (!string.IsNullOrWhiteSpace(tokenAuthVersionRaw))
        {
            if (!int.TryParse(tokenAuthVersionRaw, out var parsedVer))
            {
                throw new HubException("FORBIDDEN: Phiên đăng nhập không hợp lệ (auth_version).");
            }
            tokenAuthVersion = parsedVer;
        }

        var isActiveStudent = await context.Users
            .AsNoTracking()
            .AnyAsync(
                u => u.Id == currentUserId
                     && u.IsActive
                     && !u.MustChangePassword
                     && u.Role == "student"
                     && (!tokenAuthVersion.HasValue || u.AuthVersion == tokenAuthVersion.Value),
                Context!.ConnectionAborted);

        if (!isActiveStudent)
        {
            throw new HubException("FORBIDDEN: Tài khoản sinh viên đã bị khóa, chưa đổi mật khẩu hoặc phiên đã hết hiệu lực.");
        }

        var session = await context.PracticeSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == parsedSessionId, Context!.ConnectionAborted);

        if (session is null)
        {
            throw new HubException("FORBIDDEN: Phiên luyện tập không tồn tại hoặc không thuộc về sinh viên này (NOT_FOUND).");
        }

        if (session.StudentId != currentUserId)
        {
            throw new HubException("FORBIDDEN: Bạn không có quyền tham gia phiên luyện tập của sinh viên khác.");
        }

        await Groups.AddToGroupAsync(Context!.ConnectionId, $"session_{parsedSessionId:D}", Context!.ConnectionAborted);
    }

    /// <summary>
    /// Cho phép Sinh viên rời nhóm phiên luyện tập khi kết thúc hoặc chuyển trang.
    /// </summary>
    public async Task LeaveSession(string sessionId)
    {
        var groupName = Guid.TryParse(sessionId, out var parsedSessionId)
            ? $"session_{parsedSessionId:D}"
            : $"session_{sessionId}";
        await Groups.RemoveFromGroupAsync(Context!.ConnectionId, groupName, Context!.ConnectionAborted);
    }
}
