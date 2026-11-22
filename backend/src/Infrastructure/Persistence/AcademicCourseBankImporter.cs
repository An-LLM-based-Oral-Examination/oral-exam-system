using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Infrastructure.Persistence;

public sealed record AcademicCourseBankImportResult(
    int CoursesProcessed,
    int ClosUpserted,
    int RubricsInserted,
    int CriteriaInserted,
    int QuestionsInserted,
    int QuestionsBackfilled,
    int ExamStructuresUpserted,
    int ExamStructuresDeferred,
    int LegacyCoursesSoftHidden,
    int LegacyClosRenamed,
    int LegacyQuestionsDeactivated,
    IReadOnlyList<string> ConflictWarnings);

/// <summary>
/// Trình nạp học liệu học thuật an toàn cho cả Development và Production
/// (không chứa mật khẩu tài khoản mẫu): nạp Semester FA26, AcademicProgram SE,
/// SystemConfigs mặc định, PRN231 + ENT001..503, gói dữ liệu gốc ReviewedCourseBanks.json
/// kèm +27 câu bổ sung SupplementaryCourseQuestions.json cho SWT301/SWR302/SWD392,
/// cấu hình ExamStructure MF-02 và ẩn mềm 3 môn cũ PRF192/CSD201/DBI202.
/// </summary>
public static class AcademicCourseBankImporter
{
    public const string ReviewedCourseBanksResourceName = "OralExamination.Persistence.Data.ReviewedCourseBanks.json";
    public const string SupplementaryCourseQuestionsResourceName = "OralExamination.Persistence.Data.SupplementaryCourseQuestions.json";
    public const string SupplementarySourceMarker = "supplementary-v1:2026-11-22";

    private const int AdvisoryLockKey = 748291035;
    private const string DefaultRubricDescription =
        "Cộng trực tiếp điểm từng tiêu chí (tối đa 10.00 điểm); chấp nhận diễn đạt tương đương đúng bản chất.";
    private const string DefaultMockBloomDistribution = "{\"Understand\": 2, \"Apply\": 2, \"Analyze\": 1}";

    private static readonly DateTime SeedTimestampUtc = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static readonly Guid CourseSwt301Id = Guid.Parse("584c8f86-968f-5346-95e3-2e26833564cb");
    public static readonly Guid CourseSwr302Id = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    public static readonly Guid CourseSwd392Id = Guid.Parse("00000000-0000-0000-0000-000000000009");

    public static readonly Guid OfferingSwt301Fa26Id = Guid.Parse("00000000-0000-0000-0008-000000000011");
    public static readonly Guid OfferingSwr302Fa26Id = Guid.Parse("00000000-0000-0000-0008-000000000012");
    public static readonly Guid OfferingSwd392Fa26Id = Guid.Parse("00000000-0000-0000-0008-000000000013");

    public static readonly Guid StructureSwt301Id = Guid.Parse("00000000-0000-0000-0008-000000000021");
    public static readonly Guid StructureSwr302Id = Guid.Parse("00000000-0000-0000-0008-000000000022");
    public static readonly Guid StructureSwd392Id = Guid.Parse("00000000-0000-0000-0008-000000000023");

    private static readonly IReadOnlyDictionary<string, (Guid OfferingId, Guid StructureId)> CourseDeterministicIds =
        new Dictionary<string, (Guid OfferingId, Guid StructureId)>(StringComparer.OrdinalIgnoreCase)
        {
            ["SWT301"] = (OfferingSwt301Fa26Id, StructureSwt301Id),
            ["SWR302"] = (OfferingSwr302Fa26Id, StructureSwr302Id),
            ["SWD392"] = (OfferingSwd392Fa26Id, StructureSwd392Id)
        };

    private static readonly string[] LegacySoftHideCourseCodes = ["PRF192", "CSD201", "DBI202"];

    public static async Task<AcademicCourseBankImportResult> ImportAsync(
        OralExamDbContext context,
        ILogger? logger = null,
        bool allowDeferExamStructuresWhenNoUsers = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var reviewedDataset = LoadReviewedDataset();
        var supplementaryDataset = LoadSupplementaryDataset();
        ValidateDatasets(reviewedDataset, supplementaryDataset);

        AcademicCourseBankImportResult? result = null;

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

            result = await ExecuteImportCoreAsync(
                context,
                reviewedDataset,
                supplementaryDataset,
                logger,
                allowDeferExamStructuresWhenNoUsers,
                cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        });

