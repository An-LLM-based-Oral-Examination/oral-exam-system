import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ProctorAttendancePage } from '../ProctorAttendancePage'
import { proctorService } from '@/services/proctor.service'
import { officialExamService } from '@/services/officialExam.service'
import { useAuth } from '@/hooks/useAuth'
import { useAntiCheatHub } from '@/hooks/useAntiCheatHub'

vi.mock('@/services/proctor.service', () => ({
  proctorService: {
    getShiftAttendance: vi.fn(),
    updateTicketAttendance: vi.fn(),
    recordViolation: vi.fn(),
    suspendTicket: vi.fn(),
  },
}))

vi.mock('@/services/officialExam.service', () => ({
  officialExamService: {
    getShifts: vi.fn(),
  },
}))

vi.mock('@/hooks/useAuth', () => ({
  useAuth: vi.fn(),
}))

vi.mock('@/hooks/useAntiCheatHub', () => ({
  useAntiCheatHub: vi.fn(),
}))

const getShiftAttendance = vi.mocked(proctorService.getShiftAttendance)
const updateTicketAttendance = vi.mocked(proctorService.updateTicketAttendance)
const getShifts = vi.mocked(officialExamService.getShifts)
const mockUseAuth = vi.mocked(useAuth)
const mockUseAntiCheatHub = vi.mocked(useAntiCheatHub)

function createSeats(totalSeats: number) {
  return Array.from({ length: totalSeats }, (_, idx) => {
    const seatNumber = idx + 1
    if (seatNumber === 1) {
      return {
        seatNumber: 1,
        isOccupied: true,
        ticketId: 'ticket-1',
        studentId: 'student-1',
        studentCode: 'SE170001',
        fullName: 'Nguyễn Văn A',
        ipAddress: '192.168.1.10',
        deviceId: 'BYOD-WIN-01',
        violationCount: 1,
        setCode: 'SET-01',
        status: 'SCHEDULED',
        isCheckedIn: false,
        isSuspended: false,
        suspensionReason: null,
        checkedInAt: null,
        submittedAt: null,
      }
    }
    return {
      seatNumber,
      isOccupied: false,
      ticketId: null,
      studentId: null,
      studentCode: null,
      fullName: null,
      ipAddress: null,
      deviceId: null,
      violationCount: 0,
      setCode: null,
      status: null,
      isCheckedIn: false,
      isSuspended: false,
      suspensionReason: null,
      checkedInAt: null,
      submittedAt: null,
    }
  })
}

