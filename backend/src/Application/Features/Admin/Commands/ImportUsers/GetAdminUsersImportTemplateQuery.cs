using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Admin.DTOs;

namespace OralExamination.Application.Features.Admin.Commands.ImportUsers;

public sealed record GetAdminUsersImportTemplateQuery(string Format = "xlsx")
    : IRequest<Result<AdminUserSpreadsheetFile>>;

public sealed class GetAdminUsersImportTemplateQueryHandler(
    IAdminUserSpreadsheetService spreadsheetService)
    : IRequestHandler<GetAdminUsersImportTemplateQuery, Result<AdminUserSpreadsheetFile>>
{
    public Task<Result<AdminUserSpreadsheetFile>> Handle(
        GetAdminUsersImportTemplateQuery request,
        CancellationToken cancellationToken)
    {
        var normalizedFormat = string.IsNullOrWhiteSpace(request.Format)
            ? "xlsx"
            : request.Format.Trim().ToLowerInvariant();

        if (normalizedFormat is not ("xlsx" or "csv"))
        {
            return Task.FromResult(Result<AdminUserSpreadsheetFile>.Failure(
                "VALIDATION: Định dạng file mẫu chỉ hỗ trợ 'xlsx' hoặc 'csv'."));
        }

        var file = spreadsheetService.GenerateTemplate(normalizedFormat);
        return Task.FromResult(Result<AdminUserSpreadsheetFile>.Success(file));
    }
}
