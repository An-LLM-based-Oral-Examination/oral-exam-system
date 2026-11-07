using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;

namespace OralExamination.Application.Features.InClassShifts.Commands.FinishStudentInClassExam;

public sealed class FinishStudentInClassExamCommandHandler(
    IApplicationDbContext context)
    : IRequestHandler<FinishStudentInClassExamCommand, Result<FinishStudentInClassExamResponse>>
{
    public async Task<Result<FinishStudentInClassExamResponse>> Handle(
        FinishStudentInClassExamCommand request,
        CancellationToken cancellationToken)
    {
        var shift = await context.InClassExamShifts
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Result<FinishStudentInClassExamResponse>.Failure(InClassShiftErrors.ShiftNotFound);
        }

        var participant = await context.InClassShiftParticipants
            .FirstOrDefaultAsync(
                p => p.ShiftId == request.ShiftId && p.StudentId == request.StudentId,
                cancellationToken);

        if (participant is null)
        {
            return Result<FinishStudentInClassExamResponse>.Failure(InClassShiftErrors.ParticipantNotFound);
        }

        if (EnterInClassShiftCommandHandler.IsCompletedParticipation(participant.Status))
        {
            return Result<FinishStudentInClassExamResponse>.Success(new FinishStudentInClassExamResponse(
                shift.Id,
                participant.StudentId,
                participant.Status,
                DateTime.UtcNow));
        }

        participant.Status = "SUBMITTED";
        await context.SaveChangesAsync(cancellationToken);

        return Result<FinishStudentInClassExamResponse>.Success(new FinishStudentInClassExamResponse(
            shift.Id,
            participant.StudentId,
            participant.Status,
            DateTime.UtcNow));
    }
}
