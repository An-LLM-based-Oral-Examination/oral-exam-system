using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.ImportExamRoster;

public sealed class PreviewExamRosterImportQueryHandler(IApplicationDbContext context)
    : IRequestHandler<PreviewExamRosterImportQuery, Result<BulkExamTicketImportPreview>>
{
    public async Task<Result<BulkExamTicketImportPreview>> Handle(
        PreviewExamRosterImportQuery request,
        CancellationToken cancellationToken)
    {
        var validation = await BulkExamRosterImport.ValidateAsync(context, request.ActorUserId, request.ShiftId,
            request.Rows, cancellationToken);
        return validation.IsSuccess
            ? Result<BulkExamTicketImportPreview>.Success(validation.Value.Preview)
            : Result<BulkExamTicketImportPreview>.Failure(validation.Error);
    }
}

public sealed class CommitExamRosterImportCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CommitExamRosterImportCommand, Result<BulkExamTicketImportCommitResult>>
{
    public Task<Result<BulkExamTicketImportCommitResult>> Handle(
        CommitExamRosterImportCommand request,
        CancellationToken cancellationToken) =>
        context.ExecuteBulkExamTicketImportAsync(request.ShiftId,
            ct => CommitLockedAsync(request, ct), cancellationToken);

    private async Task<Result<BulkExamTicketImportCommitResult>> CommitLockedAsync(
        CommitExamRosterImportCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await BulkExamRosterImport.ValidateAsync(context, request.ActorUserId, request.ShiftId,
            request.Rows, cancellationToken);
        if (!validation.IsSuccess)
            return Result<BulkExamTicketImportCommitResult>.Failure(validation.Error);
        if (!validation.Value.Preview.CanCommit)
            return Result<BulkExamTicketImportCommitResult>.Failure("CONFLICT: Dữ liệu import không còn hợp lệ; hãy xem preview và sửa toàn bộ lỗi trước khi xác nhận.");

        var tickets = validation.Value.Rows.Zip(validation.Value.ExamSets, (row, examSet) => new StudentExamTicket
        {
            ShiftId = request.ShiftId,
            StudentId = row.StudentId,
            ExamSetId = examSet.Id,
            SeatNumber = row.SeatNumber,
            IpAddress = row.IpAddress,
            Status = ExamTicketStatus.Scheduled,
            IsLocked = false,
            CheckedInAt = DateTime.UtcNow
        }).ToList();

        context.StudentExamTickets.AddRange(tickets);
        context.AuditLogs.Add(new AuditLog
        {
            UserId = request.ActorUserId,
            Action = "BULK_EXAM_TICKETS_IMPORTED",
            EntityName = nameof(RealExamSessionShift),
            EntityId = request.ShiftId,
            NewValues = JsonSerializer.Serialize(new
            {
                count = tickets.Count,
                studentCodes = validation.Value.Rows.Select(row => row.StudentCode).ToArray(),
                examSetCodes = validation.Value.ExamSets.Select(set => set.SetCode).ToArray()
            })
        });
        await context.SaveChangesAsync(cancellationToken);

        return Result<BulkExamTicketImportCommitResult>.Success(new BulkExamTicketImportCommitResult(
            request.ShiftId,
            tickets.Count,
            tickets.Select(ticket => new ExamTicketResponse(ticket.Id, ticket.ShiftId, ticket.StudentId,
                ticket.SeatNumber, ticket.IpAddress, ticket.Status, ticket.CheckedInAt, ticket.ExamSetId)).ToList()));
    }
}

internal static class BulkExamRosterImport
{
    private const int MaxRows = 500;

    internal sealed record ValidatedRow(string StudentCode, Guid StudentId, int SeatNumber, string IpAddress);
    internal sealed record ValidationState(
        BulkExamTicketImportPreview Preview,
        IReadOnlyList<ValidatedRow> Rows,
        IReadOnlyList<ExamSet> ExamSets);

    internal static async Task<Result<ValidationState>> ValidateAsync(
        IApplicationDbContext context,
        Guid actorUserId,
        Guid shiftId,
        IReadOnlyList<BulkExamTicketImportRow> inputRows,
        CancellationToken cancellationToken)
    {
        var actor = await context.Users.AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == actorUserId && user.IsActive, cancellationToken);
        if (actor is null || actor.Role != UserRole.ExaminationOfficer)
            return Result<ValidationState>.Failure(OfficialExamErrors.Forbidden);

        var shift = await context.RealExamSessionShifts.AsNoTracking()
            .Include(item => item.Session)
            .FirstOrDefaultAsync(item => item.Id == shiftId, cancellationToken);
        if (shift?.Session is null)
            return Result<ValidationState>.Failure(OfficialExamErrors.ShiftNotFound);

        var issues = new List<BulkExamTicketImportIssue>();
        if (inputRows.Count is 0 or > MaxRows)
            issues.Add(new(null, $"Tệp phải chứa từ 1 đến {MaxRows} dòng dữ liệu."));

