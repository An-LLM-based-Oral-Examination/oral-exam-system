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
    updateUser: vi.fn(),
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

  it('normalizes PascalCase roles for short labels, edit-role select pre-selection, and submission', async () => {
    vi.mocked(adminService.getUsers).mockResolvedValue({
      items: [
        {
          ...sampleUser,
          id: 'user-head',
          email: 'head.se@fpt.edu.vn',
          fullName: 'Trưởng Bộ Môn SE',
          studentCode: null,
          role: 'DepartmentHead',
        },
        {
          ...sampleUser,
          id: 'user-eo',
          email: 'eo@fpt.edu.vn',
          fullName: 'Cán Bộ Khảo Thí',
          studentCode: null,
          role: 'ExaminationOfficer',
        },
      ],
      totalCount: 2,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    })
    vi.mocked(adminService.updateUserRole).mockResolvedValueOnce({
      ...sampleUser,
      id: 'user-head',
      email: 'head.se@fpt.edu.vn',
      fullName: 'Trưởng Bộ Môn SE',
      studentCode: null,
      role: 'department_head',
    })

    render(
      <MemoryRouter>
        <AdminUsersPage />
      </MemoryRouter>,
    )

    await screen.findAllByText('Trưởng Bộ Môn SE')
    expect(screen.getAllByText('Trưởng BM').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Khảo thí').length).toBeGreaterThan(0)

    const roleButtons = screen.getAllByRole('button', { name: 'Đổi vai trò' })
    fireEvent.click(roleButtons[0])

    const roleSelect = screen.getByLabelText('Vai trò mới') as HTMLSelectElement
    expect(roleSelect.value).toBe('department_head')

    fireEvent.click(screen.getByRole('button', { name: 'Lưu vai trò' }))

    await waitFor(() => {
      expect(adminService.updateUserRole).toHaveBeenCalledWith('user-head', {
        role: 'department_head',
      })
    })
  })

  it('R2: supports optional username for non-student roles with validation, lowercase normalization, student role switch clear, and 422/409 mapping', async () => {
    vi.mocked(adminService.createUser)
      .mockRejectedValueOnce({
        status: 422,
        detail: 'Tên đăng nhập không hợp lệ.',
        errors: { Username: ['Tên đăng nhập chứa từ khóa cấm.'] },
      })
      .mockRejectedValueOnce({
        status: 409,
        detail: 'CONFLICT: Tên đăng nhập đã tồn tại trong hệ thống.',
      })
      .mockResolvedValueOnce({
        ...sampleUser,
        id: 'user-lec-1',
        email: 'lec01@fpt.edu.vn',
        fullName: 'Giảng Viên 01',
        studentCode: null,
        username: 'giangvien.se',
        role: 'lecturer',
      })
      .mockResolvedValueOnce({
        ...sampleUser,
        id: 'user-dh-1',
        email: 'dh01@fpt.edu.vn',
        fullName: 'Trưởng BM 01',
        studentCode: null,
        username: null,
        role: 'department_head',
      })

    render(
      <MemoryRouter>
        <AdminUsersPage />
      </MemoryRouter>,
    )

    await screen.findAllByText('Nguyễn Văn A')
    fireEvent.click(screen.getByRole('button', { name: /Tạo tài khoản mới/i }))

    // Switch role to lecturer -> username input appears
    fireEvent.change(screen.getByLabelText(/Vai trò RBAC/i), { target: { value: 'lecturer' } })
    const usernameInput = screen.getByLabelText(/Tên đăng nhập \(username/i) as HTMLInputElement
    expect(screen.queryByLabelText(/Mã sinh viên/i)).toBeNull()

    // Client validation: < 3 chars & invalid chars
    fireEvent.change(screen.getByLabelText(/Email đăng nhập/i), {
      target: { value: 'lec01@fpt.edu.vn' },
    })
    fireEvent.change(screen.getByLabelText(/Họ và tên/i), {
      target: { value: 'Giảng Viên 01' },
    })
    fireEvent.change(screen.getByLabelText(/Mật khẩu tạm thời/i), {
      target: { value: 'Temp#2026' },
    })
    fireEvent.change(usernameInput, { target: { value: 'ab' } })
    fireEvent.click(screen.getByRole('button', { name: 'Tạo tài khoản' }))
    expect(screen.getByText('Tên đăng nhập phải từ 3 đến 32 ký tự.')).toBeInTheDocument()
    expect(adminService.createUser).not.toHaveBeenCalled()

    fireEvent.change(usernameInput, { target: { value: 'gv@invalid' } })
    fireEvent.click(screen.getByRole('button', { name: 'Tạo tài khoản' }))
    expect(screen.getByText(/Tên đăng nhập chỉ được chứa chữ thường/i)).toBeInTheDocument()
    expect(adminService.createUser).not.toHaveBeenCalled()

    // Switching back to student clears username and switching back to lecturer stays cleared
    fireEvent.change(usernameInput, { target: { value: '  GiangVien.SE  ' } })
    fireEvent.change(screen.getByLabelText(/Vai trò RBAC/i), { target: { value: 'student' } })
    fireEvent.change(screen.getByLabelText(/Vai trò RBAC/i), { target: { value: 'lecturer' } })
    expect((screen.getByLabelText(/Tên đăng nhập \(username/i) as HTMLInputElement).value).toBe('')

    // Submit with normalized lowercase username -> 422 -> 409 -> success
    fireEvent.change(screen.getByLabelText(/Tên đăng nhập \(username/i), {
      target: { value: '  GiangVien.SE  ' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Tạo tài khoản' }))

    await waitFor(() => {
      expect(adminService.createUser).toHaveBeenCalledWith({
        email: 'lec01@fpt.edu.vn',
        fullName: 'Giảng Viên 01',
        password: 'Temp#2026',
        role: 'lecturer',
        username: 'giangvien.se',
      })
      expect(screen.getByText('Tên đăng nhập chứa từ khóa cấm.')).toBeInTheDocument()
    })

    fireEvent.click(screen.getByRole('button', { name: 'Tạo tài khoản' }))
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('[409]')
    })

    fireEvent.click(screen.getByRole('button', { name: 'Tạo tài khoản' }))
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).toBeNull()
    })

    // Blank username for non-student omits username key
    fireEvent.click(screen.getByRole('button', { name: /Tạo tài khoản mới/i }))
    fireEvent.change(screen.getByLabelText(/Vai trò RBAC/i), {
      target: { value: 'department_head' },
    })
    fireEvent.change(screen.getByLabelText(/Email đăng nhập/i), {
      target: { value: 'dh01@fpt.edu.vn' },
    })
    fireEvent.change(screen.getByLabelText(/Họ và tên/i), {
      target: { value: 'Trưởng BM 01' },
    })
    fireEvent.change(screen.getByLabelText(/Mật khẩu tạm thời/i), {
      target: { value: 'Temp#2026' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Tạo tài khoản' }))

    await waitFor(() => {
      expect(adminService.createUser).toHaveBeenLastCalledWith({
        email: 'dh01@fpt.edu.vn',
        fullName: 'Trưởng BM 01',
        password: 'Temp#2026',
        role: 'department_head',
      })
    })
  })

  it('R3: edits student profile keeping username read-only and shows auth_version notice on email change', async () => {
    vi.mocked(adminService.updateUser).mockResolvedValueOnce({
      ...sampleUser,
      email: 'sv01.updated@fpt.edu.vn',
      fullName: 'Nguyễn Văn A Updated',
      studentCode: 'SE180199',
    })

    render(
      <MemoryRouter>
        <AdminUsersPage />
      </MemoryRouter>,
    )

    const editBtns = await screen.findAllByRole('button', { name: 'Sửa' })
    fireEvent.click(editBtns[0])

    const dialog = screen.getByRole('dialog')
    expect(dialog).toHaveTextContent('@anvse180101')
    expect(screen.queryByLabelText(/Tên đăng nhập \(username/i)).toBeNull()

    fireEvent.change(screen.getByLabelText(/Email đăng nhập/i), {
      target: { value: '  SV01.UPDATED@fpt.edu.vn  ' },
    })
    fireEvent.change(screen.getByLabelText(/Họ và tên/i), {
      target: { value: 'Nguyễn Văn A Updated' },
    })
    fireEvent.change(screen.getByLabelText(/Mã sinh viên \(MSSV\)/i), {
      target: { value: 'se180199' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu thay đổi' }))

    await waitFor(() => {
      expect(adminService.updateUser).toHaveBeenCalledWith('user-1', {
        email: 'sv01.updated@fpt.edu.vn',
        fullName: 'Nguyễn Văn A Updated',
        studentCode: 'SE180199',
      })
      expect(screen.getByRole('status')).toHaveTextContent(
        /đã tăng auth_version và thu hồi phiên đăng nhập cũ/i,
      )
    })
  })

  it('R3: edits non-student profile allowing username clear to null and keeps dialog open on 422/409', async () => {
    vi.mocked(adminService.getUsers).mockResolvedValue({
      items: [
        {
          ...sampleUser,
          id: 'user-lec',
          email: 'lec@fpt.edu.vn',
          fullName: 'Giảng Viên Cũ',
          studentCode: null,
          username: 'lec.old',
          role: 'lecturer',
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    })
    vi.mocked(adminService.updateUser)
      .mockRejectedValueOnce({
        status: 422,
        detail: 'Dữ liệu không hợp lệ.',
        errors: { Username: ['Username không hợp lệ từ server.'] },
      })
      .mockRejectedValueOnce({
        status: 409,
        detail: 'CONFLICT: Email đã thuộc tài khoản khác.',
      })
      .mockResolvedValueOnce({
        ...sampleUser,
        id: 'user-lec',
        email: 'lec@fpt.edu.vn',
        fullName: 'Giảng Viên Mới',
        studentCode: null,
        username: null,
        role: 'lecturer',
      })

    render(
      <MemoryRouter>
        <AdminUsersPage />
      </MemoryRouter>,
    )

    const editBtns = await screen.findAllByRole('button', { name: 'Sửa' })
    fireEvent.click(editBtns[0])

    expect(screen.queryByLabelText(/Mã sinh viên/i)).toBeNull()
    fireEvent.change(screen.getByLabelText(/Họ và tên/i), {
      target: { value: 'Giảng Viên Mới' },
    })
    fireEvent.change(screen.getByLabelText(/Tên đăng nhập \(username/i), {
      target: { value: '   ' },
    })

    fireEvent.click(screen.getByRole('button', { name: 'Lưu thay đổi' }))
    await waitFor(() => {
      expect(adminService.updateUser).toHaveBeenCalledWith('user-lec', {
        email: 'lec@fpt.edu.vn',
        fullName: 'Giảng Viên Mới',
        username: null,
        studentCode: null,
      })
      expect(screen.getByText('Username không hợp lệ từ server.')).toBeInTheDocument()
    })
    expect(screen.getByRole('dialog')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Lưu thay đổi' }))
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('[409]')
    })
    expect(screen.getByRole('dialog')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Lưu thay đổi' }))
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).toBeNull()
    })
  })

  it('R6: validates and normalizes ?role= URL query param against allowed ADMIN_ROLE_OPTIONS', async () => {
    const { unmount } = render(
      <MemoryRouter initialEntries={['/admin/users?role=bogus']}>
        <AdminUsersPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(adminService.getUsers).toHaveBeenCalledWith(
        expect.objectContaining({ role: undefined }),
      )
    })
    expect(screen.queryByRole('button', { name: 'Xóa bộ lọc' })).toBeNull()
    unmount()

    vi.mocked(adminService.getUsers).mockClear()
    render(
      <MemoryRouter initialEntries={['/admin/users?role=DepartmentHead']}>
        <AdminUsersPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(adminService.getUsers).toHaveBeenCalledWith(
        expect.objectContaining({ role: 'department_head' }),
      )
    })
  })

  it('BUG-1: requires and submits studentCode when changing a non-student role to student', async () => {
    vi.mocked(adminService.getUsers).mockResolvedValue({
      items: [
        {
          ...sampleUser,
          id: 'user-lec-promote',
          email: 'lec.promote@fpt.edu.vn',
          fullName: 'Giảng Viên Chuyển Role',
          studentCode: null,
          username: 'lec_promote',
          role: 'lecturer',
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    })
    vi.mocked(adminService.updateUserRole).mockResolvedValueOnce({
      ...sampleUser,
      id: 'user-lec-promote',
      role: 'student',
      studentCode: 'SE180999',
    })

    render(
      <MemoryRouter>
        <AdminUsersPage />
      </MemoryRouter>,
    )

    const roleButtons = await screen.findAllByRole('button', { name: 'Đổi vai trò' })
    fireEvent.click(roleButtons[0])

    expect(screen.queryByLabelText(/Mã sinh viên \(MSSV\)/i)).toBeNull()
    fireEvent.change(screen.getByLabelText('Vai trò mới'), { target: { value: 'student' } })

    const studentCodeInput = screen.getByLabelText(/Mã sinh viên \(MSSV\)/i)
    fireEvent.click(screen.getByRole('button', { name: 'Lưu vai trò' }))
    expect(
      await screen.findByText(/Mã sinh viên là bắt buộc khi gán vai trò sinh viên\./i),
    ).toBeInTheDocument()
    expect(adminService.updateUserRole).not.toHaveBeenCalled()

    fireEvent.change(studentCodeInput, { target: { value: 'se180999' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu vai trò' }))

    await waitFor(() =>
      expect(adminService.updateUserRole).toHaveBeenCalledWith('user-lec-promote', {
        role: 'student',
        studentCode: 'SE180999',
      }),
    )
  })
})
