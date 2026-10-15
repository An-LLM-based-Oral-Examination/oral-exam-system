using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Auth.DTOs;

namespace OralExamination.Application.Features.Auth.Commands.PasswordLogin;

public sealed record PasswordLoginCommand(string? Username, string? Password) : IRequest<Result<PasswordLoginResponse>>;
