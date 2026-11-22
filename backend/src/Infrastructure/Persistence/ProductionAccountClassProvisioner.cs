using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Infrastructure.Persistence;

public sealed record ProductionProvisionResult(
    int UsersCreated,
    int UsersUpdated,
    int CourseLecturersUpserted,
    int ClassesUpserted,
    int EnrollmentsCreated);

/// <summary>
/// Nạp tài khoản kiểm thử, phân công giảng viên, lớp học (kèm JoinPasswordHash và JoinPasswordProtected AES-GCM)
/// và ghi danh sinh viên cho môi trường Production VPS từ tệp JSON hoặc biến môi trường bên ngoài.
/// Không chứa bất kỳ mật khẩu mặc định nào trong mã nguồn.
/// </summary>
public static class ProductionAccountClassProvisioner
{
    private const int AdvisoryLockKey = 627418503;
    private const int MinUserPasswordLength = 8;
    private const int MinClassJoinPasswordLength = 6;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<ProductionProvisionResult> ProvisionAsync(
        OralExamDbContext context,
        IPasswordHasher passwordHasher,
        IClassJoinPasswordProtector classJoinPasswordProtector,
        string configJson,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(classJoinPasswordProtector);

        if (string.IsNullOrWhiteSpace(configJson))
        {
            throw new InvalidOperationException("[FAIL CLOSED] Provisioning configuration JSON is empty.");
        }

        ProductionProvisionConfig payload;
        try
        {
            payload = JsonSerializer.Deserialize<ProductionProvisionConfig>(configJson, JsonOptions)
                ?? throw new InvalidOperationException("[FAIL CLOSED] Provisioning configuration JSON deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("[FAIL CLOSED] Provisioning configuration JSON is malformed.", ex);
        }

        ValidatePayload(payload);

        ProductionProvisionResult? result = null;

        await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = context.Database.IsRelational()
                ? await context.Database.BeginTransactionAsync(cancellationToken)
                : null;

            if (context.Database.IsNpgsql())
            {
                await context.Database.ExecuteSqlRawAsync(
                    $"SELECT pg_advisory_xact_lock({AdvisoryLockKey})",
                    cancellationToken);
            }

            result = await ExecuteProvisionCoreAsync(
                context,
                passwordHasher,
                classJoinPasswordProtector,
                payload,
                logger,
                cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        });

        return result!;
    }

