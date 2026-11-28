using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Admin.DTOs;

namespace OralExamination.Application.Features.Admin.Commands.ImportUsers;

public sealed record PreviewAdminUsersImportQuery(
    Stream FileStream,
    string? FileName,
    long? ContentLength = null,
    string? DefaultPassword = null)
    : IRequest<Result<AdminUserImportPreviewResponseDto>>;

public sealed class PreviewAdminUsersImportQueryHandler(
    IApplicationDbContext context,
    IAdminUserSpreadsheetService spreadsheetService)
    : IRequestHandler<PreviewAdminUsersImportQuery, Result<AdminUserImportPreviewResponseDto>>
{
    public async Task<Result<AdminUserImportPreviewResponseDto>> Handle(
        PreviewAdminUsersImportQuery request,
        CancellationToken cancellationToken)
    {
        var defaultPasswordError = AdminUserImportEvaluator.ValidateDefaultPassword(request.DefaultPassword);
        if (defaultPasswordError is not null)
        {
            return Result<AdminUserImportPreviewResponseDto>.Failure(defaultPasswordError);
        }

        var parseResult = await spreadsheetService.ParseAsync(
            request.FileStream,
            request.FileName,
            request.ContentLength,
            cancellationToken);

        if (!parseResult.IsSuccess)
        {
            return Result<AdminUserImportPreviewResponseDto>.Failure(
                AdminUserImportEvaluator.MapParseError(parseResult));
        }

        var evaluated = await AdminUserImportEvaluator.EvaluateAsync(
            context,
            parseResult.Rows,
            request.DefaultPassword,
            generatePasswordsForValidRows: false,
            cancellationToken);

        var previewRows = evaluated.Select(item => item.Preview).ToList();
        var validCount = previewRows.Count(r => r.IsValid);
        var duplicateCount = previewRows.Count(r => r.Status == "DUPLICATE");
        var invalidCount = previewRows.Count(r => r.Status == "INVALID");

        return Result<AdminUserImportPreviewResponseDto>.Success(new AdminUserImportPreviewResponseDto
        {
            FileName = Path.GetFileName(request.FileName ?? string.Empty),
            TotalRows = previewRows.Count,
            ValidCount = validCount,
            InvalidCount = invalidCount,
            DuplicateCount = duplicateCount,
            SkippedCount = invalidCount + duplicateCount,
            HasDefaultPassword = !string.IsNullOrEmpty(request.DefaultPassword),
            Rows = previewRows
        });
    }
}
