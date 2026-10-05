using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OralExamination.Application.Common.Interfaces;

/// <summary>
/// Yêu cầu chấm điểm câu trả lời vấn đáp của sinh viên bằng AI (Gemini).
/// BẮT BUỘC: Đầu vào chỉ nhận bản transcript text, TUYỆT ĐỐI KHÔNG nhận file âm thanh thô.
/// </summary>
public sealed class AiGradingRequest
{
    public string Transcript { get; set; } = null!;
    public string QuestionText { get; set; } = null!;
    public string? ModelAnswer { get; set; }
    public List<AiGradingRubricCriterionDto> RubricCriteria { get; set; } = new();
}

public sealed class AiGradingRubricCriterionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public decimal MaxScore { get; set; }
}

/// <summary>
/// Kết quả chấm điểm trả về từ AI Engine.
/// Bao gồm điểm số, nhận xét, độ tin cậy và chuỗi suy luận Chain-of-Thought (CoT).
/// </summary>
public sealed class AiGradingResult
{
    public decimal Score { get; set; }
    public string Feedback { get; set; } = null!;
    public decimal ConfidenceScore { get; set; }
    public bool IsSuspicious { get; set; }
    public string? SuspiciousReason { get; set; }
    public string? CotTrace { get; set; }
    public List<AiGradingCriterionDetailResult> Details { get; set; } = new();
}

public sealed class AiGradingCriterionDetailResult
{
    public Guid CriterionId { get; set; }
    public decimal Score { get; set; }
    public string Comment { get; set; } = null!;
}

/// <summary>
/// Hợp đồng Service chấm điểm vấn đáp bằng AI (Gemini Chain-of-Thought Grading Engine).
/// </summary>
public interface IAiGradingService
{
    /// <summary>
    /// Chấm điểm câu trả lời vấn đáp của thí sinh dựa trên văn bản transcript và barem tiêu chí.
    /// </summary>
    /// <param name="request">Bản ghi chứa transcript, câu hỏi, model answer và danh sách tiêu chí barem</param>
    /// <param name="cancellationToken">Cancellation Token</param>
    /// <returns>Kết quả chấm điểm chi tiết kèm CoT trace và cờ nghi ngờ nếu độ tin cậy thấp</returns>
    Task<AiGradingResult> GradeAnswerAsync(AiGradingRequest request, CancellationToken cancellationToken = default);
}
