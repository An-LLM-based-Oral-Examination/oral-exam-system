using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OralExamination.Application.Features.Courses.Commands.UpdateCourse;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using OralExamination.UnitTests.Features.InClassShifts;
using Xunit;

namespace OralExamination.UnitTests.Features.AcademicHierarchy;

/// <summary>
/// Bộ kiểm thử tích hợp CSDL PostgreSQL 16 thật cho Academic Foundation (TASK-004A).
/// Chạy trực tiếp trên oralexam_integration_test, kiểm chứng:
/// 1. ck_semesters_dates: Ràng buộc end_date > start_date (SQLState 23514).
/// 2. ix_semesters_code: Ràng buộc unique mã học kỳ (SQLState 23505).
/// 3. ck_courses_credits: Ràng buộc credits BETWEEN 1 AND 10 (SQLState 23514).
/// 4. ix_courses_code: Ràng buộc unique mã môn học (SQLState 23505).
/// 5. fk_courses_department_head: Khóa ngoại trỏ đến users(id) (SQLState 23503).
/// 6. ON DELETE RESTRICT: Ngăn chặn xóa học kỳ khi đang có môn học liên kết (SQLState 23503).
/// 7. T4A-F002: Chuyển học kỳ môn học A -> B đồng bộ offering trong cùng transaction.
/// 8. T4A-F002: Từ chối chuyển học kỳ (CONFLICT) và rollback khi đã có lớp học phụ thuộc.
/// Tự động dọn dẹp (cleanup) sạch sẽ sau khi test hoàn tất, bảo đảm ZERO-POLLUTION (0 rows rác).
/// </summary>
[Trait("Category", "Integration")]
public class AcademicFoundationPostgreSqlIntegrationTests : IAsyncLifetime
{
    private readonly List<Guid> _createdCourseIds = new();
    private readonly List<Guid> _createdSemesterIds = new();
    private readonly List<Guid> _createdUserIds = new();
    private readonly List<Guid> _createdClassIds = new();
    private OralExamDbContext? _context;

    public Task InitializeAsync()
    {
        _context = CreateNewContext();
        return Task.CompletedTask;
    }

