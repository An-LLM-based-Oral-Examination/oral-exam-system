import { beforeEach, describe, expect, it, vi } from 'vitest'
import apiClient from './api.client'
import { adminService } from './admin.service'

describe('adminService M2 contracts', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('fetches runtime diagnostics from /admin/diagnostics/runtime', async () => {
    const payload = {
      timestamp: '2026-11-18T12:00:00Z',
      uptime: '0.02:15:30',
      boundedChannels: { gradingQueue: { count: 12, capacity: 1000 } },
      deadLetterQueue: {
        totalPending: 3,
        byFlowType: { practice: 2, mockExam: 1, inClassExam: 0 },
      },
      abuseMetrics: { requests: 100, rejected: 2, loginLockouts: 0 },
      performance: {
        totalRequests: 100,
        serverErrors5xx: 0,
        serverErrorRatePercent: 0,
        averageLatencyMs: 18.4,
        p95LatencyMs: 42.1,
      },
      system: { workingSetMb: 128.5, gcTotalMemoryMb: 42.3, threadCount: 16 },
    }
    const get = vi.spyOn(apiClient, 'get').mockResolvedValue({ data: payload } as never)

    await expect(adminService.getRuntimeDiagnostics()).resolves.toEqual(payload)
    expect(get).toHaveBeenCalledWith('/admin/diagnostics/runtime')
  })

  it('fetches academic programs and assigns department head', async () => {
    const programs = [
      {
        id: 'prog-1',
        code: 'SE',
        name: 'Software Engineering',
        departmentHeadUserId: 'dh-1',
        departmentHeadName: 'Trưởng BM SE',
        isActive: true,
        coursesCount: 14,
      },
    ]
    const get = vi.spyOn(apiClient, 'get').mockResolvedValue({ data: programs } as never)
    const put = vi.spyOn(apiClient, 'put').mockResolvedValue({ data: programs[0] } as never)

    await expect(adminService.getAcademicPrograms(true)).resolves.toEqual(programs)
    expect(get).toHaveBeenCalledWith('/academic-programs', { params: { isActive: true } })

    await expect(adminService.assignProgramDepartmentHead('prog-1', 'dh-1')).resolves.toEqual(
      programs[0],
    )
    expect(put).toHaveBeenCalledWith('/academic-programs/prog-1/department-head', {
      departmentHeadUserId: 'dh-1',
    })
  })
})
