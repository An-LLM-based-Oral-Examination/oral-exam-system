using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// Bộ kiểm thử thực nghiệm đối kháng Milestone 1 (Empirical Challenger M1_2).
/// Thực thi độc lập bởi Challenger 2 (challenger_m1_2).
/// 
/// Trọng tâm thử thách đối kháng:
/// 1. Kiểm tra tính toàn vẹn của mô hình EF Core:
///    - Thuộc tính ExamInputMode trong thực thể Course: kiểu CLR string, giá trị mặc định VoiceWithTranscriptEdit.
///    - Cấu hình Fluent API trong OralExamDbContext: cột exam_input_mode, độ dài tối đa 30, IsRequired = true, DefaultValue = "VoiceWithTranscriptEdit".
/// 2. Kiểm tra tính tương thích giữa Course.cs và 01_schema.sql (bảng courses):
///    - Định nghĩa cột exam_input_mode VARCHAR(30) NOT NULL DEFAULT 'VoiceWithTranscriptEdit' ở cả 2 tệp schema.
///    - Ràng buộc CONSTRAINT ck_courses_input_mode CHECK (exam_input_mode IN ('VoiceOnly', 'VoiceWithTranscriptEdit', 'VoiceWithTranscriptEdit')).
///    - Khớp 100% giữa DomainEnums.ExamInputMode.All và SQL CHECK constraint.
/// 3. Kiểm thử ca biên Validator và Persistence:
///    - Khả năng lưu và đọc lại qua EF Core với cả 2 giá trị ExamInputMode.
///    - Chặn các giá trị lạ, sai hoa thường hoặc khoảng trắng qua UpdateCourseConfigurationCommandValidator.
/// </summary>
public class AdversarialMilestone1ModelIntegrityChallengerTests
{
    private readonly UpdateCourseConfigurationCommandValidator _validator = new();

    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    #region 1. EF Core Model Metadata & Configuration Integrity

    [Fact(DisplayName = "ADV-M1-01: EF Core Model Metadata của Course.ExamInputMode phải chuẩn xác (column: exam_input_mode, max_length: 30, default: VoiceWithTranscriptEdit, required: true)")]
    public void EFCore_Model_Must_Configure_ExamInputMode_Correctly()
    {
        using var context = CreateDbContext();
        var courseEntity = context.Model.FindEntityType(typeof(Course));
        courseEntity.Should().NotBeNull("Thực thể Course phải được đăng ký trong EF Core Model");

        var property = courseEntity!.FindProperty(nameof(Course.ExamInputMode));
        property.Should().NotBeNull("Thuộc tính ExamInputMode phải được ánh xạ trong EF Core Model");

        // 1. CLR Type
        property!.ClrType.Should().Be(typeof(string), "Thuộc tính ExamInputMode phải có kiểu CLR là string");

        // 2. Column Name
        property.GetColumnName().Should().Be("exam_input_mode", "Tên cột trong CSDL phải là exam_input_mode");

        // 3. Max Length
        property.GetMaxLength().Should().Be(30, "Độ dài tối đa của cột phải là 30 ký tự (VARCHAR(30))");

        // 4. Default Value
        property.GetDefaultValue().Should().Be("VoiceWithTranscriptEdit", "Giá trị mặc định phải là 'VoiceWithTranscriptEdit'");

        // 5. Required / Not Null
        property.IsNullable.Should().BeFalse("Thuộc tính ExamInputMode phải là NOT NULL (IsRequired)");
    }

    [Fact(DisplayName = "ADV-M1-02: Thực thể Course khởi tạo mặc định phải có ExamInputMode = VoiceWithTranscriptEdit")]
    public void Course_Instance_Default_ExamInputMode_Must_Be_VoiceWithTranscriptEdit()
    {
        var course = new Course();
        course.ExamInputMode.Should().Be(ExamInputMode.VoiceWithTranscriptEdit);
        course.ExamInputMode.Should().Be("VoiceWithTranscriptEdit");
    }

