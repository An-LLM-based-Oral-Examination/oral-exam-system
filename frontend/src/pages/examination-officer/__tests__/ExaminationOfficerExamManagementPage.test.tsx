import { MemoryRouter } from 'react-router-dom'
import { render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ExaminationOfficerExamManagementPage } from '../ExaminationOfficerExamManagementPage'
import { officialExamService } from '@/services/officialExam.service'

vi.mock('@/services/officialExam.service', () => ({
  officialExamService: {
    getSessions: vi.fn(),
    getShifts: vi.fn(),
    createSession: vi.fn(),
    createShift: vi.fn(),
  },
}))

const getSessions = vi.mocked(officialExamService.getSessions)
const getShifts = vi.mocked(officialExamService.getShifts)

describe('ExaminationOfficerExamManagementPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    getSessions.mockResolvedValue([
      {
        id: 'session-1',
        courseId: 'course-1',
        examStructureId: 'structure-1',
        title: 'PRN231 Final Oral Exam',
        examDate: '2026-11-20',
        hasFollowUp: true,
        maxFollowUpQuestions: 2,
        examInputMode: 'VoiceWithTranscriptEdit',
        transcriptBufferSeconds: 60,
        status: 'SCHEDULED',
      },
    ])
    getShifts.mockResolvedValue([
      {
        id: 'shift-1',
        sessionId: 'session-1',
        shiftName: 'Ca 1',
        roomLab: 'Lab 301',
        startTime: '2026-11-20T08:00:00.000Z',
        endTime: '2026-11-20T09:30:00.000Z',
        proctorUserId: 'proctor-1',
        examinerUserId: 'examiner-1',
        examinerName: 'Examiner',
        proctorName: 'Proctor',
        status: 'SCHEDULED',
      },
    ])
  })

  it('loads official sessions and shifts from the examination officer APIs', async () => {
    render(
      <MemoryRouter>
        <ExaminationOfficerExamManagementPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getAllByText('PRN231 Final Oral Exam').length).toBeGreaterThan(0))
    expect(screen.getByText('Ca 1')).toBeInTheDocument()
    expect(getSessions).toHaveBeenCalledWith()
    expect(getShifts).toHaveBeenCalledWith()
  })

  it('keeps follow-up configuration read-only on the examination officer portal', async () => {
    render(
      <MemoryRouter>
        <ExaminationOfficerExamManagementPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getByText(/Follow-up/)).toBeInTheDocument())
    expect(screen.getByText(/Kh.*ng c.* quy.*n thay/)).toBeInTheDocument()
  })
})
