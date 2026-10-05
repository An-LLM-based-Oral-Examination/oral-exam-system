using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class Semester
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<Course> Courses { get; set; } = new List<Course>();
    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
}
