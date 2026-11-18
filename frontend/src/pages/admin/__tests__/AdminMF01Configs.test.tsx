import { MemoryRouter } from 'react-router-dom'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AdminDashboardPage } from '../AdminDashboardPage'
import { MF01_CONFIG_RULES } from '../mf01ConfigRules'
import { adminService, type SystemConfigItem } from '@/services/admin.service'
import { toast } from '@/components/common/toastStore'

vi.mock('@/components/admin/AbuseMetricsPanel', () => ({
  AbuseMetricsPanel: () => <div data-testid="abuse-metrics-panel" />,
}))

vi.mock('@/services/admin.service', () => ({
  adminService: {
    getUsers: vi.fn(),
    createUser: vi.fn(),
    updateUserStatus: vi.fn(),
    updateUserRole: vi.fn(),
    getSystemConfigs: vi.fn(),
    updateSystemConfig: vi.fn(),
    getAuditLogs: vi.fn(),
    getSemesters: vi.fn(),
    createSemester: vi.fn(),
    updateSemester: vi.fn(),
    getCourses: vi.fn(),
    createCourse: vi.fn(),
    updateCourse: vi.fn(),
    assignDepartmentHead: vi.fn(),
    getDepartmentHeads: vi.fn(),
  },
}))

vi.mock('@/components/common/toastStore', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
    info: vi.fn(),
  },
}))

const mockGetUsers = vi.mocked(adminService.getUsers)
const mockGetSystemConfigs = vi.mocked(adminService.getSystemConfigs)
const mockUpdateSystemConfig = vi.mocked(adminService.updateSystemConfig)
const mockToastError = vi.mocked(toast.error)
const mockToastSuccess = vi.mocked(toast.success)

const initialMF01Configs: SystemConfigItem[] = [
  {
    id: 'cfg-1',
    key: 'MaxPracticeFollowUpQuestions',
    value: '2',
    description: 'Số câu hỏi phụ tối đa khi điểm câu gốc trong [4.0, 8.0].',
    updatedAt: '2026-11-15T00:00:00Z',
  },
  {
    id: 'cfg-2',
    key: 'MinMixedPracticeQuestions',
    value: '3',
    description: 'Số câu hỏi tối thiểu trong phiên Progressive/Mixed.',
    updatedAt: '2026-11-15T00:00:00Z',
  },
  {
    id: 'cfg-3',
    key: 'MaxMixedPracticeQuestions',
    value: '10',
    description: 'Số câu hỏi tối đa trong phiên Progressive/Mixed.',
    updatedAt: '2026-11-15T00:00:00Z',
  },
  {
    id: 'cfg-4',
    key: 'TranscriptBufferSeconds',
    value: '60',
    description: 'Thời gian đệm nghe audio/sửa transcript.',
    updatedAt: '2026-11-15T00:00:00Z',
  },
  {
    id: 'cfg-5',
    key: 'SessionInactivityTimeoutMinutes',
    value: '5',
    description: 'Thời gian không tương tác trước khi tự động đóng phiên.',
    updatedAt: '2026-11-15T00:00:00Z',
  },
]

