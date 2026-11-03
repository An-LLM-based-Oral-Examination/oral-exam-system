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
        if (actor is null || !actor.IsActive || (actor.Role != UserRole.DepartmentHead && actor.Role != UserRole.ExaminationOfficer))
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        var shift = await _context.RealExamSessionShifts
            .Include(s => s.Session)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);
        if (shift is null)
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
        var resolvedExamSetId = await TryResolveOrCreateExamSetIdAsync(_context, shift, request.SeatNumber, cancellationToken);
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

    internal static async Task<Guid?> TryResolveOrCreateExamSetIdAsync(
        IApplicationDbContext context,
        RealExamSessionShift? shift,
        int seatNumber,
        CancellationToken cancellationToken)
    {
        if (shift?.Session is null)
        {
            return null;
        }

        var candidateSetsQuery = context.ExamSets
            .AsNoTracking()
            .Where(es => es.IsActive);

        if (shift.Session.ExamStructureId.HasValue && shift.Session.ExamStructureId.Value != Guid.Empty)
        {
            candidateSetsQuery = candidateSetsQuery.Where(es => es.StructureId == shift.Session.ExamStructureId.Value || es.CourseId == shift.Session.CourseId);
        }
        else
        {
            candidateSetsQuery = candidateSetsQuery.Where(es => es.CourseId == shift.Session.CourseId);
        }

        var candidateSets = await candidateSetsQuery
            .OrderBy(es => es.GeneratedAt)
            .ThenBy(es => es.Id)
            .Select(es => es.Id)
            .ToListAsync(cancellationToken);

        // Filter out exam sets already assigned in this shift to prevent duplicate set in same room (uq_tickets_shift_exam_set)
        var assignedSetIds = await context.StudentExamTickets
            .AsNoTracking()
            .Where(t => t.ShiftId == shift.Id && t.ExamSetId.HasValue)
            .Select(t => t.ExamSetId!.Value)
            .ToListAsync(cancellationToken);

        var availableSet = candidateSets.FirstOrDefault(id => !assignedSetIds.Contains(id));
        if (availableSet != Guid.Empty && availableSet != default)
        {
            return availableSet;
        }

        var approvedQuestions = await context.ExamQuestions
            .AsNoTracking()
            .Where(q => q.CourseId == shift.Session.CourseId && q.IsActive && q.ApprovalStatus == ApprovalStatus.Approved)
            .OrderBy(q => q.CreatedAt)
            .Take(5)
            .Select(q => q.Id)
            .ToListAsync(cancellationToken);

        if (approvedQuestions.Count == 0)
        {
            return null;
        }

        var structureId = shift.Session.ExamStructureId
            ?? await context.ExamStructures
                .Where(s => s.CourseId == shift.Session.CourseId && s.IsActive)
                .Select(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);

        if (structureId == Guid.Empty)
        {
            return null;
        }

        var autoExamSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = structureId,
            CourseId = shift.Session.CourseId,
            SetCode = $"AUTO-SET-{(seatNumber > 0 ? seatNumber : 1):D2}-{Guid.NewGuid().ToString("N")[..4]}",
            GeneratedAt = DateTime.UtcNow,
            IsActive = true
        };
        context.ExamSets.Add(autoExamSet);

        for (int i = 0; i < approvedQuestions.Count; i++)
        {
            context.ExamSetQuestions.Add(new ExamSetQuestion
            {
                Id = Guid.NewGuid(),
                ExamSetId = autoExamSet.Id,
                ExamQuestionId = approvedQuestions[i],
                OrderIndex = i + 1
            });
        }

        return autoExamSet.Id;
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
            ticket.CheckedInAt,
            ticket.ExamSetId);
    }
}
