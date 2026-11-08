using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.Commands.AddClassStudent;
using OralExamination.Application.Features.AcademicHierarchy.Commands.RemoveClassStudent;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.AcademicHierarchy;

public class LecturerClassStudentEnrollmentTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: $"EnrollmentTestDb_{Guid.NewGuid():N}")
            .Options;

        return new OralExamDbContext(options);
    }

    private static (Semester semester, Course course, Class testClass, User ownerLecturer, User otherLecturer, User deptHead, User admin, User student1, User student2, User inactiveStudent, User lecturerUser)
        SeedTestData(OralExamDbContext context, int maxStudents = 35)
    {
        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };

        var deptHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "depthead@fpt.edu.vn",
            FullName = "Department Head",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@fpt.edu.vn",
            FullName = "Administrator",
            Role = UserRole.Admin,
            IsActive = true
        };

        var program = new AcademicProgram
        {
            Id = Guid.NewGuid(),
            Code = "SE",
            Name = "Software Engineering",
            DepartmentHeadUserId = deptHead.Id,
            IsActive = true
        };

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Cross-Platform Apps",
            AcademicProgramId = program.Id,
            DepartmentHeadUserId = deptHead.Id,
            IsActive = true
        };

        var ownerLecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@fpt.edu.vn",
            FullName = "Owner Lecturer",
            Role = UserRole.Lecturer,
            IsActive = true
        };

        var otherLecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "other@fpt.edu.vn",
            FullName = "Other Lecturer",
            Role = UserRole.Lecturer,
            IsActive = true
        };

        var student1 = new User
        {
            Id = Guid.NewGuid(),
            Email = "hoanglvse170001@fpt.edu.vn",
            FullName = "Le Vu Hoang",
            StudentCode = "SE170001",
            Role = UserRole.Student,
            IsActive = true
        };

        var student2 = new User
        {
            Id = Guid.NewGuid(),
            Email = "maittse170002@fpt.edu.vn",
            FullName = "Tran Thi Mai",
            StudentCode = "SE170002",
            Role = UserRole.Student,
            IsActive = true
        };

        var inactiveStudent = new User
        {
            Id = Guid.NewGuid(),
            Email = "inactive@fpt.edu.vn",
            FullName = "Inactive Student",
            StudentCode = "SE170999",
            Role = UserRole.Student,
            IsActive = false
        };

        var nonStudentRoleUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "anotherlecturer@fpt.edu.vn",
            FullName = "Another Staff",
            Role = UserRole.Lecturer,
            IsActive = true
        };

        var testClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1801-NET",
            Name = "Lớp SE1801-NET",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = ownerLecturer.Id,
            MaxStudents = maxStudents,
            IsActive = true
        };

        context.Semesters.Add(semester);
        context.AcademicPrograms.Add(program);
        context.Courses.Add(course);
        context.Users.AddRange(deptHead, admin, ownerLecturer, otherLecturer, student1, student2, inactiveStudent, nonStudentRoleUser);
        context.Classes.Add(testClass);
        context.SaveChanges();

        return (semester, course, testClass, ownerLecturer, otherLecturer, deptHead, admin, student1, student2, inactiveStudent, nonStudentRoleUser);
    }

    [Fact(DisplayName = "1. Giảng viên sở hữu lớp thêm sinh viên thành công bằng StudentCode (MSSV)")]
    public async Task AddStudent_WhenCallerIsClassOwner_ByStudentCode_ReturnsSuccess()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, owner, _, _, _, student1, _, _, _) = SeedTestData(context);

        var handler = new AddClassStudentCommandHandler(context);
        var command = new AddClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: owner.Id,
            ActorRole: UserRole.Lecturer,
            StudentCode: "se170001"); // Case-insensitive test

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.StudentId.Should().Be(student1.Id);
        result.Value.StudentCode.Should().Be("SE170001");
        result.Value.Status.Should().Be("enrolled");
        result.Value.JoinMethod.Should().Be("assigned");

        var enrollment = await context.ClassEnrollments.FirstOrDefaultAsync(e => e.ClassId == testClass.Id && e.StudentId == student1.Id);
        enrollment.Should().NotBeNull();
        enrollment!.Status.Should().Be("enrolled");
        enrollment.JoinMethod.Should().Be("assigned");
    }

    [Fact(DisplayName = "2. Giảng viên sở hữu lớp thêm sinh viên thành công bằng Email")]
    public async Task AddStudent_WhenCallerIsClassOwner_ByEmail_ReturnsSuccess()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, owner, _, _, _, student1, _, _, _) = SeedTestData(context);

        var handler = new AddClassStudentCommandHandler(context);
        var command = new AddClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: owner.Id,
            ActorRole: UserRole.Lecturer,
            Email: "HOANGLVSE170001@FPT.EDU.VN"); // Case-insensitive test

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StudentId.Should().Be(student1.Id);
    }

    [Fact(DisplayName = "3. Giảng viên sở hữu lớp thêm sinh viên thành công bằng StudentId (Guid)")]
    public async Task AddStudent_WhenCallerIsClassOwner_ByStudentId_ReturnsSuccess()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, owner, _, _, _, student1, _, _, _) = SeedTestData(context);

        var handler = new AddClassStudentCommandHandler(context);
        var command = new AddClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: owner.Id,
            ActorRole: UserRole.Lecturer,
            StudentId: student1.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StudentId.Should().Be(student1.Id);
    }

    [Fact(DisplayName = "4. Giảng viên sở hữu lớp thêm sinh viên thành công bằng Identifier tự do")]
    public async Task AddStudent_WhenCallerIsClassOwner_ByIdentifier_ReturnsSuccess()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, owner, _, _, _, student1, _, _, _) = SeedTestData(context);

        var handler = new AddClassStudentCommandHandler(context);
        var command = new AddClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: owner.Id,
            ActorRole: UserRole.Lecturer,
            Identifier: "SE170001");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StudentId.Should().Be(student1.Id);
    }

    [Fact(DisplayName = "5. Giảng viên khác lớp cố tình thêm sinh viên bị chặn FORBIDDEN (403)")]
    public async Task AddStudent_WhenCallerIsAnotherLecturer_Returns403Forbidden()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, _, otherLecturer, _, _, student1, _, _, _) = SeedTestData(context);

        var handler = new AddClassStudentCommandHandler(context);
        var command = new AddClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: otherLecturer.Id,
            ActorRole: UserRole.Lecturer,
            StudentId: student1.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("FORBIDDEN:");
    }

    [Fact(DisplayName = "6. Admin có quyền thêm sinh viên vào lớp của bất kỳ giảng viên nào")]
    public async Task AddStudent_WhenCallerIsAdmin_ReturnsSuccess()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, _, _, _, admin, student1, _, _, _) = SeedTestData(context);

        var handler = new AddClassStudentCommandHandler(context);
        var command = new AddClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: admin.Id,
            ActorRole: UserRole.Admin,
            StudentId: student1.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StudentId.Should().Be(student1.Id);
    }

    [Fact(DisplayName = "7. Trưởng bộ môn có quyền thêm sinh viên vào lớp thuộc môn do mình phụ trách")]
    public async Task AddStudent_WhenCallerIsDepartmentHeadOfCourse_ReturnsSuccess()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, _, _, deptHead, _, student1, _, _, _) = SeedTestData(context);

        var handler = new AddClassStudentCommandHandler(context);
        var command = new AddClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: deptHead.Id,
            ActorRole: UserRole.DepartmentHead,
            StudentId: student1.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StudentId.Should().Be(student1.Id);
    }

    [Fact(DisplayName = "8. Thêm sinh viên với ClassId không tồn tại trả về NOT_FOUND (404)")]
    public async Task AddStudent_WhenClassNotFound_Returns404NotFound()
    {
        await using var context = CreateDbContext();
        var (_, _, _, owner, _, _, _, student1, _, _, _) = SeedTestData(context);

        var handler = new AddClassStudentCommandHandler(context);
        var command = new AddClassStudentCommand(
            ClassId: Guid.NewGuid(),
            ActorUserId: owner.Id,
            ActorRole: UserRole.Lecturer,
            StudentId: student1.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("NOT_FOUND:");
    }

    [Fact(DisplayName = "9. Thêm sinh viên không tồn tại trả về NOT_FOUND (404)")]
    public async Task AddStudent_WhenStudentNotFound_Returns404NotFound()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, owner, _, _, _, _, _, _, _) = SeedTestData(context);

        var handler = new AddClassStudentCommandHandler(context);
        var command = new AddClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: owner.Id,
            ActorRole: UserRole.Lecturer,
            StudentCode: "SE999999");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("NOT_FOUND:");
    }

    [Fact(DisplayName = "10. Thêm người dùng không phải vai trò sinh viên trả về VALIDATION (400)")]
    public async Task AddStudent_WhenUserIsNotStudentRole_Returns400BadRequest()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, owner, _, _, _, _, _, _, nonStudent) = SeedTestData(context);

        var handler = new AddClassStudentCommandHandler(context);
        var command = new AddClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: owner.Id,
            ActorRole: UserRole.Lecturer,
            StudentId: nonStudent.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("VALIDATION:");
    }

    [Fact(DisplayName = "11. Thêm sinh viên có tài khoản bị khóa trả về VALIDATION (400)")]
    public async Task AddStudent_WhenStudentIsInactive_Returns400BadRequest()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, owner, _, _, _, _, _, inactiveStudent, _) = SeedTestData(context);

        var handler = new AddClassStudentCommandHandler(context);
        var command = new AddClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: owner.Id,
            ActorRole: UserRole.Lecturer,
            StudentId: inactiveStudent.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("VALIDATION:");
    }

    [Fact(DisplayName = "12. Thêm sinh viên đã ghi danh sẵn trong lớp trả về CONFLICT (409)")]
    public async Task AddStudent_WhenStudentAlreadyEnrolled_Returns409Conflict()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, owner, _, _, _, student1, _, _, _) = SeedTestData(context);

        // Pre-enroll
        context.ClassEnrollments.Add(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = testClass.Id,
            StudentId = student1.Id,
            Status = "enrolled",
            EnrolledAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var handler = new AddClassStudentCommandHandler(context);
        var command = new AddClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: owner.Id,
            ActorRole: UserRole.Lecturer,
            StudentId: student1.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT:");
    }

    [Fact(DisplayName = "13. Sinh viên từng bị dropped được kích hoạt lại trạng thái enrolled")]
    public async Task AddStudent_WhenStudentWasPreviouslyDropped_ReactivatesEnrollment()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, owner, _, _, _, student1, _, _, _) = SeedTestData(context);

        // Previously dropped enrollment
        context.ClassEnrollments.Add(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = testClass.Id,
            StudentId = student1.Id,
            Status = "dropped",
            EnrolledAt = DateTime.UtcNow.AddDays(-10)
        });
        await context.SaveChangesAsync();

        var handler = new AddClassStudentCommandHandler(context);
        var command = new AddClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: owner.Id,
            ActorRole: UserRole.Lecturer,
            StudentId: student1.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("enrolled");

        var enrollment = await context.ClassEnrollments.FirstOrDefaultAsync(e => e.ClassId == testClass.Id && e.StudentId == student1.Id);
        enrollment.Should().NotBeNull();
        enrollment!.Status.Should().Be("enrolled");
    }

    [Fact(DisplayName = "14. Lớp học đã đạt sĩ số tối đa MaxStudents trả về CONFLICT (409)")]
    public async Task AddStudent_WhenClassCapacityReached_Returns409Conflict()
    {
        await using var context = CreateDbContext();
        // Seed with MaxStudents = 1
        var (_, _, testClass, owner, _, _, _, student1, student2, _, _) = SeedTestData(context, maxStudents: 1);

        // Fill capacity
        context.ClassEnrollments.Add(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = testClass.Id,
            StudentId = student1.Id,
            Status = "enrolled",
            EnrolledAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var handler = new AddClassStudentCommandHandler(context);
        var command = new AddClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: owner.Id,
            ActorRole: UserRole.Lecturer,
            StudentId: student2.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT:");
    }

    [Fact(DisplayName = "15. Giảng viên sở hữu lớp xóa sinh viên khỏi lớp thành công bằng StudentId (Guid)")]
    public async Task RemoveStudent_WhenCallerIsClassOwner_ByStudentId_ReturnsSuccess()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, owner, _, _, _, student1, _, _, _) = SeedTestData(context);

        context.ClassEnrollments.Add(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = testClass.Id,
            StudentId = student1.Id,
            Status = "enrolled",
            EnrolledAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var handler = new RemoveClassStudentCommandHandler(context);
        var command = new RemoveClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: owner.Id,
            ActorRole: UserRole.Lecturer,
            StudentId: student1.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.StudentId.Should().Be(student1.Id);

        var enrollmentInDb = await context.ClassEnrollments.FirstOrDefaultAsync(e => e.ClassId == testClass.Id && e.StudentId == student1.Id);
        enrollmentInDb.Should().BeNull();
    }

    [Fact(DisplayName = "16. Giảng viên sở hữu lớp xóa sinh viên thành công bằng StudentCode (MSSV)")]
    public async Task RemoveStudent_WhenCallerIsClassOwner_ByStudentCode_ReturnsSuccess()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, owner, _, _, _, student1, _, _, _) = SeedTestData(context);

        context.ClassEnrollments.Add(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = testClass.Id,
            StudentId = student1.Id,
            Status = "enrolled",
            EnrolledAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var handler = new RemoveClassStudentCommandHandler(context);
        var command = new RemoveClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: owner.Id,
            ActorRole: UserRole.Lecturer,
            StudentCode: "se170001");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StudentId.Should().Be(student1.Id);
    }

    [Fact(DisplayName = "17. Giảng viên khác lớp cố tình xóa sinh viên bị chặn FORBIDDEN (403)")]
    public async Task RemoveStudent_WhenCallerIsAnotherLecturer_Returns403Forbidden()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, _, otherLecturer, _, _, student1, _, _, _) = SeedTestData(context);

        context.ClassEnrollments.Add(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = testClass.Id,
            StudentId = student1.Id,
            Status = "enrolled",
            EnrolledAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var handler = new RemoveClassStudentCommandHandler(context);
        var command = new RemoveClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: otherLecturer.Id,
            ActorRole: UserRole.Lecturer,
            StudentId: student1.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("FORBIDDEN:");
    }

    [Fact(DisplayName = "18. Xóa sinh viên chưa ghi danh trong lớp trả về NOT_FOUND (404)")]
    public async Task RemoveStudent_WhenStudentNotEnrolledInClass_Returns404NotFound()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, owner, _, _, _, student1, _, _, _) = SeedTestData(context);

        // Not enrolled
        var handler = new RemoveClassStudentCommandHandler(context);
        var command = new RemoveClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: owner.Id,
            ActorRole: UserRole.Lecturer,
            StudentId: student1.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("NOT_FOUND:");
    }

    [Fact(DisplayName = "19. ClassesController Add & Remove Student endpoints trả về HTTP Status Codes chuẩn")]
    public async Task Controller_AddAndRemoveStudent_Endpoints_ReturnCorrectStatusCodes()
    {
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();

        var mediatorMock = new Mock<ISender>();

        mediatorMock.Setup(m => m.Send(It.IsAny<AddClassStudentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AddClassStudentResponseDto>.Success(new AddClassStudentResponseDto(
                classId, studentId, "Le Vu Hoang", "hoang@fpt.edu.vn", "SE170001", DateTime.UtcNow, "enrolled", "assigned")));

        mediatorMock.Setup(m => m.Send(It.IsAny<RemoveClassStudentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RemoveClassStudentResponseDto>.Success(new RemoveClassStudentResponseDto(
                classId, studentId, "Le Vu Hoang", "hoang@fpt.edu.vn", "SE170001", "Success")));

        var controller = new ClassesController(mediatorMock.Object);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, lecturerId.ToString()),
            new Claim(ClaimTypes.Role, UserRole.Lecturer)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        // Test Add
        var addResult = await controller.AddStudentToClass(classId, new AddClassStudentRequest(StudentCode: "SE170001"));
        var createdResult = addResult as ObjectResult;
        createdResult.Should().NotBeNull();
        createdResult!.StatusCode.Should().Be(StatusCodes.Status201Created);

        // Test Remove by Route
        var removeResult = await controller.RemoveStudentFromClass(classId, studentId);
        var okResult = removeResult as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        // Test Remove Flexible
        var removeFlexResult = await controller.RemoveStudentFlexible(classId, studentCode: "SE170001");
        var okFlexResult = removeFlexResult as OkObjectResult;
        okFlexResult.Should().NotBeNull();
        okFlexResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "20. Controller AddStudentToClass trả về HTTP 403 Forbidden RFC 7807 khi phát hiện BOLA attack")]
    public async Task Controller_AddStudent_WhenBOLAAttack_Returns403ForbiddenWithRfc7807()
    {
        var classId = Guid.NewGuid();
        var attackerLecturerId = Guid.NewGuid();

        var mediatorMock = new Mock<ISender>();
        mediatorMock.Setup(m => m.Send(It.IsAny<AddClassStudentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AddClassStudentResponseDto>.Failure(
                "FORBIDDEN: Giảng viên chỉ được phép quản lý danh sách sinh viên của lớp học phần do chính mình phụ trách."));

        var controller = new ClassesController(mediatorMock.Object);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, attackerLecturerId.ToString()),
            new Claim(ClaimTypes.Role, UserRole.Lecturer)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        var result = await controller.AddStudentToClass(classId, new AddClassStudentRequest(StudentCode: "SE170001"));

        var objectResult = result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        var problemDetails = objectResult.Value as ProblemDetails;
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(StatusCodes.Status403Forbidden);
        problemDetails.Title.Should().Be("Forbidden");
        problemDetails.Type.Should().Be("https://tools.ietf.org/html/rfc7807");
        problemDetails.Detail.Should().Contain("FORBIDDEN:");
    }

    [Fact(DisplayName = "21. Controller RemoveStudent endpoints trả về HTTP 403 Forbidden RFC 7807 khi phát hiện BOLA attack")]
    public async Task Controller_RemoveStudent_WhenBOLAAttack_Returns403ForbiddenWithRfc7807()
    {
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var attackerLecturerId = Guid.NewGuid();

        var mediatorMock = new Mock<ISender>();
        mediatorMock.Setup(m => m.Send(It.IsAny<RemoveClassStudentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RemoveClassStudentResponseDto>.Failure(
                "FORBIDDEN: Giảng viên chỉ được phép quản lý danh sách sinh viên của lớp học phần do chính mình phụ trách."));

        var controller = new ClassesController(mediatorMock.Object);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, attackerLecturerId.ToString()),
            new Claim(ClaimTypes.Role, UserRole.Lecturer)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        // Remove by route
        var routeResult = await controller.RemoveStudentFromClass(classId, studentId);
        var routeObj = routeResult as ObjectResult;
        routeObj.Should().NotBeNull();
        routeObj!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var routeProblem = routeObj.Value as ProblemDetails;
        routeProblem.Should().NotBeNull();
        routeProblem!.Status.Should().Be(StatusCodes.Status403Forbidden);
        routeProblem.Type.Should().Be("https://tools.ietf.org/html/rfc7807");

        // Remove flexible
        var flexResult = await controller.RemoveStudentFlexible(classId, studentId: studentId);
        var flexObj = flexResult as ObjectResult;
        flexObj.Should().NotBeNull();
        flexObj!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var flexProblem = flexObj.Value as ProblemDetails;
        flexProblem.Should().NotBeNull();
        flexProblem!.Status.Should().Be(StatusCodes.Status403Forbidden);
        flexProblem.Type.Should().Be("https://tools.ietf.org/html/rfc7807");
    }

    [Fact(DisplayName = "22. Controller AddStudent trả về HTTP 404 Not Found RFC 7807 khi sinh viên không tồn tại")]
    public async Task Controller_AddStudent_WhenStudentNotFound_Returns404NotFoundWithRfc7807()
    {
        var classId = Guid.NewGuid();
        var mediatorMock = new Mock<ISender>();
        mediatorMock.Setup(m => m.Send(It.IsAny<AddClassStudentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AddClassStudentResponseDto>.Failure(
                "NOT_FOUND: Không tìm thấy sinh viên trong hệ thống với thông tin nhận diện đã cung cấp."));

        var controller = new ClassesController(mediatorMock.Object);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, UserRole.Lecturer)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        var result = await controller.AddStudentToClass(classId, new AddClassStudentRequest(StudentCode: "SE999999"));

        var notFoundResult = result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
        notFoundResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var problemDetails = notFoundResult.Value as ProblemDetails;
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(StatusCodes.Status404NotFound);
        problemDetails.Title.Should().Be("Not Found");
        problemDetails.Type.Should().Be("https://tools.ietf.org/html/rfc7807");
        problemDetails.Detail.Should().Contain("Không tìm thấy sinh viên");
    }

    [Fact(DisplayName = "23. Controller AddStudent trả về HTTP 400 Bad Request RFC 7807 khi thêm user không phải student")]
    public async Task Controller_AddStudent_WhenNonStudentRole_Returns400BadRequestWithRfc7807()
    {
        var classId = Guid.NewGuid();
        var mediatorMock = new Mock<ISender>();
        mediatorMock.Setup(m => m.Send(It.IsAny<AddClassStudentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AddClassStudentResponseDto>.Failure(
                "VALIDATION: Người dùng được chỉ định không có vai trò Sinh viên (role != student)."));

        var controller = new ClassesController(mediatorMock.Object);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, UserRole.Lecturer)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        var result = await controller.AddStudentToClass(classId, new AddClassStudentRequest(StudentId: Guid.NewGuid()));

        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var problemDetails = badRequestResult.Value as ProblemDetails;
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Title.Should().Be("Bad Request");
        problemDetails.Type.Should().Be("https://tools.ietf.org/html/rfc7807");
        problemDetails.Detail.Should().Contain("role != student");
    }

    [Fact(DisplayName = "24. Controller AddStudent trả về HTTP 409 Conflict RFC 7807 khi trùng hoặc vượt sĩ số")]
    public async Task Controller_AddStudent_WhenConflict_Returns409ConflictWithRfc7807()
    {
        var classId = Guid.NewGuid();
        var mediatorMock = new Mock<ISender>();
        mediatorMock.Setup(m => m.Send(It.IsAny<AddClassStudentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AddClassStudentResponseDto>.Failure(
                "CONFLICT: Sinh viên đã ghi danh vào lớp học này rồi."));

        var controller = new ClassesController(mediatorMock.Object);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, UserRole.Lecturer)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        var result = await controller.AddStudentToClass(classId, new AddClassStudentRequest(StudentCode: "SE170001"));

        var conflictResult = result as ConflictObjectResult;
        conflictResult.Should().NotBeNull();
        conflictResult!.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        var problemDetails = conflictResult.Value as ProblemDetails;
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(StatusCodes.Status409Conflict);
        problemDetails.Title.Should().Be("Conflict");
        problemDetails.Type.Should().Be("https://tools.ietf.org/html/rfc7807");
    }

    [Fact(DisplayName = "25. Controller RemoveStudent trả về HTTP 404 Not Found RFC 7807 khi sinh viên không có trong lớp")]
    public async Task Controller_RemoveStudent_WhenNotEnrolled_Returns404NotFoundWithRfc7807()
    {
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var mediatorMock = new Mock<ISender>();
        mediatorMock.Setup(m => m.Send(It.IsAny<RemoveClassStudentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RemoveClassStudentResponseDto>.Failure(
                "NOT_FOUND: Sinh viên hiện không ghi danh trong lớp học này."));

        var controller = new ClassesController(mediatorMock.Object);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, UserRole.Lecturer)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        var result = await controller.RemoveStudentFromClass(classId, studentId);

        var notFoundResult = result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
        notFoundResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var problemDetails = notFoundResult.Value as ProblemDetails;
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(StatusCodes.Status404NotFound);
        problemDetails.Type.Should().Be("https://tools.ietf.org/html/rfc7807");
    }

    [Fact(DisplayName = "26. Giảng viên sở hữu lớp thêm sinh viên thành công khi Identifier là chuỗi Guid")]
    public async Task AddStudent_WhenIdentifierIsGuidString_ResolvesStudentAndReturnsSuccess()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, owner, _, _, _, student1, _, _, _) = SeedTestData(context);

        var handler = new AddClassStudentCommandHandler(context);
        var command = new AddClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: owner.Id,
            ActorRole: UserRole.Lecturer,
            Identifier: student1.Id.ToString());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StudentId.Should().Be(student1.Id);

        var enrollment = await context.ClassEnrollments.FirstOrDefaultAsync(e => e.ClassId == testClass.Id && e.StudentId == student1.Id);
        enrollment.Should().NotBeNull();
        enrollment!.Status.Should().Be("enrolled");
    }

    [Fact(DisplayName = "27. Giảng viên sở hữu lớp xóa sinh viên bằng Email thành công và bản ghi ClassEnrollment bị xóa vật lý")]
    public async Task RemoveStudent_WhenCallerIsClassOwner_ByEmail_PhysicallyRemovesEnrollment()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, owner, _, _, _, student1, _, _, _) = SeedTestData(context);

        // Pre-enroll
        var enrollmentId = Guid.NewGuid();
        context.ClassEnrollments.Add(new ClassEnrollment
        {
            Id = enrollmentId,
            ClassId = testClass.Id,
            StudentId = student1.Id,
            Status = "enrolled",
            EnrolledAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var handler = new RemoveClassStudentCommandHandler(context);
        var command = new RemoveClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: owner.Id,
            ActorRole: UserRole.Lecturer,
            Email: "hoanglvse170001@fpt.edu.vn");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StudentId.Should().Be(student1.Id);

        // Verify physical removal from database
        var enrollmentInDb = await context.ClassEnrollments.FirstOrDefaultAsync(e => e.Id == enrollmentId);
        enrollmentInDb.Should().BeNull();

        // Verify AuditLog record
        var auditLog = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "STUDENT_REMOVED_FROM_CLASS" && a.EntityId == testClass.Id);
        auditLog.Should().NotBeNull();
        auditLog!.OldValues.Should().Contain(student1.Id.ToString());
    }

    [Fact(DisplayName = "28. Lệnh Add và Remove không cung cấp bất kỳ định danh sinh viên nào bị chặn VALIDATION")]
    public async Task AddAndRemove_WhenAllIdentifiersMissing_ReturnsValidationError()
    {
        await using var context = CreateDbContext();
        var (_, _, testClass, owner, _, _, _, _, _, _, _) = SeedTestData(context);

        // Add
        var addHandler = new AddClassStudentCommandHandler(context);
        var addCommand = new AddClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: owner.Id,
            ActorRole: UserRole.Lecturer);

        var addResult = await addHandler.Handle(addCommand, CancellationToken.None);
        addResult.IsSuccess.Should().BeFalse();
        addResult.Error.Should().StartWith("VALIDATION:");

        // Remove
        var removeHandler = new RemoveClassStudentCommandHandler(context);
        var removeCommand = new RemoveClassStudentCommand(
            ClassId: testClass.Id,
            ActorUserId: owner.Id,
            ActorRole: UserRole.Lecturer);

        var removeResult = await removeHandler.Handle(removeCommand, CancellationToken.None);
        removeResult.IsSuccess.Should().BeFalse();
        removeResult.Error.Should().StartWith("VALIDATION:");
    }
}
