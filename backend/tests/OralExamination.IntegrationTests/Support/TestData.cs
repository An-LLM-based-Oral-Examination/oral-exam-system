using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;

namespace OralExamination.IntegrationTests.Support;

internal sealed record AcademicData(Course Course, Rubric Rubric, CourseClo Clo, User Student, User Lecturer);
internal sealed record OfficialData(AcademicData Academic, StudentExamTicket Ticket, ExamQuestion Question);

internal static class TestData
{
    public static async Task<AcademicData> AcademicAsync(OralExamDbContext context)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var semester = new Semester
        {
            Code = "IT" + suffix, Name = "Integration semester",
            StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31)
        };
        var student = new User { Email = suffix + "@student.example.test", FullName = "Integration Student" };
        var lecturer = new User { Email = suffix + "@lecturer.example.test", FullName = "Integration Lecturer", Role = "lecturer" };
        var course = new Course { Code = "IT" + suffix, Name = "Integration Course", Semester = semester };
        var rubric = new Rubric { Course = course, Name = "Integration Rubric" };
        var clo = new CourseClo { Course = course, CloCode = "CLO1", Description = "Integration outcome" };
        context.AddRange(student, lecturer, rubric, clo);
        await context.SaveChangesAsync();
        return new(course, rubric, clo, student, lecturer);
    }

    public static async Task<OfficialData> OfficialAsync(OralExamDbContext context, bool locked = false)
    {
        var academic = await AcademicAsync(context);
        var structure = new ExamStructure
        {
            CourseId = academic.Course.Id, Name = "Integration structure", TotalQuestions = 1,
            CreatedBy = academic.Lecturer.Id
        };
        var session = new OfficialExamSession
        {
            CourseId = academic.Course.Id, ExamStructure = structure,
            Title = "Integration official session", ExamDate = new DateOnly(2026, 11, 14)
        };
        var question = new ExamQuestion
        {
            CourseId = academic.Course.Id, RubricId = academic.Rubric.Id, CloId = academic.Clo.Id,
            Structure = structure, Title = "Integration question", Content = "Explain a transaction."
        };
        var set = new ExamSet
        {
            CourseId = academic.Course.Id, Structure = structure, SetCode = "IT-SET", Status = "HANDED_OVER"
        };
        var shift = new RealExamSessionShift
        {
            Session = session, ShiftName = "Integration shift", RoomLab = "IT-LAB",
            StartTime = DateTime.UtcNow.AddHours(-1), EndTime = DateTime.UtcNow.AddHours(1)
        };
        var ticket = new StudentExamTicket
        {
            Shift = shift, StudentId = academic.Student.Id, ExamSet = set, SeatNumber = 1,
            IpAddress = "127.0.0.1", Status = locked ? ExamTicketStatus.Locked : ExamTicketStatus.InProgress,
            IsLocked = locked, AudioVerifiedAt = DateTime.UtcNow,
            AudioObjectKey = "integration/test.webm", AudioHashSha256 = new string('a', 64)
        };
        context.AddRange(ticket, new ExamSetQuestion { ExamSet = set, ExamQuestion = question, OrderIndex = 1 });
        await context.SaveChangesAsync();
        return new(academic, ticket, question);
    }
}
