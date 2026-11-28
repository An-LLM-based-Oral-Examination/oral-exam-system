using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.InClassShifts.Commands.MarkInClassParticipantAbsent;

public sealed class MarkInClassParticipantAbsentCommandHandler(
    IApplicationDbContext context)
    : IRequestHandler<MarkInClassParticipantAbsentCommand, Result<InClassShiftParticipantResponse>>
{
    public Task<Result<InClassShiftParticipantResponse>> Handle(
        MarkInClassParticipantAbsentCommand request,
        CancellationToken cancellationToken) =>
        context.ExecuteShiftMutationAsync(
            request.ShiftId,
            isOfficial: false,
            async ct =>
            {
                if (request.ActorUserId == Guid.Empty)
                {
                    return Result<InClassShiftParticipantResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
                }

                var actor = await context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, ct);

                if (actor is null || !actor.IsActive || actor.Role != UserRole.Lecturer)
                {
                    return Result<InClassShiftParticipantResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
                }

                var shift = await context.InClassExamShifts
                    .Include(s => s.Class)
                        .ThenInclude(c => c.Course)
                    .Include(s => s.Class)
                        .ThenInclude(c => c.CourseOffering)
                    .FirstOrDefaultAsync(s => s.Id == request.ShiftId, ct);

                if (shift is null)
                {
                    return Result<InClassShiftParticipantResponse>.Failure(InClassShiftErrors.ShiftNotFound);
                }

                if (shift.Class.LecturerId != actor.Id)
                {
                    return Result<InClassShiftParticipantResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
                }

                if (!InClassShiftAcademicContext.IsOperational(shift.Class))
                {
                    return Result<InClassShiftParticipantResponse>.Failure(InClassShiftErrors.ClassNotFound);
                }

                if (shift.IsLocked || string.Equals(shift.Status, "FINALIZED", StringComparison.OrdinalIgnoreCase))
                {
                    return Result<InClassShiftParticipantResponse>.Failure(InClassShiftErrors.ShiftLocked);
                }

                if (string.Equals(shift.Status, "SCHEDULED", StringComparison.OrdinalIgnoreCase))
                {
                    return Result<InClassShiftParticipantResponse>.Failure(
                        "CONFLICT: Chỉ có thể đánh dấu vắng mặt khi ca thi đang mở hoặc đã đóng.");
                }

                if (request.StudentId == Guid.Empty)
                {
                    return Result<InClassShiftParticipantResponse>.Failure(
                        $"NOT_FOUND: {InClassShiftErrors.StudentNotEnrolled}");
                }

                var enrollment = await context.ClassEnrollments
                    .AsNoTracking()
                    .Include(e => e.Student)
                    .FirstOrDefaultAsync(
                        e => e.ClassId == shift.ClassId &&
                             e.StudentId == request.StudentId &&
                             e.Status == "enrolled",
                        ct);

                if (enrollment is null || enrollment.Student is null || !enrollment.Student.IsActive)
                {
                    return Result<InClassShiftParticipantResponse>.Failure(
                        $"NOT_FOUND: {InClassShiftErrors.StudentNotEnrolled}");
                }

                var utcNow = DateTime.UtcNow;
                var participant = await context.InClassShiftParticipants
                    .FirstOrDefaultAsync(
                        p => p.ShiftId == shift.Id && p.StudentId == request.StudentId,
                        ct);

                if (participant is not null)
                {
                    if (string.Equals(participant.Status, "ABSENT", StringComparison.OrdinalIgnoreCase))
                    {
                        return Result<InClassShiftParticipantResponse>.Success(MapDto(participant, enrollment.Student));
                    }

                    var hasSubmissions = await context.InClassExamSubmissions
                        .AsNoTracking()
                        .AnyAsync(s => s.ShiftId == shift.Id && s.StudentId == request.StudentId, ct);

                    if (EnterInClassShiftCommandHandler.IsCompletedParticipation(participant.Status) || hasSubmissions)
                    {
                        return Result<InClassShiftParticipantResponse>.Failure(
                            "CONFLICT: Sinh viên đã tham gia hoặc nộp bài trong ca thi này, không thể đánh dấu vắng mặt.");
                    }

                    participant.Status = "ABSENT";
                    participant.FinishedAt ??= utcNow;
                    participant.TotalAiScore = null;
                    participant.TotalFinalScore = null;
                    await context.SaveChangesAsync(ct);

                    return Result<InClassShiftParticipantResponse>.Success(MapDto(participant, enrollment.Student));
                }

                participant = new InClassShiftParticipant
                {
                    Id = Guid.NewGuid(),
                    ShiftId = shift.Id,
                    StudentId = request.StudentId,
                    AssignedQuestionId = null,
                    AssignedQuestionIdsJson = "[]",
                    TotalAiScore = null,
                    TotalFinalScore = null,
                    JoinedAt = utcNow,
                    FinishedAt = utcNow,
                    Status = "ABSENT"
                };

                context.InClassShiftParticipants.Add(participant);
                await context.SaveChangesAsync(ct);

                return Result<InClassShiftParticipantResponse>.Success(MapDto(participant, enrollment.Student));
            },
            cancellationToken);

    private static InClassShiftParticipantResponse MapDto(InClassShiftParticipant participant, User student) =>
        new(
            participant.Id,
            participant.ShiftId,
            participant.StudentId,
            student.FullName,
            student.Email,
            participant.Status,
            participant.JoinedAt,
            participant.AssignedQuestionId,
            null,
            EnterInClassShiftCommandHandler.ParseGuidList(participant.AssignedQuestionIdsJson),
            Array.Empty<InClassAssignedQuestionDto>(),
            participant.TotalAiScore,
            participant.TotalFinalScore,
            student.StudentCode);
}
