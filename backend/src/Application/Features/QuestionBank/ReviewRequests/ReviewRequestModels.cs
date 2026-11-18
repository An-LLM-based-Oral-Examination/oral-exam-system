using System.Text.Json;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.QuestionBank.ReviewRequests;

public sealed record ReviewCriterion(string Name, string? Description, decimal MaxScore, string? BloomLevel,
    Guid? Id = null, decimal? Weight = null, int? OrderIndex = null)
{
    public bool Matches(ReviewCriterion other) =>
        (this with { Id = Id ?? other.Id, Weight = Weight ?? other.Weight, OrderIndex = OrderIndex ?? other.OrderIndex }) == other;
}
public sealed record ReviewQuestionSnapshot(
    string Title, string Content, string? SampleAnswer, string Difficulty, string BloomLevel,
    Guid? CloId, string? CloCode, string? ImageUrl, string? ImageDescription, string Source,
    List<ReviewCriterion> Criteria, string? RubricName = null, Guid? RubricId = null,
    string? RubricDescription = null, decimal? RubricTotalMaxScore = null,
    string? KeyPointsJson = null, DateTime? CreatedAt = null)
{
    public static ReviewQuestionSnapshot From(PracticeQuestion q) =>
        Create(q.Title, q.Content, q.SampleAnswer, q.Difficulty, q.BloomLevel, q.CloId, q.Clo?.CloCode,
            q.ImageUrl, q.ImageDescription, q.Source, q.Rubric, q.KeyPoints, q.CreatedAt);

    public static ReviewQuestionSnapshot From(ExamQuestion q) =>
        Create(q.Title, q.Content, q.SampleAnswer, q.Difficulty, q.BloomLevel, q.CloId, q.Clo?.CloCode,
            q.ImageUrl, q.ImageDescription, q.Source, q.Rubric, q.KeyPoints, q.CreatedAt);

    private static ReviewQuestionSnapshot Create(string title, string content, string? answer,
        string difficulty, string bloom, Guid? cloId, string? cloCode, string? image, string? imageDescription,
        string source, Rubric? rubric, string keyPointsJson, DateTime createdAt) =>
        new(title, content, answer, difficulty, bloom, cloId, cloCode, image, imageDescription, source,
            rubric?.Criteria.OrderBy(c => c.OrderIndex).ThenBy(c => c.Id)
                .Select(c => new ReviewCriterion(c.CriterionName, c.Description, c.MaxScore, c.BloomLevel,
                    c.Id, c.Weight, c.OrderIndex)).ToList() ?? [],
            rubric?.Name, rubric?.Id, rubric?.Description, rubric?.TotalMaxScore, keyPointsJson, createdAt);

    public string ToJson() => JsonSerializer.Serialize(this);
    public bool Matches(ReviewQuestionSnapshot other) =>
        (this with
        {
            Criteria = other.Criteria,
            RubricName = RubricName ?? other.RubricName,
            RubricId = RubricId ?? other.RubricId,
            RubricDescription = RubricId is null ? other.RubricDescription : RubricDescription,
            RubricTotalMaxScore = RubricTotalMaxScore ?? other.RubricTotalMaxScore,
            KeyPointsJson = KeyPointsJson ?? other.KeyPointsJson,
            CreatedAt = CreatedAt ?? other.CreatedAt
        }) == other
        && Criteria.Count == other.Criteria.Count
        && Criteria.Zip(other.Criteria).All(pair => pair.First.Matches(pair.Second));
    public static ReviewQuestionSnapshot? Parse(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<ReviewQuestionSnapshot>(json);
}

public sealed record ReviewRequestSummary(Guid Id, Guid CourseId, string CourseCode, string CourseName,
    string LecturerName, string LecturerEmail, DateTime? SubmittedAt, string? Notes,
    string Status, int Total, int Pending, int Approved, int NeedsRevision, int Rejected, long Version);
public sealed record ReviewRequestItemDto(Guid Id, Guid QuestionId, string UsageScope, string Decision,
    string? ReviewNotes, DateTime? ReviewedAt, string? ReviewerName, ReviewQuestionSnapshot? Question);
public sealed record ReviewRequestDetail(ReviewRequestSummary Summary, List<ReviewRequestItemDto> Items);
public sealed record ReviewRequestPage(List<ReviewRequestSummary> Items, int TotalCount, int Page, int PageSize);
public sealed record ReviewRequestDecision(List<Guid> ItemIds, string Decision, string? Notes, long Version);
