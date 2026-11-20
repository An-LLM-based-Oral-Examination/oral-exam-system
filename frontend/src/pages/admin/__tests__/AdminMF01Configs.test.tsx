import { MemoryRouter } from 'react-router-dom'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AdminConfigsPage } from '../AdminConfigsPage'
import { MF01_CONFIG_RULES } from '../mf01ConfigRules'
import { adminService, type SystemConfigItem } from '@/services/admin.service'
import { toast } from '@/components/common/toastStore'

vi.mock('@/services/admin.service', () => ({
  adminService: {
    getSystemConfigs: vi.fn(),
    updateSystemConfig: vi.fn(),
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

describe('AdminConfigsPage — MF-01 Configs Validation & Management (M3)', () => {
  beforeEach(() => {
    vi.clearAllMocks()
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

  it('2. Hiển thị danh sách cấu hình MF-01 và khôi phục bộ lọc ?q= từ URL', async () => {
    render(
      <MemoryRouter initialEntries={['/admin/configs?q=Transcript']}>
        <AdminConfigsPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(mockGetSystemConfigs).toHaveBeenCalled()
      expect(screen.getAllByText('TranscriptBufferSeconds').length).toBeGreaterThan(0)
    })
    expect(screen.queryByText('MaxPracticeFollowUpQuestions')).not.toBeInTheDocument()
    expect(screen.getByPlaceholderText(/Tìm khóa tham số/i)).toHaveValue('Transcript')
  })

  it('3. Từ chối giá trị ngoài khoảng hợp lệ hoặc không phải số nguyên và hiện FieldError', async () => {
    render(
      <MemoryRouter>
        <AdminConfigsPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getAllByText('MaxPracticeFollowUpQuestions').length).toBeGreaterThan(0)
    })

    fireEvent.click(
      screen.getAllByRole('button', { name: 'Chỉnh sửa MaxPracticeFollowUpQuestions' })[0],
    )
    expect(screen.getByRole('dialog')).toBeInTheDocument()

    const input = screen.getByLabelText(/Giá trị cấu hình/i)

    // Decimal string '2.5'
    fireEvent.change(input, { target: { value: '2.5' } })
    fireEvent.click(screen.getByRole('button', { name: /lưu cấu hình/i }))
    expect(mockToastError).toHaveBeenCalledWith(
      'Giá trị cấu hình [MaxPracticeFollowUpQuestions] phải là một số nguyên hợp lệ.',
    )
    expect(
      screen.getByText(
        'Giá trị cấu hình [MaxPracticeFollowUpQuestions] phải là một số nguyên hợp lệ.',
      ),
    ).toBeInTheDocument()

    // Non-numeric 'abc' (coerced to empty by number input) and explicit empty ''
    fireEvent.change(input, { target: { value: '3' } })
    fireEvent.change(input, { target: { value: 'abc' } })
    fireEvent.click(screen.getByRole('button', { name: /lưu cấu hình/i }))
    expect(
      screen.getByText(
        'Giá trị cấu hình [MaxPracticeFollowUpQuestions] phải là một số nguyên hợp lệ.',
      ),
    ).toBeInTheDocument()

    fireEvent.change(input, { target: { value: '3' } })
    fireEvent.change(input, { target: { value: '' } })
    fireEvent.click(screen.getByRole('button', { name: /lưu cấu hình/i }))
    expect(
      screen.getByText(
        'Giá trị cấu hình [MaxPracticeFollowUpQuestions] phải là một số nguyên hợp lệ.',
      ),
    ).toBeInTheDocument()

    // Out-of-range '6'
    fireEvent.change(input, { target: { value: '6' } })
    fireEvent.click(screen.getByRole('button', { name: /lưu cấu hình/i }))

    expect(mockToastError).toHaveBeenCalledWith(
      expect.stringContaining('phải nằm trong khoảng từ 1 đến 5'),
    )
    expect(screen.getByText(/phải nằm trong khoảng từ 1 đến 5/i)).toBeInTheDocument()
    expect(mockUpdateSystemConfig).not.toHaveBeenCalled()

    // Escape closes dialog cleanly
    fireEvent.keyDown(window, { key: 'Escape' })
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    })
  })

  it('4. T8A-F011: Validate hai chiều quan hệ Min/Max giữa MinMixedPracticeQuestions và MaxMixedPracticeQuestions', async () => {
    const customConfigs: SystemConfigItem[] = [
      ...initialMF01Configs.filter(
        (c) => c.key !== 'MinMixedPracticeQuestions' && c.key !== 'MaxMixedPracticeQuestions',
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
        <AdminConfigsPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getAllByText('MaxMixedPracticeQuestions').length).toBeGreaterThan(0)
    })

    fireEvent.click(
      screen.getAllByRole('button', { name: 'Chỉnh sửa MaxMixedPracticeQuestions' })[0],
    )
    fireEvent.change(screen.getByLabelText(/Giá trị cấu hình/i), { target: { value: '6' } })
    fireEvent.click(screen.getByRole('button', { name: /lưu cấu hình/i }))

    expect(mockToastError).toHaveBeenCalledWith(
      'Số câu tối đa (6) không được nhỏ hơn số câu tối thiểu (7).',
    )
    expect(
      screen.getByText('Số câu tối đa (6) không được nhỏ hơn số câu tối thiểu (7).'),
    ).toBeInTheDocument()
    expect(mockUpdateSystemConfig).not.toHaveBeenCalled()
    unmount()

    const customConfigs2: SystemConfigItem[] = [
      ...initialMF01Configs.filter(
        (c) => c.key !== 'MinMixedPracticeQuestions' && c.key !== 'MaxMixedPracticeQuestions',
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
        <AdminConfigsPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getAllByText('MinMixedPracticeQuestions').length).toBeGreaterThan(0)
    })

    fireEvent.click(
      screen.getAllByRole('button', { name: 'Chỉnh sửa MinMixedPracticeQuestions' })[0],
    )
    fireEvent.change(screen.getByLabelText(/Giá trị cấu hình/i), { target: { value: '6' } })
    fireEvent.click(screen.getByRole('button', { name: /lưu cấu hình/i }))

    expect(mockToastError).toHaveBeenCalledWith(
      'Số câu tối thiểu (6) không được lớn hơn số câu tối đa (5).',
    )
    expect(mockUpdateSystemConfig).not.toHaveBeenCalled()
  })

  it('5. Cập nhật thành công giá trị biên và hiện inline banner + toast', async () => {
    mockUpdateSystemConfig.mockResolvedValue({
      id: 'cfg-1',
      key: 'MaxPracticeFollowUpQuestions',
      value: '1',
      description: 'Số câu hỏi phụ tối đa',
      updatedAt: '2026-11-15T00:00:00Z',
    })

    render(
      <MemoryRouter>
        <AdminConfigsPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getAllByText('MaxPracticeFollowUpQuestions').length).toBeGreaterThan(0)
    })

    fireEvent.click(
      screen.getAllByRole('button', { name: 'Chỉnh sửa MaxPracticeFollowUpQuestions' })[0],
    )
    fireEvent.change(screen.getByLabelText(/Giá trị cấu hình/i), { target: { value: '1' } })
    fireEvent.click(screen.getByRole('button', { name: /lưu cấu hình/i }))

    await waitFor(() => {
      expect(mockUpdateSystemConfig).toHaveBeenCalledWith('MaxPracticeFollowUpQuestions', {
        value: '1',
        description: 'Số câu hỏi phụ tối đa khi điểm câu gốc trong [4.0, 8.0].',
      })
      expect(mockToastSuccess).toHaveBeenCalledWith(
        expect.stringContaining('Đã cập nhật cấu hình [MaxPracticeFollowUpQuestions]'),
      )
      expect(screen.getByRole('status')).toHaveTextContent(
        'Đã cập nhật cấu hình [MaxPracticeFollowUpQuestions].',
      )
    })
  })

  it('6. Ánh xạ lỗi RFC 7807 cả dạng Axios response lẫn flattened 400/422 vào dialog alert & FieldError', async () => {
    mockUpdateSystemConfig
      .mockRejectedValueOnce({
        response: {
          status: 400,
          data: {
            detail: 'Giá trị cấu hình không nằm trong giới hạn cho phép của hệ thống.',
            status: 400,
            title: 'Bad Request',
            errors: { Value: ['Giá trị vượt ngưỡng cho phép.'] },
          },
        },
      })
      .mockRejectedValueOnce({
        status: 422,
        detail: 'Vi phạm ràng buộc nghiệp vụ cấu hình.',
        errors: { Value: ['Không hợp lệ ở mức 422.'] },
      })

    render(
      <MemoryRouter>
        <AdminConfigsPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getAllByText('SessionInactivityTimeoutMinutes').length).toBeGreaterThan(0)
    })

    fireEvent.click(
      screen.getAllByRole('button', { name: 'Chỉnh sửa SessionInactivityTimeoutMinutes' })[0],
    )
    fireEvent.change(screen.getByLabelText(/Giá trị cấu hình/i), { target: { value: '10' } })
    fireEvent.click(screen.getByRole('button', { name: /lưu cấu hình/i }))

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith(
        'Giá trị cấu hình không nằm trong giới hạn cho phép của hệ thống.',
      )
      expect(screen.getByRole('alert')).toHaveTextContent(
        'Giá trị cấu hình không nằm trong giới hạn cho phép của hệ thống.',
      )
      expect(screen.getByText('Giá trị vượt ngưỡng cho phép.')).toBeInTheDocument()
    })

    // Flattened 422 ProblemDetails
    fireEvent.click(screen.getByRole('button', { name: /lưu cấu hình/i }))
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(
        'Vi phạm ràng buộc nghiệp vụ cấu hình.',
      )
      expect(screen.getByText('Không hợp lệ ở mức 422.')).toBeInTheDocument()
    })
  })

  it('7. Khởi tạo các tham số mặc định còn thiếu khi system_configs chưa đủ 5 khóa MF-01', async () => {
    mockGetSystemConfigs
      .mockResolvedValueOnce([initialMF01Configs[0]])
      .mockResolvedValueOnce(initialMF01Configs)
    mockUpdateSystemConfig.mockResolvedValue(initialMF01Configs[1])

    render(
      <MemoryRouter>
        <AdminConfigsPage />
      </MemoryRouter>,
    )

    const initBtn = await screen.findByRole('button', {
      name: /Khởi tạo 4 tham số mặc định còn thiếu/i,
    })
    fireEvent.click(initBtn)

    await waitFor(() => {
      expect(mockUpdateSystemConfig).toHaveBeenCalledTimes(4)
      expect(screen.getByRole('status')).toHaveTextContent(
        /Đã khởi tạo 4 tham số mặc định/i,
      )
    })
  })

  it('8. Hiển thị Loading State, Empty State và ẩn EmptyState khi có lỗi 401/403/429/500 kèm Retry', async () => {
    let resolveConfigs: (data: SystemConfigItem[]) => void
    mockGetSystemConfigs
      .mockReturnValueOnce(
        new Promise((res) => {
          resolveConfigs = res
        }),
      )
      .mockResolvedValueOnce([])
      .mockRejectedValueOnce({ status: 401, detail: 'Phiên đăng nhập hết hạn.' })
      .mockRejectedValueOnce({ status: 403, detail: 'Không có quyền xem cấu hình.' })
      .mockRejectedValueOnce({ status: 429, detail: 'Quá giới hạn yêu cầu.' })
      .mockRejectedValueOnce({
        status: 500,
        detail: 'Npgsql.PostgresException: 42703: column "secret_col" does not exist',
      })
      .mockResolvedValueOnce(initialMF01Configs)

    render(
      <MemoryRouter>
        <AdminConfigsPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getByText('Đang tải danh sách tham số...')).toBeInTheDocument()
    })

    resolveConfigs!(initialMF01Configs)
    await waitFor(() => {
      expect(screen.getAllByText('MaxPracticeFollowUpQuestions').length).toBeGreaterThan(0)
    })

    // Empty state
    fireEvent.click(screen.getByRole('button', { name: 'Làm mới' }))
    await waitFor(() => {
      expect(
        screen.getByText('Chưa có tham số hệ thống nào trong bảng system_configs.'),
      ).toBeInTheDocument()
    })

    // 401 Error suppresses EmptyState and shows re-login link
    fireEvent.click(screen.getByRole('button', { name: 'Làm mới' }))
    await waitFor(() => {
      expect(screen.getByRole('link', { name: 'Đăng nhập lại' })).toHaveAttribute('href', '/login')
      expect(
        screen.queryByText('Chưa có tham số hệ thống nào trong bảng system_configs.'),
      ).toBeNull()
    })

    // Retry -> 403 -> Retry -> 429 -> Retry -> 500 (Postgres sanitized) -> Retry -> success
    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }))
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Không có quyền xem cấu hình.')
    })

    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }))
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Quá giới hạn yêu cầu.')
    })

    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }))
    await waitFor(() => {
      const alert = screen.getByRole('alert')
      expect(alert).toHaveTextContent('Không thể tải danh sách cấu hình hệ thống.')
      expect(alert).not.toHaveTextContent(/Npgsql|42703|secret_col/i)
      expect(
        screen.queryByText('Chưa có tham số hệ thống nào trong bảng system_configs.'),
      ).toBeNull()
    })

    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }))
    await waitFor(() => {
      expect(screen.getAllByText('MaxPracticeFollowUpQuestions').length).toBeGreaterThan(0)
    })
  })

  it('9. Làm mới danh sách sau khởi tạo mặc định lỗi một phần và ẩn EmptyState khi có listProblem dù filter 0 kết quả', async () => {
    mockGetSystemConfigs
      .mockResolvedValueOnce([initialMF01Configs[0]])
      .mockResolvedValueOnce([initialMF01Configs[0], initialMF01Configs[1]])
    mockUpdateSystemConfig
      .mockResolvedValueOnce(initialMF01Configs[1])
      .mockRejectedValueOnce({ status: 429, detail: 'Vượt giới hạn tốc độ.' })

    render(
      <MemoryRouter initialEntries={['/admin/configs?q=NonExistentKey']}>
        <AdminConfigsPage />
      </MemoryRouter>,
    )

    const initBtn = await screen.findByRole('button', {
      name: /Khởi tạo 4 tham số mặc định còn thiếu/i,
    })
    fireEvent.click(initBtn)

    await waitFor(() => {
      expect(mockGetSystemConfigs).toHaveBeenCalledTimes(2)
      expect(screen.getByRole('alert')).toHaveTextContent('Vượt giới hạn tốc độ.')
    })

    // EmptyState for search must stay suppressed while listProblem is active
    expect(screen.queryByText('Không tìm thấy tham số phù hợp')).toBeNull()
  })
})
