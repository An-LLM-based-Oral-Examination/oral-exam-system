using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;

namespace OralExamination.Application.Features.InClassShifts.Commands.CreateInClassShift;

public sealed record CreateInClassShiftCommand(
    Guid ActorUserId,
    Guid ClassId,
    string ShiftName,
    DateTime StartTime,
    DateTime EndTime,
    string? Description = null,
    string QuestionPoolMode = "ALL_COURSE_BANK",
    IReadOnlyList<Guid>? SelectedQuestionIds = null,
    int TotalQuestionsPerStudent = 1,
    bool HasFollowUp = false,
    int MaxFollowUpQuestions = 2,
    bool IsAutoPublishGrades = true
) : IRequest<Result<InClassExamShiftResponse>>;
