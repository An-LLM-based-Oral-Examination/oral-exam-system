using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Admin.DTOs;

namespace OralExamination.Application.Features.Admin.Commands.CreateUser;

public sealed record CreateUserCommand(
    Guid ActorAdminId,
    string Email,
    string FullName,
    string Password,
    string Role,
    string? StudentCode = null,
    string? Department = null) : IRequest<Result<AdminUserItemDto>>;