    private OralExamDbContext CreateNewContext()
    {
        var connStr = InClassShiftConcurrencyIntegrationTests.GetPostgresConnectionString();
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseNpgsql(connStr)
            .Options;
        return new OralExamDbContext(options);
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            try
            {
                // 1. Dọn dẹp classes
                var classes = await _context.Classes
                    .Where(cl => _createdClassIds.Contains(cl.Id) ||
                                 _createdCourseIds.Contains(cl.CourseId) ||
                                 _createdSemesterIds.Contains(cl.SemesterId))
                    .ToListAsync();
                if (classes.Count > 0)
                {
                    _context.Classes.RemoveRange(classes);
                    await _context.SaveChangesAsync();
                }

                // 2. Dọn dẹp course_lecturers
                var courseLecturers = await _context.CourseLecturers
                    .Where(cl => _createdCourseIds.Contains(cl.CourseId) ||
                                 (cl.SemesterId.HasValue && _createdSemesterIds.Contains(cl.SemesterId.Value)))
                    .ToListAsync();
                if (courseLecturers.Count > 0)
                {
                    _context.CourseLecturers.RemoveRange(courseLecturers);
                    await _context.SaveChangesAsync();
                }

                // 3. Dọn dẹp course_offerings
                var courseOfferings = await _context.CourseOfferings
                    .Where(co => _createdCourseIds.Contains(co.CourseId) || _createdSemesterIds.Contains(co.SemesterId))
                    .ToListAsync();
                if (courseOfferings.Count > 0)
                {
                    _context.CourseOfferings.RemoveRange(courseOfferings);
                    await _context.SaveChangesAsync();
                }

                // 4. Dọn dẹp courses
                if (_createdCourseIds.Count > 0)
                {
                    var courses = await _context.Courses
                        .Where(c => _createdCourseIds.Contains(c.Id))
                        .ToListAsync();
                    if (courses.Count > 0)
                    {
                        _context.Courses.RemoveRange(courses);
                        await _context.SaveChangesAsync();
                    }
                }

                // 5. Dọn dẹp semesters
                if (_createdSemesterIds.Count > 0)
                {
                    var semesters = await _context.Semesters
                        .Where(s => _createdSemesterIds.Contains(s.Id))
                        .ToListAsync();
                    if (semesters.Count > 0)
                    {
                        _context.Semesters.RemoveRange(semesters);
                        await _context.SaveChangesAsync();
                    }
                }

                // 6. Dọn dẹp users
                if (_createdUserIds.Count > 0)
                {
                    var users = await _context.Users
                        .Where(u => _createdUserIds.Contains(u.Id))
                        .ToListAsync();
                    if (users.Count > 0)
                    {
                        _context.Users.RemoveRange(users);
                        await _context.SaveChangesAsync();
                    }
                }

                // 7. Dọn dẹp audit_logs
                var allTargetIds = _createdCourseIds
                    .Concat(_createdSemesterIds)
                    .Concat(_createdUserIds)
                    .Concat(_createdClassIds)
                    .ToList();

                if (allTargetIds.Count > 0)
                {
                    var audits = await _context.AuditLogs
                        .Where(a => a.EntityId.HasValue && allTargetIds.Contains(a.EntityId.Value))
                        .ToListAsync();
                    if (audits.Count > 0)
                    {
                        _context.AuditLogs.RemoveRange(audits);
                        await _context.SaveChangesAsync();
                    }
                }
            }
            finally
            {
                await _context.DisposeAsync();
            }
        }
    }

    [Fact(DisplayName = "PG 1a. ck_semesters_dates từ chối chèn học kỳ có ngày kết thúc trước ngày bắt đầu")]
    public async Task PostgreSql_CkSemestersDates_Rejects_EndDate_Before_StartDate()
    {
        await using var ctx = CreateNewContext();

        var invalidSemester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = $"INV_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Invalid Date Semester",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 9, 1), // Invalid: EndDate < StartDate
            IsActive = true
        };

        _createdSemesterIds.Add(invalidSemester.Id);
        ctx.Semesters.Add(invalidSemester);

        var act = async () => await ctx.SaveChangesAsync();

        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        var pgEx = ex.Which.InnerException as PostgresException;
        pgEx.Should().NotBeNull();
        pgEx!.SqlState.Should().Be("23514"); // check_violation
        pgEx.ConstraintName.Should().Be("ck_semesters_dates");
    }

    [Fact(DisplayName = "PG 1b. ck_semesters_dates từ chối chèn học kỳ có ngày kết thúc BẰNG ngày bắt đầu (T4A-F006)")]
    public async Task PostgreSql_CkSemestersDates_Rejects_EndDate_Equal_To_StartDate()
    {
        await using var ctx = CreateNewContext();

        var equalSemester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = $"EQU_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Equal Date Semester",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 10, 1), // Invalid: EndDate == StartDate
            IsActive = true
        };

        _createdSemesterIds.Add(equalSemester.Id);
        ctx.Semesters.Add(equalSemester);

        var act = async () => await ctx.SaveChangesAsync();

        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        var pgEx = ex.Which.InnerException as PostgresException;
        pgEx.Should().NotBeNull();
        pgEx!.SqlState.Should().Be("23514"); // check_violation
        pgEx.ConstraintName.Should().Be("ck_semesters_dates");
    }

    [Fact(DisplayName = "PG 2. ix_semesters_code từ chối chèn trùng mã học kỳ")]
    public async Task PostgreSql_IxSemestersCode_Rejects_Duplicate_Code()
    {
        await using var ctx = CreateNewContext();
        var uniqueCode = $"SEM_{Guid.NewGuid():N}"[..18].ToUpperInvariant();

        var sem1 = new Semester
        {
            Id = Guid.NewGuid(),
            Code = uniqueCode,
            Name = "Semester First",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30),
            IsActive = true
        };
        _createdSemesterIds.Add(sem1.Id);
        ctx.Semesters.Add(sem1);
        await ctx.SaveChangesAsync();

        var sem2 = new Semester
        {
            Id = Guid.NewGuid(),
            Code = uniqueCode, // Duplicate
            Name = "Semester Second",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30),
            IsActive = true
        };
        _createdSemesterIds.Add(sem2.Id);
        ctx.Semesters.Add(sem2);

        var act = async () => await ctx.SaveChangesAsync();

        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        var pgEx = ex.Which.InnerException as PostgresException;
        pgEx.Should().NotBeNull();
        pgEx!.SqlState.Should().Be("23505"); // unique_violation
    }

    [Fact(DisplayName = "PG 3a. ck_courses_credits từ chối credits <= 0 (raw SQL và EF Core -1) (T4A-F008)")]
    public async Task PostgreSql_CkCoursesCredits_Rejects_Credits_Less_Than_Or_Equal_To_Zero()
    {
        await using var ctx = CreateNewContext();

        var sem = new Semester
        {
            Id = Guid.NewGuid(),
            Code = $"SEM_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Semester for Credits Test",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30),
            IsActive = true
        };
        _createdSemesterIds.Add(sem.Id);
        ctx.Semesters.Add(sem);
        await ctx.SaveChangesAsync();

        // 3a.1: Credits = -1 via EF Core
        var invalidCourseNeg = new Course
        {
            Id = Guid.NewGuid(),
            Code = $"CRSN_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Negative Credits Course",
            Credits = -1,
            SemesterId = sem.Id,
            IsActive = true
        };
        _createdCourseIds.Add(invalidCourseNeg.Id);
        ctx.Courses.Add(invalidCourseNeg);

        var actNeg = async () => await ctx.SaveChangesAsync();
        var exNeg = await actNeg.Should().ThrowAsync<DbUpdateException>();
        var pgExNeg = exNeg.Which.InnerException as PostgresException;
        pgExNeg.Should().NotBeNull();
        pgExNeg!.SqlState.Should().Be("23514"); // check_violation
        pgExNeg.ConstraintName.Should().Be("ck_courses_credits");

        // Detach failed entity
        ctx.Entry(invalidCourseNeg).State = EntityState.Detached;

        // 3a.2: Credits = 0 via Raw SQL to prove database physically rejects credits = 0
        var zeroCourseId = Guid.NewGuid();
        var zeroCode = $"CRS0_{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        _createdCourseIds.Add(zeroCourseId);

        var actZero = async () => await ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO courses (id, code, name, credits, semester_id, is_active) VALUES ({0}, {1}, {2}, 0, {3}, true)",
            zeroCourseId, zeroCode, "Zero Credits Course", sem.Id);

        var exZero = await actZero.Should().ThrowAsync<PostgresException>();
        exZero.Which.SqlState.Should().Be("23514"); // check_violation
        exZero.Which.ConstraintName.Should().Be("ck_courses_credits");
    }

    [Fact(DisplayName = "PG 3b. ck_courses_credits từ chối credits = 11 (> 10) (T4A-F008)")]
    public async Task PostgreSql_CkCoursesCredits_Rejects_Credits_Greater_Than_10()
    {
        await using var ctx = CreateNewContext();

        var sem = new Semester
        {
            Id = Guid.NewGuid(),
            Code = $"SEM_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Semester for Credits 11 Test",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30),
            IsActive = true
        };
        _createdSemesterIds.Add(sem.Id);
        ctx.Semesters.Add(sem);
        await ctx.SaveChangesAsync();

        var invalidCourse11 = new Course
        {
            Id = Guid.NewGuid(),
            Code = $"CRS11_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Eleven Credits Course",
            Credits = 11, // Exceeds upper limit 10
            SemesterId = sem.Id,
            IsActive = true
        };
        _createdCourseIds.Add(invalidCourse11.Id);
        ctx.Courses.Add(invalidCourse11);

        var act = async () => await ctx.SaveChangesAsync();

        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        var pgEx = ex.Which.InnerException as PostgresException;
        pgEx.Should().NotBeNull();
        pgEx!.SqlState.Should().Be("23514"); // check_violation
        pgEx.ConstraintName.Should().Be("ck_courses_credits");
    }

    [Fact(DisplayName = "PG 4. ix_courses_code từ chối chèn trùng mã môn học")]
    public async Task PostgreSql_IxCoursesCode_Rejects_Duplicate_Code()
    {
        await using var ctx = CreateNewContext();

        var sem = new Semester
        {
            Id = Guid.NewGuid(),
            Code = $"SEM_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Semester for Course Test",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30),
            IsActive = true
        };
        _createdSemesterIds.Add(sem.Id);
        ctx.Semesters.Add(sem);
        await ctx.SaveChangesAsync();

        var duplicateCode = $"CRS_{Guid.NewGuid():N}"[..18].ToUpperInvariant();

        var course1 = new Course
        {
            Id = Guid.NewGuid(),
            Code = duplicateCode,
            Name = "Course 1",
            Credits = 3,
            SemesterId = sem.Id,
            IsActive = true
        };
        _createdCourseIds.Add(course1.Id);
        ctx.Courses.Add(course1);
        await ctx.SaveChangesAsync();

        var course2 = new Course
        {
            Id = Guid.NewGuid(),
            Code = duplicateCode, // Duplicate
            Name = "Course 2",
            Credits = 3,
            SemesterId = sem.Id,
            IsActive = true
        };
        _createdCourseIds.Add(course2.Id);
        ctx.Courses.Add(course2);

        var act = async () => await ctx.SaveChangesAsync();

        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        var pgEx = ex.Which.InnerException as PostgresException;
        pgEx.Should().NotBeNull();
        pgEx!.SqlState.Should().Be("23505"); // unique_violation
    }

    [Fact(DisplayName = "PG 5. Khóa ngoại department_head_user_id từ chối User ID không tồn tại")]
    public async Task PostgreSql_FkCoursesDepartmentHead_Rejects_NonExistent_User()
    {
        await using var ctx = CreateNewContext();

        var sem = new Semester
        {
            Id = Guid.NewGuid(),
            Code = $"SEM_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Semester for Course Test",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30),
            IsActive = true
        };
        _createdSemesterIds.Add(sem.Id);
        ctx.Semesters.Add(sem);
        await ctx.SaveChangesAsync();

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = $"CRS_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Course with Bad Dept Head",
            Credits = 3,
            SemesterId = sem.Id,
            DepartmentHeadUserId = Guid.NewGuid(), // Non-existent user
            IsActive = true
        };
        _createdCourseIds.Add(course.Id);
        ctx.Courses.Add(course);

        var act = async () => await ctx.SaveChangesAsync();

        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        var pgEx = ex.Which.InnerException as PostgresException;
        pgEx.Should().NotBeNull();
        pgEx!.SqlState.Should().Be("23503"); // foreign_key_violation
    }

    [Fact(DisplayName = "PG 6. ON DELETE RESTRICT bảo vệ học kỳ: Từ chối xóa học kỳ khi đang có môn học liên kết")]
    public async Task PostgreSql_FkCoursesSemester_OnDeleteRestrict_Prevents_Semester_Deletion()
    {
        await using var ctx = CreateNewContext();

        var sem = new Semester
        {
            Id = Guid.NewGuid(),
            Code = $"SEM_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Protected Semester",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30),
            IsActive = true
        };
        _createdSemesterIds.Add(sem.Id);
        ctx.Semesters.Add(sem);
        await ctx.SaveChangesAsync();

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = $"CRS_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Dependent Course",
            Credits = 3,
            SemesterId = sem.Id,
            IsActive = true
        };
        _createdCourseIds.Add(course.Id);
        ctx.Courses.Add(course);
        await ctx.SaveChangesAsync();

        // Attempt to delete semester in PostgreSQL while course references it with ON DELETE RESTRICT
        var act = async () => await ctx.Database.ExecuteSqlRawAsync(
            "DELETE FROM semesters WHERE id = {0}", sem.Id);

        var ex = await act.Should().ThrowAsync<PostgresException>();
        ex.Which.SqlState.Should().Be("23503"); // foreign_key_violation due to ON DELETE RESTRICT
    }

    [Fact(DisplayName = "PG 7. T4A-F002: Chuyển học kỳ A -> B đồng bộ CourseOffering trong cùng transaction")]
    public async Task PostgreSql_Course_Semester_Transition_A_To_B_Syncs_CourseOffering_In_Transaction()
    {
        await using var ctx = CreateNewContext();

        // 1. Tạo 2 học kỳ A và B
        var semA = new Semester
        {
            Id = Guid.NewGuid(),
            Code = $"SEMA_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Semester A",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30),
            IsActive = true
        };
        var semB = new Semester
        {
            Id = Guid.NewGuid(),
            Code = $"SEMB_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Semester B",
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 8, 31),
            IsActive = true
        };
        _createdSemesterIds.AddRange(new[] { semA.Id, semB.Id });
        ctx.Semesters.AddRange(semA, semB);

        // 2. Tạo môn học gắn với Học kỳ A và offering ban đầu của A
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = $"CRSTR_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Course for Transition",
            Credits = 3,
            SemesterId = semA.Id,
            IsActive = true
        };
        _createdCourseIds.Add(course.Id);
        ctx.Courses.Add(course);

        var offeringA = new CourseOffering
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SemesterId = semA.Id,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.CourseOfferings.Add(offeringA);
        await ctx.SaveChangesAsync();

        // 3. Thực thi UpdateCourseCommandHandler chuyển môn từ Học kỳ A sang Học kỳ B
        var validator = new UpdateCourseCommandValidator();
        var handler = new UpdateCourseCommandHandler(ctx, validator);

        var updateCmd = new UpdateCourseCommand(
            course.Id,
            course.Code,
            course.Name,
            course.Credits,
            course.Description,
            null,
            course.MaxMockExamsPerDay,
            course.MockExamDurationMinutes,
            course.MockExamTotalQuestions,
            course.IsActive,
            semB.Id); // Target Semester B

        var result = await handler.Handle(updateCmd, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value!.SemesterId.Should().Be(semB.Id);

        // 4. Kiểm tra trong PostgreSQL thật:
        var updatedCourseInDb = await ctx.Courses.AsNoTracking().FirstAsync(c => c.Id == course.Id);
        updatedCourseInDb.SemesterId.Should().Be(semB.Id);

        // Offering mới ở học kỳ B đã được tạo và enabled
        var offeringBInDb = await ctx.CourseOfferings.AsNoTracking()
            .FirstOrDefaultAsync(co => co.CourseId == course.Id && co.SemesterId == semB.Id);
        offeringBInDb.Should().NotBeNull();
        offeringBInDb!.IsEnabled.Should().BeTrue();

        // Offering cũ ở học kỳ A được bảo toàn lịch sử và chuyển sang IsEnabled = false
        var offeringAInDb = await ctx.CourseOfferings.AsNoTracking()
            .FirstOrDefaultAsync(co => co.CourseId == course.Id && co.SemesterId == semA.Id);
        offeringAInDb.Should().NotBeNull();
        offeringAInDb!.IsEnabled.Should().BeFalse();
    }

    [Fact(DisplayName = "PG 8. T4A-F002: Từ chối chuyển học kỳ (409 Conflict) và rollback toàn vẹn khi có Class phụ thuộc")]
    public async Task PostgreSql_Course_Semester_Transition_Fails_When_Class_Dependency_Exists_And_RollsBack()
    {
        await using var ctx = CreateNewContext();

        // 1. Tạo 2 học kỳ A và B
        var semA = new Semester
        {
            Id = Guid.NewGuid(),
            Code = $"SCA_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Semester Conf A",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30),
            IsActive = true
        };
        var semB = new Semester
        {
            Id = Guid.NewGuid(),
            Code = $"SCB_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Semester Conf B",
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 8, 31),
            IsActive = true
        };
        _createdSemesterIds.AddRange(new[] { semA.Id, semB.Id });
        ctx.Semesters.AddRange(semA, semB);

        // 2. Tạo Lecturer sở hữu lớp học
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = $"lec_{Guid.NewGuid():N}"[..18] + "@fpt.edu.vn",
            FullName = "Lecturer With Class",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        _createdUserIds.Add(lecturer.Id);
        ctx.Users.Add(lecturer);

        // 3. Tạo môn học và lớp học phần liên kết với Học kỳ A
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = $"CRSC_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Course With Class Dependency",
            Credits = 3,
            SemesterId = semA.Id,
            IsActive = true
        };
        _createdCourseIds.Add(course.Id);
        ctx.Courses.Add(course);

        var academicClass = new Class
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SemesterId = semA.Id,
            LecturerId = lecturer.Id,
            Code = $"SE18_{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            Name = "Class SE18 Dependent",
            JoinCode = $"JC{Guid.NewGuid():N}"[..6].ToUpperInvariant(),
            JoinPasswordHash = "hashed_pw",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _createdClassIds.Add(academicClass.Id);
        ctx.Classes.Add(academicClass);

        await ctx.SaveChangesAsync();

        // 4. Thử cập nhật chuyển học kỳ sang Học kỳ B
        var validator = new UpdateCourseCommandValidator();
        var handler = new UpdateCourseCommandHandler(ctx, validator);

        var updateCmd = new UpdateCourseCommand(
            course.Id,
            course.Code,
            course.Name,
            course.Credits,
            course.Description,
            null,
            course.MaxMockExamsPerDay,
            course.MockExamDurationMinutes,
            course.MockExamTotalQuestions,
            course.IsActive,
            semB.Id); // Target Semester B

        var result = await handler.Handle(updateCmd, CancellationToken.None);

        // Phải trả về lỗi CONFLICT (được controller map sang HTTP 409 Conflict)
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT:");
        result.Error.Should().Contain("Không thể chuyển học kỳ của môn học vì đã có lớp học hoặc phân công giảng viên",
            "T4A-F002: Phải bảo vệ tính toàn vẹn và từ chối chuyển học kỳ khi có lớp phụ thuộc");

        // 5. Kiểm tra trong PostgreSQL: Dữ liệu không bị thay đổi (Rollback sạch)
        var courseInDb = await ctx.Courses.AsNoTracking().FirstAsync(c => c.Id == course.Id);
        courseInDb.SemesterId.Should().Be(semA.Id, "Course.SemesterId phải giữ nguyên là Học kỳ A");

        var offeringBInDb = await ctx.CourseOfferings.AsNoTracking()
            .FirstOrDefaultAsync(co => co.CourseId == course.Id && co.SemesterId == semB.Id);
        offeringBInDb.Should().BeNull("Tuyệt đối không được tạo offering ở Học kỳ B khi transaction thất bại");
    }

    [Fact(DisplayName = "PG 9. T4A-F002-R1.1: Từ chối chuyển học kỳ (409 Conflict) và rollback toàn vẹn khi có CourseLecturer phụ thuộc offering cũ dù SemesterId NULL")]
    public async Task PostgreSql_Course_Semester_Transition_Fails_When_Lecturer_Offering_Dependency_Exists_With_Null_SemesterId_And_RollsBack()
    {
        await using var ctx = CreateNewContext();

        // 1. Tạo 2 học kỳ A và B
        var semA = new Semester
        {
            Id = Guid.NewGuid(),
            Code = $"SLA_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Semester Lec A",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30),
            IsActive = true
        };
        var semB = new Semester
        {
            Id = Guid.NewGuid(),
            Code = $"SLB_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Semester Lec B",
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 8, 31),
            IsActive = true
        };
        _createdSemesterIds.AddRange(new[] { semA.Id, semB.Id });
        ctx.Semesters.AddRange(semA, semB);

        // 2. Tạo Lecturer
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = $"lec_dep_{Guid.NewGuid():N}"[..18] + "@fpt.edu.vn",
            FullName = "Lecturer Dependent On Offering",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        _createdUserIds.Add(lecturer.Id);
        ctx.Users.Add(lecturer);

        // 3. Tạo môn học và offering cho Học kỳ A
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = $"CRSL_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            Name = "Course With Lecturer Offering Dependency",
            Credits = 3,
            SemesterId = semA.Id,
            IsActive = true
        };
        _createdCourseIds.Add(course.Id);
        ctx.Courses.Add(course);

        var offeringA = new CourseOffering
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SemesterId = semA.Id,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.CourseOfferings.Add(offeringA);

        // 4. Tạo CourseLecturer phụ thuộc vào offeringA nhưng SemesterId = NULL
        var courseLecturer = new CourseLecturer
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            LecturerUserId = lecturer.Id,
            AssignedByUserId = lecturer.Id,
            CourseOfferingId = offeringA.Id,
            SemesterId = null, // Bắt buộc NULL để kiểm chứng T4A-F002-R1.1
            IsActive = true,
            AssignedAt = DateTime.UtcNow
        };
        ctx.CourseLecturers.Add(courseLecturer);

        await ctx.SaveChangesAsync();

        // 5. Thử cập nhật chuyển học kỳ sang Học kỳ B
        var validator = new UpdateCourseCommandValidator();
        var handler = new UpdateCourseCommandHandler(ctx, validator);

        var updateCmd = new UpdateCourseCommand(
            course.Id,
            course.Code,
            course.Name,
            course.Credits,
            course.Description,
            null,
            course.MaxMockExamsPerDay,
            course.MockExamDurationMinutes,
            course.MockExamTotalQuestions,
            course.IsActive,
            semB.Id); // Target Semester B

        var result = await handler.Handle(updateCmd, CancellationToken.None);

        // Phải trả về lỗi CONFLICT (HTTP 409 Conflict)
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT:");
        result.Error.Should().Contain("Không thể chuyển học kỳ của môn học vì đã có lớp học hoặc phân công giảng viên",
            "T4A-F002-R1.1: Phải phát hiện phân công giảng viên phụ thuộc qua offering cũ dù SemesterId NULL");

        // 6. Kiểm tra trong PostgreSQL: Rollback toàn vẹn
        var courseInDb = await ctx.Courses.AsNoTracking().FirstAsync(c => c.Id == course.Id);
        courseInDb.SemesterId.Should().Be(semA.Id, "Course.SemesterId phải giữ nguyên là Học kỳ A");

        var offeringAInDb = await ctx.CourseOfferings.AsNoTracking().FirstAsync(co => co.Id == offeringA.Id);
        offeringAInDb.IsEnabled.Should().BeTrue("Offering A phải giữ nguyên trạng thái IsEnabled = true");

        var offeringBInDb = await ctx.CourseOfferings.AsNoTracking()
            .FirstOrDefaultAsync(co => co.CourseId == course.Id && co.SemesterId == semB.Id);
        offeringBInDb.Should().BeNull("Tuyệt đối không được tạo offering ở Học kỳ B khi transaction thất bại");
    }
}
