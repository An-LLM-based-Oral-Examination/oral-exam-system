using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.OpenExamShift;

public sealed class OpenExamShiftCommandHandler : IRequestHandler<OpenExamShiftCommand, Result<ExamShiftResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<OpenExamShiftCommand> _validator;

    public OpenExamShiftCommandHandler(
        IApplicationDbContext context,
        IValidator<OpenExamShiftCommand> validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Result<ExamShiftResponse>> Handle(OpenExamShiftCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ExamShiftResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        var actor = await _context.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == request.ActorUserId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role != UserRole.ExaminationOfficer)
        {
            return Result<ExamShiftResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        var startTimeUtc = OfficialExamSchedulingHelper.NormalizeUtc(request.StartTime);
        var endTimeUtc = OfficialExamSchedulingHelper.NormalizeUtc(request.EndTime);

        return await _context.ExecuteOfficialShiftScheduleBatchAsync(
            async ct =>
            {
                var session = await _context.OfficialExamSessions
                    .AsNoTracking()
                    .Where(s => s.Id == request.SessionId)
                    .Select(s => new { s.Id, s.ExamAttemptType })
                    .FirstOrDefaultAsync(ct);
                if (session is null)
                {
                    return Result<ExamShiftResponse>.Failure(OfficialExamErrors.SessionNotFound);
                }

                if (request.ExaminerUserId == request.ProctorUserId)
                {
                    return Result<ExamShiftResponse>.Failure("Giảng viên chấm thi không được trùng với người coi thi.");
                }

                var proctor = await _context.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == request.ProctorUserId, ct);
                if (proctor is null
                    || !proctor.IsActive
                    || proctor.Role != UserRole.Proctor)
                {
                    return Result<ExamShiftResponse>.Failure(OfficialExamErrors.ProctorInvalid);
                }

                var examiner = await _context.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == request.ExaminerUserId, ct);
                if (examiner is null
                    || !examiner.IsActive
                    || (examiner.Role != UserRole.Lecturer && examiner.Role != UserRole.DepartmentHead))
                {
                    return Result<ExamShiftResponse>.Failure("Giảng viên chấm thi phải là giảng viên đang hoạt động.");
                }

                var scheduleConflict = await OfficialExamSchedulingHelper.FindShiftScheduleConflictAsync(
                    _context,
                    request.RoomLab!,
                    startTimeUtc,
                    endTimeUtc,
                    proctor.Id,
                    examiner.Id,
                    excludeShiftId: null,
                    ct);
                if (scheduleConflict is not null)
                {
                    return Result<ExamShiftResponse>.Failure(scheduleConflict);
                }

                var shift = new RealExamSessionShift
                {
                    SessionId = request.SessionId,
                    ShiftName = request.ShiftName!.Trim(),
                    RoomLab = request.RoomLab!.Trim(),
                    MaxSeats = request.MaxSeats,
                    StartTime = startTimeUtc,
                    EndTime = endTimeUtc,
                    ProctorUserId = proctor.Id,
                    ExaminerUserId = examiner.Id,
                    Status = "scheduled",
                    CreatedAt = DateTime.UtcNow
                };
                _context.RealExamSessionShifts.Add(shift);
                await _context.SaveChangesAsync(ct);

                return Result<ExamShiftResponse>.Success(new ExamShiftResponse(
                    shift.Id,
                    shift.SessionId,
                    shift.ShiftName,
                    shift.RoomLab,
                    shift.StartTime,
                    shift.EndTime,
                    shift.ProctorUserId,
                    shift.Status,
                    shift.ExaminerUserId,
                    examiner.FullName,
                    proctor.FullName,
                    shift.MaxSeats,
                    IsExamPublished: false,
                    IsGradesPublished: false,
                    GradesPublishedAt: null,
                    CertificateHash: null,
                    ExamAttemptType: string.IsNullOrWhiteSpace(session.ExamAttemptType)
                        ? OfficialExamAttemptType.FinalExam
                        : session.ExamAttemptType,
                    AssignedTicketsCount: 0,
                    RemainingSeats: shift.MaxSeats));
            },
            cancellationToken);
    }
}
