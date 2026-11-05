using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;

namespace OralExamination.Application.Features.InClassShifts.Queries.GetStudentInClassSubmission;

public sealed record GetStudentInClassSubmissionQuery(
    Guid StudentId,
    Guid ShiftId,
    Guid? QuestionId = null,
    int? FollowUpIndex = null
) : IRequest<Result<InClassExamSubmissionResponse>>;