    private static async Task<ProductionProvisionResult> ExecuteProvisionCoreAsync(
        OralExamDbContext context,
        IPasswordHasher passwordHasher,
        IClassJoinPasswordProtector classJoinPasswordProtector,
        ProductionProvisionConfig payload,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var semesterCode = string.IsNullOrWhiteSpace(payload.SemesterCode) ? "FA26" : payload.SemesterCode.Trim();
        var programCode = string.IsNullOrWhiteSpace(payload.AcademicProgramCode) ? "SE" : payload.AcademicProgramCode.Trim();

        var semester = await context.Semesters
            .FirstOrDefaultAsync(s => s.Code.ToLower() == semesterCode.ToLower(), cancellationToken)
            ?? throw new InvalidOperationException($"[FAIL CLOSED] Semester '{semesterCode}' does not exist. Run --import-course-banks first.");

        var academicProgram = await context.AcademicPrograms
            .FirstOrDefaultAsync(p => p.Code.ToLower() == programCode.ToLower(), cancellationToken);

        var usersCreated = 0;
        var usersUpdated = 0;
        var courseLecturersUpserted = 0;
        var classesUpserted = 0;
        var enrollmentsCreated = 0;

        // 1. Upsert users (fail closed on cross-row collisions; never log plaintext passwords)
        var existingUsers = await context.Users.ToListAsync(cancellationToken);

        foreach (var userSpec in payload.Users)
        {
            var normalizedEmail = userSpec.Email.Trim().ToLowerInvariant();
            var trimmedUsername = userSpec.Username.Trim();
            var normalizedUsername = trimmedUsername.ToLowerInvariant();
            var trimmedStudentCode = string.IsNullOrWhiteSpace(userSpec.StudentCode) ? null : userSpec.StudentCode.Trim();
            var normalizedStudentCode = trimmedStudentCode?.ToLowerInvariant();
            var canonicalRole = NormalizeRole(userSpec.Role);

            var matchingUsers = existingUsers
                .Where(u =>
                    (userSpec.Id.HasValue && u.Id == userSpec.Id.Value) ||
                    string.Equals(u.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(u.Username) && string.Equals(u.Username, normalizedUsername, StringComparison.OrdinalIgnoreCase)) ||
                    (normalizedStudentCode is not null && !string.IsNullOrWhiteSpace(u.StudentCode) && string.Equals(u.StudentCode, normalizedStudentCode, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (matchingUsers.Count > 1)
            {
                throw new InvalidOperationException(
                    $"[FAIL CLOSED] Account '{trimmedUsername}' ({normalizedEmail}) collides with multiple existing user rows.");
            }

            if (matchingUsers.Count == 0)
            {
                var newUser = new User
                {
                    Id = userSpec.Id ?? Guid.NewGuid(),
                    Email = normalizedEmail,
                    Username = trimmedUsername,
                    FullName = userSpec.FullName.Trim(),
                    StudentCode = trimmedStudentCode,
                    PasswordHash = passwordHasher.Hash(userSpec.Password),
                    Role = canonicalRole,
                    MustChangePassword = userSpec.MustChangePassword,
                    IsActive = userSpec.IsActive,
                    AuthVersion = 1,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };
                context.Users.Add(newUser);
                existingUsers.Add(newUser);
                usersCreated++;
            }
            else
            {
                var existing = matchingUsers[0];
                var passwordMatches = !string.IsNullOrWhiteSpace(existing.PasswordHash)
                    && passwordHasher.Verify(userSpec.Password, existing.PasswordHash);
                var roleChanged = !string.Equals(existing.Role, canonicalRole, StringComparison.Ordinal);
                var activeChanged = existing.IsActive != userSpec.IsActive;
                var mustChangeChanged = existing.MustChangePassword != userSpec.MustChangePassword;

                if (!passwordMatches || roleChanged || activeChanged)
                {
                    existing.AuthVersion += 1;
                }

                if (!passwordMatches)
                {
                    existing.PasswordHash = passwordHasher.Hash(userSpec.Password);
                }

                existing.Email = normalizedEmail;
                existing.Username = trimmedUsername;
                existing.FullName = userSpec.FullName.Trim();
                existing.StudentCode = trimmedStudentCode ?? existing.StudentCode;
                existing.Role = canonicalRole;
                existing.MustChangePassword = userSpec.MustChangePassword;
                existing.IsActive = userSpec.IsActive;
                existing.UpdatedAt = nowUtc;

                if (!passwordMatches || roleChanged || activeChanged || mustChangeChanged)
                {
                    usersUpdated++;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        var usersByUsername = existingUsers
            .Where(u => !string.IsNullOrWhiteSpace(u.Username))
            .ToDictionary(u => u.Username!.Trim(), u => u, StringComparer.OrdinalIgnoreCase);

        // 2. Resolve DepartmentHead & assign to AcademicProgram + active courses
        User? departmentHead = null;
        if (!string.IsNullOrWhiteSpace(payload.DepartmentHeadUsername))
        {
            if (!usersByUsername.TryGetValue(payload.DepartmentHeadUsername.Trim(), out departmentHead)
                || !departmentHead.IsActive
                || !string.Equals(departmentHead.Role, UserRole.DepartmentHead, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"[FAIL CLOSED] DepartmentHead '{payload.DepartmentHeadUsername}' was not found or is not an active department_head.");
            }
        }
        else
        {
            departmentHead = existingUsers
                .Where(u => u.IsActive && string.Equals(u.Role, UserRole.DepartmentHead, StringComparison.Ordinal))
                .OrderBy(u => u.CreatedAt)
                .FirstOrDefault();
        }

        var allCourses = await context.Courses.ToListAsync(cancellationToken);
        var coursesByCode = allCourses.ToDictionary(c => c.Code.Trim(), c => c, StringComparer.OrdinalIgnoreCase);

        if (payload.AssignProgramAndActiveCoursesDepartmentHead && departmentHead is not null)
        {
            if (academicProgram is not null)
            {
                academicProgram.DepartmentHeadUserId = departmentHead.Id;
                academicProgram.IsActive = true;
                academicProgram.UpdatedAt = nowUtc;
            }

            foreach (var activeCourse in allCourses.Where(c => c.IsActive))
            {
                activeCourse.DepartmentHeadUserId = departmentHead.Id;
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        // Load offerings for target semester
        var offeringsByCourseId = (await context.CourseOfferings
            .Where(o => o.SemesterId == semester.Id)
            .ToListAsync(cancellationToken))
            .ToDictionary(o => o.CourseId, o => o);

        // 3. Assign CourseLecturers (with CourseOfferingId)
        var fallbackAssignerId = departmentHead?.Id
            ?? existingUsers
                .Where(u => u.IsActive && string.Equals(u.Role, UserRole.Admin, StringComparison.Ordinal))
                .OrderBy(u => u.CreatedAt)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefault();

        var existingLecturers = await context.CourseLecturers.ToListAsync(cancellationToken);
        foreach (var assignmentSpec in payload.CourseLecturerAssignments)
        {
            if (fallbackAssignerId is null)
            {
                throw new InvalidOperationException(
                    "[FAIL CLOSED] Cannot assign course lecturers without an active DepartmentHead or Admin user for AssignedByUserId.");
            }

            if (!usersByUsername.TryGetValue(assignmentSpec.LecturerUsername.Trim(), out var lecturer)
                || !lecturer.IsActive
                || (lecturer.Role != UserRole.Lecturer && lecturer.Role != UserRole.DepartmentHead))
            {
                throw new InvalidOperationException(
                    $"[FAIL CLOSED] Lecturer '{assignmentSpec.LecturerUsername}' was not found or is not an active lecturer.");
            }

            foreach (var rawCourseCode in assignmentSpec.CourseCodes)
            {
                var courseCode = rawCourseCode.Trim();
                if (!coursesByCode.TryGetValue(courseCode, out var course) || !course.IsActive)
                {
                    throw new InvalidOperationException(
                        $"[FAIL CLOSED] Cannot assign lecturer '{lecturer.Username}' to missing or inactive course '{courseCode}'.");
                }

                if (!offeringsByCourseId.TryGetValue(course.Id, out var offering) || !offering.IsEnabled)
                {
                    throw new InvalidOperationException(
                        $"[FAIL CLOSED] Course '{courseCode}' has no enabled CourseOffering in semester '{semester.Code}'.");
                }

                var existingAssignment = existingLecturers.FirstOrDefault(cl =>
                    cl.CourseId == course.Id && cl.LecturerUserId == lecturer.Id);

                if (existingAssignment is null)
                {
                    var newAssignment = new CourseLecturer
                    {
                        Id = Guid.NewGuid(),
                        CourseId = course.Id,
                        CourseOfferingId = offering.Id,
                        LecturerUserId = lecturer.Id,
                        AssignedByUserId = fallbackAssignerId.Value,
                        SemesterId = semester.Id,
                        AssignedAt = nowUtc,
                        IsActive = true,
                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc
                    };
                    context.CourseLecturers.Add(newAssignment);
                    existingLecturers.Add(newAssignment);
                }
                else
                {
                    existingAssignment.CourseOfferingId = offering.Id;
                    existingAssignment.SemesterId = semester.Id;
                    if (existingAssignment.AssignedByUserId == Guid.Empty)
                    {
                        existingAssignment.AssignedByUserId = fallbackAssignerId.Value;
                    }
                    existingAssignment.IsActive = true;
                    existingAssignment.UpdatedAt = nowUtc;
                }

                courseLecturersUpserted++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        // 4. Upsert Classes (only on active courses; set JoinPasswordHash + JoinPasswordProtected)
        var existingClasses = await context.Classes.ToListAsync(cancellationToken);
        foreach (var classSpec in payload.Classes)
        {
            var courseCode = classSpec.CourseCode.Trim();
            if (!coursesByCode.TryGetValue(courseCode, out var course) || !course.IsActive)
            {
                throw new InvalidOperationException(
                    $"[FAIL CLOSED] Cannot provision class '{classSpec.Code}' on missing or inactive course '{courseCode}'.");
            }

            if (!offeringsByCourseId.TryGetValue(course.Id, out var offering) || !offering.IsEnabled)
            {
                throw new InvalidOperationException(
                    $"[FAIL CLOSED] Course '{courseCode}' has no enabled CourseOffering in semester '{semester.Code}'.");
            }

            if (!usersByUsername.TryGetValue(classSpec.LecturerUsername.Trim(), out var lecturer)
                || !lecturer.IsActive
                || (lecturer.Role != UserRole.Lecturer && lecturer.Role != UserRole.DepartmentHead))
            {
                throw new InvalidOperationException(
                    $"[FAIL CLOSED] Class '{classSpec.Code}' references invalid lecturer '{classSpec.LecturerUsername}'.");
            }

            var classCode = classSpec.Code.Trim();
            var joinCode = string.IsNullOrWhiteSpace(classSpec.JoinCode)
                ? classCode.Replace("-", string.Empty).ToUpperInvariant()
                : classSpec.JoinCode.Trim().ToUpperInvariant();
            var className = string.IsNullOrWhiteSpace(classSpec.Name) ? $"Lớp {classCode}" : classSpec.Name.Trim();

            var joinPasswordHash = passwordHasher.Hash(classSpec.JoinPassword);
            var joinPasswordProtected = classJoinPasswordProtector.Protect(classSpec.JoinPassword)
                ?? throw new InvalidOperationException($"[FAIL CLOSED] Failed to encrypt join password for class '{classCode}'.");

            var existingClass = existingClasses.FirstOrDefault(c =>
                (classSpec.Id.HasValue && c.Id == classSpec.Id.Value) ||
                string.Equals(c.Code, classCode, StringComparison.OrdinalIgnoreCase));

            if (existingClass is null)
            {
                var newClass = new Class
                {
                    Id = classSpec.Id ?? Guid.NewGuid(),
                    Code = classCode,
                    Name = className,
                    JoinCode = joinCode,
                    CourseId = course.Id,
                    CourseOfferingId = offering.Id,
                    SemesterId = semester.Id,
                    LecturerId = lecturer.Id,
                    MaxStudents = classSpec.MaxStudents is > 0 ? classSpec.MaxStudents.Value : 40,
                    JoinPasswordHash = joinPasswordHash,
                    JoinPasswordProtected = joinPasswordProtected,
                    IsJoinEnabled = classSpec.IsJoinEnabled,
                    AllowSelfEnrollment = classSpec.AllowSelfEnrollment,
                    IsActive = true,
                    CreatedAt = nowUtc
                };
                context.Classes.Add(newClass);
                existingClasses.Add(newClass);
            }
            else
            {
                existingClass.Name = className;
                existingClass.JoinCode = joinCode;
                existingClass.CourseId = course.Id;
                existingClass.CourseOfferingId = offering.Id;
                existingClass.SemesterId = semester.Id;
                existingClass.LecturerId = lecturer.Id;
                existingClass.MaxStudents = classSpec.MaxStudents is > 0 ? classSpec.MaxStudents.Value : existingClass.MaxStudents;
                existingClass.JoinPasswordHash = joinPasswordHash;
                existingClass.JoinPasswordProtected = joinPasswordProtected;
                existingClass.IsJoinEnabled = classSpec.IsJoinEnabled;
                existingClass.AllowSelfEnrollment = classSpec.AllowSelfEnrollment;
                existingClass.IsActive = true;
            }

            classesUpserted++;
        }

        await context.SaveChangesAsync(cancellationToken);

        // 5. Upsert ClassEnrollments (e.g. Group-2A into 10 classes; leave Group-2B unenrolled)
        var classesByCode = existingClasses.ToDictionary(c => c.Code.Trim(), c => c, StringComparer.OrdinalIgnoreCase);
        var existingEnrollments = await context.ClassEnrollments.ToListAsync(cancellationToken);

        foreach (var enrollmentSpec in payload.Enrollments)
        {
            var joinMethod = string.IsNullOrWhiteSpace(enrollmentSpec.JoinMethod) ? "assigned" : enrollmentSpec.JoinMethod.Trim();
            foreach (var rawStudentUsername in enrollmentSpec.StudentUsernames)
            {
                var studentUsername = rawStudentUsername.Trim();
                if (!usersByUsername.TryGetValue(studentUsername, out var student)
                    || !student.IsActive
                    || !string.Equals(student.Role, UserRole.Student, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"[FAIL CLOSED] Enrollment student '{studentUsername}' was not found or is not an active student.");
                }

                foreach (var rawClassCode in enrollmentSpec.ClassCodes)
                {
                    var classCode = rawClassCode.Trim();
                    if (!classesByCode.TryGetValue(classCode, out var cls) || !cls.IsActive)
                    {
                        throw new InvalidOperationException(
                            $"[FAIL CLOSED] Enrollment target class '{classCode}' was not found or is inactive.");
                    }

                    var existingEnrollment = existingEnrollments.FirstOrDefault(e =>
                        e.ClassId == cls.Id && e.StudentId == student.Id);

                    if (existingEnrollment is null)
                    {
                        var enrollment = new ClassEnrollment
                        {
                            Id = Guid.NewGuid(),
                            ClassId = cls.Id,
                            StudentId = student.Id,
                            JoinMethod = joinMethod,
                            JoinedViaCode = string.Equals(joinMethod, "code", StringComparison.OrdinalIgnoreCase),
                            Status = "enrolled",
                            EnrolledAt = nowUtc
                        };
                        context.ClassEnrollments.Add(enrollment);
                        existingEnrollments.Add(enrollment);
                        enrollmentsCreated++;
                    }
                    else if (!string.Equals(existingEnrollment.Status, "enrolled", StringComparison.OrdinalIgnoreCase))
                    {
                        existingEnrollment.Status = "enrolled";
                        existingEnrollment.JoinMethod = joinMethod;
                    }
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        logger?.LogInformation(
            "Production account/class provisioning completed (UsersCreated={UsersCreated}, UsersUpdated={UsersUpdated}, CourseLecturers={CourseLecturers}, Classes={Classes}, EnrollmentsCreated={EnrollmentsCreated}).",
            usersCreated,
            usersUpdated,
            courseLecturersUpserted,
            classesUpserted,
            enrollmentsCreated);

        return new ProductionProvisionResult(
            usersCreated,
            usersUpdated,
            courseLecturersUpserted,
            classesUpserted,
            enrollmentsCreated);
    }

    private static void ValidatePayload(ProductionProvisionConfig payload)
    {
        if (payload.Users is null)
        {
            throw new InvalidOperationException("[FAIL CLOSED] Provisioning field 'users' cannot be null.");
        }

        if (payload.CourseLecturerAssignments is null)
        {
            throw new InvalidOperationException("[FAIL CLOSED] Provisioning field 'courseLecturerAssignments' cannot be null.");
        }

        if (payload.Classes is null)
        {
            throw new InvalidOperationException("[FAIL CLOSED] Provisioning field 'classes' cannot be null.");
        }

        if (payload.Enrollments is null)
        {
            throw new InvalidOperationException("[FAIL CLOSED] Provisioning field 'enrollments' cannot be null.");
        }

        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenStudentCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenClassCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var u in payload.Users)
        {
            if (u is null)
            {
                throw new InvalidOperationException("[FAIL CLOSED] User entry in 'users' cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(u.Email) ||
                string.IsNullOrWhiteSpace(u.Username) ||
                string.IsNullOrWhiteSpace(u.FullName) ||
                string.IsNullOrWhiteSpace(u.Role))
            {
                throw new InvalidOperationException("[FAIL CLOSED] User entry must specify email, username, fullName, and role.");
            }

            if (string.IsNullOrWhiteSpace(u.Password) ||
                !string.Equals(u.Password, u.Password.Trim(), StringComparison.Ordinal) ||
                u.Password.Length < MinUserPasswordLength)
            {
                throw new InvalidOperationException(
                    $"[FAIL CLOSED] Password for user '{u.Username}' must be at least {MinUserPasswordLength} characters with no leading/trailing whitespace.");
            }

            NormalizeRole(u.Role);

            if (!seenEmails.Add(u.Email.Trim()))
            {
                throw new InvalidOperationException($"[FAIL CLOSED] Duplicate email '{u.Email}' in provisioning payload.");
            }

            if (!seenUsernames.Add(u.Username.Trim()))
            {
                throw new InvalidOperationException($"[FAIL CLOSED] Duplicate username '{u.Username}' in provisioning payload.");
            }

            if (!string.IsNullOrWhiteSpace(u.StudentCode) && !seenStudentCodes.Add(u.StudentCode.Trim()))
            {
                throw new InvalidOperationException($"[FAIL CLOSED] Duplicate studentCode '{u.StudentCode}' in provisioning payload.");
            }
        }

        foreach (var a in payload.CourseLecturerAssignments)
        {
            if (a is null)
            {
                throw new InvalidOperationException("[FAIL CLOSED] Entry in 'courseLecturerAssignments' cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(a.LecturerUsername))
            {
                throw new InvalidOperationException("[FAIL CLOSED] CourseLecturerAssignment entry must specify lecturerUsername.");
            }

            if (a.CourseCodes is null || a.CourseCodes.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException("[FAIL CLOSED] CourseLecturerAssignment 'courseCodes' cannot be null or contain empty entries.");
            }
        }

        foreach (var c in payload.Classes)
        {
            if (c is null)
            {
                throw new InvalidOperationException("[FAIL CLOSED] Class entry in 'classes' cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(c.Code) ||
                string.IsNullOrWhiteSpace(c.CourseCode) ||
                string.IsNullOrWhiteSpace(c.LecturerUsername))
            {
                throw new InvalidOperationException("[FAIL CLOSED] Class entry must specify code, courseCode, and lecturerUsername.");
            }

            if (string.IsNullOrWhiteSpace(c.JoinPassword) ||
                !string.Equals(c.JoinPassword, c.JoinPassword.Trim(), StringComparison.Ordinal) ||
                c.JoinPassword.Length < MinClassJoinPasswordLength)
            {
                throw new InvalidOperationException(
                    $"[FAIL CLOSED] JoinPassword for class '{c.Code}' must be at least {MinClassJoinPasswordLength} characters with no leading/trailing whitespace.");
            }

            if (!seenClassCodes.Add(c.Code.Trim()))
            {
                throw new InvalidOperationException($"[FAIL CLOSED] Duplicate class code '{c.Code}' in provisioning payload.");
            }
        }

        foreach (var e in payload.Enrollments)
        {
            if (e is null)
            {
                throw new InvalidOperationException("[FAIL CLOSED] Entry in 'enrollments' cannot be null.");
            }

            if (e.StudentUsernames is null || e.StudentUsernames.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException("[FAIL CLOSED] Enrollment 'studentUsernames' cannot be null or contain empty entries.");
            }

            if (e.ClassCodes is null || e.ClassCodes.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException("[FAIL CLOSED] Enrollment 'classCodes' cannot be null or contain empty entries.");
            }
        }
    }

    private static string NormalizeRole(string role)
    {
        var trimmed = role.Trim().ToLowerInvariant();
        if (!UserRole.All.Contains(trimmed, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"[FAIL CLOSED] Unsupported role '{role}'. Allowed roles: {string.Join(", ", UserRole.All)}.");
        }

        return trimmed;
    }

    private sealed class ProductionProvisionConfig
    {
        public string SemesterCode { get; set; } = "FA26";
        public string AcademicProgramCode { get; set; } = "SE";
        public string? DepartmentHeadUsername { get; set; }
        public bool AssignProgramAndActiveCoursesDepartmentHead { get; set; } = true;
        public List<ProvisionUserSpec> Users { get; set; } = [];
        public List<ProvisionCourseLecturerSpec> CourseLecturerAssignments { get; set; } = [];
        public List<ProvisionClassSpec> Classes { get; set; } = [];
        public List<ProvisionEnrollmentSpec> Enrollments { get; set; } = [];
    }

    private sealed class ProvisionUserSpec
    {
        public Guid? Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? StudentCode { get; set; }
        public string Password { get; set; } = string.Empty;
        public bool MustChangePassword { get; set; }
        public bool IsActive { get; set; } = true;
    }

    private sealed class ProvisionCourseLecturerSpec
    {
        public string LecturerUsername { get; set; } = string.Empty;
        public List<string> CourseCodes { get; set; } = [];
    }

    private sealed class ProvisionClassSpec
    {
        public Guid? Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? JoinCode { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string LecturerUsername { get; set; } = string.Empty;
        public string JoinPassword { get; set; } = string.Empty;
        public int? MaxStudents { get; set; }
        public bool AllowSelfEnrollment { get; set; } = true;
        public bool IsJoinEnabled { get; set; } = true;
    }

    private sealed class ProvisionEnrollmentSpec
    {
        public List<string> StudentUsernames { get; set; } = [];
        public List<string> ClassCodes { get; set; } = [];
        public string? JoinMethod { get; set; }
    }
}
