using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// Empirical Challenger 1 for Milestone M1 (DB Seeder & SQL Seed Data).
/// Adversarial stress testing for database constraints, UUID collision resistance, and idempotency.
/// </summary>
public class AdversarialMilestone1DbSeederChallengerTests
{
    private static OralExamDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: $"M1ChallengerDb_{Guid.NewGuid():N}")
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "ADV-M1-SEED-01: Rubric Constraint ck_rubrics_total_score = 10.00m trên 100% Rubrics (SE & ENT)")]
    public async Task Rubrics_TotalMaxScore_Must_Equal_Exactly_10_00_Across_All_Courses()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var rubrics = await context.Rubrics
            .Include(r => r.Course)
            .Include(r => r.Criteria)
            .ToListAsync();

        rubrics.Should().NotBeEmpty("Phải có ít nhất 1 rubric được seed trong CSDL");
        rubrics.Count.Should().BeGreaterOrEqualTo(9, "Phải có ít nhất 3 rubric SE (PRN231, SWD392, SWR302) và 6 rubric ENT (ENT001..ENT503)");

        foreach (var rubric in rubrics)
        {
            // ck_rubrics_total_score CHECK (total_max_score = 10.00)
            rubric.TotalMaxScore.Should().Be(10.00m,
                $"Rubric ID {rubric.Id} của môn {rubric.Course?.Code ?? "Unknown"} phải có TotalMaxScore = 10.00m để thỏa mãn ck_rubrics_total_score");

            rubric.Criteria.Should().NotBeEmpty(
                $"Rubric ID {rubric.Id} của môn {rubric.Course?.Code ?? "Unknown"} không được để trống tiêu chí");

            // Capstone Invariant: Tổng điểm các tiêu chí thành phần = 10.00m
            var criteriaScoreSum = rubric.Criteria.Sum(c => c.MaxScore);
            criteriaScoreSum.Should().Be(10.00m,
                $"Rubric ID {rubric.Id} của môn {rubric.Course?.Code ?? "Unknown"} có tổng MaxScore các tiêu chí = {criteriaScoreSum}, bắt buộc phải bằng 10.00m");

            // Capstone Invariant: Tổng trọng số các tiêu chí thành phần = 1.00m
            var criteriaWeightSum = rubric.Criteria.Sum(c => c.Weight);
            criteriaWeightSum.Should().Be(1.00m,
                $"Rubric ID {rubric.Id} của môn {rubric.Course?.Code ?? "Unknown"} có tổng Weight các tiêu chí = {criteriaWeightSum}, bắt buộc phải bằng 1.00m");
        }
    }

    [Fact(DisplayName = "ADV-M1-SEED-02: Rubric Criteria Constraints (max_score > 0 and <= 10.0, weight > 0 and <= 1.0)")]
    public async Task RubricCriteria_Scores_And_Weights_Must_Satisfy_Check_Constraints()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var criteria = await context.RubricCriteria
            .Include(c => c.Rubric)
            .ToListAsync();

        criteria.Should().NotBeEmpty();

        foreach (var c in criteria)
        {
            // CONSTRAINT ck_rubric_criteria_score CHECK (max_score > 0 AND max_score <= 10.00)
            c.MaxScore.Should().BeGreaterThan(0,
                $"Criterion '{c.CriterionName}' (ID {c.Id}) có MaxScore <= 0 vi phạm ck_rubric_criteria_score");
            c.MaxScore.Should().BeLessOrEqualTo(10.00m,
                $"Criterion '{c.CriterionName}' (ID {c.Id}) có MaxScore > 10.00 vi phạm ck_rubric_criteria_score");

            // CONSTRAINT ck_rubric_criteria_weight CHECK (weight > 0 AND weight <= 1.00)
            c.Weight.Should().BeGreaterThan(0,
                $"Criterion '{c.CriterionName}' (ID {c.Id}) có Weight <= 0 vi phạm ck_rubric_criteria_weight");
            c.Weight.Should().BeLessOrEqualTo(1.00m,
                $"Criterion '{c.CriterionName}' (ID {c.Id}) có Weight > 1.00 vi phạm ck_rubric_criteria_weight");

            c.CriterionName.Should().NotBeNullOrWhiteSpace("Tên tiêu chí không được rỗng");
            c.RubricId.Should().NotBeEmpty("RubricId của tiêu chí không được là Guid rỗng");
        }
    }

    [Fact(DisplayName = "ADV-M1-SEED-03: FPTU Standardized Speaking Rubrics (ENT001 - ENT503) phải có đúng 5 tiêu chí chuẩn hóa x 2.0đ")]
    public async Task Ent_Rubrics_Must_Conform_To_5_Standardized_Fptu_Criteria()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var entCourseCodes = new[] { "ENT001", "ENT104", "ENT203", "ENT303", "ENT403", "ENT503" };

        var entCourses = await context.Courses
            .Include(c => c.Rubrics)
                .ThenInclude(r => r.Criteria)
            .Where(c => entCourseCodes.Contains(c.Code))
            .ToListAsync();

        entCourses.Should().HaveCount(6, "Phải seed đủ 6 học phần ENT001 - ENT503");

        foreach (var course in entCourses)
        {
            course.Rubrics.Should().NotBeEmpty($"Học phần {course.Code} phải có rubric");
            var activeRubric = course.Rubrics.First(r => r.IsActive);

            activeRubric.TotalMaxScore.Should().Be(10.00m);
            activeRubric.Criteria.Should().HaveCount(5,
                $"Học phần {course.Code} phải có đúng 5 tiêu chí theo chuẩn FPTU Speaking Rubric");

            foreach (var crit in activeRubric.Criteria)
            {
                crit.MaxScore.Should().Be(2.00m,
                    $"Tiêu chí '{crit.CriterionName}' của môn {course.Code} phải có điểm tối đa 2.00m");
                crit.Weight.Should().Be(0.20m,
                    $"Tiêu chí '{crit.CriterionName}' của môn {course.Code} phải có trọng số 0.20m (20%)");
            }

            activeRubric.Criteria.Sum(c => c.MaxScore).Should().Be(10.00m);
            activeRubric.Criteria.Sum(c => c.Weight).Should().Be(1.00m);
        }
    }

    [Fact(DisplayName = "ADV-M1-SEED-04: UUID Collision Resistance - Không có bất kỳ UUID nào bị trùng lặp trong Seeder")]
    public async Task Seeder_Entities_Must_Have_Zero_UUID_Collisions()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var allGuids = new List<(string EntityType, Guid Id)>();

        allGuids.AddRange((await context.Users.Select(x => x.Id).ToListAsync()).Select(id => ("User", id)));
        allGuids.AddRange((await context.Semesters.Select(x => x.Id).ToListAsync()).Select(id => ("Semester", id)));
        allGuids.AddRange((await context.Courses.Select(x => x.Id).ToListAsync()).Select(id => ("Course", id)));
        allGuids.AddRange((await context.Rubrics.Select(x => x.Id).ToListAsync()).Select(id => ("Rubric", id)));
        allGuids.AddRange((await context.RubricCriteria.Select(x => x.Id).ToListAsync()).Select(id => ("RubricCriterion", id)));
        allGuids.AddRange((await context.ExamStructures.Select(x => x.Id).ToListAsync()).Select(id => ("ExamStructure", id)));
        allGuids.AddRange((await context.PracticeQuestions.Select(x => x.Id).ToListAsync()).Select(id => ("PracticeQuestion", id)));
        allGuids.AddRange((await context.ExamQuestions.Select(x => x.Id).ToListAsync()).Select(id => ("ExamQuestion", id)));
        allGuids.AddRange((await context.Classes.Select(x => x.Id).ToListAsync()).Select(id => ("Class", id)));
        allGuids.AddRange((await context.ClassEnrollments.Select(x => x.Id).ToListAsync()).Select(id => ("ClassEnrollment", id)));
        allGuids.AddRange((await context.SystemConfigs.Select(x => x.Id).ToListAsync()).Select(id => ("SystemConfig", id)));

        // Kiểm tra không có Guid rỗng
        allGuids.Should().NotContain(x => x.Id == Guid.Empty, "Không được tồn tại Guid.Empty trong dữ liệu seed");

        // Kiểm tra tính đơn nhất (uniqueness / zero collision)
        var duplicates = allGuids
            .GroupBy(x => x.Id)
            .Where(g => g.Count() > 1)
            .Select(g => new { Id = g.Key, Entities = string.Join(", ", g.Select(e => e.EntityType)) })
            .ToList();

        duplicates.Should().BeEmpty(
            $"Phát hiện UUID bị xung đột (collision) trong Seeder: {string.Join("; ", duplicates.Select(d => $"{d.Id} ({d.Entities})"))}");
    }

    [Fact(DisplayName = "ADV-M1-SEED-05: ENT vs SE Disjoint UUID Domains - Dải ENT (0eXX) tách biệt hoàn toàn khỏi SE")]
    public async Task Ent_UUID_Domain_Must_Be_Strictly_Disjoint_From_Se_Entities()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var entCourses = await context.Courses
            .Where(c => c.Code.StartsWith("ENT"))
            .ToListAsync();

        var seCourses = await context.Courses
            .Where(c => !c.Code.StartsWith("ENT"))
            .ToListAsync();

        entCourses.Should().NotBeEmpty();
        seCourses.Should().NotBeEmpty();

        var entCourseIds = entCourses.Select(c => c.Id).ToHashSet();
        var seCourseIds = seCourses.Select(c => c.Id).ToHashSet();

        entCourseIds.Overlaps(seCourseIds).Should().BeFalse("ENT Course IDs không được trùng lặp với SE Course IDs");

        var entRubricIds = (await context.Rubrics
            .Where(r => entCourseIds.Contains(r.CourseId))
            .Select(r => r.Id)
            .ToListAsync()).ToHashSet();

        var seRubricIds = (await context.Rubrics
            .Where(r => seCourseIds.Contains(r.CourseId))
            .Select(r => r.Id)
            .ToListAsync()).ToHashSet();

        entRubricIds.Overlaps(seRubricIds).Should().BeFalse("ENT Rubric IDs không được trùng lặp với SE Rubric IDs");

        // Tất cả ENT GUIDs trong Seeder đều tuân thủ format 00000000-0000-0000-0000-000000000eXX
        foreach (var id in entCourseIds.Concat(entRubricIds))
        {
            var strId = id.ToString();
            strId.Should().StartWith("00000000-0000-0000-0000-000000000e",
                $"ID {strId} của thực thể ENT phải thuộc dải tiền tố chuẩn hóa 00000000-0000-0000-0000-000000000eXX");
        }
    }

    [Fact(DisplayName = "ADV-M1-SEED-06: Idempotency Stress Test - Gọi SeedAsync 3 lần liên tiếp bảo toàn 100% số lượng thực thể")]
    public async Task SeedAsync_Multiple_Consecutive_Invocations_Must_Be_100_Percent_Idempotent()
    {
        await using var context = CreateInMemoryContext();

        // Run 1
        await DevelopmentDataSeeder.SeedAsync(context);
        var courses1 = await context.Courses.CountAsync();
        var rubrics1 = await context.Rubrics.CountAsync();
        var criteria1 = await context.RubricCriteria.CountAsync();
        var questions1 = await context.PracticeQuestions.CountAsync();
        var configs1 = await context.SystemConfigs.CountAsync();

        // Run 2
        await DevelopmentDataSeeder.SeedAsync(context);
        var courses2 = await context.Courses.CountAsync();
        var rubrics2 = await context.Rubrics.CountAsync();
        var criteria2 = await context.RubricCriteria.CountAsync();
        var questions2 = await context.PracticeQuestions.CountAsync();
        var configs2 = await context.SystemConfigs.CountAsync();

        // Run 3
        await DevelopmentDataSeeder.SeedAsync(context);
        var courses3 = await context.Courses.CountAsync();
        var rubrics3 = await context.Rubrics.CountAsync();
        var criteria3 = await context.RubricCriteria.CountAsync();
        var questions3 = await context.PracticeQuestions.CountAsync();
        var configs3 = await context.SystemConfigs.CountAsync();

        courses2.Should().Be(courses1);
        courses3.Should().Be(courses1);

        rubrics2.Should().Be(rubrics1);
        rubrics3.Should().Be(rubrics1);

        criteria2.Should().Be(criteria1);
        criteria3.Should().Be(criteria1);

        questions2.Should().Be(questions1);
        questions3.Should().Be(questions1);

        configs2.Should().Be(configs1);
        configs3.Should().Be(configs1);
    }

    [Fact(DisplayName = "ADV-M1-SEED-07: Static Image Assets - Tất cả ảnh của câu hỏi Multimodal ENT phải tồn tại trên ổ đĩa")]
    public async Task Multimodal_Questions_Referenced_Images_Must_Exist_On_Disk()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var multimodalQuestions = await context.PracticeQuestions
            .Where(q => !string.IsNullOrEmpty(q.ImageUrl))
            .ToListAsync();

        multimodalQuestions.Should().NotBeEmpty("Phải có ít nhất các câu hỏi Multimodal ENT001, ENT104, ENT203 chứa ảnh");

        // Tìm thư mục API wwwroot
        var baseDir = AppContext.BaseDirectory;
        // Đi lên tìm thư mục 05_Source_Code
        var currentDir = new DirectoryInfo(baseDir);
        while (currentDir != null && !Directory.Exists(Path.Combine(currentDir.FullName, "05_Source_Code")) && !File.Exists(Path.Combine(currentDir.FullName, "OralExamination.sln")))
        {
            currentDir = currentDir.Parent;
        }

        string wwwrootDir;
        if (currentDir != null && File.Exists(Path.Combine(currentDir.FullName, "OralExamination.sln")))
        {
            wwwrootDir = Path.Combine(currentDir.FullName, "src", "API", "wwwroot");
        }
        else if (currentDir != null && Directory.Exists(Path.Combine(currentDir.FullName, "05_Source_Code", "backend", "src", "API", "wwwroot")))
        {
            wwwrootDir = Path.Combine(currentDir.FullName, "05_Source_Code", "backend", "src", "API", "wwwroot");
        }
        else
        {
            wwwrootDir = @"D:\Đồ Án\05_Source_Code\backend\src\API\wwwroot";
        }

        Directory.Exists(wwwrootDir).Should().BeTrue($"Thư mục wwwroot phải tồn tại: {wwwrootDir}");

        foreach (var q in multimodalQuestions)
        {
            q.ImageDescription.Should().NotBeNullOrWhiteSpace($"Câu hỏi '{q.Title}' phải có ImageDescription mô tả ngữ cảnh ảnh");

            var relativePath = q.ImageUrl!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(wwwrootDir, relativePath);

            File.Exists(fullPath).Should().BeTrue(
                $"Tệp ảnh '{q.ImageUrl}' tham chiếu trong câu hỏi '{q.Title}' không tồn tại tại đường dẫn: {fullPath}");

            var fileInfo = new FileInfo(fullPath);
            fileInfo.Length.Should().BeGreaterThan(1024, $"Tệp ảnh '{fullPath}' có kích thước quá nhỏ ({fileInfo.Length} bytes)");
        }
    }

    [Fact(DisplayName = "ADV-M1-SEED-08: Kiểm định SQL 02_seed.sql - Đảm bảo tính nhất quán, không xung đột UUID và ON CONFLICT DO NOTHING")]
    public void SqlSeedFile_Must_Be_Idempotent_And_Have_Zero_UUID_Collisions()
    {
        var candidatePaths = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "infra", "postgres", "init", "02_seed.sql")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "..", "infra", "postgres", "init", "02_seed.sql")),
            @"D:\Đồ Án\infra\postgres\init\02_seed.sql",
            @"D:\Đồ Án\05_Source_Code\infra\postgres\init\02_seed.sql"
        };
        var sqlPaths = candidatePaths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        sqlPaths.Should().NotBeEmpty("Phải tìm thấy ít nhất 1 tệp 02_seed.sql hợp lệ");

        foreach (var path in sqlPaths)
        {
            File.Exists(path).Should().BeTrue($"Tệp SQL seed phải tồn tại: {path}");

            var content = File.ReadAllText(path);

            // 1. Kiểm tra 100% lệnh INSERT đều có ON CONFLICT
            var insertMatches = Regex.Matches(content, @"INSERT\s+INTO\s+([a-zA-Z0-9_]+)", RegexOptions.IgnoreCase);
            insertMatches.Count.Should().BeGreaterThan(10, "Tệp 02_seed.sql phải chứa nhiều lệnh INSERT");

            var onConflictMatches = Regex.Matches(content, @"ON\s+CONFLICT\s*(\([a-zA-Z0-9_,\s]+\))?\s*DO\s+NOTHING", RegexOptions.IgnoreCase);
            onConflictMatches.Count.Should().Be(insertMatches.Count,
                $"Số lượng mệnh đề ON CONFLICT DO NOTHING ({onConflictMatches.Count}) trong {path} phải bằng số lượng INSERT ({insertMatches.Count}) để đảm bảo Idempotent 100%");

            // 2. Trích xuất tất cả UUID dạng 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
            var uuidMatches = Regex.Matches(content, @"'([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})'");
            var primaryKeyUuids = new List<string>();

            // 3. Trích xuất UUID đầu tiên trong mỗi dòng VALUES (chính là khóa chính id)
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("('") || trimmed.StartsWith("VALUES ('") || trimmed.StartsWith("VALUES('"))
                {
                    var m = Regex.Match(trimmed, @"'([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})'");
                    if (m.Success)
                    {
                        primaryKeyUuids.Add(m.Groups[1].Value.ToLowerInvariant());
                    }
                }
            }

            // Kiểm tra collision giữa các Primary Keys trong SQL
            var duplicatePks = primaryKeyUuids
                .GroupBy(x => x)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            duplicatePks.Should().BeEmpty(
                $"Phát hiện khóa chính UUID bị trùng lặp trong tệp SQL {path}: {string.Join(", ", duplicatePks)}");

            // 4. Kiểm tra sự hiện diện của 6 ENT courses trong SQL
            content.Should().Contain("ENT001");
            content.Should().Contain("ENT104");
            content.Should().Contain("ENT203");
            content.Should().Contain("ENT303");
            content.Should().Contain("ENT403");
            content.Should().Contain("ENT503");
        }
    }
}
