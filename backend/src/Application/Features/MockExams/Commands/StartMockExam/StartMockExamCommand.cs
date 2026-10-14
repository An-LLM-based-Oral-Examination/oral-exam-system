using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.DTOs;

namespace OralExamination.Application.Features.MockExams.Commands.StartMockExam;

/// <summary>
/// Command khởi tạo ca thi thử bấm giờ có hạn ngạch (MF-02).
/// </summary>
public record StartMockExamCommand(
    Guid CourseId,
    Guid StudentId,
    bool HasFollowUp
) : IRequest<Result<StartMockExamResponse>>;
