import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AbuseMetricsPanel } from './AbuseMetricsPanel'

const get = vi.hoisted(() => vi.fn())
vi.mock('@/services/api.client', () => ({ apiClient: { get } }))

describe('AbuseMetricsPanel', () => {
  beforeEach(() => { get.mockReset() })
  it('shows process-scoped counters and refreshes on demand', async () => {
    get.mockResolvedValue({ data: { requests: 20, rejected: 3, loginLockouts: 1, scope: 'api-process' } })
    render(<AbuseMetricsPanel />)
    await screen.findByText('Yêu cầu: 20')
    expect(screen.getByText('Bị giới hạn: 3')).toBeTruthy()
    expect(screen.getByText('Khóa đăng nhập: 1')).toBeTruthy()
    expect(screen.getByText(/Đặt lại khi API khởi động lại/)).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Làm mới chỉ số bảo vệ' }))
    await waitFor(() => expect(get).toHaveBeenCalledTimes(2))
    expect(get).toHaveBeenCalledWith('/diagnostics/abuse')
  })
  it('shows unavailability instead of invented zeroes', async () => {
    get.mockRejectedValue(new Error('network unavailable'))
    render(<AbuseMetricsPanel />)
    await screen.findByRole('alert')
    expect(screen.queryByText('Yêu cầu: 0')).toBeNull()
  })
})
