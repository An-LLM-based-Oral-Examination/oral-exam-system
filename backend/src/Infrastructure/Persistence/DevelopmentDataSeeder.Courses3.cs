using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Security;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;

namespace OralExamination.Infrastructure.Persistence;

public static partial class DevelopmentDataSeeder
{
    public static readonly Guid CoursePrf192Id = Guid.Parse("00000000-0000-0000-0003-000000000001");
    public static readonly Guid CourseCsd201Id = Guid.Parse("00000000-0000-0000-0003-000000000002");
    public static readonly Guid CourseDbi202Id = Guid.Parse("00000000-0000-0000-0003-000000000003");

    public static readonly Guid RubricPrf192Id = Guid.Parse("00000000-0000-0000-0003-000000000011");
    public static readonly Guid RubricCsd201Id = Guid.Parse("00000000-0000-0000-0003-000000000012");
    public static readonly Guid RubricDbi202Id = Guid.Parse("00000000-0000-0000-0003-000000000013");

    public static readonly Guid StructurePrf192Id = Guid.Parse("00000000-0000-0000-0003-000000000021");
    public static readonly Guid StructureCsd201Id = Guid.Parse("00000000-0000-0000-0003-000000000022");
    public static readonly Guid StructureDbi202Id = Guid.Parse("00000000-0000-0000-0003-000000000023");

    public static readonly Guid ClassPrf192Id = Guid.Parse("00000000-0000-0000-0003-000000000041");
    public static readonly Guid ClassCsd201Id = Guid.Parse("00000000-0000-0000-0003-000000000042");
    public static readonly Guid ClassDbi202Id = Guid.Parse("00000000-0000-0000-0003-000000000043");

    public static readonly Guid ClassSwt301Id = Guid.Parse("00000000-0000-0000-0008-000000000031");
    public static readonly Guid ClassSwr302Id = Guid.Parse("00000000-0000-0000-0008-000000000032");
    public static readonly Guid ClassSwd392Id = Guid.Parse("00000000-0000-0000-0008-000000000033");

    public static readonly Guid ExamSetPrn231Set02Id = Guid.Parse("00000000-0000-0000-0000-00000000001a");
    public static readonly Guid ExamSetPrf192Id = Guid.Parse("00000000-0000-0000-0003-000000000031");
    public static readonly Guid ExamSetCsd201Id = Guid.Parse("00000000-0000-0000-0003-000000000032");
    public static readonly Guid ExamSetDbi202Id = Guid.Parse("00000000-0000-0000-0003-000000000033");

    public static readonly Guid ExaminationOfficerUserId = Guid.Parse("00000000-0000-0000-0000-00000000000c");
    public static readonly Guid AcademicProgramSeId = Guid.Parse("00000000-0000-0000-0003-000000000090");

    public static readonly Guid OfferingPrn231Fa26Id = Guid.Parse("00000000-0000-0000-0003-000000000091");
    public static readonly Guid OfferingPrf192Fa26Id = Guid.Parse("00000000-0000-0000-0003-000000000094");
    public static readonly Guid OfferingCsd201Fa26Id = Guid.Parse("00000000-0000-0000-0003-000000000095");
    public static readonly Guid OfferingDbi202Fa26Id = Guid.Parse("00000000-0000-0000-0003-000000000096");

    public static readonly Guid InClassShiftPrf192Id = Guid.Parse("00000000-0000-0000-0003-000000000097");
    public static readonly Guid InClassShiftCsd201Id = Guid.Parse("00000000-0000-0000-0003-000000000098");
    public static readonly Guid InClassShiftDbi202Id = Guid.Parse("00000000-0000-0000-0003-000000000099");

    public static readonly Guid InClassShiftSwt301Id = Guid.Parse("00000000-0000-0000-0008-000000000041");
    public static readonly Guid InClassShiftSwr302Id = Guid.Parse("00000000-0000-0000-0008-000000000042");
    public static readonly Guid InClassShiftSwd392Id = Guid.Parse("00000000-0000-0000-0008-000000000043");

