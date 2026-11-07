namespace OralExamination.Domain.Entities;

public sealed class AcademicProgram
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Guid? DepartmentHeadUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? DepartmentHeadUser { get; set; }
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}
