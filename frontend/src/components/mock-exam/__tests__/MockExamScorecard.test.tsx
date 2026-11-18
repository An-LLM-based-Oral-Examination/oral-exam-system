import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { MockExamScorecard } from '../MockExamScorecard'
import apiClient from '@/services/api.client'
import { mockExamService } from '@/services/mockExam.service'
import type { MockExamScorecard as MockExamScorecardData } from '@/types/mockExam.types'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

const basePendingScorecard: MockExamScorecardData = {
  sessionId: '11111111-1111-4111-8111-111111111111',
  courseId: '22222222-2222-4222-8222-222222222222',
  courseCode: 'PRN231',
  courseName: 'Building Cross-Platform Back-End Application With .NET',
  totalScore: 0,
  classification: 'Đang chờ chấm điểm',
  isGradingPending: true,
  totalQuestions: 2,
  answeredQuestions: 1,
  durationMinutes: 20,
  isOverdue: false,
  startedAt: '2026-11-18T08:00:00Z',
  submittedAt: '2026-11-18T08:15:00Z',
  questions: [
    {
      questionId: '33333333-3333-4333-8333-333333333333',
      orderIndex: 1,
      title: 'CQRS Pattern',
      content: 'Giải thích cơ chế tách đọc và ghi trong CQRS.',
      bloomLevel: 'Understand',
      difficulty: 'medium',
      transcript: 'CQRS tách riêng mô hình Command ghi dữ liệu và Query truy vấn dữ liệu.',
      score: 0,
      feedback: null,
      confidenceScore: null,
      criteriaScores: [],
      isFollowUp: false,
      answerId: '44444444-4444-4444-8444-444444444444',
    },
    {
      questionId: '55555555-5555-4555-8555-555555555555',
      orderIndex: 2,
      title: 'Outbox Pattern',
      content: 'Khi nào cần dùng Transactional Outbox?',
      bloomLevel: 'Apply',
      difficulty: 'hard',
      transcript: null,
      score: 0,
      feedback: null,
      confidenceScore: null,
      criteriaScores: [],
      isFollowUp: false,
      answerId: null,
    },
  ],
}

describe('MockExamScorecard deferred grading UI', () => {
  it('displays "Chờ chấm" instead of "0.00 / 10" and calm pending notice when isGradingPending is true', () => {
    render(<MockExamScorecard scorecard={basePendingScorecard} />)

    expect(screen.getByText('Chờ chấm')).toBeInTheDocument()
    expect(screen.getByText('Đang chờ chấm điểm')).toBeInTheDocument()
    expect(
      screen.getByText(
        /Bài thi thử đã được lưu an toàn\. Hệ thống chấm điểm AI đang xử lý tự động/,
      ),
    ).toBeInTheDocument()
    expect(screen.queryByText('0.00')).toBeNull()

    // Câu 1 đã trả lời nhưng chưa có đánh giá AI -> hiện trạng thái chờ chấm thay vì 0.0/10
    expect(screen.getAllByText('Đang chờ hệ thống chấm điểm tự động').length).toBeGreaterThanOrEqual(
      1,
    )
    expect(screen.queryByText(/Độ tin cậy 100%/)).toBeNull()
    expect(
      screen.getByText(
        'CQRS tách riêng mô hình Command ghi dữ liệu và Query truy vấn dữ liệu.',
      ),
    ).toBeInTheDocument()
  })

  it('still treats answered question as pending when ungraded zero-score rubric criteria placeholders are present', () => {
    const scorecardWithZeroPlaceholders: MockExamScorecardData = {
      ...basePendingScorecard,
      questions: [
        {
          ...basePendingScorecard.questions[0],
          criteriaScores: [
            {
              criterionId: 'c1',
              criterionName: 'CQRS Separation',
              bloomLevel: 'Understand',
              maxScore: 5,
              score: 0,
              comment: '',
            },
          ],
        },
      ],
    }

    render(<MockExamScorecard scorecard={scorecardWithZeroPlaceholders} />)
    expect(screen.getAllByText('Đang chờ hệ thống chấm điểm tự động').length).toBeGreaterThanOrEqual(
      1,
    )
  })

  it('renders numeric total score and question scores normally when grading is completed', () => {
    const gradedScorecard: MockExamScorecardData = {
      ...basePendingScorecard,
      totalScore: 8.5,
      classification: 'Giỏi',
      isGradingPending: false,
      questions: [
        {
          ...basePendingScorecard.questions[0],
          score: 8.5,
          feedback: 'Trình bày rõ ràng bản chất CQRS.',
        },
      ],
    }

    render(<MockExamScorecard scorecard={gradedScorecard} />)

    expect(screen.getByText('8.50')).toBeInTheDocument()
    expect(screen.getByText('Giỏi')).toBeInTheDocument()
    expect(screen.queryByText('Chờ chấm')).toBeNull()
    expect(screen.getByText('8.5/10')).toBeInTheDocument()
  })

  it('normalizes isGradingPending from HTTP 202 submitSession response', async () => {
    vi.spyOn(apiClient, 'post').mockResolvedValue({
      status: 202,
      data: {
        sessionId: '11111111-1111-4111-8111-111111111111',
        courseId: '22222222-2222-4222-8222-222222222222',
        courseCode: 'PRN231',
        courseName: 'Back-End .NET',
        totalScore: 0,
        classification: 'Đang chờ chấm điểm',
        IsGradingPending: true,
        totalQuestions: 1,
        answeredQuestions: 1,
        durationMinutes: 20,
        isOverdue: false,
        startedAt: '2026-11-18T08:00:00Z',
        submittedAt: '2026-11-18T08:15:00Z',
        questions: [],
      },
    } as never)

    const result = await mockExamService.submitSession('11111111-1111-4111-8111-111111111111')
    expect(result.isGradingPending).toBe(true)
    expect(result.classification).toBe('Đang chờ chấm điểm')
  })
})
