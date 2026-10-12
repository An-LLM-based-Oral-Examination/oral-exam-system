using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.Auth.Commands.SignOut;

public sealed record SignOutCommand(string? RefreshToken) : IRequest<Result<bool>>;
