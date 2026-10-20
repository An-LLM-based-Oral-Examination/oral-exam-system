using System;
using System.Collections.Generic;

namespace OralExamination.Application.Features.Proctor.DTOs;

public sealed record ProctorSeatItemDto
{
    public int SeatNumber { get; init; }
    public bool IsOccupied { get; init; }
    public Guid? TicketId { get; init; }
    public Guid? StudentId { get; init; }
    public string? StudentCode { get; init; }
    public string? FullName { get; init; }
    public string? IpAddress { get; init; }
    public string? Status { get; init; }
    public bool IsCheckedIn { get; init; }
    public bool IsSuspended { get; init; }
    public string? SuspensionReason { get; init; }
    public DateTime? CheckedInAt { get; init; }
    public DateTime? SubmittedAt { get; init; }
}

public sealed record ShiftAttendanceMonitorResponseDto
{
    public Guid ShiftId { get; init; }
    public string ShiftName { get; init; } = string.Empty;
    public string RoomLab { get; init; } = string.Empty;
    public string ShiftStatus { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public int TotalSeats { get; init; } = 40;
    public int AssignedTicketsCount { get; init; }
    public int CheckedInCount { get; init; }
    public int InProgressCount { get; init; }
    public int SubmittedCount { get; init; }
    public int SuspendedCount { get; init; }
    public IReadOnlyList<ProctorSeatItemDto> Seats { get; init; } = Array.Empty<ProctorSeatItemDto>();
}

public sealed record TicketProctorActionResponseDto
{
    public Guid TicketId { get; init; }
    public Guid ShiftId { get; init; }
    public Guid StudentId { get; init; }
    public string StudentCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public int SeatNumber { get; init; }
    public string? IpAddress { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsSuspended { get; init; }
    public string? SuspensionReason { get; init; }
    public DateTime UpdatedAt { get; init; }
}
