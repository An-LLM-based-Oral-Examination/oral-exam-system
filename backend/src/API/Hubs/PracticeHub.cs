using Microsoft.AspNetCore.SignalR;
using OralExamination.Application.Common.Interfaces;

namespace OralExamination.API.Hubs;

public class PracticeHub : Hub<IPracticeClient>
{
    /// <summary>
    /// Cho phép Frontend tham gia nhóm phiên luyện tập để nhận kết quả chấm điểm realtime.
    /// </summary>
    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"session_{sessionId}");
    }

    /// <summary>
    /// Cho phép Frontend rời nhóm phiên luyện tập khi kết thúc hoặc chuyển trang.
    /// </summary>
    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session_{sessionId}");
    }
}