    [Fact(DisplayName = "ADV-M1-03: EF Core có thể lưu trữ và truy vấn Course với cả 2 giá trị ExamInputMode mà không lỗi")]
    public void EFCore_Must_Persist_And_Retrieve_All_Valid_ExamInputModes()
    {
        using var context = CreateDbContext();

        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26_M1",
            Name = "Fall 2026 M1",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };
        context.Semesters.Add(semester);

        var course1 = new Course
        {
            Id = Guid.NewGuid(),
            Code = "C1_VOICE_ONLY",
            Name = "Course Voice Only",
            Credits = 3,
            SemesterId = semester.Id,
            ExamInputMode = ExamInputMode.VoiceOnly
        };

        var course2 = new Course
        {
            Id = Guid.NewGuid(),
            Code = "C2_TRANSCRIPT_EDIT",
            Name = "Course Transcript Edit",
            Credits = 3,
            SemesterId = semester.Id,
            ExamInputMode = ExamInputMode.VoiceWithTranscriptEdit
        };

        context.Courses.AddRange(course1, course2);
        context.SaveChanges();

        var retrieved1 = context.Courses.FirstOrDefault(c => c.Code == "C1_VOICE_ONLY");
        retrieved1.Should().NotBeNull();
        retrieved1!.ExamInputMode.Should().Be("VoiceOnly");

