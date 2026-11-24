using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Admin.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.Admin.Commands.ImportUsers;

public sealed record CommitAdminUsersImportCommand(
    Guid ActorAdminId,
    Stream FileStream,
    string? FileName,
    long? ContentLength = null,
    string? DefaultPassword = null)
    : IRequest<Result<AdminUserImportCommitResponseDto>>;

public sealed class CommitAdminUsersImportCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    IAdminUserSpreadsheetService spreadsheetService)
    : IRequestHandler<CommitAdminUsersImportCommand, Result<AdminUserImportCommitResponseDto>>
{
    public async Task<Result<AdminUserImportCommitResponseDto>> Handle(
        CommitAdminUsersImportCommand request,
        CancellationToken cancellationToken)
    {
        var defaultPasswordError = AdminUserImportEvaluator.ValidateDefaultPassword(request.DefaultPassword);
        if (defaultPasswordError is not null)
        {
            return Result<AdminUserImportCommitResponseDto>.Failure(defaultPasswordError);
        }

        var parseResult = await spreadsheetService.ParseAsync(
            request.FileStream,
            request.FileName,
            request.ContentLength,
            cancellationToken);

        if (!parseResult.IsSuccess)
        {
            return Result<AdminUserImportCommitResponseDto>.Failure(
                AdminUserImportEvaluator.MapParseError(parseResult));
        }

        var evaluated = await AdminUserImportEvaluator.EvaluateAsync(
            context,
            parseResult.Rows,
            request.DefaultPassword,
            generatePasswordsForValidRows: true,
            cancellationToken);

        var validEntries = evaluated.Where(item => item.Preview.IsValid).ToList();
        if (validEntries.Count == 0)
        {
            return Result<AdminUserImportCommitResponseDto>.Failure(
                "UNPROCESSABLE: Không có dòng hợp lệ nào để tạo tài khoản. Vui lòng kiểm tra danh sách lỗi và thử lại.");
        }

        var now = DateTime.UtcNow;
        var createdRows = new List<AdminUserImportCreatedRowDto>(validEntries.Count);
        var skippedRows = new List<AdminUserImportSkippedRowDto>(evaluated.Count - validEntries.Count);

        foreach (var entry in evaluated)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var preview = entry.Preview;
            if (!preview.IsValid)
            {
                skippedRows.Add(new AdminUserImportSkippedRowDto
                {
                    RowNumber = preview.RowNumber,
                    Email = preview.Email,
                    FullName = preview.FullName,
                    Role = preview.Role,
                    StudentCode = preview.StudentCode,
                    Reason = preview.ErrorMessage ?? "Dòng dữ liệu không hợp lệ."
                });
                continue;
            }

            var plainPassword = entry.EffectivePlainPassword!;
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = preview.Email,
                FullName = preview.FullName,
                PasswordHash = passwordHasher.Hash(plainPassword),
                Role = preview.Role,
                StudentCode = preview.StudentCode,
                Username = preview.Username,
                MustChangePassword = true,
                IsActive = true,
                AuthVersion = 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            context.Users.Add(user);

            createdRows.Add(new AdminUserImportCreatedRowDto
            {
                RowNumber = preview.RowNumber,
                UserId = userId,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                StudentCode = user.StudentCode,
                Username = user.Username,
                TemporaryPassword = plainPassword,
                PasswordSource = preview.PasswordSource,
                MustChangePassword = true
            });
        }

        var safeFileName = Path.GetFileName(request.FileName ?? "import.xlsx")
            .Replace(";", "_", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);

        var roleSummary = string.Join(
            ",",
            createdRows
                .GroupBy(r => r.Role)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => $"{g.Key}:{g.Count()}"));

        // AuditLog records counts and metadata only; temporary plaintext passwords are NEVER logged or stored.
        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorAdminId == Guid.Empty ? null : request.ActorAdminId,
            Action = "USERS_BULK_IMPORTED",
            EntityName = "users",
            EntityId = null,
            NewValues = $"fileName={safeFileName};totalRows={evaluated.Count};createdCount={createdRows.Count};skippedCount={skippedRows.Count};roles={roleSummary};mustChangePassword=true",
            CreatedAt = now
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (AdminUserImportEvaluator.IsUniqueConstraintViolation(ex))
        {
            return Result<AdminUserImportCommitResponseDto>.Failure(
                "CONFLICT: Một số Email, Mã sinh viên hoặc Tên đăng nhập vừa bị tạo đồng thời bởi thao tác khác. Vui lòng kiểm tra lại file và thử lại.");
        }

        var workbook = spreadsheetService.GenerateResultWorkbook(createdRows, skippedRows, now);

        return Result<AdminUserImportCommitResponseDto>.Success(new AdminUserImportCommitResponseDto
        {
            TotalRows = evaluated.Count,
            CreatedCount = createdRows.Count,
            SkippedCount = skippedRows.Count,
            ResultFileName = workbook.FileName,
            ResultFileContentType = workbook.ContentType,
            ResultFileBase64 = Convert.ToBase64String(workbook.Content),
            CreatedRows = createdRows,
            SkippedRows = skippedRows
        });
    }
}
