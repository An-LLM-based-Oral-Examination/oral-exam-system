import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ExaminationOfficerExamDetailPage } from '../ExaminationOfficerExamDetailPage'
import { ExaminationOfficerDlqPage } from '../ExaminationOfficerDlqPage'
import { officialExamService } from '@/services/officialExam.service'
import { deadLetterQueueService } from '@/services/deadLetterQueue.service'

vi.mock('@/services/officialExam.service', () => ({
  officialExamService: {
    getSessions: vi.fn(),
    getShifts: vi.fn(),
    getExamSets: vi.fn(),
    publishShift: vi.fn(),
    updateShift: vi.fn(),
  },
}))

vi.mock('@/services/deadLetterQueue.service', () => ({
  deadLetterQueueService: {
    listForExaminationOfficer: vi.fn(),
    replayForExaminationOfficer: vi.fn(),
    resolveForExaminationOfficer: vi.fn(),
  },
}))

describe('EO Design-System Dialogs (zero window.confirm / window.prompt)', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('uses ConfirmDialog instead of window.confirm to publish a shift in ExaminationOfficerExamDetailPage', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm')
    vi.mocked(officialExamService.getSessions).mockResolvedValue([
      {
        id: 'session-1',
        courseId: 'course-1',
        title: 'PRN231 · FE',
        examDate: '2026-11-20',
        hasFollowUp: false,
        maxFollowUpQuestions: 2,
        examInputMode: 'VoiceOnly',
        transcriptBufferSeconds: 60,
        status: 'scheduled',
      },
    ])
    vi.mocked(officialExamService.getShifts).mockResolvedValue([
      {
        id: 'shift-1',
        sessionId: 'session-1',
        shiftName: 'Ca 1',
        roomLab: 'LAB-301',
        startTime: '2026-11-20T08:00:00.000Z',
        endTime: '2026-11-20T09:30:00.000Z',
        proctorUserId: 'proctor-1',
        examinerUserId: 'examiner-1',
        examinerName: 'Giám khảo 1',
        proctorName: 'Giám thị 1',
        status: 'scheduled',
        isExamPublished: false,
      },
    ])
    vi.mocked(officialExamService.getExamSets).mockResolvedValue([])
    vi.mocked(officialExamService.publishShift).mockResolvedValue({
      success: true,
      shiftId: 'shift-1',
      message: 'Đã xuất bản đề thi.',
    })

    render(
      <MemoryRouter initialEntries={['/examination-officer/exams/session-1']}>
        <Routes>
          <Route
            path="/examination-officer/exams/:sessionId"
            element={<ExaminationOfficerExamDetailPage />}
          />
        </Routes>
      </MemoryRouter>,
    )

    fireEvent.click(await screen.findByRole('button', { name: 'Xuất bản đề' }))
    expect(confirmSpy).not.toHaveBeenCalled()
    expect(await screen.findByRole('alertdialog')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận xuất bản' }))
    await waitFor(() => expect(officialExamService.publishShift).toHaveBeenCalledWith('shift-1'))
    expect(await screen.findByRole('button', { name: 'Đã xuất bản' })).toBeDisabled()
    expect(screen.getByText('scheduled')).toBeInTheDocument()
    expect(screen.queryByText('PUBLISHED')).not.toBeInTheDocument()
    confirmSpy.mockRestore()
  })

  it('uses FormDialog with inline >=5 char validation instead of window.prompt in ExaminationOfficerDlqPage', async () => {
    const promptSpy = vi.spyOn(window, 'prompt')
    vi.mocked(deadLetterQueueService.listForExaminationOfficer).mockResolvedValue([
      {
        id: 'dlq-1',
        flowType: 'OFFICIAL_EXAM',
        taskType: 'OFFICIAL_GRADING',
        payloadJson: '{"ticketId":"ticket-1"}',
        status: 'pending',
        retryCount: 3,
        errorMessage: 'Gemini timeout after 3 retries',
        createdAt: '2026-11-20T08:30:00.000Z',
        resolvedAt: null,
      },
    ])
    vi.mocked(deadLetterQueueService.resolveForExaminationOfficer).mockResolvedValue({
      id: 'dlq-1',
      dlqId: 'dlq-1',
      status: 'resolved',
      message: 'Đã đánh dấu xử lý.',
    })

    render(<ExaminationOfficerDlqPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'Xử lý' }))
    expect(promptSpy).not.toHaveBeenCalled()
    expect(await screen.findByRole('dialog')).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText(/Ghi chú xử lý/), { target: { value: 'abc' } })
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận đã xử lý' }))
    expect(await screen.findByText('Ghi chú xử lý cần ít nhất 5 ký tự.')).toBeInTheDocument()
    expect(deadLetterQueueService.resolveForExaminationOfficer).not.toHaveBeenCalled()

    fireEvent.change(screen.getByLabelText(/Ghi chú xử lý/), {
      target: { value: 'Giám khảo đã chấm thủ công bằng chứng âm thanh.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận đã xử lý' }))

    await waitFor(() =>
      expect(deadLetterQueueService.resolveForExaminationOfficer).toHaveBeenCalledWith(
        'dlq-1',
        'Giám khảo đã chấm thủ công bằng chứng âm thanh.',
      ),
    )
    promptSpy.mockRestore()
  })
})
