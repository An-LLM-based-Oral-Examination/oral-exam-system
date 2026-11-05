using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;

namespace OralExamination.API.Hubs;

[Authorize]
public class PracticeHub : Hub<IPracticeClient>
{
    private readonly IApplicationDbContext? _context;

    public PracticeHub(IApplicationDbContext? context = null)
    {
        _context = context;
    }

    /// <summary>
    /// Cho phép Frontend tham gia nhóm phiên luyện tập để nhận kết quả chấm điểm realtime.
    /// </summary>
    public async Task JoinSession(string sessionId)
    {
        if (!Guid.TryParse(sessionId, out var parsedSessionId) || parsedSessionId == Guid.Empty)
        {
            throw new HubException("BAD_REQUEST: Mã phiên luyện tập không hợp lệ.");
        }

        var userIdStr = Context?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Context?.User?.FindFirstValue("sub");
        var hasUserId = Guid.TryParse(userIdStr, out var currentUserId) && currentUserId != Guid.Empty;
        var isElevatedRole = Context?.User?.IsInRole("admin") == true
            || Context?.User?.IsInRole("department_head") == true
            || Context?.User?.IsInRole("lecturer") == true;

        if (!hasUserId && !isElevatedRole)
        {
            throw new HubException("UNAUTHORIZED: Không xác định được danh tính người dùng.");
        }

        if (_context != null)
        {
            var session = await _context.PracticeSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == parsedSessionId);

            if (session != null)
            {
                if (!isElevatedRole && session.StudentId != currentUserId)
                {
                    throw new HubException("FORBIDDEN: Bạn không có quyền tham gia phiên luyện tập của sinh viên khác.");
                }
            }
            else
            {
                var inClassShift = await _context.InClassExamShifts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == parsedSessionId);

                if (inClassShift == null)
                {
                    throw new HubException("NOT_FOUND: Phiên luyện tập hoặc ca thi trong lớp không tồn tại.");
                }

                if (!isElevatedRole)
                {
                    var isParticipantOrEnrolled = await _context.InClassShiftParticipants
                        .AsNoTracking()
                        .AnyAsync(p => p.ShiftId == parsedSessionId && p.StudentId == currentUserId)
                        || await _context.ClassEnrollments
                            .AsNoTracking()
                            .AnyAsync(e => e.ClassId == inClassShift.ClassId && e.StudentId == currentUserId);

                    if (!isParticipantOrEnrolled)
                    {
                        throw new HubException("FORBIDDEN: Bạn không thuộc lớp học phần hoặc ca thi trong lớp này.");
                    }
                }
            }
        }

        await Groups.AddToGroupAsync(Context!.ConnectionId, $"session_{parsedSessionId:D}");
    }

    /// <summary>
    /// Alias cho phép Frontend tham gia nhóm ca thi trong lớp (MF-05) để nhận điểm realtime.
    /// </summary>
    public Task JoinShift(string shiftId) => JoinSession(shiftId);

    /// <summary>
    /// Cho phép Frontend rời nhóm phiên luyện tập khi kết thúc hoặc chuyển trang.
    /// </summary>
    public async Task LeaveSession(string sessionId)
    {
        var groupName = Guid.TryParse(sessionId, out var parsedSessionId)
            ? $"session_{parsedSessionId:D}"
            : $"session_{sessionId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Alias cho phép Frontend rời nhóm ca thi trong lớp (MF-05).
    /// </summary>
    public Task LeaveShift(string shiftId) => LeaveSession(shiftId);
}
