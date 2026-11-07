using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Notifications.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Notifications.Queries.GetNotifications;

public sealed record GetNotificationsQuery(
    Guid UserId,
    bool? UnreadOnly = null,
    int Limit = 50,
    int Page = 1,
    int PageSize = 0) : IRequest<Result<NotificationsSummaryResponseDto>>;

public sealed class GetNotificationsQueryHandler
    : IRequestHandler<GetNotificationsQuery, Result<NotificationsSummaryResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public GetNotificationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<NotificationsSummaryResponseDto>> Handle(
        GetNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var userExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == request.UserId, cancellationToken);

        if (!userExists)
        {
            return Result<NotificationsSummaryResponseDto>.Failure("NOT_FOUND: Không tìm thấy thông tin người dùng.");
        }

        var baseQuery = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == request.UserId);

        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var unreadCount = await baseQuery.CountAsync(n => !n.IsRead, cancellationToken);

        var query = baseQuery;
        if (request.UnreadOnly == true)
        {
            query = query.Where(n => !n.IsRead);
        }

        query = query.OrderByDescending(n => n.CreatedAt);

        int page;
        int pageSize;
        if (request.PageSize > 0)
        {
            page = Math.Max(1, request.Page);
            pageSize = Math.Clamp(request.PageSize, 1, 50);
        }
        else
        {
            page = 1;
            pageSize = Math.Clamp(request.Limit, 1, 100);
        }

        var dbItems = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = dbItems.Select(n =>
        {
            IReadOnlyDictionary<string, string?>? metadata = null;
            if (!string.IsNullOrWhiteSpace(n.MetadataJson))
            {
                try
                {
                    metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(n.MetadataJson);
                }
                catch
                {
                    metadata = null;
                }
            }

            if (metadata == null && !string.IsNullOrWhiteSpace(n.RelatedEntityName) && n.RelatedEntityId.HasValue)
            {
                var dict = new Dictionary<string, string?>
                {
                    ["entityName"] = n.RelatedEntityName,
                    ["entityId"] = n.RelatedEntityId.Value.ToString()
                };

                if (string.Equals(n.RelatedEntityName, "student_exam_tickets", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(n.RelatedEntityName, "student_exam_ticket", StringComparison.OrdinalIgnoreCase))
                {
                    dict["ticketId"] = n.RelatedEntityId.Value.ToString();
                }
                else if (string.Equals(n.RelatedEntityName, "appeal_requests", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(n.RelatedEntityName, "appeal_request", StringComparison.OrdinalIgnoreCase))
                {
                    dict["appealId"] = n.RelatedEntityId.Value.ToString();
                }
                else if (string.Equals(n.RelatedEntityName, "exam_questions", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(n.RelatedEntityName, "exam_question", StringComparison.OrdinalIgnoreCase))
                {
                    dict["questionId"] = n.RelatedEntityId.Value.ToString();
                }

                metadata = dict;
            }


            return new NotificationItemDto
            {
                Id = n.Id,
                UserId = n.UserId,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                RelatedEntityName = n.RelatedEntityName,
                RelatedEntityId = n.RelatedEntityId,
                Metadata = metadata,
                IsRead = n.IsRead,
                ReadAt = n.ReadAt,
                CreatedAt = n.CreatedAt
            };
        }).ToList();

        return Result<NotificationsSummaryResponseDto>.Success(new NotificationsSummaryResponseDto
        {
            Items = items,
            UnreadCount = unreadCount,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }
}

