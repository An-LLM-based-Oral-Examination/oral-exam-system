using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.API.Hubs;

public interface IAntiCheatClient
{
    Task ReceiveAntiCheatAlert(ExamAntiCheatLogDto alert);
}

[Authorize]
public class AntiCheatHub : Hub<IAntiCheatClient>
{
    /// <summary>
    /// Cho phép Giám thị, Cán bộ khảo thí và Trưởng bộ môn tham gia nhóm theo dõi ca thi realtime.
    /// </summary>
    public async Task JoinShift(string shiftId)
    {
        if (!Guid.TryParse(shiftId, out var parsedShiftId) || parsedShiftId == Guid.Empty)
        {
            throw new HubException("BAD_REQUEST: Mã ca thi không hợp lệ.");
        }

        var isAuthorized = Context?.User?.IsInRole("admin") == true
            || Context?.User?.IsInRole("examination_officer") == true
            || Context?.User?.IsInRole("department_head") == true
            || Context?.User?.IsInRole("proctor") == true
            || Context?.User?.IsInRole("lecturer") == true;

        if (!isAuthorized)
        {
            throw new HubException("FORBIDDEN: Bạn không có quyền theo dõi vi phạm an ninh phòng thi.");
        }

        await Groups.AddToGroupAsync(Context!.ConnectionId, $"shift_{parsedShiftId:D}");
    }

    /// <summary>
    /// Rời nhóm theo dõi ca thi.
    /// </summary>
    public async Task LeaveShift(string shiftId)
    {
        var groupName = Guid.TryParse(shiftId, out var parsedShiftId)
            ? $"shift_{parsedShiftId:D}"
            : $"shift_{shiftId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}