describe('ProctorAttendancePage — Shift Dropdown, 4x6 Grid, BYOD Device & Realtime Anti-Cheat', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    window.history.replaceState({}, '', '/')
    mockUseAuth.mockReturnValue({
      user: {
        id: 'proctor-1',
        email: 'proctor@fpt.edu.vn',
        fullName: 'Giám thị 1',
        role: 'Proctor',
      },
      isAuthenticated: true,
    } as never)
    getShifts.mockResolvedValue([
      {
        id: 'shift-cap',
        sessionId: 'session-1',
        shiftName: 'Ca sáng Lab 201',
        roomLab: 'Lab 201',
        startTime: '2026-11-20T08:00:00.000Z',
        endTime: '2026-11-20T09:30:00.000Z',
        proctorUserId: 'proctor-1',
        examinerUserId: 'examiner-1',
        proctorName: 'Giám thị 1',
        examinerName: 'Giám khảo 1',
        status: 'scheduled',
      },
      {
        id: 'shift-24',
        sessionId: 'session-1',
        shiftName: 'Ca mặc định 24',
        roomLab: 'Lab 202',
        startTime: '2026-11-20T10:00:00.000Z',
        endTime: '2026-11-20T11:30:00.000Z',
        proctorUserId: 'proctor-1',
        examinerUserId: 'examiner-1',
        proctorName: 'Giám thị 1',
        examinerName: 'Giám khảo 1',
        status: 'scheduled',
      },
    ])
    mockUseAntiCheatHub.mockReturnValue({
      status: 'connected',
      isConnected: true,
      alerts: [
        {
          id: 'alert-1',
          clientEventId: 'evt-1',
          ticketId: 'ticket-1',
          shiftId: 'shift-cap',
          studentId: 'student-1',
          studentCode: 'SE170001',
          studentName: 'Nguyễn Văn A',
          eventType: 'WINDOW_BLUR',
          severity: 'HIGH',
          details: 'Mất tiêu điểm màn hình Kiosk',
          violationCount: 2,
          occurredAt: '2026-11-20T08:10:00.000Z',
        },
      ],
      latestAlert: null,
      reconnectVersion: 0,
      lastError: null,
      clearAlerts: vi.fn(),
    })
  })

  it.each([24, 30, 40])(
    'uses shift dropdown (zero raw UUID input), renders exact %i seats with deviceId, live violations, and Anti-Cheat feed',
    async (totalSeats) => {
      getShiftAttendance.mockResolvedValue({
        shiftId: 'shift-cap',
        shiftName: `Ca ${totalSeats} ghế`,
        roomLab: 'Lab 201',
        shiftStatus: 'SCHEDULED',
        startTime: '2026-11-20T08:00:00.000Z',
        endTime: '2026-11-20T09:30:00.000Z',
        totalSeats,
        assignedTicketsCount: 1,
        checkedInCount: 0,
        inProgressCount: 0,
        submittedCount: 0,
        suspendedCount: 0,
        seats: createSeats(totalSeats),
      })

      render(<ProctorAttendancePage />)
      expect(screen.queryByPlaceholderText('Nhập Shift ID')).not.toBeInTheDocument()

      const shiftSelect = await screen.findByLabelText('Chọn ca thi')
      await screen.findByRole('option', { name: /Ca sáng Lab 201/ })
      fireEvent.change(shiftSelect, { target: { value: 'shift-cap' } })
      fireEvent.click(screen.getByRole('button', { name: 'Tải sơ đồ ca thi' }))

      expect(await screen.findByText(`Sơ đồ ${totalSeats} ghế Lab`)).toBeInTheDocument()
      expect(screen.getByText(/Đề: SET-01/)).toBeInTheDocument()
      expect(screen.getByText('BYOD-WIN-01')).toBeInTheDocument()
      expect(screen.getByText('VP: 2')).toBeInTheDocument()
      expect(screen.getByText(/Mất tiêu điểm màn hình Kiosk/)).toBeInTheDocument()
      expect(screen.getByText(`#${String(totalSeats).padStart(2, '0')}`)).toBeInTheDocument()
      if (totalSeats < 40) {
        expect(screen.queryByText('#40')).not.toBeInTheDocument()
      }
    },
  )

  it('rejects changing a ticket to a seat greater than totalSeats and refreshes after valid seat change', async () => {
    getShiftAttendance.mockResolvedValue({
      shiftId: 'shift-24',
      shiftName: 'Ca mặc định 24',
      roomLab: 'Lab 202',
      shiftStatus: 'SCHEDULED',
      startTime: '2026-11-20T10:00:00.000Z',
      endTime: '2026-11-20T11:30:00.000Z',
      totalSeats: 24,
      assignedTicketsCount: 1,
      checkedInCount: 0,
      inProgressCount: 0,
      submittedCount: 0,
      suspendedCount: 0,
      seats: createSeats(24),
    })
    updateTicketAttendance.mockResolvedValue({
      ticketId: 'ticket-1',
      shiftId: 'shift-24',
      studentId: 'student-1',
      studentCode: 'SE170001',
      fullName: 'Nguyễn Văn A',
      seatNumber: 12,
      ipAddress: '192.168.1.10',
      setCode: 'SET-01',
      status: 'SCHEDULED',
      isSuspended: false,
      suspensionReason: null,
      updatedAt: '2026-11-20T10:05:00.000Z',
    })

    render(<ProctorAttendancePage />)
    const shiftSelect = await screen.findByLabelText('Chọn ca thi')
    await screen.findByRole('option', { name: /Ca mặc định 24/ })
    fireEvent.change(shiftSelect, { target: { value: 'shift-24' } })
    fireEvent.click(screen.getByRole('button', { name: 'Tải sơ đồ ca thi' }))

    const seat1Button = await screen.findByRole('button', { name: /#01/i })
    fireEvent.click(seat1Button)

    const seatInput = screen.getByLabelText('Số ghế mới')
    fireEvent.change(seatInput, { target: { value: '25' } })
    fireEvent.click(screen.getByRole('button', { name: /Đổi ghế/i }))

    expect(
      await screen.findByText('Không thể đổi vé sang ghế ngoài phạm vi 1–24 của ca thi.'),
    ).toBeInTheDocument()
    expect(updateTicketAttendance).not.toHaveBeenCalled()

    fireEvent.change(seatInput, { target: { value: '12' } })
    fireEvent.click(screen.getByRole('button', { name: /Đổi ghế/i }))

    await waitFor(() =>
      expect(updateTicketAttendance).toHaveBeenCalledWith('ticket-1', {
        isPresent: false,
        seatNumber: 12,
      }),
    )
    expect(getShiftAttendance).toHaveBeenCalledTimes(2)
  })
})
