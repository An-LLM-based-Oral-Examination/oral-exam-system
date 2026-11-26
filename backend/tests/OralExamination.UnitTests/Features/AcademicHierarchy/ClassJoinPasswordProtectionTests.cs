using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OralExamination.Application.Features.AcademicHierarchy.Commands.CreateClass;
using OralExamination.Application.Features.AcademicHierarchy.Commands.ResetClassPassword;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetClassDetail;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.AcademicHierarchy;

public sealed class ClassJoinPasswordProtectionTests
{
    private const string TestMasterSecret = "test-master-secret-key-at-least-32-bytes-long-2026!";
    private const string AlternateMasterSecret = "alternate-secret-key-at-least-32-bytes-long-2026!!";

    private readonly Pbkdf2PasswordHasher _passwordHasher = new();
    private readonly AesGcmClassJoinPasswordProtector _protector = new(TestMasterSecret);

    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: $"ClassJoinPwd_{Guid.NewGuid():N}")
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "1. AesGcmClassJoinPasswordProtector: mã hóa và giải mã chuẩn UTF-8 với nonce ngẫu nhiên duy nhất")]
    public void Protector_RoundTrip_ProducesUniqueCiphertextAndRestoresPlaintext()
    {
        const string secret = "LớpHọc#2026!TiếngViệt";

        var first = _protector.Protect($"  {secret}  ");
        var second = _protector.Protect(secret);

        first.Should().NotBeNullOrWhiteSpace();
        second.Should().NotBeNullOrWhiteSpace();
        first.Should().StartWith("aes256gcm.v1.");
        first.Should().NotContain(secret);
        first.Should().NotBe(second);

        _protector.Unprotect(first).Should().Be(secret);
        _protector.Unprotect(second).Should().Be(secret);
    }

    [Fact(DisplayName = "2. AesGcmClassJoinPasswordProtector: xử lý null/empty và từ chối ciphertext bị sửa hoặc sai khóa")]
    public void Protector_HandlesNullTamperedAndWrongKeySafely()
    {
        _protector.Protect(null).Should().BeNull();
        _protector.Protect("   ").Should().BeNull();
        _protector.Unprotect(null).Should().BeNull();
        _protector.Unprotect("   ").Should().BeNull();
        _protector.Unprotect("invalid-format").Should().BeNull();

        var protectedValue = _protector.Protect("ClassPass#2026")!;
        var parts = protectedValue.Split('.');
        parts.Should().HaveCount(5);

        // Bit-flip 16-byte auth tag in-place so length stays 16 bytes and AesGcm.Decrypt enforces tag verification
        var tagBytes = Convert.FromBase64String(parts[3]);
        tagBytes.Should().HaveCount(16);
        tagBytes[0] ^= 0xFF;
        var tamperedTag = $"{parts[0]}.{parts[1]}.{parts[2]}.{Convert.ToBase64String(tagBytes)}.{parts[4]}";
        _protector.Unprotect(tamperedTag).Should().BeNull();

        // Bit-flip ciphertext in-place preserving exact byte length
        var cipherBytes = Convert.FromBase64String(parts[4]);
        cipherBytes[0] ^= 0xFF;
        var tamperedCipher = $"{parts[0]}.{parts[1]}.{parts[2]}.{parts[3]}.{Convert.ToBase64String(cipherBytes)}";
        _protector.Unprotect(tamperedCipher).Should().BeNull();

        // Different master key
        var alternateProtector = new AesGcmClassJoinPasswordProtector(AlternateMasterSecret);
        alternateProtector.Unprotect(protectedValue).Should().BeNull();
    }

    [Fact(DisplayName = "2b. AesGcmClassJoinPasswordProtector(IConfiguration): ưu tiên Security:ClassJoinPasswordKey > CLASS_JOIN_PASSWORD_KEY > Authentication:JwtSigningKey")]
    public void Protector_ConfigurationKeyPrecedence_PrefersDedicatedClassJoinKeyOverJwtKey()
    {
        var dedicatedConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:ClassJoinPasswordKey"] = TestMasterSecret,
                ["CLASS_JOIN_PASSWORD_KEY"] = AlternateMasterSecret,
                ["Authentication:JwtSigningKey"] = AlternateMasterSecret
            })
            .Build();
        var envFallbackConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CLASS_JOIN_PASSWORD_KEY"] = TestMasterSecret,
                ["Authentication:JwtSigningKey"] = AlternateMasterSecret
            })
            .Build();
        var jwtOnlyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtSigningKey"] = AlternateMasterSecret
            })
            .Build();

        var fromDedicated = new AesGcmClassJoinPasswordProtector(dedicatedConfig);
        var fromEnv = new AesGcmClassJoinPasswordProtector(envFallbackConfig);
        var fromJwt = new AesGcmClassJoinPasswordProtector(jwtOnlyConfig);

        var cipher = fromDedicated.Protect("PriorityKey#2026")!;
        fromEnv.Unprotect(cipher).Should().Be("PriorityKey#2026");
        fromJwt.Unprotect(cipher).Should().BeNull();
    }

    [Theory(DisplayName = "2c. AesGcmClassJoinPasswordProtector(IConfiguration): Security:ClassJoinPasswordKey rỗng hoặc whitespace fallback đúng sang CLASS_JOIN_PASSWORD_KEY")]
    [InlineData("")]
    [InlineData("   ")]
    public void Protector_WhenExplicitConfigKeyIsBlankOrWhitespace_FallsBackToEnvVarBeforeJwtKey(string blankExplicitKey)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:ClassJoinPasswordKey"] = blankExplicitKey,
                ["CLASS_JOIN_PASSWORD_KEY"] = TestMasterSecret,
                ["Authentication:JwtSigningKey"] = AlternateMasterSecret
            })
            .Build();

        var protectorFromConfig = new AesGcmClassJoinPasswordProtector(config);
        var cipher = _protector.Protect("EnvFallback#2026")!;

        protectorFromConfig.Unprotect(cipher).Should().Be("EnvFallback#2026");

        var jwtOnlyProtector = new AesGcmClassJoinPasswordProtector(AlternateMasterSecret);
        jwtOnlyProtector.Unprotect(protectorFromConfig.Protect("VerifyEnvKey#2026")).Should().BeNull();
    }

    [Fact(DisplayName = "3. AesGcmClassJoinPasswordProtector: mật khẩu 128 ký tự UTF-8 đa byte không vượt cột VARCHAR(1024)")]
    public void Protector_MaxLengthMultiByteUtf8Password_FitsColumnAndRoundTrips()
    {
        var maxUtf8Password = new string('ệ', 128);

        var protectedValue = _protector.Protect(maxUtf8Password);

        protectedValue.Should().NotBeNullOrWhiteSpace();
        protectedValue!.Length.Should().BeLessThanOrEqualTo(1024);
        _protector.Unprotect(protectedValue).Should().Be(maxUtf8Password);
    }

    [Fact(DisplayName = "4. AesGcmClassJoinPasswordProtector: từ chối khóa bí mật ngắn hơn 32 byte")]
    public void Protector_WhenMasterSecretTooShort_ThrowsInvalidOperationException()
    {
        Action actEmpty = () => _ = new AesGcmClassJoinPasswordProtector("");
        Action actShort = () => _ = new AesGcmClassJoinPasswordProtector("too-short-secret");

        actEmpty.Should().Throw<InvalidOperationException>();
        actShort.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "5. CreateClass + GetClassDetail: lưu bản mã hóa và chỉ giải mã cho GV sở hữu hoặc Trưởng BM sở hữu")]
    public async Task CreateAndGetClassDetail_ProtectsAndRevealsJoinPasswordStrictlyByOwnership()
    {
        await using var context = CreateDbContext();
        var (semester, course, offering, ownerHead, otherHead, ownerLecturer, student) = SeedAcademicGraph(context);

        var createHandler = new CreateClassCommandHandler(
            context,
            _passwordHasher,
            new CreateClassCommandValidator(),
            _protector);

        var created = await createHandler.Handle(new CreateClassCommand(
            Code: "SE1809-PRN",
            Name: "Lớp SE1809 PRN231",
            CourseId: course.Id,
            SemesterId: semester.Id,
            LecturerId: ownerLecturer.Id,
            Password: "ClassSecret#2026",
            MaxStudents: 35,
            AllowSelfEnrollment: true,
            IsJoinEnabled: true,
            ActorRole: UserRole.Lecturer,
            CourseOfferingId: offering.Id,
            ActorUserId: ownerLecturer.Id), CancellationToken.None);

        created.IsSuccess.Should().BeTrue();
        created.Value.JoinPassword.Should().Be("ClassSecret#2026");

        var saved = await context.Classes.SingleAsync(c => c.Id == created.Value.Id);
        saved.JoinPasswordHash.Should().NotBeNullOrWhiteSpace();
        _passwordHasher.Verify("ClassSecret#2026", saved.JoinPasswordHash!).Should().BeTrue();
        saved.JoinPasswordProtected.Should().NotBeNullOrWhiteSpace();
        saved.JoinPasswordProtected.Should().NotContain("ClassSecret#2026");

        // Enroll student to ensure even enrolled students NEVER receive JoinPassword
        context.ClassEnrollments.Add(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = saved.Id,
            StudentId = student.Id,
            Status = "enrolled",
            EnrolledAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var detailHandler = new GetClassDetailQueryHandler(context, _protector);

        // Owner lecturer receives decrypted JoinPassword
        var lecturerDetail = await detailHandler.Handle(
            new GetClassDetailQuery(saved.Id, ownerLecturer.Id, UserRole.Lecturer),
            CancellationToken.None);
        lecturerDetail.IsSuccess.Should().BeTrue();
        lecturerDetail.Value.HasPassword.Should().BeTrue();
        lecturerDetail.Value.JoinPassword.Should().Be("ClassSecret#2026");

        // Owning department head receives decrypted JoinPassword
        var ownerHeadDetail = await detailHandler.Handle(
            new GetClassDetailQuery(saved.Id, ownerHead.Id, UserRole.DepartmentHead),
            CancellationToken.None);
        ownerHeadDetail.IsSuccess.Should().BeTrue();
        ownerHeadDetail.Value.JoinPassword.Should().Be("ClassSecret#2026");

        // Non-owning department head is strictly blocked (fail-closed anti-BOLA)
        var otherHeadDetail = await detailHandler.Handle(
            new GetClassDetailQuery(saved.Id, otherHead.Id, UserRole.DepartmentHead),
            CancellationToken.None);
        otherHeadDetail.IsSuccess.Should().BeFalse();
        otherHeadDetail.Error.Should().StartWith("FORBIDDEN:");

        // Enrolled student NEVER receives JoinPassword
        var studentDetail = await detailHandler.Handle(
            new GetClassDetailQuery(saved.Id, student.Id, UserRole.Student),
            CancellationToken.None);
        studentDetail.IsSuccess.Should().BeTrue();
        studentDetail.Value.HasPassword.Should().BeTrue();
        studentDetail.Value.JoinPassword.Should().BeNull();
    }

    [Fact(DisplayName = "6. GetClassDetail: Trưởng BM không được xem mật khẩu khi môn chưa gán DepartmentHead (fail-closed)")]
    public async Task GetClassDetail_WhenCourseHasNoDepartmentHead_DoesNotLeakJoinPasswordToDepartmentHead()
    {
        await using var context = CreateDbContext();
        var (semester, _, _, _, otherHead, ownerLecturer, _) = SeedAcademicGraph(context);

        var unassignedCourse = new Course
        {
            Id = Guid.NewGuid(),
            Code = "UNASSIGNED101",
            Name = "Unassigned Course",
            SemesterId = semester.Id,
            DepartmentHeadUserId = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "CLS-UNASSIGNED",
            Name = "Lớp chưa có Trưởng BM",
            JoinCode = "UNAS0001",
            JoinPasswordHash = _passwordHasher.Hash("HiddenPass#2026"),
            JoinPasswordProtected = _protector.Protect("HiddenPass#2026"),
            CourseId = unassignedCourse.Id,
            SemesterId = semester.Id,
            LecturerId = ownerLecturer.Id,
            MaxStudents = 30,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Courses.Add(unassignedCourse);
        context.Classes.Add(targetClass);
        await context.SaveChangesAsync();

        var detailHandler = new GetClassDetailQueryHandler(context, _protector);
        var headResult = await detailHandler.Handle(
            new GetClassDetailQuery(targetClass.Id, otherHead.Id, UserRole.DepartmentHead),
            CancellationToken.None);

        headResult.IsSuccess.Should().BeFalse();
        headResult.Error.Should().StartWith("FORBIDDEN:");
    }

    [Fact(DisplayName = "6b. GetClassDetail: cách ly Shifts, EnrolledStudents và JoinPassword giữa Trưởng BM SE và Trưởng BM ENT")]
    public async Task GetClassDetail_IsolatesDepartmentHeadPrograms_HidesShiftsRosterAndJoinPasswordFromOtherProgramHead()
    {
        await using var context = CreateDbContext();
        var (semester, seCourse, offering, seHead, entHead, ownerLecturer, student) = SeedAcademicGraph(context);

        var entProgram = new AcademicProgram
        {
            Id = Guid.NewGuid(),
            Code = "ENT",
            Name = "English & Entrepreneurship",
            DepartmentHeadUserId = entHead.Id,
            IsActive = true
        };
        context.AcademicPrograms.Add(entProgram);

        var seClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1811-PRN",
            Name = "Lớp SE1811 PRN231",
            JoinCode = "SE181101",
            JoinPasswordHash = _passwordHasher.Hash("ClassSecret#2026"),
            JoinPasswordProtected = _protector.Protect("ClassSecret#2026"),
            CourseId = seCourse.Id,
            SemesterId = semester.Id,
            CourseOfferingId = offering.Id,
            LecturerId = ownerLecturer.Id,
            MaxStudents = 30,
            AllowSelfEnrollment = true,
            IsJoinEnabled = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Classes.Add(seClass);
        context.ClassEnrollments.Add(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = seClass.Id,
            StudentId = student.Id,
            Status = "enrolled",
            JoinMethod = "JOIN_CODE",
            EnrolledAt = DateTime.UtcNow
        });
        context.InClassExamShifts.Add(new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = seClass.Id,
            ShiftName = "SE1811 Progress Test 1",
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddHours(1),
            TotpSecretKey = "12345678901234567890123456789012",
            Status = "SCHEDULED",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var detailHandler = new GetClassDetailQueryHandler(context, _protector);

        var seHeadResult = await detailHandler.Handle(
            new GetClassDetailQuery(seClass.Id, seHead.Id, UserRole.DepartmentHead),
            CancellationToken.None);
        seHeadResult.IsSuccess.Should().BeTrue();
        seHeadResult.Value.Shifts.Should().ContainSingle(s => s.ShiftName == "SE1811 Progress Test 1");
        seHeadResult.Value.EnrolledStudents.Should().ContainSingle(s => s.StudentId == student.Id);
        seHeadResult.Value.JoinPassword.Should().Be("ClassSecret#2026");

        var entHeadResult = await detailHandler.Handle(
            new GetClassDetailQuery(seClass.Id, entHead.Id, UserRole.DepartmentHead),
            CancellationToken.None);
        entHeadResult.IsSuccess.Should().BeFalse();
        entHeadResult.Error.Should().StartWith("FORBIDDEN:");
    }

    [Fact(DisplayName = "7. ResetClassPassword: cập nhật JoinPasswordProtected khi đổi mật khẩu và xóa sạch khi gỡ mật khẩu")]
    public async Task ResetClassPassword_UpdatesAndClearsJoinPasswordProtectedInSyncWithHash()
    {
        await using var context = CreateDbContext();
        var (semester, course, _, _, _, ownerLecturer, _) = SeedAcademicGraph(context);

        var legacyClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "CLS-LEGACY",
            Name = "Lớp legacy hash-only",
            JoinCode = "LEGC0001",
            JoinPasswordHash = _passwordHasher.Hash("OldPass#123"),
            JoinPasswordProtected = null,
            CourseId = course.Id,
            SemesterId = semester.Id,
            CourseOfferingId = null,
            LecturerId = ownerLecturer.Id,
            MaxStudents = 30,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Classes.Add(legacyClass);
        await context.SaveChangesAsync();

        var detailHandler = new GetClassDetailQueryHandler(context, _protector);
        var resetHandler = new ResetClassPasswordCommandHandler(
            context,
            _passwordHasher,
            new ResetClassPasswordCommandValidator(),
            _protector);

        // Before reset: legacy class has HasPassword=true and JoinPassword=null
        var beforeDetail = await detailHandler.Handle(
            new GetClassDetailQuery(legacyClass.Id, ownerLecturer.Id, UserRole.Lecturer),
            CancellationToken.None);
        beforeDetail.Value.HasPassword.Should().BeTrue();
        beforeDetail.Value.JoinPassword.Should().BeNull();

        // Reset to a new password -> protected copy persisted and revealable
        var updated = await resetHandler.Handle(new ResetClassPasswordCommand(
            ClassId: legacyClass.Id,
            ActorUserId: ownerLecturer.Id,
            NewPassword: "RotatedPass#2026"), CancellationToken.None);
        updated.IsSuccess.Should().BeTrue();
        updated.Value.JoinPassword.Should().Be("RotatedPass#2026");

        var afterResetDetail = await detailHandler.Handle(
            new GetClassDetailQuery(legacyClass.Id, ownerLecturer.Id, UserRole.Lecturer),
            CancellationToken.None);
        afterResetDetail.Value.JoinPassword.Should().Be("RotatedPass#2026");

        // Clear password on non-offering class -> both hash and protected copy cleared
        var cleared = await resetHandler.Handle(new ResetClassPasswordCommand(
            ClassId: legacyClass.Id,
            ActorUserId: ownerLecturer.Id,
            NewPassword: ""), CancellationToken.None);
        cleared.IsSuccess.Should().BeTrue();
        cleared.Value.HasPassword.Should().BeFalse();

        var reloaded = await context.Classes.AsNoTracking().SingleAsync(c => c.Id == legacyClass.Id);
        reloaded.JoinPasswordHash.Should().BeNull();
        reloaded.JoinPasswordProtected.Should().BeNull();
    }

    [Fact(DisplayName = "8. CreateClassCommandValidator: chặn mật khẩu tham gia vượt quá 128 ký tự")]
    public async Task CreateClassValidator_RejectsPasswordLongerThan128Characters()
    {
        var validator = new CreateClassCommandValidator();
        var result = await validator.ValidateAsync(new CreateClassCommand(
            Code: "SE1810-PRN",
            Name: "Lớp dài",
            CourseId: Guid.NewGuid(),
            SemesterId: Guid.NewGuid(),
            LecturerId: Guid.NewGuid(),
            Password: new string('A', 129)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateClassCommand.Password));
    }

    private static (
        Semester semester,
        Course course,
        CourseOffering offering,
        User ownerHead,
        User otherHead,
        User ownerLecturer,
        User student) SeedAcademicGraph(OralExamDbContext context)
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
        var ownerHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "head.se@fpt.edu.vn",
            FullName = "Owner Head",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };
        var otherHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "head.ai@fpt.edu.vn",
            FullName = "Other Head",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };
        var ownerLecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer.se@fpt.edu.vn",
            FullName = "Owner Lecturer",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.se@fpt.edu.vn",
            FullName = "Student SE",
            StudentCode = "SE180123",
            Role = UserRole.Student,
            IsActive = true
        };
        var program = new AcademicProgram
        {
            Id = Guid.NewGuid(),
            Code = "SE",
            Name = "Software Engineering",
            DepartmentHeadUserId = ownerHead.Id,
            IsActive = true
        };
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Cross-Platform .NET",
            AcademicProgramId = program.Id,
            DepartmentHeadUserId = ownerHead.Id,
            SemesterId = semester.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var offering = new CourseOffering
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SemesterId = semester.Id,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var assignment = new CourseLecturer
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CourseOfferingId = offering.Id,
            LecturerUserId = ownerLecturer.Id,
            AssignedByUserId = ownerHead.Id,
            SemesterId = semester.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Semesters.Add(semester);
        context.Users.AddRange(ownerHead, otherHead, ownerLecturer, student);
        context.AcademicPrograms.Add(program);
        context.Courses.Add(course);
        context.CourseOfferings.Add(offering);
        context.CourseLecturers.Add(assignment);
        context.SaveChanges();

        return (semester, course, offering, ownerHead, otherHead, ownerLecturer, student);
    }
}
