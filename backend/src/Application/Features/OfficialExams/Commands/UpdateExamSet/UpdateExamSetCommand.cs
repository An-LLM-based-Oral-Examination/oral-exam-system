using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.UpdateExamSet;

public sealed record UpdateExamSetCommand(
    Guid ExamSetId,
    string? SetCode,
    List<Guid>? QuestionIds,
    Guid ActorUserId,
    string? ActorRole = null,
    List<CreateInlineExamQuestionDto>? InlineManualQuestions = null) : IRequest<Result<ExamSetDto>>;
