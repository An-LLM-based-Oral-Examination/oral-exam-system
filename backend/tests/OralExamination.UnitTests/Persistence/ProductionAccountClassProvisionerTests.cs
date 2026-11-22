using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetClassDetail;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Persistence;

public sealed class ProductionAccountClassProvisionerTests
{
    private static OralExamDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: $"ProdProvisionTestDb_{Guid.NewGuid():N}")
            .Options;

        return new OralExamDbContext(options);
    }

    private static AesGcmClassJoinPasswordProtector CreateProtector() =>
        new("unit-test-master-secret-at-least-32-bytes-long-for-aes256gcm");

    [Fact(DisplayName = "Provisioner: Nạp 15 accounts, phân công 10 môn (kèm CourseOfferingId), 10 lớp (AES-GCM JoinPasswordProtected) và 30 ghi danh Nhóm 2A idempotent")]
    public async Task ProvisionAsync_HappyPath_ProvisionsAccountsClassesOfferingsAndEnrollmentsIdempotently()
    {
        await using var context = CreateInMemoryContext();
        var adminId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = adminId,
            Email = "bootstrap-admin@fpt.edu.vn",
            Username = "admin",
            FullName = "Bootstrap Admin",
            Role = UserRole.Admin,
            IsActive = true
        });
        await context.SaveChangesAsync();

        await AcademicCourseBankImporter.ImportAsync(context, NullLogger.Instance);

        var hasher = new Pbkdf2PasswordHasher();
        var protector = CreateProtector();

        var configObj = new
        {
            semesterCode = "FA26",
            academicProgramCode = "SE",
            departmentHeadUsername = "headse",
            assignProgramAndActiveCoursesDepartmentHead = true,
            users = new object[]
            {
                new { email = "headse@fe.edu.vn", username = "headse", fullName = "Tran Van Truong", role = "department_head", password = "TestHead-Password#1", mustChangePassword = false },
                new { email = "totnt@fe.edu.vn", username = "totnt", fullName = "Nguyen Trong Tot", role = "lecturer", password = "TestLec-Password#1", mustChangePassword = false },
                new { email = "lecture@fe.edu.vn", username = "lecture", fullName = "Giang Vien Test", role = "lecturer", password = "TestLec2-Password#1", mustChangePassword = false },
                new { email = "lecturer2@fe.edu.vn", username = "lecturer2", fullName = "Pham Minh Khoa", role = "lecturer", password = "TestLec3-Password#1", mustChangePassword = false },
                new { email = "khaothi@fe.edu.vn", username = "khaothi", fullName = "Phong Khao Thi", role = "examination_officer", password = "TestExam-Password#1", mustChangePassword = false },
                new { email = "haind@fe.edu.vn", username = "haind", fullName = "Nguyen Dang Hai", role = "proctor", password = "TestProc-Password#1", mustChangePassword = false },
                new { email = "hoanglvse170001@fpt.edu.vn", username = "hoalvse170001", studentCode = "SE170001", fullName = "Le Vu Hoang", role = "student", password = "TestStu1-Password#1", mustChangePassword = false },
                new { email = "maittse170002@fpt.edu.vn", username = "maittse170002", studentCode = "SE170002", fullName = "Tran Thi Mai", role = "student", password = "TestStu2-Password#1", mustChangePassword = false },
                new { email = "thanhnqse184236@fpt.edu.vn", username = "thanhnqse184236", studentCode = "SE184236", fullName = "Nguyen Quang Thanh", role = "student", password = "TestStu3-Password#1", mustChangePassword = false },
                new { email = "thanhnqse181515@fpt.edu.vn", username = "thanhse181515", studentCode = "SE181515", fullName = "Nguyen Quang Thanh 2B", role = "student", password = "TestStu2B-Password#1", mustChangePassword = false }
            },
            courseLecturerAssignments = new object[]
            {
                new { lecturerUsername = "totnt", courseCodes = new[] { "PRN231", "SWT301", "SWR302", "SWD392", "ENT001", "ENT104", "ENT203", "ENT303", "ENT403", "ENT503" } },
                new { lecturerUsername = "lecturer2", courseCodes = new[] { "SWT301", "SWR302", "SWD392" } }
            },
            classes = new object[]
            {
                new { code = "SE1801-NET", joinCode = "SE1801NET", courseCode = "PRN231", lecturerUsername = "totnt", joinPassword = "JoinPass#2026" },
                new { code = "SE1802-SWT", joinCode = "SE1802SWT", courseCode = "SWT301", lecturerUsername = "totnt", joinPassword = "JoinPass#2026" },
                new { code = "SE1803-SWR", joinCode = "SE1803SWR", courseCode = "SWR302", lecturerUsername = "totnt", joinPassword = "JoinPass#2026" },
                new { code = "SE1804-SWD", joinCode = "SE1804SWD", courseCode = "SWD392", lecturerUsername = "totnt", joinPassword = "JoinPass#2026" },
                new { code = "SE1801-ENT001", joinCode = "SE1801ENT001", courseCode = "ENT001", lecturerUsername = "totnt", joinPassword = "JoinPass#2026" },
                new { code = "SE1801-ENT104", joinCode = "SE1801ENT104", courseCode = "ENT104", lecturerUsername = "totnt", joinPassword = "JoinPass#2026" },
                new { code = "SE1801-ENT203", joinCode = "SE1801ENT203", courseCode = "ENT203", lecturerUsername = "totnt", joinPassword = "JoinPass#2026" },
                new { code = "SE1801-ENT303", joinCode = "SE1801ENT303", courseCode = "ENT303", lecturerUsername = "totnt", joinPassword = "JoinPass#2026" },
                new { code = "SE1801-ENT403", joinCode = "SE1801ENT403", courseCode = "ENT403", lecturerUsername = "totnt", joinPassword = "JoinPass#2026" },
                new { code = "SE1801-ENT503", joinCode = "SE1801ENT503", courseCode = "ENT503", lecturerUsername = "totnt", joinPassword = "JoinPass#2026" }
            },
            enrollments = new object[]
            {
                new
                {
                    studentUsernames = new[] { "hoalvse170001", "maittse170002", "thanhnqse184236" },
                    classCodes = new[] { "SE1801-NET", "SE1802-SWT", "SE1803-SWR", "SE1804-SWD", "SE1801-ENT001", "SE1801-ENT104", "SE1801-ENT203", "SE1801-ENT303", "SE1801-ENT403", "SE1801-ENT503" },
                    joinMethod = "assigned"
                }
            }
        };

        var json = JsonSerializer.Serialize(configObj);

        var res1 = await ProductionAccountClassProvisioner.ProvisionAsync(context, hasher, protector, json, NullLogger.Instance);
        res1.UsersCreated.Should().Be(10);
        res1.ClassesUpserted.Should().Be(10);
        res1.EnrollmentsCreated.Should().Be(30);

        // Kiểm tra DepartmentHeadUserId được gắn vào SE Program và 10 môn active
        var headSe = await context.Users.AsNoTracking().FirstAsync(u => u.Username == "headse");
        (await context.AcademicPrograms.AsNoTracking().FirstAsync(p => p.Code == "SE")).DepartmentHeadUserId.Should().Be(headSe.Id);
        (await context.Courses.AsNoTracking().Where(c => c.IsActive).ToListAsync()).Should().OnlyContain(c => c.DepartmentHeadUserId == headSe.Id);

        // Kiểm tra CourseLecturers có CourseOfferingId cho cả totnt (10 môn) & lecturer2 (3 môn)
        var totnt = await context.Users.AsNoTracking().FirstAsync(u => u.Username == "totnt");
        var totntAssignments = await context.CourseLecturers.AsNoTracking().Where(cl => cl.LecturerUserId == totnt.Id && cl.IsActive).ToListAsync();
        totntAssignments.Should().HaveCount(10);
        totntAssignments.Should().OnlyContain(cl => cl.CourseOfferingId != null);

        // Kiểm tra GetClassDetailQueryHandler giải mã được JoinPasswordProtected
        var swtClass = await context.Classes.AsNoTracking().FirstAsync(c => c.Code == "SE1802-SWT");
        swtClass.CourseOfferingId.Should().NotBeNull();
        hasher.Verify("JoinPass#2026", swtClass.JoinPasswordHash!).Should().BeTrue();
        protector.Unprotect(swtClass.JoinPasswordProtected).Should().Be("JoinPass#2026");

        var classDetailHandler = new GetClassDetailQueryHandler(context, protector);
        var detailRes = await classDetailHandler.Handle(
            new GetClassDetailQuery(swtClass.Id, totnt.Id, UserRole.Lecturer),
            CancellationToken.None);
        detailRes.IsSuccess.Should().BeTrue();
        detailRes.Value.JoinPassword.Should().Be("JoinPass#2026");

        // Kiểm tra Nhóm 2B (thanhse181515) vẫn chưa ghi danh
        var stu2b = await context.Users.AsNoTracking().FirstAsync(u => u.Username == "thanhse181515");
        (await context.ClassEnrollments.AnyAsync(e => e.StudentId == stu2b.Id)).Should().BeFalse();

        // Lần chạy 2 cùng JSON -> idempotent, AuthVersion giữ nguyên = 1
        var res2 = await ProductionAccountClassProvisioner.ProvisionAsync(context, hasher, protector, json, NullLogger.Instance);
        res2.UsersCreated.Should().Be(0);
        res2.UsersUpdated.Should().Be(0);
        res2.EnrollmentsCreated.Should().Be(0);
        (await context.Users.AsNoTracking().FirstAsync(u => u.Username == "totnt")).AuthVersion.Should().Be(1);

        // Đổi mật khẩu totnt -> AuthVersion phải tăng lên 2
        var updatedJson = json.Replace("TestLec-Password#1", "NewStrongLec-Pass#2");
        await ProductionAccountClassProvisioner.ProvisionAsync(context, hasher, protector, updatedJson, NullLogger.Instance);
        var updatedTotnt = await context.Users.AsNoTracking().FirstAsync(u => u.Username == "totnt");
        updatedTotnt.AuthVersion.Should().Be(2);
        hasher.Verify("NewStrongLec-Pass#2", updatedTotnt.PasswordHash!).Should().BeTrue();
    }

    [Fact(DisplayName = "Provisioner: Chặn fail-closed khi mật khẩu yếu/khoảng trắng, role sai hoặc lớp trỏ tới môn đã ẩn mềm PRF192")]
    public async Task ProvisionAsync_InvalidPayloadsOrInactiveCourses_FailClosedWithoutPartialWrites()
    {
        await using var context = CreateInMemoryContext();
        var sem = new Semester { Id = DevelopmentDataSeeder.SemesterFa26Id, Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var prfCourse = new Course { Id = DevelopmentDataSeeder.CoursePrf192Id, Code = "PRF192", Name = "PRF192", SemesterId = sem.Id, IsActive = false };
        var lecId = Guid.NewGuid();
        context.Semesters.Add(sem);
        context.Courses.Add(prfCourse);
        context.Users.Add(new User { Id = lecId, Email = "totnt@fe.edu.vn", Username = "totnt", FullName = "Tot", Role = UserRole.Lecturer, IsActive = true });
        await context.SaveChangesAsync();

        var hasher = new Pbkdf2PasswordHasher();
        var protector = CreateProtector();

        // 1. Mật khẩu user < 8 ký tự
        var shortPwdJson = JsonSerializer.Serialize(new
        {
            users = new[] { new { email = "x@fpt.edu.vn", username = "userx", fullName = "X", role = "student", password = "short" } }
        });
        await FluentActions.Invoking(() => ProductionAccountClassProvisioner.ProvisionAsync(context, hasher, protector, shortPwdJson))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*[FAIL CLOSED]*");

        // 2. Role không hợp lệ
        var badRoleJson = JsonSerializer.Serialize(new
        {
            users = new[] { new { email = "x@fpt.edu.vn", username = "userx", fullName = "X", role = "superadmin", password = "ValidPassword#1" } }
        });
        await FluentActions.Invoking(() => ProductionAccountClassProvisioner.ProvisionAsync(context, hasher, protector, badRoleJson))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*[FAIL CLOSED]*");

        // 3. Lớp trỏ tới môn đã ẩn mềm PRF192 (IsActive == false)
        var inactiveCourseClassJson = JsonSerializer.Serialize(new
        {
            semesterCode = "FA26",
            classes = new[] { new { code = "SE1804-PRF", courseCode = "PRF192", lecturerUsername = "totnt", joinPassword = "JoinPass#2026" } }
        });
        await FluentActions.Invoking(() => ProductionAccountClassProvisioner.ProvisionAsync(context, hasher, protector, inactiveCourseClassJson))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*[FAIL CLOSED]*PRF192*");

        (await context.Users.CountAsync()).Should().Be(1);
        (await context.Classes.CountAsync()).Should().Be(0);
    }

    [Theory(DisplayName = "Provisioner: Chặn fail-closed với InvalidOperationException khi JSON chứa null collection hoặc phần tử null")]
    [InlineData("""{"users": null}""")]
    [InlineData("""{"courseLecturerAssignments": null}""")]
    [InlineData("""{"classes": null}""")]
    [InlineData("""{"enrollments": null}""")]
    [InlineData("""{"users": [null]}""")]
    [InlineData("""{"courseLecturerAssignments": [{"lecturerUsername": "totnt", "courseCodes": null}]}""")]
    [InlineData("""{"enrollments": [{"studentUsernames": null, "classCodes": ["SE1801-NET"]}]}""")]
    [InlineData("""{"enrollments": [{"studentUsernames": ["stu1"], "classCodes": null}]}""")]
    public async Task ProvisionAsync_ExplicitNullCollections_ThrowFailClosedInvalidOperationException(string jsonPayload)
    {
        await using var context = CreateInMemoryContext();
        var hasher = new Pbkdf2PasswordHasher();
        var protector = CreateProtector();

        await FluentActions.Invoking(() =>
                ProductionAccountClassProvisioner.ProvisionAsync(context, hasher, protector, jsonPayload))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*[FAIL CLOSED]*");
    }
}
