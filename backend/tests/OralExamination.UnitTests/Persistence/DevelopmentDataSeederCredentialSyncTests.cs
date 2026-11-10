using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Persistence;

public class DevelopmentDataSeederCredentialSyncTests
{
    private static OralExamDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: $"SeederSyncTestDb_{Guid.NewGuid():N}")
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "Seeder phải cập nhật PasswordHash và MustChangePassword = false cho các tài khoản kiểm thử ngay cả khi đã tồn tại hash cũ")]
    public async Task SeedAsync_Should_Update_Credentials_And_MustChangePassword_For_Test_Accounts_Even_If_Existing()
    {
        await using var context = CreateInMemoryContext();
        var hasher = new Pbkdf2PasswordHasher();

        // Giả lập database đã có sẵn các tài khoản với password hash cũ/sai và MustChangePassword = true
        context.Users.AddRange(
            new User
            {
                Id = DevelopmentDataSeeder.LecturerLectureId,
                Email = "lecture@fe.edu.vn",
                FullName = "Old Lecturer",
                Username = "lecture",
                Role = UserRole.Student, // Giả sử role cũ bị sai
                PasswordHash = hasher.Hash("OldWrongPassword123!"),
                MustChangePassword = true,
                IsActive = false
            },
            new User
            {
                Id = DevelopmentDataSeeder.StudentHaiId,
                Email = "haipndse181313@fpt.edu.vn",
                FullName = "Old Hai",
                StudentCode = "SE181313",
                Username = "haise181313",
                Role = UserRole.Student,
                PasswordHash = hasher.Hash("OldWrongPassword123!"),
                MustChangePassword = true,
                IsActive = false
            },
            new User
            {
                Id = DevelopmentDataSeeder.StudentHoangTestId,
                Email = "hoanglvse181212@fpt.edu.vn",
                FullName = "Old Hoang",
                StudentCode = "SE181212",
                Username = "hoangse181212",
                Role = UserRole.Student,
                PasswordHash = hasher.Hash("OldWrongPassword123!"),
                MustChangePassword = true,
                IsActive = false
            },
            new User
            {
                Id = DevelopmentDataSeeder.StudentTotTestId,
                Email = "totntse181414@fpt.edu.vn",
                FullName = "Old Tot",
                StudentCode = "SE181414",
                Username = "totse181414",
                Role = UserRole.Student,
                PasswordHash = hasher.Hash("OldWrongPassword123!"),
                MustChangePassword = true,
                IsActive = false
            },
            new User
            {
                Id = DevelopmentDataSeeder.StudentThanhTestId,
                Email = "thanhnqse181515@fpt.edu.vn",
                FullName = "Old Thanh",
                StudentCode = "SE181515",
                Username = "thanhse181515",
                Role = UserRole.Student,
                PasswordHash = hasher.Hash("OldWrongPassword123!"),
                MustChangePassword = true,
                IsActive = false
            }
        );
        await context.SaveChangesAsync();

        // Chạy SeedAsync
        await DevelopmentDataSeeder.SeedAsync(context);

        // Kiểm tra giảng viên lecture
        var lecturer = await context.Users.FirstAsync(u => u.Id == DevelopmentDataSeeder.LecturerLectureId);
        lecturer.Role.Should().Be(UserRole.Lecturer);
        lecturer.IsActive.Should().BeTrue();
        lecturer.MustChangePassword.Should().BeFalse();
        hasher.Verify("Lecture-123", lecturer.PasswordHash!).Should().BeTrue();

        // Kiểm tra 4 sinh viên kiểm thử
        var hai = await context.Users.FirstAsync(u => u.Id == DevelopmentDataSeeder.StudentHaiId);
        hai.MustChangePassword.Should().BeFalse();
        hai.IsActive.Should().BeTrue();
        hasher.Verify("Haise181313", hai.PasswordHash!).Should().BeTrue();

        var hoang = await context.Users.FirstAsync(u => u.Id == DevelopmentDataSeeder.StudentHoangTestId);
        hoang.MustChangePassword.Should().BeFalse();
        hoang.IsActive.Should().BeTrue();
        hasher.Verify("Hoangse181212", hoang.PasswordHash!).Should().BeTrue();

        var tot = await context.Users.FirstAsync(u => u.Id == DevelopmentDataSeeder.StudentTotTestId);
        tot.MustChangePassword.Should().BeFalse();
        tot.IsActive.Should().BeTrue();
        hasher.Verify("Totse181414", tot.PasswordHash!).Should().BeTrue();

        var thanh = await context.Users.FirstAsync(u => u.Id == DevelopmentDataSeeder.StudentThanhTestId);
        thanh.MustChangePassword.Should().BeFalse();
        thanh.IsActive.Should().BeTrue();
        hasher.Verify("Thanh181515", thanh.PasswordHash!).Should().BeTrue();
    }

    [Fact(DisplayName = "Giảng viên lecture phải được phân công trong course_lecturers cho cả 3 môn DBI202, CSD201, PRF192 trong FA26")]
    public async Task SeedAsync_Should_Assign_Lecture_To_All_Three_Courses_In_FA26()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var targetCourses = await context.Courses
            .Where(c => c.Code == "PRF192" || c.Code == "CSD201" || c.Code == "DBI202")
            .ToListAsync();

        targetCourses.Should().HaveCount(3);

        foreach (var course in targetCourses)
        {
            var assignment = await context.CourseLecturers.FirstOrDefaultAsync(cl =>
                cl.CourseId == course.Id &&
                cl.LecturerUserId == DevelopmentDataSeeder.LecturerLectureId &&
                cl.SemesterId == DevelopmentDataSeeder.SemesterFa26Id &&
                cl.IsActive);

            assignment.Should().NotBeNull($"Giảng viên lecture phải được phân công cho môn {course.Code} trong học kỳ FA26");
        }
    }

    [Fact(DisplayName = "4 sinh viên kiểm thử không được ghi danh trước vào các lớp SE1804-PRF, SE1805-CSD, SE1806-DBI")]
    public async Task SeedAsync_Should_Ensure_4_Test_Students_Are_Unenrolled_From_Target_Classes()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var testStudentIds = new[]
        {
            DevelopmentDataSeeder.StudentHaiId,
            DevelopmentDataSeeder.StudentHoangTestId,
            DevelopmentDataSeeder.StudentTotTestId,
            DevelopmentDataSeeder.StudentThanhTestId
        };

        var targetClassCodes = new[] { "SE1804-PRF", "SE1805-CSD", "SE1806-DBI" };
        var targetClasses = await context.Classes
            .Where(c => targetClassCodes.Contains(c.Code))
            .ToListAsync();

        targetClasses.Should().HaveCount(3);
        var targetClassIds = targetClasses.Select(c => c.Id).ToList();

        var enrollments = await context.ClassEnrollments
            .Where(e => targetClassIds.Contains(e.ClassId) && testStudentIds.Contains(e.StudentId))
            .ToListAsync();

        enrollments.Should().BeEmpty("Cả 4 sinh viên kiểm thử phải chưa ghi danh vào 3 lớp để có thể test tính năng classes/join");
    }
}
