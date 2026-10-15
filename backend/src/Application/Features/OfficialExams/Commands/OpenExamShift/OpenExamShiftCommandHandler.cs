using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
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

        var actor = await _context.Users.FirstOrDefaultAsync(user => user.Id == request.ActorUserId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role != UserRole.DepartmentHead)
        {
            return Result<ExamShiftResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        var sessionExists = await _context.OfficialExamSessions.AnyAsync(
            session => session.Id == request.SessionId,
            cancellationToken);
        if (!sessionExists)
        {
            return Result<ExamShiftResponse>.Failure(OfficialExamErrors.SessionNotFound);
        }

        var proctor = await _context.Users.FirstOrDefaultAsync(user => user.Id == request.ProctorUserId, cancellationToken);
        if (proctor is null
            || !proctor.IsActive
            || (proctor.Role != UserRole.Proctor && proctor.Role != UserRole.Lecturer))
        {
            return Result<ExamShiftResponse>.Failure(OfficialExamErrors.ProctorInvalid);
        }

        var shift = new RealExamSessionShift
        {
            SessionId = request.SessionId,
            ShiftName = request.ShiftName!.Trim(),
            RoomLab = request.RoomLab!.Trim(),
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            ProctorUserId = proctor.Id,
            Status = "scheduled",
            CreatedAt = DateTime.UtcNow
        };
        _context.RealExamSessionShifts.Add(shift);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<ExamShiftResponse>.Success(new ExamShiftResponse(
            shift.Id,
            shift.SessionId,
            shift.ShiftName,
            shift.RoomLab,
            shift.StartTime,
            shift.EndTime,
            shift.ProctorUserId,
            shift.Status));
    }
}
