using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.ConfigureOfficialExamFollowUp;

public sealed class ConfigureOfficialExamFollowUpCommandHandler(
    IApplicationDbContext context,
    IValidator<ConfigureOfficialExamFollowUpCommand> validator)
    : IRequestHandler<ConfigureOfficialExamFollowUpCommand, Result<OfficialExamSessionResponse>>
{
    private readonly IApplicationDbContext _context = context;
    private readonly IValidator<ConfigureOfficialExamFollowUpCommand> _validator = validator;

    public async Task<Result<OfficialExamSessionResponse>> Handle(
        ConfigureOfficialExamFollowUpCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<OfficialExamSessionResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        var actor = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);
        if (actor is null || !actor.IsActive || (actor.Role != UserRole.DepartmentHead && actor.Role != UserRole.Admin))
        {
            return Result<OfficialExamSessionResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        var session = await _context.OfficialExamSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session is null)
        {
            return Result<OfficialExamSessionResponse>.Failure(OfficialExamErrors.SessionNotFound);
        }

        session.HasFollowUp = request.HasFollowUp;
        session.MaxFollowUpQuestions = request.MaxFollowUpQuestions;

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
