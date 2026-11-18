using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.AutoAllocateSessionStudents;

public sealed record AutoAllocateSessionStudentsCommand(
    Guid ActorUserId,
    Guid SessionId,
    IReadOnlyList<Guid>? ShiftIds = null,
    bool AssignHandedOverExamSets = false,
    bool DryRun = false) : IRequest<Result<AutoAllocateSessionStudentsResponse>>;
