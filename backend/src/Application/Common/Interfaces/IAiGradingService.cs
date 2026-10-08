using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OralExamination.Application.Common.Interfaces;

public class AiGradingCriterion
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MaxScore { get; set; }
    public decimal Weight { get; set; }
}

public class AiGradingRequest
{
    public string Transcript { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public string SampleAnswer { get; set; } = string.Empty;
    public List<AiGradingCriterion> Criteria { get; set; } = new();
}

public class AiGradingResult
{
    public decimal Score { get; set; }
    public decimal ConfidenceScore { get; set; }
    public bool IsSuspicious { get; set; }
    public string SuspiciousReason { get; set; } = string.Empty;
    public string CotTrace { get; set; } = string.Empty;
    public string Feedback { get; set; } = string.Empty;
    public List<GradingDetailDto> Details { get; set; } = new();
}

public class GradingDetailDto
{
    public string CriterionId { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public interface IAiGradingService
{
    Task<AiGradingResult> GradeAnswerAsync(AiGradingRequest request, CancellationToken cancellationToken);
}
