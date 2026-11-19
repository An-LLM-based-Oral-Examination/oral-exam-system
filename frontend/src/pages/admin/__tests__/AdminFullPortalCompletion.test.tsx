import { MemoryRouter } from 'react-router-dom'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AdminCoursesPage } from '../AdminCoursesPage'
import { AdminNotificationsPage } from '../AdminNotificationsPage'
import { AdminSemestersPage } from '../AdminSemestersPage'
import { AdminUsersPage } from '../AdminUsersPage'
import { DeadLetterQueuePage } from '@/pages/lecturer/DeadLetterQueuePage'
import { adminService } from '@/services/admin.service'
import { courseCloService } from '@/services/courseClo.service'
import { deadLetterQueueService } from '@/services/deadLetterQueue.service'

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({
    user: { id: 'admin-1', role: 'Admin', email: 'admin@fpt.edu.vn', fullName: 'Admin' },
  }),
}))

vi.mock('@/services/deadLetterQueue.service', () => ({
  deadLetterQueueService: {
    list: vi.fn(),
    replay: vi.fn(),
    resolve: vi.fn(),
    listForAdmin: vi.fn(),
    replayForAdmin: vi.fn(),
    resolveForAdmin: vi.fn(),
  },
}))

vi.mock('@/services/admin.service', () => ({
  DEFAULT_ADMIN_PAGE_SIZE: 20,
  adminService: {
    getUsers: vi.fn(),
    createUser: vi.fn(),
    updateUserStatus: vi.fn(),
    updateUserRole: vi.fn(),
    resetUserPassword: vi.fn(),
    getNotifications: vi.fn(),
    createNotification: vi.fn(),
    deleteNotification: vi.fn(),
    getSemesters: vi.fn(),
    createSemester: vi.fn(),
    updateSemester: vi.fn(),
    getSemesterCourseOfferings: vi.fn(),
    syncSemesterCourseOfferings: vi.fn(),
    getCourses: vi.fn(),
    createCourse: vi.fn(),
    updateCourse: vi.fn(),
    assignDepartmentHead: vi.fn(),
    getDepartmentHeads: vi.fn(),
    getAcademicPrograms: vi.fn(),
    assignProgramDepartmentHead: vi.fn(),
  },
}))

vi.mock('@/services/courseClo.service', () => ({
  courseCloService: {
    list: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
  },
}))

