using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class RealExamSessionShift
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string ShiftName { get; set; } = null!;
    public string RoomLab { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Guid? ProctorUserId { get; set; }
    public string Status { get; set; } = "scheduled";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual OfficialExamSession Session { get; set; } = null!;
    public virtual User? ProctorUser { get; set; }
    public virtual ICollection<StudentExamTicket> StudentExamTickets { get; set; } = new List<StudentExamTicket>();
}
