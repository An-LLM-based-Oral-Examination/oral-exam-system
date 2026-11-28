import { beforeEach, describe, expect, it, vi } from 'vitest'
import apiClient from '../api.client'
import { questionService } from '../question.service'

describe('questionService batchApprove & batchApprovePendingQuestions', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('loads the current approver list from my-approved with filters and cancellation', async () => {
    const data = { items: [], totalCount: 0, pageNumber: 1, pageSize: 12, totalPages: 0 }
    const get = vi.spyOn(apiClient, 'get').mockResolvedValue({ data })
    const signal = new AbortController().signal
    const filters = { courseId: 'course-1', usageScope: 'exam' as const, search: 'graph', pageNumber: 1 }
    const result = await questionService.getMyApprovedQuestions(filters, signal)
    expect(get).toHaveBeenCalledWith('/questions/my-approved', { params: filters, signal })
    expect(result.items).toEqual([])
    expect(result.totalCount).toBe(0)
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
          imageUrl: '/uploads/questions/202611/solid.png',
          imageDescription: 'SOLID diagram',
          isPaperPrivate: true,
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
          source: 'manual',
          imageUrl: '/uploads/questions/202611/solid.png',
          isPaperPrivate: true,
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

  it('updateDraft sends PUT /questions/{id} with UpdateQuestionDraftRequest payload', async () => {
    const put = vi.spyOn(apiClient, 'put').mockResolvedValue({
      data: { questionId: 'q-draft-1' },
    } as never)

    const payload = {
      title: 'Giải thích Clean Architecture',
      content: 'Trình bày quy tắc phụ thuộc giữa các tầng trong Clean Architecture.',
      sampleAnswer:
        'Clean Architecture tổ chức phụ thuộc hướng vào trong về tầng Domain, tách biệt hoàn toàn UI và Infrastructure.',
      keyPoints: ['Dependency Rule', 'Domain Isolation'],
      difficulty: 'medium' as const,
      bloomLevel: 'Understand' as const,
      cloId: 'clo-1',
      imageUrl: '/uploads/questions/202611/clean-arch.png',
      imageDescription: 'Sơ đồ Clean Architecture',
      clearImage: false,
      rubric: {
        name: 'Rubric Clean Architecture',
        description: 'Đánh giá hiểu biết phân tầng',
        totalMaxScore: 10,
        criteria: [
          {
            criterionName: 'Dependency Rule',
            maxScore: 5,
            weight: 0.5,
            bloomLevel: 'Understand' as const,
            description: 'Nêu đúng chiều phụ thuộc',
            orderIndex: 1,
          },
          {
            criterionName: 'Layer Responsibilities',
            maxScore: 5,
            weight: 0.5,
            bloomLevel: 'Understand' as const,
            description: 'Phân biệt đúng 4 tầng',
            orderIndex: 2,
          },
        ],
      },
    }

    const res = await questionService.updateDraft('q-draft-1', payload)

    expect(put).toHaveBeenCalledWith('/questions/q-draft-1', payload)
    expect(res).toEqual({ questionId: 'q-draft-1' })
  })

  it('getQuestionById normalizes imageUrl and imageDescription (empty string -> null)', async () => {
    vi.spyOn(apiClient, 'get')
      .mockResolvedValueOnce({
        data: {
          id: 'q-img-1',
          title: 'Question with image',
          imageUrl: '/uploads/questions/202611/a.png',
          ImageDescription: 'Sơ đồ lớp',
          rubric: { id: 'r-1', name: 'Rubric', description: null, totalMaxScore: 10, criteria: [] },
        },
      } as never)
      .mockResolvedValueOnce({
        data: {
          id: 'q-img-2',
          title: 'Question blank desc',
          imageUrl: '/uploads/questions/202611/b.png',
          imageDescription: '   ',
          rubric: { id: 'r-2', name: 'Rubric', description: null, totalMaxScore: 10, criteria: [] },
        },
      } as never)

    const first = await questionService.getQuestionById('q-img-1')
    const second = await questionService.getQuestionById('q-img-2')

    expect(first.imageUrl).toBe('/uploads/questions/202611/a.png')
    expect(first.imageDescription).toBe('Sơ đồ lớp')
    expect(second.imageDescription).toBeNull()
  })

  it('setQuestionActive sends PATCH /questions/{id}/active with { isActive }', async () => {
    const patch = vi.spyOn(apiClient, 'patch').mockResolvedValue({ data: null } as never)

    await questionService.setQuestionActive('q-approved-1', false)
    expect(patch).toHaveBeenNthCalledWith(1, '/questions/q-approved-1/active', {
      isActive: false,
    })

    await questionService.setQuestionActive('q-approved-1', true)
    expect(patch).toHaveBeenNthCalledWith(2, '/questions/q-approved-1/active', {
      isActive: true,
    })
  })

  it('deleteDraft sends DELETE /questions/{id}', async () => {
    const del = vi.spyOn(apiClient, 'delete').mockResolvedValue({ data: null } as never)

    await questionService.deleteDraft('q-draft-99')

    expect(del).toHaveBeenCalledWith('/questions/q-draft-99')
  })

  it('generateFromFlm and generateFromClo override Axios timeout to 90s for synchronous AI generation', async () => {
    const post = vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: {
        courseId: 'course-1',
        courseCode: 'PRN231',
        generatedCount: 0,
        source: 'flm_api',
        questions: [],
      },
    } as never)

    const payload = {
      courseId: 'course-1',
      selectedCloCodes: ['CLO1'],
      topics: ['Clean Architecture'],
      numberOfQuestions: 5,
      usageScope: 'practice' as const,
      targetBloomLevels: ['Understand' as const],
      difficultyDistribution: {
        easy: 2,
        medium: 2,
        hard: 1,
      },
    }

    await questionService.generateFromFlm(payload)
    await questionService.generateFromClo(payload)

    expect(post).toHaveBeenNthCalledWith(
      1,
      '/questions/generate-from-flm',
      expect.objectContaining({ courseId: 'course-1', usageScope: 'practice' }),
      { timeout: 90_000 },
    )
    expect(post).toHaveBeenNthCalledWith(
      2,
      '/questions/generate-from-clo',
      expect.objectContaining({ courseId: 'course-1', usageScope: 'practice' }),
      { timeout: 90_000 },
    )
  })
})
