import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { UnlockDeviceDialog } from '../UnlockDeviceDialog'
import { officialExamService } from '@/services/officialExam.service'

vi.mock('@/services/officialExam.service', () => ({
  officialExamService: {
    unlockTicketDevice: vi.fn(),
  },
}))

const sampleTarget = {
  ticketId: 'ticket-1',
  studentCode: 'SE170001',
  fullName: 'Nguyễn Văn A',
  seatNumber: 4,
  deviceId: 'BYOD-LAPTOP-ABC12345',
  deviceUnlockCount: 0,
}

describe('UnlockDeviceDialog (EO-only BYOD Device Unlock)', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('enforces 10-500 char reason with live counter and submits valid unlock request', async () => {
    vi.mocked(officialExamService.unlockTicketDevice).mockResolvedValue({
      ticketId: 'ticket-1',
      shiftId: 'shift-1',
      studentCode: 'SE170001',
      seatNumber: 4,
      previousDeviceId: 'BYOD-LAPTOP-ABC12345',
      deviceUnlockCount: 1,
      unlockedBy: 'eo-1',
      unlockedAt: '2026-11-20T08:15:00.000Z',
      reason: 'Laptop cá nhân bị sập nguồn, chuyển máy dự phòng Lab 301.',
    })

    const onClose = vi.fn()
    const onUnlocked = vi.fn()
    render(
      <UnlockDeviceDialog
        open
        target={sampleTarget}
        onClose={onClose}
        onUnlocked={onUnlocked}
      />,
    )

    expect(screen.getByText('0/500')).toBeInTheDocument()
    const textarea = screen.getByLabelText(/Lý do mở khóa thiết bị/)
    fireEvent.change(textarea, { target: { value: 'Quá ngắn' } })
    expect(screen.getByText('8/500')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Xác nhận mở khóa thiết bị/ }))
    expect(
      await screen.findByText('Lý do mở khóa thiết bị cần ít nhất 10 ký tự.'),
    ).toBeInTheDocument()
    expect(officialExamService.unlockTicketDevice).not.toHaveBeenCalled()

    const validReason = 'Laptop cá nhân bị sập nguồn, chuyển máy dự phòng Lab 301.'
    fireEvent.change(textarea, { target: { value: validReason } })
    fireEvent.click(screen.getByRole('button', { name: /Xác nhận mở khóa thiết bị/ }))

    await waitFor(() =>
      expect(officialExamService.unlockTicketDevice).toHaveBeenCalledWith('ticket-1', validReason),
    )
    expect(onUnlocked).toHaveBeenCalledTimes(1)
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('displays RFC 7807 409 Conflict error inline when ticket is already submitted or locked', async () => {
    vi.mocked(officialExamService.unlockTicketDevice).mockRejectedValue({
      status: 409,
      detail: 'Không thể mở khóa thiết bị cho vé thi đã nộp bài hoặc đã bị khóa điểm.',
    })

    const onClose = vi.fn()
    render(
      <UnlockDeviceDialog
        open
        target={sampleTarget}
        onClose={onClose}
        onUnlocked={vi.fn()}
      />,
    )

    fireEvent.change(screen.getByLabelText(/Lý do mở khóa thiết bị/), {
      target: { value: 'Yêu cầu mở khóa khi vé thi đã hoàn tất nộp bài.' },
    })
    fireEvent.click(screen.getByRole('button', { name: /Xác nhận mở khóa thiết bị/ }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Không thể mở khóa thiết bị cho vé thi đã nộp bài hoặc đã bị khóa điểm.',
    )
    expect(onClose).not.toHaveBeenCalled()
  })
})
