using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Auth.DTOs;

namespace OralExamination.Application.Features.Auth.Commands.ChangePassword;

public sealed record ChangePasswordCommand(Guid UserId, string? CurrentPassword, string? NewPassword)
    : IRequest<Result<ChangePasswordResponse>>;
