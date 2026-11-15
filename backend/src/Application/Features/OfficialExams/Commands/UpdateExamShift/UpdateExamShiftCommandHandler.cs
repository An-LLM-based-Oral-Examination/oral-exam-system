using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Commands.OpenExamShift;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.UpdateExamShift;

public sealed class UpdateExamShiftCommandHandler(IApplicationDbContext context) : IRequestHandler<UpdateExamShiftCommand, Result>
{
    public async Task<Result> Handle(UpdateExamShiftCommand request, CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveClaimedActorAsync(context, request.ActorUserId, request.ActorRole,
                UserRole.ExaminationOfficer, cancellationToken)) return Result.Failure(OfficialExamErrors.Forbidden);
        if (request.ShiftId == Guid.Empty) return Result.Failure("Mã ca thi không được để trống.");
        var startTime = NormalizeUtc(request.StartTime);
        var endTime = NormalizeUtc(request.EndTime);
        return await context.ExecuteShiftMutationAsync(request.ShiftId, true, async ct =>
        {
            var shift = await context.RealExamSessionShifts.FirstOrDefaultAsync(s => s.Id == request.ShiftId, ct);
            if (shift is null) return Result.Failure(OfficialExamErrors.ShiftNotFound);
            var validation = await new OpenExamShiftCommandValidator().ValidateAsync(new OpenExamShiftCommand(
                request.ActorUserId, shift.SessionId, request.ShiftName, request.RoomLab, startTime,
                endTime, request.ProctorUserId, request.ExaminerUserId), ct);
            if (!validation.IsValid) return Result.Failure(validation.Errors[0].ErrorMessage);
            var conflict = await ShiftMutationGuard.Conflict(context, shift, false, ct);
            if (conflict is not null) return Result.Failure(conflict);
            if (startTime <= DateTime.UtcNow) return Result.Failure("Giờ bắt đầu mới phải ở trong tương lai.");
            var proctorValid = await context.Users.AnyAsync(u => u.Id == request.ProctorUserId && u.IsActive
                && (u.Role == UserRole.Proctor || u.Role == UserRole.Lecturer), ct);
            if (!proctorValid) return Result.Failure(OfficialExamErrors.ProctorInvalid);
            var examinerValid = await context.Users.AnyAsync(u => u.Id == request.ExaminerUserId && u.IsActive
                && (u.Role == UserRole.Lecturer || u.Role == UserRole.DepartmentHead), ct);
            if (!examinerValid) return Result.Failure("Giảng viên chấm thi phải là giảng viên đang hoạt động.");
            shift.ShiftName = request.ShiftName!.Trim();
            shift.RoomLab = request.RoomLab!.Trim();
            shift.StartTime = startTime;
            shift.EndTime = endTime;
            shift.ProctorUserId = request.ProctorUserId;
            shift.ExaminerUserId = request.ExaminerUserId;
            await context.SaveChangesAsync(ct);
            return Result.Success();
        }, cancellationToken);
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind == DateTimeKind.Local
        ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
