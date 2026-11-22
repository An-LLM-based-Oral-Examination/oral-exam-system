using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetAntiCheatLogs;

public sealed record GetAntiCheatLogsQuery(
    Guid ShiftId,
    Guid? TicketId = null,
    Guid ActorUserId = default) : IRequest<Result<List<ExamAntiCheatLogDto>>>;
