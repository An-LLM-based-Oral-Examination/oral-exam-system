import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { SuspendTicketDialog } from '../SuspendTicketDialog'
import { proctorService } from '@/services/proctor.service'

vi.mock('@/services/proctor.service', () => ({
  proctorService: {
    suspendTicket: vi.fn(),
  },
}))

describe('SuspendTicketDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('validates >=10 char reason and calls proctorService.suspendTicket', async () => {
    const onSuspended = vi.fn()
    const onClose = vi.fn()
    vi.mocked(proctorService.suspendTicket).mockResolvedValue({
      ticketId: 'ticket-1',
      shiftId: 'shift-1',
      studentId: 'stu-1',
      studentCode: 'SE170001',
      fullName: 'Nguyễn Văn A',
      seatNumber: 3,
      ipAddress: null,
      examSetId: 'set-1',
      setCode: 'SET-01',
      status: 'LOCKED',
      isSuspended: true,
      suspensionReason: 'Rời khỏi màn hình thi nhiều lần và sử dụng tài liệu.',
      updatedAt: '2026-11-24T08:30:00.000Z',
    })

    render(
      <SuspendTicketDialog
        open
        target={{
          ticketId: 'ticket-1',
          studentCode: 'SE170001',
          fullName: 'Nguyễn Văn A',
          seatNumber: 3,
          violationCount: 4,
        }}
        onClose={onClose}
        onSuspended={onSuspended}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: /Xác nhận đình chỉ thi/i }))
    expect(await screen.findByText(/ít nhất 10 ký tự/i)).toBeInTheDocument()
    expect(proctorService.suspendTicket).not.toHaveBeenCalled()

    fireEvent.change(screen.getByLabelText(/Lý do đình chỉ thi/i), {
      target: { value: 'Rời khỏi màn hình thi nhiều lần và sử dụng tài liệu.' },
    })
    fireEvent.click(screen.getByRole('button', { name: /Xác nhận đình chỉ thi/i }))

    await waitFor(() =>
      expect(proctorService.suspendTicket).toHaveBeenCalledWith('ticket-1', {
        reason: 'Rời khỏi màn hình thi nhiều lần và sử dụng tài liệu.',
        blurViolationCount: 4,
      }),
    )
    expect(onSuspended).toHaveBeenCalledOnce()
    expect(onClose).toHaveBeenCalledOnce()
  })
})
