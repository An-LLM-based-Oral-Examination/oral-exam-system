using System;
using System.Threading.Tasks;
using OralExamination.Application.Features.Practice.DTOs;

namespace OralExamination.Application.Common.Interfaces;

public interface IPracticeClient
{
    // Nhận thông báo điểm realtime
    Task ReceiveGradingResult(PracticeAnswerDetailDto result);
    // Nhận thông báo lỗi nếu chấm tạch
    Task ReceiveGradingError(Guid answerId, string error);
}