describe('Admin Modular Portal Completion (Users, Offerings, CLOs 409, Notifications 3NF, DLQ)', () => {
  beforeEach(() => {
    vi.clearAllMocks()

    vi.mocked(adminService.getUsers).mockResolvedValue({
      items: [
        {
          id: 'user-1',
          email: 'sv01@fpt.edu.vn',
          fullName: 'Nguyễn Sinh Viên',
          studentCode: 'SE180101',
          username: 'viennsse180101',
          role: 'student',
          isActive: true,
          mustChangePassword: false,
          createdAt: '2026-10-01T00:00:00Z',
          updatedAt: '2026-10-01T00:00:00Z',
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    })

    vi.mocked(adminService.getNotifications).mockResolvedValue({
      items: [
        {
          id: 'notif-1',
          userId: 'user-1',
          userEmail: 'sv01@fpt.edu.vn',
          userFullName: 'Nguyễn Sinh Viên',
          userRole: 'student',
          title: 'Thông báo lịch thi thử',
          message: 'Mở ca thi thử học kỳ FA26.',
          type: 'ACADEMIC_NOTICE',
          isRead: false,
          createdAt: '2026-11-16T07:00:00Z',
        },
      ],
      totalCount: 1,
      unreadTotalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    })

    vi.mocked(adminService.getSemesters).mockResolvedValue([
      {
        id: 'sem-1',
        code: 'FA26',
        name: 'Fall 2026',
        startDate: '2026-09-01',
        endDate: '2026-12-31',
        isActive: true,
        coursesCount: 2,
        classesCount: 4,
        createdAt: '2026-08-01T00:00:00Z',
      },
    ])

    vi.mocked(adminService.getCourses).mockResolvedValue([
      {
        id: 'course-1',
        code: 'PRN231',
        name: 'Building Cross-Platform Apps',
        credits: 3,
        academicProgramId: 'prog-1',
        semesterId: 'sem-1',
        semesterCode: 'FA26',
        maxMockExamsPerDay: 4,
        mockExamDurationMinutes: 30,
        mockExamTotalQuestions: 5,
        hasFollowUp: false,
        examInputMode: 'VoiceWithTranscriptEdit',
        isActive: true,
      },
    ])

    vi.mocked(adminService.getDepartmentHeads).mockResolvedValue([])
    vi.mocked(adminService.getAcademicPrograms).mockResolvedValue([
      {
        id: 'prog-1',
        code: 'SE',
        name: 'Kỹ thuật phần mềm',
        departmentHeadUserId: null,
        departmentHeadName: null,
        isActive: true,
        coursesCount: 1,
      },
    ])
  })

  it('1. Dispatches a new 3NF notification from AdminNotificationsPage', async () => {
    vi.mocked(adminService.createNotification).mockResolvedValue({
      createdCount: 25,
      targetScope: 'ALL_ACTIVE',
      type: 'SYSTEM_ANNOUNCEMENT',
      title: 'Bảo trì hệ thống',
      createdAt: '2026-11-16T08:00:00Z',
    })

    render(
      <MemoryRouter>
        <AdminNotificationsPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(adminService.getNotifications).toHaveBeenCalled()
      expect(screen.getAllByText('Thông báo lịch thi thử').length).toBeGreaterThan(0)
      expect(screen.getAllByText(/Chưa đọc: 1/i).length).toBeGreaterThanOrEqual(1)
    })

    fireEvent.click(screen.getByRole('button', { name: /Phát thông báo mới/i }))
    fireEvent.change(screen.getByLabelText(/Tiêu đề thông báo/i), {
      target: { value: 'Bảo trì hệ thống' },
    })
    fireEvent.change(screen.getByLabelText(/Nội dung thông báo/i), {
      target: { value: 'Hệ thống nâng cấp lúc 23:00.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Gửi thông báo' }))

    await waitFor(() => {
      expect(adminService.createNotification).toHaveBeenCalledWith(
        expect.objectContaining({
          targetScope: 'ALL_ACTIVE',
          title: 'Bảo trì hệ thống',
          message: 'Hệ thống nâng cấp lúc 23:00.',
        }),
      )
    })
  })

  it('2. Allows resetting temporary password with >= 8 characters on AdminUsersPage', async () => {
    vi.mocked(adminService.resetUserPassword).mockResolvedValue({
      id: 'user-1',
      email: 'sv01@fpt.edu.vn',
      fullName: 'Nguyễn Sinh Viên',
      studentCode: 'SE180101',
      username: 'viennsse180101',
      role: 'student',
      isActive: true,
      mustChangePassword: true,
      createdAt: '2026-10-01T00:00:00Z',
      updatedAt: '2026-11-16T08:00:00Z',
    })

    render(
      <MemoryRouter>
        <AdminUsersPage />
      </MemoryRouter>,
    )

    const resetBtns = await screen.findAllByRole('button', { name: /Cấp lại MK/i })
    fireEvent.click(resetBtns[0])
    fireEvent.change(screen.getByLabelText(/Mật khẩu tạm thời mới/i), {
      target: { value: 'NewTemp#2026' },
    })
    fireEvent.change(screen.getByLabelText(/Lý do cấp lại/i), {
      target: { value: 'Quên mật khẩu' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận cấp lại' }))

    await waitFor(() => {
      expect(adminService.resetUserPassword).toHaveBeenCalledWith('user-1', {
        temporaryPassword: 'NewTemp#2026',
        reason: 'Quên mật khẩu',
      })
    })
  })

  it('3. Syncs semester course offerings and handles CLO 409 Conflict with one-click is_active = false deactivation', async () => {
    vi.mocked(adminService.getSemesterCourseOfferings).mockResolvedValue([
      {
        courseId: 'course-1',
        code: 'PRN231',
        name: 'Building Cross-Platform Apps',
        credits: 3,
        isCourseActive: true,
        isSelected: false,
      },
    ])
    vi.mocked(adminService.syncSemesterCourseOfferings).mockResolvedValue({
      semesterId: 'sem-1',
      createdCount: 1,
      enabledCount: 0,
      disabledCount: 0,
      courses: [],
    })

    const { unmount } = render(
      <MemoryRouter>
        <AdminSemestersPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getAllByText('FA26').length).toBeGreaterThan(0))
    fireEvent.click(screen.getAllByRole('button', { name: /Môn mở trong kỳ/i })[0])

    await waitFor(() => expect(screen.getByText(/Building Cross-Platform Apps/i)).toBeInTheDocument())
    fireEvent.click(screen.getByText(/Building Cross-Platform Apps/i))
    fireEvent.click(screen.getByRole('button', { name: 'Lưu danh mục môn mở' }))

    await waitFor(() => {
      expect(adminService.syncSemesterCourseOfferings).toHaveBeenCalledWith('sem-1', ['course-1'])
    })
    unmount()

    // Now test CLO 409 Conflict + Escape guard + one-click deactivate on AdminCoursesPage
    vi.mocked(courseCloService.list).mockResolvedValue([
      {
        id: 'clo-1',
        courseId: 'course-1',
        cloCode: 'CLO1',
        description: 'Áp dụng Clean Architecture',
        bloomLevel: 'Apply',
        orderIndex: 1,
        isActive: true,
        createdAt: '2026-09-01T00:00:00Z',
      },
    ])
    vi.mocked(courseCloService.remove).mockRejectedValue({
      status: 409,
      detail: 'CONFLICT: CLO đang được câu hỏi tham chiếu.',
    })
    vi.mocked(courseCloService.update).mockResolvedValue({
      id: 'clo-1',
      courseId: 'course-1',
      cloCode: 'CLO1',
      description: 'Áp dụng Clean Architecture',
      bloomLevel: 'Apply',
      orderIndex: 1,
      isActive: false,
      createdAt: '2026-09-01T00:00:00Z',
    })

    render(
      <MemoryRouter>
        <AdminCoursesPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getAllByText('4 lượt/ngày (GMT+7)').length).toBeGreaterThan(0))

    fireEvent.click(screen.getAllByRole('button', { name: 'Quản lý CLO PRN231' })[0])
    await waitFor(() => expect(screen.getAllByText('CLO1').length).toBeGreaterThan(0))

    // Pressing Escape on ConfirmDialog closes only ConfirmDialog, keeping outer CLO FormDialog open
    fireEvent.click(screen.getByRole('button', { name: 'Xóa CLO1' }))
    expect(screen.getByRole('alertdialog')).toBeInTheDocument()
    fireEvent.keyDown(window, { key: 'Escape' })
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument())
    expect(screen.getByRole('dialog')).toBeInTheDocument()

    // Confirm delete triggers 409 Conflict + one-click deactivation button
    fireEvent.click(screen.getByRole('button', { name: 'Xóa CLO1' }))
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận xóa CLO' }))

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/is_active = false/i)
    })

    const deactivateBtn = screen.getByRole('button', {
      name: /Ngừng áp dụng CLO này \(is_active = false\)/i,
    })
    fireEvent.click(deactivateBtn)

    await waitFor(() => {
      expect(courseCloService.update).toHaveBeenCalledWith('course-1', 'clo-1', {
        cloCode: 'CLO1',
        description: 'Áp dụng Clean Architecture',
        bloomLevel: 'Apply',
        orderIndex: 1,
        isActive: false,
      })
      expect(screen.getByRole('status')).toHaveTextContent(/is_active = false/i)
    })
  })

  it('4. Unassigns Course DepartmentHead via Guid.Empty and handles USER recipient search retry/Escape on Notifications', async () => {
    vi.mocked(adminService.getDepartmentHeads).mockResolvedValue([
      {
        id: 'dh-1',
        email: 'head@fpt.edu.vn',
        fullName: 'Trưởng BM CNTT',
        role: 'department_head',
        isActive: true,
        mustChangePassword: false,
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
      },
    ])
    vi.mocked(adminService.getCourses).mockResolvedValue([
      {
        id: 'course-1',
        code: 'PRN231',
        name: 'Building Cross-Platform Apps',
        credits: 3,
        semesterId: 'sem-1',
        semesterCode: 'FA26',
        departmentHeadUserId: 'dh-1',
        departmentHeadName: 'Trưởng BM CNTT',
        maxMockExamsPerDay: 4,
        mockExamDurationMinutes: 30,
        mockExamTotalQuestions: 5,
        hasFollowUp: false,
        examInputMode: 'VoiceWithTranscriptEdit',
        isActive: true,
      },
    ])
    vi.mocked(adminService.updateCourse).mockResolvedValue({
      id: 'course-1',
      code: 'PRN231',
      name: 'Building Cross-Platform Apps',
      credits: 3,
      semesterId: 'sem-1',
      maxMockExamsPerDay: 4,
      mockExamDurationMinutes: 30,
      mockExamTotalQuestions: 5,
      hasFollowUp: false,
      examInputMode: 'VoiceWithTranscriptEdit',
      isActive: true,
    })

    const { unmount } = render(
      <MemoryRouter>
        <AdminCoursesPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getAllByText('Trưởng BM CNTT').length).toBeGreaterThan(0))
    fireEvent.click(screen.getByRole('button', { name: 'Sửa môn học PRN231' }))
    fireEvent.change(screen.getByLabelText(/Trưởng bộ môn phụ trách/i), {
      target: { value: '' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Cập nhật môn học' }))

    await waitFor(() => {
      expect(adminService.updateCourse).toHaveBeenCalledWith(
        'course-1',
        expect.objectContaining({
          departmentHeadUserId: '00000000-0000-0000-0000-000000000000',
        }),
      )
    })
    unmount()

    render(
      <MemoryRouter>
        <AdminNotificationsPage />
      </MemoryRouter>,
    )

    await waitFor(() =>
      expect(screen.getByRole('button', { name: /Phát thông báo mới/i })).toBeInTheDocument(),
    )
    fireEvent.click(screen.getByRole('button', { name: /Phát thông báo mới/i }))

    vi.mocked(adminService.getUsers).mockRejectedValueOnce(new Error('Mất kết nối danh bạ'))
    fireEvent.change(screen.getByLabelText(/Phạm vi nhận/i), { target: { value: 'USER' } })

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/Mất kết nối danh bạ/i)
    })

    fireEvent.change(screen.getByLabelText(/Tìm tài khoản người nhận/i), {
      target: { value: 'SE180101' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Tìm' }))

    await waitFor(() => {
      expect(adminService.getUsers).toHaveBeenCalledWith(
        expect.objectContaining({
          search: 'SE180101',
          isActive: true,
          page: 1,
          pageSize: 30,
        }),
      )
    })

    fireEvent.keyDown(window, { key: 'Escape' })
    await waitFor(() => {
      expect(
        screen.queryByRole('dialog', { name: /Phát thông báo hệ thống mới/i }),
      ).not.toBeInTheDocument()
    })
  })

  it('5. Resolves pending DLQ task (including legacy null flowType) with resolutionNotes', async () => {
    vi.mocked(deadLetterQueueService.listForAdmin).mockResolvedValue([
      {
        id: 'dlq-legacy-1',
        taskType: 'PracticeGrading',
        flowType: null,
        payloadJson: '{}',
        status: 'pending',
        retryCount: 3,
        errorMessage: 'Gemini 503 timeout',
        createdAt: '2026-11-16T08:00:00Z',
        resolvedAt: null,
      },
    ])
    vi.mocked(deadLetterQueueService.resolveForAdmin).mockResolvedValue({
      id: 'dlq-legacy-1',
      dlqId: 'dlq-legacy-1',
      status: 'resolved',
      message: 'Đã đánh dấu xử lý tác vụ DLQ.',
    })

    render(
      <MemoryRouter>
        <DeadLetterQueuePage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getByText('Gemini 503 timeout')).toBeInTheDocument())

    fireEvent.click(screen.getByRole('button', { name: /Đánh dấu đã xử lý/i }))
    fireEvent.change(screen.getByLabelText(/Ghi chú xử lý sự cố/i), {
      target: { value: 'Đã kiểm tra bài luyện tập và đóng cảnh báo.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận đã xử lý' }))

    await waitFor(() => {
      expect(deadLetterQueueService.resolveForAdmin).toHaveBeenCalledWith(
        'dlq-legacy-1',
        'Đã kiểm tra bài luyện tập và đóng cảnh báo.',
      )
    })
  })

  it('6. AdminSemesterOfferingsModal supports select-all active (keeping inactive disabled), clear-all, and error + Retry', async () => {
    vi.mocked(adminService.getSemesterCourseOfferings)
      .mockRejectedValueOnce({
        status: 500,
        detail: 'Không thể tải danh mục môn mở học kỳ.',
      })
      .mockResolvedValueOnce([
        {
          courseId: 'c-active-1',
          code: 'PRN231',
          name: 'Building Cross-Platform Apps',
          credits: 3,
          isCourseActive: true,
          isSelected: false,
        },
        {
          courseId: 'c-inactive-unselected',
          code: 'OLD101',
          name: 'Legacy Course',
          credits: 2,
          isCourseActive: false,
          isSelected: false,
        },
        {
          courseId: 'c-inactive-selected',
          code: 'OLD102',
          name: 'Legacy Selected Course',
          credits: 2,
          isCourseActive: false,
          isSelected: true,
        },
      ])

    render(
      <MemoryRouter>
        <AdminSemestersPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getAllByText('FA26').length).toBeGreaterThan(0))
    fireEvent.click(screen.getAllByRole('button', { name: /Môn mở trong kỳ/i })[0])

    // Initial error suppresses EmptyState and shows Retry
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Không thể tải danh mục môn mở học kỳ.')
      expect(screen.queryByText('Chưa có môn học nào để mở trong học kỳ này')).toBeNull()
    })

    // Retry loads courses
    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }))
    await waitFor(() => expect(screen.getByText(/Legacy Course/i)).toBeInTheDocument())

    const inactiveUnselectedBtn = screen.getByRole('button', { name: /OLD101/i })
    expect(inactiveUnselectedBtn).toBeDisabled()
    expect(inactiveUnselectedBtn).toHaveAttribute('aria-pressed', 'false')

    // Select all active courses -> PRN231 selected, OLD101 stays disabled/unselected
    fireEvent.click(screen.getByRole('button', { name: 'Chọn toàn bộ môn active' }))
    expect(screen.getByRole('button', { name: /PRN231/i })).toHaveAttribute('aria-pressed', 'true')
    expect(inactiveUnselectedBtn).toBeDisabled()
    expect(inactiveUnselectedBtn).toHaveAttribute('aria-pressed', 'false')

    // Clear all -> 0 selected, Bỏ chọn hết becomes disabled
    const clearAllBtn = screen.getByRole('button', { name: 'Bỏ chọn hết' })
    fireEvent.click(clearAllBtn)
    expect(screen.getByRole('button', { name: /PRN231/i })).toHaveAttribute('aria-pressed', 'false')
    expect(clearAllBtn).toBeDisabled()
  })

  it('7. AdminCourseClosModal supports create, inline update, client validation, and inline-editor Escape precedence', async () => {
    vi.mocked(courseCloService.list).mockResolvedValue([
      {
        id: 'clo-1',
        courseId: 'course-1',
        cloCode: 'CLO1',
        description: 'Áp dụng Clean Architecture',
        bloomLevel: 'Apply',
        orderIndex: 1,
        isActive: true,
        createdAt: '2026-09-01T00:00:00Z',
      },
    ])
    vi.mocked(courseCloService.create).mockResolvedValue({
      id: 'clo-2',
      courseId: 'course-1',
      cloCode: 'CLO2',
      description: 'Phân tích hiệu năng CSDL',
      bloomLevel: 'Analyze',
      orderIndex: 2,
      isActive: true,
      createdAt: '2026-11-18T00:00:00Z',
    })
    vi.mocked(courseCloService.update).mockResolvedValue({
      id: 'clo-1',
      courseId: 'course-1',
      cloCode: 'CLO1',
      description: 'Áp dụng Clean Architecture (.NET 8)',
      bloomLevel: 'Evaluate',
      orderIndex: 1,
      isActive: false,
      createdAt: '2026-09-01T00:00:00Z',
    })

    render(
      <MemoryRouter>
        <AdminCoursesPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getAllByText('PRN231').length).toBeGreaterThan(0))
    fireEvent.click(screen.getAllByRole('button', { name: 'Quản lý CLO PRN231' })[0])
    await waitFor(() => expect(screen.getAllByText('CLO1').length).toBeGreaterThan(0))

    // Open inline editor and verify Escape closes ONLY inline editor, keeping outer CLO dialog open
    fireEvent.click(screen.getByRole('button', { name: 'Thêm CLO' }))
    expect(screen.getByText('Thêm chuẩn đầu ra mới')).toBeInTheDocument()
    fireEvent.keyDown(window, { key: 'Escape' })
    await waitFor(() => {
      expect(screen.queryByText('Thêm chuẩn đầu ra mới')).not.toBeInTheDocument()
    })
    expect(screen.getByRole('dialog')).toBeInTheDocument()

    // Re-open inline editor and test client validation (duplicate cloCode, empty description, orderIndex < 1)
    fireEvent.click(screen.getByRole('button', { name: 'Thêm CLO' }))
    expect(screen.getByLabelText(/Thứ tự hiển thị/i)).toHaveValue(2)

    fireEvent.change(screen.getByLabelText(/Mã CLO/i), { target: { value: 'clo1' } })
    fireEvent.change(screen.getByLabelText(/Thứ tự hiển thị/i), { target: { value: '0' } })
    fireEvent.change(screen.getByLabelText(/Mô tả chuẩn đầu ra/i), { target: { value: '   ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu CLO' }))

    expect(screen.getByText('Mã CLO [CLO1] đã tồn tại trong môn PRN231.')).toBeInTheDocument()
    expect(
      screen.getByText('Mô tả chuẩn đầu ra bắt buộc từ 1 đến 1000 ký tự.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('Thứ tự hiển thị phải là số nguyên dương (>= 1).'),
    ).toBeInTheDocument()
    expect(courseCloService.create).not.toHaveBeenCalled()

    // Test length > 30 bound on cloCode
    fireEvent.change(screen.getByLabelText(/Mã CLO/i), {
      target: { value: 'CLO_CODE_EXCEEDING_THIRTY_CHARS_123' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu CLO' }))
    expect(screen.getByText('Mã CLO bắt buộc từ 1 đến 30 ký tự.')).toBeInTheDocument()
    expect(courseCloService.create).not.toHaveBeenCalled()

    // Valid create
    fireEvent.change(screen.getByLabelText(/Mã CLO/i), { target: { value: 'clo2' } })
    fireEvent.change(screen.getByLabelText(/Bậc Bloom/i), { target: { value: 'Analyze' } })
    fireEvent.change(screen.getByLabelText(/Thứ tự hiển thị/i), { target: { value: '2' } })
    fireEvent.change(screen.getByLabelText(/Mô tả chuẩn đầu ra/i), {
      target: { value: 'Phân tích hiệu năng CSDL' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu CLO' }))

    await waitFor(() => {
      expect(courseCloService.create).toHaveBeenCalledWith('course-1', {
        cloCode: 'CLO2',
        description: 'Phân tích hiệu năng CSDL',
        bloomLevel: 'Analyze',
        orderIndex: 2,
      })
      expect(screen.getByRole('status')).toHaveTextContent(
        'Đã tạo chuẩn đầu ra [CLO2] cho môn PRN231.',
      )
      expect(screen.getAllByText('CLO2').length).toBeGreaterThan(0)
    })

    // Inline update on CLO1
    fireEvent.click(screen.getByRole('button', { name: 'Sửa CLO1' }))
    expect(screen.getByText('Chỉnh sửa CLO1')).toBeInTheDocument()
    fireEvent.change(screen.getByLabelText(/Bậc Bloom/i), { target: { value: 'Evaluate' } })
    fireEvent.change(screen.getByLabelText(/Mô tả chuẩn đầu ra/i), {
      target: { value: 'Áp dụng Clean Architecture (.NET 8)' },
    })
    fireEvent.click(screen.getByLabelText(/Đang hoạt động \(is_active\)/i))
    fireEvent.click(screen.getByRole('button', { name: 'Lưu CLO' }))

    await waitFor(() => {
      expect(courseCloService.update).toHaveBeenCalledWith('course-1', 'clo-1', {
        cloCode: 'CLO1',
        description: 'Áp dụng Clean Architecture (.NET 8)',
        bloomLevel: 'Evaluate',
        orderIndex: 1,
        isActive: false,
      })
      expect(screen.getByRole('status')).toHaveTextContent('Đã cập nhật chuẩn đầu ra [CLO1].')
    })
  })
})