describe('AdminDashboardPage - MF-01 Configs Validation & Management (TASK-008A R1.2)', () => {
  beforeEach(() => {
    vi.clearAllMocks()

    mockGetUsers.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 15,
      totalPages: 1,
    })

    mockGetSystemConfigs.mockResolvedValue(initialMF01Configs)
  })

  it('1. Đọc đúng định nghĩa allowlist và rules cấu hình MF-01', () => {
    expect(MF01_CONFIG_RULES.MaxPracticeFollowUpQuestions.min).toBe(1)
    expect(MF01_CONFIG_RULES.MaxPracticeFollowUpQuestions.max).toBe(5)
    expect(MF01_CONFIG_RULES.MinMixedPracticeQuestions.min).toBe(3)
    expect(MF01_CONFIG_RULES.MinMixedPracticeQuestions.max).toBe(10)
    expect(MF01_CONFIG_RULES.TranscriptBufferSeconds.min).toBe(10)
    expect(MF01_CONFIG_RULES.TranscriptBufferSeconds.max).toBe(300)
    expect(MF01_CONFIG_RULES.SessionInactivityTimeoutMinutes.min).toBe(1)
    expect(MF01_CONFIG_RULES.SessionInactivityTimeoutMinutes.max).toBe(60)
  })

  it('2. Chuyển sang tab configs và hiển thị đầy đủ danh sách cấu hình MF-01', async () => {
    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>
    )

    const configsTab = screen.getByRole('button', { name: /cấu hình hệ thống/i })
    fireEvent.click(configsTab)

    await waitFor(() => {
      expect(mockGetSystemConfigs).toHaveBeenCalled()
      expect(screen.getByText('MaxPracticeFollowUpQuestions')).toBeInTheDocument()
      expect(screen.getByText('TranscriptBufferSeconds')).toBeInTheDocument()
      expect(screen.getByText('SessionInactivityTimeoutMinutes')).toBeInTheDocument()
    })
  })

  it('3. Từ chối giá trị ngoài khoảng hợp lệ của MaxPracticeFollowUpQuestions (min=1, max=5)', async () => {
    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>
    )

    const configsTab = screen.getByRole('button', { name: /cấu hình hệ thống/i })
    fireEvent.click(configsTab)

    await waitFor(() => {
      expect(screen.getByText('MaxPracticeFollowUpQuestions')).toBeInTheDocument()
    })

    const row = screen.getByText('MaxPracticeFollowUpQuestions').closest('tr')
    expect(row).not.toBeNull()
    const editBtn = row!.querySelector('button')
    expect(editBtn).not.toBeNull()
    fireEvent.click(editBtn!)

    expect(screen.getByText('Chỉnh Sửa Tham Số Hệ Thống')).toBeInTheDocument()

    const input = screen.getByDisplayValue('2')
    fireEvent.change(input, { target: { value: '6' } })

    const saveBtn = screen.getByRole('button', { name: /lưu cấu hình/i })
    fireEvent.click(saveBtn)

    expect(mockToastError).toHaveBeenCalledWith(
      expect.stringContaining('phải nằm trong khoảng từ 1 đến 5')
    )
    expect(mockUpdateSystemConfig).not.toHaveBeenCalled()
  })

  it('4. T8A-F011: Validate quan hệ min/max giữa MinMixedPracticeQuestions và MaxMixedPracticeQuestions khi cả hai giá trị đều nằm trong [3, 10]', async () => {
    // Chiều 1: Min hiện tại = 7, sửa Max thành 6 (cả 6 và 7 đều thuộc [3, 10], nhưng Max < Min)
    const customConfigs: SystemConfigItem[] = [
      ...initialMF01Configs.filter(
        (c) => c.key !== 'MinMixedPracticeQuestions' && c.key !== 'MaxMixedPracticeQuestions'
      ),
      {
        id: 'cfg-2',
        key: 'MinMixedPracticeQuestions',
        value: '7',
        description: 'Số câu tối thiểu',
        updatedAt: '2026-11-15T00:00:00Z',
      },
      {
        id: 'cfg-3',
        key: 'MaxMixedPracticeQuestions',
        value: '10',
        description: 'Số câu tối đa',
        updatedAt: '2026-11-15T00:00:00Z',
      },
    ]
    mockGetSystemConfigs.mockResolvedValue(customConfigs)

    const { unmount } = render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>
    )

    const configsTab = screen.getByRole('button', { name: /cấu hình hệ thống/i })
    fireEvent.click(configsTab)

    await waitFor(() => {
      expect(screen.getByText('MaxMixedPracticeQuestions')).toBeInTheDocument()
    })

    const maxRow = screen.getByText('MaxMixedPracticeQuestions').closest('tr')
    const maxEditBtn = maxRow!.querySelector('button')
    fireEvent.click(maxEditBtn!)

    const maxInput = screen.getByDisplayValue('10')
    // Nhập Max = 6 (nằm trong [3, 10], nhưng nhỏ hơn Min=7 hiện tại)
    fireEvent.change(maxInput, { target: { value: '6' } })

    const saveBtn = screen.getByRole('button', { name: /lưu cấu hình/i })
    fireEvent.click(saveBtn)

    expect(mockToastError).toHaveBeenCalledWith(
      'Số câu tối đa (6) không được nhỏ hơn số câu tối thiểu (7).'
    )
    expect(mockUpdateSystemConfig).not.toHaveBeenCalled()

    unmount()

    // Chiều 2: Max hiện tại = 5, sửa Min thành 6 (cả 5 và 6 đều thuộc [3, 10], nhưng Min > Max)
    const customConfigs2: SystemConfigItem[] = [
      ...initialMF01Configs.filter(
        (c) => c.key !== 'MinMixedPracticeQuestions' && c.key !== 'MaxMixedPracticeQuestions'
      ),
      {
        id: 'cfg-2',
        key: 'MinMixedPracticeQuestions',
        value: '3',
        description: 'Số câu tối thiểu',
        updatedAt: '2026-11-15T00:00:00Z',
      },
      {
        id: 'cfg-3',
        key: 'MaxMixedPracticeQuestions',
        value: '5',
        description: 'Số câu tối đa',
        updatedAt: '2026-11-15T00:00:00Z',
      },
    ]
    mockGetSystemConfigs.mockResolvedValue(customConfigs2)

    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>
    )

    const configsTab2 = screen.getByRole('button', { name: /cấu hình hệ thống/i })
    fireEvent.click(configsTab2)

    await waitFor(() => {
      expect(screen.getByText('MinMixedPracticeQuestions')).toBeInTheDocument()
    })

    const minRow = screen.getByText('MinMixedPracticeQuestions').closest('tr')
    const minEditBtn = minRow!.querySelector('button')
    fireEvent.click(minEditBtn!)

    const minInput = screen.getByDisplayValue('3')
    // Nhập Min = 6 (nằm trong [3, 10], nhưng lớn hơn Max=5 hiện tại)
    fireEvent.change(minInput, { target: { value: '6' } })

    const saveBtn2 = screen.getByRole('button', { name: /lưu cấu hình/i })
    fireEvent.click(saveBtn2)

    expect(mockToastError).toHaveBeenCalledWith(
      'Số câu tối thiểu (6) không được lớn hơn số câu tối đa (5).'
    )
    expect(mockUpdateSystemConfig).not.toHaveBeenCalled()
  })

  it('5. T8A-F011: Cập nhật thành công các giá trị biên hợp lệ (min & max boundary values)', async () => {
    mockUpdateSystemConfig.mockResolvedValue({
      id: 'cfg-1',
      key: 'MaxPracticeFollowUpQuestions',
      value: '1',
      description: 'Số câu hỏi phụ tối đa',
      updatedAt: '2026-11-15T00:00:00Z',
    })

    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>
    )

    const configsTab = screen.getByRole('button', { name: /cấu hình hệ thống/i })
    fireEvent.click(configsTab)

    await waitFor(() => {
      expect(screen.getByText('MaxPracticeFollowUpQuestions')).toBeInTheDocument()
    })

    // Thử biên dưới: 1
    const row = screen.getByText('MaxPracticeFollowUpQuestions').closest('tr')
    const editBtn = row!.querySelector('button')
    fireEvent.click(editBtn!)

    const input = screen.getByDisplayValue('2')
    fireEvent.change(input, { target: { value: '1' } })

    const saveBtn = screen.getByRole('button', { name: /lưu cấu hình/i })
    fireEvent.click(saveBtn)

    await waitFor(() => {
      expect(mockUpdateSystemConfig).toHaveBeenCalledWith('MaxPracticeFollowUpQuestions', {
        value: '1',
        description: 'Số câu hỏi phụ tối đa khi điểm câu gốc trong [4.0, 8.0].',
      })
      expect(mockToastSuccess).toHaveBeenCalledWith(
        expect.stringContaining('Đã cập nhật cấu hình [MaxPracticeFollowUpQuestions]')
      )
    })
  })

  it('6. Hiển thị chi tiết lỗi RFC 7807 trả về từ backend khi cập nhật thất bại', async () => {
    mockUpdateSystemConfig.mockRejectedValueOnce({
      response: {
        data: {
          detail: 'Giá trị cấu hình không nằm trong giới hạn cho phép của hệ thống.',
          status: 400,
          title: 'Bad Request',
        },
      },
    })

    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>
    )

    const configsTab = screen.getByRole('button', { name: /cấu hình hệ thống/i })
    fireEvent.click(configsTab)

    await waitFor(() => {
      expect(screen.getByText('SessionInactivityTimeoutMinutes')).toBeInTheDocument()
    })

    const row = screen.getByText('SessionInactivityTimeoutMinutes').closest('tr')
    const editBtn = row!.querySelector('button')
    fireEvent.click(editBtn!)

    const input = screen.getByDisplayValue('5')
    fireEvent.change(input, { target: { value: '10' } })

    const saveBtn = screen.getByRole('button', { name: /lưu cấu hình/i })
    fireEvent.click(saveBtn)

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith(
        'Giá trị cấu hình không nằm trong giới hạn cho phép của hệ thống.'
      )
    })
  })

  it('7. T8A-F011: Hiển thị đúng trạng thái đang tải (Loading State)', async () => {
    // Trả về promise chưa resolve ngay để bắt loading
    let resolveConfigs: (data: SystemConfigItem[]) => void
    mockGetSystemConfigs.mockReturnValueOnce(
      new Promise((res) => {
        resolveConfigs = res
      })
    )

    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>
    )

    const configsTab = screen.getByRole('button', { name: /cấu hình hệ thống/i })
    fireEvent.click(configsTab)

    await waitFor(() => {
      expect(screen.getByText('Đang tải danh sách tham số...')).toBeInTheDocument()
    })

    // Resolve để kết thúc sạch
    resolveConfigs!(initialMF01Configs)
    await waitFor(() => {
      expect(screen.getByText('MaxPracticeFollowUpQuestions')).toBeInTheDocument()
    })
  })

  it('8. T8A-F011: Hiển thị đúng trạng thái danh sách rỗng (Empty State)', async () => {
    mockGetSystemConfigs.mockResolvedValueOnce([])

    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>
    )

    const configsTab = screen.getByRole('button', { name: /cấu hình hệ thống/i })
    fireEvent.click(configsTab)

    await waitFor(() => {
      expect(
        screen.getByText('Chưa có tham số hệ thống nào trong bảng system_configs.')
      ).toBeInTheDocument()
    })
  })

  it('9. T8A-F011: Xử lý lỗi khi tải danh sách cấu hình thất bại (Error State)', async () => {
    mockGetSystemConfigs.mockRejectedValueOnce(new Error('Lỗi kết nối máy chủ cấu hình'))

    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>
    )

    const configsTab = screen.getByRole('button', { name: /cấu hình hệ thống/i })
    fireEvent.click(configsTab)

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith(
        'Không thể tải danh sách cấu hình hệ thống.'
      )
    })
  })
})
