import { MemoryRouter } from 'react-router-dom'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AdminDashboardPage } from '../AdminDashboardPage'
import { adminService } from '@/services/admin.service'

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

const mockGetUsers = vi.mocked(adminService.getUsers)
const mockGetSemesters = vi.mocked(adminService.getSemesters)
const mockCreateSemester = vi.mocked(adminService.createSemester)
const mockGetCourses = vi.mocked(adminService.getCourses)
const mockCreateCourse = vi.mocked(adminService.createCourse)
const mockGetDepartmentHeads = vi.mocked(adminService.getDepartmentHeads)
const mockAssignDepartmentHead = vi.mocked(adminService.assignDepartmentHead)

describe('AdminDashboardPage - Academic Foundation (TASK-004A)', () => {
  beforeEach(() => {
    vi.clearAllMocks()

    mockGetUsers.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 15,
      totalPages: 1,
    })

    mockGetSemesters.mockResolvedValue([
      {
        id: 'sem-1',
        code: 'FA26',
        name: 'Fall 2026',
        startDate: '2026-09-01T00:00:00Z',
        endDate: '2026-12-31T00:00:00Z',
        isActive: true,
        createdAt: '2026-08-01T00:00:00Z',
      },
    ])

    mockGetDepartmentHeads.mockResolvedValue([
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

    mockGetCourses.mockResolvedValue([
      {
        id: 'course-1',
        code: 'PRN231',
        name: 'Building Cross-Platform Apps',
        credits: 3,
        description: 'Dotnet 8 Clean Architecture',
        departmentHeadUserId: 'dh-1',
        departmentHeadName: 'Trưởng BM CNTT',
        semesterId: 'sem-1',
        maxMockExamsPerDay: 3,
        mockExamDurationMinutes: 30,
        mockExamTotalQuestions: 5,
        hasFollowUp: true,
        examInputMode: 'VoiceWithTranscriptEdit',
        isActive: true,
      },
    ])
  })

  it('renders Semesters tab and shows semester list without delete action', async () => {
    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>,
    )

    // Switch to Semesters tab
    const tabBtn = screen.getByRole('button', { name: /Học kỳ/i })
    fireEvent.click(tabBtn)

    await waitFor(() => {
      expect(mockGetSemesters).toHaveBeenCalled()
      expect(screen.getByText('FA26')).toBeInTheDocument()
      expect(screen.getByText('Fall 2026')).toBeInTheDocument()
    })

    // Confirm immutability rule text is present and no delete button exists
    expect(screen.getByText(/CẤM XÓA/i)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Xóa/i })).not.toBeInTheDocument()
  })

  it('allows creating a semester and calls adminService.createSemester', async () => {
    mockCreateSemester.mockResolvedValue({
      id: 'sem-2',
      code: 'SP27',
      name: 'Spring 2027',
      startDate: '2027-01-01T00:00:00Z',
      endDate: '2027-04-30T00:00:00Z',
      isActive: true,
      createdAt: '2026-11-15T00:00:00Z',
    })

    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>,
    )

    fireEvent.click(screen.getByRole('button', { name: /Học kỳ/i }))

    await waitFor(() => expect(screen.getByText('FA26')).toBeInTheDocument())

    // Click "Thêm học kỳ mới"
    const addBtn = screen.getByRole('button', { name: /Thêm học kỳ mới/i })
    fireEvent.click(addBtn)

    expect(screen.getByText('Tạo Học Kỳ Mới')).toBeInTheDocument()

    // Fill the form
    fireEvent.change(screen.getByPlaceholderText(/ví dụ: FA26/i), { target: { value: 'sp27' } })
    fireEvent.change(screen.getByPlaceholderText(/ví dụ: Fall 2026/i), { target: { value: 'Spring 2027' } })

    const submitBtn = screen.getByRole('button', { name: 'Tạo học kỳ' })
    fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(mockCreateSemester).toHaveBeenCalledWith(
        expect.objectContaining({
          code: 'SP27',
          name: 'Spring 2027',
        }),
      )
    })
  })

  it('renders Courses tab and displays course details', async () => {
    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>,
    )

    // Switch to Courses tab
    const tabBtn = screen.getByRole('button', { name: /Môn học/i })
    fireEvent.click(tabBtn)

    await waitFor(() => {
      expect(mockGetCourses).toHaveBeenCalled()
      expect(mockGetDepartmentHeads).toHaveBeenCalled()
      expect(screen.getByText('PRN231')).toBeInTheDocument()
      expect(screen.getByText('Building Cross-Platform Apps')).toBeInTheDocument()
      expect(screen.getByText('3 TC')).toBeInTheDocument()
      expect(screen.getByText('Trưởng BM CNTT')).toBeInTheDocument()
    })
  })

  it('allows creating a course and calls adminService.createCourse', async () => {
    mockCreateCourse.mockResolvedValue({
      id: 'course-2',
      code: 'SWD392',
      name: 'Software Architecture and Design',
      credits: 3,
      departmentHeadUserId: 'dh-1',
      departmentHeadName: 'Trưởng BM CNTT',
      semesterId: 'sem-1',
      maxMockExamsPerDay: 3,
      mockExamDurationMinutes: 30,
      mockExamTotalQuestions: 5,
      hasFollowUp: true,
      examInputMode: 'VoiceWithTranscriptEdit',
      isActive: true,
    })

    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>,
    )

    fireEvent.click(screen.getByRole('button', { name: /Môn học/i }))

    await waitFor(() => expect(screen.getByText('PRN231')).toBeInTheDocument())

    // Click "Thêm môn học mới"
    const addBtn = screen.getByRole('button', { name: /Thêm môn học mới/i })
    fireEvent.click(addBtn)

    expect(screen.getByText('Tạo Môn Học Mới')).toBeInTheDocument()

    // Fill form
    fireEvent.change(screen.getByPlaceholderText(/ví dụ: PRN231/i), { target: { value: 'swd392' } })
    fireEvent.change(screen.getByPlaceholderText(/ví dụ: Cross-Platform/i), { target: { value: 'Software Architecture' } })

    const submitBtn = screen.getByRole('button', { name: 'Tạo môn học' })
    fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(mockCreateCourse).toHaveBeenCalledWith(
        expect.objectContaining({
          code: 'SWD392',
          name: 'Software Architecture',
          credits: 3,
          semesterId: 'sem-1',
        }),
      )
    })
  })

  it('T4A-F005: directly navigating from Users to Courses loads semesters and submits with explicit semesterId', async () => {
    mockCreateCourse.mockResolvedValue({
      id: 'course-new',
      code: 'PRN232',
      name: 'Advanced .NET Architecture',
      credits: 4,
      departmentHeadUserId: 'dh-1',
      departmentHeadName: 'Trưởng BM CNTT',
      semesterId: 'sem-1',
      maxMockExamsPerDay: 3,
      mockExamDurationMinutes: 30,
      mockExamTotalQuestions: 5,
      hasFollowUp: true,
      examInputMode: 'VoiceWithTranscriptEdit',
      isActive: true,
    })

    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>,
    )

    // Initially on Users tab
    await waitFor(() => expect(mockGetUsers).toHaveBeenCalled())

    // Directly click Courses tab without opening Semesters tab first
    const coursesTabBtn = screen.getByRole('button', { name: /Môn học/i })
    fireEvent.click(coursesTabBtn)

    await waitFor(() => {
      expect(mockGetCourses).toHaveBeenCalledWith(true)
      expect(mockGetDepartmentHeads).toHaveBeenCalled()
      // Proves semesters are preloaded when directly opening Courses tab (T4A-F005)
      expect(mockGetSemesters).toHaveBeenCalled()
      expect(screen.getByText('PRN231')).toBeInTheDocument()
    })

    // Click "Thêm môn học mới"
    const addCourseBtn = screen.getByRole('button', { name: /Thêm môn học mới/i })
    fireEvent.click(addCourseBtn)

    expect(screen.getByText('Tạo Môn Học Mới')).toBeInTheDocument()

    // Fill form
    fireEvent.change(screen.getByPlaceholderText(/ví dụ: PRN231/i), { target: { value: 'PRN232' } })
    fireEvent.change(screen.getByPlaceholderText(/ví dụ: Cross-Platform/i), { target: { value: 'Advanced .NET Architecture' } })

    // Verify semester select contains FA26 option
    const semesterSelect = screen.getByRole('combobox', { name: /Học kỳ áp dụng/i })
    expect(semesterSelect).toHaveValue('sem-1')

    const submitBtn = screen.getByRole('button', { name: 'Tạo môn học' })
    fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(mockCreateCourse).toHaveBeenCalledWith(
        expect.objectContaining({
          code: 'PRN232',
          name: 'Advanced .NET Architecture',
          credits: 3,
          semesterId: 'sem-1',
        }),
      )
    })
  })

  it('T4A-F006: rejects semester creation when EndDate equals StartDate', async () => {
    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>,
    )

    fireEvent.click(screen.getByRole('button', { name: /Học kỳ/i }))

    await waitFor(() => expect(screen.getByText('FA26')).toBeInTheDocument())

    fireEvent.click(screen.getByRole('button', { name: /Thêm học kỳ mới/i }))

    expect(screen.getByText('Tạo Học Kỳ Mới')).toBeInTheDocument()

    fireEvent.change(screen.getByPlaceholderText(/ví dụ: FA26/i), { target: { value: 'FA27' } })
    fireEvent.change(screen.getByPlaceholderText(/ví dụ: Fall 2026/i), { target: { value: 'Fall 2027' } })

    // Set identical start and end dates (EndDate == StartDate)
    const dateInputs = screen.getAllByDisplayValue(/\d{4}-\d{2}-\d{2}/)
    // Start date and end date
    fireEvent.change(dateInputs[0], { target: { value: '2027-09-01' } })
    fireEvent.change(dateInputs[1], { target: { value: '2027-09-01' } })

    const submitBtn = screen.getByRole('button', { name: 'Tạo học kỳ' })
    fireEvent.click(submitBtn)

    // Must NOT call createSemester when EndDate == StartDate
    expect(mockCreateSemester).not.toHaveBeenCalled()
  })

  it('allows assigning Department Head to a course', async () => {
    mockAssignDepartmentHead.mockResolvedValue({
      courseId: 'course-1',
      departmentHeadUserId: 'dh-1',
    })

    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>,
    )

    fireEvent.click(screen.getByRole('button', { name: /Môn học/i }))

    await waitFor(() => expect(screen.getByText('PRN231')).toBeInTheDocument())

    // Click "Gán Trưởng BM"
    const assignBtn = screen.getByRole('button', { name: /Gán Trưởng BM/i })
    fireEvent.click(assignBtn)

    expect(screen.getByText('Gán Trưởng Bộ Môn')).toBeInTheDocument()

    const saveBtn = screen.getByRole('button', { name: 'Lưu phân công' })
    fireEvent.click(saveBtn)

    await waitFor(() => {
      expect(mockAssignDepartmentHead).toHaveBeenCalledWith('course-1', 'dh-1')
    })
  })
})
