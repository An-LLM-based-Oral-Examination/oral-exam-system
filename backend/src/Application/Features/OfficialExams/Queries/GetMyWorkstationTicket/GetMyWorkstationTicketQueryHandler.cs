using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetMyWorkstationTicket;

public sealed class GetMyWorkstationTicketQueryHandler : IRequestHandler<GetMyWorkstationTicketQuery, Result<StudentWorkstationTicketResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<GetMyWorkstationTicketQuery> _validator;

    public GetMyWorkstationTicketQueryHandler(
        IApplicationDbContext context,
        IValidator<GetMyWorkstationTicketQuery> validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Result<StudentWorkstationTicketResponse>> Handle(
        GetMyWorkstationTicketQuery request,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<StudentWorkstationTicketResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        var actor = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == request.ActorUserId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role != UserRole.Student)
        {
            return Result<StudentWorkstationTicketResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        if (!WorkstationAddress.TryNormalize(request.RemoteIpAddress, out _))
        {
            return Result<StudentWorkstationTicketResponse>.Failure(OfficialExamErrors.WorkstationUnknown);
        }

        var tickets = await _context.StudentExamTickets
            .AsNoTracking()
            .Include(item => item.Shift)
                .ThenInclude(shift => shift.Session)
            .Where(item => item.StudentId == actor.Id)
            .ToListAsync(cancellationToken);

        var onThisMachine = tickets
            .Where(item => WorkstationAddress.Same(item.IpAddress, request.RemoteIpAddress))
            .ToList();
        if (onThisMachine.Count == 0)
        {
            return Result<StudentWorkstationTicketResponse>.Failure(OfficialExamErrors.WorkstationTicketNotFound);
        }

        var openStatus = onThisMachine
            .Where(item => ExamShiftWindow.IsOpenStatus(item.Shift.Status))
            .ToList();
        if (openStatus.Count == 0)
        {
            return Result<StudentWorkstationTicketResponse>.Failure(OfficialExamErrors.ShiftClosed);
        }

        var inWindow = openStatus
            .Where(item => ExamShiftWindow.Contains(item.Shift, request.AtUtc))
            .ToList();
        if (inWindow.Count == 0)
        {
            return Result<StudentWorkstationTicketResponse>.Failure(OfficialExamErrors.ShiftNotOpen);
        }

        if (inWindow.Count > 1)
        {
            return Result<StudentWorkstationTicketResponse>.Failure(OfficialExamErrors.OverlappingShifts);
        }

        var ticket = inWindow[0];
        string? examSetCode = null;
        IReadOnlyList<WorkstationExamQuestionDto>? workstationQuestions = null;
        if (ticket.ExamSetId.HasValue)
        {
            examSetCode = await _context.ExamSets
                .AsNoTracking()
                .Where(es => es.Id == ticket.ExamSetId.Value)
                .Select(es => es.SetCode)
                .FirstOrDefaultAsync(cancellationToken);

            workstationQuestions = await _context.ExamSetQuestions
                .AsNoTracking()
                .Include(esq => esq.ExamQuestion)
                .Where(esq => esq.ExamSetId == ticket.ExamSetId.Value && esq.ExamQuestion != null)
                .OrderBy(esq => esq.OrderIndex)
                .Select(esq => new WorkstationExamQuestionDto(
                    esq.ExamQuestionId,
                    esq.OrderIndex,
                    esq.ExamQuestion.Title,
                    esq.ExamQuestion.Content,
                    esq.ExamQuestion.BloomLevel,
                    esq.ExamQuestion.Difficulty,
                    esq.ExamQuestion.ImageUrl,
                    esq.ExamQuestion.ImageDescription))
                .ToListAsync(cancellationToken);
        }

        return Result<StudentWorkstationTicketResponse>.Success(new StudentWorkstationTicketResponse(
            ticket.Id,
            ticket.ShiftId,
            ticket.Shift.ShiftName,
            ticket.Shift.RoomLab,
            ticket.Shift.StartTime,
            ticket.Shift.EndTime,
            ticket.Shift.Status,
            ticket.Shift.Session?.Title ?? string.Empty,
            ticket.StudentId,
            ticket.SeatNumber,
            ticket.Status,
            ticket.ExamSetId,
            examSetCode,
            ticket.Shift.Session?.ExamInputMode ?? "VoiceWithTranscriptEdit",
            ticket.Shift.Session?.TranscriptBufferSeconds ?? 60,
            ticket.Shift.Session?.HasFollowUp ?? false,
            ticket.Shift.Session?.MaxFollowUpQuestions ?? 2,
            workstationQuestions));
    }
}
