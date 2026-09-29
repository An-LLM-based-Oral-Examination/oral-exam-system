using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class Semester
{
    public int SemesterId { get; set; }

    public string SemesterCode { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
}
