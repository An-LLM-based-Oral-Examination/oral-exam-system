import { beforeEach, describe, expect, it, vi } from 'vitest'
import apiClient from './api.client'
import { practiceService } from './practice.service'

const courseId = '00000000-0000-4000-8000-000000000001'
const studentId = '00000000-0000-4000-8000-000000000002'
const sessionId = '00000000-0000-4000-8000-000000000003'
const questionId = '00000000-0000-4000-8000-000000000004'
const answerId = '00000000-0000-4000-8000-000000000005'

const question = {
  questionId,
  content: 'Explain dependency inversion with a concrete example.',
  imageUrl: null,
  imageAlt: null,
  rubricCriteria: ['Definition', 'Example'],
}

describe('practiceService MF-01 contract', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('starts a session with the selected practice mode and normalizes questions', async () => {
    const post = vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: { sessionId, transcriptBufferSeconds: 60, questions: [question] },
    } as never)

    const result = await practiceService.startSession({
      courseId,
      isFullSession: false,
      difficulty: 'medium',
      questionCount: null,
      studentId,
    })

    expect(post).toHaveBeenCalledWith('/practice/sessions', {
      courseId,
      isFullSession: false,
      difficulty: 'medium',
      questionCount: null,
      studentId,
    })
    expect(result).toMatchObject({ sessionId, transcriptBufferSeconds: 60 })
    expect(result.questions[0]).toMatchObject({ questionId, content: question.content })
  })

  it('requests the next question on-demand and preserves the anti-cheat query contract', async () => {
    const post = vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: {
        hasMoreQuestions: true,
        message: null,
        question: {
          id: questionId,
          content: question.content,
          difficulty: 'MEDIUM',
          questionOrder: 1,
          rubricCriteria: question.rubricCriteria,
        },
      },
    } as never)

    const result = await practiceService.getNextQuestion(sessionId, studentId)

    expect(post).toHaveBeenCalledWith(`/practice/sessions/${sessionId}/next-question`, undefined, {
      params: { studentId },
    })
    expect(result.question).toMatchObject({ id: questionId, difficulty: 'medium', questionOrder: 1 })
  })

  it('submits a transcript answer and rejects invalid identifiers before any request', async () => {
    const post = vi.spyOn(apiClient, 'post').mockResolvedValue({ data: { answerId } } as never)
    const payload = {
      questionId,
      studentId,
      answerText: 'Dependency inversion separates policy from implementation details.',
      isFollowUp: false,
      parentAnswerId: null,
    }

    await expect(practiceService.submitAnswer(sessionId, payload)).resolves.toEqual({ answerId })
    expect(post).toHaveBeenCalledWith(`/practice/sessions/${sessionId}/answers`, payload)

    await expect(
      practiceService.submitAnswer('not-a-guid', payload),
    ).rejects.toMatchObject({ status: 400 })
    expect(post).toHaveBeenCalledTimes(1)
  })

  it('requires HTTP 202 for full-session batch persistence and completes through the API', async () => {
    const post = vi
      .spyOn(apiClient, 'post')
      .mockResolvedValueOnce({ status: 202, data: null } as never)
      .mockResolvedValueOnce({ data: { message: 'Session completed.' } } as never)

    await practiceService.submitBatch(sessionId, {
      studentId,
      answers: [
        {
          questionId,
          answerText: 'A complete answer transcript with enough detail.',
          isFollowUp: false,
          parentAnswerId: null,
        },
      ],
    })
    await expect(practiceService.completeSession(sessionId, studentId)).resolves.toEqual({
      message: 'Session completed.',
    })

    expect(post).toHaveBeenNthCalledWith(1, `/practice/sessions/${sessionId}/batch-submit`, {
      studentId,
      answers: [
        {
          questionId,
          answerText: 'A complete answer transcript with enough detail.',
          isFollowUp: false,
          parentAnswerId: null,
        },
      ],
    })
    expect(post).toHaveBeenNthCalledWith(2, `/practice/sessions/${sessionId}/complete`, undefined, {
      params: { studentId },
    })
  })

  it('does not bypass the backend validation when the batch response is not accepted', async () => {
    vi.spyOn(apiClient, 'post').mockResolvedValue({ status: 200, data: null } as never)

    await expect(
      practiceService.submitBatch(sessionId, {
        studentId,
        answers: [
          {
            questionId,
            answerText: 'A complete answer transcript with enough detail.',
            isFollowUp: false,
          },
        ],
      }),
    ).rejects.toMatchObject({ status: 502 })
  })

  it('preserves the backend HTTP 410 timeout contract for an expired session', async () => {
    vi.spyOn(apiClient, 'post').mockRejectedValue({
      status: 410,
      title: 'Phiên luyện tập đã hết hạn',
      detail: 'Phiên không còn hoạt động do quá thời gian không tương tác.',
    })

    await expect(
      practiceService.submitAnswer(sessionId, {
        questionId,
        studentId,
        answerText: 'Dependency inversion separates policy from implementation details.',
        isFollowUp: false,
        parentAnswerId: null,
      }),
    ).rejects.toMatchObject({
      status: 410,
      detail: 'Phiên không còn hoạt động do quá thời gian không tương tác.',
    })
  })
})
