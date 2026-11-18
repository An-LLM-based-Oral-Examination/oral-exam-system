import { MemoryRouter } from 'react-router-dom'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AdminDashboardPage } from '../AdminDashboardPage'
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
  },
}))

vi.mock('@/components/admin/AbuseMetricsPanel', () => ({
  AbuseMetricsPanel: () => <div data-testid="abuse-metrics-panel" />,
}))

vi.mock('@/services/admin.service', () => ({
  adminService: {
    getUsers: vi.fn(),
    createUser: vi.fn(),
    updateUserStatus: vi.fn(),
    updateUserRole: vi.fn(),
    resetUserPassword: vi.fn(),
    getSystemConfigs: vi.fn(),
    updateSystemConfig: vi.fn(),
    getAuditLogs: vi.fn(),
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

describe('AdminDashboardPage - Full Portal Completion (Users, Offerings, CLOs, Notifications 3NF, Recovery)', () => {
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
      pageSize: 15,
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
        semesterId: 'sem-1',
        maxMockExamsPerDay: 4,
        mockExamDurationMinutes: 30,
        mockExamTotalQuestions: 5,
        hasFollowUp: false,
        examInputMode: 'VoiceWithTranscriptEdit',
        isActive: true,
      },
    ])

    vi.mocked(adminService.getDepartmentHeads).mockResolvedValue([])
  })

  it('1. Restores Notifications tab from URL ?tab=notifications and dispatches a new 3NF notification', async () => {
    vi.mocked(adminService.createNotification).mockResolvedValue({
      createdCount: 25,
      targetScope: 'ALL_ACTIVE',
      type: 'SYSTEM_ANNOUNCEMENT',
      title: 'Bảo trì hệ thống',
      createdAt: '2026-11-16T08:00:00Z',
    })

    render(
      <MemoryRouter initialEntries={['/admin/dashboard?tab=notifications']}>
        <AdminDashboardPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(adminService.getNotifications).toHaveBeenCalled()
      expect(screen.getByText('Thông báo lịch thi thử')).toBeInTheDocument()
      expect(screen.getByText(/Chưa đọc: 1/i)).toBeInTheDocument()
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

  it('2. Allows resetting temporary password with >= 8 characters and revoking sessions', async () => {
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
        <AdminDashboardPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getByText('@viennsse180101')).toBeInTheDocument())

    fireEvent.click(screen.getByRole('button', { name: 'Cấp lại MK' }))
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

  it('3. Syncs semester course offerings and displays 409 Conflict guidance when deleting a referenced CLO', async () => {
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

    render(
      <MemoryRouter initialEntries={['/admin/dashboard?tab=semesters']}>
        <AdminDashboardPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getByText('FA26')).toBeInTheDocument())
    fireEvent.click(screen.getByRole('button', { name: /Môn mở trong kỳ/i }))

    await waitFor(() => expect(screen.getByText(/Building Cross-Platform Apps/i)).toBeInTheDocument())
    fireEvent.click(screen.getByText(/Building Cross-Platform Apps/i))
    fireEvent.click(screen.getByRole('button', { name: 'Lưu danh mục môn mở' }))

    await waitFor(() => {
      expect(adminService.syncSemesterCourseOfferings).toHaveBeenCalledWith('sem-1', ['course-1'])
    })

    // Switch to Courses tab and test CLO 409 Conflict guidance
    fireEvent.click(screen.getByRole('button', { name: /Môn học/i }))
    await waitFor(() => expect(screen.getByText('4 lượt/ngày (GMT+7)')).toBeInTheDocument())

    fireEvent.click(screen.getByRole('button', { name: /^CLO$/i }))
    await waitFor(() => expect(screen.getByText('CLO1')).toBeInTheDocument())

    fireEvent.click(screen.getByRole('button', { name: /^Xóa$/i }))
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận xóa CLO' }))

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/is_active = false/i)
    })
  })

  it('4. Searches active recipients server-side for USER notifications, surfaces load errors, and unassigns DepartmentHead via Guid.Empty', async () => {
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

    render(
      <MemoryRouter initialEntries={['/admin/dashboard?tab=courses']}>
        <AdminDashboardPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getByText('Trưởng BM CNTT')).toBeInTheDocument())
    fireEvent.click(screen.getByRole('button', { name: /Sửa/i }))
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

    // Switch to Notifications tab -> USER scope -> error banner + retry search
    fireEvent.click(screen.getByRole('button', { name: /Thông báo/i }))
    await waitFor(() => expect(screen.getByRole('button', { name: /Phát thông báo mới/i })).toBeInTheDocument())
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

    // Escape closes modal cleanly
    fireEvent.keyDown(window, { key: 'Escape' })
    await waitFor(() => {
      expect(screen.queryByRole('dialog', { name: /Phát thông báo hệ thống mới/i })).not.toBeInTheDocument()
    })
  })

  it('5. Resolves pending DLQ task (including legacy null flowType) with resolutionNotes', async () => {
    vi.mocked(deadLetterQueueService.list).mockResolvedValue([
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
    vi.mocked(deadLetterQueueService.resolve).mockResolvedValue({
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
      expect(deadLetterQueueService.resolve).toHaveBeenCalledWith(
        'dlq-legacy-1',
        'Đã kiểm tra bài luyện tập và đóng cảnh báo.',
      )
    })
  })
})
