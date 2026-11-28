import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ExaminationOfficerGradePublicationPage } from '../ExaminationOfficerGradePublicationPage'
import { officialExamService } from '@/services/officialExam.service'
import type { AuditEvidenceItem, ExamShift } from '@/types/officialExam.types'

vi.mock('@/services/officialExam.service', () => ({
  officialExamService: {
    getAuditEvidence: vi.fn(),
    getShifts: vi.fn(),
    publishGrades: vi.fn(),
    exportShiftExcel: vi.fn(),
    exportShiftPdf: vi.fn(),
  },
}))

const getAuditEvidence = vi.mocked(officialExamService.getAuditEvidence)
const getShifts = vi.mocked(officialExamService.getShifts)
const publishGrades = vi.mocked(officialExamService.publishGrades)

const baseShift: ExamShift = {
  id: 'shift-grade-1',
  sessionId: 'session-1',
  shiftName: 'Ca 1 - Sáng',
  roomLab: 'LAB-301',
  startTime: '2026-11-20T08:00:00.000Z',
  endTime: '2026-11-20T09:30:00.000Z',
  proctorUserId: 'proctor-1',
  examinerUserId: 'examiner-1',
  examinerName: 'Giám khảo A',
  proctorName: 'Giám thị A',
  status: 'completed',
  isGradesPublished: false,
  gradesPublishedAt: null,
}

const auditedItem: AuditEvidenceItem = {
  ticketId: 'ticket-1',
  studentCode: 'SE170001',
  fullName: 'Nguyen Van A',
  seatNumber: 1,
  status: 'AUDITED',
  audioR2Url: null,
  audioHashSha256: null,
  transcriptWhisper: 'Answer',
  aiScore: 8.0,
  confidenceScore: 0.92,
  isSuspicious: false,
  suspiciousReason: null,
  cotTrace: 'Solid',
  finalScore: 8.0,
  isLocked: false,
}

function renderGradePage() {
  return render(
    <MemoryRouter initialEntries={['/examination-officer/grades/shift-grade-1']}>
      <Routes>
        <Route
          path="/examination-officer/grades/:shiftId"
          element={<ExaminationOfficerGradePublicationPage />}
        />
      </Routes>
    </MemoryRouter>,
  )
}

describe('ExaminationOfficerGradePublicationPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    getShifts.mockResolvedValue([baseShift])
    getAuditEvidence.mockResolvedValue([auditedItem])
    publishGrades.mockResolvedValue({
      shiftId: 'shift-grade-1',
      totalPublished: 1,
      publishedAt: '2026-11-20T12:00:00.000Z',
      message: 'Đã công bố điểm toàn ca.',
    })
  })

  it('publishes via ConfirmDialog (zero window.confirm) and preserves ĐÃ CÔNG BỐ across F5 remount', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm')
    const firstRender = renderGradePage()

    expect(await screen.findByText('SE170001 · Nguyen Van A')).toBeInTheDocument()
    expect(screen.getByText('CHƯA CÔNG BỐ')).toBeInTheDocument()

    getAuditEvidence.mockResolvedValue([{ ...auditedItem, status: 'PUBLISHED', isLocked: true }])
    getShifts.mockResolvedValue([
      {
        ...baseShift,
        isGradesPublished: true,
        gradesPublishedAt: '2026-11-20T12:00:00.000Z',
      },
    ])

    fireEvent.click(screen.getByRole('button', { name: 'Công bố điểm' }))
    expect(confirmSpy).not.toHaveBeenCalled()
    expect(await screen.findByRole('alertdialog')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận công bố điểm' }))
    await waitFor(() => expect(publishGrades).toHaveBeenCalledWith('shift-grade-1'))
    expect(await screen.findByText('ĐÃ CÔNG BỐ')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Đã công bố' })).toBeDisabled()

    firstRender.unmount()

    // Simulate F5 full reload where local React state resets to initial values
    renderGradePage()
    expect(await screen.findByText('SE170001 · Nguyen Van A')).toBeInTheDocument()
    expect(screen.getByText('ĐÃ CÔNG BỐ')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Đã công bố' })).toBeDisabled()

    confirmSpy.mockRestore()
  })

  it('blocks publish when an unresolved suspicious submission has not been audited yet', async () => {
    getAuditEvidence.mockResolvedValue([
      { ...auditedItem, status: 'AI_GRADED', isSuspicious: true, suspiciousReason: 'Confidence < 0.7' },
    ])

    renderGradePage()

    expect(await screen.findByText(/Còn 1 bài nghi ngờ chưa được Giám khảo hậu kiểm/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Công bố điểm' })).toBeDisabled()
  })

  it('keeps CHƯA CÔNG BỐ and allows publishing when a ticket is anti-cheat auto-locked (status LOCKED, isLocked false)', async () => {
    getAuditEvidence.mockResolvedValue([
      {
        ...auditedItem,
        status: 'LOCKED',
        isLocked: false,
        aiScore: 0,
        finalScore: 0,
        isSuspicious: false,
      },
    ])

    renderGradePage()

    expect(await screen.findByText('SE170001 · Nguyen Van A')).toBeInTheDocument()
    expect(screen.getByText('CHƯA CÔNG BỐ')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Công bố điểm' })).toBeEnabled()
  })

  it('allows publishing when all tickets are pre-locked (isLocked=true, status=AUDITED) and blocks in_progress before endTime', async () => {
    getAuditEvidence.mockResolvedValue([
      {
        ...auditedItem,
        status: 'AUDITED',
        isLocked: true,
      },
    ])

    const { unmount } = renderGradePage()
    expect(await screen.findByText('SE170001 · Nguyen Van A')).toBeInTheDocument()
    expect(screen.getByText('CHƯA CÔNG BỐ')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Công bố điểm' })).toBeEnabled()
    unmount()

    getShifts.mockResolvedValue([
      {
        ...baseShift,
        status: 'in_progress',
        endTime: '2099-11-20T09:30:00.000Z',
      },
    ])
    getAuditEvidence.mockResolvedValue([
      {
        ...auditedItem,
        status: 'SUBMITTED',
        aiScore: null,
        finalScore: null,
        isLocked: false,
      },
    ])

    renderGradePage()
    expect(
      await screen.findByText(/Ca thi đang diễn ra và chưa tới giờ kết thúc\./),
    ).toBeInTheDocument()
    expect(screen.getByText(/Còn 1 bài thi chưa hoàn tất chấm AI\./)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Công bố điểm' })).toBeDisabled()
  })
})
