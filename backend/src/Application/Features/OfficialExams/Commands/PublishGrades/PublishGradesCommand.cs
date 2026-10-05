using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.PublishGrades;

/// <summary>
/// CQRS Command công bố điểm thi toàn bộ ca thi phòng Lab (MF-04) và kích hoạt One-Way Lock.
/// Bắt buộc 100% sinh viên trong ca thi đã có điểm hoàn chỉnh.
/// </summary>
public sealed record PublishGradesCommand(
    Guid ShiftId,
    Guid LecturerId
) : IRequest<Result<PublishGradesResponseDto>>;
