using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Auth.DTOs;

namespace OralExamination.Application.Features.Auth.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserDto>>
{
    public async Task<Result<CurrentUserDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await context.Users.AsNoTracking()
            .Where(user => user.Id == request.UserId)
            .Select(user => new CurrentUserDto(
                user.Id, user.Email, user.Username, user.FullName, user.StudentCode,
                user.Role, user.IsActive, user.MustChangePassword))
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Result<CurrentUserDto>.Failure(AuthErrors.AccountNotFound);
        }

        return user.IsActive
            ? Result<CurrentUserDto>.Success(user)
            : Result<CurrentUserDto>.Failure(AuthErrors.AccountLocked);
    }
}
