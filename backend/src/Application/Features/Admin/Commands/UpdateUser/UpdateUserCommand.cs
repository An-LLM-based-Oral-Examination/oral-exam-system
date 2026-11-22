using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Admin.DTOs;

namespace OralExamination.Application.Features.Admin.Commands.UpdateUser;

public sealed record UpdateUserCommand(
    Guid ActorAdminId,
    Guid TargetUserId,
    string Email,
    string FullName,
    string? Username = null,
    string? StudentCode = null) : IRequest<Result<AdminUserItemDto>>;
