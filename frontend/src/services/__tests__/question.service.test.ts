import { beforeEach, describe, expect, it, vi } from 'vitest'
import apiClient from '../api.client'
import { questionService } from '../question.service'

describe('questionService batchApprove & batchApprovePendingQuestions', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('batchApprove posts BatchApproveQuestionsRequestDto { courseId, questions } to /questions/batch-approve', async () => {
    const post = vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: {
        courseId: 'course-1',
        totalApproved: 1,
        approvedQuestions: [],
      },
    } as never)

    const result = await questionService.batchApprove({
      courseId: 'course-1',
      approverId: 'dept-head-1',
      questions: [
        {
          title: 'What is SOLID?',
          content: 'Explain the five SOLID principles in OOP.',
          difficulty: 'medium',
          bloomLevel: 'Understand',
          usageScope: 'practice',
          source: 'manual',
          modelAnswer: 'Single Responsibility, Open-Closed, Liskov Substitution, Interface Segregation, Dependency Inversion.',
          keyPoints: ['SRP', 'OCP', 'LSP', 'ISP', 'DIP'],
          hasFollowUp: false,
          rubric: {
            name: 'Rubric SOLID',
            totalMaxScore: 10,
            criteria: [
              {
                criterionName: 'Explanation',
                maxScore: 10,
                weight: 100,
                bloomLevel: 'Understand',
                description: 'Explains all principles accurately.',
              },
            ],
          },
        },
      ],
    })

    expect(post).toHaveBeenCalledWith('/questions/batch-approve', {
      courseId: 'course-1',
      approverId: 'dept-head-1',
      questions: [
        expect.objectContaining({
          title: 'What is SOLID?',
          usageScope: 'practice',
        }),
      ],
    })
    expect(result).toEqual({ approvedCount: 1 })
  })

  it('batchApprovePendingQuestions transitions each pending question via /questions/{id}/review-decision', async () => {
    const post = vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: {
        id: 'q-1',
        title: 'Question 1',
        rubric: { id: 'r-1', name: 'R', description: null, totalMaxScore: 10, criteria: [] },
      },
    } as never)

    const result = await questionService.batchApprovePendingQuestions(['q-1', 'q-2', 'q-1'], {
      reviewerId: 'dh-1',
    })

    expect(post).toHaveBeenCalledTimes(2)
    expect(post).toHaveBeenNthCalledWith(1, '/questions/q-1/review-decision', {
      decision: 'APPROVED',
      reviewNotes: 'Câu hỏi và Barem Rubric 10.0 điểm đạt chuẩn thẩm định của Bộ môn.',
      reviewerId: 'dh-1',
    })
    expect(post).toHaveBeenNthCalledWith(2, '/questions/q-2/review-decision', {
      decision: 'APPROVED',
      reviewNotes: 'Câu hỏi và Barem Rubric 10.0 điểm đạt chuẩn thẩm định của Bộ môn.',
      reviewerId: 'dh-1',
    })
    expect(result).toEqual({
      approvedCount: 2,
      approvedIds: ['q-1', 'q-2'],
      failed: [],
    })
  })

  it('batchApprovePendingQuestions continues after per-item failures and reports partial results', async () => {
    const post = vi
      .spyOn(apiClient, 'post')
      .mockResolvedValueOnce({
        data: {
          id: 'q-1',
          title: 'Question 1',
          rubric: { id: 'r-1', name: 'R', description: null, totalMaxScore: 10, criteria: [] },
        },
      } as never)
      .mockRejectedValueOnce({
        status: 403,
        detail: 'FORBIDDEN: Không được tự phê duyệt câu hỏi của mình.',
      })
      .mockResolvedValueOnce({
        data: {
          id: 'q-3',
          title: 'Question 3',
          rubric: { id: 'r-3', name: 'R', description: null, totalMaxScore: 10, criteria: [] },
        },
      } as never)

    const result = await questionService.batchApprovePendingQuestions(['q-1', 'q-2', 'q-3'])

    expect(post).toHaveBeenCalledTimes(3)
    expect(result).toEqual({
      approvedCount: 2,
      approvedIds: ['q-1', 'q-3'],
      failed: [
        {
          id: 'q-2',
          message: 'Không được tự phê duyệt câu hỏi của mình.',
        },
      ],
    })
  })
})
