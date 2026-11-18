using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.BatchOpenExamShifts;

public sealed class BatchOpenExamShiftsCommandHandler(IApplicationDbContext context)
    : IRequestHandler<BatchOpenExamShiftsCommand, Result<BatchOpenExamShiftsResponse>>
{
    public async Task<Result<BatchOpenExamShiftsResponse>> Handle(
        BatchOpenExamShiftsCommand request,
        CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveOfficerOrAdminAsync(context, request.ActorUserId, cancellationToken))
        {
            return Result<BatchOpenExamShiftsResponse>.Failure($"FORBIDDEN: {OfficialExamErrors.Forbidden}");
        }

        if (request.SessionId == Guid.Empty)
        {
            return Result<BatchOpenExamShiftsResponse>.Failure($"NOT_FOUND: {OfficialExamErrors.SessionNotFound}");
        }

        if (string.IsNullOrWhiteSpace(request.ShiftGroupName))
        {
            return Result<BatchOpenExamShiftsResponse>.Failure("BAD_REQUEST: Tên nhóm ca thi (shiftGroupName) không được để trống.");
        }

        if (request.StartTime == default || request.EndTime == default)
        {
            return Result<BatchOpenExamShiftsResponse>.Failure("BAD_REQUEST: Thời gian bắt đầu và kết thúc ca thi là bắt buộc.");
        }

        var startTimeUtc = OfficialExamSchedulingHelper.NormalizeUtc(request.StartTime);
        var endTimeUtc = OfficialExamSchedulingHelper.NormalizeUtc(request.EndTime);

        if (endTimeUtc <= startTimeUtc)
        {
            return Result<BatchOpenExamShiftsResponse>.Failure("BAD_REQUEST: Giờ kết thúc ca thi phải lớn hơn giờ bắt đầu.");
        }

        if (request.Rooms is null || request.Rooms.Count == 0)
        {
            return Result<BatchOpenExamShiftsResponse>.Failure("BAD_REQUEST: Cần khai báo ít nhất 1 phòng thi (rooms) cho ca thi.");
        }

        var normalizedGroupName = request.ShiftGroupName.Trim();
        var seenRooms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenStaffIdsInBatch = new HashSet<Guid>();

        foreach (var room in request.Rooms)
        {
            if (room is null || string.IsNullOrWhiteSpace(room.RoomLab))
            {
                return Result<BatchOpenExamShiftsResponse>.Failure("BAD_REQUEST: Mã phòng Lab (roomLab) không được để trống.");
            }

            var trimmedLab = room.RoomLab.Trim();
            if (trimmedLab.Length > 50)
            {
                return Result<BatchOpenExamShiftsResponse>.Failure("BAD_REQUEST: Mã phòng Lab không được vượt quá 50 ký tự.");
            }

            var combinedShiftName = $"{normalizedGroupName} - {trimmedLab}";
            if (combinedShiftName.Length > 100)
            {
                return Result<BatchOpenExamShiftsResponse>.Failure("BAD_REQUEST: Tổng độ dài tên ca và mã phòng Lab không được vượt quá 100 ký tự.");
            }

            if (room.MaxSeats < OfficialExamRules.MinSeat || room.MaxSeats > OfficialExamRules.DefaultShiftCapacity)
            {
                return Result<BatchOpenExamShiftsResponse>.Failure(
                    $"UNPROCESSABLE: Sức chứa phòng thi chuẩn phải từ {OfficialExamRules.MinSeat} đến {OfficialExamRules.DefaultShiftCapacity} sinh viên/phòng.");
            }

            if (!seenRooms.Add(trimmedLab))
            {
                return Result<BatchOpenExamShiftsResponse>.Failure(
                    $"CONFLICT: Mã phòng thi '{trimmedLab}' bị khai báo trùng lặp trong cùng một ca thi.");
            }

            if (room.ProctorUserId.HasValue && room.ProctorUserId.Value == Guid.Empty)
            {
                return Result<BatchOpenExamShiftsResponse>.Failure("UNPROCESSABLE: Mã Giám thị (proctorUserId) không hợp lệ.");
            }

            if (room.ExaminerUserId.HasValue && room.ExaminerUserId.Value == Guid.Empty)
            {
                return Result<BatchOpenExamShiftsResponse>.Failure("UNPROCESSABLE: Mã Giám khảo (examinerUserId) không hợp lệ.");
            }

            if (room.ProctorUserId.HasValue && room.ExaminerUserId.HasValue
                && room.ProctorUserId.Value == room.ExaminerUserId.Value)
            {
                return Result<BatchOpenExamShiftsResponse>.Failure(
                    "UNPROCESSABLE: Giám thị coi thi và Giảng viên chấm thi trong cùng một phòng không được trùng một người.");
            }

            if (room.ProctorUserId.HasValue && !seenStaffIdsInBatch.Add(room.ProctorUserId.Value))
            {
                return Result<BatchOpenExamShiftsResponse>.Failure(
                    "CONFLICT: Một cán bộ không thể coi thi hoặc chấm thi đồng thời ở nhiều phòng trong cùng một ca.");
            }

            if (room.ExaminerUserId.HasValue && !seenStaffIdsInBatch.Add(room.ExaminerUserId.Value))
            {
                return Result<BatchOpenExamShiftsResponse>.Failure(
                    "CONFLICT: Một cán bộ không thể coi thi hoặc chấm thi đồng thời ở nhiều phòng trong cùng một ca.");
            }
        }

        return await context.ExecuteOfficialShiftScheduleBatchAsync(
            ct => ExecuteLockedAsync(request, normalizedGroupName, startTimeUtc, endTimeUtc, ct),
            cancellationToken);
    }

    private async Task<Result<BatchOpenExamShiftsResponse>> ExecuteLockedAsync(
        BatchOpenExamShiftsCommand request,
        string normalizedGroupName,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        CancellationToken cancellationToken)
    {
        var session = await context.OfficialExamSessions
            .AsNoTracking()
            .Where(item => item.Id == request.SessionId)
            .Select(item => new { item.Id, item.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return Result<BatchOpenExamShiftsResponse>.Failure($"NOT_FOUND: {OfficialExamErrors.SessionNotFound}");
        }

        if (string.Equals(session.Status, "concluded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(session.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return Result<BatchOpenExamShiftsResponse>.Failure(
                "CONFLICT: Không thể mở thêm ca thi cho kỳ thi đã kết thúc hoặc đã hủy.");
        }

        var proctorIds = request.Rooms!
            .Where(room => room.ProctorUserId.HasValue)
            .Select(room => room.ProctorUserId!.Value)
            .Distinct()
            .ToList();

        var examinerIds = request.Rooms!
            .Where(room => room.ExaminerUserId.HasValue)
            .Select(room => room.ExaminerUserId!.Value)
            .Distinct()
            .ToList();

        var referencedUserIds = proctorIds.Concat(examinerIds).Distinct().ToList();
        var referencedUsers = referencedUserIds.Count == 0
            ? new Dictionary<Guid, (string FullName, string Role, bool IsActive)>()
            : (await context.Users
                .AsNoTracking()
                .Where(user => referencedUserIds.Contains(user.Id))
                .Select(user => new { user.Id, user.FullName, user.Role, user.IsActive })
                .ToListAsync(cancellationToken))
                .ToDictionary(user => user.Id, user => (user.FullName, user.Role, user.IsActive));

        foreach (var proctorId in proctorIds)
        {
            if (!referencedUsers.TryGetValue(proctorId, out var user)
                || !user.IsActive
                || user.Role != UserRole.Proctor)
            {
                return Result<BatchOpenExamShiftsResponse>.Failure(
                    "UNPROCESSABLE: Cán bộ coi thi (proctorUserId) bắt buộc phải là tài khoản đang hoạt động có vai trò Giám thị (proctor).");
            }
        }

        foreach (var examinerId in examinerIds)
        {
            if (!referencedUsers.TryGetValue(examinerId, out var user)
                || !user.IsActive
                || user.Role != UserRole.Lecturer)
            {
                return Result<BatchOpenExamShiftsResponse>.Failure(
                    "UNPROCESSABLE: Giảng viên chấm thi (examinerUserId) bắt buộc phải là tài khoản đang hoạt động có vai trò Giảng viên (lecturer).");
            }
        }

        var overlappingShifts = await context.RealExamSessionShifts
            .AsNoTracking()
            .Where(existing =>
                existing.Status.ToLower() != "cancelled"
                && existing.StartTime < endTimeUtc
                && startTimeUtc < existing.EndTime)
            .Select(existing => new
            {
                existing.Id,
                existing.RoomLab,
                existing.ProctorUserId,
                existing.ExaminerUserId
            })
            .ToListAsync(cancellationToken);

        var occupiedRoomSet = overlappingShifts
            .Select(item => item.RoomLab.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var occupiedStaffSet = overlappingShifts
            .SelectMany(item => new[] { item.ProctorUserId, item.ExaminerUserId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

        foreach (var room in request.Rooms!)
        {
            var trimmedLab = room.RoomLab!.Trim();
            if (occupiedRoomSet.Contains(trimmedLab))
            {
                return Result<BatchOpenExamShiftsResponse>.Failure(
                    $"CONFLICT: Phòng thi '{trimmedLab}' đã có ca thi khác hoạt động trùng khung giờ đã chọn.");
            }

            if (room.ProctorUserId.HasValue && occupiedStaffSet.Contains(room.ProctorUserId.Value))
            {
                return Result<BatchOpenExamShiftsResponse>.Failure(
                    $"CONFLICT: Giám thị '{referencedUsers[room.ProctorUserId.Value].FullName}' đã được phân công ca thi khác trùng khung giờ.");
            }

            if (room.ExaminerUserId.HasValue && occupiedStaffSet.Contains(room.ExaminerUserId.Value))
            {
                return Result<BatchOpenExamShiftsResponse>.Failure(
                    $"CONFLICT: Giám khảo '{referencedUsers[room.ExaminerUserId.Value].FullName}' đã được phân công ca thi khác trùng khung giờ.");
            }
        }

        var nowUtc = DateTime.UtcNow;
        var createdShifts = new List<RealExamSessionShift>(request.Rooms.Count);
        foreach (var room in request.Rooms)
        {
            var trimmedLab = room.RoomLab!.Trim();
            createdShifts.Add(new RealExamSessionShift
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                ShiftName = $"{normalizedGroupName} - {trimmedLab}",
                RoomLab = trimmedLab,
                MaxSeats = room.MaxSeats,
                StartTime = startTimeUtc,
                EndTime = endTimeUtc,
                ProctorUserId = room.ProctorUserId,
                ExaminerUserId = room.ExaminerUserId,
                Status = "scheduled",
                IsExamPublished = false,
                CreatedAt = nowUtc
            });
        }

        context.RealExamSessionShifts.AddRange(createdShifts);
        context.AuditLogs.Add(new AuditLog
        {
            UserId = request.ActorUserId,
            Action = "OFFICIAL_EXAM_SHIFTS_BATCH_CREATED",
            EntityName = nameof(OfficialExamSession),
            EntityId = session.Id,
            NewValues = JsonSerializer.Serialize(new
            {
                sessionId = session.Id,
                shiftGroupName = normalizedGroupName,
                startTime = startTimeUtc,
                endTime = endTimeUtc,
                rooms = createdShifts.Select(item => new { item.Id, item.RoomLab, item.MaxSeats }).ToArray()
            }),
            CreatedAt = nowUtc
        });

        await context.SaveChangesAsync(cancellationToken);

        var shiftResponses = createdShifts
            .Select(shift => new ExamShiftResponse(
                shift.Id,
                shift.SessionId,
                shift.ShiftName,
                shift.RoomLab,
                shift.StartTime,
                shift.EndTime,
                shift.ProctorUserId,
                shift.Status,
                shift.ExaminerUserId,
                shift.ExaminerUserId.HasValue && referencedUsers.TryGetValue(shift.ExaminerUserId.Value, out var ex) ? ex.FullName : null,
                shift.ProctorUserId.HasValue && referencedUsers.TryGetValue(shift.ProctorUserId.Value, out var pr) ? pr.FullName : null,
                shift.MaxSeats))
            .ToList();

        return Result<BatchOpenExamShiftsResponse>.Success(new BatchOpenExamShiftsResponse(
            session.Id,
            normalizedGroupName,
            createdShifts.Count,
            createdShifts.Sum(shift => shift.MaxSeats),
            shiftResponses));
    }
}
