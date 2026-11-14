import { beforeEach, describe, expect, it, vi } from 'vitest'
import apiClient from './api.client'
import { appealsService } from './appeals.service'

const appealId = '00000000-0000-4000-8000-000000000010'
const ticketId = '00000000-0000-4000-8000-000000000011'
const lecturerId = '00000000-0000-4000-8000-000000000012'

function appealPayload(overrides: Record<string, unknown> = {}) {
  return {
    id: appealId,
    ticketId,
    studentId: '00000000-0000-4000-8000-000000000013',
    studentCode: 'SE170001',
    studentName: 'Le Vu Hoang',
    sessionId: '00000000-0000-4000-8000-000000000014',
    sessionTitle: 'PRN231 Oral Examination',
    submissionId: null,
    reason: 'The recorded answer was not fully considered.',
    status: 'PENDING',
    assignedTo: null,
    assignedToName: '',
    reviewedBy: null,
    reviewedByName: null,
    decision: null,
    originalScore: 7.5,
    proposedScore: null,
    reviewNotes: null,
    createdAt: '2026-11-14T01:00:00.000Z',
    updatedAt: '2026-11-14T01:00:00.000Z',
    resolvedAt: null,
    ...overrides,
  }
}

describe('appealsService MF-04 workflow contract', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('creates and lists student-owned appeals through the API', async () => {
    const post = vi.spyOn(apiClient, 'post').mockResolvedValue({ data: appealPayload() } as never)
    const get = vi.spyOn(apiClient, 'get').mockResolvedValue({
      data: [appealPayload()],
    } as never)

    await expect(
      appealsService.create({ ticketId, studentId: 'student-1', reason: 'Please review the answer.' }),
    ).resolves.toMatchObject({ id: appealId, status: 'PENDING' })
    await expect(appealsService.list({ status: 'PENDING' })).resolves.toHaveLength(1)

    expect(post).toHaveBeenCalledWith('/appeals', {
      ticketId,
      studentId: 'student-1',
      reason: 'Please review the answer.',
    })
    expect(get).toHaveBeenCalledWith('/appeals', { params: { status: 'PENDING' } })
  })

  it('keeps examination officer review and lecturer assignment on their dedicated endpoints', async () => {
    const put = vi.spyOn(apiClient, 'put').mockResolvedValue({
      data: appealPayload({ status: 'IN_REVIEW', reviewNotes: 'Eligible for regrade.' }),
    } as never)
    const post = vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: appealPayload({ status: 'IN_REVIEW', assignedTo: lecturerId, assignedToName: 'Qualified Lecturer' }),
    } as never)

    await appealsService.review(appealId, {
      decision: 'IN_REVIEW',
      reviewNotes: 'Eligible for regrade.',
    })
    await appealsService.assignLecturer(appealId, { lecturerId })

    expect(put).toHaveBeenCalledWith(`/appeals/${appealId}/review`, {
      decision: 'IN_REVIEW',
      reviewNotes: 'Eligible for regrade.',
    })
    expect(post).toHaveBeenCalledWith(
      `/examination-officer/appeals/${appealId}/assign-lecturer`,
      { lecturerId },
    )
  })

  it('supports proposal and batch assignment without changing the responsible role', async () => {
    const get = vi.spyOn(apiClient, 'get').mockResolvedValue({
      data: {
        totalAppeals: 1,
        totalLecturers: 1,
        exceedsStandardQuota: false,
        lecturerSummaries: [{ lecturerId, lecturerName: 'Qualified Lecturer', email: 'lecturer@test', assignedCount: 0 }],
        items: [{
          appealId,
          ticketId,
          studentCode: 'SE170001',
          studentName: 'Le Vu Hoang',
          sessionId: 'session-1',
          sessionTitle: 'PRN231 Oral Examination',
          initialExaminerUserId: null,
          initialExaminerName: null,
          proposedLecturerId: lecturerId,
          proposedLecturerName: 'Qualified Lecturer',
          originalScore: 7.5,
          reason: 'Please review the answer.',
        }],
      },
    } as never)
    const post = vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: { totalAssigned: 1, assignedCount: 1, failedCount: 0, assignedAppeals: [appealPayload({ assignedTo: lecturerId })] },
    } as never)

    await expect(appealsService.getAllocationProposal('session-1')).resolves.toMatchObject({
      totalAppeals: 1,
      items: [{ proposedLecturerId: lecturerId }],
    })
    await expect(appealsService.batchAssign({ assignments: [{ appealId, lecturerId }] })).resolves.toMatchObject({
      assignedCount: 1,
    })

    expect(get).toHaveBeenCalledWith('/examination-officer/appeals/propose-allocation', {
      params: { sessionId: 'session-1' },
    })
    expect(post).toHaveBeenCalledWith('/examination-officer/appeals/batch-assign', {
      assignments: [{ appealId, lecturerId }],
    })
  })

  it('keeps lecturer regrade as the final grading input before examination-officer decision', async () => {
    const put = vi.spyOn(apiClient, 'put').mockResolvedValue({
      data: appealPayload({ status: 'REGRADED_BY_LECTURER', proposedScore: 8.25 }),
    } as never)

    await expect(
      appealsService.regrade(appealId, { newScore: 8.25, reviewNotes: 'Independent evidence review completed.' }),
    ).resolves.toMatchObject({ status: 'REGRADED_BY_LECTURER', proposedScore: 8.25 })
    expect(put).toHaveBeenCalledWith(`/appeals/${appealId}/regrade`, {
      newScore: 8.25,
      reviewNotes: 'Independent evidence review completed.',
    })
  })
})
