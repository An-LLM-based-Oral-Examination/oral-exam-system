using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Auth.DTOs;

namespace OralExamination.Application.Features.Auth.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<Result<CurrentUserDto>>;