        var retrieved2 = context.Courses.FirstOrDefault(c => c.Code == "C2_TRANSCRIPT_EDIT");
        retrieved2.Should().NotBeNull();
        retrieved2!.ExamInputMode.Should().Be("VoiceWithTranscriptEdit");
    }

    #endregion

    #region 2. SQL Schema Compatibility (01_schema.sql)

    [Fact(DisplayName = "ADV-M1-04: 01_schema.sql tại infra/postgres/init phải chứa đúng định nghĩa exam_input_mode và CHECK constraint")]
    public void Schema_Infra_Must_Contain_Correct_ExamInputMode_Definition()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        string? schemaPath = null;

        while (currentDir != null)
        {
            var candidate = Path.Combine(currentDir.FullName, "infra", "postgres", "init", "01_schema.sql");
            if (File.Exists(candidate))
            {
                schemaPath = candidate;
                break;
            }
            currentDir = currentDir.Parent;
        }

        schemaPath.Should().NotBeNull("Tệp infra/postgres/init/01_schema.sql phải tồn tại");
        var sql = File.ReadAllText(schemaPath!);

        sql.Should().Contain("exam_input_mode             VARCHAR(30) NOT NULL DEFAULT 'VoiceWithTranscriptEdit',");
        sql.Should().Contain("CONSTRAINT ck_courses_input_mode CHECK (exam_input_mode IN ('VoiceOnly', 'VoiceWithTranscriptEdit'))");
    }

    [Fact(DisplayName = "ADV-M1-05: 01_schema.sql tại 05_Source_Code/infra/postgres/init phải chứa đúng định nghĩa exam_input_mode và CHECK constraint")]
    public void Schema_SourceCode_Must_Contain_Correct_ExamInputMode_Definition()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        string? schemaPath = null;

        while (currentDir != null)
        {
            var candidate = Path.Combine(currentDir.FullName, "05_Source_Code", "infra", "postgres", "init", "01_schema.sql");
            if (File.Exists(candidate))
            {
                schemaPath = candidate;
                break;
            }
            currentDir = currentDir.Parent;
        }

        schemaPath.Should().NotBeNull("Tệp 05_Source_Code/infra/postgres/init/01_schema.sql phải tồn tại");
        var sql = File.ReadAllText(schemaPath!);

        sql.Should().Contain("exam_input_mode             VARCHAR(30) NOT NULL DEFAULT 'VoiceWithTranscriptEdit',");
        sql.Should().Contain("CONSTRAINT ck_courses_input_mode CHECK (exam_input_mode IN ('VoiceOnly', 'VoiceWithTranscriptEdit'))");
    }

    [Fact(DisplayName = "ADV-M1-06: DomainEnums.ExamInputMode phải khớp chính xác 100% với 2 giá trị trong SQL CHECK constraint")]
    public void DomainEnums_ExamInputMode_Must_Match_SQL_Check_Constraint()
    {
        ExamInputMode.VoiceOnly.Should().Be("VoiceOnly");
        ExamInputMode.VoiceWithTranscriptEdit.Should().Be("VoiceWithTranscriptEdit");

        ExamInputMode.All.Should().HaveCount(2);
        ExamInputMode.All.Should().BeEquivalentTo(new[]
        {
            "VoiceOnly",
            "VoiceWithTranscriptEdit"
        });
    }

    #endregion

    #region 3. Validator Edge Cases for ExamInputMode

    [Theory(DisplayName = "ADV-M1-07: Validator PHẢI chấp thuận khi ExamInputMode là null hoặc 1 trong 2 giá trị hợp lệ")]
    [InlineData(null)]
    [InlineData("VoiceOnly")]
    [InlineData("VoiceWithTranscriptEdit")]
    public void Validator_Must_Pass_For_Valid_ExamInputModes(string? inputMode)
    {
        var command = new UpdateCourseConfigurationCommand(
            CourseId: Guid.NewGuid(),
            TranscriptBufferSeconds: 60,
            MaxFollowUpQuestions: 1,
            HasFollowUp: true,
            ExamInputMode: inputMode
        );

        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "ADV-M1-08: Validator PHẢI chặn khi ExamInputMode là giá trị bất thường, sai hoa thường hoặc khoảng trắng")]
    [InlineData("VoiceAndTextInput")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("voice_only")]
    [InlineData("VOICEONLY")]
    [InlineData("VoiceOnly ")]
    [InlineData(" VoiceOnly")]
    [InlineData("Voice")]
    [InlineData("Text")]
    [InlineData("VoiceAndText")]
    [InlineData("INVALID_MODE")]
    public void Validator_Must_Fail_For_Invalid_ExamInputModes(string inputMode)
    {
        var command = new UpdateCourseConfigurationCommand(
            CourseId: Guid.NewGuid(),
            TranscriptBufferSeconds: 60,
            MaxFollowUpQuestions: 1,
            HasFollowUp: true,
            ExamInputMode: inputMode
        );

        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateCourseConfigurationCommand.ExamInputMode));
        result.Errors.First(e => e.PropertyName == nameof(UpdateCourseConfigurationCommand.ExamInputMode))
            .ErrorMessage.Should().Contain("Phương thức thi ExamInputMode không hợp lệ");
    }

    #endregion

    #region 4. SystemConfig Model & Seed Data Verification

    [Fact(DisplayName = "ADV-M1-09: EF Core Model Metadata PHẢI Seed đầy đủ 5 cấu hình Admin cho system_configs")]
    public void EFCore_Model_Must_Seed_All_Required_SystemConfigs()
    {
        using var context = CreateDbContext();
        context.Database.EnsureCreated();

        var configs = context.SystemConfigs.ToList();
        configs.Should().NotBeEmpty("Bảng system_configs phải có seed data mặc định");

        var configDict = configs.ToDictionary(s => s.Key, s => s.Value);

        configDict.Should().ContainKey("MaxPracticeQuestionsPerSession");
        configDict["MaxPracticeQuestionsPerSession"].Should().Be("10");

        configDict.Should().ContainKey("MinMixedPracticeQuestions");
        configDict["MinMixedPracticeQuestions"].Should().Be("3");

        configDict.Should().ContainKey("MaxMixedPracticeQuestions");
        configDict["MaxMixedPracticeQuestions"].Should().Be("10");

        configDict.Should().ContainKey("TranscriptBufferSeconds");
        configDict["TranscriptBufferSeconds"].Should().Be("60");

        configDict.Should().ContainKey("MaxPracticeFollowUpQuestions");
        configDict["MaxPracticeFollowUpQuestions"].Should().Be("2");
    }

    #endregion
}
