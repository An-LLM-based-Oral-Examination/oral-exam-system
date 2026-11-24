using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.InClassShifts.Commands.FinishStudentInClassExam;

public sealed class FinishStudentInClassExamCommandHandler(
    IApplicationDbContext context,
    IGradingQueueChannel gradingQueue)
    : IRequestHandler<FinishStudentInClassExamCommand, Result<FinishStudentInClassExamResponse>>
{
    public async Task<Result<FinishStudentInClassExamResponse>> Handle(
        FinishStudentInClassExamCommand request,
        CancellationToken cancellationToken)
    {
        var outcome = await context.ExecuteInClassSubmissionAsync(
            request.ShiftId,
            request.StudentId,
            async ct =>
            {
                var shift = await context.InClassExamShifts
                    .Include(s => s.Class)
                        .ThenInclude(c => c.Course)
                    .Include(s => s.Class)
                        .ThenInclude(c => c.CourseOffering)
                    .FirstOrDefaultAsync(s => s.Id == request.ShiftId, ct);

                if (shift is null)
                {
                    return new FinishOutcome(Result<FinishStudentInClassExamResponse>.Failure(InClassShiftErrors.ShiftNotFound), []);
                }

                if (!InClassShiftAcademicContext.IsOperational(shift.Class))
                {
                    return new FinishOutcome(Result<FinishStudentInClassExamResponse>.Failure(InClassShiftErrors.ClassNotFound), []);
                }

                if (shift.IsLocked || shift.Status == "FINALIZED")
                {
                    return new FinishOutcome(
                        Result<FinishStudentInClassExamResponse>.Failure(InClassShiftErrors.ShiftLocked),
                        []);
                }

                var participant = await context.InClassShiftParticipants
                    .FirstOrDefaultAsync(
                        p => p.ShiftId == request.ShiftId && p.StudentId == request.StudentId,
                        ct);

                if (participant is null)
                {
                    return new FinishOutcome(Result<FinishStudentInClassExamResponse>.Failure(InClassShiftErrors.ParticipantNotFound), []);
                }

                if (string.Equals(participant.Status, "ABSENT", StringComparison.OrdinalIgnoreCase))
                {
                    return new FinishOutcome(
                        Result<FinishStudentInClassExamResponse>.Failure("CONFLICT: Sinh viên đang ở trạng thái vắng mặt (ABSENT), không thể chốt bài thi."),
                        []);
                }

                var utcNow = DateTime.UtcNow;
                if (participant.Status is "GRADED" or "FINALIZED" or "COMPLETED")
                {
                    if (!participant.FinishedAt.HasValue)
                    {
                        participant.FinishedAt = utcNow;
                        await context.SaveChangesAsync(ct);
                    }

                    return new FinishOutcome(
                        Result<FinishStudentInClassExamResponse>.Success(new FinishStudentInClassExamResponse(
                            shift.Id,
                            participant.StudentId,
                            participant.Status,
                            participant.FinishedAt.Value)),
                        []);
                }

                // Finish là cổng toàn vẹn cuối cùng dưới lock (ShiftId, StudentId):
                // đặt FinishedAt vĩnh viễn và tạo mọi câu chính còn thiếu trước khi xác nhận SUBMITTED.
                participant.FinishedAt ??= utcNow;
                var created = await InClassShiftAutoSubmit.FinalizeParticipantInTransactionAsync(
                    context,
                    participant,
                    ct);

                return new FinishOutcome(
                    Result<FinishStudentInClassExamResponse>.Success(new FinishStudentInClassExamResponse(
                        shift.Id,
                        participant.StudentId,
                        participant.Status,
                        participant.FinishedAt ?? utcNow)),
                    created);
            },
            cancellationToken);

        if (outcome.CreatedSubmissions.Count > 0)
        {
            await InClassShiftAutoSubmit.EnqueueSubmissionsAsync(
                gradingQueue,
                outcome.CreatedSubmissions,
                cancellationToken);
        }

        return outcome.Result;
    }

    private sealed record FinishOutcome(
        Result<FinishStudentInClassExamResponse> Result,
        IReadOnlyList<InClassExamSubmission> CreatedSubmissions);
}
