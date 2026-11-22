import { beforeEach, describe, expect, it, vi } from 'vitest'
import apiClient from './api.client'
import { deadLetterQueueService } from './deadLetterQueue.service'

const task = {
  id: 'dlq-1',
  taskType: 'MF04_GRADING',
  flowType: 'MF-04',
  managedByRole: 'examination_officer',
  payloadJson: '{"ticketId":"ticket-1"}',
  errorMessage: 'Transient grading timeout',
  retryCount: 2,
  status: 'PENDING',
  createdAt: '2026-11-14T01:00:00.000Z',
  resolvedAt: null,
}

describe('deadLetterQueueService ownership contract', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('filters generic DLQ tasks and omits ALL from the query string', async () => {
    const get = vi.spyOn(apiClient, 'get').mockResolvedValue({ data: [task] } as never)

    await expect(deadLetterQueueService.list('PENDING', 'ALL')).resolves.toMatchObject([{ id: 'dlq-1' }])
    expect(get).toHaveBeenCalledWith('/dlq/tasks', { params: { status: 'PENDING' } })
  })

  it('uses examination-officer endpoints for MF-04 replay and resolution', async () => {
    const get = vi.spyOn(apiClient, 'get').mockResolvedValue({ data: [task] } as never)
    const post = vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: { id: 'task-1', dlqId: 'dlq-1', status: 'REPLAYED', message: 'Replay queued.' },
    } as never)

    await deadLetterQueueService.listForExaminationOfficer('PENDING')
    await deadLetterQueueService.replayForExaminationOfficer('dlq-1')
    await deadLetterQueueService.resolveForExaminationOfficer('dlq-1', 'Verified after replay')

    expect(get).toHaveBeenCalledWith('/examination-officer/dlq/tasks', {
      params: { status: 'PENDING' },
    })
    expect(post).toHaveBeenNthCalledWith(1, '/examination-officer/dlq/tasks/dlq-1/replay')
    expect(post).toHaveBeenNthCalledWith(2, '/examination-officer/dlq/tasks/dlq-1/resolve', {
      resolutionNotes: 'Verified after replay',
    })
  })

  it('uses admin endpoints for MF-01/02/05 DLQ listing, replay and resolution', async () => {
    const adminTask = { ...task, taskType: 'PracticeGrading', flowType: 'PRACTICE', managedByRole: 'admin' }
    const get = vi.spyOn(apiClient, 'get').mockResolvedValue({ data: [adminTask] } as never)
    const post = vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: { id: 'task-2', dlqId: 'dlq-1', status: 'resolved', message: 'Resolved.' },
    } as never)

    await deadLetterQueueService.listForAdmin('pending', 'ALL')
    await deadLetterQueueService.listForAdmin('pending', 'PRACTICE')
    await deadLetterQueueService.listForAdmin('pending', 'IN_CLASS_EXAM')
    await deadLetterQueueService.replayForAdmin('dlq-1')
    await deadLetterQueueService.resolveForAdmin('dlq-1', 'Checked manually')

    expect(get).toHaveBeenNthCalledWith(1, '/admin/dlq/tasks', {
      params: { status: 'pending' },
    })
    expect(get).toHaveBeenNthCalledWith(2, '/admin/dlq/tasks', {
      params: { status: 'pending', flowType: 'PRACTICE' },
    })
    expect(get).toHaveBeenNthCalledWith(3, '/admin/dlq/tasks', {
      params: { status: 'pending', flowType: 'IN_CLASS_EXAM' },
    })
    expect(post).toHaveBeenNthCalledWith(1, '/admin/dlq/tasks/dlq-1/replay')
    expect(post).toHaveBeenNthCalledWith(2, '/admin/dlq/tasks/dlq-1/resolve', {
      resolutionNotes: 'Checked manually',
    })
  })
})
