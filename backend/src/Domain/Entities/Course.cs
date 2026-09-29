using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class Course
{
    public int CourseId { get; set; }

    public string CourseCode { get; set; } = null!;

    public string CourseName { get; set; } = null!;

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();

    public virtual ICollection<ExamStructure> ExamStructures { get; set; } = new List<ExamStructure>();

    public virtual ICollection<MockExamQuota> MockExamQuota { get; set; } = new List<MockExamQuota>();

    public virtual ICollection<PracticeQuestion> PracticeQuestions { get; set; } = new List<PracticeQuestion>();
}
