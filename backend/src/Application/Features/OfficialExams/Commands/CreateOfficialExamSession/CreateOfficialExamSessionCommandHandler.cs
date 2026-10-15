using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.CreateOfficialExamSession;

public sealed class CreateOfficialExamSessionCommandHandler : IRequestHandler<CreateOfficialExamSessionCommand, Result<OfficialExamSessionResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<CreateOfficialExamSessionCommand> _validator;

    public CreateOfficialExamSessionCommandHandler(
        IApplicationDbContext context,
        IValidator<CreateOfficialExamSessionCommand> validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Result<OfficialExamSessionResponse>> Handle(CreateOfficialExamSessionCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<OfficialExamSessionResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        var actor = await _context.Users.FirstOrDefaultAsync(user => user.Id == request.ActorUserId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role != UserRole.DepartmentHead)
        {
            return Result<OfficialExamSessionResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        var courseExists = await _context.Courses.AnyAsync(
            course => course.Id == request.CourseId && course.IsActive,
            cancellationToken);
        if (!courseExists)
        {
            return Result<OfficialExamSessionResponse>.Failure(OfficialExamErrors.CourseNotFound);
        }

        var structureMatchesCourse = await _context.ExamStructures.AnyAsync(
            structure => structure.Id == request.ExamStructureId
                && structure.CourseId == request.CourseId
                && structure.IsActive,
            cancellationToken);
        if (!structureMatchesCourse)
        {
            return Result<OfficialExamSessionResponse>.Failure(OfficialExamErrors.StructureNotFound);
        }

        var session = new OfficialExamSession
        {
            CourseId = request.CourseId,
            ExamStructureId = request.ExamStructureId,
            Title = request.Title!.Trim(),
            ExamDate = request.ExamDate,
            HasFollowUp = request.HasFollowUp,
            MaxFollowUpQuestions = request.MaxFollowUpQuestions,
            ExamInputMode = request.ExamInputMode!,
            TranscriptBufferSeconds = request.TranscriptBufferSeconds,
            Status = "scheduled",
            CreatedBy = actor.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.OfficialExamSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<OfficialExamSessionResponse>.Success(new OfficialExamSessionResponse(
            session.Id,
            session.CourseId,
            session.ExamStructureId,
            session.Title,
            session.ExamDate,
            session.HasFollowUp,
            session.MaxFollowUpQuestions,
            session.ExamInputMode,
            session.TranscriptBufferSeconds,
            session.Status));
    }
}