    public static Task SeedAdditionalThreeCoursesAndClosAsync(
        OralExamDbContext context,
        CancellationToken cancellationToken = default) =>
        SeedAdditionalThreeCoursesAndClosAsync(context, classJoinPasswordProtector: null, cancellationToken);

    public static async Task SeedAdditionalThreeCoursesAndClosAsync(
        OralExamDbContext context,
        IClassJoinPasswordProtector? classJoinPasswordProtector,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeClassesLecturersAndShiftsAsync(context, classJoinPasswordProtector, cancellationToken);
    }

    private static async Task EnsureSeClassesLecturersAndShiftsAsync(
        OralExamDbContext context,
        IClassJoinPasswordProtector? classJoinPasswordProtector,
        CancellationToken cancellationToken)
    {
        var semester = await context.Semesters
            .FirstOrDefaultAsync(s => s.Id == SemesterFa26Id || s.Code == "FA26", cancellationToken);
        var semesterId = semester?.Id ?? SemesterFa26Id;

        var existingProgram = await context.AcademicPrograms
            .FirstOrDefaultAsync(p => p.Id == AcademicProgramSeId || p.Code == "SE", cancellationToken);
        if (existingProgram == null)
        {
            existingProgram = new AcademicProgram
            {
                Id = AcademicProgramSeId,
                Code = "SE",
                Name = "Kỹ thuật Phần mềm (Software Engineering)",
                DepartmentHeadUserId = DepartmentHeadUserId,
                IsActive = true,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            };
            context.AcademicPrograms.Add(existingProgram);
            await context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            existingProgram.DepartmentHeadUserId = DepartmentHeadUserId;
            existingProgram.IsActive = true;
            existingProgram.UpdatedAt = SeedTimestampUtc;
        }

        var activeSeCodes = new[] { "PRN231", "SWT301", "SWR302", "SWD392" };
        var allCourses = await context.Courses.ToListAsync(cancellationToken);
        var existingOfferings = await context.CourseOfferings.ToListAsync(cancellationToken);
        var existingCourseLecturers = await context.CourseLecturers.ToListAsync(cancellationToken);
        var courseToOfferingId = new Dictionary<Guid, Guid>();

        foreach (var courseCode in activeSeCodes)
        {
            var actualCourse = allCourses.FirstOrDefault(c =>
                string.Equals(c.Code, courseCode, StringComparison.OrdinalIgnoreCase));
            if (actualCourse == null) continue;

            actualCourse.AcademicProgramId = existingProgram.Id;
            actualCourse.DepartmentHeadUserId = DepartmentHeadUserId;
            actualCourse.IsActive = true;

            var offering = existingOfferings.FirstOrDefault(o =>
                o.CourseId == actualCourse.Id && o.SemesterId == semesterId);
            if (offering != null)
            {
                offering.IsEnabled = true;
                offering.UpdatedAt = SeedTimestampUtc;
                courseToOfferingId[actualCourse.Id] = offering.Id;
            }

            var effectiveLectureId = context.Users.Local
                .FirstOrDefault(u => string.Equals(u.Username, "lecture", StringComparison.OrdinalIgnoreCase))?.Id
                ?? LecturerLectureId;
            var lecturerIdsToAssign = effectiveLectureId == LecturerLectureId
                ? new[] { LecturerUserId, LecturerLectureId }
                : new[] { LecturerUserId, LecturerLectureId, effectiveLectureId };

            foreach (var targetLecturerId in lecturerIdsToAssign)
            {
                var existingAssignment = existingCourseLecturers.FirstOrDefault(cl =>
                    cl.CourseId == actualCourse.Id && cl.LecturerUserId == targetLecturerId);

                if (existingAssignment == null)
                {
                    var newAssignment = new CourseLecturer
                    {
                        Id = Guid.NewGuid(),
                        CourseId = actualCourse.Id,
                        CourseOfferingId = courseToOfferingId.GetValueOrDefault(actualCourse.Id) is var oid && oid != Guid.Empty ? oid : null,
                        LecturerUserId = targetLecturerId,
                        AssignedByUserId = DepartmentHeadUserId,
                        SemesterId = semesterId,
                        AssignedAt = SeedTimestampUtc,
                        IsActive = true,
                        CreatedAt = SeedTimestampUtc,
                        UpdatedAt = SeedTimestampUtc
                    };
                    context.CourseLecturers.Add(newAssignment);
                    existingCourseLecturers.Add(newAssignment);
                }
                else
                {
                    if (courseToOfferingId.TryGetValue(actualCourse.Id, out var oid))
                    {
                        existingAssignment.CourseOfferingId ??= oid;
                    }
                    existingAssignment.SemesterId ??= semesterId;
                    existingAssignment.IsActive = true;
                    existingAssignment.UpdatedAt = SeedTimestampUtc;
                }
            }

            if (string.Equals(courseCode, "PRN231", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var extraLecturerId in new[] { Lecturer2UserId, Lecturer3UserId, Lecturer4UserId })
            {
                var extraAssignment = existingCourseLecturers.FirstOrDefault(cl =>
                    cl.CourseId == actualCourse.Id && cl.LecturerUserId == extraLecturerId);
                if (extraAssignment == null)
                {
                    extraAssignment = new CourseLecturer
                    {
                        Id = Guid.NewGuid(),
                        CourseId = actualCourse.Id,
                        CourseOfferingId = courseToOfferingId.GetValueOrDefault(actualCourse.Id) is var oid && oid != Guid.Empty ? oid : null,
                        LecturerUserId = extraLecturerId,
                        AssignedByUserId = DepartmentHeadUserId,
                        SemesterId = semesterId,
                        AssignedAt = SeedTimestampUtc,
                        IsActive = true,
                        CreatedAt = SeedTimestampUtc,
                        UpdatedAt = SeedTimestampUtc
                    };
                    context.CourseLecturers.Add(extraAssignment);
                    existingCourseLecturers.Add(extraAssignment);
                }
                else
                {
                    if (courseToOfferingId.TryGetValue(actualCourse.Id, out var oid))
                    {
                        extraAssignment.CourseOfferingId ??= oid;
                    }
                    extraAssignment.SemesterId ??= semesterId;
                    extraAssignment.IsActive = true;
                    extraAssignment.UpdatedAt = SeedTimestampUtc;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        // Seed/upsert active classes for the 3 new SE courses (SWT301, SWR302, SWD392)
        var swtCourse = allCourses.FirstOrDefault(c => string.Equals(c.Code, "SWT301", StringComparison.OrdinalIgnoreCase));
        var swrCourse = allCourses.FirstOrDefault(c => string.Equals(c.Code, "SWR302", StringComparison.OrdinalIgnoreCase));
        var swdCourse = allCourses.FirstOrDefault(c => string.Equals(c.Code, "SWD392", StringComparison.OrdinalIgnoreCase));

        var newSeClasses = new List<Class>();
        if (swtCourse is not null)
        {
            newSeClasses.Add(new Class
            {
                Id = ClassSwt301Id,
                CourseId = swtCourse.Id,
                CourseOfferingId = courseToOfferingId.GetValueOrDefault(swtCourse.Id) is var o1 && o1 != Guid.Empty ? o1 : null,
                Code = "SE1802-SWT",
                Name = "Lớp SE1802-SWT",
                JoinCode = "SE1802SWT",
                SemesterId = semesterId,
                LecturerId = LecturerUserId,
                MaxStudents = 40,
                IsActive = true,
                IsJoinEnabled = true,
                AllowSelfEnrollment = true,
                CreatedAt = SeedTimestampUtc
            });
        }
        if (swrCourse is not null)
        {
            newSeClasses.Add(new Class
            {
                Id = ClassSwr302Id,
                CourseId = swrCourse.Id,
                CourseOfferingId = courseToOfferingId.GetValueOrDefault(swrCourse.Id) is var o2 && o2 != Guid.Empty ? o2 : null,
                Code = "SE1803-SWR",
                Name = "Lớp SE1803-SWR",
                JoinCode = "SE1803SWR",
                SemesterId = semesterId,
                LecturerId = LecturerUserId,
                MaxStudents = 40,
                IsActive = true,
                IsJoinEnabled = true,
                AllowSelfEnrollment = true,
                CreatedAt = SeedTimestampUtc
            });
        }
        if (swdCourse is not null)
        {
            newSeClasses.Add(new Class
            {
                Id = ClassSwd392Id,
                CourseId = swdCourse.Id,
                CourseOfferingId = courseToOfferingId.GetValueOrDefault(swdCourse.Id) is var o3 && o3 != Guid.Empty ? o3 : null,
                Code = "SE1804-SWD",
                Name = "Lớp SE1804-SWD",
                JoinCode = "SE1804SWD",
                SemesterId = semesterId,
                LecturerId = LecturerUserId,
                MaxStudents = 40,
                IsActive = true,
                IsJoinEnabled = true,
                AllowSelfEnrollment = true,
                CreatedAt = SeedTimestampUtc
            });
        }

        var existingClasses = await context.Classes.ToListAsync(cancellationToken);
        foreach (var cls in newSeClasses)
        {
            cls.JoinPasswordHash = Pbkdf2PasswordHasher.HashForSeed("123456", cls.Code);
            if (classJoinPasswordProtector is not null)
            {
                cls.JoinPasswordProtected = classJoinPasswordProtector.Protect("123456");
            }

            var existing = existingClasses.FirstOrDefault(c => c.Id == cls.Id || c.Code == cls.Code);
            if (existing == null)
            {
                context.Classes.Add(cls);
                existingClasses.Add(cls);
            }
            else
            {
                existing.CourseId = cls.CourseId;
                existing.CourseOfferingId ??= cls.CourseOfferingId;
                existing.IsActive = true;
                existing.IsJoinEnabled = true;
                existing.AllowSelfEnrollment = true;
                if (string.IsNullOrWhiteSpace(existing.JoinCode))
                {
                    existing.JoinCode = cls.JoinCode;
                }
                if (string.IsNullOrWhiteSpace(existing.Name))
                {
                    existing.Name = cls.Name;
                }
                if (string.IsNullOrWhiteSpace(existing.JoinPasswordHash))
                {
                    existing.JoinPasswordHash = Pbkdf2PasswordHasher.HashForSeed("123456", existing.Code);
                }
                if (classJoinPasswordProtector is not null && string.IsNullOrWhiteSpace(existing.JoinPasswordProtected))
                {
                    existing.JoinPasswordProtected = classJoinPasswordProtector.Protect("123456");
                }
            }
        }

        // Soft-hide pre-existing legacy classes (SE1804-PRF, SE1805-CSD, SE1806-DBI) without deleting rows
        var legacyClassIds = new HashSet<Guid> { ClassPrf192Id, ClassCsd201Id, ClassDbi202Id };
        var legacyClassCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SE1804-PRF", "SE1805-CSD", "SE1806-DBI" };
        foreach (var legacyCls in existingClasses.Where(c => legacyClassIds.Contains(c.Id) || legacyClassCodes.Contains(c.Code)))
        {
            legacyCls.IsActive = false;
            legacyCls.IsJoinEnabled = false;
            legacyCls.AllowSelfEnrollment = false;
        }

        await context.SaveChangesAsync(cancellationToken);

        // Close any unlocked IN_PROGRESS shifts on legacy classes (never re-open them; never touch IsLocked == true)
        var legacyShiftIds = new[] { InClassShiftPrf192Id, InClassShiftCsd201Id, InClassShiftDbi202Id };
        var openLegacyShifts = await context.InClassExamShifts
            .Where(s => legacyShiftIds.Contains(s.Id) && !s.IsLocked && s.Status == "IN_PROGRESS")
            .ToListAsync(cancellationToken);
        foreach (var legacyShift in openLegacyShifts)
        {
            legacyShift.Status = "CLOSED";
            legacyShift.UpdatedAt = DateTime.UtcNow;
        }
        await context.SaveChangesAsync(cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var shiftStart = nowUtc.AddHours(-1);
        var shiftEnd = nowUtc.AddMonths(4);

        var shiftSeeds = new (Guid ShiftId, Guid ClassId, string ShiftName, string SecretSeed, int TotalQuestions, bool HasFollowUp, int MaxFollowUp)[]
        {
            (InClassShiftPrn231Id, ClassPrn231Id, "Kiểm tra Vấn đáp Quá trình PRN231 - Clean Architecture & .NET 8 Web API", "PRN231-TOTP-SECRET-FA26-SE1701PRN", 2, true, 2),
            (InClassShiftSwt301Id, ClassSwt301Id, "Kiểm tra Vấn đáp Quá trình SWT301 - Software Testing", "SWT301-TOTP-SECRET-FA26-SE1802SWT", 2, true, 2),
            (InClassShiftSwr302Id, ClassSwr302Id, "Kiểm tra Vấn đáp Quá trình SWR302 - Software Requirements", "SWR302-TOTP-SECRET-FA26-SE1803SWR", 2, true, 2),
            (InClassShiftSwd392Id, ClassSwd392Id, "Kiểm tra Vấn đáp Quá trình SWD392 - Software Architecture and Design", "SWD392-TOTP-SECRET-FA26-SE1804SWD", 2, true, 2)
        };

        foreach (var (shiftId, classId, shiftName, secretSeed, totalQuestions, hasFollowUp, maxFollowUp) in shiftSeeds)
        {
            var shift = await context.InClassExamShifts
                .FirstOrDefaultAsync(s => s.Id == shiftId, cancellationToken);

            if (shift == null)
            {
                if (existingClasses.Any(c => c.Id == classId && c.IsActive))
                {
                    shift = new InClassExamShift
                    {
                        Id = shiftId,
                        ClassId = classId,
                        LecturerId = LecturerUserId,
                        ShiftName = shiftName,
                        ExamDurationMinutes = 20,
                        QuestionPoolMode = "ALL_COURSE_BANK",
                        TotalQuestionsPerStudent = totalQuestions,
                        HasFollowUp = hasFollowUp,
                        MaxFollowUpQuestions = maxFollowUp,
                        IsAutoPublishGrades = true,
                        StartTime = shiftStart,
                        EndTime = shiftEnd,
                        TotpSecretKey = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(secretSeed)),
                        Status = "IN_PROGRESS",
                        IsLocked = false,
                        CreatedAt = SeedTimestampUtc,
                        UpdatedAt = nowUtc
                    };
                    context.InClassExamShifts.Add(shift);
                }
            }
            else
            {
                // Self-healing: if dev sample shift exists but was closed, finalized or expired, reset to IN_PROGRESS
                shift.Status = "IN_PROGRESS";
                shift.StartTime = shiftStart;
                shift.EndTime = shiftEnd;
                shift.IsLocked = false;
                shift.FinalizedAt = null;
                shift.UpdatedAt = nowUtc;
            }

            if (existingClasses.Any(c => c.Id == classId && c.IsActive))
            {
                var staticPwd = await context.InClassShiftPasswords
                    .FirstOrDefaultAsync(p => p.ShiftId == shiftId && p.PlainCode == "123456", cancellationToken);

                if (staticPwd == null)
                {
                    context.InClassShiftPasswords.Add(new InClassShiftPassword
                    {
                        Id = Guid.NewGuid(),
                        ShiftId = shiftId,
                        PlainCode = "123456",
                        PasswordHash = Pbkdf2PasswordHasher.HashForSeed("123456", "123456"),
                        ValidFrom = nowUtc.AddDays(-1),
                        ValidTo = nowUtc.AddYears(1),
                        CreatedAt = SeedTimestampUtc
                    });
                }
                else
                {
                    staticPwd.ValidFrom = nowUtc.AddDays(-1);
                    staticPwd.ValidTo = nowUtc.AddYears(1);
                }
            }
        }

        var seededShiftIds = shiftSeeds.Select(item => item.ShiftId).ToArray();
        var shiftsMissingDuration = await context.InClassExamShifts
            .Where(shift => seededShiftIds.Contains(shift.Id) && shift.ExamDurationMinutes == null)
            .ToListAsync(cancellationToken);
        foreach (var shift in shiftsMissingDuration)
        {
            shift.ExamDurationMinutes = 20;
            shift.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    internal static async Task EnsurePrnAndEntClosAsync(OralExamDbContext context, CancellationToken cancellationToken)
    {
        var existingClos = await context.CourseClos.ToListAsync(cancellationToken);

        var targetClos = new List<CourseClo>
        {
            // PRN231 (4 CLOs)
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000061"), CourseId = CoursePrn231Id, CloCode = "CLO1", Description = "Hiểu và áp dụng kiến trúc ASP.NET Core Web API, Middleware, Dependency Injection", BloomLevel = BloomLevel.Understand, OrderIndex = 1, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000062"), CourseId = CoursePrn231Id, CloCode = "CLO2", Description = "Thiết kế và triển khai RESTful API, DTOs, Validation và xử lý lỗi chuẩn RFC 7807", BloomLevel = BloomLevel.Apply, OrderIndex = 2, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000063"), CourseId = CoursePrn231Id, CloCode = "CLO3", Description = "Tích hợp Entity Framework Core, Migration, tối ưu hóa truy vấn và giao dịch cơ sở dữ liệu", BloomLevel = BloomLevel.Analyze, OrderIndex = 3, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000064"), CourseId = CoursePrn231Id, CloCode = "CLO4", Description = "Bảo mật hệ thống bằng JWT, OAuth 2.0, phân quyền RBAC và kiểm thử tự động", BloomLevel = BloomLevel.Evaluate, OrderIndex = 4, IsActive = true, CreatedAt = SeedTimestampUtc },

            // ENT001 (4 CLOs)
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000001e61"), CourseId = CourseEnt001Id, CloCode = "CLO1", Description = "Answer foundational personal questions about daily routines, family, food, and campus life using complete sentences", BloomLevel = BloomLevel.Understand, OrderIndex = 1, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000001e62"), CourseId = CourseEnt001Id, CloCode = "CLO2", Description = "Describe people, household activities, and visual scenes clearly using present tenses", BloomLevel = BloomLevel.Apply, OrderIndex = 2, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000001e63"), CourseId = CourseEnt001Id, CloCode = "CLO3", Description = "Narrate weekend experiences and discuss healthy study habits with coherent reasons", BloomLevel = BloomLevel.Analyze, OrderIndex = 3, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000001e64"), CourseId = CourseEnt001Id, CloCode = "CLO4", Description = "Compare living arrangements and articulate personal English learning goals", BloomLevel = BloomLevel.Evaluate, OrderIndex = 4, IsActive = true, CreatedAt = SeedTimestampUtc },

            // ENT104 (4 CLOs)
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000002e61"), CourseId = CourseEnt104Id, CloCode = "CLO1", Description = "Engage in introductory conversations on entertainment, fitness, and daily life with natural fluency", BloomLevel = BloomLevel.Understand, OrderIndex = 1, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000002e62"), CourseId = CourseEnt104Id, CloCode = "CLO2", Description = "Describe workplace scenes, digital tool usage, and volunteer experiences in structured English", BloomLevel = BloomLevel.Apply, OrderIndex = 2, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000002e63"), CourseId = CourseEnt104Id, CloCode = "CLO3", Description = "Analyze teamwork dynamics, presentation skills, and in-person versus remote meetings", BloomLevel = BloomLevel.Analyze, OrderIndex = 3, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000002e64"), CourseId = CourseEnt104Id, CloCode = "CLO4", Description = "Evaluate consumer habits and career-readiness competencies for university students", BloomLevel = BloomLevel.Evaluate, OrderIndex = 4, IsActive = true, CreatedAt = SeedTimestampUtc },

            // ENT203 (4 CLOs)
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000003e61"), CourseId = CourseEnt203Id, CloCode = "CLO1", Description = "Extract and communicate key travel itinerary details accurately from visual flyers and posters", BloomLevel = BloomLevel.Understand, OrderIndex = 1, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000003e62"), CourseId = CourseEnt203Id, CloCode = "CLO2", Description = "Perform polite travel role-plays and information transfers in hospitality scenarios", BloomLevel = BloomLevel.Apply, OrderIndex = 2, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000003e63"), CourseId = CourseEnt203Id, CloCode = "CLO3", Description = "Compare travel modes, coastal versus cultural itineraries, and heritage preservation practices", BloomLevel = BloomLevel.Analyze, OrderIndex = 3, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000003e64"), CourseId = CourseEnt203Id, CloCode = "CLO4", Description = "Discuss cross-cultural greetings and strategies to avoid intercultural misunderstandings", BloomLevel = BloomLevel.Evaluate, OrderIndex = 4, IsActive = true, CreatedAt = SeedTimestampUtc },

            // ENT303 (4 CLOs)
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000004e61"), CourseId = CourseEnt303Id, CloCode = "CLO1", Description = "Use polite expressions and describe everyday social, reading, gift-giving, and eco-friendly customs", BloomLevel = BloomLevel.Understand, OrderIndex = 1, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000004e62"), CourseId = CourseEnt303Id, CloCode = "CLO2", Description = "Role-play cross-cultural etiquette, workplace internship advice, and community volunteering plans", BloomLevel = BloomLevel.Apply, OrderIndex = 2, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000004e63"), CourseId = CourseEnt303Id, CloCode = "CLO3", Description = "Narrate conflict resolution stories and analyze digital netiquette challenges", BloomLevel = BloomLevel.Analyze, OrderIndex = 3, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000004e64"), CourseId = CourseEnt303Id, CloCode = "CLO4", Description = "Evaluate cultural preservation and generational communication differences in modern society", BloomLevel = BloomLevel.Evaluate, OrderIndex = 4, IsActive = true, CreatedAt = SeedTimestampUtc },

            // ENT403 (4 CLOs)
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000005e61"), CourseId = CourseEnt403Id, CloCode = "CLO1", Description = "Describe personal traits, moods, social media habits, and learning preferences fluently", BloomLevel = BloomLevel.Understand, OrderIndex = 1, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000005e62"), CourseId = CourseEnt403Id, CloCode = "CLO2", Description = "Deliver structured monologues on stress coping, positive habits, and stepping out of comfort zones", BloomLevel = BloomLevel.Apply, OrderIndex = 2, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000005e63"), CourseId = CourseEnt403Id, CloCode = "CLO3", Description = "Analyze personality diversity in leadership, unseen traits, and social media effects on mindset", BloomLevel = BloomLevel.Analyze, OrderIndex = 3, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000005e64"), CourseId = CourseEnt403Id, CloCode = "CLO4", Description = "Evaluate perfectionism, burnout prevention, and growth-mindset strategies", BloomLevel = BloomLevel.Evaluate, OrderIndex = 4, IsActive = true, CreatedAt = SeedTimestampUtc },

            // ENT503 (4 CLOs)
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000006e61"), CourseId = CourseEnt503Id, CloCode = "CLO1", Description = "Articulate academic goals, major choices, time management habits, and decision-making support", BloomLevel = BloomLevel.Understand, OrderIndex = 1, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000006e62"), CourseId = CourseEnt503Id, CloCode = "CLO2", Description = "Present coherent monologues on difficult choices, learning from setbacks, and 5-year career plans", BloomLevel = BloomLevel.Apply, OrderIndex = 2, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000006e63"), CourseId = CourseEnt503Id, CloCode = "CLO3", Description = "Analyze SMART goal frameworks, family influence on career paths, and team achievements", BloomLevel = BloomLevel.Analyze, OrderIndex = 3, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000006e64"), CourseId = CourseEnt503Id, CloCode = "CLO4", Description = "Critically evaluate AI impact on future careers and multidimensional definitions of personal success", BloomLevel = BloomLevel.Evaluate, OrderIndex = 4, IsActive = true, CreatedAt = SeedTimestampUtc }
        };

        foreach (var clo in targetClos)
        {
            var existing = existingClos.FirstOrDefault(c =>
                c.Id == clo.Id || (c.CourseId == clo.CourseId && c.CloCode == clo.CloCode));
            if (existing == null)
            {
                context.CourseClos.Add(clo);
                existingClos.Add(clo);
            }
            else
            {
                existing.IsActive = true;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
