using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;

namespace OralExamination.Application.Features.InClassShifts.Commands.SubmitInClassShiftAnswer;

public sealed record SubmitInClassShiftAnswerCommand(
    Guid StudentId,
    Guid ShiftId,
    Guid QuestionId,
    string TranscriptText,
    bool IsFollowUp = false,
    Guid? ParentSubmissionId = null,
    int FollowUpIndex = 0
) : IRequest<Result<SubmitInClassAnswerResponse>>;
