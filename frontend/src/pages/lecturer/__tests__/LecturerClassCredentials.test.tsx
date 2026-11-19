import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { LecturerClassDetailPage } from '../LecturerClassDetailPage'
import { LecturerClassManagementPage } from '../LecturerClassManagementPage'

const mocks = vi.hoisted(() => ({
  listMyOfferings: vi.fn(),
  listSemesters: vi.fn(),
  listLecturerClasses: vi.fn(),
  createClass: vi.fn(),
  getClassDetail: vi.fn(),
  resetPassword: vi.fn(),
}))

vi.mock('@/services/class.service', () => ({
  classService: {
    listMyOfferings: mocks.listMyOfferings,
    listSemesters: mocks.listSemesters,
    listLecturerClasses: mocks.listLecturerClasses,
    createClass: mocks.createClass,
    getClassDetail: mocks.getClassDetail,
    resetPassword: mocks.resetPassword,
  },
}))

vi.mock('@/pages/lecturer/InClassShiftHubPage', () => ({
  InClassShiftHubPage: ({ classId }: { classId: string }) => (
    <div data-testid="embedded-shifts">Shifts for {classId}</div>
  ),
}))

function installMatchMedia() {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    configurable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: true,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  })
}

const baseDetail = {
  classId: 'class-1',
  code: 'SE1802-CSD',
  name: 'Lớp SE1802',
  joinCode: 'JOIN8888',
  joinPassword: null,
  courseCode: 'CSD201',
  courseName: 'Data Structures',
  semesterCode: 'FA26',
  lecturerName: 'Giảng viên A',
  enrolledCount: 12,
  maxStudents: 35,
  hasPassword: true,
  isJoinEnabled: true,
  allowSelfEnrollment: true,
  isActive: true,
  createdAt: '2026-11-18T00:00:00Z',
  students: [],
  shifts: [],
}

