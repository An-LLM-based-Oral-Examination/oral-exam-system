import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AdminNotificationsPage } from '../AdminNotificationsPage'
import { adminService } from '@/services/admin.service'

vi.mock('@/services/admin.service', () => ({
  DEFAULT_ADMIN_PAGE_SIZE: 20,
  adminService: {
    getNotifications: vi.fn(),
    createNotification: vi.fn(),
    deleteNotification: vi.fn(),
    getUsers: vi.fn(),
  },
}))

const sampleNotification = {
  id: 'notif-1',
  userId: 'user-1',
  userEmail: 'sv01@fpt.edu.vn',
  userFullName: 'Nguyễn Sinh Viên',
  userRole: 'student',
  title: 'Thông báo lịch thi thử',
  message: 'Lịch thi thử mở từ thứ Hai.',
  type: 'ACADEMIC_NOTICE',
  isRead: false,
  createdAt: '2026-11-16T07:00:00Z',
}

describe('AdminNotificationsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(adminService.getNotifications).mockResolvedValue({
      items: [sampleNotification],
      totalCount: 25,
      unreadTotalCount: 4,
      page: 1,
      pageSize: 20,
      totalPages: 2,
    })
    vi.mocked(adminService.getUsers).mockResolvedValue({
      items: [
        {
          id: 'u-active',
          email: 'sv02@fpt.edu.vn',
          fullName: 'Trần Văn B',
          role: 'student',
          isActive: true,
          mustChangePassword: false,
          createdAt: '2026-10-01T00:00:00Z',
          updatedAt: '2026-10-01T00:00:00Z',
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 30,
      totalPages: 1,
    })
  })

  it('auto-fetches from URL params and updates when filters or pagination change', async () => {
    render(
      <MemoryRouter initialEntries={['/admin/notifications?type=ACADEMIC_NOTICE&read=unread']}>
        <AdminNotificationsPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(adminService.getNotifications).toHaveBeenCalledWith({
        search: undefined,
        type: 'ACADEMIC_NOTICE',
        role: undefined,
        isRead: false,
        page: 1,
        pageSize: 20,
      })
    })
    expect(screen.getAllByText(/Chưa đọc: 4/).length).toBeGreaterThanOrEqual(1)

    fireEvent.click(screen.getByRole('button', { name: 'Trang 2' }))
    await waitFor(() => {
      expect(adminService.getNotifications).toHaveBeenCalledWith(
        expect.objectContaining({ page: 2, type: 'ACADEMIC_NOTICE', isRead: false }),
      )
    })
  })

  it('dispatches USER notification with active recipient search and revokes notification via ConfirmDialog', async () => {
    vi.mocked(adminService.createNotification).mockResolvedValue({
      createdCount: 1,
      targetScope: 'USER',
      targetUserId: 'u-active',
      type: 'SYSTEM_ANNOUNCEMENT',
      title: 'Nhắc nhở',
      createdAt: '2026-11-18T10:00:00Z',
    })
    vi.mocked(adminService.deleteNotification).mockResolvedValue(undefined)

    render(
      <MemoryRouter>
        <AdminNotificationsPage />
      </MemoryRouter>,
    )

    await screen.findAllByText('Thông báo lịch thi thử')
    fireEvent.click(screen.getByRole('button', { name: /Phát thông báo mới/i }))

    fireEvent.change(screen.getByLabelText(/Phạm vi nhận/i), { target: { value: 'USER' } })
    await waitFor(() => {
      expect(screen.getByRole('option', { name: /Trần Văn B/i })).toBeInTheDocument()
    })

    fireEvent.change(screen.getByLabelText(/Chọn người nhận/i), { target: { value: 'u-active' } })
    fireEvent.change(screen.getByLabelText(/Tiêu đề thông báo/i), {
      target: { value: 'Nhắc nhở' },
    })
    fireEvent.change(screen.getByLabelText(/Nội dung thông báo/i), {
      target: { value: 'Vui lòng kiểm tra mic trước giờ thi.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Gửi thông báo' }))

    await waitFor(() => {
      expect(adminService.createNotification).toHaveBeenCalledWith({
        targetScope: 'USER',
        targetRole: undefined,
        targetUserId: 'u-active',
        type: 'SYSTEM_ANNOUNCEMENT',
        title: 'Nhắc nhở',
        message: 'Vui lòng kiểm tra mic trước giờ thi.',
      })
    })

    const revokeButtons = screen.getAllByRole('button', { name: /Thu hồi/i })
    fireEvent.click(revokeButtons[0])
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận thu hồi' }))

    await waitFor(() => {
      expect(adminService.deleteNotification).toHaveBeenCalledWith('notif-1')
    })
  })

  it('clears stale targetUserId when recipient search excludes the previously selected user', async () => {
    vi.mocked(adminService.getUsers)
      .mockResolvedValueOnce({
        items: [
          {
            id: 'u-1',
            email: 'u1@fpt.edu.vn',
            fullName: 'Nguyễn Văn Một',
            role: 'student',
            isActive: true,
            mustChangePassword: false,
            createdAt: '2026-10-01T00:00:00Z',
            updatedAt: '2026-10-01T00:00:00Z',
          },
          {
            id: 'u-2',
            email: 'u2@fpt.edu.vn',
            fullName: 'Trần Văn Hai',
            role: 'student',
            isActive: true,
            mustChangePassword: false,
            createdAt: '2026-10-01T00:00:00Z',
            updatedAt: '2026-10-01T00:00:00Z',
          },
        ],
        totalCount: 2,
        page: 1,
        pageSize: 30,
        totalPages: 1,
      })
      .mockResolvedValueOnce({
        items: [
          {
            id: 'u-2',
            email: 'u2@fpt.edu.vn',
            fullName: 'Trần Văn Hai',
            role: 'student',
            isActive: true,
            mustChangePassword: false,
            createdAt: '2026-10-01T00:00:00Z',
            updatedAt: '2026-10-01T00:00:00Z',
          },
        ],
        totalCount: 1,
        page: 1,
        pageSize: 30,
        totalPages: 1,
      })

    render(
      <MemoryRouter>
        <AdminNotificationsPage />
      </MemoryRouter>,
    )

    await screen.findAllByText('Thông báo lịch thi thử')
    fireEvent.click(screen.getByRole('button', { name: /Phát thông báo mới/i }))
    fireEvent.change(screen.getByLabelText(/Phạm vi nhận/i), { target: { value: 'USER' } })

    await waitFor(() => {
      expect(screen.getByRole('option', { name: /Nguyễn Văn Một/i })).toBeInTheDocument()
    })

    const targetSelect = screen.getByLabelText(/Chọn người nhận/i) as HTMLSelectElement
    fireEvent.change(targetSelect, { target: { value: 'u-1' } })
    expect(targetSelect.value).toBe('u-1')

    fireEvent.change(screen.getByLabelText(/Tìm tài khoản người nhận/i), {
      target: { value: 'u2' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Tìm' }))

    await waitFor(() => {
      expect(screen.queryByRole('option', { name: /Nguyễn Văn Một/i })).toBeNull()
    })
    const refreshedSelect = screen.getByLabelText(/Chọn người nhận/i) as HTMLSelectElement
    expect(refreshedSelect.value).toBe('')

    fireEvent.change(screen.getByLabelText(/Tiêu đề thông báo/i), {
      target: { value: 'Kiểm tra' },
    })
    fireEvent.change(screen.getByLabelText(/Nội dung thông báo/i), {
      target: { value: 'Nội dung kiểm tra.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Gửi thông báo' }))

    expect(await screen.findByText('Vui lòng chọn một tài khoản đang hoạt động.')).toBeInTheDocument()
    expect(adminService.createNotification).not.toHaveBeenCalled()
  })
})
