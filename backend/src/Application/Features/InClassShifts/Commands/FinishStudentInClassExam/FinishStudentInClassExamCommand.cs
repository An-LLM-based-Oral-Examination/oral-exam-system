using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.InClassShifts.Commands.FinishStudentInClassExam;

public sealed record FinishStudentInClassExamCommand(
    Guid StudentId,
    Guid ShiftId
) : IRequest<Result<FinishStudentInClassExamResponse>>;

public sealed record FinishStudentInClassExamResponse(
    Guid ShiftId,
    Guid StudentId,
    string Status,
    DateTime FinishedAt
);
