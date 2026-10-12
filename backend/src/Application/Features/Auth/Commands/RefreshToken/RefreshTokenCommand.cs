using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Auth.DTOs;

namespace OralExamination.Application.Features.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string? RefreshToken) : IRequest<Result<RefreshTokenResponse>>;
