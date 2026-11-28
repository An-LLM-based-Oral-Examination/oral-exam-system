import { Link, MemoryRouter, Route, Routes } from 'react-router-dom'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ExaminationOfficerRosterImportPage } from '../ExaminationOfficerRosterImportPage'
import { officialExamService } from '@/services/officialExam.service'
import { proctorService } from '@/services/proctor.service'

vi.mock('@/services/officialExam.service', () => ({
  officialExamService: {
    getShifts: vi.fn(),
    getSessions: vi.fn(),
    getExamSets: vi.fn(),
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
const getExamSets = vi.mocked(officialExamService.getExamSets)
const previewTicketRosterImport = vi.mocked(officialExamService.previewTicketRosterImport)
const commitTicketRosterImport = vi.mocked(officialExamService.commitTicketRosterImport)
const getShiftAttendance = vi.mocked(proctorService.getShiftAttendance)

const shift = {
  id: 'shift-1',
  sessionId: 'session-1',
  shiftName: 'Ca 1',
  roomLab: 'Lab 301',
  startTime: '2026-11-20T08:00:00.000Z',
  endTime: '2026-11-20T09:30:00.000Z',
  proctorUserId: null,
  examinerUserId: null,
  examinerName: null,
  proctorName: null,
  status: 'SCHEDULED',
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/examination-officer/shifts/shift-1/tickets/import']}>
      <Routes>
        <Route path="/examination-officer/shifts/:shiftId/tickets/import" element={<ExaminationOfficerRosterImportPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('ExaminationOfficerRosterImportPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
    sessionStorage.clear()
    getShifts.mockResolvedValue([{ ...shift, maxSeats: 24, assignedTicketsCount: 0, remainingSeats: 24 }])
    getExamSets.mockResolvedValue([])
    getSessions.mockResolvedValue([
      {
        id: 'session-1',
        courseId: 'course-1',
        title: 'CSD201 · FA26 · Thi thật',
        examDate: '2026-11-20',
        hasFollowUp: false,
        maxFollowUpQuestions: 0,
        examInputMode: 'VoiceOnly',
        transcriptBufferSeconds: 60,
        status: 'SCHEDULED',
      },
    ])
    getShiftAttendance.mockResolvedValue({
      shiftId: 'shift-1',
      shiftName: 'Ca 1',
      roomLab: 'Lab 301',
      shiftStatus: 'SCHEDULED',
      startTime: shift.startTime,
      endTime: shift.endTime,
      totalSeats: 40,
      assignedTicketsCount: 1,
      checkedInCount: 0,
      inProgressCount: 0,
      submittedCount: 0,
      suspendedCount: 0,
      seats: [
        {
          seatNumber: 1,
          isOccupied: true,
          ticketId: 'ticket-1',
          studentId: 'student-1',
          studentCode: 'SE170001',
          fullName: 'Nguyen An',
          ipAddress: '192.168.1.10',
          status: 'SCHEDULED',
          isCheckedIn: false,
          isSuspended: false,
          suspensionReason: null,
          checkedInAt: null,
          submittedAt: null,
        },
      ],
    })
  })

  it('previews backend fullName/setCode and commits only after confirmation', async () => {
    const file = new File(['studentCode,seatNumber,ipAddress\nSE170001,1,192.168.1.10'], 'roster.csv', {
      type: 'text/csv',
    })
    previewTicketRosterImport.mockResolvedValue({
      shiftId: 'shift-1',
      totalRowCount: 1,
      validRowCount: 1,
      errorRowCount: 0,
      availableHandedOverSetCount: 1,
      rows: [
        {
          sourceRow: 2,
          studentCode: 'SE170001',
          fullName: 'Nguyen An',
          seatNumber: 1,
          ipAddress: '192.168.1.10',
          setCode: 'SET-01',
          isValid: true,
          errors: [],
        },
      ],
      globalErrors: [],
      canCommit: true,
    })
    commitTicketRosterImport.mockResolvedValue({ shiftId: 'shift-1', createdTicketCount: 1, tickets: [] })

    renderPage()
    const input = await screen.findByLabelText('Chọn file roster')
    fireEvent.change(input, { target: { files: [file] } })
    fireEvent.click(screen.getByRole('button', { name: 'Preview roster' }))

    await waitFor(() => expect(previewTicketRosterImport).toHaveBeenCalledWith('shift-1', file))
    expect(await screen.findByText('Nguyen An')).toBeInTheDocument()
    expect(screen.getByText('SET-01')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận phát vé hàng loạt' }))
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận import' }))

    await waitFor(() => expect(commitTicketRosterImport).toHaveBeenCalledWith('shift-1', file))
    await waitFor(() => expect(getShiftAttendance).toHaveBeenCalledWith('shift-1'))
    expect(await screen.findByText('Danh sách vé vừa cập nhật')).toBeInTheDocument()
  })

  it('disables commit when preview returns any row error', async () => {
    const file = new File(['studentCode,seatNumber,ipAddress\nSE170001,41,192.168.1.10'], 'roster.csv', {
      type: 'text/csv',
    })
    previewTicketRosterImport.mockResolvedValue({
      shiftId: 'shift-1',
      totalRowCount: 1,
      validRowCount: 0,
      errorRowCount: 1,
      availableHandedOverSetCount: 1,
      rows: [
        {
          sourceRow: 2,
          studentCode: 'SE170001',
          fullName: 'Nguyen An',
          seatNumber: 41,
          ipAddress: '192.168.1.10',
          setCode: null,
          isValid: false,
          errors: [{ sourceRow: 2, code: 'SEAT_OUT_OF_RANGE', field: 'seatNumber', message: 'Số ghế phải từ 1 đến 40.' }],
        },
      ],
      globalErrors: [],
      canCommit: false,
    })

    renderPage()
    const input = await screen.findByLabelText('Chọn file roster')
    fireEvent.change(input, { target: { files: [file] } })
    fireEvent.click(screen.getByRole('button', { name: 'Preview roster' }))

    await waitFor(() => expect(previewTicketRosterImport).toHaveBeenCalled())
    expect(screen.getByRole('button', { name: 'Xác nhận phát vé hàng loạt' })).toBeDisabled()
    expect(screen.getByText('Số ghế phải từ 1 đến 40.')).toBeInTheDocument()
  })

  it('shows RFC 7807 detail and errorCode after a stale commit and keeps the file for re-preview', async () => {
    const file = new File(['studentCode,seatNumber,ipAddress\nSE170001,1,192.168.1.10'], 'stale-roster.csv', {
      type: 'text/csv',
    })
    previewTicketRosterImport.mockResolvedValue({
      shiftId: 'shift-1',
      totalRowCount: 1,
      validRowCount: 1,
      errorRowCount: 0,
      availableHandedOverSetCount: 1,
      rows: [],
      globalErrors: [],
      canCommit: true,
    })
    commitTicketRosterImport.mockRejectedValue({
      status: 409,
      detail: 'Ghế hoặc bộ đề đã được sử dụng bởi một lượt import khác.',
      errorCode: 'BULK_IMPORT_STALE',
    })

    renderPage()
    const input = await screen.findByLabelText('Chọn file roster')
    fireEvent.change(input, { target: { files: [file] } })
    fireEvent.click(screen.getByRole('button', { name: 'Preview roster' }))
    await waitFor(() => expect(previewTicketRosterImport).toHaveBeenCalledWith('shift-1', file))

    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận phát vé hàng loạt' }))
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận import' }))

    await waitFor(() => expect(commitTicketRosterImport).toHaveBeenCalledWith('shift-1', file))
    expect(await screen.findByRole('alert')).toHaveTextContent('Ghế hoặc bộ đề đã được sử dụng bởi một lượt import khác.')
    expect(screen.getByRole('alert')).toHaveTextContent('BULK_IMPORT_STALE')
    expect(screen.getByText('stale-roster.csv')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Preview roster' })).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('disables commit when preview returns a global parser issue', async () => {
    const file = new File(['studentCode,seatNumber\nSE170001,1'], 'invalid-header.csv', { type: 'text/csv' })
    previewTicketRosterImport.mockResolvedValue({
      shiftId: 'shift-1',
      totalRowCount: 0,
      validRowCount: 0,
      errorRowCount: 0,
      availableHandedOverSetCount: 0,
      rows: [],
      globalErrors: [{ sourceRow: null, code: 'INVALID_ROSTER_HEADER', field: null, message: 'Header không hợp lệ.' }],
      canCommit: false,
    })

    renderPage()
    const input = await screen.findByLabelText('Chọn file roster')
    fireEvent.change(input, { target: { files: [file] } })
    fireEvent.click(screen.getByRole('button', { name: 'Preview roster' }))

    await waitFor(() => expect(previewTicketRosterImport).toHaveBeenCalled())
    expect(screen.getByText('Header không hợp lệ.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Xác nhận phát vé hàng loạt' })).toBeDisabled()
  })

  it('rejects a roster larger than 1 MB before calling preview API', async () => {
    const oversizedFile = new File([new Uint8Array(1_048_577)], 'oversized.csv', { type: 'text/csv' })

    renderPage()
    const input = await screen.findByLabelText('Chọn file roster')
    fireEvent.change(input, { target: { files: [oversizedFile] } })

    expect(await screen.findByRole('alert')).toHaveTextContent('Dung lượng file không được vượt quá 1 MB.')
    expect(previewTicketRosterImport).not.toHaveBeenCalled()
    expect(screen.queryByRole('button', { name: 'Preview roster' })).not.toBeInTheDocument()
  })

  it('displays pre-upload capacity and valid seat range 1–maxSeats and never persists roster in browser storage', async () => {
    const file = new File(['studentCode,seatNumber,ipAddress\nSE170025,25,192.168.1.25'], 'seat25.csv', {
      type: 'text/csv',
    })
    previewTicketRosterImport.mockResolvedValueOnce({
      shiftId: 'shift-1',
      totalRowCount: 1,
      validRowCount: 0,
      errorRowCount: 1,
      availableHandedOverSetCount: 2,
      maxSeats: 24,
      existingTicketCount: 0,
      remainingSeats: 24,
      reservedExamSetCodes: [],
      rows: [
        {
          sourceRow: 2,
          studentCode: 'SE170025',
          fullName: 'Le Van C',
          seatNumber: 25,
          ipAddress: '192.168.1.25',
          setCode: null,
          isValid: false,
          errors: [
            {
              sourceRow: 2,
              code: 'SEAT_OUT_OF_RANGE',
              field: 'seatNumber',
              message: 'Số ghế phải nằm trong khoảng 1 đến 24 của ca thi.',
            },
          ],
          issues: ['Số ghế phải nằm trong khoảng 1 đến 24 của ca thi.'],
        },
      ],
      globalErrors: [],
      canCommit: false,
    })

    renderPage()
    expect(await screen.findByText('Sức chứa ca thi: 24')).toBeInTheDocument()
    expect(screen.getByText('Ghế hợp lệ: 1–24')).toBeInTheDocument()

    const input = await screen.findByLabelText('Chọn file roster')
    fireEvent.change(input, { target: { files: [file] } })
    fireEvent.click(screen.getByRole('button', { name: 'Preview roster' }))

    expect(
      await screen.findByText('Số ghế phải nằm trong khoảng 1 đến 24 của ca thi.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Xác nhận phát vé hàng loạt' })).toBeDisabled()
    expect(localStorage.length).toBe(0)
    expect(sessionStorage.length).toBe(0)
  })

  it('ignores stale async responses when switching shiftId rapidly', async () => {
    let resolveSlowShifts!: (value: typeof shift[]) => void
    const slowShiftsPromise = new Promise<typeof shift[]>((resolve) => {
      resolveSlowShifts = resolve
    })

    getShifts
      .mockReturnValueOnce(slowShiftsPromise as never)
      .mockResolvedValueOnce([
        {
          ...shift,
          id: 'shift-2',
          shiftName: 'Ca 2 Mới',
          roomLab: 'LAB-NEW-B',
          maxSeats: 24,
          assignedTicketsCount: 0,
          remainingSeats: 24,
        },
      ])

    render(
      <MemoryRouter initialEntries={['/examination-officer/shifts/shift-1/tickets/import']}>
        <Link to="/examination-officer/shifts/shift-2/tickets/import">Switch to shift 2</Link>
        <Routes>
          <Route
            path="/examination-officer/shifts/:shiftId/tickets/import"
            element={<ExaminationOfficerRosterImportPage />}
          />
        </Routes>
      </MemoryRouter>,
    )

    await waitFor(() => expect(getShifts).toHaveBeenCalledTimes(1))
    fireEvent.click(screen.getByRole('link', { name: 'Switch to shift 2' }))
    expect(await screen.findByText(/Phòng LAB-NEW-B/)).toBeInTheDocument()

    resolveSlowShifts([
      {
        ...shift,
        id: 'shift-1',
        shiftName: 'Ca 1 Cũ',
        roomLab: 'LAB-OLD-A',
      },
    ])

    await waitFor(() => expect(screen.getByText(/Phòng LAB-NEW-B/)).toBeInTheDocument())
    expect(screen.queryByText(/LAB-OLD-A/)).not.toBeInTheDocument()
  })
})
