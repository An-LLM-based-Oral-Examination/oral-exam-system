using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams.Commands.BatchOpenExamShifts;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamShiftStaff;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class OfficialExamShiftStaffTests
{
    private static OralExamDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase($"official-shift-staff-{Guid.NewGuid():N}")
            .Options;
        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "EO Shift Staff: chỉ trả Proctor thật cho Giám thị và lọc Giám khảo đúng môn, không lẫn TBM ngành khác")]
    public async Task GetOfficialExamShiftStaff_ReturnsOnlyActiveProctors_AndCourseScopedExaminersWithoutOtherProgramHeads()
    {
        await using var db = CreateContext();

        var officer = new User { Email = "eo@fpt.edu.vn", FullName = "Cán bộ Khảo thí", Role = UserRole.ExaminationOfficer, IsActive = true };
        var proctor1 = new User { Email = "haind@fpt.edu.vn", FullName = "Nguyễn Đăng Hải", Role = UserRole.Proctor, IsActive = true };
        var proctor2 = new User { Email = "proctor@fpt.edu.vn", FullName = "Giám thị 02", Role = UserRole.Proctor, IsActive = true };
        var inactiveProctor = new User { Email = "inactive.proctor@fpt.edu.vn", FullName = "Giám thị đã khóa", Role = UserRole.Proctor, IsActive = false };

        var headSe = new User { Email = "headse@fpt.edu.vn", FullName = "Trưởng BM SE", Role = UserRole.DepartmentHead, IsActive = true };
        var headEnt = new User { Email = "headent@fpt.edu.vn", FullName = "Trưởng Bộ Môn Tiếng Anh", Role = UserRole.DepartmentHead, IsActive = true };

        var lecSwd1 = new User { Email = "totnt@fpt.edu.vn", FullName = "Nguyễn Trọng Tốt", Role = UserRole.Lecturer, IsActive = true };
        var lecSwd2 = new User { Email = "lec2@fpt.edu.vn", FullName = "Lê Giảng Viên", Role = UserRole.Lecturer, IsActive = true };
        var lecSeOther = new User { Email = "lecse@fpt.edu.vn", FullName = "GV Ngành SE Khác", Role = UserRole.Lecturer, IsActive = true };
        var lecEnt = new User { Email = "gvent@fpt.edu.vn", FullName = "GV Tiếng Anh", Role = UserRole.Lecturer, IsActive = true };
        var inactiveLec = new User { Email = "inactive.lec@fpt.edu.vn", FullName = "GV Đã Khóa", Role = UserRole.Lecturer, IsActive = false };

        var semester = new Semester
        {
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };
        var programSe = new AcademicProgram { Code = "SE", Name = "Software Engineering", DepartmentHeadUserId = headSe.Id, IsActive = true };
        var programEnt = new AcademicProgram { Code = "ENT", Name = "English", DepartmentHeadUserId = headEnt.Id, IsActive = true };

        var swdCourse = new Course
        {
            Code = "SWD392",
            Name = "SW Architecture and Design",
            AcademicProgramId = programSe.Id,
            SemesterId = semester.Id,
            DepartmentHeadUserId = headSe.Id,
            IsActive = true
        };
        var prnCourse = new Course
        {
            Code = "PRN231",
            Name = "Backend .NET",
            AcademicProgramId = programSe.Id,
            SemesterId = semester.Id,
            DepartmentHeadUserId = headSe.Id,
            IsActive = true
        };
        var entCourse = new Course
        {
            Code = "ENT104",
            Name = "English 1",
            AcademicProgramId = programEnt.Id,
            SemesterId = semester.Id,
            DepartmentHeadUserId = headEnt.Id,
            IsActive = true
        };

        db.Users.AddRange(officer, proctor1, proctor2, inactiveProctor, headSe, headEnt, lecSwd1, lecSwd2, lecSeOther, lecEnt, inactiveLec);
        db.Semesters.Add(semester);
        db.AcademicPrograms.AddRange(programSe, programEnt);
        db.Courses.AddRange(swdCourse, prnCourse, entCourse);
        db.CourseLecturers.AddRange(
            new CourseLecturer { CourseId = swdCourse.Id, LecturerUserId = lecSwd1.Id, AssignedByUserId = headSe.Id, SemesterId = semester.Id, IsActive = true },
            new CourseLecturer { CourseId = swdCourse.Id, LecturerUserId = lecSwd2.Id, AssignedByUserId = headSe.Id, SemesterId = semester.Id, IsActive = true },
            new CourseLecturer { CourseId = swdCourse.Id, LecturerUserId = inactiveLec.Id, AssignedByUserId = headSe.Id, SemesterId = semester.Id, IsActive = true },
            new CourseLecturer { CourseId = prnCourse.Id, LecturerUserId = lecSeOther.Id, AssignedByUserId = headSe.Id, SemesterId = semester.Id, IsActive = true },
            new CourseLecturer { CourseId = entCourse.Id, LecturerUserId = lecEnt.Id, AssignedByUserId = headEnt.Id, SemesterId = semester.Id, IsActive = true });
        await db.SaveChangesAsync();

        var handler = new GetOfficialExamShiftStaffQueryHandler(db);
        var result = await handler.Handle(
            new GetOfficialExamShiftStaffQuery(officer.Id, swdCourse.Id, semester.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Proctors.Select(p => p.UserId).Should().BeEquivalentTo(new[] { proctor1.Id, proctor2.Id });
        result.Value.Proctors.Should().OnlyContain(p => p.Role == UserRole.Proctor);

        var examinerIds = result.Value.Examiners.Select(e => e.UserId).ToList();
        examinerIds.Should().Contain(lecSwd1.Id);
        examinerIds.Should().Contain(lecSwd2.Id);
        examinerIds.Should().Contain(headSe.Id);
        examinerIds.Should().Contain(lecSeOther.Id);

        // Tuyệt đối không lộ TBM Tiếng Anh, GV Tiếng Anh, GV bị khóa hoặc Proctor sang danh sách Giám khảo SWD392
        examinerIds.Should().NotContain(headEnt.Id);
        examinerIds.Should().NotContain(lecEnt.Id);
        examinerIds.Should().NotContain(inactiveLec.Id);
        examinerIds.Should().NotContain(proctor1.Id);

        // Thứ tự ưu tiên: GV phụ trách môn & TBM của môn đứng trước GV cùng ngành
        result.Value.Examiners.Take(2).Should().OnlyContain(e => e.IsCourseAssigned && e.Role == UserRole.Lecturer);
        result.Value.Examiners[2].UserId.Should().Be(headSe.Id);
        result.Value.Examiners.Last().UserId.Should().Be(lecSeOther.Id);
        result.Value.Examiners.Last().IsCourseAssigned.Should().BeFalse();
    }

    [Fact(DisplayName = "EO Shift Staff: chặn vai trò không phải Khảo thí (403) và báo lỗi nếu courseId không tồn tại (404)")]
    public async Task GetOfficialExamShiftStaff_RejectsNonOfficerAndMissingCourse()
    {
        await using var db = CreateContext();
        var lecturer = new User { Email = "lec@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var officer = new User { Email = "eo@fpt.edu.vn", FullName = "EO", Role = UserRole.ExaminationOfficer, IsActive = true };
        db.Users.AddRange(lecturer, officer);
        await db.SaveChangesAsync();

        var handler = new GetOfficialExamShiftStaffQueryHandler(db);

        var forbidden = await handler.Handle(
            new GetOfficialExamShiftStaffQuery(lecturer.Id, null, null),
            CancellationToken.None);
        forbidden.IsSuccess.Should().BeFalse();
        forbidden.Error.Should().StartWith("FORBIDDEN:");

        var inactiveOfficer = new User { Email = "eo.inactive@fpt.edu.vn", FullName = "EO Inactive", Role = UserRole.ExaminationOfficer, IsActive = false };
        db.Users.Add(inactiveOfficer);
        await db.SaveChangesAsync();

        var forbiddenInactive = await handler.Handle(
            new GetOfficialExamShiftStaffQuery(inactiveOfficer.Id, null, null),
            CancellationToken.None);
        forbiddenInactive.IsSuccess.Should().BeFalse();
        forbiddenInactive.Error.Should().StartWith("FORBIDDEN:");

        var allWithoutCourse = await handler.Handle(
            new GetOfficialExamShiftStaffQuery(officer.Id, null, null),
            CancellationToken.None);
        allWithoutCourse.IsSuccess.Should().BeTrue();
        allWithoutCourse.Value.Examiners.Select(e => e.UserId).Should().Contain(lecturer.Id);

        var notFound = await handler.Handle(
            new GetOfficialExamShiftStaffQuery(officer.Id, Guid.NewGuid(), null),
            CancellationToken.None);
        notFound.IsSuccess.Should().BeFalse();
        notFound.Error.Should().StartWith("NOT_FOUND:");
    }

    [Fact(DisplayName = "EO Shift Staff: khi môn chưa có đủ 2 GV phụ trách thì bổ sung GV dự phòng nhưng tuyệt đối không kéo TBM ngành khác")]
    public async Task GetOfficialExamShiftStaff_FallbackExaminers_NeverIncludesOtherDepartmentHeads()
    {
        await using var db = CreateContext();

        var officer = new User { Email = "eo@fpt.edu.vn", FullName = "EO", Role = UserRole.ExaminationOfficer, IsActive = true };
        var headSe = new User { Email = "headse@fpt.edu.vn", FullName = "Trưởng BM SE", Role = UserRole.DepartmentHead, IsActive = true };
        var headEnt = new User { Email = "headent@fpt.edu.vn", FullName = "Trưởng BM Tiếng Anh", Role = UserRole.DepartmentHead, IsActive = true };
        var spareLecturer = new User { Email = "spare@fpt.edu.vn", FullName = "GV Dự Phòng", Role = UserRole.Lecturer, IsActive = true };

        var semester = new Semester
        {
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };
        var swdCourse = new Course
        {
            Code = "SWD392",
            Name = "SWD",
            SemesterId = semester.Id,
            DepartmentHeadUserId = headSe.Id,
            IsActive = true
        };

        db.Users.AddRange(officer, headSe, headEnt, spareLecturer);
        db.Semesters.Add(semester);
        db.Courses.Add(swdCourse);
        await db.SaveChangesAsync();

        var handler = new GetOfficialExamShiftStaffQueryHandler(db);
        var result = await handler.Handle(
            new GetOfficialExamShiftStaffQuery(officer.Id, swdCourse.Id, semester.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Examiners.Select(e => e.UserId).Should().Equal(headSe.Id, spareLecturer.Id);
        result.Value.Examiners.Select(e => e.UserId).Should().NotContain(headEnt.Id);
    }

    [Fact(DisplayName = "BatchOpenExamShifts: chấp nhận DepartmentHead làm Giám khảo khớp OpenExamShift, vẫn chặn Proctor")]
    public async Task BatchOpenExamShifts_AcceptsDepartmentHeadAsExaminer_RejectsProctor()
    {
        await using var db = CreateContext();

        var officer = new User { Email = "eo@fpt.edu.vn", FullName = "EO", Role = UserRole.ExaminationOfficer, IsActive = true };
        var proctor1 = new User { Email = "p1@fpt.edu.vn", FullName = "Proctor 1", Role = UserRole.Proctor, IsActive = true };
        var proctor2 = new User { Email = "p2@fpt.edu.vn", FullName = "Proctor 2", Role = UserRole.Proctor, IsActive = true };
        var lecturer = new User { Email = "l1@fpt.edu.vn", FullName = "Lecturer 1", Role = UserRole.Lecturer, IsActive = true };
        var deptHead = new User { Email = "dh@fpt.edu.vn", FullName = "Trưởng BM SE", Role = UserRole.DepartmentHead, IsActive = true };

        var semester = new Semester
        {
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };
        var course = new Course { Code = "SWD392", Name = "SWD", SemesterId = semester.Id, DepartmentHeadUserId = deptHead.Id, IsActive = true };
        var session = new OfficialExamSession
        {
            CourseId = course.Id,
            SemesterId = semester.Id,
            Title = "SWD392 FE",
            ExamDate = new DateOnly(2026, 11, 28),
            ExamInputMode = ExamInputMode.VoiceWithTranscriptEdit,
            Status = "scheduled",
            CreatedBy = officer.Id
        };

        db.Users.AddRange(officer, proctor1, proctor2, lecturer, deptHead);
        db.Semesters.Add(semester);
        db.Courses.Add(course);
        db.OfficialExamSessions.Add(session);
        await db.SaveChangesAsync();

        var handler = new BatchOpenExamShiftsCommandHandler(db);
        var start = DateTime.UtcNow.AddHours(2);
        var end = start.AddHours(1);

        var okResult = await handler.Handle(
            new BatchOpenExamShiftsCommand(
                officer.Id,
                session.Id,
                "Ca 1",
                start,
                end,
                new[]
                {
                    new BatchOpenExamShiftRoomDto { RoomLab = "A1", MaxSeats = 24, ProctorUserId = proctor1.Id, ExaminerUserId = lecturer.Id },
                    new BatchOpenExamShiftRoomDto { RoomLab = "A2", MaxSeats = 24, ProctorUserId = proctor2.Id, ExaminerUserId = deptHead.Id }
                }),
            CancellationToken.None);

        okResult.IsSuccess.Should().BeTrue();
        okResult.Value.CreatedRoomCount.Should().Be(2);

        var badExaminer = await handler.Handle(
            new BatchOpenExamShiftsCommand(
                officer.Id,
                session.Id,
                "Ca 2",
                end.AddMinutes(15),
                end.AddHours(1).AddMinutes(15),
                new[]
                {
                    new BatchOpenExamShiftRoomDto { RoomLab = "A3", MaxSeats = 24, ProctorUserId = proctor1.Id, ExaminerUserId = proctor2.Id }
                }),
            CancellationToken.None);

        badExaminer.IsSuccess.Should().BeFalse();
        badExaminer.Error.Should().StartWith("UNPROCESSABLE:");
    }
}
