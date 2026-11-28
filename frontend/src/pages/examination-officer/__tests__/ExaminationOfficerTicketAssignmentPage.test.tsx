import { MemoryRouter } from 'react-router-dom'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ExaminationOfficerTicketAssignmentPage } from '../ExaminationOfficerTicketAssignmentPage'
import { officialExamService } from '@/services/officialExam.service'

vi.mock('@/services/officialExam.service', () => ({
  officialExamService: {
    getShifts: vi.fn(),
    getSessions: vi.fn(),
    assignTicket: vi.fn(),
    previewTicketRosterImport: vi.fn(),
    commitTicketRosterImport: vi.fn(),
    downloadTicketRosterTemplate: vi.fn(),
  },
}))

vi.mock('@/services/proctor.service', () => ({
  proctorService: {
    getShiftAttendance: vi.fn(),
  },
}))

const getShifts = vi.mocked(officialExamService.getShifts)
const getSessions = vi.mocked(officialExamService.getSessions)
const assignTicket = vi.mocked(officialExamService.assignTicket)

const shift = {
  id: 'shift-1',
  sessionId: 'session-1',
  shiftName: 'Ca sáng',
  roomLab: 'Lab 7.1',
  startTime: '2026-11-20T08:00:00.000Z',
  endTime: '2026-11-20T09:30:00.000Z',
  proctorUserId: 'proctor-1',
  examinerUserId: 'examiner-1',
  examinerName: 'Giám khảo',
  proctorName: 'Giám thị',
  status: 'SCHEDULED',
}

describe('ExaminationOfficerTicketAssignmentPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    getShifts.mockResolvedValue([shift])
    getSessions.mockResolvedValue([
      {
        id: 'session-1',
        courseId: 'course-csd201',
        title: 'CSD201 · FA26 · Thi thật 2026-11-20',
        examDate: '2026-11-20',
        hasFollowUp: false,
        maxFollowUpQuestions: 0,
        examInputMode: 'VoiceOnly',
        transcriptBufferSeconds: 60,
        status: 'SCHEDULED',
      },
    ])
    assignTicket.mockResolvedValue({
      id: 'ticket-1',
      shiftId: 'shift-1',
      studentId: 'student-1',
      studentCode: 'SE170002',
      seatNumber: 1,
      ipAddress: null,
      status: 'SCHEDULED',
      checkedInAt: '2026-11-20T07:55:00.000Z',
      examSetId: null,
    })
  })

  it('opens the centered ticket dialog and submits the student code payload', async () => {
    render(
      <MemoryRouter>
        <ExaminationOfficerTicketAssignmentPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getByRole('button', { name: 'Cấp vé thi' })).toBeEnabled())
    expect(screen.queryByLabelText('Ca thi')).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Cấp vé thi' }))
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(screen.getByLabelText(/Ca thi/)).toBeInTheDocument()
    expect(screen.getByText(/Mã số sinh viên/)).toBeInTheDocument()
    expect(screen.queryByText('Student ID')).not.toBeInTheDocument()

    fireEvent.change(screen.getByLabelText(/Ca thi/), { target: { value: 'shift-1' } })
    fireEvent.change(screen.getByLabelText(/Mã số sinh viên/), {
      target: { value: 'SE170002' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận cấp vé' }))

    await waitFor(() =>
      expect(assignTicket).toHaveBeenCalledWith({
        shiftId: 'shift-1',
        studentCode: 'SE170002',
        seatNumber: 1,
        ipAddress: null,
      }),
    )
  })

  it('opens bulk import in the same centered dialog with the selected shift', async () => {
    render(
      <MemoryRouter>
        <ExaminationOfficerTicketAssignmentPage />
      </MemoryRouter>,
    )

    await waitFor(() =>
      expect(screen.getByRole('button', { name: /Import hàng loạt/i })).toBeEnabled(),
    )
    fireEvent.click(screen.getByRole('button', { name: /Import hàng loạt/i }))

    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(
      screen
        .getAllByRole('tab', { name: 'Import hàng loạt' })
        .some((tab) => tab.getAttribute('aria-selected') === 'true'),
    ).toBe(true)
    expect(screen.getByLabelText('Chọn file roster')).toBeInTheDocument()
    expect(screen.getByLabelText(/Ca thi/)).toHaveValue('shift-1')
    expect(screen.getByRole('heading', { name: /Import hàng loạt · Ca sáng/ })).toBeInTheDocument()
  })

  it('opens the dialog for the row action with that shift preselected', async () => {
    render(
      <MemoryRouter>
        <ExaminationOfficerTicketAssignmentPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getByRole('button', { name: 'Cấp vé' })).toBeEnabled())
    fireEvent.click(screen.getByRole('button', { name: 'Cấp vé' }))

    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(screen.getByLabelText(/Ca thi/)).toHaveValue('shift-1')
  })

  it('EO-09: clamps auto-incremented seatNumber to selectedShift.maxSeats after issuing last seat', async () => {
    getShifts.mockResolvedValue([{ ...shift, maxSeats: 2 }])

    render(
      <MemoryRouter>
        <ExaminationOfficerTicketAssignmentPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getByRole('button', { name: 'Cấp vé thi' })).toBeEnabled())
    fireEvent.click(screen.getByRole('button', { name: 'Cấp vé thi' }))

    const seatInput = screen.getByLabelText(/Số ghế/) as HTMLInputElement
    fireEvent.change(seatInput, { target: { value: '2' } })
    fireEvent.change(screen.getByLabelText(/Mã số sinh viên/), {
      target: { value: 'SE170002' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận cấp vé' }))

    await waitFor(() => expect(assignTicket).toHaveBeenCalledTimes(1))
    expect(seatInput.value).toBe('2')
  })
})
