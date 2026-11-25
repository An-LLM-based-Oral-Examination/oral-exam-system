namespace OralExamination.Application.Features.OfficialExams.DTOs;

public sealed record RequestExamPreparation(int RequiredSetCount, DateTime Deadline, string? Notes = null);
public sealed record ConfigureExamBlueprint(string Name, int TotalQuestions, int DurationMinutes,
    int EasyCount, int MediumCount, int HardCount, List<Guid> CloIds);
public sealed record AssignExamAuthor(Guid StructureId, Guid LecturerId, int SetCount, DateTime Deadline, string? Notes = null);
public sealed record SaveExamSetDraft(List<Guid> QuestionIds);
public sealed record ReviewExamSet(string Notes);
public sealed record HandOverExamPreparation(List<Guid> ExamSetIds);
public sealed record DuplicateExamQuestion(Guid QuestionId, string SetCode, Guid SessionId, string SessionTitle, string SemesterCode, string QuestionTitle);

public sealed record SubmitExamAssignment(List<Guid> ExamSetIds);
