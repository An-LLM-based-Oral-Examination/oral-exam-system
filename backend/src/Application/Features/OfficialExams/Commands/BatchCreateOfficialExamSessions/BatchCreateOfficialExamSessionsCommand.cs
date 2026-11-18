using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.BatchCreateOfficialExamSessions;

public sealed record BatchCreateOfficialExamSessionsCommand(
    Guid ActorUserId,
    Guid SemesterId,
    DateOnly ExamDate,
    string? ExamInputMode,
    int TranscriptBufferSeconds,
    bool SkipExisting,
    IReadOnlyList<BatchCreateOfficialExamSessionCourseItemDto>? Courses)
    : IRequest<Result<BatchCreateOfficialExamSessionsResponse>>;
