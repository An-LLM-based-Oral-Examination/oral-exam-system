using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class ClassEnrollment
{
    public Guid EnrollmentId { get; set; }

    public int ClassId { get; set; }

    public Guid StudentId { get; set; }

    public string? StudentCode { get; set; }

    public DateTime EnrolledAt { get; set; }

    public string Status { get; set; } = null!;

    public virtual Class Class { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
