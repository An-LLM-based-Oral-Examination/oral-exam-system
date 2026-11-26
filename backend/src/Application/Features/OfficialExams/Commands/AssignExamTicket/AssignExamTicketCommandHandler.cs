using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.AssignExamTicket;

public sealed class AssignExamTicketCommandHandler : IRequestHandler<AssignExamTicketCommand, Result<ExamTicketResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<AssignExamTicketCommand> _validator;

    public AssignExamTicketCommandHandler(
        IApplicationDbContext context,
        IValidator<AssignExamTicketCommand> validator)
    {
        _context = context;
        _validator = validator;
    }

    public Task<Result<ExamTicketResponse>> Handle(AssignExamTicketCommand request, CancellationToken cancellationToken) =>
        _context.ExecuteShiftMutationAsync(request.ShiftId, true,
            ct => HandleLockedAsync(request, ct), cancellationToken);

    private async Task<Result<ExamTicketResponse>> HandleLockedAsync(AssignExamTicketCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ExamTicketResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        var actor = await _context.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == request.ActorUserId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role != UserRole.ExaminationOfficer)
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        var shift = await _context.RealExamSessionShifts
            .AsNoTracking()
            .Include(s => s.Session)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);
        if (shift is null)
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.ShiftNotFound);
        }

        if (request.SeatNumber > shift.MaxSeats)
        {
            return Result<ExamTicketResponse>.Failure(
                $"Số ghế phải nằm trong khoảng {OfficialExamRules.MinSeat} đến {shift.MaxSeats} của ca thi.");
        }

        var assignedTicketCount = await _context.StudentExamTickets.CountAsync(
            ticket => ticket.ShiftId == request.ShiftId, cancellationToken);
        if (assignedTicketCount >= shift.MaxSeats)
        {
            return Result<ExamTicketResponse>.Failure("CONFLICT: Ca thi đã đủ số lượng vé theo sức chứa được cấu hình.");
        }

        User? student = null;
        if (request.StudentId != Guid.Empty)
        {
            student = await _context.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == request.StudentId, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.StudentCode))
        {
            var normalizedCode = request.StudentCode.Trim().ToUpperInvariant();
            student = await _context.Users.AsNoTracking().FirstOrDefaultAsync(
                user => user.StudentCode != null && user.StudentCode.ToUpper() == normalizedCode,
                cancellationToken);
        }

        if (student is null || !student.IsActive || student.Role != UserRole.Student)
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.StudentNotFound);
        }

        var seatTaken = await _context.StudentExamTickets.AnyAsync(
            ticket => ticket.ShiftId == request.ShiftId && ticket.SeatNumber == request.SeatNumber,
            cancellationToken);
        if (seatTaken)
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.SeatTaken);
        }

        var studentAssigned = await _context.StudentExamTickets.AnyAsync(
            ticket => ticket.ShiftId == request.ShiftId && ticket.StudentId == student.Id,
            cancellationToken);
        if (studentAssigned)
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.StudentAlreadyAssigned);
        }

        var resolvedExamSetId = await TryResolveHandedOverExamSetIdAsync(_context, shift, cancellationToken);
        if (!resolvedExamSetId.HasValue)
        {
            return Result<ExamTicketResponse>.Failure(
                $"UNPROCESSABLE: {OfficialExamErrors.NoAvailableExamSet} Vui lòng yêu cầu Trưởng bộ môn bàn giao thêm bộ đề (HANDED_OVER).");
        }

        string? ipAddress = null;
        if (!string.IsNullOrWhiteSpace(request.IpAddress))
        {
            WorkstationAddress.TryNormalize(request.IpAddress, out ipAddress);
        }

        var ticket = new StudentExamTicket
        {
            ShiftId = request.ShiftId,
            StudentId = student.Id,
            ExamSetId = resolvedExamSetId,
            SeatNumber = request.SeatNumber,
            IpAddress = ipAddress,
            Status = ExamTicketStatus.Scheduled,
            IsLocked = false,
            CheckedInAt = DateTime.UtcNow
        };
        _context.StudentExamTickets.Add(ticket);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<ExamTicketResponse>.Success(ToResponse(ticket));
    }

    internal static async Task<Guid?> TryResolveHandedOverExamSetIdAsync(
        IApplicationDbContext context,
        RealExamSessionShift? shift,
        CancellationToken cancellationToken)
    {
        if (shift is null)
        {
            return null;
        }

        var session = shift.Session ?? await context.OfficialExamSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shift.SessionId, cancellationToken);

        if (session is null)
        {
            return null;
        }

        var candidateSetsQuery = context.ExamSets
            .AsNoTracking()
            .Where(es => es.IsActive
                && !es.IsSampleSet
                && es.Status == ExamSetStatus.HandedOver
                && es.CourseId == session.CourseId
                && (!es.SessionId.HasValue || es.SessionId == shift.SessionId));

        if (session.ExamStructureId.HasValue && session.ExamStructureId.Value != Guid.Empty)
        {
            candidateSetsQuery = candidateSetsQuery.Where(
                es => es.StructureId == session.ExamStructureId.Value);
        }

        var candidateSets = await candidateSetsQuery
            .OrderBy(es => es.GeneratedAt)
            .ThenBy(es => es.Id)
            .Select(es => es.Id)
            .ToListAsync(cancellationToken);

        var assignedSetIds = await context.StudentExamTickets
            .AsNoTracking()
            .Where(t => t.ShiftId == shift.Id && t.ExamSetId.HasValue)
            .Select(t => t.ExamSetId!.Value)
            .ToListAsync(cancellationToken);

        var availableSet = candidateSets.FirstOrDefault(id => !assignedSetIds.Contains(id));
        return availableSet != Guid.Empty ? availableSet : null;
    }

    internal static ExamTicketResponse ToResponse(StudentExamTicket ticket, string? examSetCode = null)
    {
        return new ExamTicketResponse(
            ticket.Id,
            ticket.ShiftId,
            ticket.StudentId,
            ticket.SeatNumber,
            ticket.IpAddress,
            ticket.Status,
            ticket.CheckedInAt,
            ticket.ExamSetId,
            examSetCode ?? ticket.ExamSet?.SetCode,
            ticket.DeviceId,
            ticket.DeviceLockedAt,
            ticket.DeviceUnlockCount);
    }
}
