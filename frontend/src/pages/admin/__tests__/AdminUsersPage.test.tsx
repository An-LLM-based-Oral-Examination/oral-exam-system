import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AdminUsersPage } from '../AdminUsersPage'
import { adminService } from '@/services/admin.service'

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({
    user: { id: 'admin-self', role: 'Admin', email: 'admin@fpt.edu.vn', fullName: 'Admin Self' },
  }),
}))

vi.mock('@/services/admin.service', () => ({
  DEFAULT_ADMIN_PAGE_SIZE: 20,
  adminService: {
    getUsers: vi.fn(),
    createUser: vi.fn(),
    updateUserRole: vi.fn(),
    updateUserStatus: vi.fn(),
    resetUserPassword: vi.fn(),
  },
}))

const sampleUser = {
  id: 'user-1',
  email: 'sv01@fpt.edu.vn',
  fullName: 'Nguyễn Văn A',
  studentCode: 'SE180101',
  username: 'anvse180101',
  role: 'student',
  isActive: true,
  mustChangePassword: true,
  createdAt: '2026-10-01T00:00:00Z',
  updatedAt: '2026-10-01T00:00:00Z',
}

describe('AdminUsersPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(adminService.getUsers).mockResolvedValue({
      items: [sampleUser],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    })
  })

  it('restores filters and page from URL search params on mount', async () => {
    render(
      <MemoryRouter initialEntries={['/admin/users?q=sv01&role=student&status=active&page=2']}>
        <AdminUsersPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(adminService.getUsers).toHaveBeenCalledWith({
        search: 'sv01',
        role: 'student',
        isActive: true,
        page: 2,
        pageSize: 20,
      })
    })
    expect(screen.getByPlaceholderText(/Tìm email/i)).toHaveValue('sv01')
  })

  it('creates user without dead department field and maps RFC 7807 field errors', async () => {
    vi.mocked(adminService.createUser).mockRejectedValueOnce({
      status: 400,
      detail: 'Dữ liệu tạo tài khoản không hợp lệ.',
      errors: {
        Email: ['Email đã được sử dụng.'],
      },
    })

    render(
      <MemoryRouter>
        <AdminUsersPage />
      </MemoryRouter>,
    )

    await screen.findAllByText('Nguyễn Văn A')
    fireEvent.click(screen.getByRole('button', { name: /Tạo tài khoản mới/i }))

    expect(screen.queryByLabelText(/Phòng ban|Department/i)).toBeNull()
    expect(screen.getByText(/mustChangePassword = true/i)).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText(/Email đăng nhập/i), {
      target: { value: 'new@fpt.edu.vn' },
    })
    fireEvent.change(screen.getByLabelText(/Họ và tên/i), {
      target: { value: 'Sinh Viên Mới' },
    })
    fireEvent.change(screen.getByLabelText(/Mật khẩu tạm thời/i), {
      target: { value: 'Temp#2026' },
    })
    fireEvent.change(screen.getByLabelText(/Mã sinh viên/i), {
      target: { value: 'se180202' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Tạo tài khoản' }))

    await waitFor(() => {
      expect(adminService.createUser).toHaveBeenCalledWith({
        email: 'new@fpt.edu.vn',
        fullName: 'Sinh Viên Mới',
        password: 'Temp#2026',
        role: 'student',
        studentCode: 'SE180202',
      })
    })
    expect(await screen.findByText('Email đã được sử dụng.')).toBeInTheDocument()

    fireEvent.keyDown(window, { key: 'Escape' })
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).toBeNull()
    })
  })

  it('handles 409 self-demotion in Edit Role FormDialog and 409 self-lock in ConfirmDialog cleanly', async () => {
    vi.mocked(adminService.getUsers).mockResolvedValue({
      items: [{ ...sampleUser, id: 'admin-self', email: 'admin@fpt.edu.vn', role: 'admin', studentCode: null }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    })
    vi.mocked(adminService.updateUserRole).mockRejectedValueOnce({
      status: 409,
      detail: 'CONFLICT: Không thể tự hạ quyền Admin của chính mình.',
    })
    vi.mocked(adminService.updateUserStatus).mockRejectedValueOnce({
      status: 409,
      detail: 'CONFLICT: Không thể tự khóa tài khoản Admin đang đăng nhập.',
    })

    render(
      <MemoryRouter>
        <AdminUsersPage />
      </MemoryRouter>,
    )

    const roleButtons = await screen.findAllByRole('button', { name: 'Đổi vai trò' })
    fireEvent.click(roleButtons[0])
    fireEvent.click(screen.getByRole('button', { name: 'Lưu vai trò' }))

    const roleAlert = await screen.findByRole('alert')
    expect(roleAlert).toHaveTextContent('[409]')
    expect(roleAlert).toHaveTextContent('Không thể tự hạ quyền Admin của chính mình.')

    fireEvent.keyDown(window, { key: 'Escape' })
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).toBeNull()
    })

    const lockButtons = screen.getAllByRole('button', { name: /^Khóa/ })
    fireEvent.click(lockButtons[0])

    const confirmDialog = await screen.findByRole('alertdialog')
    expect(confirmDialog).toHaveTextContent(
      'Lưu ý: Tài khoản Admin không thể tự khóa chính mình (HTTP 409).',
    )

    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận khóa tài khoản' }))

    await waitFor(() => {
      expect(adminService.updateUserStatus).toHaveBeenCalledWith('admin-self', {
        isActive: false,
        reason: undefined,
      })
      const lockAlert = screen.getByRole('alert')
      expect(lockAlert).toHaveTextContent('[409]')
      expect(lockAlert).toHaveTextContent('Không thể tự khóa tài khoản Admin đang đăng nhập.')
    })
    expect(screen.getByRole('alertdialog')).toBeInTheDocument()
  })

  it('renders 403 forbidden banner and 429 amber rate-limit banner with retry recovery', async () => {
    vi.mocked(adminService.getUsers)
      .mockRejectedValueOnce({
        status: 403,
        detail: 'Bạn không có quyền truy cập danh sách người dùng.',
      })
      .mockRejectedValueOnce({
        status: 429,
        detail: 'Quá giới hạn yêu cầu.',
      })
      .mockResolvedValueOnce({
        items: [sampleUser],
        totalCount: 1,
        page: 1,
        pageSize: 20,
        totalPages: 1,
      })

    render(
      <MemoryRouter>
        <AdminUsersPage />
      </MemoryRouter>,
    )

    const forbiddenAlert = await screen.findByRole('alert')
    expect(forbiddenAlert).toHaveTextContent('[403]')
    expect(forbiddenAlert).toHaveTextContent('Bạn không có quyền truy cập danh sách người dùng.')
    expect(screen.queryByText(/Chưa có tài khoản nào trong hệ thống/i)).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }))

    await waitFor(() => {
      const rateLimitAlert = screen.getByRole('alert')
      expect(rateLimitAlert).toHaveTextContent('[429]')
      expect(rateLimitAlert).toHaveTextContent('Quá giới hạn yêu cầu.')
      expect(rateLimitAlert.className).toContain('bg-[#fff8eb]')
    })

    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }))

    expect((await screen.findAllByText('Nguyễn Văn A')).length).toBeGreaterThan(0)
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('renders empty state and 401 re-login state when unauthorized', async () => {
    vi.mocked(adminService.getUsers)
      .mockResolvedValueOnce({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 })
      .mockRejectedValueOnce({ status: 401, detail: 'Phiên đăng nhập hết hạn.' })

    render(
      <MemoryRouter>
        <AdminUsersPage />
      </MemoryRouter>,
    )

    await screen.findByText(/Chưa có tài khoản nào trong hệ thống/i)
    fireEvent.click(screen.getByRole('button', { name: 'Làm mới' }))

    await waitFor(() => {
      expect(screen.getByRole('link', { name: 'Đăng nhập lại' })).toHaveAttribute('href', '/login')
    })
    expect(screen.queryByText(/Chưa có tài khoản nào trong hệ thống/i)).toBeNull()
  })

  it('validates and submits resetUserPassword with inline FieldError and 400/422 mapping', async () => {
    vi.mocked(adminService.resetUserPassword)
      .mockRejectedValueOnce({
        status: 422,
        detail: 'Mật khẩu tạm thời không đạt chính sách bảo mật.',
        errors: {
          TemporaryPassword: ['Mật khẩu phải chứa chữ hoa và số.'],
        },
      })
      .mockResolvedValueOnce({
        ...sampleUser,
        mustChangePassword: true,
      })

    render(
      <MemoryRouter>
        <AdminUsersPage />
      </MemoryRouter>,
    )

    const resetBtns = await screen.findAllByRole('button', { name: 'Cấp lại MK' })
    fireEvent.click(resetBtns[0])

    // Client validation (< 8 chars)
    fireEvent.change(screen.getByLabelText(/Mật khẩu tạm thời mới/i), {
      target: { value: 'short' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận cấp lại' }))
    expect(screen.getByText(/Mật khẩu tạm thời phải từ 8 đến 128 ký tự/i)).toBeInTheDocument()
    expect(adminService.resetUserPassword).not.toHaveBeenCalled()

    // Server 422 field error mapping
    fireEvent.change(screen.getByLabelText(/Mật khẩu tạm thời mới/i), {
      target: { value: 'password123' },
    })
    fireEvent.change(screen.getByLabelText(/Lý do cấp lại/i), {
      target: { value: 'Quên mật khẩu' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận cấp lại' }))

    await waitFor(() => {
      expect(screen.getByText('Mật khẩu phải chứa chữ hoa và số.')).toBeInTheDocument()
    })

    // Valid submit
    fireEvent.change(screen.getByLabelText(/Mật khẩu tạm thời mới/i), {
      target: { value: 'TempPass#2026' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận cấp lại' }))

    await waitFor(() => {
      expect(adminService.resetUserPassword).toHaveBeenLastCalledWith('user-1', {
        temporaryPassword: 'TempPass#2026',
        reason: 'Quên mật khẩu',
      })
      expect(screen.getByRole('status')).toHaveTextContent(/mustChangePassword = true/i)
    })
  })

  it('toggles password visibility with eye icons in create-user and reset-password dialogs and resets state on reopen', async () => {
    render(
      <MemoryRouter>
        <AdminUsersPage />
      </MemoryRouter>,
    )

    await screen.findAllByText('Nguyễn Văn A')

    // Create user modal eye toggle
    fireEvent.click(screen.getByRole('button', { name: /Tạo tài khoản mới/i }))
    const createPwd = screen.getByLabelText(/Mật khẩu tạm thời \(tối thiểu 8 ký tự\)/i) as HTMLInputElement
    expect(createPwd.type).toBe('password')
    fireEvent.click(screen.getByRole('button', { name: 'Hiện mật khẩu' }))
    expect(createPwd.type).toBe('text')
    fireEvent.keyDown(window, { key: 'Escape' })
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())

    // Reopen create modal -> must reset back to password
    fireEvent.click(screen.getByRole('button', { name: /Tạo tài khoản mới/i }))
    expect((screen.getByLabelText(/Mật khẩu tạm thời \(tối thiểu 8 ký tự\)/i) as HTMLInputElement).type).toBe('password')
    fireEvent.keyDown(window, { key: 'Escape' })
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())

    // Reset password modal eye toggle
    const resetBtns = screen.getAllByRole('button', { name: 'Cấp lại MK' })
    fireEvent.click(resetBtns[0])
    const resetPwd = screen.getByLabelText(/Mật khẩu tạm thời mới \(8–128 ký tự\)/i) as HTMLInputElement
    expect(resetPwd.type).toBe('password')
    fireEvent.click(screen.getByRole('button', { name: 'Hiện mật khẩu' }))
    expect(resetPwd.type).toBe('text')
    fireEvent.keyDown(window, { key: 'Escape' })
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())

    // Reopen reset modal -> must reset back to password
    fireEvent.click(screen.getAllByRole('button', { name: 'Cấp lại MK' })[0])
    expect((screen.getByLabelText(/Mật khẩu tạm thời mới \(8–128 ký tự\)/i) as HTMLInputElement).type).toBe('password')
  })
})