        var normalized = new List<(int SourceRow, string StudentCode, int SeatNumber, string IpAddress)>();
        foreach (var row in inputRows)
        {
            var studentCode = row.StudentCode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(studentCode))
                issues.Add(new(row.SourceRow, "MSSV không được để trống."));
            if (row.SeatNumber < OfficialExamRules.MinSeat || row.SeatNumber > shift.MaxSeats)
                issues.Add(new(row.SourceRow,
                    $"Số ghế phải nằm trong khoảng {OfficialExamRules.MinSeat} đến {shift.MaxSeats} của ca thi."));
            if (!WorkstationAddress.TryNormalize(row.IpAddress, out var ipAddress))
            {
                issues.Add(new(row.SourceRow, OfficialExamErrors.IpInvalid));
                ipAddress = string.Empty;
            }
            normalized.Add((row.SourceRow, studentCode, row.SeatNumber, ipAddress));
        }

        AddDuplicates(normalized, item => item.StudentCode, "MSSV bị lặp trong tệp.", issues);
        AddDuplicates(normalized, item => item.SeatNumber.ToString(), "Số ghế bị lặp trong tệp.", issues);
        AddDuplicates(normalized.Where(item => item.IpAddress.Length > 0).ToList(), item => item.IpAddress,
            "Địa chỉ IP máy trạm bị lặp trong tệp.", issues);

        var studentCodes = normalized.Where(item => item.StudentCode.Length > 0)
            .Select(item => item.StudentCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var students = await context.Users.AsNoTracking()
            .Where(user => user.StudentCode != null && studentCodes.Contains(user.StudentCode))
            .Select(user => new { user.Id, user.StudentCode, user.FullName, user.Role, user.IsActive })
            .ToListAsync(cancellationToken);
        var studentsByCode = students.ToDictionary(user => user.StudentCode!, StringComparer.OrdinalIgnoreCase);

        var existingTickets = await context.StudentExamTickets.AsNoTracking()
            .Where(ticket => ticket.ShiftId == shiftId)
            .Select(ticket => new { ticket.StudentId, ticket.SeatNumber, ticket.IpAddress, ticket.ExamSetId })
            .ToListAsync(cancellationToken);
        if (existingTickets.Count + normalized.Count > shift.MaxSeats)
            issues.Add(new(null,
                $"Import vượt sức chứa ca thi: đã có {existingTickets.Count} vé, tệp có {normalized.Count} dòng, tối đa {shift.MaxSeats}."));
        var validRows = new List<ValidatedRow>();
        foreach (var row in normalized)
        {
            if (!studentsByCode.TryGetValue(row.StudentCode, out var student) || !student.IsActive || student.Role != UserRole.Student)
            {
                issues.Add(new(row.SourceRow, "Không tìm thấy sinh viên đang hoạt động có MSSV này."));
                continue;
            }
            if (existingTickets.Any(ticket => ticket.StudentId == student.Id))
                issues.Add(new(row.SourceRow, "Sinh viên đã có vé trong ca thi này."));
            if (existingTickets.Any(ticket => ticket.SeatNumber == row.SeatNumber))
                issues.Add(new(row.SourceRow, OfficialExamErrors.SeatTaken));
            if (existingTickets.Any(ticket => WorkstationAddress.Same(ticket.IpAddress, row.IpAddress)))
                issues.Add(new(row.SourceRow, "Địa chỉ IP máy trạm đã được gán trong ca thi này."));
            validRows.Add(new(row.StudentCode, student.Id, row.SeatNumber, row.IpAddress));
        }

        var candidateSets = await context.ExamSets.AsNoTracking()
            .Where(set => set.IsActive && set.Status == ExamSetStatus.HandedOver &&
                set.CourseId == shift.Session.CourseId &&
                set.SessionId == shift.SessionId)
            .OrderBy(set => set.HandedOverAt).ThenBy(set => set.Id)
            .ToListAsync(cancellationToken);
        var assignedSetIds = existingTickets.Where(ticket => ticket.ExamSetId.HasValue)
            .Select(ticket => ticket.ExamSetId!.Value).ToHashSet();
        var availableSets = candidateSets.Where(set => !assignedSetIds.Contains(set.Id)).ToList();
        if (issues.Count == 0 && availableSets.Count < validRows.Count)
            issues.Add(new(null, $"Thiếu bộ đề HANDED_OVER chưa gán: cần {validRows.Count}, hiện có {availableSets.Count}. Không tạo vé một phần."));

        var orderedIssues = issues.OrderBy(issue => issue.SourceRow ?? 0).ToList();
        var previewRows = normalized.Select(row => new BulkExamTicketImportPreviewRow(
            row.SourceRow,
            row.StudentCode,
            studentsByCode.TryGetValue(row.StudentCode, out var student) ? student.FullName : null,
            row.SeatNumber,
            row.IpAddress,
            orderedIssues.Where(issue => issue.SourceRow == row.SourceRow)
                .Select(issue => issue.Message).ToList())).ToList();
        var preview = new BulkExamTicketImportPreview(shiftId, validRows.Count, availableSets.Count,
            orderedIssues,
            availableSets.Take(validRows.Count).Select(set => set.SetCode).ToList(),
            previewRows);
        return Result<ValidationState>.Success(new ValidationState(preview, validRows, availableSets.Take(validRows.Count).ToList()));
    }

    private static void AddDuplicates<T>(IReadOnlyList<(int SourceRow, string StudentCode, int SeatNumber, string IpAddress)> rows,
        Func<(int SourceRow, string StudentCode, int SeatNumber, string IpAddress), T> keySelector,
        string message,
        ICollection<BulkExamTicketImportIssue> issues) where T : notnull
    {
        foreach (var duplicate in rows.GroupBy(keySelector).Where(group => group.Count() > 1))
            foreach (var row in duplicate)
                issues.Add(new(row.SourceRow, message));
    }
}
