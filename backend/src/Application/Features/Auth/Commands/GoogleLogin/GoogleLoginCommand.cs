using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Auth.DTOs;

namespace OralExamination.Application.Features.Auth.Commands.GoogleLogin;

public sealed record GoogleLoginCommand(string? IdToken, string? BrowserNonce = null) : IRequest<Result<GoogleLoginResponse>>;
