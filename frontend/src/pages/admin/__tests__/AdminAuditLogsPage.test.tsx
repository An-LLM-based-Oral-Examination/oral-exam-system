import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AdminAuditLogsPage } from '../AdminAuditLogsPage'
import { adminService } from '@/services/admin.service'

vi.mock('@/services/admin.service', () => ({
  DEFAULT_ADMIN_PAGE_SIZE: 20,
  adminService: {
    getAuditLogs: vi.fn(),
    getUsers: vi.fn(),
  },
}))

const jsonLog = {
  id: 'log-json',
  userId: 'u-1',
  userEmail: 'admin@fpt.edu.vn',
  userFullName: 'Admin One',
  action: 'USER_ROLE_CHANGED',
  entityName: 'users',
  entityId: 'user-99',
  oldValues: '{"role":"student","isActive":true}',
  newValues: '{"role":"lecturer","isActive":true}',
  ipAddress: '10.0.0.1',
  createdAt: '2026-11-18T09:00:00Z',
}

const kvLog = {
  id: 'log-kv',
  userId: 'u-1',
  userEmail: 'admin@fpt.edu.vn',
  userFullName: 'Admin One',
  action: 'USER_STATUS_CHANGED',
  entityName: 'users',
  entityId: 'user-88',
  oldValues: 'is_active=true; auth_version=1',
  newValues: 'is_active=false; auth_version=2 | Vi phạm quy chế',
  ipAddress: '10.0.0.2',
  createdAt: '2026-11-18T09:05:00Z',
}

const scalarLog = {
  id: 'log-scalar',
  userId: 'u-1',
  userEmail: 'admin@fpt.edu.vn',
  userFullName: 'Admin One',
  action: 'SYSTEM_CONFIG_UPDATED',
  entityName: 'system_configs',
  entityId: 'cfg-1',
  oldValues: '2',
  newValues: '3',
  ipAddress: '10.0.0.3',
  createdAt: '2026-11-18T09:10:00Z',
}

