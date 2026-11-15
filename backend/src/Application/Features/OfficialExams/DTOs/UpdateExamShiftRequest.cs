namespace OralExamination.Application.Features.OfficialExams.DTOs;

public sealed record UpdateExamShiftRequest(string? ShiftName, string? RoomLab, DateTime StartTime,
    DateTime EndTime, Guid ProctorUserId, Guid ExaminerUserId);
