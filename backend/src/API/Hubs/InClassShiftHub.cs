using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.Practice.DTOs;

namespace OralExamination.API.Hubs;

public interface IInClassShiftClient
{
    Task ReceiveGradingResult(PracticeAnswerDetailDto result);
    Task InClassSubmissionGraded(object payload);
    Task InClassShiftGradesFinalized(object payload);
    Task ReceiveGradingError(Guid answerId, string error);
}

[Authorize]
public sealed class InClassShiftHub(IApplicationDbContext context) : Hub<IInClassShiftClient>
{
    public async Task JoinShift(string shiftId)
    {
        if (!Guid.TryParse(shiftId, out var parsedShiftId) || parsedShiftId == Guid.Empty)
        {
            throw new HubException("BAD_REQUEST: Mã ca thi trong lớp không hợp lệ.");
        }

        var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
        if (!Guid.TryParse(userIdValue, out var userId) || userId == Guid.Empty)
        {
            throw new HubException("UNAUTHORIZED: Không xác định được danh tính người dùng.");
        }

        var shift = await context.InClassExamShifts
            .AsNoTracking()
            .Include(item => item.Class)
                .ThenInclude(classEntity => classEntity.Course)
            .Include(item => item.Class)
                .ThenInclude(classEntity => classEntity.CourseOffering)
            .FirstOrDefaultAsync(item => item.Id == parsedShiftId, Context.ConnectionAborted);

        if (shift is null)
        {
            throw new HubException("NOT_FOUND: Ca thi trong lớp không tồn tại.");
        }

        var academicContextActive = shift.Class.IsActive &&
            shift.Class.Course.IsActive &&
            (shift.Class.CourseOffering is null || shift.Class.CourseOffering.IsEnabled);
        if (!academicContextActive)
        {
            throw new HubException("NOT_FOUND: Lớp học phần hoặc môn học đã ngừng hoạt động.");
        }

        var isLecturer = Context.User?.IsInRole("lecturer") == true;
        var isStudent = Context.User?.IsInRole("student") == true;

        if (isLecturer)
        {
            var isAssigned = shift.LecturerId == userId ||
                shift.Class.LecturerId == userId ||
                await context.CourseLecturers.AsNoTracking().AnyAsync(
                    assignment => assignment.CourseId == shift.Class.CourseId &&
                                  assignment.LecturerUserId == userId,
                    Context.ConnectionAborted);
            if (!isAssigned)
            {
                throw new HubException("FORBIDDEN: Giảng viên không phụ trách ca thi này.");
            }
        }
        else if (isStudent)
        {
            var isEnrolled = await context.ClassEnrollments.AsNoTracking().AnyAsync(
                enrollment => enrollment.ClassId == shift.ClassId &&
                              enrollment.StudentId == userId &&
                              enrollment.Status == "enrolled",
                Context.ConnectionAborted);
            if (!isEnrolled)
            {
                throw new HubException("FORBIDDEN: Sinh viên không thuộc lớp học phần này.");
            }
        }
        else
        {
            throw new HubException("FORBIDDEN: Bạn không có quyền truy cập ca thi này.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, AudienceGroupName(parsedShiftId));
        if (isStudent)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, StudentGroupName(parsedShiftId, userId));
        }
        else
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, StaffGroupName(parsedShiftId));
        }
    }

    public Task JoinInClassShiftGroup(string shiftId) => JoinShift(shiftId);

    public async Task LeaveShift(string shiftId)
    {
        if (!Guid.TryParse(shiftId, out var parsedShiftId) || parsedShiftId == Guid.Empty)
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AudienceGroupName(parsedShiftId));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, StaffGroupName(parsedShiftId));

        var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
        if (Guid.TryParse(userIdValue, out var userId) && userId != Guid.Empty)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, StudentGroupName(parsedShiftId, userId));
        }
    }

    public Task LeaveInClassShiftGroup(string shiftId) => LeaveShift(shiftId);

    public static string AudienceGroupName(Guid shiftId) => $"inclass:{shiftId:D}:audience";
    public static string StaffGroupName(Guid shiftId) => $"inclass:{shiftId:D}:staff";
    public static string StudentGroupName(Guid shiftId, Guid studentId) =>
        $"inclass:{shiftId:D}:student:{studentId:D}";
}
