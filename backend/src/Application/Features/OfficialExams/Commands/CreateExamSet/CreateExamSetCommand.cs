using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.CreateExamSet;

public sealed record CreateExamSetCommand(
    Guid StructureId,
    Guid CourseId,
    Guid? SessionId,
    string SetCode,
    List<Guid> QuestionIds,
    Guid ActorUserId,
    string? ActorRole = null,
    Guid? AssignmentId = null,
    bool IsSampleSet = false,
    List<CreateInlineExamQuestionDto>? InlineManualQuestions = null) : IRequest<Result<ExamSetDto>>
{
    public CreateExamSetCommand(Guid structureId, Guid courseId, Guid? sessionId, string setCode, List<Guid> questionIds,
        Guid actorUserId, string? actorRole, bool isSampleSet, List<CreateInlineExamQuestionDto>? inlineManualQuestions = null)
        : this(structureId, courseId, sessionId, setCode, questionIds, actorUserId, actorRole, null, isSampleSet, inlineManualQuestions) { }
}
