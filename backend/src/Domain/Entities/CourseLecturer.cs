using System;

namespace OralExamination.Domain.Entities;

public partial class CourseLecturer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Guid LecturerUserId { get; set; }
    public Guid AssignedByUserId { get; set; }
    public Guid? SemesterId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual Course Course { get; set; } = null!;
    public virtual User LecturerUser { get; set; } = null!;
    public virtual User AssignedByUser { get; set; } = null!;
    public virtual Semester? Semester { get; set; }
}
