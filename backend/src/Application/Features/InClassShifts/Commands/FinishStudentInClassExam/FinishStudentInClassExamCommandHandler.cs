using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts;

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
        var shift = await context.InClassExamShifts
            .Include(s => s.Class)
                .ThenInclude(c => c.Course)
            .Include(s => s.Class)
                .ThenInclude(c => c.CourseOffering)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Result<FinishStudentInClassExamResponse>.Failure(InClassShiftErrors.ShiftNotFound);
        }

        if (!InClassShiftAcademicContext.IsOperational(shift.Class))
        {
            return Result<FinishStudentInClassExamResponse>.Failure(InClassShiftErrors.ClassNotFound);
        }

        var participant = await context.InClassShiftParticipants
            .FirstOrDefaultAsync(
                p => p.ShiftId == request.ShiftId && p.StudentId == request.StudentId,
                cancellationToken);

        if (participant is null)
        {
            return Result<FinishStudentInClassExamResponse>.Failure(InClassShiftErrors.ParticipantNotFound);
        }

        if (participant.Status is "GRADED" or "FINALIZED" or "COMPLETED")
        {
            return Result<FinishStudentInClassExamResponse>.Success(new FinishStudentInClassExamResponse(
                shift.Id,
                participant.StudentId,
                participant.Status,
                DateTime.UtcNow));
        }

        // Finish là cổng toàn vẹn cuối cùng: bảo đảm mọi câu chính được giao đều có
        // submission trước khi participant được xác nhận SUBMITTED. Hàm này cũng
        // phục hồi dữ liệu cũ bị trạng thái SUBMITTED nhưng thiếu bài.
        await InClassShiftAutoSubmit.FinalizeParticipantAsync(
            context,
            gradingQueue,
            participant,
            cancellationToken);

        return Result<FinishStudentInClassExamResponse>.Success(new FinishStudentInClassExamResponse(
            shift.Id,
            participant.StudentId,
            participant.Status,
            DateTime.UtcNow));
    }
}
