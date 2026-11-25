using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.AutoAllocateSessionStudents;
using OralExamination.Application.Features.OfficialExams.Commands.BatchCreateOfficialExamSessions;
using OralExamination.Application.Features.OfficialExams.Commands.BatchOpenExamShifts;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamEnrollmentEstimate;
using OralExamination.Application.Features.OfficialExams.Queries.GetSemesterEligibleExamCourses;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class OfficialExamFourStepSchedulingTests
{
    private sealed class SeededOfficialExamRandomGenerator(int seed) : IOfficialExamRandomGenerator
    {
        private readonly Random _random = new(seed);
        public int NextInt32(int toExclusive) => _random.Next(toExclusive);
    }

    private static OralExamDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private static User CreateUser(string role, string codePrefix, int index, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Email = $"{codePrefix.ToLowerInvariant()}{index:D4}-{Guid.NewGuid():N}@fpt.edu.vn",
        FullName = $"{codePrefix} {index:D4}",
        StudentCode = role == UserRole.Student ? $"SE{index:D6}" : null,
        Username = $"{codePrefix.ToLowerInvariant()}{index:D4}",
        Role = role,
        IsActive = isActive
    };

    private static List<User> SeedEnrolledStudents(
        OralExamDbContext context,
        IReadOnlyList<Class> activeClasses,
        int studentCount)
    {
        var students = new List<User>(studentCount);
        for (var i = 1; i <= studentCount; i++)
        {
            var student = CreateUser(UserRole.Student, "SV", i);
            students.Add(student);
            var targetClass = activeClasses[(i - 1) % activeClasses.Count];
            context.ClassEnrollments.Add(new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = targetClass.Id,
                StudentId = student.Id,
                Status = "enrolled",
                JoinMethod = "assigned"
            });
        }

        context.Users.AddRange(students);
        return students;
    }

    private static void SeedSessionCandidates(
        OralExamDbContext context,
        Guid sessionId,
        IEnumerable<User> students,
        Guid importedBy)
    {
        foreach (var student in students)
        {
            context.OfficialExamCandidates.Add(new OfficialExamCandidate
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                StudentId = student.Id,
                ImportedBy = importedBy,
                ImportedAt = DateTime.UtcNow
            });
        }
    }

    [Fact(DisplayName = "MF04-STEP1: Lấy môn mở theo Học kỳ tính đúng DISTINCT sinh viên enrolled hợp lệ, ceil(S/24) phòng và loại bỏ offering tắt / SV dropped")]
    public async Task GetSemesterEligibleCourses_ComputesDistinctEnrolledStudents_AndRecommendsCeil24Rooms()
    {
        await using var context = CreateContext();
        var officer = CreateUser(UserRole.ExaminationOfficer, "EO", 1);
        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };
        var otherSemester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "SP26",
            Name = "Spring 2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30),
            IsActive = true
        };

        var dbiCourse = new Course
        {
            Id = Guid.NewGuid(),
            Code = "DBI202",
            Name = "Introduction to Databases",
            SemesterId = otherSemester.Id,
            HasFollowUp = true,
            MaxFollowUpQuestions = 3,
            IsActive = true
        };
        var csdCourse = new Course
        {
            Id = Guid.NewGuid(),
            Code = "CSD201",
            Name = "Data Structures and Algorithms",
            SemesterId = semester.Id,
            HasFollowUp = false,
            MaxFollowUpQuestions = 2,
            IsActive = true
        };
        var disabledOfferingCourse = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            SemesterId = semester.Id,
            IsActive = true
        };

        var dbiOffering = new CourseOffering
        {
            Id = Guid.NewGuid(),
            CourseId = dbiCourse.Id,
            SemesterId = semester.Id,
            IsEnabled = true
        };
        var disabledOffering = new CourseOffering
        {
            Id = Guid.NewGuid(),
            CourseId = disabledOfferingCourse.Id,
            SemesterId = semester.Id,
            IsEnabled = false
        };

        var classes = Enumerable.Range(1, 4).Select(idx => new Class
        {
            Id = Guid.NewGuid(),
            Code = $"SE180{idx}-DBI",
            Name = $"Lớp DBI {idx}",
            JoinCode = $"DBI2020{idx}",
            CourseId = dbiCourse.Id,
            CourseOfferingId = dbiOffering.Id,
            SemesterId = semester.Id,
            IsActive = true
        }).ToList();

        var inactiveClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1899-DBI-OFF",
            Name = "Lớp ngừng hoạt động",
            JoinCode = "DBIOFF99",
            CourseId = dbiCourse.Id,
            CourseOfferingId = dbiOffering.Id,
            SemesterId = semester.Id,
            IsActive = false
        };

        context.Users.Add(officer);
        context.Semesters.AddRange(semester, otherSemester);
        context.Courses.AddRange(dbiCourse, csdCourse, disabledOfferingCourse);
        context.CourseOfferings.AddRange(dbiOffering, disabledOffering);
        context.Classes.AddRange(classes);
        context.Classes.Add(inactiveClass);

        var students = SeedEnrolledStudents(context, classes, 122);

        // 1 sinh viên có mặt ở 2 lớp cùng môn cùng kỳ -> DISTINCT vẫn phải giữ đúng 122
        context.ClassEnrollments.Add(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classes[1].Id,
            StudentId = students[0].Id,
            Status = "enrolled",
            JoinMethod = "assigned"
        });

        // Sinh viên dropped, sinh viên bị khóa và sinh viên ở lớp inactive không được tính
        var droppedStudent = CreateUser(UserRole.Student, "DROP", 500);
        var inactiveStudent = CreateUser(UserRole.Student, "LOCK", 501, isActive: false);
        var inactiveClassStudent = CreateUser(UserRole.Student, "INACTCLS", 502);
        context.Users.AddRange(droppedStudent, inactiveStudent, inactiveClassStudent);
        context.ClassEnrollments.AddRange(
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = classes[0].Id, StudentId = droppedStudent.Id, Status = "dropped", JoinMethod = "assigned" },
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = classes[0].Id, StudentId = inactiveStudent.Id, Status = "enrolled", JoinMethod = "assigned" },
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = inactiveClass.Id, StudentId = inactiveClassStudent.Id, Status = "enrolled", JoinMethod = "assigned" });

        var activeStructure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = dbiCourse.Id,
            CreatedBy = officer.Id,
            Name = "Cấu trúc DBI202 FA26",
            IsActive = true
        };
        context.ExamStructures.Add(activeStructure);
        var preparedSession = new OfficialExamSession { CourseId = dbiCourse.Id, SemesterId = semester.Id, Title = "DBI Final",
            ExamStructureId = activeStructure.Id, ExamDate = new DateOnly(2026, 12, 20) };
        context.OfficialExamSessions.Add(preparedSession);
        context.ExamSets.AddRange(
            new ExamSet { Id = Guid.NewGuid(), CourseId = dbiCourse.Id, StructureId = activeStructure.Id, SetCode = "SET-01", SessionId = preparedSession.Id, Status = ExamSetStatus.HandedOver, IsActive = true },
            new ExamSet { Id = Guid.NewGuid(), CourseId = dbiCourse.Id, StructureId = activeStructure.Id, SetCode = "SET-02", Status = ExamSetStatus.Official, IsActive = true });

        await context.SaveChangesAsync();

        var handler = new GetSemesterEligibleExamCoursesQueryHandler(context);
        var result = await handler.Handle(new GetSemesterEligibleExamCoursesQuery(officer.Id, semester.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2, "môn SWD392 có CourseOffering bị tắt không được liệt kê");
        result.Value.Select(c => c.CourseCode).Should().Equal("CSD201", "DBI202");

        var dbiDto = result.Value.Single(c => c.CourseId == dbiCourse.Id);
        dbiDto.ActiveClassCount.Should().Be(4);
        dbiDto.EligibleStudentCount.Should().Be(122);
        dbiDto.MaxStudentsPerRoom.Should().Be(24);
        dbiDto.RecommendedRoomCount.Should().Be(6);
        dbiDto.ActiveExamStructureId.Should().Be(activeStructure.Id);
        dbiDto.HandedOverExamSetCount.Should().Be(1);
        dbiDto.HasFollowUp.Should().BeTrue();
        dbiDto.MaxFollowUpQuestions.Should().Be(3);
        dbiDto.ExistingSessionId.Should().Be(preparedSession.Id);

        var csdDto = result.Value.Single(c => c.CourseId == csdCourse.Id);
        csdDto.EligibleStudentCount.Should().Be(0);
        csdDto.RecommendedRoomCount.Should().Be(0);

        var missingSemester = await handler.Handle(new GetSemesterEligibleExamCoursesQuery(officer.Id, Guid.NewGuid()), CancellationToken.None);
        missingSemester.IsSuccess.Should().BeFalse();
        missingSemester.Error.Should().StartWith("NOT_FOUND:");
    }

    [Fact(DisplayName = "MF04-STEP2: Tạo hàng loạt kỳ thi tự động kế thừa Follow-up & cấu trúc đề từ Trưởng BM, tách biệt theo SemesterId và hỗ trợ skipExisting")]
    public async Task BatchCreateSessions_InheritsAcademicFollowUp_IsolatesBySemester_AndEnforcesConflictOrSkip()
    {
        await using var context = CreateContext();
        var officer = CreateUser(UserRole.ExaminationOfficer, "EO", 1);
        var semFa26 = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) };
        var semSp27 = new Semester { Id = Guid.NewGuid(), Code = "SP27", Name = "Spring 2027", StartDate = new DateOnly(2027, 1, 1), EndDate = new DateOnly(2027, 4, 30) };

        var course1 = new Course
        {
            Id = Guid.NewGuid(),
            Code = "DBI202",
            Name = "Databases",
            SemesterId = semFa26.Id,
            HasFollowUp = true,
            MaxFollowUpQuestions = 4,
            IsActive = true
        };
        var course2 = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "ASP.NET Core",
            SemesterId = semFa26.Id,
            HasFollowUp = false,
            MaxFollowUpQuestions = 0, // Ngoài khoảng 1..5 -> phải tự động clamp về 1 để thỏa ck_official_sessions_max_follow_up
            IsActive = true
        };
        var sp27Offering = new CourseOffering
        {
            Id = Guid.NewGuid(),
            CourseId = course1.Id,
            SemesterId = semSp27.Id,
            IsEnabled = true
        };
        var structure1 = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course1.Id,
            CreatedBy = officer.Id,
            Name = "DBI Structure",
            IsActive = true
        };

        context.Users.Add(officer);
        context.Semesters.AddRange(semFa26, semSp27);
        context.Courses.AddRange(course1, course2);
        context.CourseOfferings.Add(sp27Offering);
        context.ExamStructures.Add(structure1);
        await context.SaveChangesAsync();

        var handler = new BatchCreateOfficialExamSessionsCommandHandler(context);
        var createResult = await handler.Handle(new BatchCreateOfficialExamSessionsCommand(
            officer.Id,
            semFa26.Id,
            new DateOnly(2026, 12, 15),
            ExamInputMode.VoiceOnly,
            90,
            SkipExisting: false,
            new[]
            {
                new BatchCreateOfficialExamSessionCourseItemDto { CourseId = course1.Id, Title = "Kỳ thi Cuối kỳ FA26 - DBI202" }
            }), CancellationToken.None);

        createResult.IsSuccess.Should().BeTrue();
        createResult.Value.CreatedCount.Should().Be(1);
        var createdDbi = createResult.Value.Sessions.Single();
        createdDbi.SemesterId.Should().Be(semFa26.Id);
        createdDbi.ExamStructureId.Should().Be(structure1.Id);
        createdDbi.HasFollowUp.Should().BeTrue("kế thừa 100% từ cấu hình môn của Trưởng Bộ Môn");
        createdDbi.MaxFollowUpQuestions.Should().Be(4);
        createdDbi.ExamInputMode.Should().Be(ExamInputMode.VoiceOnly);
        createdDbi.TranscriptBufferSeconds.Should().Be(90);

        // Gọi trùng khi SkipExisting = false -> 409 Conflict, không tạo một phần
        var conflictResult = await handler.Handle(new BatchCreateOfficialExamSessionsCommand(
            officer.Id,
            semFa26.Id,
            new DateOnly(2026, 12, 15),
            ExamInputMode.VoiceWithTranscriptEdit,
            60,
            SkipExisting: false,
            new[]
            {
                new BatchCreateOfficialExamSessionCourseItemDto { CourseId = course1.Id },
                new BatchCreateOfficialExamSessionCourseItemDto { CourseId = course2.Id }
            }), CancellationToken.None);

        conflictResult.IsSuccess.Should().BeFalse();
        conflictResult.Error.Should().StartWith("CONFLICT:");
        (await context.OfficialExamSessions.CountAsync()).Should().Be(1);

        // Gọi lại với SkipExisting = true -> bỏ qua DBI202, tạo mới PRN231 và clamp MaxFollowUpQuestions = 1
        var skipResult = await handler.Handle(new BatchCreateOfficialExamSessionsCommand(
            officer.Id,
            semFa26.Id,
            new DateOnly(2026, 12, 16),
            ExamInputMode.VoiceWithTranscriptEdit,
            60,
            SkipExisting: true,
            new[]
            {
                new BatchCreateOfficialExamSessionCourseItemDto { CourseId = course1.Id },
                new BatchCreateOfficialExamSessionCourseItemDto { CourseId = course2.Id }
            }), CancellationToken.None);

        skipResult.IsSuccess.Should().BeTrue();
        skipResult.Value.CreatedCount.Should().Be(1);
        skipResult.Value.Sessions.Single().CourseId.Should().Be(course2.Id);
        skipResult.Value.Sessions.Single().MaxFollowUpQuestions.Should().Be(1);

        // Cùng môn DBI202 nhưng ở kỳ SP27 mở mới -> thành công nhờ cột 3NF SemesterId
        var sp27Result = await handler.Handle(new BatchCreateOfficialExamSessionsCommand(
            officer.Id,
            semSp27.Id,
            new DateOnly(2027, 4, 20),
            ExamInputMode.VoiceWithTranscriptEdit,
            60,
            SkipExisting: false,
            new[]
            {
                new BatchCreateOfficialExamSessionCourseItemDto { CourseId = course1.Id }
            }), CancellationToken.None);

        sp27Result.IsSuccess.Should().BeTrue();
        sp27Result.Value.CreatedCount.Should().Be(1);
        sp27Result.Value.Sessions.Single().SemesterId.Should().Be(semSp27.Id);
    }

    [Fact(DisplayName = "MF04-STEP3: Tạo Ca K Phòng chặn maxSeats > 24, trùng phòng/cán bộ giao thoa giờ, sai vai trò và cho phép ca nối tiếp liền kề")]
    public async Task BatchOpenShifts_ValidatesCapacity24_StaffRoles_AndTimeOverlapConflicts()
    {
        await using var context = CreateContext();
        var officer = CreateUser(UserRole.ExaminationOfficer, "EO", 1);
        var proctor1 = CreateUser(UserRole.Proctor, "PR", 1);
        var proctor2 = CreateUser(UserRole.Proctor, "PR", 2);
        var examiner1 = CreateUser(UserRole.Lecturer, "LEC", 1);
        var examiner2 = CreateUser(UserRole.Lecturer, "LEC", 2);
        var student = CreateUser(UserRole.Student, "SV", 1);

        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) };
        var course = new Course { Id = Guid.NewGuid(), Code = "DBI202", Name = "Databases", SemesterId = semester.Id, IsActive = true };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SemesterId = semester.Id,
            Title = "Kỳ thi DBI202",
            ExamDate = new DateOnly(2026, 12, 15),
            Status = "scheduled"
        };

        context.Users.AddRange(officer, proctor1, proctor2, examiner1, examiner2, student);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.OfficialExamSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new BatchOpenExamShiftsCommandHandler(context);
        var shift1Start = new DateTime(2026, 12, 15, 0, 30, 0, DateTimeKind.Utc);
        var shift1End = new DateTime(2026, 12, 15, 2, 30, 0, DateTimeKind.Utc);

        // 1. Vượt trần 24 ghế/phòng chuẩn -> 422
        var overCap = await handler.Handle(new BatchOpenExamShiftsCommand(
            officer.Id, session.Id, "Ca 1", shift1Start, shift1End,
            new[] { new BatchOpenExamShiftRoomDto { RoomLab = "LAB-301", MaxSeats = 25 } }), CancellationToken.None);
        overCap.IsSuccess.Should().BeFalse();
        overCap.Error.Should().StartWith("UNPROCESSABLE:");

        // 2. Trùng phòng case-insensitive trong cùng mảng rooms -> 409
        var dupRoomInRequest = await handler.Handle(new BatchOpenExamShiftsCommand(
            officer.Id, session.Id, "Ca 1", shift1Start, shift1End,
            new[]
            {
                new BatchOpenExamShiftRoomDto { RoomLab = "LAB-301", MaxSeats = 24 },
                new BatchOpenExamShiftRoomDto { RoomLab = "lab-301", MaxSeats = 24 }
            }), CancellationToken.None);
        dupRoomInRequest.IsSuccess.Should().BeFalse();
        dupRoomInRequest.Error.Should().StartWith("CONFLICT:");

        // 3. Truyền sai role cho proctorUserId (truyền lecturer thay vì proctor) -> 422
        var wrongProctorRole = await handler.Handle(new BatchOpenExamShiftsCommand(
            officer.Id, session.Id, "Ca 1", shift1Start, shift1End,
            new[] { new BatchOpenExamShiftRoomDto { RoomLab = "LAB-301", ProctorUserId = examiner1.Id } }), CancellationToken.None);
        wrongProctorRole.IsSuccess.Should().BeFalse();
        wrongProctorRole.Error.Should().StartWith("UNPROCESSABLE:");

        // 4. Truyền sai role cho examinerUserId (truyền proctor thay vì lecturer) -> 422
        var wrongExaminerRole = await handler.Handle(new BatchOpenExamShiftsCommand(
            officer.Id, session.Id, "Ca 1", shift1Start, shift1End,
            new[] { new BatchOpenExamShiftRoomDto { RoomLab = "LAB-301", ExaminerUserId = proctor1.Id } }), CancellationToken.None);
        wrongExaminerRole.IsSuccess.Should().BeFalse();
        wrongExaminerRole.Error.Should().StartWith("UNPROCESSABLE:");

        // 5. Hợp lệ: Mở nguyên tử 3 phòng cho Ca 1
        var createdShift1 = await handler.Handle(new BatchOpenExamShiftsCommand(
            officer.Id, session.Id, "Ca 1 - Sáng", shift1Start, shift1End,
            new[]
            {
                new BatchOpenExamShiftRoomDto { RoomLab = "LAB-301", MaxSeats = 24, ProctorUserId = proctor1.Id, ExaminerUserId = examiner1.Id },
                new BatchOpenExamShiftRoomDto { RoomLab = "LAB-302", MaxSeats = 24, ProctorUserId = proctor2.Id, ExaminerUserId = examiner2.Id },
                new BatchOpenExamShiftRoomDto { RoomLab = "LAB-303", MaxSeats = 24 }
            }), CancellationToken.None);

        createdShift1.IsSuccess.Should().BeTrue();
        createdShift1.Value.CreatedRoomCount.Should().Be(3);
        createdShift1.Value.TotalCapacityCreated.Should().Be(72);
        createdShift1.Value.Shifts.Select(s => s.ShiftName)
            .Should().Equal("Ca 1 - Sáng - LAB-301", "Ca 1 - Sáng - LAB-302", "Ca 1 - Sáng - LAB-303");

        // 6. Giao thoa khung giờ (01:30 - 03:30) trùng phòng LAB-301 -> 409 Conflict & không lưu phòng nào
        var overlapRoom = await handler.Handle(new BatchOpenExamShiftsCommand(
            officer.Id, session.Id, "Ca Giao Thoa",
            shift1Start.AddHours(1), shift1End.AddHours(1),
            new[]
            {
                new BatchOpenExamShiftRoomDto { RoomLab = "LAB-304", MaxSeats = 24 },
                new BatchOpenExamShiftRoomDto { RoomLab = "LAB-301", MaxSeats = 24 }
            }), CancellationToken.None);
        overlapRoom.IsSuccess.Should().BeFalse();
        overlapRoom.Error.Should().StartWith("CONFLICT:");
        (await context.RealExamSessionShifts.CountAsync()).Should().Be(3);

        // 7. Giao thoa khung giờ trùng Giám thị proctor1 ở phòng khác -> 409 Conflict
        var overlapProctor = await handler.Handle(new BatchOpenExamShiftsCommand(
            officer.Id, session.Id, "Ca Giao Thoa Giám Thị",
            shift1Start.AddMinutes(30), shift1End.AddMinutes(30),
            new[] { new BatchOpenExamShiftRoomDto { RoomLab = "LAB-401", MaxSeats = 24, ProctorUserId = proctor1.Id } }), CancellationToken.None);
        overlapProctor.IsSuccess.Should().BeFalse();
        overlapProctor.Error.Should().StartWith("CONFLICT:");

        // 8. Ca nối tiếp liền kề (02:30 - 04:30, StartTime == Shift1.EndTime) dùng lại LAB-301 & proctor1 -> Thành công
        var contiguousShift = await handler.Handle(new BatchOpenExamShiftsCommand(
            officer.Id, session.Id, "Ca 2 - Nối tiếp",
            shift1End, shift1End.AddHours(2),
            new[] { new BatchOpenExamShiftRoomDto { RoomLab = "LAB-301", MaxSeats = 24, ProctorUserId = proctor1.Id, ExaminerUserId = examiner1.Id } }), CancellationToken.None);
        contiguousShift.IsSuccess.Should().BeTrue();
        (await context.RealExamSessionShifts.CountAsync()).Should().Be(4);
    }

    [Fact(DisplayName = "MF04-STEP4A: 122 SV chia vào Ca 1 (3 phòng) + Ca 2 (3 phòng) -> Ca 1 [24,24,24], Ca 2 cân bằng [17,17,16], sau đó thêm 5 SV mới tự động Water-Filling [19,18,18]")]
    public async Task AutoAllocate_122Students_AcrossTwoShifts_BalancesFinalShift_17_17_16_AndSupportsIncrementalWaterFilling()
    {
        await using var context = CreateContext();
        var officer = CreateUser(UserRole.ExaminationOfficer, "EO", 1);
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) };
        var course = new Course { Id = Guid.NewGuid(), Code = "DBI202", Name = "Databases", SemesterId = semester.Id, IsActive = true };
        var cls = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1801",
            Name = "SE1801",
            JoinCode = "SE1801DBI",
            CourseId = course.Id,
            SemesterId = semester.Id,
            IsActive = true
        };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SemesterId = semester.Id,
            Title = "Kỳ thi FA26 - DBI202",
            ExamDate = new DateOnly(2026, 12, 15),
            Status = "scheduled"
        };

        context.Users.Add(officer);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.Classes.Add(cls);
        context.OfficialExamSessions.Add(session);

        var initialStudents = SeedEnrolledStudents(context, new[] { cls }, 122);
        SeedSessionCandidates(context, session.Id, initialStudents, officer.Id);
        await context.SaveChangesAsync();

        var shiftBatchHandler = new BatchOpenExamShiftsCommandHandler(context);
        var estimateHandler = new GetOfficialExamEnrollmentEstimateQueryHandler(context);

        var initialEstimate = await estimateHandler.Handle(new GetOfficialExamEnrollmentEstimateQuery(officer.Id, session.Id), CancellationToken.None);
        initialEstimate.IsSuccess.Should().BeTrue();
        initialEstimate.Value.RecommendedTotalRooms.Should().Be(6);
        initialEstimate.Value.RecommendedAdditionalRooms.Should().Be(6, "khi chưa mở phòng nào cần mở thêm 6 phòng cho 122 SV");

        var s1Start = new DateTime(2026, 12, 15, 0, 30, 0, DateTimeKind.Utc);
        var s1End = new DateTime(2026, 12, 15, 2, 30, 0, DateTimeKind.Utc);
        var s2Start = new DateTime(2026, 12, 15, 3, 0, 0, DateTimeKind.Utc);
        var s2End = new DateTime(2026, 12, 15, 5, 0, 0, DateTimeKind.Utc);

        await shiftBatchHandler.Handle(new BatchOpenExamShiftsCommand(
            officer.Id, session.Id, "Ca 1", s1Start, s1End,
            new[]
            {
                new BatchOpenExamShiftRoomDto { RoomLab = "LAB-301", MaxSeats = 24 },
                new BatchOpenExamShiftRoomDto { RoomLab = "LAB-302", MaxSeats = 24 },
                new BatchOpenExamShiftRoomDto { RoomLab = "LAB-303", MaxSeats = 24 }
            }), CancellationToken.None);

        var midEstimate = await estimateHandler.Handle(new GetOfficialExamEnrollmentEstimateQuery(officer.Id, session.Id), CancellationToken.None);
        midEstimate.IsSuccess.Should().BeTrue();
        midEstimate.Value.RecommendedAdditionalRooms.Should().Be(3, "đã mở 72 ghế cho 122 SV, còn thiếu 50 ghế nên đề xuất mở thêm ceil(50/24)=3 phòng");

        await shiftBatchHandler.Handle(new BatchOpenExamShiftsCommand(
            officer.Id, session.Id, "Ca 2", s2Start, s2End,
            new[]
            {
                new BatchOpenExamShiftRoomDto { RoomLab = "LAB-301", MaxSeats = 24 },
                new BatchOpenExamShiftRoomDto { RoomLab = "LAB-302", MaxSeats = 24 },
                new BatchOpenExamShiftRoomDto { RoomLab = "LAB-303", MaxSeats = 24 }
            }), CancellationToken.None);

        var beforeEstimate = await estimateHandler.Handle(new GetOfficialExamEnrollmentEstimateQuery(officer.Id, session.Id), CancellationToken.None);
        beforeEstimate.IsSuccess.Should().BeTrue();
        beforeEstimate.Value.EligibleStudentCount.Should().Be(122);
        beforeEstimate.Value.UnallocatedStudentCount.Should().Be(122);
        beforeEstimate.Value.RecommendedTotalRooms.Should().Be(6);
        beforeEstimate.Value.RecommendedAdditionalRooms.Should().Be(0, "đã có 144 ghế trống đủ cho 122 SV nên không cần mở thêm phòng");
        beforeEstimate.Value.CurrentShiftRoomCount.Should().Be(6);
        beforeEstimate.Value.CurrentTotalCapacity.Should().Be(144);
        beforeEstimate.Value.CurrentAvailableSeats.Should().Be(144);

        // Kiểm tra DryRun = true trước: trả đúng cấu trúc chia [24,24,24] & [17,17,16] nhưng không ghi DB
        var allocHandler = new AutoAllocateSessionStudentsCommandHandler(
            context, new SeededOfficialExamRandomGenerator(20261116));

        var dryRunResult = await allocHandler.Handle(
            new AutoAllocateSessionStudentsCommand(officer.Id, session.Id, DryRun: true),
            CancellationToken.None);

        dryRunResult.IsSuccess.Should().BeTrue();
        dryRunResult.Value.DryRun.Should().BeTrue();
        dryRunResult.Value.ShiftGroups[0].Rooms.Select(r => r.AssignedNow).Should().Equal(24, 24, 24);
        dryRunResult.Value.ShiftGroups[1].Rooms.Select(r => r.AssignedNow).Should().Equal(17, 17, 16);
        (await context.StudentExamTickets.CountAsync()).Should().Be(0, "DryRun = true tuyệt đối không ghi vé xuống DB");

        // Chạy phân bổ thật (DryRun = false)
        var realAlloc = await allocHandler.Handle(
            new AutoAllocateSessionStudentsCommand(officer.Id, session.Id, DryRun: false),
            CancellationToken.None);

        realAlloc.IsSuccess.Should().BeTrue();
        realAlloc.Value.TotalEligibleStudents.Should().Be(122);
        realAlloc.Value.PreviouslyAllocatedCount.Should().Be(0);
        realAlloc.Value.NewlyAllocatedCount.Should().Be(122);
        realAlloc.Value.ShiftGroups.Should().HaveCount(2);

        var group1 = realAlloc.Value.ShiftGroups[0];
        group1.IsFinalBalancedShift.Should().BeFalse();
        group1.TotalAssignedInGroup.Should().Be(72);
        group1.Rooms.Select(r => r.TotalAfter).Should().Equal(24, 24, 24);

        var group2 = realAlloc.Value.ShiftGroups[1];
        group2.IsFinalBalancedShift.Should().BeTrue();
        group2.TotalAssignedInGroup.Should().Be(50);
        group2.Rooms.Select(r => r.TotalAfter).Should().Equal(17, 17, 16);
        (group2.Rooms.Max(r => r.TotalAfter) - group2.Rooms.Min(r => r.TotalAfter)).Should().BeLessThanOrEqualTo(1);

        var persistedTickets = await context.StudentExamTickets.AsNoTracking().ToListAsync();
        persistedTickets.Should().HaveCount(122);
        persistedTickets.Select(t => t.StudentId).Distinct().Should().HaveCount(122);
        persistedTickets.All(t => t.Status == ExamTicketStatus.Scheduled && t.ExamSetId == null).Should().BeTrue();

        // Kiểm tra xáo trộn ngẫu nhiên: thứ tự sinh viên theo vé không trùng thứ tự ID tăng dần gốc
        var orderedInitialIds = initialStudents.Select(s => s.Id).OrderBy(id => id).ToList();
        var allocatedOrderIds = persistedTickets.Select(t => t.StudentId).ToList();
        allocatedOrderIds.Should().NotEqual(orderedInitialIds);

        // Kiểm tra ghế ngồi liên tiếp 1..count và không trùng trong từng phòng
        foreach (var room in realAlloc.Value.ShiftGroups.SelectMany(g => g.Rooms))
        {
            var seats = persistedTickets
                .Where(t => t.ShiftId == room.ShiftId)
                .Select(t => t.SeatNumber)
                .OrderBy(n => n)
                .ToList();
            seats.Should().Equal(Enumerable.Range(1, room.TotalAfter));
        }

        // Bổ sung thêm 5 sinh viên mới vào danh sách dự thi (APPEND) -> phân bổ lần 2 bằng Water-Filling
        for (var extra = 1; extra <= 5; extra++)
        {
            var newStudent = CreateUser(UserRole.Student, "LATE", extra);
            context.Users.Add(newStudent);
            context.ClassEnrollments.Add(new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = cls.Id,
                StudentId = newStudent.Id,
                Status = "enrolled",
                JoinMethod = "code"
            });
            context.OfficialExamCandidates.Add(new OfficialExamCandidate
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                StudentId = newStudent.Id,
                ImportedBy = officer.Id,
                ImportedAt = DateTime.UtcNow
            });
        }
        await context.SaveChangesAsync();

        var secondAlloc = await allocHandler.Handle(
            new AutoAllocateSessionStudentsCommand(officer.Id, session.Id, DryRun: false),
            CancellationToken.None);

        secondAlloc.IsSuccess.Should().BeTrue();
        secondAlloc.Value.TotalEligibleStudents.Should().Be(127);
        secondAlloc.Value.PreviouslyAllocatedCount.Should().Be(122);
        secondAlloc.Value.NewlyAllocatedCount.Should().Be(5);

        // Phòng Ca 1 đã đầy [24,24,24] nên AssignedNow = [0,0,0]; Ca 2 đang [17,17,16] nhận thêm 5 SV thành [19,18,18]
        secondAlloc.Value.ShiftGroups[0].Rooms.Select(r => r.AssignedNow).Should().Equal(0, 0, 0);
        secondAlloc.Value.ShiftGroups[1].IsFinalBalancedShift.Should().BeTrue();
        secondAlloc.Value.ShiftGroups[1].Rooms.Select(r => r.TotalAfter).Should().Equal(19, 18, 18);

        var allTicketsAfterSecondPass = await context.StudentExamTickets.AsNoTracking().ToListAsync();
        allTicketsAfterSecondPass.Should().HaveCount(127);
        allTicketsAfterSecondPass.Select(t => t.StudentId).Distinct().Should().HaveCount(127);
        foreach (var room in secondAlloc.Value.ShiftGroups[1].Rooms)
        {
            var seats = allTicketsAfterSecondPass
                .Where(t => t.ShiftId == room.ShiftId)
                .Select(t => t.SeatNumber)
                .OrderBy(n => n)
                .ToList();
            seats.Should().Equal(Enumerable.Range(1, room.TotalAfter));
        }
    }

    [Fact(DisplayName = "MF04-STEP4B: Kiểm chứng 1 ca duy nhất (25 SV -> [13,12]), dư ca phía sau (40 SV -> Ca 1 [20,20], Ca 2 [0,0]) và cấp bộ đề HANDED_OVER không trùng")]
    public async Task AutoAllocate_HandlesSingleShift_SurplusTrailingShifts_InsufficientSeats_AndHandedOverExamSets()
    {
        await using var context = CreateContext();
        var officer = CreateUser(UserRole.ExaminationOfficer, "EO", 1);
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) };

        // Case 1: 25 SV trong 1 ca duy nhất gồm 2 phòng -> [13, 12] + gán bộ đề HANDED_OVER không trùng
        var courseSingle = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", SemesterId = semester.Id, IsActive = true };
        var classSingle = new Class { Id = Guid.NewGuid(), Code = "SE1810", Name = "SE1810", JoinCode = "SE1810PRN", CourseId = courseSingle.Id, SemesterId = semester.Id, IsActive = true };
        var structSingle = new ExamStructure { Id = Guid.NewGuid(), CourseId = courseSingle.Id, CreatedBy = officer.Id, Name = "Struct", IsActive = true };
        var sessionSingle = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = courseSingle.Id, SemesterId = semester.Id, Title = "PRN231", ExamDate = new DateOnly(2026, 12, 20), Status = "scheduled" };

        context.Users.Add(officer);
        context.Semesters.Add(semester);
        context.Courses.Add(courseSingle);
        context.Classes.Add(classSingle);
        context.ExamStructures.Add(structSingle);
        context.OfficialExamSessions.Add(sessionSingle);
        var singleStudents = SeedEnrolledStudents(context, new[] { classSingle }, 25);
        SeedSessionCandidates(context, sessionSingle.Id, singleStudents, officer.Id);
        await context.SaveChangesAsync();

        var shiftHandler = new BatchOpenExamShiftsCommandHandler(context);
        var t0 = new DateTime(2026, 12, 20, 1, 0, 0, DateTimeKind.Utc);
        await shiftHandler.Handle(new BatchOpenExamShiftsCommand(
            officer.Id, sessionSingle.Id, "Ca Duy Nhất", t0, t0.AddHours(2),
            new[]
            {
                new BatchOpenExamShiftRoomDto { RoomLab = "LAB-101", MaxSeats = 24 },
                new BatchOpenExamShiftRoomDto { RoomLab = "LAB-102", MaxSeats = 24 }
            }), CancellationToken.None);

        // Chưa đủ bộ đề HANDED_OVER (mới có 10 bộ < 13 SV của phòng lớn nhất) -> chặn 422
        for (var idx = 1; idx <= 10; idx++)
        {
            context.ExamSets.Add(new ExamSet
            {
                Id = Guid.NewGuid(),
                CourseId = courseSingle.Id,
                StructureId = structSingle.Id,
                SetCode = $"PRN-SET-{idx:D2}",
                SessionId = sessionSingle.Id,
                Status = ExamSetStatus.HandedOver,
                IsActive = true
            });
        }
        await context.SaveChangesAsync();

        var allocHandler = new AutoAllocateSessionStudentsCommandHandler(context, new SeededOfficialExamRandomGenerator(99));
        var missingSetsResult = await allocHandler.Handle(
            new AutoAllocateSessionStudentsCommand(officer.Id, sessionSingle.Id, AssignHandedOverExamSets: true),
            CancellationToken.None);
        missingSetsResult.IsSuccess.Should().BeFalse();
        missingSetsResult.Error.Should().StartWith("UNPROCESSABLE:");

        // Bổ sung thêm 3 bộ đề HANDED_OVER (tổng 13 bộ >= max room 13) -> Thành công [13, 12], không trùng đề trong cùng phòng
        for (var idx = 11; idx <= 13; idx++)
        {
            context.ExamSets.Add(new ExamSet
            {
                Id = Guid.NewGuid(),
                CourseId = courseSingle.Id,
                StructureId = structSingle.Id,
                SetCode = $"PRN-SET-{idx:D2}",
                SessionId = sessionSingle.Id,
                Status = ExamSetStatus.HandedOver,
                IsActive = true
            });
        }
        await context.SaveChangesAsync();

        var singleShiftAlloc = await allocHandler.Handle(
            new AutoAllocateSessionStudentsCommand(officer.Id, sessionSingle.Id, AssignHandedOverExamSets: true),
            CancellationToken.None);
        singleShiftAlloc.IsSuccess.Should().BeTrue();
        singleShiftAlloc.Value.ShiftGroups.Should().ContainSingle();
        singleShiftAlloc.Value.ShiftGroups[0].IsFinalBalancedShift.Should().BeTrue();
        singleShiftAlloc.Value.ShiftGroups[0].Rooms.Select(r => r.TotalAfter).Should().Equal(13, 12);

        foreach (var room in singleShiftAlloc.Value.ShiftGroups[0].Rooms)
        {
            var assignedSetsInRoom = await context.StudentExamTickets
                .Where(t => t.ShiftId == room.ShiftId)
                .Select(t => t.ExamSetId)
                .ToListAsync();
            assignedSetsInRoom.Should().NotContainNulls();
            assignedSetsInRoom.Distinct().Should().HaveCount(room.TotalAfter, "không được trùng bộ đề trong cùng một phòng thi");
        }

        // Gọi lại khi toàn bộ 25 SV đã có vé -> 422 (0 SV chưa xếp)
        var zeroUnallocated = await allocHandler.Handle(
            new AutoAllocateSessionStudentsCommand(officer.Id, sessionSingle.Id),
            CancellationToken.None);
        zeroUnallocated.IsSuccess.Should().BeFalse();
        zeroUnallocated.Error.Should().StartWith("UNPROCESSABLE:");

        // Case 2: 40 SV mở Ca 1 (2 phòng = 48 ghế) + Ca 2 (2 phòng = 48 ghế) -> Ca 1 cân bằng [20, 20], Ca 2 dư giữ [0, 0]
        var courseSurplus = new Course { Id = Guid.NewGuid(), Code = "SWR302", Name = "Requirements", SemesterId = semester.Id, IsActive = true };
        var classSurplus = new Class { Id = Guid.NewGuid(), Code = "SE1820", Name = "SE1820", JoinCode = "SE1820SWR", CourseId = courseSurplus.Id, SemesterId = semester.Id, IsActive = true };
        var sessionSurplus = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = courseSurplus.Id, SemesterId = semester.Id, Title = "SWR302", ExamDate = new DateOnly(2026, 12, 21), Status = "scheduled" };
        context.Courses.Add(courseSurplus);
        context.Classes.Add(classSurplus);
        context.OfficialExamSessions.Add(sessionSurplus);
        var surplusStudents = SeedEnrolledStudents(context, new[] { classSurplus }, 40);
        SeedSessionCandidates(context, sessionSurplus.Id, surplusStudents, officer.Id);
        await context.SaveChangesAsync();

        // Mới mở 1 phòng 24 ghế cho 40 SV -> Thiếu 16 ghế (cần mở thêm 1 phòng) -> chặn 422
        var c1Start = new DateTime(2026, 12, 21, 1, 0, 0, DateTimeKind.Utc);
        await shiftHandler.Handle(new BatchOpenExamShiftsCommand(
            officer.Id, sessionSurplus.Id, "Ca 1", c1Start, c1Start.AddHours(2),
            new[] { new BatchOpenExamShiftRoomDto { RoomLab = "LAB-201", MaxSeats = 24 } }), CancellationToken.None);

        var insufficientSeats = await allocHandler.Handle(
            new AutoAllocateSessionStudentsCommand(officer.Id, sessionSurplus.Id),
            CancellationToken.None);
        insufficientSeats.IsSuccess.Should().BeFalse();
        insufficientSeats.Error.Should().Contain("thiếu 16 ghế").And.Contain("1 phòng");
        (await context.StudentExamTickets.CountAsync(t => t.Shift.SessionId == sessionSurplus.Id)).Should().Be(0);

        // Mở thêm LAB-202 cho Ca 1 và thêm Ca 2 gồm 2 phòng (LAB-201, LAB-202)
        await shiftHandler.Handle(new BatchOpenExamShiftsCommand(
            officer.Id, sessionSurplus.Id, "Ca 1", c1Start, c1Start.AddHours(2),
            new[] { new BatchOpenExamShiftRoomDto { RoomLab = "LAB-202", MaxSeats = 24 } }), CancellationToken.None);

        var c2Start = c1Start.AddHours(3);
        await shiftHandler.Handle(new BatchOpenExamShiftsCommand(
            officer.Id, sessionSurplus.Id, "Ca 2", c2Start, c2Start.AddHours(2),
            new[]
            {
                new BatchOpenExamShiftRoomDto { RoomLab = "LAB-201", MaxSeats = 24 },
                new BatchOpenExamShiftRoomDto { RoomLab = "LAB-202", MaxSeats = 24 }
            }), CancellationToken.None);

        var surplusAlloc = await allocHandler.Handle(
            new AutoAllocateSessionStudentsCommand(officer.Id, sessionSurplus.Id),
            CancellationToken.None);

        surplusAlloc.IsSuccess.Should().BeTrue();
        surplusAlloc.Value.ShiftGroups.Should().HaveCount(2);
        surplusAlloc.Value.ShiftGroups[0].IsFinalBalancedShift.Should().BeTrue();
        surplusAlloc.Value.ShiftGroups[0].Rooms.Select(r => r.TotalAfter).Should().Equal(20, 20);
        surplusAlloc.Value.ShiftGroups[1].IsFinalBalancedShift.Should().BeFalse();
        surplusAlloc.Value.ShiftGroups[1].Rooms.Select(r => r.TotalAfter).Should().Equal(0, 0);
    }
}
