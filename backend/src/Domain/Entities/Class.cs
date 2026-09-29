using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class Class
{
    public int ClassId { get; set; }

    public int CourseId { get; set; }

    public int SemesterId { get; set; }

    public Guid InstructorId { get; set; }

    public string ClassName { get; set; } = null!;

    public virtual ICollection<ClassEnrollment> ClassEnrollments { get; set; } = new List<ClassEnrollment>();

    public virtual Course Course { get; set; } = null!;

    public virtual User Instructor { get; set; } = null!;

    public virtual ICollection<RealExamSession> RealExamSessions { get; set; } = new List<RealExamSession>();

    public virtual Semester Semester { get; set; } = null!;
}
