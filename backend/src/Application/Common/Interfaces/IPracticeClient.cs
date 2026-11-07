using System;
using System.Threading.Tasks;
using OralExamination.Application.Features.Practice.DTOs;

namespace OralExamination.Application.Common.Interfaces;

public interface IPracticeClient
{
    // Nhận thông báo điểm realtime (MF-01 & MF-05)
    Task ReceiveGradingResult(PracticeAnswerDetailDto result);
    // Nhận sự kiện chấm điểm ca thi trong lớp MF-05 theo đặc tả tích hợp FE
    Task InClassSubmissionGraded(object payload);
    // Nhận thông báo lỗi nếu chấm tạch
    Task ReceiveGradingError(Guid answerId, string error);
}
