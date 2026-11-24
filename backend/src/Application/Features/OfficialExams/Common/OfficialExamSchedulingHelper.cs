using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Common;

internal sealed class ShiftRoomAllocationSlot
{
    public required Guid ShiftId { get; init; }
    public required string RoomLab { get; init; }
    public required int MaxSeats { get; init; }
    public required DateTime StartTime { get; init; }
    public required DateTime EndTime { get; init; }
    public required int AssignedBefore { get; init; }
    public required IReadOnlyList<int> AvailableSeatNumbers { get; init; }
    public required IReadOnlySet<Guid> ExistingExamSetIds { get; init; }

    public int EffectiveCapacity => Math.Min(OfficialExamRules.MaxSeat, MaxSeats);
    public int AvailableCapacity => Math.Min(Math.Max(0, EffectiveCapacity - AssignedBefore), AvailableSeatNumbers.Count);
    public int AssignedNow { get; set; }
    public int TotalAfter => AssignedBefore + AssignedNow;
}

internal sealed record ShiftGroupAllocationPlan(
    DateTime StartTime,
    DateTime EndTime,
    bool IsFinalBalancedShift,
    IReadOnlyList<ShiftRoomAllocationSlot> Rooms);

internal static class OfficialExamSchedulingHelper
{
    public static int ComputeRecommendedRoomCount(int studentCount) =>
        studentCount <= 0
            ? 0
            : (studentCount + OfficialExamRules.DefaultShiftCapacity - 1) / OfficialExamRules.DefaultShiftCapacity;

    public static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public static async Task<List<Course>> GetEligibleCoursesForSemesterAsync(
        IApplicationDbContext context,
        Guid semesterId,
        CancellationToken cancellationToken)
    {
        return await context.Courses
            .AsNoTracking()
            .Where(course => course.IsActive &&
                (course.CourseOfferings.Any(offering => offering.SemesterId == semesterId && offering.IsEnabled)
                 || (course.SemesterId == semesterId
                     && !course.CourseOfferings.Any(offering => offering.SemesterId == semesterId && !offering.IsEnabled))))
            .OrderBy(course => course.Code)
            .ToListAsync(cancellationToken);
    }

    public static async Task<List<Guid>> GetEligibleEnrolledStudentIdsAsync(
        IApplicationDbContext context,
        Guid courseId,
        Guid semesterId,
        CancellationToken cancellationToken)
    {
        return await context.ClassEnrollments
            .AsNoTracking()
            .Where(enrollment =>
                enrollment.Status == "enrolled"
                && enrollment.Student.IsActive
                && enrollment.Student.Role == UserRole.Student
                && enrollment.Class.IsActive
                && enrollment.Class.CourseId == courseId
                && enrollment.Class.SemesterId == semesterId
                && enrollment.Class.Course.IsActive
                && (enrollment.Class.CourseOffering == null || enrollment.Class.CourseOffering.IsEnabled))
            .Select(enrollment => enrollment.StudentId)
            .Distinct()
            .OrderBy(studentId => studentId)
            .ToListAsync(cancellationToken);
    }

    public static async Task<List<Guid>> GetImportedCandidateStudentIdsAsync(
        IApplicationDbContext context,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var candidateStudentIds = await context.OfficialExamCandidates
            .AsNoTracking()
            .Where(candidate => candidate.SessionId == sessionId)
            .Select(candidate => candidate.StudentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (candidateStudentIds.Count == 0)
        {
            return [];
        }

        return await context.Users
            .AsNoTracking()
            .Where(user =>
                candidateStudentIds.Contains(user.Id)
                && user.IsActive
                && user.Role == UserRole.Student)
            .Select(user => user.Id)
            .OrderBy(studentId => studentId)
            .ToListAsync(cancellationToken);
    }

    public static void ShuffleInPlace<T>(IList<T> items, IOfficialExamRandomGenerator randomGenerator)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var swapIndex = randomGenerator.NextInt32(i + 1);
            (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
        }
    }

    /// <summary>
    /// Phân bổ sinh viên vào các ca theo thứ tự khung giờ tăng dần:
    /// - Các ca trước ca cuối nhận sinh viên: lấp đầy tối đa sức chứa MaxSeats của phòng (mặc định 24, tối đa 40).
    /// - Ca cuối cùng nhận sinh viên (remaining &lt;= tổng ghế trống của ca): chia cân bằng đều theo Water-Filling
    ///   (tại mỗi bước cấp +1 SV cho phòng còn ghế trống có tổng sĩ số AssignedBefore + AssignedNow nhỏ nhất),
    ///   đảm bảo chênh lệch giữa các phòng trong ca cuối &lt;= 1 SV khi trống đồng đều và không bao giờ vượt trần MaxSeats.
    /// </summary>
    public static List<ShiftGroupAllocationPlan> AllocateAcrossShiftGroups(
        IEnumerable<ShiftRoomAllocationSlot> candidateRooms,
        int totalStudentsToAllocate)
    {
        var groupedShifts = candidateRooms
            .GroupBy(room => (room.StartTime, room.EndTime))
            .OrderBy(group => group.Key.StartTime)
            .ThenBy(group => group.Key.EndTime)
            .ToList();

        var remaining = totalStudentsToAllocate;
        var plans = new List<ShiftGroupAllocationPlan>(groupedShifts.Count);

        foreach (var group in groupedShifts)
        {
            var orderedRooms = group
                .OrderBy(room => room.RoomLab, StringComparer.OrdinalIgnoreCase)
                .ThenBy(room => room.ShiftId)
                .ToList();

            var groupAvailableCapacity = orderedRooms.Sum(room => room.AvailableCapacity);
            if (remaining <= 0 || groupAvailableCapacity <= 0)
            {
                plans.Add(new ShiftGroupAllocationPlan(group.Key.StartTime, group.Key.EndTime, false, orderedRooms));
                continue;
            }

            if (remaining > groupAvailableCapacity)
            {
                foreach (var room in orderedRooms)
                {
                    room.AssignedNow = room.AvailableCapacity;
                }

                remaining -= groupAvailableCapacity;
                plans.Add(new ShiftGroupAllocationPlan(group.Key.StartTime, group.Key.EndTime, false, orderedRooms));
                continue;
            }

            DistributeBalancedWaterFilling(orderedRooms, remaining);
            remaining = 0;
            plans.Add(new ShiftGroupAllocationPlan(group.Key.StartTime, group.Key.EndTime, true, orderedRooms));
        }

        return plans;
    }

    private static void DistributeBalancedWaterFilling(IReadOnlyList<ShiftRoomAllocationSlot> rooms, int countToAssign)
    {
        for (var step = 0; step < countToAssign; step++)
        {
            ShiftRoomAllocationSlot? bestRoom = null;
            for (var index = 0; index < rooms.Count; index++)
            {
                var candidate = rooms[index];
                if (candidate.AssignedNow >= candidate.AvailableCapacity)
                {
                    continue;
                }

                if (bestRoom is null || candidate.TotalAfter < bestRoom.TotalAfter)
                {
                    bestRoom = candidate;
                }
            }

            if (bestRoom is not null)
            {
                bestRoom.AssignedNow++;
            }
        }
    }
}
