using System.Collections.Generic;

namespace OralExamination.Application.Common.Interfaces;

/// <summary>Supplementary teaching scope; database CLO descriptions remain authoritative.</summary>
public interface IQuestionGenerationGuidanceProvider
{
    AiQuestionGenerationGuidance? Get(string courseCode, IEnumerable<string> selectedCloCodes);
}

public sealed record AiQuestionGenerationGuidance(
    string CourseScope, IReadOnlyList<AiCloGenerationGuidance> Clos);

public sealed record AiCloGenerationGuidance(string Code, string Topics, string Boundaries);
