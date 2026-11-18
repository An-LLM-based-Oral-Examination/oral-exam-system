using System;
using System.Collections.Generic;

namespace OralExamination.Application.Features.OfficialExams.DTOs;

public sealed class DistributeShiftTicketsRequest
{
    public bool AllowRandomShuffle { get; set; } = true;

    public DistributeShiftTicketsRequest() { }

    public DistributeShiftTicketsRequest(bool allowRandomShuffle)
    {
        AllowRandomShuffle = allowRandomShuffle;
    }
}

public sealed record DistributeShiftTicketsItemDto(
    Guid TicketId,
    string StudentCode,
    int SeatNumber,
    Guid ExamSetId);

public sealed record DistributeShiftTicketsResponse(
    Guid ShiftId,
    int TotalDistributed,
    string Message,
    IReadOnlyList<DistributeShiftTicketsItemDto> DistributedTickets);
