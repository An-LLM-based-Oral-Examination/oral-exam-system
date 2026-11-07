using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Common.Security;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftCurrentPassword;

public sealed class GetInClassShiftCurrentPasswordQueryHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher)
    : IRequestHandler<GetInClassShiftCurrentPasswordQuery, Result<ActiveShiftPasswordResponse>>
{
    public async Task<Result<ActiveShiftPasswordResponse>> Handle(
        GetInClassShiftCurrentPasswordQuery request,
        CancellationToken cancellationToken)
    {
        var shift = await context.InClassExamShifts
            .Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Result<ActiveShiftPasswordResponse>.Failure(InClassShiftErrors.ShiftNotFound);
        }

        var actor = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);

        if (actor is null || !actor.IsActive || actor.Role is not (UserRole.Lecturer or UserRole.DepartmentHead or UserRole.Admin))
        {
            return Result<ActiveShiftPasswordResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
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
                return Result<ActiveShiftPasswordResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
            }
        }

        if (InClassShiftLifecycle.TryCloseIfDue(shift, DateTime.UtcNow))
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        if (shift.Status is "CLOSED" or "ENDED")
        {
            return Result<ActiveShiftPasswordResponse>.Failure(InClassShiftErrors.ShiftClosed);
        }

        if (shift.Status != "IN_PROGRESS")
        {
            return Result<ActiveShiftPasswordResponse>.Failure(InClassShiftErrors.ShiftNotOpen);
        }

        // Tính toán OTP chu kỳ hiện tại kèm mốc thời gian
        var details = TotpPasswordHelper.GetCurrentPasswordDetails(shift.TotpSecretKey);

        // Lưu bản ghi vào in_class_shift_passwords nếu chưa tồn tại cho chu kỳ này (Audit Trail)
        bool exists = await context.InClassShiftPasswords.AnyAsync(
            p => p.ShiftId == shift.Id && p.ValidFrom == details.ValidFrom,
            cancellationToken);

        if (!exists)
        {
            var shiftPassword = new InClassShiftPassword
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                PlainCode = details.Password,
                PasswordHash = passwordHasher.Hash(details.Password),
                ValidFrom = details.ValidFrom,
                ValidTo = details.ValidTo,
                CreatedAt = DateTime.UtcNow
            };

            context.InClassShiftPasswords.Add(shiftPassword);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Nếu có request đồng thời vừa ghi bản ghi OTP của cùng cửa sổ 5 phút, bỏ qua lỗi trùng khóa và tiếp tục trả về mã OTP hợp lệ
            }
        }

        var response = new ActiveShiftPasswordResponse(
            shift.Id,
            details.Password,
            details.RemainingSeconds,
            details.ValidFrom,
            details.ValidTo
        );

        return Result<ActiveShiftPasswordResponse>.Success(response);
    }
}
