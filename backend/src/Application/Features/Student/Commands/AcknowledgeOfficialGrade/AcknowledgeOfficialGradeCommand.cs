using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Student.DTOs;

namespace OralExamination.Application.Features.Student.Commands.AcknowledgeOfficialGrade;

/// <summary>
/// Command cho phép sinh viên xác nhận đồng ý kết quả điểm thi thật trên Student Portal (MF-04).
/// </summary>
public sealed record AcknowledgeOfficialGradeCommand(Guid TicketId, Guid StudentId)
    : IRequest<Result<AcknowledgeOfficialGradeResponseDto>>;
