import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AdminOverviewPage } from '../AdminOverviewPage'
import { adminService } from '@/services/admin.service'

vi.mock('@/components/admin/AbuseMetricsPanel', () => ({
  AbuseMetricsPanel: () => <div data-testid="abuse-metrics-panel" />,
}))

vi.mock('@/services/admin.service', () => ({
  adminService: {
    getUsers: vi.fn(),
    getSemesters: vi.fn(),
    getCourses: vi.fn(),
    getRuntimeDiagnostics: vi.fn(),
  },
}))

describe('AdminOverviewPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(adminService.getUsers).mockResolvedValue({
      items: [],
      totalCount: 42,
      page: 1,
      pageSize: 1,
      totalPages: 42,
    })
    vi.mocked(adminService.getSemesters).mockResolvedValue([
      {
        id: 'sem-1',
        code: 'FA26',
        name: 'Fall 2026',
        startDate: '2026-09-01',
        endDate: '2026-12-31',
        isActive: true,
        createdAt: '2026-08-01T00:00:00Z',
      },
      {
        id: 'sem-2',
        code: 'SU26',
        name: 'Summer 2026',
        startDate: '2026-05-01',
        endDate: '2026-08-31',
        isActive: false,
        createdAt: '2026-04-01T00:00:00Z',
      },
    ])
    vi.mocked(adminService.getCourses).mockResolvedValue([
      {
        id: 'c-1',
        code: 'PRN231',
        name: 'Cross-Platform',
        credits: 3,
        maxMockExamsPerDay: 3,
        mockExamDurationMinutes: 30,
        mockExamTotalQuestions: 5,
        hasFollowUp: false,
        examInputMode: 'VoiceWithTranscriptEdit',
        isActive: true,
      },
    ])
    vi.mocked(adminService.getRuntimeDiagnostics).mockResolvedValue({
      timestamp: '2026-11-18T12:00:00Z',
      uptime: '0.01:00:00',
      boundedChannels: { gradingQueue: { count: 4, capacity: 1000 } },
      deadLetterQueue: {
        totalPending: 2,
        byFlowType: { practice: 1, mockExam: 1, inClassExam: 0 },
      },
      abuseMetrics: { requests: 10, rejected: 0, loginLockouts: 0 },
      performance: {
        totalRequests: 10,
        serverErrors5xx: 0,
        serverErrorRatePercent: 0,
        averageLatencyMs: 12.5,
        p95LatencyMs: 25.0,
      },
      system: { workingSetMb: 120, gcTotalMemoryMb: 40, threadCount: 12 },
    })
  })

  it('renders KPI strip, diagnostics panels, and quick nav strictly within /admin/*', async () => {
    render(
      <MemoryRouter>
        <AdminOverviewPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getByText('42')).toBeInTheDocument()
      expect(screen.getByText('1/2')).toBeInTheDocument()
      expect(screen.getByText('1/1')).toBeInTheDocument()
      expect(screen.getByText('4/1000 (0%)')).toBeInTheDocument()
    })

    expect(screen.getByTestId('abuse-metrics-panel')).toBeInTheDocument()

    const links = screen.getAllByRole('link').map((el) => el.getAttribute('href'))
    expect(links.length).toBeGreaterThanOrEqual(10)
    expect(links.every((href) => href?.startsWith('/admin/'))).toBe(true)
    expect(links.some((href) => href?.includes('official-exam') || href?.includes('appeals'))).toBe(
      false,
    )
  })

  it('handles partial Promise.allSettled failure gracefully and preserves working panels', async () => {
    vi.mocked(adminService.getUsers).mockRejectedValueOnce({
      status: 503,
      detail: 'Dịch vụ người dùng tạm thời quá tải.',
    })

    render(
      <MemoryRouter>
        <AdminOverviewPage />
      </MemoryRouter>,
    )

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Dịch vụ người dùng tạm thời quá tải.')
    expect(screen.getByText('1/2')).toBeInTheDocument()
  })
})
