using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.AcademicCatalog.Commands.CreateAcademicProgram;
using OralExamination.Application.Features.AcademicCatalog.Commands.UpdateAcademicProgram;
using OralExamination.Application.Features.Courses.Commands.CreateCourse;
using OralExamination.Application.Features.Courses.Commands.UpdateCourse;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.AcademicCatalog;

public sealed class AcademicProgramManagementTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private static (User admin, User headSe, User headEnt, User lecturer, Semester semester, AcademicProgram seProgram) SeedBase(OralExamDbContext context)
    {
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@prog-test.vn",
            FullName = "Admin Test",
            Role = UserRole.Admin,
            IsActive = true
        };
        var headSe = new User
        {
            Id = Guid.NewGuid(),
            Email = "headse@prog-test.vn",
            FullName = "Trưởng BM SE",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };
        var headEnt = new User
        {
            Id = Guid.NewGuid(),
            Email = "headent@prog-test.vn",
            FullName = "Trưởng BM ENT",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer@prog-test.vn",
            FullName = "Giảng viên",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };
        var seProgram = new AcademicProgram
        {
            Id = Guid.NewGuid(),
            Code = "SE",
            Name = "Software Engineering",
            DepartmentHeadUserId = headSe.Id,
            IsActive = true
        };

        context.Users.AddRange(admin, headSe, headEnt, lecturer);
        context.Semesters.Add(semester);
        context.AcademicPrograms.Add(seProgram);
        context.SaveChanges();

        return (admin, headSe, headEnt, lecturer, semester, seProgram);
    }

    [Fact(DisplayName = "AP-01: CreateAcademicProgram chuẩn hóa mã HOA, ghi AuditLog và chặn trùng mã (CONFLICT) không phân biệt hoa thường")]
    public async Task CreateAcademicProgram_NormalizesCode_WritesAudit_AndRejectsDuplicate()
    {
        await using var context = CreateDbContext();
        var (admin, _, headEnt, _, _, _) = SeedBase(context);

        var handler = new CreateAcademicProgramCommandHandler(context);
        var createResult = await handler.Handle(
            new CreateAcademicProgramCommand(admin.Id, " ent ", "Bộ môn Tiếng Anh (ENT)", headEnt.Id, true),
            CancellationToken.None);

        createResult.IsSuccess.Should().BeTrue();
        createResult.Value!.Code.Should().Be("ENT");
        createResult.Value.DepartmentHeadUserId.Should().Be(headEnt.Id);
        createResult.Value.DepartmentHeadName.Should().Be(headEnt.FullName);

        var audit = await context.AuditLogs.FirstOrDefaultAsync(
            a => a.Action == "ACADEMIC_PROGRAM_CREATED" && a.EntityId == createResult.Value.Id);
        audit.Should().NotBeNull();

        var duplicateResult = await handler.Handle(
            new CreateAcademicProgramCommand(admin.Id, "EnT", "Duplicate ENT", null, true),
            CancellationToken.None);
        duplicateResult.IsSuccess.Should().BeFalse();
        duplicateResult.Error.Should().StartWith("CONFLICT:");
    }

    [Fact(DisplayName = "AP-02: CreateAcademicProgram từ chối mã không hợp lệ hoặc user không phải Trưởng BM đang hoạt động")]
    public async Task CreateAcademicProgram_RejectsInvalidCodeAndNonDepartmentHead()
    {
        await using var context = CreateDbContext();
        var (admin, _, _, lecturer, _, _) = SeedBase(context);

        var handler = new CreateAcademicProgramCommandHandler(context);

        var badCode = await handler.Handle(
            new CreateAcademicProgramCommand(admin.Id, "ENT 01!", "Tên hợp lệ"),
            CancellationToken.None);
        badCode.IsSuccess.Should().BeFalse();
        badCode.Error.Should().StartWith("VALIDATION:");

        var wrongRole = await handler.Handle(
            new CreateAcademicProgramCommand(admin.Id, "AI", "Trí tuệ nhân tạo", lecturer.Id),
            CancellationToken.None);
        wrongRole.IsSuccess.Should().BeFalse();
        wrongRole.Error.Should().StartWith("VALIDATION:");

        var missingUser = await handler.Handle(
            new CreateAcademicProgramCommand(admin.Id, "AI", "Trí tuệ nhân tạo", Guid.NewGuid()),
            CancellationToken.None);
        missingUser.IsSuccess.Should().BeFalse();
        missingUser.Error.Should().StartWith("NOT_FOUND:");
    }

    [Fact(DisplayName = "AP-03: UpdateAcademicProgram đồng bộ Trưởng BM mới xuống các môn thuộc ngành và cho phép tháo Trưởng BM qua Guid.Empty")]
    public async Task UpdateAcademicProgram_SyncsDepartmentHeadToCourses_AndSupportsUnassign()
    {
        await using var context = CreateDbContext();
        var (admin, headSe, headEnt, _, semester, seProgram) = SeedBase(context);

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Cross-Platform",
            Credits = 3,
            SemesterId = semester.Id,
            AcademicProgramId = seProgram.Id,
            DepartmentHeadUserId = headSe.Id,
            IsActive = true
        };
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        var handler = new UpdateAcademicProgramCommandHandler(context);

        var reassigned = await handler.Handle(
            new UpdateAcademicProgramCommand(admin.Id, seProgram.Id, "SE", "Kỹ thuật phần mềm", true, headEnt.Id),
            CancellationToken.None);
        reassigned.IsSuccess.Should().BeTrue();
        reassigned.Value!.DepartmentHeadUserId.Should().Be(headEnt.Id);
        reassigned.Value.CoursesCount.Should().Be(1);

        var reloadedCourse = await context.Courses.FirstAsync(c => c.Id == course.Id);
        reloadedCourse.DepartmentHeadUserId.Should().Be(headEnt.Id);

        var unassigned = await handler.Handle(
            new UpdateAcademicProgramCommand(admin.Id, seProgram.Id, "SE", "Kỹ thuật phần mềm", true, Guid.Empty),
            CancellationToken.None);
        unassigned.IsSuccess.Should().BeTrue();
        unassigned.Value!.DepartmentHeadUserId.Should().BeNull();
        unassigned.Value.DepartmentHeadName.Should().BeNull();

        // Tháo Trưởng BM ở ngành không tự ý xóa quyền môn đang vận hành
        (await context.Courses.FirstAsync(c => c.Id == course.Id)).DepartmentHeadUserId.Should().Be(headEnt.Id);
    }

    [Fact(DisplayName = "AP-04: CreateCourse chọn ngành ENT sẽ gắn đúng ENT & kế thừa headent (không hardcode SE); Guid.Empty gắn null; ngành tạm khóa bị từ chối")]
    public async Task CreateCourse_UsesRequestedAcademicProgram_AndRejectsInactiveProgram()
    {
        await using var context = CreateDbContext();
        var (admin, _, headEnt, _, semester, _) = SeedBase(context);

        var entProgram = new AcademicProgram
        {
            Id = Guid.NewGuid(),
            Code = "ENT",
            Name = "Bộ môn Tiếng Anh",
            DepartmentHeadUserId = headEnt.Id,
            IsActive = true
        };
        var inactiveProgram = new AcademicProgram
        {
            Id = Guid.NewGuid(),
            Code = "OLD",
            Name = "Ngành cũ",
            IsActive = false
        };
        context.AcademicPrograms.AddRange(entProgram, inactiveProgram);
        await context.SaveChangesAsync();

        var handler = new CreateCourseCommandHandler(context);

        // 1. Tạo môn thuộc ENT không truyền DepartmentHeadUserId -> tự kế thừa headent (KHÔNG dính SE/headse)
        var entCourse = await handler.Handle(
            new CreateCourseCommand(
                Code: "ENT601",
                Name: "Academic Writing",
                Credits: 3,
                SemesterId: semester.Id,
                ActorUserId: admin.Id,
                AcademicProgramId: entProgram.Id),
            CancellationToken.None);
        entCourse.IsSuccess.Should().BeTrue();
        entCourse.Value!.AcademicProgramId.Should().Be(entProgram.Id);
        entCourse.Value.DepartmentHeadUserId.Should().Be(headEnt.Id);

        // 2. Truyền AcademicProgramId = Guid.Empty -> chủ động không gắn ngành (null), không fallback SE
        var standaloneCourse = await handler.Handle(
            new CreateCourseCommand(
                Code: "GEN101",
                Name: "General Skills",
                Credits: 2,
                DepartmentHeadUserId: Guid.Empty,
                SemesterId: semester.Id,
                ActorUserId: admin.Id,
                AcademicProgramId: Guid.Empty),
            CancellationToken.None);
        standaloneCourse.IsSuccess.Should().BeTrue();
        standaloneCourse.Value!.AcademicProgramId.Should().BeNull();
        standaloneCourse.Value.DepartmentHeadUserId.Should().BeNull();

        // 3. Ngành đang tạm khóa -> VALIDATION
        var lockedProgramAttempt = await handler.Handle(
            new CreateCourseCommand(
                Code: "OLD101",
                Name: "Old Course",
                Credits: 3,
                SemesterId: semester.Id,
                ActorUserId: admin.Id,
                AcademicProgramId: inactiveProgram.Id),
            CancellationToken.None);
        lockedProgramAttempt.IsSuccess.Should().BeFalse();
        lockedProgramAttempt.Error.Should().StartWith("VALIDATION:");
    }

    [Fact(DisplayName = "AP-05: UpdateCourse chuyển ngành SE -> ENT, tháo ngành bằng Guid.Empty, và giữ nguyên khi null")]
    public async Task UpdateCourse_SwitchesUnassignsOrPreservesAcademicProgram()
    {
        await using var context = CreateDbContext();
        var (admin, headSe, headEnt, _, semester, seProgram) = SeedBase(context);

        var entProgram = new AcademicProgram
        {
            Id = Guid.NewGuid(),
            Code = "ENT",
            Name = "Bộ môn Tiếng Anh",
            DepartmentHeadUserId = headEnt.Id,
            IsActive = true
        };
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "ENT203",
            Name = "English 203",
            Credits = 3,
            SemesterId = semester.Id,
            AcademicProgramId = seProgram.Id,
            DepartmentHeadUserId = headSe.Id,
            IsActive = true
        };
        context.AcademicPrograms.Add(entProgram);
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        var handler = new UpdateCourseCommandHandler(context);

        // 1. AcademicProgramId == null -> giữ nguyên seProgram.Id
        var preserved = await handler.Handle(
            new UpdateCourseCommand(course.Id, "ENT203", "English 203 Updated", 3, null, null, 3, 20, 5, true, semester.Id, admin.Id, null),
            CancellationToken.None);
        preserved.IsSuccess.Should().BeTrue();
        preserved.Value!.AcademicProgramId.Should().Be(seProgram.Id);

        // 2. Chuyển sang ENT
        var switched = await handler.Handle(
            new UpdateCourseCommand(course.Id, "ENT203", "English 203 Updated", 3, null, headEnt.Id, 3, 20, 5, true, semester.Id, admin.Id, entProgram.Id),
            CancellationToken.None);
        switched.IsSuccess.Should().BeTrue();
        switched.Value!.AcademicProgramId.Should().Be(entProgram.Id);

        // 3. Tháo ngành bằng Guid.Empty
        var cleared = await handler.Handle(
            new UpdateCourseCommand(course.Id, "ENT203", "English 203 Updated", 3, null, headEnt.Id, 3, 20, 5, true, semester.Id, admin.Id, Guid.Empty),
            CancellationToken.None);
        cleared.IsSuccess.Should().BeTrue();
        cleared.Value!.AcademicProgramId.Should().BeNull();
    }

    [Fact(DisplayName = "AP-06: UpdateAcademicProgram xử lý trùng mã CONFLICT, NOT_FOUND, Trưởng BM bị khóa VALIDATION và giữ nguyên Trưởng BM khi null")]
    public async Task UpdateAcademicProgram_EdgeCases_ConflictNotFoundInactiveHeadAndPreserveNull()
    {
        await using var context = CreateDbContext();
        var (admin, headSe, _, _, _, seProgram) = SeedBase(context);

        var inactiveHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "locked-head@prog-test.vn",
            FullName = "Locked Head",
            Role = UserRole.DepartmentHead,
            IsActive = false
        };
        var entProgram = new AcademicProgram
        {
            Id = Guid.NewGuid(),
            Code = "ENT",
            Name = "Bộ môn Tiếng Anh",
            DepartmentHeadUserId = headSe.Id,
            IsActive = true
        };
        context.Users.Add(inactiveHead);
        context.AcademicPrograms.Add(entProgram);
        await context.SaveChangesAsync();

        var handler = new UpdateAcademicProgramCommandHandler(context);

        var notFound = await handler.Handle(
            new UpdateAcademicProgramCommand(admin.Id, Guid.NewGuid(), "NEW", "Không tồn tại", true),
            CancellationToken.None);
        notFound.IsSuccess.Should().BeFalse();
        notFound.Error.Should().StartWith("NOT_FOUND:");

        var duplicateCode = await handler.Handle(
            new UpdateAcademicProgramCommand(admin.Id, entProgram.Id, "se", "Trùng SE", true),
            CancellationToken.None);
        duplicateCode.IsSuccess.Should().BeFalse();
        duplicateCode.Error.Should().StartWith("CONFLICT:");

        var lockedHead = await handler.Handle(
            new UpdateAcademicProgramCommand(admin.Id, entProgram.Id, "ENT", "Bộ môn ENT", true, inactiveHead.Id),
            CancellationToken.None);
        lockedHead.IsSuccess.Should().BeFalse();
        lockedHead.Error.Should().StartWith("VALIDATION:");

        var preservedHead = await handler.Handle(
            new UpdateAcademicProgramCommand(admin.Id, entProgram.Id, "ENT", "Bộ môn ENT Mới", true, null),
            CancellationToken.None);
        preservedHead.IsSuccess.Should().BeTrue();
        preservedHead.Value!.DepartmentHeadUserId.Should().Be(headSe.Id);
        preservedHead.Value.DepartmentHeadName.Should().Be(headSe.FullName);
    }

    [Fact(DisplayName = "AP-07: UpdateCourse chặn chuyển sang ngành tạm khóa nhưng vẫn cho phép giữ nguyên ngành đang tạm khóa của chính môn đó")]
    public async Task UpdateCourse_RejectsSwitchingToInactiveProgram_AllowsKeepingOwnInactiveProgram()
    {
        await using var context = CreateDbContext();
        var (admin, headSe, _, _, semester, seProgram) = SeedBase(context);

        var inactiveProgram = new AcademicProgram
        {
            Id = Guid.NewGuid(),
            Code = "OLD",
            Name = "Ngành cũ tạm khóa",
            IsActive = false
        };
        var courseInSe = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Backend",
            Credits = 3,
            SemesterId = semester.Id,
            AcademicProgramId = seProgram.Id,
            DepartmentHeadUserId = headSe.Id,
            IsActive = true
        };
        var courseInOld = new Course
        {
            Id = Guid.NewGuid(),
            Code = "OLD101",
            Name = "Legacy Course",
            Credits = 3,
            SemesterId = semester.Id,
            AcademicProgramId = inactiveProgram.Id,
            DepartmentHeadUserId = headSe.Id,
            IsActive = true
        };
        context.AcademicPrograms.Add(inactiveProgram);
        context.Courses.AddRange(courseInSe, courseInOld);
        await context.SaveChangesAsync();

        var handler = new UpdateCourseCommandHandler(context);

        var switchToInactive = await handler.Handle(
            new UpdateCourseCommand(courseInSe.Id, "PRN231", "Backend", 3, null, headSe.Id, 3, 20, 5, true, semester.Id, admin.Id, inactiveProgram.Id),
            CancellationToken.None);
        switchToInactive.IsSuccess.Should().BeFalse();
        switchToInactive.Error.Should().StartWith("VALIDATION:");

        var keepOwnInactive = await handler.Handle(
            new UpdateCourseCommand(courseInOld.Id, "OLD101", "Legacy Course Updated", 3, null, headSe.Id, 3, 20, 5, true, semester.Id, admin.Id, inactiveProgram.Id),
            CancellationToken.None);
        keepOwnInactive.IsSuccess.Should().BeTrue();
        keepOwnInactive.Value!.AcademicProgramId.Should().Be(inactiveProgram.Id);
        keepOwnInactive.Value.Name.Should().Be("Legacy Course Updated");
    }

    [Fact(DisplayName = "AP-08: CreateAcademicProgram & UpdateAcademicProgram bắt TOCTOU DbUpdateException (23505) trả CONFLICT thay vì 500")]
    public async Task CreateAndUpdateAcademicProgram_CatchUniqueViolation23505_ReturnConflict()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var context = new UniqueViolationThrowingDbContext(options);
        var (admin, _, _, _, _, seProgram) = SeedBase(context);
        context.ThrowOnNextSave = true;

        var createHandler = new CreateAcademicProgramCommandHandler(context);
        var createResult = await createHandler.Handle(
            new CreateAcademicProgramCommand(admin.Id, "AI", "Artificial Intelligence"),
            CancellationToken.None);
        createResult.IsSuccess.Should().BeFalse();
        createResult.Error.Should().StartWith("CONFLICT:");

        context.ChangeTracker.Clear();
        context.ThrowOnNextSave = true;

        var updateHandler = new UpdateAcademicProgramCommandHandler(context);
        var updateResult = await updateHandler.Handle(
            new UpdateAcademicProgramCommand(admin.Id, seProgram.Id, "AI", "Renamed Conflict", true),
            CancellationToken.None);
        updateResult.IsSuccess.Should().BeFalse();
        updateResult.Error.Should().StartWith("CONFLICT:");
    }

    private sealed class FakePostgresUniqueException : Exception
    {
        public string SqlState => "23505";
        public string ConstraintName => "ix_academic_programs_code";

        public FakePostgresUniqueException()
            : base("23505: duplicate key value violates unique constraint \"ix_academic_programs_code\"")
        {
        }
    }

    private sealed class UniqueViolationThrowingDbContext(DbContextOptions<OralExamDbContext> options)
        : OralExamDbContext(options)
    {
        public bool ThrowOnNextSave { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnNextSave)
            {
                ThrowOnNextSave = false;
                throw new DbUpdateException("Duplicate key violation", new FakePostgresUniqueException());
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
