using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetBankCurationCandidates;

public sealed class GetBankCurationCandidatesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetBankCurationCandidatesQuery, Result<BankCurationCandidatesResponseDto>>
{
    public async Task<Result<BankCurationCandidatesResponseDto>> Handle(
        GetBankCurationCandidatesQuery request,
        CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveClaimedActorAsync(
                context, request.ActorUserId, request.ActorRole, UserRole.DepartmentHead, cancellationToken))
        {
            return Result<BankCurationCandidatesResponseDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var course = await context.Courses
            .AsNoTracking()
            .Include(c => c.AcademicProgram)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId && c.IsActive, cancellationToken);

        if (course is null)
        {
            return Result<BankCurationCandidatesResponseDto>.Failure(OfficialExamErrors.CourseNotFound);
        }

        if (!AcademicCourseOwnership.IsOwnedBy(course, request.ActorUserId))
        {
            return Result<BankCurationCandidatesResponseDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var poolQuestions = await context.ExamQuestions
            .AsNoTracking()
            .Where(q =>
                q.CourseId == request.CourseId
                && !q.IsPaperCopy
                && !q.IsDeleted
                && q.ApprovalStatus == ApprovalStatus.Approved
                && (q.BankCurationStatus == BankCurationStatus.PendingCuration
                    || (q.IsActive
                        && (q.BankCurationStatus == BankCurationStatus.BankReady
                            || q.BankCurationStatus == BankCurationStatus.CuratedIntoBank))))
            .Select(q => new
            {
                q.Id,
                q.OriginQuestionId,
                q.Title,
                q.Content,
                q.Difficulty,
                q.BloomLevel,
                q.CloId,
                CloCode = q.Clo != null ? q.Clo.CloCode : null,
                q.ApprovalStatus,
                q.BankCurationStatus,
                q.IsActive,
                q.SubmittedBy,
                SubmittedByName = q.SubmittedByUser != null ? q.SubmittedByUser.FullName : null,
                q.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var pendingCandidates = poolQuestions
            .Where(q => q.BankCurationStatus == BankCurationStatus.PendingCuration)
            .ToList();

        if (pendingCandidates.Count == 0)
        {
            return Result<BankCurationCandidatesResponseDto>.Success(new BankCurationCandidatesResponseDto
            {
                CourseId = course.Id,
                CourseCode = course.Code,
                TotalPendingCount = 0,
                RedCount = 0,
                YellowCount = 0,
                ClearCount = 0,
                Items = []
            });
        }

        var allPoolIdSet = poolQuestions.Select(q => q.Id).ToHashSet();
        var originToPoolIds = poolQuestions
            .Where(q => q.OriginQuestionId.HasValue)
            .GroupBy(q => q.OriginQuestionId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var rawSetCodeRows = await context.ExamSetQuestions
            .AsNoTracking()
            .Where(esq =>
                esq.ExamSet.CourseId == request.CourseId
                && esq.ExamSet.IsActive
                && !esq.ExamSet.IsSampleSet)
            .Select(esq => new
            {
                esq.ExamQuestionId,
                esq.SourceQuestionId,
                OriginQuestionId = esq.ExamQuestion.OriginQuestionId,
                esq.ExamSet.SetCode
            })
            .ToListAsync(cancellationToken);

        var setCodesMap = new Dictionary<Guid, HashSet<string>>();
        foreach (var row in rawSetCodeRows)
        {
            var matchedPoolIds = new List<Guid>();
            if (allPoolIdSet.Contains(row.ExamQuestionId)) matchedPoolIds.Add(row.ExamQuestionId);
            if (row.SourceQuestionId.HasValue && allPoolIdSet.Contains(row.SourceQuestionId.Value)) matchedPoolIds.Add(row.SourceQuestionId.Value);
            if (row.OriginQuestionId.HasValue && allPoolIdSet.Contains(row.OriginQuestionId.Value)) matchedPoolIds.Add(row.OriginQuestionId.Value);
            var canonicalOrigin = row.SourceQuestionId ?? row.OriginQuestionId ?? row.ExamQuestionId;
            if (originToPoolIds.TryGetValue(canonicalOrigin, out var promotedIds))
            {
                matchedPoolIds.AddRange(promotedIds);
            }

            foreach (var poolId in matchedPoolIds.Distinct())
            {
                if (!setCodesMap.TryGetValue(poolId, out var codes))
                {
                    codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    setCodesMap[poolId] = codes;
                }
                codes.Add(row.SetCode);
            }
        }

        var setCodesByQuestion = setCodesMap.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList());

        var trigramsByQuestion = poolQuestions.ToDictionary(
            q => q.Id,
            q => TrigramSimilarityCalculator.ExtractTrigrams(
                TrigramSimilarityCalculator.ComposeQuestionText(q.Title, q.Content)));

        var promotedOriginIds = poolQuestions
            .Where(q => q.OriginQuestionId.HasValue)
            .Select(q => q.OriginQuestionId!.Value)
            .ToHashSet();

        var existingBankQuestions = poolQuestions
            .Where(q =>
                !promotedOriginIds.Contains(q.Id)
                && q.IsActive
                && q.BankCurationStatus is BankCurationStatus.BankReady or BankCurationStatus.CuratedIntoBank)
            .ToList();

        var allCandidateDtos = new List<BankCurationCandidateDto>(pendingCandidates.Count);

        foreach (var candidate in pendingCandidates)
        {
            var candidateTrigrams = trigramsByQuestion[candidate.Id];
            var matches = new List<BankCurationSimilarityMatchDto>();
            var maxSimilarity = 0m;

            // 1. Đối chiếu với toàn bộ câu hỏi đã có trong kho chọn chung (BANK_READY / CURATED_INTO_BANK)
            foreach (var bankQuestion in existingBankQuestions)
            {
                var similarity = TrigramSimilarityCalculator.CalculateSimilarity(
                    candidateTrigrams,
                    trigramsByQuestion[bankQuestion.Id]);
                if (similarity > maxSimilarity)
                {
                    maxSimilarity = similarity;
                }

                var similarityPercent = TrigramSimilarityCalculator.ToSimilarityPercent(similarity);
                var band = TrigramSimilarityCalculator.ClassifyBand(similarity);
                if (band != TrigramSimilarityCalculator.BandClear)
                {
                    matches.Add(new BankCurationSimilarityMatchDto
                    {
                        ComparedQuestionId = bankQuestion.Id,
                        Title = bankQuestion.Title,
                        Content = bankQuestion.Content,
                        Difficulty = bankQuestion.Difficulty,
                        BloomLevel = bankQuestion.BloomLevel,
                        BankCurationStatus = bankQuestion.BankCurationStatus,
                        IsFromExistingBank = true,
                        SourceSetCodes = setCodesByQuestion.TryGetValue(bankQuestion.Id, out var bankSets) ? bankSets : [],
                        SimilarityScore = similarity,
                        SimilarityPercent = similarityPercent,
                        Band = band
                    });
                }
            }

            // 2. Đối chiếu chéo với các câu hỏi tạo tay PENDING_CURATION khác trong cùng môn
            foreach (var otherCandidate in pendingCandidates)
            {
                if (otherCandidate.Id == candidate.Id)
                {
                    continue;
                }

                var similarity = TrigramSimilarityCalculator.CalculateSimilarity(
                    candidateTrigrams,
                    trigramsByQuestion[otherCandidate.Id]);
                if (similarity > maxSimilarity)
                {
                    maxSimilarity = similarity;
                }

                var similarityPercent = TrigramSimilarityCalculator.ToSimilarityPercent(similarity);
                var band = TrigramSimilarityCalculator.ClassifyBand(similarity);
                if (band != TrigramSimilarityCalculator.BandClear)
                {
                    matches.Add(new BankCurationSimilarityMatchDto
                    {
                        ComparedQuestionId = otherCandidate.Id,
                        Title = otherCandidate.Title,
                        Content = otherCandidate.Content,
                        Difficulty = otherCandidate.Difficulty,
                        BloomLevel = otherCandidate.BloomLevel,
                        BankCurationStatus = otherCandidate.BankCurationStatus,
                        IsFromExistingBank = false,
                        SourceSetCodes = setCodesByQuestion.TryGetValue(otherCandidate.Id, out var peerSets) ? peerSets : [],
                        SimilarityScore = similarity,
                        SimilarityPercent = similarityPercent,
                        Band = band
                    });
                }
            }

            matches = matches
                .OrderByDescending(m => m.SimilarityScore)
                .ThenBy(m => m.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var highestBand = TrigramSimilarityCalculator.ClassifyBand(maxSimilarity);

            allCandidateDtos.Add(new BankCurationCandidateDto
            {
                QuestionId = candidate.Id,
                Title = candidate.Title,
                Content = candidate.Content,
                Difficulty = candidate.Difficulty,
                BloomLevel = candidate.BloomLevel,
                CloId = candidate.CloId,
                CloCode = candidate.CloCode,
                ApprovalStatus = candidate.ApprovalStatus,
                BankCurationStatus = candidate.BankCurationStatus,
                SubmittedBy = candidate.SubmittedBy,
                SubmittedByName = candidate.SubmittedByName,
                SourceSetCodes = setCodesByQuestion.TryGetValue(candidate.Id, out var ownSets) ? ownSets : [],
                CreatedAt = candidate.CreatedAt,
                MaxSimilarityScore = maxSimilarity,
                MaxSimilarityPercent = TrigramSimilarityCalculator.ToSimilarityPercent(maxSimilarity),
                HighestBand = highestBand,
                Matches = matches
            });
        }

        var redCount = allCandidateDtos.Count(c => c.HighestBand == TrigramSimilarityCalculator.BandRed80Plus);
        var yellowCount = allCandidateDtos.Count(c => c.HighestBand == TrigramSimilarityCalculator.BandYellow70To79);
        var clearCount = allCandidateDtos.Count(c => c.HighestBand == TrigramSimilarityCalculator.BandClear);

        var orderedItems = allCandidateDtos
            .OrderBy(c => c.HighestBand switch
            {
                TrigramSimilarityCalculator.BandRed80Plus => 0,
                TrigramSimilarityCalculator.BandYellow70To79 => 1,
                _ => 2
            })
            .ThenByDescending(c => c.MaxSimilarityScore)
            .ThenByDescending(c => c.CreatedAt)
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.Band)
            && !string.Equals(request.Band.Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var rawBand = request.Band.Trim().ToUpperInvariant();
            var normalizedBand = rawBand switch
            {
                "RED" => TrigramSimilarityCalculator.BandRed80Plus,
                "YELLOW" => TrigramSimilarityCalculator.BandYellow70To79,
                _ => rawBand
            };
            orderedItems = orderedItems
                .Where(c => string.Equals(c.HighestBand, normalizedBand, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return Result<BankCurationCandidatesResponseDto>.Success(new BankCurationCandidatesResponseDto
        {
            CourseId = course.Id,
            CourseCode = course.Code,
            TotalPendingCount = allCandidateDtos.Count,
            RedCount = redCount,
            YellowCount = yellowCount,
            ClearCount = clearCount,
            Items = orderedItems
        });
    }
}
