using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OralExamination.Application.Features.Courses.Commands.CreateCourseClo;
using OralExamination.Application.Features.Courses.Commands.DeleteCourseClo;
using OralExamination.Application.Features.Courses.Commands.UpdateCourseClo;
using OralExamination.Application.Features.Courses.Queries.GetCourseClos;
using OralExamination.Application.Features.Courses.Queries.GetManagedCloCourses;
using OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Courses;

public class CourseCloOwnershipTests
{
    private static OralExamDbContext NewContext() => new(new DbContextOptionsBuilder<OralExamDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Course NewCourse(Guid? owner = null, Guid? programOwner = null)
    {
        var course = new Course { Code = "PRN", Name = "Course", DepartmentHeadUserId = owner };
        if (programOwner.HasValue)
        {
            course.AcademicProgram = new AcademicProgram { Code = "SE", Name = "Software", DepartmentHeadUserId = programOwner };
            course.AcademicProgramId = course.AcademicProgram.Id;
        }
        return course;
    }

    private static CourseClo NewClo(Course course, string code = "CLO1") => new()
    { CourseId = course.Id, CloCode = code, Description = "Outcome", BloomLevel = BloomLevel.Apply };

    private static string Head(Guid id) => $"{UserRole.DepartmentHead}:{id}";

    [Theory]
    [InlineData(null)]
    [InlineData("department_head")]
    [InlineData("department_head:invalid")]
    [InlineData("student")]
    [InlineData("examination_officer")]
    public async Task Create_MissingIdentityOrNonManager_IsForbidden(string? actor)
    {
        await using var db = NewContext();
        var course = NewCourse(Guid.NewGuid());
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        var result = await new CreateCourseCloCommandHandler(db).Handle(
            new(course.Id, "CLO1", "Outcome", BloomLevel.Apply, 1, actor), default);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("FORBIDDEN:");
        (await db.CourseClos.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_ProgramOwnerWinsOverLegacyCourseOwner()
    {
        await using var db = NewContext();
        var owner = Guid.NewGuid();
        var oldOwner = Guid.NewGuid();
        var course = NewCourse(oldOwner, owner);
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        var handler = new CreateCourseCloCommandHandler(db);
        var denied = await handler.Handle(new(course.Id, "CLO1", "Outcome", BloomLevel.Apply, 1, Head(oldOwner)), default);
        denied.IsSuccess.Should().BeFalse();
        var allowed = await handler.Handle(new(course.Id, " clo1 ", " Outcome ", BloomLevel.Apply, 1, Head(owner)), default);
        allowed.IsSuccess.Should().BeTrue();
        allowed.Value.CloCode.Should().Be("CLO1");
        allowed.Value.Description.Should().Be("Outcome");
    }

    [Fact]
    public async Task Create_NoOwner_FailsClosed()
    {
        await using var db = NewContext();
        var course = NewCourse();
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        var result = await new CreateCourseCloCommandHandler(db).Handle(
            new(course.Id, "CLO1", "Outcome", BloomLevel.Apply, 1, Head(Guid.NewGuid())), default);
        result.Error.Should().StartWith("FORBIDDEN:");
    }

    [Fact]
    public async Task Create_CaseAndWhitespaceDuplicateInLegacyData_IsConflict()
    {
        await using var db = NewContext();
        var course = NewCourse();
        db.Courses.Add(course);
        db.CourseClos.Add(NewClo(course, " clo1 "));
        await db.SaveChangesAsync();
        var result = await new CreateCourseCloCommandHandler(db).Handle(
            new(course.Id, "CLO1", "Outcome", BloomLevel.Apply, 1, UserRole.Admin), default);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT:");
    }

    [Fact]
    public async Task Update_AndDelete_UnassignedCourse_RejectWithoutMutation()
    {
        await using var db = NewContext();
        var course = NewCourse();
        var clo = NewClo(course);
        db.Courses.Add(course);
        db.CourseClos.Add(clo);
        await db.SaveChangesAsync();
        var actor = Head(Guid.NewGuid());
        var update = await new UpdateCourseCloCommandHandler(db).Handle(
            new(course.Id, clo.Id, "NEW", "Changed", BloomLevel.Create, 2, true, actor), default);
        var delete = await new DeleteCourseCloCommandHandler(db).Handle(new(course.Id, clo.Id, actor), default);
        update.Error.Should().StartWith("FORBIDDEN:");
        delete.Error.Should().StartWith("FORBIDDEN:");
        clo.CloCode.Should().Be("CLO1");
        (await db.CourseClos.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("practice")]
    [InlineData("exam")]
    [InlineData("slotId")]
    [InlineData("slotCode")]
    public async Task Delete_ReferencedClo_IsConflictAndPreservesReferences(string reference)
    {
        await using var db = NewContext();
        var course = NewCourse();
        var clo = NewClo(course);
        db.Courses.Add(course);
        db.CourseClos.Add(clo);
        if (reference == "practice") db.PracticeQuestions.Add(new PracticeQuestion
        { CourseId = course.Id, CloId = clo.Id, Title = "Question", Content = "Content", Difficulty = "easy" });
        if (reference == "exam") db.ExamQuestions.Add(new ExamQuestion
        { CourseId = course.Id, CloId = clo.Id, Title = "Question", Content = "Content", Difficulty = "easy" });
        if (reference.StartsWith("slot")) db.ExamStructures.Add(new ExamStructure
        {
            CourseId = course.Id, Name = "Structure", SlotDefinitions = reference == "slotId"
                ? $"[{{\"CloId\":\"{clo.Id}\"}}]" : "[{\"cloCodes\":[\"clo1\"]}]"
        });
        await db.SaveChangesAsync();
        var result = await new DeleteCourseCloCommandHandler(db).Handle(new(course.Id, clo.Id, UserRole.Admin), default);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT: CLO_IN_USE:");
        (await db.CourseClos.AnyAsync(c => c.Id == clo.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_UnrelatedSlotClo_DoesNotBlock()
    {
        await using var db = NewContext();
        var course = NewCourse();
        var clo = NewClo(course);
        db.Courses.Add(course);
        db.CourseClos.Add(clo);
        db.ExamStructures.Add(new ExamStructure { CourseId = course.Id, Name = "Structure", SlotDefinitions = "[{\"cloCode\":\"CLO10\"}]" });
        await db.SaveChangesAsync();
        var result = await new DeleteCourseCloCommandHandler(db).Handle(new(course.Id, clo.Id, UserRole.Admin), default);
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("practice", true)]
    [InlineData("exam", true)]
    [InlineData("practice", false)]
    [InlineData("exam", false)]
    public async Task DraftQuestion_RejectsForeignOrInactiveClo(string scope, bool foreignCourse)
    {
        await using var db = NewContext();
        var course = NewCourse();
        var other = NewCourse();
        other.Code = "OTHER";
        var clo = NewClo(foreignCourse ? other : course);
        clo.IsActive = foreignCourse;
        var admin = new User { Email = "admin@example.test", FullName = "Admin", Role = UserRole.Admin, IsActive = true };
        db.Courses.AddRange(course, other);
        db.CourseClos.Add(clo);
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        var command = new CreateDraftQuestionCommand(course.Id, admin.Id, "Question", "Question content",
            null, [], "easy", BloomLevel.Apply, "manual", scope, false, null,
            new RubricDraftDto("Rubric", null, 10m,
                [new("First", 5m, 0.5m, BloomLevel.Apply, "Criterion"), new("Second", 5m, 0.5m, BloomLevel.Apply, "Criterion")]),
            CloId: clo.Id);
        var result = await new CreateDraftQuestionCommandHandler(db).Handle(command, default);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("CLO");
        (await db.PracticeQuestions.CountAsync()).Should().Be(0);
        (await db.ExamQuestions.CountAsync()).Should().Be(0);
        (await db.Rubrics.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ListAndCoursePicker_UseCanonicalOwnership_AndDoNotExposeForeignClos()
    {
        await using var db = NewContext();
        var owner = Guid.NewGuid();
        var legacyOwner = Guid.NewGuid();
        var owned = NewCourse(legacyOwner, owner);
        var legacy = NewCourse(owner); legacy.Code = "LEGACY";
        var foreign = NewCourse(owner, Guid.NewGuid()); foreign.Code = "FOREIGN";
        var unassigned = NewCourse(); unassigned.Code = "UNASSIGNED";
        db.Courses.AddRange(owned, legacy, foreign, unassigned);
        db.CourseClos.AddRange(NewClo(owned), NewClo(foreign));
        await db.SaveChangesAsync();
        var picker = await new GetManagedCloCoursesQueryHandler(db).Handle(new(Head(owner)), default);
        picker.IsSuccess.Should().BeTrue();
        picker.Value.Select(c => c.Id).Should().BeEquivalentTo([owned.Id, legacy.Id]);
        var handler = new GetCourseClosQueryHandler(db);
        var allowed = await handler.Handle(new(owned.Id, false, Head(owner)), default);
        allowed.Value.Should().ContainSingle();
        var denied = await handler.Handle(new(foreign.Id, false, Head(owner)), default);
        denied.Error.Should().StartWith("FORBIDDEN:");
        (await handler.Handle(new(owned.Id), default)).Error.Should().StartWith("FORBIDDEN:");
    }

    [Fact]
    public async Task Update_LegacyCaseDuplicate_IsConflictAndDoesNotChangeCode()
    {
        await using var db = NewContext();
        var course = NewCourse();
        var clo = NewClo(course);
        db.Courses.Add(course);
        db.CourseClos.AddRange(clo, NewClo(course, " clo2 "));
        await db.SaveChangesAsync();
        var result = await new UpdateCourseCloCommandHandler(db).Handle(
            new(course.Id, clo.Id, "CLO2", "Changed", BloomLevel.Apply, 2, true, UserRole.Admin), default);
        result.Error.Should().StartWith("CONFLICT:");
        clo.CloCode.Should().Be("CLO1");
    }

    [Theory]
    [InlineData(typeof(PracticeQuestion))]
    [InlineData(typeof(ExamQuestion))]
    public void Model_QuestionCloReference_RestrictsConcurrentDelete(Type questionType)
    {
        using var db = NewContext();
        var foreignKey = db.Model.FindEntityType(questionType)!.GetForeignKeys()
            .Single(key => key.PrincipalEntityType.ClrType == typeof(CourseClo));
        foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    [Fact]
    public void SchemaRepair_CloReferencesNeverUseSetNull()
    {
        var sql = PostgresSchemaReconciler.GetPreMigrationSql() + PostgresSchemaReconciler.GetPostMigrationSql();
        sql.Should().Contain("FOREIGN KEY (clo_id) REFERENCES course_clos(id) ON DELETE RESTRICT;");
        sql.Should().NotContain("FOREIGN KEY (clo_id) REFERENCES course_clos(id) ON DELETE SET NULL;");
    }

    [Fact]
    public async Task Update_CodeReferencedBySlot_RequiresReferenceToBeRemovedFirst()
    {
        await using var db = NewContext();
        var course = NewCourse();
        var clo = NewClo(course);
        db.Courses.Add(course);
        db.CourseClos.Add(clo);
        db.ExamStructures.Add(new ExamStructure { CourseId = course.Id, Name = "Structure", SlotDefinitions = "[{\"clo_code\":\"CLO1\"}]" });
        await db.SaveChangesAsync();
        var result = await new UpdateCourseCloCommandHandler(db).Handle(
            new(course.Id, clo.Id, "RENAMED", "Outcome", BloomLevel.Apply, 1, true, UserRole.Admin), default);
        result.Error.Should().StartWith("CONFLICT: CLO_IN_USE:");
        clo.CloCode.Should().Be("CLO1");
    }

    [Fact]
    public void CloRestrictionMigration_IsDiscoveredAndGeneratesReversibleSqlWithoutDatabaseConnection()
    {
        using var db = new OralExamDbContext(new DbContextOptionsBuilder<OralExamDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=offline_script_only;Username=offline").Options);
        // The legacy baseline is intentionally not reversible because it repairs a shared
        // schema. Validate the CLO migration's own Up/Down contract without attempting
        // to roll that baseline back.
        const string before = "20261113235959_RepairLegacySchemaBaseline";
        const string after = "20261114010000_RestrictQuestionCloDeletion";
        db.Database.GetMigrations().Should().Contain(after);
        var migrator = db.GetService<IMigrator>();
        var up = migrator.GenerateScript(before, after);
        up.Should().Contain("FK_practice_questions_course_clos_clo_id");
        up.Should().Contain("FK_exam_questions_course_clos_clo_id");
        up.Should().Contain("ON DELETE RESTRICT");
        var down = migrator.GenerateScript(after, before);
        down.Should().Contain("ON DELETE SET NULL");
    }
}
