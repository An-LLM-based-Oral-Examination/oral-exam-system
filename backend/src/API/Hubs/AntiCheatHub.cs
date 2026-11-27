using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.API.Hubs;

public interface IAntiCheatClient
{
    Task ReceiveAntiCheatAlert(ExamAntiCheatLogDto alert);
}

[Authorize]
public class AntiCheatHub(IApplicationDbContext context) : Hub<IAntiCheatClient>
{
    /// <summary>
    /// Cho phép Giám thị, Giám khảo, Trưởng bộ môn phụ trách môn hoặc Cán bộ khảo thí đang hoạt động tham gia nhóm theo dõi ca thi realtime.
    /// </summary>
    public async Task JoinShift(string shiftId)
    {
        if (!Guid.TryParse(shiftId, out var parsedShiftId) || parsedShiftId == Guid.Empty)
        {
            throw new HubException("BAD_REQUEST: Mã ca thi không hợp lệ.");
        }

        var userIdValue = Context?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context?.User?.FindFirstValue("sub");
        if (!Guid.TryParse(userIdValue, out var userId) || userId == Guid.Empty)
        {
            throw new HubException("UNAUTHORIZED: Không xác định được danh tính người dùng.");
        }

        var hasAllowedRoleClaim = Context?.User?.IsInRole(UserRole.ExaminationOfficer) == true
            || Context?.User?.IsInRole(UserRole.DepartmentHead) == true
            || Context?.User?.IsInRole(UserRole.Proctor) == true
            || Context?.User?.IsInRole(UserRole.Lecturer) == true;

        if (!hasAllowedRoleClaim)
        {
            throw new HubException("FORBIDDEN: Bạn không có quyền theo dõi vi phạm an ninh phòng thi.");
        }

        var ct = Context?.ConnectionAborted ?? default;

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

        var actor = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.IsActive, u.MustChangePassword, u.Role, u.AuthVersion })
            .FirstOrDefaultAsync(ct);

        if (actor is null
            || !actor.IsActive
            || actor.MustChangePassword
            || Context?.User?.IsInRole(actor.Role) != true
            || (tokenAuthVersion.HasValue && actor.AuthVersion != tokenAuthVersion.Value))
        {
            throw new HubException("FORBIDDEN: Tài khoản đã bị khóa, chưa đổi mật khẩu hoặc thay đổi quyền truy cập.");
        }

        var shift = await context.RealExamSessionShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == parsedShiftId, ct);

        if (shift is null)
        {
            throw new HubException("NOT_FOUND: Ca thi không tồn tại.");
        }

        var canMonitor = await OfficialExamAuthorization.CanMonitorShiftAntiCheatAsync(
            context,
            actor.Id,
            shift,
            ct);

        if (!canMonitor)
        {
            throw new HubException("FORBIDDEN: Bạn không phụ trách ca thi phòng Lab này.");
        }

        await Groups.AddToGroupAsync(Context!.ConnectionId, $"shift_{parsedShiftId:D}", ct);
    }

    /// <summary>
    /// Rời nhóm theo dõi ca thi.
    /// </summary>
    public async Task LeaveShift(string shiftId)
    {
        var groupName = Guid.TryParse(shiftId, out var parsedShiftId)
            ? $"shift_{parsedShiftId:D}"
            : $"shift_{shiftId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName, Context.ConnectionAborted);
    }
}