describe('AdminAuditLogsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(adminService.getUsers).mockResolvedValue({
      items: [
        {
          id: 'u-1',
          email: 'admin@fpt.edu.vn',
          fullName: 'Admin One',
          role: 'admin',
          isActive: true,
          mustChangePassword: false,
          createdAt: '2026-01-01T00:00:00Z',
          updatedAt: '2026-01-01T00:00:00Z',
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 50,
      totalPages: 1,
    })
    vi.mocked(adminService.getAuditLogs).mockResolvedValue({
      items: [jsonLog, kvLog, scalarLog],
      totalCount: 40,
      page: 1,
      pageSize: 20,
      totalPages: 2,
    })
  })

  it('requests page 2 from API when pagination changes (fixes loadLogs(1) regression) and restores URL filters', async () => {
    render(
      <MemoryRouter initialEntries={['/admin/audit-logs?entity=users&action=USER_CREATED&userId=u-1']}>
        <AdminAuditLogsPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(adminService.getAuditLogs).toHaveBeenCalledWith({
        entityName: 'users',
        action: 'USER_CREATED',
        userId: 'u-1',
        page: 1,
        pageSize: 20,
      })
    })

    fireEvent.click(screen.getByRole('button', { name: 'Trang 2' }))

    await waitFor(() => {
      expect(adminService.getAuditLogs).toHaveBeenCalledWith({
        entityName: 'users',
        action: 'USER_CREATED',
        userId: 'u-1',
        page: 2,
        pageSize: 20,
      })
    })
  })

  it('opens AuditLogDiffDialog parsing JSON, key=value with reason, and scalar payloads + Escape close', async () => {
    render(
      <MemoryRouter>
        <AdminAuditLogsPage />
      </MemoryRouter>,
    )

    const diffButtons = await screen.findAllByRole('button', { name: /Xem diff USER_STATUS_CHANGED/i })
    fireEvent.click(diffButtons[0])

    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(screen.getByText('Vi phạm quy chế')).toBeInTheDocument()
    expect(screen.getByText('is_active')).toBeInTheDocument()
    expect(screen.getAllByText('Thay đổi').length).toBeGreaterThanOrEqual(1)

    fireEvent.click(screen.getByRole('button', { name: /Xem dữ liệu gốc/i }))
    expect(screen.getByText('is_active=true; auth_version=1')).toBeInTheDocument()

    fireEvent.keyDown(window, { key: 'Escape' })
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).toBeNull()
    })

    const jsonDiffButtons = screen.getAllByRole('button', { name: /Xem diff USER_ROLE_CHANGED/i })
    fireEvent.click(jsonDiffButtons[0])
    expect(screen.getByText('student')).toBeInTheDocument()
    expect(screen.getByText('lecturer')).toBeInTheDocument()
  })

  it('renders both oldNote and newNote when both exist and differ, and hides EmptyState on fetch error', async () => {
    vi.mocked(adminService.getAuditLogs).mockResolvedValueOnce({
      items: [
        {
          ...kvLog,
          id: 'log-both-notes',
          oldValues: 'is_active=true | Ghi chú cũ',
          newValues: 'is_active=false | Ghi chú mới',
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    })

    const { unmount } = render(
      <MemoryRouter>
        <AdminAuditLogsPage />
      </MemoryRouter>,
    )

    const diffButtons = await screen.findAllByRole('button', { name: /Xem diff USER_STATUS_CHANGED/i })
    fireEvent.click(diffButtons[0])
    expect(screen.getByText('Ghi chú cũ')).toBeInTheDocument()
    expect(screen.getByText('Ghi chú mới')).toBeInTheDocument()
    unmount()

    vi.mocked(adminService.getAuditLogs).mockRejectedValueOnce({
      status: 500,
      detail: 'Không thể truy vấn audit logs.',
    })

    render(
      <MemoryRouter>
        <AdminAuditLogsPage />
      </MemoryRouter>,
    )

    expect(await screen.findByRole('alert')).toHaveTextContent('Không thể truy vấn audit logs.')
    expect(screen.queryByText('Chưa có nhật ký kiểm toán')).toBeNull()
  })

  it('R4/R6: fetches actors with pageSize 100, exposes all 6 new AUDIT_ACTION_PRESETS, supports custom ?userId fallback and actorsError retry', async () => {
    vi.mocked(adminService.getUsers)
      .mockRejectedValueOnce({ status: 503, detail: 'Danh bạ tạm bận' })
      .mockResolvedValueOnce({
        items: [
          {
            id: 'u-1',
            email: 'admin@fpt.edu.vn',
            fullName: 'Admin One',
            role: 'admin',
            isActive: true,
            mustChangePassword: false,
            createdAt: '2026-01-01T00:00:00Z',
            updatedAt: '2026-01-01T00:00:00Z',
          },
        ],
        totalCount: 1,
        page: 1,
        pageSize: 100,
        totalPages: 1,
      })

    render(
      <MemoryRouter initialEntries={['/admin/audit-logs?userId=u-missing']}>
        <AdminAuditLogsPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(adminService.getUsers).toHaveBeenCalledWith({ page: 1, pageSize: 100 })
      expect(screen.getByRole('status')).toHaveTextContent(
        /Không tải được danh sách người thực hiện/i,
      )
    })

    // Audit logs still render even when actors fails
    expect(screen.getAllByText('USER_ROLE_CHANGED').length).toBeGreaterThan(0)

    // Custom ?userId fallback option exists
    fireEvent.click(screen.getByRole('button', { name: 'Người thực hiện' }))
    expect(await screen.findByRole('option', { name: 'ID: u-missing (tùy chỉnh)' })).toBeInTheDocument()

    // Retry loading actors directory
    fireEvent.click(screen.getByRole('button', { name: 'Tải lại danh bạ' }))
    await waitFor(() => {
      expect(adminService.getUsers).toHaveBeenCalledTimes(2)
      expect(screen.queryByText(/Không tải được danh sách người thực hiện/i)).toBeNull()
    })

    // Verify all 6 R4 presets in action FilterDropMenu
    fireEvent.click(screen.getByRole('button', { name: 'Hành động (action)' }))
    const expectedNewPresets = [
      'USERS_BULK_IMPORTED — Import tài khoản hàng loạt',
      'USER_UPDATED — Cập nhật hồ sơ tài khoản',
      'ACADEMIC_PROGRAM_CREATED — Tạo chương trình ĐT',
      'ACADEMIC_PROGRAM_UPDATED — Cập nhật chương trình ĐT',
      'ACADEMIC_PROGRAM_DEPARTMENT_HEAD_ASSIGNED — Gán Trưởng BM ngành',
      'SEMESTER_COURSE_OFFERINGS_SYNCED — Đồng bộ môn mở theo kỳ',
      'COURSE_DEPARTMENT_HEAD_ASSIGNED — Gán Trưởng BM môn học',
    ]
    for (const label of expectedNewPresets) {
      expect(await screen.findByRole('option', { name: label })).toBeInTheDocument()
    }
  })
})
