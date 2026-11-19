import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AbuseMetricsPanel } from './AbuseMetricsPanel'

const get = vi.hoisted(() => vi.fn())
vi.mock('@/services/api.client', () => ({ apiClient: { get } }))

describe('AbuseMetricsPanel', () => {
  beforeEach(() => {
    get.mockReset()
  })

  it('shows process-scoped counters and refreshes on demand', async () => {
    get.mockResolvedValue({
      data: { requests: 20, rejected: 3, loginLockouts: 1, scope: 'api-process' },
    })
    render(
      <MemoryRouter>
        <AbuseMetricsPanel />
      </MemoryRouter>,
    )

    await screen.findByText('20')
    expect(screen.getByText('Yêu cầu')).toBeInTheDocument()
    expect(screen.getByText('Bị giới hạn')).toBeInTheDocument()
    expect(screen.getByText('3')).toBeInTheDocument()
    expect(screen.getByText('Khóa đăng nhập')).toBeInTheDocument()
    expect(screen.getByText('1')).toBeInTheDocument()
    expect(screen.getByText(/Đặt lại khi API khởi động lại/)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Làm mới chỉ số bảo vệ' }))
    await waitFor(() => expect(get).toHaveBeenCalledTimes(2))
    expect(get).toHaveBeenCalledWith('/diagnostics/abuse')
  })

  it('shows RFC 7807 alert and retry without invented zeroes on failure', async () => {
    get.mockRejectedValue({
      status: 403,
      detail: 'Bạn không có quyền truy cập chỉ số bảo vệ.',
    })
    render(
      <MemoryRouter>
        <AbuseMetricsPanel />
      </MemoryRouter>,
    )

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Bạn không có quyền truy cập chỉ số bảo vệ.')
    expect(screen.queryByText('0')).toBeNull()
    expect(screen.getByRole('button', { name: /Thử lại/i })).toBeInTheDocument()
  })
})
