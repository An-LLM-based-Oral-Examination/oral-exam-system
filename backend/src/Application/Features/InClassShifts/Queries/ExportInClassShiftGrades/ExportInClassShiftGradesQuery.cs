using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;

namespace OralExamination.Application.Features.InClassShifts.Queries.ExportInClassShiftGrades;

public sealed record ExportInClassShiftGradesQuery(
    Guid ActorUserId,
    Guid ShiftId) : IRequest<Result<InClassShiftExportFileDto>>;