describe('Lecturer class credentials management & password reset', () => {
  beforeEach(() => {
    installMatchMedia()
    vi.clearAllMocks()

    mocks.listMyOfferings.mockResolvedValue([
      {
        courseOfferingId: 'offering-1',
        courseId: 'course-1',
        courseCode: 'CSD201',
        courseName: 'Data Structures',
        semesterId: 'sem-1',
        semesterCode: 'FA26',
      },
    ])
    mocks.listSemesters.mockResolvedValue([
      {
        id: 'sem-1',
        code: 'FA26',
        name: 'Fall 2026',
        startDate: '2026-09-01',
        endDate: '2026-12-31',
        isActive: true,
      },
    ])
    mocks.listLecturerClasses.mockResolvedValue([])
    mocks.getClassDetail.mockResolvedValue(baseDetail)
  })

  it('creates a class, opens credentials dialog with joinPassword only (hides joinCode), and copies safely', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    })

    mocks.createClass.mockResolvedValue({
      id: 'class-new',
      code: 'SE1802-CSD',
      name: 'Lớp SE1802',
      joinCode: 'JOIN8888',
      courseId: 'course-1',
      courseCode: 'CSD201',
      courseName: 'Data Structures',
      semesterId: 'sem-1',
      semesterCode: 'FA26',
      lecturerId: 'lec-1',
      lecturerName: 'Giảng viên A',
      maxStudents: 35,
      hasPassword: true,
      isJoinEnabled: true,
      allowSelfEnrollment: true,
      createdAt: '2026-11-18T00:00:00Z',
      courseOfferingId: 'offering-1',
      joinPassword: 'ClassPass#2026',
    })

    render(
      <MemoryRouter>
        <LecturerClassManagementPage />
      </MemoryRouter>,
    )

    const createBtn = await screen.findAllByRole('button', { name: 'Tạo lớp' })
    fireEvent.click(createBtn[0])

    fireEvent.change(screen.getByPlaceholderText('SE1802-CSD'), { target: { value: 'SE1802-CSD' } })
    fireEvent.change(screen.getByPlaceholderText('Lớp SE1802-CSD'), { target: { value: 'Lớp SE1802' } })
    fireEvent.change(screen.getByPlaceholderText('Ít nhất 8 ký tự'), { target: { value: 'ClassPass#2026' } })

    fireEvent.submit(document.getElementById('create-class-form')!)

    expect(await screen.findByText('Thông tin tham gia lớp học')).toBeTruthy()
    expect(screen.queryByText('JOIN8888')).toBeNull()
    expect(screen.queryByText(/Mã tham gia/i)).toBeNull()
    expect(screen.getByText('ClassPass#2026')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Sao chép mật khẩu' }))
    await waitFor(() => expect(writeText).toHaveBeenCalledWith('ClassPass#2026'))
  })

  it('handles missing navigator.clipboard without unhandled rejection', async () => {
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: undefined,
    })

    mocks.createClass.mockResolvedValue({
      id: 'class-new',
      code: 'SE1802-CSD',
      name: 'Lớp SE1802',
      joinCode: 'JOIN7777',
      courseId: 'course-1',
      courseCode: 'CSD201',
      courseName: 'Data Structures',
      semesterId: 'sem-1',
      semesterCode: 'FA26',
      lecturerId: null,
      lecturerName: null,
      maxStudents: 35,
      hasPassword: true,
      isJoinEnabled: true,
      allowSelfEnrollment: true,
      createdAt: '2026-11-18T00:00:00Z',
      courseOfferingId: null,
      joinPassword: 'ClassPass#2026',
    })

    render(
      <MemoryRouter>
        <LecturerClassManagementPage />
      </MemoryRouter>,
    )

    const createBtn = await screen.findAllByRole('button', { name: 'Tạo lớp' })
    fireEvent.click(createBtn[0])
    fireEvent.change(screen.getByPlaceholderText('SE1802-CSD'), { target: { value: 'SE1802-CSD' } })
    fireEvent.change(screen.getByPlaceholderText('Lớp SE1802-CSD'), { target: { value: 'Lớp SE1802' } })
    fireEvent.change(screen.getByPlaceholderText('Ít nhất 8 ký tự'), { target: { value: 'ClassPass#2026' } })
    fireEvent.submit(document.getElementById('create-class-form')!)

    expect(await screen.findByText('ClassPass#2026')).toBeTruthy()
    expect(screen.queryByText('JOIN7777')).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Sao chép mật khẩu' }))
    expect(screen.getByText('ClassPass#2026')).toBeTruthy()
  })

  it('hides joinCode on detail page, validates reset modal password, preserves session password across reload, and clears stale state on reopen', async () => {
    mocks.getClassDetail
      .mockResolvedValueOnce(baseDetail)
      .mockResolvedValueOnce({ ...baseDetail, joinCode: 'JOIN9999', isJoinEnabled: true, joinPassword: null })

    mocks.resetPassword.mockResolvedValue({
      classId: 'class-1',
      code: 'SE1802-CSD',
      name: 'Lớp SE1802',
      joinCode: 'JOIN9999',
      hasPassword: true,
      isJoinEnabled: true,
      updatedAt: '2026-11-18T05:00:00Z',
      joinPassword: 'FreshPassword#9',
    })

    render(
      <MemoryRouter initialEntries={['/lecturer/classes/class-1']}>
        <Routes>
          <Route path="/lecturer/classes/:classId" element={<LecturerClassDetailPage />} />
        </Routes>
      </MemoryRouter>,
    )

    expect(await screen.findByText('Lớp SE1802')).toBeTruthy()
    expect(screen.queryByText('JOIN8888')).toBeNull()
    expect(screen.queryByText(/Mã tham gia/i)).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: 'Đặt lại mật khẩu lớp' }))
    const passwordInput = await screen.findByPlaceholderText('Ít nhất 8 ký tự')
    expect(screen.queryByText(/Mã tham gia mới/i)).toBeNull()

    // Validation blocks < 8 char password
    fireEvent.change(passwordInput, { target: { value: 'short' } })
    fireEvent.submit(document.getElementById('reset-class-password-form')!)

    expect(await screen.findByText('Mật khẩu mới cần ít nhất 8 ký tự.')).toBeTruthy()
    expect(mocks.resetPassword).not.toHaveBeenCalled()

    // Close and reopen: stale values and errors must be cleared
    fireEvent.click(screen.getByRole('button', { name: 'Hủy' }))
    fireEvent.click(screen.getByRole('button', { name: 'Đặt lại mật khẩu lớp' }))
    const freshPasswordInput = (await screen.findByPlaceholderText('Ít nhất 8 ký tự')) as HTMLInputElement
    expect(freshPasswordInput.value).toBe('')
    expect(screen.queryByText('Mật khẩu mới cần ít nhất 8 ký tự.')).toBeNull()

    // Valid submit
    fireEvent.change(freshPasswordInput, { target: { value: 'FreshPassword#9' } })
    fireEvent.submit(document.getElementById('reset-class-password-form')!)

    await waitFor(() =>
      expect(mocks.resetPassword).toHaveBeenCalledWith('class-1', {
        newPassword: 'FreshPassword#9',
        isJoinEnabled: true,
      }),
    )

    // Credentials dialog opens showing password only (no joinCode) AND reload happened without wiping session password
    expect(await screen.findByText('Đã cập nhật mật khẩu lớp')).toBeTruthy()
    expect(screen.getByText('FreshPassword#9')).toBeTruthy()
    expect(screen.queryByText('JOIN9999')).toBeNull()
    expect(mocks.getClassDetail).toHaveBeenCalledTimes(2)

    fireEvent.click(screen.getByRole('button', { name: 'Đã lưu thông tin' }))
    // Header reveal button should now allow revealing FreshPassword#9 even though getClassDetail returned null
    fireEvent.click(screen.getByRole('button', { name: 'Hiện mật khẩu tham gia' }))
    expect(screen.getByText('FreshPassword#9')).toBeTruthy()
  })

  it('keeps reset dialog open and strips VALIDATION prefix on 422/409 errors', async () => {
    mocks.resetPassword.mockRejectedValue({
      status: 422,
      detail: 'VALIDATION: Lớp của course offering phải duy trì mật khẩu tham gia cố định.',
    })

    render(
      <MemoryRouter initialEntries={['/lecturer/classes/class-1']}>
        <Routes>
          <Route path="/lecturer/classes/:classId" element={<LecturerClassDetailPage />} />
        </Routes>
      </MemoryRouter>,
    )

    await screen.findByText('Lớp SE1802')
    fireEvent.click(screen.getByRole('button', { name: 'Đặt lại mật khẩu lớp' }))
    fireEvent.change(await screen.findByPlaceholderText('Ít nhất 8 ký tự'), {
      target: { value: 'ValidPass123' },
    })
    fireEvent.submit(document.getElementById('reset-class-password-form')!)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Lớp của course offering phải duy trì mật khẩu tham gia cố định.')
    expect(alert).not.toHaveTextContent('VALIDATION:')
    expect(document.getElementById('reset-class-password-form')).toBeTruthy()
  })

  it('preserves detail view and sessionJoinPassword reveal if reload fails transiently after resetPassword', async () => {
    mocks.getClassDetail
      .mockResolvedValueOnce(baseDetail)
      .mockRejectedValueOnce({ status: 500, detail: 'Transient server error' })

    mocks.resetPassword.mockResolvedValueOnce({
      classId: 'class-1',
      code: 'SE1802-CSD',
      name: 'Lớp SE1802',
      joinCode: 'JOIN9999',
      hasPassword: true,
      isJoinEnabled: false,
      updatedAt: '2026-11-18T05:00:00Z',
      joinPassword: 'FreshPassword#9',
    })

    render(
      <MemoryRouter initialEntries={['/lecturer/classes/class-1']}>
        <Routes>
          <Route path="/lecturer/classes/:classId" element={<LecturerClassDetailPage />} />
        </Routes>
      </MemoryRouter>,
    )

    expect(await screen.findByText('Lớp SE1802')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Đặt lại mật khẩu lớp' }))
    fireEvent.change(await screen.findByPlaceholderText('Ít nhất 8 ký tự'), {
      target: { value: 'FreshPassword#9' },
    })
    fireEvent.submit(document.getElementById('reset-class-password-form')!)

    expect(await screen.findByText('Đã cập nhật mật khẩu lớp')).toBeTruthy()
    expect(await screen.findByRole('alert')).toHaveTextContent('Transient server error')

    fireEvent.click(screen.getByRole('button', { name: 'Đã lưu thông tin' }))

    expect(screen.getByText('Lớp SE1802')).toBeTruthy()
    expect(screen.queryByText('JOIN9999')).toBeNull()
    expect(screen.getByText('Đã khóa')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Hiện mật khẩu tham gia' }))
    expect(screen.getByText('FreshPassword#9')).toBeTruthy()
  })

  it('removes temporary textarea from document.body when navigator.clipboard is missing and execCommand throws', async () => {
    const originalExecCommand = Object.getOwnPropertyDescriptor(document, 'execCommand')
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: undefined,
    })
    Object.defineProperty(document, 'execCommand', {
      configurable: true,
      writable: true,
      value: vi.fn(() => {
        throw new Error('Copy blocked')
      }),
    })

    try {
      mocks.getClassDetail.mockResolvedValueOnce({
        ...baseDetail,
        joinPassword: 'CopyFallback#2026',
      })
      mocks.createClass.mockResolvedValueOnce({
        id: 'class-new',
        code: 'SE1802-CSD',
        name: 'Lớp SE1802',
        joinCode: 'JOIN7777',
        courseId: 'course-1',
        courseCode: 'CSD201',
        courseName: 'Data Structures',
        semesterId: 'sem-1',
        semesterCode: 'FA26',
        lecturerId: null,
        lecturerName: null,
        maxStudents: 35,
        hasPassword: true,
        isJoinEnabled: true,
        allowSelfEnrollment: true,
        createdAt: '2026-11-18T00:00:00Z',
        courseOfferingId: null,
        joinPassword: 'ClassPass#2026',
      })

      render(
        <MemoryRouter initialEntries={['/lecturer/classes/class-1']}>
          <Routes>
            <Route path="/lecturer/classes/:classId" element={<LecturerClassDetailPage />} />
          </Routes>
        </MemoryRouter>,
      )

      expect(await screen.findByText('Lớp SE1802')).toBeTruthy()
      fireEvent.click(screen.getByRole('button', { name: 'Sao chép mật khẩu tham gia' }))
      await waitFor(() => expect(document.body.querySelectorAll('textarea').length).toBe(0))

      const managementView = render(
        <MemoryRouter>
          <LecturerClassManagementPage />
        </MemoryRouter>,
      )
      const createBtn = await managementView.findAllByRole('button', { name: 'Tạo lớp' })
      fireEvent.click(createBtn[0])
      fireEvent.change(managementView.getByPlaceholderText('SE1802-CSD'), { target: { value: 'SE1802-CSD' } })
      fireEvent.change(managementView.getByPlaceholderText('Lớp SE1802-CSD'), { target: { value: 'Lớp SE1802' } })
      fireEvent.change(managementView.getByPlaceholderText('Ít nhất 8 ký tự'), { target: { value: 'ClassPass#2026' } })
      fireEvent.submit(document.getElementById('create-class-form')!)

      expect(await managementView.findByText('ClassPass#2026')).toBeTruthy()
      fireEvent.click(managementView.getByRole('button', { name: 'Sao chép mật khẩu' }))
      await waitFor(() => expect(document.body.querySelectorAll('textarea').length).toBe(0))
    } finally {
      if (originalExecCommand) {
        Object.defineProperty(document, 'execCommand', originalExecCommand)
      } else {
        delete (document as { execCommand?: unknown }).execCommand
      }
    }
  })

  it('reveals, hides, and copies persisted joinPassword immediately on detail page without resetting', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    })

    mocks.getClassDetail.mockResolvedValueOnce({
      ...baseDetail,
      joinPassword: 'PersistedPass#2026',
    })

    render(
      <MemoryRouter initialEntries={['/lecturer/classes/class-1']}>
        <Routes>
          <Route path="/lecturer/classes/:classId" element={<LecturerClassDetailPage />} />
        </Routes>
      </MemoryRouter>,
    )

    expect(await screen.findByText('Lớp SE1802')).toBeTruthy()
    expect(screen.queryByText('PersistedPass#2026')).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: 'Hiện mật khẩu tham gia' }))
    expect(screen.getByText('PersistedPass#2026')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Sao chép mật khẩu tham gia' }))
    await waitFor(() => expect(writeText).toHaveBeenCalledWith('PersistedPass#2026'))

    fireEvent.click(screen.getByRole('button', { name: 'Ẩn mật khẩu tham gia' }))
    expect(screen.queryByText('PersistedPass#2026')).toBeNull()
  })

  it('keeps eye icon visible for legacy hash-only classes and supports eye toggles in create/reset password inputs', async () => {
    mocks.getClassDetail.mockResolvedValueOnce({
      ...baseDetail,
      hasPassword: true,
      joinPassword: null,
    })

    const detailView = render(
      <MemoryRouter initialEntries={['/lecturer/classes/class-1']}>
        <Routes>
          <Route path="/lecturer/classes/:classId" element={<LecturerClassDetailPage />} />
        </Routes>
      </MemoryRouter>,
    )

    expect(await detailView.findByText('Lớp SE1802')).toBeTruthy()
    const legacyEye = detailView.getByRole('button', { name: 'Hiện mật khẩu tham gia' })
    fireEvent.click(legacyEye)
    expect(detailView.getByText('••••••••')).toBeTruthy()

    // Reset password modal eye toggle
    fireEvent.click(detailView.getByRole('button', { name: 'Đặt lại mật khẩu lớp' }))
    const resetInput = (await detailView.findByPlaceholderText('Ít nhất 8 ký tự')) as HTMLInputElement
    expect(resetInput.type).toBe('password')
    fireEvent.click(detailView.getByRole('button', { name: 'Hiện mật khẩu' }))
    expect(resetInput.type).toBe('text')
    fireEvent.click(detailView.getByRole('button', { name: 'Ẩn mật khẩu' }))
    expect(resetInput.type).toBe('password')
    detailView.unmount()

    // Create class modal eye toggle
    const managementView = render(
      <MemoryRouter>
        <LecturerClassManagementPage />
      </MemoryRouter>,
    )
    const createBtn = await managementView.findAllByRole('button', { name: 'Tạo lớp' })
    fireEvent.click(createBtn[0])
    const createInput = (await managementView.findByPlaceholderText('Ít nhất 8 ký tự')) as HTMLInputElement
    expect(createInput.type).toBe('password')
    fireEvent.click(managementView.getByRole('button', { name: 'Hiện mật khẩu' }))
    expect(createInput.type).toBe('text')
    fireEvent.click(managementView.getByRole('button', { name: 'Ẩn mật khẩu' }))
    expect(createInput.type).toBe('password')
  })
})
