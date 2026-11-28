using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;

namespace OralExamination.UnitTests.Features.OfficialExams;

internal static class ExamPreparationTestData
{
    public static async Task AttachAsync(OralExamDbContext db, ExamSet set, Guid? author = null)
    {
        var course = await db.Courses.FirstAsync(x => x.Id == set.CourseId);
        var lecturer = author ?? set.CreatedBy ?? Guid.NewGuid();
        if (!await db.Users.AnyAsync(x => x.Id == lecturer)) db.Users.Add(new User { Id = lecturer, Email = $"{lecturer:N}@exam.test", FullName = "Author", Role = UserRole.Lecturer });
        var semester = await db.Semesters.FirstOrDefaultAsync(x => x.Id == course.SemesterId);
        if (semester is null)
        {
            semester = new Semester { Code = "FA26", Name = "Fall", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) };
            db.Semesters.Add(semester);
            course.SemesterId = semester.Id;
        }
        var session = await db.OfficialExamSessions.FirstOrDefaultAsync(x => x.Id == set.SessionId);
        if (session is null)
        {
            session = new OfficialExamSession { CourseId = course.Id, SemesterId = semester.Id, Title = "Final exam", ExamDate = new DateOnly(2026, 12, 20) };
            db.OfficialExamSessions.Add(session);
        }
        var structure = await db.ExamStructures.FirstOrDefaultAsync(x => x.Id == set.StructureId);
        if (structure is null) { structure = new ExamStructure { Id = set.StructureId, CourseId = course.Id, Name = "Blueprint" }; db.ExamStructures.Add(structure); }
        var clo = await db.CourseClos.FirstOrDefaultAsync(x => x.CourseId == course.Id);
        if (clo is null) { clo = new CourseClo { CourseId = course.Id, CloCode = "CLO1", Description = "Outcome", BloomLevel = "Apply" }; db.CourseClos.Add(clo); }
        if (!await db.CourseLecturers.AnyAsync(x => x.CourseId == course.Id && x.LecturerUserId == lecturer))
            db.CourseLecturers.Add(new CourseLecturer { CourseId = course.Id, SemesterId = semester.Id, LecturerUserId = lecturer, AssignedByUserId = lecturer });
        var sourceIds = set.ExamSetQuestions.Select(x => x.SourceQuestionId ?? x.ExamQuestionId).ToList();
        if (sourceIds.Count == 0)
        {
            var q = new ExamQuestion { CourseId = course.Id, Title = "Question", Content = "Explain", SubmittedBy = lecturer, RubricId = Guid.NewGuid() };
            db.ExamQuestions.Add(q); sourceIds.Add(q.Id);
        }
        await db.SaveChangesAsync();
        var sources = await db.ExamQuestions.Where(x => sourceIds.Contains(x.Id)).ToListAsync();
        foreach (var q in sources)
        {
            // Recreate the author-owned source draft before snapshotting a pre-seeded pending paper.
            if (q.ApprovalStatus == ApprovalStatus.SubmittedForReview && (q.SubmittedBy is null || q.SubmittedBy == lecturer))
                q.ApprovalStatus = ApprovalStatus.Draft;
            q.CloId = clo.Id; q.SubmittedBy ??= lecturer; q.SampleAnswer = "A complete model answer explaining the concept and examples.";
            if (!await db.Rubrics.AnyAsync(x => x.Id == q.RubricId))
            {
                var rubric = new Rubric { Id = q.RubricId, CourseId = course.Id, Name = "Rubric", Criteria = [
                    new RubricCriterion { CriterionName = "Explanation", MaxScore = 6, Weight = .6m },
                    new RubricCriterion { CriterionName = "Example", MaxScore = 4, Weight = .4m, OrderIndex = 2 }] };
                db.Rubrics.Add(rubric);
            }
        }
        structure.TotalQuestions = sources.Count;
        structure.EasyCount = sources.Count(x => x.Difficulty == "easy");
        structure.MediumCount = sources.Count(x => x.Difficulty == "medium");
        structure.HardCount = sources.Count(x => x.Difficulty == "hard");
        await db.SaveChangesAsync();
        if (!await db.ExamStructureClos.AnyAsync(x => x.StructureId == structure.Id))
            db.ExamStructureClos.Add(new ExamStructureClo { StructureId = structure.Id, CloId = clo.Id });
        var preparation = await db.ExamPreparations.FirstOrDefaultAsync(x => x.SessionId == session.Id);
        if (preparation is null) { preparation = new ExamPreparation { SessionId = session.Id, RequiredSetCount = 1, Deadline = DateTime.UtcNow.AddDays(10), RequestedBy = lecturer }; db.ExamPreparations.Add(preparation); }
        var assignment = new ExamAuthoringAssignment { PreparationId = preparation.Id, StructureId = structure.Id, LecturerId = lecturer,
            AssignedBy = lecturer, SetCount = 2, Deadline = DateTime.UtcNow.AddDays(5) };
        db.ExamAuthoringAssignments.Add(assignment);
        set.SessionId = session.Id; set.CreatedBy = lecturer; set.AssignmentId = assignment.Id;
        session.ExamStructureId = structure.Id;
        if (db.Entry(set).State == EntityState.Detached) set.ExamSetQuestions.Clear();
        var error = await ExamPaperRules.SetQuestions(db, set, sourceIds, lecturer, default);
        if (error is not null) throw new InvalidOperationException(error);
        await db.SaveChangesAsync();
    }
}
