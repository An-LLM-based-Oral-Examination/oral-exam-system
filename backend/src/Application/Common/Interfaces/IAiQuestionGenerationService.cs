using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OralExamination.Application.Features.QuestionBank.DTOs;

namespace OralExamination.Application.Common.Interfaces;

public interface IAiQuestionGenerationService
{
    Task<List<GeneratedQuestionDraftDto>> GenerateQuestionsAsync(
        AiQuestionGenerationParams parameters,
        CancellationToken cancellationToken = default);
}

public sealed record AiQuestionGenerationParams(
    string CourseCode,
    string CourseName,
    List<string> SelectedCloCodes,
    List<string> Topics,
    int NumberOfQuestions,
    string UsageScope,
    List<string> TargetBloomLevels,
    int EasyCount,
    int MediumCount,
    int HardCount
);
