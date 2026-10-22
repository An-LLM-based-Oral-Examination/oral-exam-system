using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Admin.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.Admin.Commands.UpdateSystemConfig;

public sealed record UpdateSystemConfigCommand(
    Guid ActorAdminId,
    string Key,
    string Value,
    string? Description = null) : IRequest<Result<SystemConfigItemDto>>;

public sealed class UpdateSystemConfigCommandHandler
    : IRequestHandler<UpdateSystemConfigCommand, Result<SystemConfigItemDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdateSystemConfigCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SystemConfigItemDto>> Handle(
        UpdateSystemConfigCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Value))
        {
            return Result<SystemConfigItemDto>.Failure(
                "VALIDATION: Khóa cấu hình (Key) và giá trị (Value) không được để trống.");
        }

        var key = request.Key.Trim();
        var value = request.Value.Trim();

        var validationError = ValidateConfigInvariant(key, value);
        if (validationError is not null)
        {
            return Result<SystemConfigItemDto>.Failure($"VALIDATION: {validationError}");
        }

        if (key == "MinMixedPracticeQuestions" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minQ)) { var maxCfg = await _context.SystemConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Key == "MaxMixedPracticeQuestions", cancellationToken); var maxExisting = maxCfg != null && int.TryParse(maxCfg.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMax) ? parsedMax : 10; if (minQ > maxExisting) return Result<SystemConfigItemDto>.Failure($"VALIDATION: MinMixedPracticeQuestions ({minQ}) không được lớn hơn MaxMixedPracticeQuestions hiện tại ({maxExisting})."); } else if (key == "MaxMixedPracticeQuestions" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxQ)) { var minCfg = await _context.SystemConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Key == "MinMixedPracticeQuestions", cancellationToken); var minExisting = minCfg != null && int.TryParse(minCfg.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMin) ? parsedMin : 3; if (maxQ < minExisting) return Result<SystemConfigItemDto>.Failure($"VALIDATION: MaxMixedPracticeQuestions ({maxQ}) không được nhỏ hơn MinMixedPracticeQuestions hiện tại ({minExisting})."); } var config = await _context.SystemConfigs
            .FirstOrDefaultAsync(c => c.Key == key, cancellationToken);

        var now = DateTime.UtcNow;
        var oldValue = config?.Value;

        if (config is null)
        {
            config = new SystemConfig
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = value,
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                UpdatedAt = now
            };
            _context.SystemConfigs.Add(config);
        }
        else
        {
            config.Value = value;
            if (!string.IsNullOrWhiteSpace(request.Description))
            {
                config.Description = request.Description.Trim();
            }
            config.UpdatedAt = now;
        }

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorAdminId == Guid.Empty ? null : request.ActorAdminId,
            Action = "SYSTEM_CONFIG_UPDATED",
            EntityName = "system_configs",
            EntityId = config.Id,
            OldValues = oldValue is null ? null : $"{key}={oldValue}",
            NewValues = $"{key}={value}",
            CreatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result<SystemConfigItemDto>.Success(new SystemConfigItemDto
        {
            Id = config.Id,
            Key = config.Key,
            Value = config.Value,
            Description = config.Description,
            UpdatedAt = config.UpdatedAt
        });
    }

    private static string? ValidateConfigInvariant(string key, string value)
    {
        switch (key)
        {
            case "TranscriptBufferSeconds":
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bufferSec) ||
                    bufferSec < 10 || bufferSec > 300)
                {
                    return "TranscriptBufferSeconds bắt buộc là số nguyên trong khoảng từ 10 đến 300 giây.";
                }
                break;

            case "MaxPracticeFollowUpQuestions":
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxFollowUp) ||
                    maxFollowUp < 1 || maxFollowUp > 5)
                {
                    return "MaxPracticeFollowUpQuestions bắt buộc là số nguyên trong khoảng từ 1 đến 5 câu.";
                }
                break;

            case "MinMixedPracticeQuestions":
            case "MaxMixedPracticeQuestions":
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mixedCount) ||
                    mixedCount < 3 || mixedCount > 10)
                {
                    return $"{key} bắt buộc là số nguyên trong khoảng từ 3 đến 10 câu.";
                }
                break;

            case "SessionInactivityTimeoutMinutes":
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutMin) ||
                    timeoutMin < 1 || timeoutMin > 60)
                {
                    return "SessionInactivityTimeoutMinutes bắt buộc là số nguyên trong khoảng từ 1 đến 60 phút.";
                }
                break;

            case "max_mock_exams_per_day":
            case "MaxMockExamsPerDay":
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quota) ||
                    quota < 1 || quota > 50)
                {
                    return "Hạn ngạch thi thử mỗi ngày (max_mock_exams_per_day) phải là số nguyên từ 1 đến 50.";
                }
                break;
        }

        return null;
    }
}
