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

    public async Task<Result<ExamTicketResponse>> Handle(AssignExamTicketCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ExamTicketResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        var actor = await _context.Users.FirstOrDefaultAsync(user => user.Id == request.ActorUserId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role != UserRole.DepartmentHead)
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        var shiftExists = await _context.RealExamSessionShifts.AnyAsync(shift => shift.Id == request.ShiftId, cancellationToken);
        if (!shiftExists)
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.ShiftNotFound);
        }

        var student = await _context.Users.FirstOrDefaultAsync(user => user.Id == request.StudentId, cancellationToken);
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
            ticket => ticket.ShiftId == request.ShiftId && ticket.StudentId == request.StudentId,
            cancellationToken);
        if (studentAssigned)
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.StudentAlreadyAssigned);
        }

        WorkstationAddress.TryNormalize(request.IpAddress, out var ipAddress);
        var ticket = new StudentExamTicket
        {
            ShiftId = request.ShiftId,
            StudentId = student.Id,
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

    internal static ExamTicketResponse ToResponse(StudentExamTicket ticket)
    {
        return new ExamTicketResponse(
            ticket.Id,
            ticket.ShiftId,
            ticket.StudentId,
            ticket.SeatNumber,
            ticket.IpAddress,
            ticket.Status,
            ticket.CheckedInAt);
    }
}
