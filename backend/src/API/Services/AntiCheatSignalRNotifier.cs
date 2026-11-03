using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using OralExamination.API.Hubs;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.API.Services;

public sealed class AntiCheatSignalRNotifier(IHubContext<AntiCheatHub, IAntiCheatClient> hubContext)
    : IAntiCheatNotifier
{
    public async Task NotifyAntiCheatEventAsync(ExamAntiCheatLogDto logDto, CancellationToken ct = default)
    {
        var groupName = $"shift_{logDto.ShiftId:D}";
        await hubContext.Clients.Group(groupName).ReceiveAntiCheatAlert(logDto);
    }
}
