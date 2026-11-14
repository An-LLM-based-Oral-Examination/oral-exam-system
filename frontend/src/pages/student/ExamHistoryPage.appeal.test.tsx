import type { ComponentProps } from 'react'
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { isPublishedOfficialTicket } from '@/lib/officialExamStatus'
import { OfficialExamScorecardView } from './ExamHistoryPage'

type Props = ComponentProps<typeof OfficialExamScorecardView>
const initial: Props = {
  ticket: { ticketId: 'ticket', shiftId: 'shift', courseCode: 'CSD201', courseName: 'Algorithms',
    sessionTitle: 'QA', shiftName: 'Shift', roomLab: 'Lab', examDate: '', startTime: '', endTime: '',
    seatNumber: 1, status: 'PUBLISHED', finalScore: 8, isLocked: true, studentAcknowledgementStatus: 'PENDING' },
  grade: { ticketId: 'ticket', studentCode: 'SE123456', fullName: 'QA', courseCode: 'CSD201',
    status: 'PUBLISHED', isLocked: true, totalScore: 8, publishedAt: '2026-11-14T00:00:00Z', publishedBy: 'QA',
    studentAcknowledgementStatus: 'PENDING', detailedScorecard: [], internalAppeal: {
      canAppeal: true, canAcknowledge: true, instruction: 'Có thể gửi đơn.', appealDeadlineDays: 3,
      appealDeadlineAtUtc: '2026-11-17T00:00:00Z', appealEndpoint: '/api/v1/appeals',
    } },
  appeal: undefined, appealReason: 'Xin xem xét lại', appealReasonError: '',
  onChangeAppealReason: vi.fn(), onAcknowledge: vi.fn(), onCreateAppeal: vi.fn(), busy: false,
}
afterEach(() => { cleanup(); vi.useRealTimers(); vi.clearAllMocks() })
describe('official result appeal controls', () => {
  it('keeps unpublished official tickets out of the results view', () => {
    expect(isPublishedOfficialTicket('PUBLISHED')).toBe(true)
    expect(isPublishedOfficialTicket('locked')).toBe(true)
    expect(isPublishedOfficialTicket('AI_GRADED')).toBe(false)
    expect(isPublishedOfficialTicket('SCHEDULED')).toBe(false)
  })

  it('shows the actual deadline and disables submission exactly at expiry without reload', () => {
    vi.useFakeTimers(); vi.setSystemTime(new Date('2026-11-16T23:59:59Z'))
    render(<OfficialExamScorecardView {...initial} />)
    expect(screen.getByText(/Hạn gửi phúc khảo:/).textContent).toContain('17/11/2026')
    const button = screen.getByRole('button', { name: 'Gửi đơn phúc khảo' }) as HTMLButtonElement
    expect(button.disabled).toBe(false)
    act(() => vi.advanceTimersByTime(1000))
    expect(button.disabled).toBe(true)
    fireEvent.click(button)
    expect(initial.onCreateAppeal).not.toHaveBeenCalled()
  })
  it('blocks acknowledgement while the server reports an active appeal', () => {
    render(<OfficialExamScorecardView {...initial} grade={{ ...initial.grade, internalAppeal: {
      ...initial.grade.internalAppeal, canAcknowledge: false, canAppeal: false,
      instruction: 'Đơn phúc khảo đang xử lý.',
    } }} />)
    const button = screen.getByRole('button', { name: 'Xác nhận đồng ý điểm thi' }) as HTMLButtonElement
    expect(button.disabled).toBe(true)
    fireEvent.click(button)
    expect(initial.onAcknowledge).not.toHaveBeenCalled()
  })
  it('fails closed when the publication/deadline is missing', () => {
    render(<OfficialExamScorecardView {...initial} grade={{ ...initial.grade, publishedAt: null,
      internalAppeal: { ...initial.grade.internalAppeal, appealDeadlineAtUtc: null, canAcknowledge: false } }} />)
    expect((screen.getByRole('button', { name: 'Gửi đơn phúc khảo' }) as HTMLButtonElement).disabled).toBe(true)
    expect(screen.getByText(/Hạn gửi phúc khảo:/).textContent).toContain('Chưa ghi nhận')
  })
  it('does not permit appeal from an acknowledged grade even with stale permission', () => {
    render(<OfficialExamScorecardView {...initial} grade={{ ...initial.grade, studentAcknowledgementStatus: 'ACKNOWLEDGED' }} />)
    expect(screen.queryByRole('button', { name: 'Xác nhận đồng ý điểm thi' })).toBeNull()
    expect((screen.getByRole('button', { name: 'Gửi đơn phúc khảo' }) as HTMLButtonElement).disabled).toBe(true)
  })
})