        return result!;
    }

    private static async Task<AcademicCourseBankImportResult> ExecuteImportCoreAsync(
        OralExamDbContext context,
        ReviewedCourseBankDataset reviewedDataset,
        SupplementaryCourseQuestionsDataset supplementaryDataset,
        ILogger? logger,
        bool allowDeferExamStructuresWhenNoUsers,
        CancellationToken cancellationToken)
    {
        var conflicts = new List<string>();
        var coursesProcessed = 0;
        var closUpserted = 0;
        var rubricsInserted = 0;
        var criteriaInserted = 0;
        var questionsInserted = 0;
        var questionsBackfilled = 0;
        var examStructuresUpserted = 0;
        var examStructuresDeferred = 0;
        var legacyClosRenamed = 0;
        var legacyQuestionsDeactivated = 0;

        // Step A: Ensure Semester FA26, AcademicProgram SE, SystemConfigs, and PRN231 + ENT001..503 catalog
        var semester = await EnsureFa26SemesterAsync(context, cancellationToken);
        var seProgram = await EnsureSeAcademicProgramAsync(context, cancellationToken);
        await DevelopmentDataSeeder.EnsureSystemConfigsAsync(context, cancellationToken);

        await DevelopmentDataSeeder.EnsurePrnAndEntCatalogAsync(
            context,
            semester.Id,
            seProgram.Id,
            cancellationToken);

        // Soft-hide legacy PRF192, CSD201, DBI202 without deleting rows or FKs
        var legacyCoursesSoftHidden = await SoftHideLegacyCoursesAsync(context, cancellationToken);

        // Resolve optional DepartmentHead for new SE courses (without forging fake users)
        var resolvedDeptHeadId = await ResolveDepartmentHeadUserIdAsync(context, seProgram, cancellationToken);

        // Pre-index supplementary questions by courseCode
        var supplementaryByCourse = supplementaryDataset.Questions
            .GroupBy(q => q.CourseCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var allCourses = await context.Courses.ToListAsync(cancellationToken);

        foreach (var courseDto in reviewedDataset.Courses)
        {
            coursesProcessed++;
            if (!CourseDeterministicIds.TryGetValue(courseDto.CourseCode, out var deterministicIds))
            {
                throw new InvalidOperationException($"Course '{courseDto.CourseCode}' lacks deterministic offering/structure mapping.");
            }

            // Step B: existingCourseLookup by Code, remap CourseId if DB already has different ID
            var existingCourse = allCourses.FirstOrDefault(c =>
                string.Equals(c.Code, courseDto.CourseCode, StringComparison.OrdinalIgnoreCase));

            if (existingCourse is null)
            {
                var idCollisionCourse = allCourses.FirstOrDefault(c => c.Id == courseDto.Id);
                if (idCollisionCourse is not null)
                {
                    throw new InvalidOperationException(
                        $"ID policy violation: Course ID '{courseDto.Id}' already belongs to '{idCollisionCourse.Code}' instead of '{courseDto.CourseCode}'.");
                }

                existingCourse = new Course
                {
                    Id = courseDto.Id,
                    Code = courseDto.CourseCode,
                    Name = courseDto.CourseName,
                    Credits = courseDto.Credits,
                    AcademicProgramId = seProgram.Id,
                    SemesterId = semester.Id,
                    DepartmentHeadUserId = resolvedDeptHeadId,
                    HasFollowUp = courseDto.HasFollowUp,
                    TranscriptBufferSeconds = 60,
                    MaxFollowUpQuestions = 2,
                    MaxMockExamsPerDay = 3,
                    MockExamTotalQuestions = 5,
                    MockExamDurationMinutes = 30,
                    MockExamBloomDistribution = DefaultMockBloomDistribution,
                    ExamInputMode = ExamInputMode.VoiceWithTranscriptEdit,
                    IsActive = courseDto.IsActive,
                    CreatedAt = courseDto.CreatedAt ?? SeedTimestampUtc
                };
                context.Courses.Add(existingCourse);
                allCourses.Add(existingCourse);
            }
            else
            {
                existingCourse.Name = courseDto.CourseName;
                existingCourse.Credits = courseDto.Credits;
                existingCourse.AcademicProgramId ??= seProgram.Id;
                existingCourse.DepartmentHeadUserId ??= resolvedDeptHeadId;
                existingCourse.HasFollowUp = courseDto.HasFollowUp;
                existingCourse.TranscriptBufferSeconds = existingCourse.TranscriptBufferSeconds is >= 10 and <= 300
                    ? existingCourse.TranscriptBufferSeconds
                    : 60;
                existingCourse.MaxFollowUpQuestions = existingCourse.MaxFollowUpQuestions is >= 1 and <= 5
                    ? existingCourse.MaxFollowUpQuestions
                    : 2;
                existingCourse.MaxMockExamsPerDay = 3;
                existingCourse.MockExamTotalQuestions = 5;
                existingCourse.MockExamDurationMinutes = 30;
                existingCourse.MockExamBloomDistribution = DefaultMockBloomDistribution;
                existingCourse.ExamInputMode = string.IsNullOrWhiteSpace(existingCourse.ExamInputMode)
                    ? ExamInputMode.VoiceWithTranscriptEdit
                    : existingCourse.ExamInputMode;
                existingCourse.IsActive = courseDto.IsActive;
            }

            var effectiveCourseId = existingCourse.Id;
            await context.SaveChangesAsync(cancellationToken);

            // Ensure CourseOffering for (effectiveCourseId, semester.Id)
            var offering = await context.CourseOfferings.FirstOrDefaultAsync(
                o => o.CourseId == effectiveCourseId && o.SemesterId == semester.Id,
                cancellationToken);
            if (offering is null)
            {
                var offeringById = await context.CourseOfferings.FirstOrDefaultAsync(
                    o => o.Id == deterministicIds.OfferingId,
                    cancellationToken);
                if (offeringById is not null && offeringById.CourseId != effectiveCourseId)
                {
                    throw new InvalidOperationException(
                        $"ID policy violation: CourseOffering '{deterministicIds.OfferingId}' belongs to another course.");
                }

                offering = offeringById ?? new CourseOffering
                {
                    Id = deterministicIds.OfferingId,
                    CourseId = effectiveCourseId,
                    SemesterId = semester.Id,
                    IsEnabled = courseDto.CourseOffering?.IsEnabled ?? true,
                    CreatedAt = SeedTimestampUtc,
                    UpdatedAt = SeedTimestampUtc
                };
                if (offeringById is null)
                {
                    context.CourseOfferings.Add(offering);
                }
            }
            else
            {
                offering.IsEnabled = courseDto.CourseOffering?.IsEnabled ?? true;
                offering.UpdatedAt = SeedTimestampUtc;
            }
            await context.SaveChangesAsync(cancellationToken);

            // Step C & D: Active assessment guard + legacyData deactivation + LEGACY- CLO rename BEFORE inserting new CLOs
            if (courseDto.LegacyData is not null)
            {
                var (renamedCount, deactivatedCount) = await ApplyLegacyDataRulesAsync(
                    context,
                    effectiveCourseId,
                    courseDto.CourseCode,
                    courseDto.LegacyData,
                    cancellationToken);
                legacyClosRenamed += renamedCount;
                legacyQuestionsDeactivated += deactivatedCount;
            }

            // Step E1: Upsert CourseClos (respecting uq_course_clos_course_code & idPolicy)
            var existingClosForCourse = await context.CourseClos
                .Where(c => c.CourseId == effectiveCourseId)
                .ToListAsync(cancellationToken);

            var incomingCloIds = courseDto.Clos.Select(c => c.Id).ToHashSet();
            var otherCourseCloIds = (await context.CourseClos
                .AsNoTracking()
                .Where(c => incomingCloIds.Contains(c.Id) && c.CourseId != effectiveCourseId)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            if (otherCourseCloIds.Count > 0)
            {
                throw new InvalidOperationException(
                    $"ID policy violation: CLO IDs ({string.Join(", ", otherCourseCloIds)}) already belong to a different course.");
            }

            var cloCodeToEffectiveId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var cloDto in courseDto.Clos)
            {
                var existingClo = existingClosForCourse.FirstOrDefault(c => c.Id == cloDto.Id)
                    ?? existingClosForCourse.FirstOrDefault(c => string.Equals(c.CloCode, cloDto.Code, StringComparison.OrdinalIgnoreCase));

                if (existingClo is null)
                {
                    var newClo = new CourseClo
                    {
                        Id = cloDto.Id,
                        CourseId = effectiveCourseId,
                        CloCode = cloDto.Code,
                        Description = cloDto.Description,
                        BloomLevel = cloDto.OralBankBloom,
                        OrderIndex = cloDto.OrderIndex,
                        IsActive = cloDto.IsActive,
                        CreatedAt = cloDto.CreatedAt ?? SeedTimestampUtc
                    };
                    context.CourseClos.Add(newClo);
                    existingClosForCourse.Add(newClo);
                    cloCodeToEffectiveId[cloDto.Code] = newClo.Id;
                    closUpserted++;
                }
                else
                {
                    cloCodeToEffectiveId[cloDto.Code] = existingClo.Id;
                    if (!string.Equals(existingClo.Description, cloDto.Description, StringComparison.Ordinal) ||
                        !string.Equals(existingClo.BloomLevel, cloDto.OralBankBloom, StringComparison.OrdinalIgnoreCase))
                    {
                        var warning = $"Conflict preserved: Course '{courseDto.CourseCode}' CLO '{existingClo.CloCode}' (Id={existingClo.Id}) differs from seed; keeping existing database content.";
                        conflicts.Add(warning);
                        logger?.LogWarning("{Warning}", warning);
                    }
                    else
                    {
                        existingClo.IsActive = true;
                    }
                }
            }
            await context.SaveChangesAsync(cancellationToken);

            // Step E2: Combine original (104) + supplementary (+27) questions for this course
            var combinedQuestions = new List<NormalizedImportQuestion>(courseDto.Questions.Count + 12);
            foreach (var q in courseDto.Questions)
            {
                var reviewNotes = string.IsNullOrWhiteSpace(q.ReviewId)
                    ? $"dataset:{reviewedDataset.DatasetVersion}"
                    : $"dataset:{reviewedDataset.DatasetVersion} | {q.ReviewId}";
                combinedQuestions.Add(new NormalizedImportQuestion(
                    q.Id,
                    q.CloCode,
                    q.Title,
                    q.Content,
                    q.SampleAnswer,
                    q.KeyPoints,
                    q.Difficulty,
                    q.BloomLevel,
                    q.HasFollowUp,
                    q.FollowUpPrompt,
                    q.Source,
                    q.ApprovalStatus,
                    reviewNotes,
                    q.IsActive,
                    q.IsDeleted,
                    q.CreatedAt ?? SeedTimestampUtc,
                    q.Rubric));
            }

            if (supplementaryByCourse.TryGetValue(courseDto.CourseCode, out var suppList))
            {
                foreach (var sq in suppList)
                {
                    combinedQuestions.Add(new NormalizedImportQuestion(
                        sq.Id,
                        sq.CloCode,
                        sq.Title,
                        sq.Content,
                        sq.SampleAnswer,
                        sq.KeyPoints,
                        sq.Difficulty,
                        sq.BloomLevel,
                        sq.HasFollowUp,
                        sq.FollowUpPrompt,
                        sq.Source,
                        sq.ApprovalStatus,
                        sq.ReviewNotes,
                        sq.IsActive,
                        sq.IsDeleted,
                        sq.CreatedAt ?? SeedTimestampUtc,
                        sq.Rubric));
                }
            }

            // Pre-load existing Rubrics, Criteria, and PracticeQuestions for idPolicy and existingContent checks
            var targetRubricIds = combinedQuestions.Select(q => q.Rubric.Id).ToHashSet();
            var existingRubricsById = await context.Rubrics
                .Where(r => targetRubricIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, cancellationToken);

            var targetCriterionIds = combinedQuestions.SelectMany(q => q.Rubric.Criteria.Select(c => c.Id)).ToHashSet();
            var existingCriteriaById = await context.RubricCriteria
                .Where(c => targetCriterionIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, cancellationToken);

            var targetQuestionIds = combinedQuestions.Select(q => q.Id).ToHashSet();
            var existingQuestionsById = await context.PracticeQuestions
                .Where(q => targetQuestionIds.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id, cancellationToken);

            foreach (var q in combinedQuestions)
            {
                if (!cloCodeToEffectiveId.TryGetValue(q.CloCode, out var effectiveCloId))
                {
                    throw new InvalidOperationException(
                        $"Question '{q.Id}' in '{courseDto.CourseCode}' references unknown CLO '{q.CloCode}'.");
                }

                // Rubric idPolicy & insert (preserve manual edits if already exists)
                if (existingRubricsById.TryGetValue(q.Rubric.Id, out var existingRubric))
                {
                    if (existingRubric.CourseId != effectiveCourseId)
                    {
                        throw new InvalidOperationException(
                            $"ID policy violation: Rubric '{q.Rubric.Id}' belongs to course '{existingRubric.CourseId}' instead of '{effectiveCourseId}'.");
                    }
                }
                else
                {
                    var rubricEntity = new Rubric
                    {
                        Id = q.Rubric.Id,
                        CourseId = effectiveCourseId,
                        Name = q.Rubric.Name,
                        Description = string.IsNullOrWhiteSpace(q.Rubric.Description)
                            ? DefaultRubricDescription
                            : q.Rubric.Description,
                        TotalMaxScore = q.Rubric.TotalMaxScore,
                        IsActive = q.Rubric.IsActive,
                        CreatedAt = q.Rubric.CreatedAt ?? SeedTimestampUtc
                    };
                    context.Rubrics.Add(rubricEntity);
                    existingRubricsById[rubricEntity.Id] = rubricEntity;
                    rubricsInserted++;
                }

                // RubricCriteria idPolicy & insert
                foreach (var c in q.Rubric.Criteria)
                {
                    if (existingCriteriaById.TryGetValue(c.Id, out var existingCrit))
                    {
                        if (existingCrit.RubricId != q.Rubric.Id)
                        {
                            throw new InvalidOperationException(
                                $"ID policy violation: RubricCriterion '{c.Id}' belongs to rubric '{existingCrit.RubricId}' instead of '{q.Rubric.Id}'.");
                        }
                    }
                    else
                    {
                        var critEntity = new RubricCriterion
                        {
                            Id = c.Id,
                            RubricId = q.Rubric.Id,
                            CriterionName = c.CriterionName,
                            Description = c.Description,
                            MaxScore = c.MaxScore,
                            Weight = c.Weight,
                            BloomLevel = c.BloomLevel,
                            OrderIndex = c.OrderIndex,
                            CreatedAt = c.CreatedAt ?? SeedTimestampUtc
                        };
                        context.RubricCriteria.Add(critEntity);
                        existingCriteriaById[critEntity.Id] = critEntity;
                        criteriaInserted++;
                    }
                }

                // PracticeQuestion idPolicy & insert (preserve lecturer edits if already exists)
                var serializedKeyPoints = JsonSerializer.Serialize(q.KeyPoints);
                if (existingQuestionsById.TryGetValue(q.Id, out var existingQuestion))
                {
                    if (existingQuestion.CourseId != effectiveCourseId)
                    {
                        throw new InvalidOperationException(
                            $"ID policy violation: PracticeQuestion '{q.Id}' belongs to course '{existingQuestion.CourseId}' instead of '{effectiveCourseId}'.");
                    }

                    if (!existingQuestion.CloId.HasValue)
                    {
                        existingQuestion.CloId = effectiveCloId;
                        questionsBackfilled++;
                    }
                }
                else
                {
                    var questionEntity = new PracticeQuestion
                    {
                        Id = q.Id,
                        CourseId = effectiveCourseId,
                        CloId = effectiveCloId,
                        RubricId = q.Rubric.Id,
                        Title = q.Title,
                        Content = q.Content,
                        SampleAnswer = q.SampleAnswer,
                        KeyPoints = serializedKeyPoints,
                        Difficulty = q.Difficulty,
                        BloomLevel = q.BloomLevel,
                        HasFollowUp = q.HasFollowUp,
                        FollowUpPrompt = q.FollowUpPrompt,
                        ApprovalStatus = q.ApprovalStatus,
                        Source = q.Source,
                        ApprovedBy = null,
                        ApprovedAt = null,
                        SubmittedBy = null,
                        ReviewNotes = q.ReviewNotes,
                        IsActive = q.IsActive,
                        IsDeleted = q.IsDeleted,
                        CreatedAt = q.CreatedAt
                    };
                    context.PracticeQuestions.Add(questionEntity);
                    existingQuestionsById[questionEntity.Id] = questionEntity;
                    questionsInserted++;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        // Step F: Upsert MF-02 ExamStructures for SWT301, SWR302, SWD392 + PRN231 + ENT001..503
        var structureTargets = new List<(string CourseCode, Guid StructureId, string Name, int DurationMinutes, string BloomDistribution)>
        {
            ("SWT301", StructureSwt301Id, "Cấu trúc Đề thi thử MF-02 SWT301 (FA26)", 30, DefaultMockBloomDistribution),
            ("SWR302", StructureSwr302Id, "Cấu trúc Đề thi thử MF-02 SWR302 (FA26)", 30, DefaultMockBloomDistribution),
            ("SWD392", StructureSwd392Id, "Cấu trúc Đề thi thử MF-02 SWD392 (FA26)", 30, DefaultMockBloomDistribution),
            ("PRN231", DevelopmentDataSeeder.StructurePrn231Id, "Cấu trúc Đề thi Vấn đáp Cuối kỳ PRN231 (FA26)", 30, DefaultMockBloomDistribution),
            ("ENT001", DevelopmentDataSeeder.StructureEnt001Id, "FPTU Speaking Test Structure ENT001 (FA26)", 20, DefaultMockBloomDistribution),
            ("ENT104", DevelopmentDataSeeder.StructureEnt104Id, "FPTU Speaking Test Structure ENT104 (FA26)", 20, DefaultMockBloomDistribution),
            ("ENT203", DevelopmentDataSeeder.StructureEnt203Id, "FPTU Speaking Test Structure ENT203 (FA26)", 20, DefaultMockBloomDistribution),
            ("ENT303", DevelopmentDataSeeder.StructureEnt303Id, "FPTU Speaking Test Structure ENT303 (FA26)", 20, DefaultMockBloomDistribution),
            ("ENT403", DevelopmentDataSeeder.StructureEnt403Id, "FPTU Speaking Test Structure ENT403 (FA26)", 20, DefaultMockBloomDistribution),
            ("ENT503", DevelopmentDataSeeder.StructureEnt503Id, "FPTU Speaking Test Structure ENT503 (FA26)", 20, DefaultMockBloomDistribution)
        };

        var refreshedCourses = await context.Courses.ToListAsync(cancellationToken);
        var existingStructures = await context.ExamStructures.ToListAsync(cancellationToken);

        foreach (var (courseCode, structureId, name, durationMinutes, bloomDist) in structureTargets)
        {
            var course = refreshedCourses.FirstOrDefault(c => string.Equals(c.Code, courseCode, StringComparison.OrdinalIgnoreCase));
            if (course is null)
            {
                continue;
            }

            var existing = existingStructures.FirstOrDefault(s => s.Id == structureId);
            if (existing is not null)
            {
                if (existing.CourseId != course.Id)
                {
                    throw new InvalidOperationException(
                        $"ID policy violation: ExamStructure '{structureId}' belongs to course '{existing.CourseId}' instead of '{course.Id}'.");
                }

                existing.IsActive = true;
                existing.TotalQuestions = 5;
                existing.DurationMinutes = durationMinutes;
                existing.BloomDistribution = bloomDist;
                examStructuresUpserted++;
                continue;
            }

            // If another active structure already exists for this course (e.g., created by DeptHead), keep it;
            // only create deterministic MF-02 structure if no active structure exists for this course yet.
            if (existingStructures.Any(s => s.CourseId == course.Id && s.IsActive))
            {
                continue;
            }

            var creatorId = await ResolveExamStructureCreatorIdAsync(context, course, seProgram, cancellationToken);
            if (!creatorId.HasValue)
            {
                if (allowDeferExamStructuresWhenNoUsers)
                {
                    examStructuresDeferred++;
                    logger?.LogWarning(
                        "Deferring MF-02 ExamStructure creation for {CourseCode}: no active DepartmentHead or Admin user exists yet.",
                        courseCode);
                    continue;
                }

                throw new InvalidOperationException(
                    $"[FAIL CLOSED] Cannot create MF-02 ExamStructure for {courseCode}: no active DepartmentHead or Admin user exists for CreatedBy.");
            }

            var structure = new ExamStructure
            {
                Id = structureId,
                CourseId = course.Id,
                CreatedBy = creatorId.Value,
                Name = name,
                TotalQuestions = 5,
                DurationMinutes = durationMinutes,
                BloomDistribution = bloomDist,
                SlotDefinitions = "[]",
                RequiredSetCount = 1,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            };
            context.ExamStructures.Add(structure);
            existingStructures.Add(structure);
            examStructuresUpserted++;
        }

        await context.SaveChangesAsync(cancellationToken);

        return new AcademicCourseBankImportResult(
            coursesProcessed,
            closUpserted,
            rubricsInserted,
            criteriaInserted,
            questionsInserted,
            questionsBackfilled,
            examStructuresUpserted,
            examStructuresDeferred,
            legacyCoursesSoftHidden,
            legacyClosRenamed,
            legacyQuestionsDeactivated,
            conflicts);
    }

    private static async Task<(int RenamedClos, int DeactivatedQuestions)> ApplyLegacyDataRulesAsync(
        OralExamDbContext context,
        Guid effectiveCourseId,
        string courseCode,
        LegacyCourseDataDto legacyData,
        CancellationToken cancellationToken)
    {
        var renamedClos = 0;
        var deactivatedQuestions = 0;

        var legacyPracticeIds = legacyData.PracticeQuestionIds.ToHashSet();
        var legacyExamIds = legacyData.ExamQuestionIds.ToHashSet();
        var legacyCloIds = legacyData.CourseCloIds.ToHashSet();
        var legacyRubricIds = legacyData.RubricIds.ToHashSet();
        var legacyStructureIds = legacyData.ExamStructureIds.ToHashSet();
        var legacySetIds = legacyData.ExamSetIds.ToHashSet();

        var legacyPracticeRows = legacyPracticeIds.Count == 0
            ? []
            : await context.PracticeQuestions
                .Where(q => q.CourseId == effectiveCourseId && legacyPracticeIds.Contains(q.Id))
                .ToListAsync(cancellationToken);

        var legacyExamRows = legacyExamIds.Count == 0
            ? []
            : await context.ExamQuestions
                .Where(q => q.CourseId == effectiveCourseId && legacyExamIds.Contains(q.Id))
                .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(legacyData.ImporterReviewNotesMarker))
        {
            legacyExamRows = legacyExamRows
                .Where(q => q.ApprovedBy == null
                    && q.SubmittedBy == null
                    && q.ReviewNotes != null
                    && q.ReviewNotes.Contains(legacyData.ImporterReviewNotesMarker, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var legacyCloRows = legacyCloIds.Count == 0
            ? []
            : await context.CourseClos
                .Where(c => c.CourseId == effectiveCourseId && legacyCloIds.Contains(c.Id))
                .ToListAsync(cancellationToken);

        var hasPendingLegacyChanges =
            legacyPracticeRows.Any(q => q.IsActive) ||
            legacyExamRows.Any(q => q.IsActive) ||
            legacyCloRows.Any(c => c.IsActive || !c.CloCode.StartsWith("LEGACY-", StringComparison.OrdinalIgnoreCase));

        if (hasPendingLegacyChanges)
        {
            await EnsureNoActiveAssessmentsOnCourseAsync(context, effectiveCourseId, courseCode, cancellationToken);
        }

        foreach (var pq in legacyPracticeRows)
        {
            if (pq.IsActive)
            {
                pq.IsActive = false;
                deactivatedQuestions++;
            }
        }

        foreach (var eq in legacyExamRows)
        {
            if (eq.IsActive)
            {
                eq.IsActive = false;
                deactivatedQuestions++;
            }
        }

        if (legacyStructureIds.Count > 0)
        {
            var legacyStructures = await context.ExamStructures
                .Where(s => s.CourseId == effectiveCourseId && legacyStructureIds.Contains(s.Id))
                .ToListAsync(cancellationToken);
            foreach (var s in legacyStructures)
            {
                s.IsActive = false;
            }
        }

        if (legacySetIds.Count > 0)
        {
            var legacySets = await context.ExamSets
                .Where(s => s.CourseId == effectiveCourseId && legacySetIds.Contains(s.Id))
                .ToListAsync(cancellationToken);
            foreach (var s in legacySets)
            {
                s.IsActive = false;
            }
        }

        // Rename legacy CLO codes BEFORE inserting new CLO1..CLOn (uq_course_clos_course_code is unconditional)
        foreach (var clo in legacyCloRows)
        {
            var changed = false;
            if (clo.IsActive)
            {
                clo.IsActive = false;
                changed = true;
            }

            if (!clo.CloCode.StartsWith("LEGACY-", StringComparison.OrdinalIgnoreCase))
            {
                var suffix = clo.Id.ToString("N")[^12..];
                var baseCode = clo.CloCode.Trim();
                var maxBaseLen = Math.Max(1, 50 - "LEGACY--".Length - suffix.Length);
                if (baseCode.Length > maxBaseLen)
                {
                    baseCode = baseCode[..maxBaseLen];
                }

                clo.CloCode = $"LEGACY-{baseCode}-{suffix}";
                changed = true;
            }

            if (changed)
            {
                renamedClos++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        // sharedLegacyRubric: deactivate only if no active non-legacy questions still reference it
        if (legacyRubricIds.Count > 0)
        {
            var legacyRubrics = await context.Rubrics
                .Where(r => r.CourseId == effectiveCourseId && legacyRubricIds.Contains(r.Id) && r.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var rubric in legacyRubrics)
            {
                var stillUsedByPractice = await context.PracticeQuestions.AnyAsync(
                    q => q.RubricId == rubric.Id && q.IsActive && !q.IsDeleted && !legacyPracticeIds.Contains(q.Id),
                    cancellationToken);
                var stillUsedByExam = await context.ExamQuestions.AnyAsync(
                    q => q.RubricId == rubric.Id && q.IsActive && !q.IsDeleted && !legacyExamIds.Contains(q.Id),
                    cancellationToken);

                if (!stillUsedByPractice && !stillUsedByExam)
                {
                    rubric.IsActive = false;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        return (renamedClos, deactivatedQuestions);
    }

    private static async Task EnsureNoActiveAssessmentsOnCourseAsync(
        OralExamDbContext context,
        Guid courseId,
        string courseCode,
        CancellationToken cancellationToken)
    {
        var hasActivePractice = await context.PracticeSessions.AnyAsync(
            s => s.CourseId == courseId && s.Status == PracticeSessionStatus.InProgress,
            cancellationToken);
        if (hasActivePractice)
        {
            throw new InvalidOperationException(
                $"ActiveAssessmentGuard: Cannot deactivate legacy bank for '{courseCode}' while a PracticeSession is in_progress.");
        }

        var hasActiveMock = await context.MockExamSessions.AnyAsync(
            s => s.CourseId == courseId && s.Status == "in_progress",
            cancellationToken);
        if (hasActiveMock)
        {
            throw new InvalidOperationException(
                $"ActiveAssessmentGuard: Cannot deactivate legacy bank for '{courseCode}' while a MockExamSession is in_progress.");
        }

        var hasActiveInClass = await (
            from shift in context.InClassExamShifts
            join cls in context.Classes on shift.ClassId equals cls.Id
            where cls.CourseId == courseId && shift.Status == "IN_PROGRESS"
            select shift.Id)
            .AnyAsync(cancellationToken);
        if (hasActiveInClass)
        {
            throw new InvalidOperationException(
                $"ActiveAssessmentGuard: Cannot deactivate legacy bank for '{courseCode}' while an InClassExamShift is IN_PROGRESS.");
        }

        var hasActiveOfficial = await context.OfficialExamSessions.AnyAsync(
            s => s.CourseId == courseId && (s.Status == "scheduled" || s.Status == "in_progress"),
            cancellationToken);
        if (hasActiveOfficial)
        {
            throw new InvalidOperationException(
                $"ActiveAssessmentGuard: Cannot deactivate legacy bank for '{courseCode}' while an OfficialExamSession is scheduled or in_progress.");
        }
    }

    private static async Task<int> SoftHideLegacyCoursesAsync(
        OralExamDbContext context,
        CancellationToken cancellationToken)
    {
        var upperCodes = LegacySoftHideCourseCodes.Select(c => c.ToUpperInvariant()).ToHashSet();
        var legacyCourses = await context.Courses
            .Where(c => upperCodes.Contains(c.Code.ToUpper()))
            .ToListAsync(cancellationToken);

        if (legacyCourses.Count == 0)
        {
            return 0;
        }

        var hiddenCount = 0;
        var legacyCourseIds = legacyCourses.Select(c => c.Id).ToList();

        foreach (var course in legacyCourses)
        {
            if (course.IsActive)
            {
                course.IsActive = false;
                hiddenCount++;
            }
        }

        var legacyOfferings = await context.CourseOfferings
            .Where(o => legacyCourseIds.Contains(o.CourseId))
            .ToListAsync(cancellationToken);
        foreach (var offering in legacyOfferings)
        {
            if (offering.IsEnabled)
            {
                offering.IsEnabled = false;
                offering.UpdatedAt = DateTime.UtcNow;
            }
        }

        var legacyClasses = await context.Classes
            .Where(c => legacyCourseIds.Contains(c.CourseId))
            .ToListAsync(cancellationToken);
        var legacyClassIds = legacyClasses.Select(c => c.Id).ToList();
        foreach (var cls in legacyClasses)
        {
            cls.IsActive = false;
            cls.IsJoinEnabled = false;
            cls.AllowSelfEnrollment = false;
        }

        if (legacyClassIds.Count > 0)
        {
            var openLegacyShifts = await context.InClassExamShifts
                .Where(s => legacyClassIds.Contains(s.ClassId) && !s.IsLocked && s.Status == "IN_PROGRESS")
                .ToListAsync(cancellationToken);
            foreach (var shift in openLegacyShifts)
            {
                shift.Status = "CLOSED";
                shift.UpdatedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return hiddenCount;
    }

    private static async Task<Semester> EnsureFa26SemesterAsync(
        OralExamDbContext context,
        CancellationToken cancellationToken)
    {
        var semester = await context.Semesters
            .FirstOrDefaultAsync(s => s.Id == DevelopmentDataSeeder.SemesterFa26Id || s.Code == "FA26", cancellationToken);
        if (semester is null)
        {
            semester = new Semester
            {
                Id = DevelopmentDataSeeder.SemesterFa26Id,
                Code = "FA26",
                Name = "Fall 2026",
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 12, 31),
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            };
            context.Semesters.Add(semester);
            await context.SaveChangesAsync(cancellationToken);
        }

        return semester;
    }

    private static async Task<AcademicProgram> EnsureSeAcademicProgramAsync(
        OralExamDbContext context,
        CancellationToken cancellationToken)
    {
        var program = await context.AcademicPrograms
            .FirstOrDefaultAsync(p => p.Id == DevelopmentDataSeeder.AcademicProgramSeId || p.Code == "SE", cancellationToken);
        if (program is null)
        {
            program = new AcademicProgram
            {
                Id = DevelopmentDataSeeder.AcademicProgramSeId,
                Code = "SE",
                Name = "Kỹ thuật Phần mềm (Software Engineering)",
                IsActive = true,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            };
            context.AcademicPrograms.Add(program);
            await context.SaveChangesAsync(cancellationToken);
        }

        return program;
    }

    private static async Task<Guid?> ResolveDepartmentHeadUserIdAsync(
        OralExamDbContext context,
        AcademicProgram seProgram,
        CancellationToken cancellationToken)
    {
        if (seProgram.DepartmentHeadUserId.HasValue)
        {
            var isValid = await context.Users.AnyAsync(
                u => u.Id == seProgram.DepartmentHeadUserId.Value && u.IsActive,
                cancellationToken);
            if (isValid)
            {
                return seProgram.DepartmentHeadUserId.Value;
            }
        }

        var deptHead = await context.Users
            .Where(u => u.Role == UserRole.DepartmentHead && u.IsActive)
            .OrderBy(u => u.CreatedAt)
            .ThenBy(u => u.Id)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return deptHead;
    }

    private static async Task<Guid?> ResolveExamStructureCreatorIdAsync(
        OralExamDbContext context,
        Course course,
        AcademicProgram seProgram,
        CancellationToken cancellationToken)
    {
        if (course.DepartmentHeadUserId.HasValue)
        {
            var exists = await context.Users.AnyAsync(
                u => u.Id == course.DepartmentHeadUserId.Value && u.IsActive,
                cancellationToken);
            if (exists)
            {
                return course.DepartmentHeadUserId.Value;
            }
        }

        if (seProgram.DepartmentHeadUserId.HasValue)
        {
            var exists = await context.Users.AnyAsync(
                u => u.Id == seProgram.DepartmentHeadUserId.Value && u.IsActive,
                cancellationToken);
            if (exists)
            {
                return seProgram.DepartmentHeadUserId.Value;
            }
        }

        var deptHead = await context.Users
            .Where(u => u.Role == UserRole.DepartmentHead && u.IsActive)
            .OrderBy(u => u.CreatedAt)
            .ThenBy(u => u.Id)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (deptHead.HasValue)
        {
            return deptHead.Value;
        }

        return await context.Users
            .Where(u => u.Role == UserRole.Admin && u.IsActive)
            .OrderBy(u => u.CreatedAt)
            .ThenBy(u => u.Id)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static ReviewedCourseBankDataset LoadReviewedDataset()
    {
        var assembly = typeof(AcademicCourseBankImporter).Assembly;
        using var stream = assembly.GetManifestResourceStream(ReviewedCourseBanksResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource '{ReviewedCourseBanksResourceName}'.");
        return JsonSerializer.Deserialize<ReviewedCourseBankDataset>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize '{ReviewedCourseBanksResourceName}'.");
    }

    private static SupplementaryCourseQuestionsDataset LoadSupplementaryDataset()
    {
        var assembly = typeof(AcademicCourseBankImporter).Assembly;
        using var stream = assembly.GetManifestResourceStream(SupplementaryCourseQuestionsResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource '{SupplementaryCourseQuestionsResourceName}'.");
        return JsonSerializer.Deserialize<SupplementaryCourseQuestionsDataset>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize '{SupplementaryCourseQuestionsResourceName}'.");
    }

    private static void ValidateDatasets(
        ReviewedCourseBankDataset reviewed,
        SupplementaryCourseQuestionsDataset supplementary)
    {
        if (reviewed.Courses is null || reviewed.Courses.Count != 3)
        {
            throw new InvalidOperationException("ReviewedCourseBanks.json must contain exactly 3 courses.");
        }

        if (supplementary.Questions is null || supplementary.Questions.Count != 27)
        {
            throw new InvalidOperationException("SupplementaryCourseQuestions.json must contain exactly 27 questions.");
        }

        var allGuids = new HashSet<Guid>();
        void TrackGuid(Guid id, string label)
        {
            if (id == Guid.Empty || !allGuids.Add(id))
            {
                throw new InvalidOperationException($"Duplicate or empty GUID detected in embedded datasets: {id} ({label}).");
            }
        }

        foreach (var course in reviewed.Courses)
        {
            TrackGuid(course.Id, $"course:{course.CourseCode}");
            foreach (var clo in course.Clos)
            {
                TrackGuid(clo.Id, $"clo:{course.CourseCode}:{clo.Code}");
            }

            foreach (var q in course.Questions)
            {
                TrackGuid(q.Id, $"question:{q.Id}");
                ValidateQuestionAndRubric(q.Title, q.SampleAnswer, q.KeyPoints, q.ApprovalStatus, q.Source, q.ApprovedBy, q.SubmittedBy, q.Rubric, isSupplementary: false, q.ReviewNotes);
                TrackGuid(q.Rubric.Id, $"rubric:{q.Rubric.Id}");
                foreach (var c in q.Rubric.Criteria)
                {
                    TrackGuid(c.Id, $"criterion:{c.Id}");
                }
            }
        }

        foreach (var sq in supplementary.Questions)
        {
            TrackGuid(sq.Id, $"supp_question:{sq.SupplementaryCode}");
            ValidateQuestionAndRubric(sq.Title, sq.SampleAnswer, sq.KeyPoints, sq.ApprovalStatus, sq.Source, sq.ApprovedBy, sq.SubmittedBy, sq.Rubric, isSupplementary: true, sq.ReviewNotes);
            TrackGuid(sq.Rubric.Id, $"supp_rubric:{sq.SupplementaryCode}");
            foreach (var c in sq.Rubric.Criteria)
            {
                TrackGuid(c.Id, $"supp_criterion:{sq.SupplementaryCode}:{c.OrderIndex}");
            }
        }
    }

    private static void ValidateQuestionAndRubric(
        string title,
        string? sampleAnswer,
        IReadOnlyList<string> keyPoints,
        string approvalStatus,
        string source,
        Guid? approvedBy,
        Guid? submittedBy,
        ImportRubricDto rubric,
        bool isSupplementary,
        string? reviewNotes)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 300)
        {
            throw new InvalidOperationException($"Invalid question title length: '{title}'.");
        }

        if (string.IsNullOrWhiteSpace(sampleAnswer) || sampleAnswer.Length < (isSupplementary ? 150 : 50))
        {
            throw new InvalidOperationException($"Invalid sampleAnswer length on '{title}'.");
        }

        if (keyPoints is null || keyPoints.Count == 0)
        {
            throw new InvalidOperationException($"Question '{title}' must include KeyPoints.");
        }

        if (!string.Equals(approvalStatus, ApprovalStatus.Approved, StringComparison.Ordinal) ||
            !string.Equals(source, QuestionSource.Manual, StringComparison.Ordinal) ||
            approvedBy.HasValue ||
            submittedBy.HasValue)
        {
            throw new InvalidOperationException($"Question '{title}' violates seed approvalPolicy (must be manual/APPROVED with null ApprovedBy/SubmittedBy).");
        }

        if (isSupplementary && (string.IsNullOrWhiteSpace(reviewNotes) || !reviewNotes.Contains(SupplementarySourceMarker, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Supplementary question '{title}' is missing '{SupplementarySourceMarker}' in ReviewNotes.");
        }

        if (rubric.TotalMaxScore != 10.00m || rubric.Criteria.Count == 0)
        {
            throw new InvalidOperationException($"Rubric '{rubric.Name}' must have TotalMaxScore == 10.00 and at least 1 criterion.");
        }

        if (rubric.Criteria.Sum(c => c.MaxScore) != 10.00m || rubric.Criteria.Sum(c => c.Weight) != 1.00m)
        {
            throw new InvalidOperationException($"Rubric '{rubric.Name}' criteria sum must equal 10.00 score and 1.00 weight.");
        }

        foreach (var c in rubric.Criteria)
        {
            if (string.IsNullOrWhiteSpace(c.CriterionName) || c.CriterionName.Length > 200)
            {
                throw new InvalidOperationException($"CriterionName exceeds 200 chars on rubric '{rubric.Name}'.");
            }
        }
    }

    private sealed record NormalizedImportQuestion(
        Guid Id,
        string CloCode,
        string Title,
        string Content,
        string SampleAnswer,
        IReadOnlyList<string> KeyPoints,
        string Difficulty,
        string BloomLevel,
        bool HasFollowUp,
        string? FollowUpPrompt,
        string Source,
        string ApprovalStatus,
        string? ReviewNotes,
        bool IsActive,
        bool IsDeleted,
        DateTime CreatedAt,
        ImportRubricDto Rubric);

    private sealed class ReviewedCourseBankDataset
    {
        public string DatasetVersion { get; set; } = string.Empty;
        public List<ReviewedCourseDto> Courses { get; set; } = [];
    }

    private sealed class ReviewedCourseDto
    {
        public Guid Id { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public int Credits { get; set; } = 3;
        public CourseOfferingDto? CourseOffering { get; set; }
        public bool IsActive { get; set; } = true;
        public bool HasFollowUp { get; set; }
        public DateTime? CreatedAt { get; set; }
        public LegacyCourseDataDto? LegacyData { get; set; }
        public JsonElement? Terminology { get; set; }
        public List<ReviewedCloDto> Clos { get; set; } = [];
        public List<ReviewedQuestionDto> Questions { get; set; } = [];
    }

    private sealed class CourseOfferingDto
    {
        public string SemesterCode { get; set; } = "FA26";
        public bool IsEnabled { get; set; } = true;
    }

    private sealed class LegacyCourseDataDto
    {
        public string? ImporterReviewNotesMarker { get; set; }
        public List<Guid> CourseCloIds { get; set; } = [];
        public List<Guid> PracticeQuestionIds { get; set; } = [];
        public List<Guid> ExamQuestionIds { get; set; } = [];
        public List<Guid> RubricIds { get; set; } = [];
        public List<Guid> ExamStructureIds { get; set; } = [];
        public List<Guid> ExamSetIds { get; set; } = [];
    }

    private sealed class ReviewedCloDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string OralBankBloom { get; set; } = BloomLevel.Understand;
        public int OrderIndex { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? CreatedAt { get; set; }
    }

    private sealed class ReviewedQuestionDto
    {
        public Guid Id { get; set; }
        public string? ReviewId { get; set; }
        public string CloCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string SampleAnswer { get; set; } = string.Empty;
        public List<string> KeyPoints { get; set; } = [];
        public string Difficulty { get; set; } = QuestionDifficulty.Medium;
        public string BloomLevel { get; set; } = Domain.Enums.BloomLevel.Understand;
        public bool HasFollowUp { get; set; }
        public string? FollowUpPrompt { get; set; }
        public string Source { get; set; } = QuestionSource.Manual;
        public string ApprovalStatus { get; set; } = Domain.Enums.ApprovalStatus.Approved;
        public Guid? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public Guid? SubmittedBy { get; set; }
        public string? ReviewNotes { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; }
        public DateTime? CreatedAt { get; set; }
        public ImportRubricDto Rubric { get; set; } = new();
    }

    private sealed class SupplementaryCourseQuestionsDataset
    {
        public string DatasetVersion { get; set; } = string.Empty;
        public List<SupplementaryQuestionDto> Questions { get; set; } = [];
    }

    private sealed class SupplementaryQuestionDto
    {
        public Guid Id { get; set; }
        public string SupplementaryCode { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string CloCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string SampleAnswer { get; set; } = string.Empty;
        public List<string> KeyPoints { get; set; } = [];
        public string Difficulty { get; set; } = QuestionDifficulty.Hard;
        public string BloomLevel { get; set; } = Domain.Enums.BloomLevel.Analyze;
        public bool HasFollowUp { get; set; }
        public string? FollowUpPrompt { get; set; }
        public string Source { get; set; } = QuestionSource.Manual;
        public string ApprovalStatus { get; set; } = Domain.Enums.ApprovalStatus.Approved;
        public Guid? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public Guid? SubmittedBy { get; set; }
        public string? ReviewNotes { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; }
        public DateTime? CreatedAt { get; set; }
        public ImportRubricDto Rubric { get; set; } = new();
    }

    private sealed class ImportRubricDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal TotalMaxScore { get; set; } = 10.00m;
        public bool IsActive { get; set; } = true;
        public DateTime? CreatedAt { get; set; }
        public List<ImportRubricCriterionDto> Criteria { get; set; } = [];
    }

    private sealed class ImportRubricCriterionDto
    {
        public Guid Id { get; set; }
        public Guid RubricId { get; set; }
        public string CriterionName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal MaxScore { get; set; }
        public decimal Weight { get; set; }
        public string? BloomLevel { get; set; }
        public int OrderIndex { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
