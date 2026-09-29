using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class RealExamAssignment
{
    public Guid AssignmentId { get; set; }

    public Guid ShiftId { get; set; }

    public Guid StudentId { get; set; }

    public int? BoothNumber { get; set; }

    public string AttendanceStatus { get; set; } = null!;

    public virtual RealExamResult? RealExamResult { get; set; }

    public virtual RealExamSessionShift Shift { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
