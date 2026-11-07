using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.InClassShifts.Commands.FinalizeInClassShiftGrades;

public sealed class FinalizeInClassShiftGradesCommandHandler(
    IApplicationDbContext context)
    : IRequestHandler<FinalizeInClassShiftGradesCommand, Result<FinalizeInClassGradesResponse>>
{
    public async Task<Result<FinalizeInClassGradesResponse>> Handle(
        FinalizeInClassShiftGradesCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);

        if (actor is null || !actor.IsActive || actor.Role is not (UserRole.Lecturer or UserRole.DepartmentHead or UserRole.Admin))
        {
            return Result<FinalizeInClassGradesResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }

        var shift = await context.InClassExamShifts
            .Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Result<FinalizeInClassGradesResponse>.Failure(InClassShiftErrors.ShiftNotFound);
        }

        if (shift.IsLocked || shift.Status == "FINALIZED")
        {
            return Result<FinalizeInClassGradesResponse>.Failure(InClassShiftErrors.ShiftLocked);
        }

        if (actor.Role == UserRole.Lecturer)
        {
            bool isAssigned = shift.LecturerId == actor.Id ||
                shift.Class.LecturerId == actor.Id ||
                await context.CourseLecturers.AnyAsync(
                    cl => cl.CourseId == shift.Class.CourseId && cl.LecturerUserId == actor.Id,
                    cancellationToken);

            if (!isAssigned)
            {
                return Result<FinalizeInClassGradesResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
            }
        }

        shift.Status = "FINALIZED";
        shift.IsLocked = true;
        shift.FinalizedAt = DateTime.UtcNow;
        shift.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        var response = new FinalizeInClassGradesResponse(
            shift.Id,
            shift.ShiftName,
            shift.Status,
            shift.IsLocked,
            shift.FinalizedAt
        );

        return Result<FinalizeInClassGradesResponse>.Success(response);
    }
}
