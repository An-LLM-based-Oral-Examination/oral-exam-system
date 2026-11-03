using System.Threading;
using System.Threading.Tasks;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Common.Interfaces;

public interface IAntiCheatNotifier
{
    Task NotifyAntiCheatEventAsync(ExamAntiCheatLogDto logDto, CancellationToken ct = default);
}
