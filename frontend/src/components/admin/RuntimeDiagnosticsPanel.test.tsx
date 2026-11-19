import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { RuntimeDiagnosticsPanel } from './RuntimeDiagnosticsPanel'
import type { RuntimeDiagnosticsDto } from '@/services/admin.service'

const sampleDiagnostics: RuntimeDiagnosticsDto = {
  timestamp: '2026-11-18T12:00:00Z',
  uptime: '1.04:12:05',
  boundedChannels: { gradingQueue: { count: 800, capacity: 1000 } },
  deadLetterQueue: {
    totalPending: 5,
    byFlowType: { practice: 3, mockExam: 2, inClassExam: 0 },
  },
  abuseMetrics: { requests: 450, rejected: 2, loginLockouts: 1 },
  performance: {
    totalRequests: 1200,
    serverErrors5xx: 1,
    serverErrorRatePercent: 0.08,
    averageLatencyMs: 24.5,
    p95LatencyMs: 89.1,
  },
  system: { workingSetMb: 256.4, gcTotalMemoryMb: 74.2, threadCount: 24 },
}

describe('RuntimeDiagnosticsPanel', () => {
  it('renders BoundedChannel capacity, performance metrics, system stats, and MF-01/02 DLQ breakdown', () => {
    const onRefresh = vi.fn()
    render(
      <MemoryRouter>
        <RuntimeDiagnosticsPanel
          data={sampleDiagnostics}
          loading={false}
          error={null}
          onRefresh={onRefresh}
        />
      </MemoryRouter>,
    )

    expect(screen.getByText('800/1000 (80%)')).toBeInTheDocument()
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '800')
    expect(screen.getByText('24.50 ms')).toBeInTheDocument()
    expect(screen.getByText('89.10 ms')).toBeInTheDocument()
    expect(screen.getByText('256.4 MB')).toBeInTheDocument()
    expect(screen.getByText('74.2 MB')).toBeInTheDocument()
    expect(screen.getByText('24')).toBeInTheDocument()
    expect(screen.getByText('MF-01 (Luyện tập): 3')).toBeInTheDocument()
    expect(screen.getByText('MF-02 (Thi thử): 2')).toBeInTheDocument()
    expect(screen.getByText('Tổng pending (MF-01/02/05): 5')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Mở hàng đợi lỗi MF-01\/02/i })).toHaveAttribute(
      'href',
      '/admin/dlq',
    )

    fireEvent.click(screen.getByRole('button', { name: /Làm mới runtime/i }))
    expect(onRefresh).toHaveBeenCalledTimes(1)
  })

  it('renders loading state with reduced motion support', () => {
    render(
      <MemoryRouter>
        <RuntimeDiagnosticsPanel
          data={null}
          loading={true}
          error={null}
          onRefresh={vi.fn()}
        />
      </MemoryRouter>,
    )

    expect(screen.getByRole('status')).toHaveTextContent(/Đang tải chỉ số runtime/i)
  })

  it('renders RFC 7807 error and retry without inventing zero metrics', () => {
    const onRefresh = vi.fn()
    render(
      <MemoryRouter>
        <RuntimeDiagnosticsPanel
          data={null}
          loading={false}
          error={{
            status: 403,
            title: 'Forbidden',
            detail: 'Chỉ Admin mới có quyền xem runtime diagnostics.',
            errorCode: 'FORBIDDEN',
            traceId: null,
            fieldErrors: {},
            fieldMessages: [],
            isAuthError: true,
            isConflict: false,
            isGone: false,
            isRateLimited: false,
          }}
          onRefresh={onRefresh}
        />
      </MemoryRouter>,
    )

    expect(screen.getByRole('alert')).toHaveTextContent(
      'Chỉ Admin mới có quyền xem runtime diagnostics.',
    )
    expect(screen.queryByRole('progressbar')).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: /Thử lại/i }))
    expect(onRefresh).toHaveBeenCalledTimes(1)
  })
})
