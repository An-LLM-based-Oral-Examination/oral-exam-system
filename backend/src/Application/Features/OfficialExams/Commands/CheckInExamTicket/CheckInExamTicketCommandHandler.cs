using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.AssignExamTicket;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.CheckInExamTicket;

public sealed class CheckInExamTicketCommandHandler : IRequestHandler<CheckInExamTicketCommand, Result<ExamTicketResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<CheckInExamTicketCommand> _validator;

    public CheckInExamTicketCommandHandler(
        IApplicationDbContext context,
        IValidator<CheckInExamTicketCommand> validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Result<ExamTicketResponse>> Handle(CheckInExamTicketCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ExamTicketResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        var actor = await _context.Users.FirstOrDefaultAsync(user => user.Id == request.ActorUserId, cancellationToken);
        if (actor is null || !actor.IsActive)
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        var ticket = await _context.StudentExamTickets
            .Include(item => item.Shift)
            .FirstOrDefaultAsync(item => item.Id == request.TicketId, cancellationToken);
        if (ticket is null)
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.TicketNotFound);
        }

        var isOwner = actor.Id == ticket.StudentId && actor.Role == UserRole.Student;
        var isProctor = ticket.Shift.ProctorUserId == actor.Id
            && (actor.Role == UserRole.Proctor || actor.Role == UserRole.Lecturer);
        if (!isOwner && !isProctor)
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        if (ticket.SeatNumber < OfficialExamRules.MinSeat || ticket.SeatNumber > OfficialExamRules.MaxSeat)
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.SeatOutOfRange);
        }

        if (!string.Equals(ticket.Status, ExamTicketStatus.Scheduled, StringComparison.OrdinalIgnoreCase))
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.TicketNotScheduled);
        }

        if (!WorkstationAddress.TryNormalize(request.RemoteIpAddress, out _))
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.WorkstationUnknown);
        }

        if (!WorkstationAddress.Same(ticket.IpAddress, request.RemoteIpAddress))
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.WrongWorkstation);
        }

        if (!ExamShiftWindow.IsOpenStatus(ticket.Shift.Status))
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.ShiftClosed);
        }

        if (!ExamShiftWindow.Contains(ticket.Shift, request.AtUtc))
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.ShiftNotOpen);
        }

        ticket.Status = ExamTicketStatus.InProgress;
        ticket.CheckedInAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Result<ExamTicketResponse>.Success(AssignExamTicketCommandHandler.ToResponse(ticket));
    }
}
