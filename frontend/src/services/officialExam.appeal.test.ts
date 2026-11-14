import { afterEach, describe, expect, it, vi } from 'vitest'
import apiClient from './api.client'
import { officialExamService } from './officialExam.service'

afterEach(() => vi.restoreAllMocks())

describe('official grade appeal contract', () => {
  it('preserves server deadline and independent acknowledgement permission', async () => {
    vi.spyOn(apiClient, 'get').mockResolvedValue({ data: {
      ticketId: 'ticket', studentCode: 'SE123456', fullName: 'QA', courseCode: 'CSD201',
      status: 'PUBLISHED', isLocked: true, totalScore: 8,
      publishedAt: '2026-11-14T00:00:00Z', publishedBy: 'QA',
      studentAcknowledgementStatus: 'PENDING', detailedScorecard: [],
      internalAppeal: { canAppeal: false, instruction: 'Đã hết hạn phúc khảo.',
        appealDeadlineDays: 3, appealDeadlineAtUtc: '2026-11-17T00:00:00Z',
        canAcknowledge: true, appealEndpoint: '/api/v1/appeals' },
    } })
    const grade = await officialExamService.getStudentGrade('ticket')
    expect(grade.internalAppeal).toMatchObject({
      appealDeadlineAtUtc: '2026-11-17T00:00:00Z', canAcknowledge: true, canAppeal: false,
    })
  })
})
