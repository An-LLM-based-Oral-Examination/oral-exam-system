using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class Class
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = null!;
    public Guid CourseId { get; set; }
    public Guid SemesterId { get; set; }
    public Guid? LecturerId { get; set; }
    public int MaxStudents { get; set; } = 35;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Course Course { get; set; } = null!;
    public virtual Semester Semester { get; set; } = null!;
    public virtual User? Lecturer { get; set; }
    public virtual ICollection<ClassEnrollment> Enrollments { get; set; } = new List<ClassEnrollment>();
}
