namespace OralExamination.Application.Common.Models;

/// <summary>
/// Deliberately contains identifiers only. The worker reloads authoritative content
/// from PostgreSQL, which keeps transcripts out of queue payloads and makes replay safe.
/// </summary>
public sealed record OfficialExamGradingTask(Guid JobId, Guid SubmissionId);
