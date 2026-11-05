using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.SyncCourseOfferingLecturers;

public sealed record SyncCourseOfferingLecturersCommand(
    Guid ActorUserId,
    Guid CourseOfferingId,
    IReadOnlyCollection<Guid> LecturerUserIds) : IRequest<Result<SyncCourseOfferingLecturersResponseDto>>;
