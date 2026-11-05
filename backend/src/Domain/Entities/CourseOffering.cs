namespace OralExamination.Domain.Entities;

public sealed class CourseOffering
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Guid SemesterId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Course Course { get; set; } = null!;
    public Semester Semester { get; set; } = null!;
    public ICollection<CourseLecturer> LecturerAssignments { get; set; } = new List<CourseLecturer>();
    public ICollection<Class> Classes { get; set; } = new List<Class>();
}
