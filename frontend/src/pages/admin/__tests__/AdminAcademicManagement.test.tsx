import { MemoryRouter } from 'react-router-dom'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AdminCoursesPage } from '../AdminCoursesPage'
import { AdminSemestersPage } from '../AdminSemestersPage'
import { adminService } from '@/services/admin.service'

vi.mock('@/services/admin.service', () => ({
  adminService: {
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
    createAcademicProgram: vi.fn(),
    updateAcademicProgram: vi.fn(),
    assignProgramDepartmentHead: vi.fn(),
  },
}))

const mockGetSemesters = vi.mocked(adminService.getSemesters)
const mockCreateSemester = vi.mocked(adminService.createSemester)
const mockUpdateSemester = vi.mocked(adminService.updateSemester)
const mockGetCourses = vi.mocked(adminService.getCourses)
const mockCreateCourse = vi.mocked(adminService.createCourse)
const mockUpdateCourse = vi.mocked(adminService.updateCourse)
const mockGetDepartmentHeads = vi.mocked(adminService.getDepartmentHeads)
const mockAssignDepartmentHead = vi.mocked(adminService.assignDepartmentHead)
const mockGetAcademicPrograms = vi.mocked(adminService.getAcademicPrograms)
const mockCreateAcademicProgram = vi.mocked(adminService.createAcademicProgram)
const mockUpdateAcademicProgram = vi.mocked(adminService.updateAcademicProgram)
const mockAssignProgramDepartmentHead = vi.mocked(adminService.assignProgramDepartmentHead)

describe('Admin Academic Management — Semesters, Programs & Courses (M3)', () => {
  beforeEach(() => {
    vi.clearAllMocks()

    mockGetSemesters.mockResolvedValue([
      {
        id: 'sem-1',
        code: 'FA26',
        name: 'Fall 2026',
        startDate: '2026-09-01T00:00:00Z',
        endDate: '2026-12-31T00:00:00Z',
        isActive: true,
        coursesCount: 2,
        classesCount: 4,
        createdAt: '2026-08-01T00:00:00Z',
      },
      {
        id: 'sem-old',
        code: 'SU25',
        name: 'Summer 2025',
        startDate: '2025-05-01T00:00:00Z',
        endDate: '2025-08-31T00:00:00Z',
        isActive: false,
        coursesCount: 1,
        classesCount: 2,
        createdAt: '2025-04-01T00:00:00Z',
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

    mockGetAcademicPrograms.mockResolvedValue([
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

    mockGetCourses.mockResolvedValue([
      {
        id: 'course-1',
        code: 'PRN231',
        name: 'Building Cross-Platform Apps',
        credits: 3,
        description: 'Dotnet 8 Clean Architecture',
        academicProgramId: 'prog-1',
        departmentHeadUserId: 'dh-1',
        departmentHeadName: 'Trưởng BM CNTT',
        semesterId: 'sem-1',
        semesterCode: 'FA26',
        maxMockExamsPerDay: 3,
        mockExamDurationMinutes: 30,
        mockExamTotalQuestions: 5,
        hasFollowUp: true,
        examInputMode: 'VoiceWithTranscriptEdit',
        isActive: true,
      },
    ])
  })

  it('renders Semesters page with ON DELETE RESTRICT notice, URL filter restore, and zero delete buttons', async () => {
    render(
      <MemoryRouter initialEntries={['/admin/semesters?q=FA26&status=active']}>
        <AdminSemestersPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(mockGetSemesters).toHaveBeenCalled()
      expect(screen.getAllByText('FA26').length).toBeGreaterThan(0)
      expect(screen.getAllByText('Fall 2026').length).toBeGreaterThan(0)
    })

    expect(screen.queryByText('SU25')).not.toBeInTheDocument()
    expect(screen.getByPlaceholderText(/Tìm mã học kỳ/i)).toHaveValue('FA26')
    expect(screen.getByText(/ON DELETE RESTRICT/i)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^Xóa$/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Xóa học kỳ/i })).not.toBeInTheDocument()
  })

  it('creates a semester with uppercase code and rejects EndDate == StartDate', async () => {
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
        <AdminSemestersPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getAllByText('FA26').length).toBeGreaterThan(0))

    fireEvent.click(screen.getByRole('button', { name: /Thêm học kỳ mới/i }))
    expect(screen.getByRole('dialog')).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText(/Mã học kỳ/i), { target: { value: 'sp27' } })
    fireEvent.change(screen.getByLabelText(/Tên học kỳ/i), { target: { value: 'Spring 2027' } })
    fireEvent.change(screen.getByLabelText(/Ngày bắt đầu/i), { target: { value: '2027-09-01' } })
    fireEvent.change(screen.getByLabelText(/Ngày kết thúc/i), { target: { value: '2027-09-01' } })

    fireEvent.click(screen.getByRole('button', { name: 'Tạo học kỳ' }))
    expect(mockCreateSemester).not.toHaveBeenCalled()
    expect(
      screen.getByText(/Ngày kết thúc học kỳ bắt buộc phải sau ngày bắt đầu/i),
    ).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText(/Ngày kết thúc/i), { target: { value: '2027-12-20' } })
    fireEvent.click(screen.getByRole('button', { name: 'Tạo học kỳ' }))

    await waitFor(() => {
      expect(mockCreateSemester).toHaveBeenCalledWith(
        expect.objectContaining({
          code: 'SP27',
          name: 'Spring 2027',
        }),
      )
    })
  })

  it('handles 401/403/409/429 and keyboard Escape/FilterDropMenu navigation on Semesters page', async () => {
    mockGetSemesters
      .mockRejectedValueOnce({ status: 401, detail: 'Phiên đăng nhập hết hạn.' })
      .mockRejectedValueOnce({ status: 403, detail: 'Không đủ quyền truy cập.' })
      .mockRejectedValueOnce({ status: 429, detail: 'Quá giới hạn yêu cầu.' })

    const { unmount } = render(
      <MemoryRouter>
        <AdminSemestersPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getByRole('link', { name: 'Đăng nhập lại' })).toHaveAttribute('href', '/login')
    })
    expect(screen.queryByText(/Chưa có học kỳ nào trong hệ thống/i)).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }))
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Không đủ quyền truy cập.')
    })

    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }))
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Quá giới hạn yêu cầu.')
    })
    unmount()

    mockGetSemesters.mockResolvedValue([
      {
        id: 'sem-1',
        code: 'FA26',
        name: 'Fall 2026',
        startDate: '2026-09-01',
        endDate: '2026-12-31',
        isActive: true,
        createdAt: '2026-08-01T00:00:00Z',
      },
    ])
    mockCreateSemester.mockRejectedValueOnce({
      status: 409,
      detail: 'CONFLICT: Học kỳ có mã FA26 đã tồn tại.',
    })

    render(
      <MemoryRouter>
        <AdminSemestersPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getAllByText('FA26').length).toBeGreaterThan(0))

    // FilterDropMenu keyboard navigation
    const filterBtn = screen.getByRole('button', { name: 'Trạng thái học kỳ' })
    fireEvent.keyDown(filterBtn, { key: 'ArrowDown' })
    await waitFor(() => expect(screen.getByRole('listbox')).toBeInTheDocument())
    fireEvent.keyDown(window, { key: 'Escape' })
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument())

    // 409 conflict inside dialog + Escape close
    fireEvent.click(screen.getByRole('button', { name: /Thêm học kỳ mới/i }))
    fireEvent.change(screen.getByLabelText(/Mã học kỳ/i), { target: { value: 'FA26' } })
    fireEvent.change(screen.getByLabelText(/Tên học kỳ/i), { target: { value: 'Fall 2026' } })
    fireEvent.click(screen.getByRole('button', { name: 'Tạo học kỳ' }))

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Học kỳ có mã FA26 đã tồn tại.')
    })

    fireEvent.keyDown(window, { key: 'Escape' })
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('T4A-F005: preloads semesters, programs, and courses on AdminCoursesPage and creates course with explicit semesterId', async () => {
    mockCreateCourse.mockResolvedValue({
      id: 'course-2',
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
      <MemoryRouter initialEntries={['/admin/courses?q=PRN&semester=sem-1&status=active']}>
        <AdminCoursesPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(mockGetCourses).toHaveBeenCalledWith(true)
      expect(mockGetDepartmentHeads).toHaveBeenCalled()
      expect(mockGetSemesters).toHaveBeenCalled()
      expect(mockGetAcademicPrograms).toHaveBeenCalled()
      expect(screen.getAllByText('PRN231').length).toBeGreaterThan(0)
      expect(screen.getAllByText('3 TC').length).toBeGreaterThan(0)
      expect(screen.getAllByText('Trưởng BM CNTT').length).toBeGreaterThan(0)
    })

    fireEvent.click(screen.getByRole('button', { name: /Thêm môn học mới/i }))
    expect(screen.getByRole('dialog')).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText(/^Mã môn$/i), { target: { value: 'prn232' } })
    fireEvent.change(screen.getByLabelText(/Tên môn học/i), {
      target: { value: 'Advanced .NET Architecture' },
    })

    const semesterSelect = screen.getByRole('combobox', { name: /Học kỳ áp dụng/i })
    expect(semesterSelect).toHaveValue('sem-1')

    fireEvent.click(screen.getByRole('button', { name: 'Tạo môn học' }))

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

  it('assigns Department Head to both a course and an academic program', async () => {
    mockAssignDepartmentHead.mockResolvedValue({
      courseId: 'course-1',
      courseCode: 'PRN231',
      courseName: 'Building Cross-Platform Apps',
      departmentHeadUserId: 'dh-1',
      departmentHeadName: 'Trưởng BM CNTT',
      departmentHeadEmail: 'head@fpt.edu.vn',
    })
    mockAssignProgramDepartmentHead.mockResolvedValue({
      id: 'prog-1',
      code: 'SE',
      name: 'Kỹ thuật phần mềm',
      departmentHeadUserId: 'dh-1',
      departmentHeadName: 'Trưởng BM CNTT',
      isActive: true,
      coursesCount: 1,
    })

    render(
      <MemoryRouter>
        <AdminCoursesPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getAllByText('PRN231').length).toBeGreaterThan(0))

    // Assign Program Dept Head
    fireEvent.click(screen.getByRole('button', { name: /Gán Trưởng BM ngành SE/i }))
    fireEvent.change(screen.getByLabelText(/Chọn Trưởng bộ môn/i), { target: { value: 'dh-1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu phân công ngành' }))

    await waitFor(() => {
      expect(mockAssignProgramDepartmentHead).toHaveBeenCalledWith('prog-1', 'dh-1')
    })

    // Assign Course Dept Head
    fireEvent.click(screen.getAllByRole('button', { name: 'Gán Trưởng BM PRN231' })[0])
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Lưu phân công' }))

    await waitFor(() => {
      expect(mockAssignDepartmentHead).toHaveBeenCalledWith('course-1', 'dh-1')
    })
  })

  it('edits an existing semester via updateSemester and maps 400/422 server FieldError on AdminSemestersPage', async () => {
    mockUpdateSemester
      .mockRejectedValueOnce({
        response: {
          status: 422,
          data: {
            detail: 'Dữ liệu học kỳ không hợp lệ.',
            errors: {
              Code: ['Mã học kỳ đã tồn tại ở kỳ khác.'],
              EndDate: ['Ngày kết thúc vượt quá giới hạn.'],
            },
          },
        },
      })
      .mockResolvedValueOnce({
        id: 'sem-1',
        code: 'FA26',
        name: 'Fall 2026 Updated',
        startDate: '2026-09-01T00:00:00Z',
        endDate: '2027-01-15T00:00:00Z',
        isActive: true,
        coursesCount: 2,
        classesCount: 4,
        createdAt: '2026-08-01T00:00:00Z',
      })

    render(
      <MemoryRouter>
        <AdminSemestersPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getAllByText('FA26').length).toBeGreaterThan(0))

    fireEvent.click(screen.getAllByRole('button', { name: 'Sửa học kỳ FA26' })[0])
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(screen.getByLabelText(/Mã học kỳ/i)).toHaveValue('FA26')
    expect(screen.getByLabelText(/Tên học kỳ/i)).toHaveValue('Fall 2026')

    fireEvent.change(screen.getByLabelText(/Tên học kỳ/i), {
      target: { value: 'Fall 2026 Updated' },
    })
    fireEvent.change(screen.getByLabelText(/Ngày kết thúc/i), {
      target: { value: '2027-01-15' },
    })

    // First submit triggers 422 field errors
    fireEvent.click(screen.getByRole('button', { name: 'Cập nhật học kỳ' }))
    await waitFor(() => {
      expect(screen.getByText('Mã học kỳ đã tồn tại ở kỳ khác.')).toBeInTheDocument()
      expect(screen.getByText('Ngày kết thúc vượt quá giới hạn.')).toBeInTheDocument()
    })

    // Second submit succeeds
    fireEvent.click(screen.getByRole('button', { name: 'Cập nhật học kỳ' }))
    await waitFor(() => {
      expect(mockUpdateSemester).toHaveBeenLastCalledWith('sem-1', {
        code: 'FA26',
        name: 'Fall 2026 Updated',
        startDate: '2026-09-01',
        endDate: '2027-01-15',
        isActive: true,
      })
      expect(screen.getByRole('status')).toHaveTextContent(
        'Đã cập nhật học kỳ [FA26] thành công.',
      )
    })
  })

  it('validates Course credits/maxMockExamsPerDay bounds and rejects decimal strings like 3.5', async () => {
    render(
      <MemoryRouter>
        <AdminCoursesPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getAllByText('PRN231').length).toBeGreaterThan(0))

    fireEvent.click(screen.getByRole('button', { name: /Thêm môn học mới/i }))
    fireEvent.change(screen.getByLabelText(/^Mã môn$/i), { target: { value: 'SWD392' } })
    fireEvent.change(screen.getByLabelText(/Tên môn học/i), {
      target: { value: 'Software Architecture' },
    })

    // Decimal string '3.5' and '4.9' must fail integer validation
    fireEvent.change(screen.getByLabelText(/Số tín chỉ/i), { target: { value: '3.5' } })
    fireEvent.change(screen.getByLabelText(/Hạn ngạch thi thử mỗi ngày/i), {
      target: { value: '4.9' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Tạo môn học' }))

    expect(screen.getByText('Số tín chỉ phải là số nguyên từ 1 đến 10.')).toBeInTheDocument()
    expect(
      screen.getByText('Hạn ngạch thi thử mỗi ngày (max_mock_exams_per_day) phải từ 1 đến 20 lượt.'),
    ).toBeInTheDocument()
    expect(mockCreateCourse).not.toHaveBeenCalled()

    // Out-of-bounds: credits 11 and maxMockExamsPerDay 21
    fireEvent.change(screen.getByLabelText(/Số tín chỉ/i), { target: { value: '11' } })
    fireEvent.change(screen.getByLabelText(/Hạn ngạch thi thử mỗi ngày/i), {
      target: { value: '21' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Tạo môn học' }))

    expect(screen.getByText('Số tín chỉ phải là số nguyên từ 1 đến 10.')).toBeInTheDocument()
    expect(
      screen.getByText('Hạn ngạch thi thử mỗi ngày (max_mock_exams_per_day) phải từ 1 đến 20 lượt.'),
    ).toBeInTheDocument()
    expect(mockCreateCourse).not.toHaveBeenCalled()
  })

  it('clears course description with empty string on Edit and maps 400/422 server FieldError', async () => {
    mockUpdateCourse
      .mockRejectedValueOnce({
        response: {
          status: 400,
          data: {
            detail: 'Dữ liệu môn học chưa hợp lệ.',
            errors: {
              Code: ['Mã môn học bị trùng.'],
              DepartmentHeadUserId: ['Trưởng bộ môn không hợp lệ.'],
            },
          },
        },
      })
      .mockResolvedValueOnce({
        id: 'course-1',
        code: 'PRN231',
        name: 'Building Cross-Platform Apps',
        credits: 3,
        description: null,
        academicProgramId: 'prog-1',
        departmentHeadUserId: 'dh-1',
        departmentHeadName: 'Trưởng BM CNTT',
        semesterId: 'sem-1',
        semesterCode: 'FA26',
        maxMockExamsPerDay: 3,
        mockExamDurationMinutes: 30,
        mockExamTotalQuestions: 5,
        hasFollowUp: true,
        examInputMode: 'VoiceWithTranscriptEdit',
        isActive: true,
      })

    render(
      <MemoryRouter>
        <AdminCoursesPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getAllByText('PRN231').length).toBeGreaterThan(0))

    fireEvent.click(screen.getByRole('button', { name: 'Sửa môn học PRN231' }))
    expect(screen.getByLabelText(/Mô tả tóm tắt môn học/i)).toHaveValue(
      'Dotnet 8 Clean Architecture',
    )

    // Clear description
    fireEvent.change(screen.getByLabelText(/Mô tả tóm tắt môn học/i), {
      target: { value: '   ' },
    })

    // First submit triggers 400 FieldErrors including departmentHeadUserId
    fireEvent.click(screen.getByRole('button', { name: 'Cập nhật môn học' }))
    await waitFor(() => {
      expect(screen.getByText('Mã môn học bị trùng.')).toBeInTheDocument()
      expect(screen.getByText('Trưởng bộ môn không hợp lệ.')).toBeInTheDocument()
      expect(screen.getByLabelText(/Trưởng bộ môn phụ trách/i)).toHaveAttribute(
        'aria-invalid',
        'true',
      )
    })

    // Second submit succeeds and sends description: "" (NOT undefined)
    fireEvent.click(screen.getByRole('button', { name: 'Cập nhật môn học' }))
    await waitFor(() => {
      expect(mockUpdateCourse).toHaveBeenLastCalledWith(
        'course-1',
        expect.objectContaining({
          description: '',
        }),
      )
    })
  })

  it('surfaces 401/403/429 + Retry on courses, programsProblem, and getDepartmentHeads/getSemesters partial rejections', async () => {
    // 1) Auxiliary partial rejections (heads & semesters fail while courses succeed)
    mockGetDepartmentHeads.mockRejectedValueOnce({
      status: 403,
      detail: 'Không có quyền đọc danh sách Trưởng bộ môn.',
    })
    mockGetSemesters.mockRejectedValueOnce({
      status: 500,
      detail: 'Lỗi máy chủ khi tải học kỳ.',
    })

    const { unmount } = render(
      <MemoryRouter>
        <AdminCoursesPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getAllByText('PRN231').length).toBeGreaterThan(0)
      const alert = screen.getByRole('alert')
      expect(alert).toHaveTextContent('Không có quyền đọc danh sách Trưởng bộ môn.')
      expect(alert).toHaveTextContent('Lỗi máy chủ khi tải học kỳ.')
    })

    // Retry reloads all data cleanly
    mockGetDepartmentHeads.mockResolvedValueOnce([
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
    mockGetSemesters.mockResolvedValueOnce([])
    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }))
    await waitFor(() => {
      expect(screen.queryByRole('alert')).toBeNull()
    })
    unmount()

    // 2) programsProblem isolated while courses succeed, plus courses 401 -> 403 -> 429 + Retry
    mockGetAcademicPrograms.mockRejectedValueOnce({
      status: 429,
      detail: 'Quá tải danh sách chương trình đào tạo.',
    })
    const { unmount: unmount2 } = render(
      <MemoryRouter>
        <AdminCoursesPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getAllByText('PRN231').length).toBeGreaterThan(0)
      expect(screen.getByRole('alert')).toHaveTextContent('Quá tải danh sách chương trình đào tạo.')
      expect(screen.queryByText('Chưa có chương trình đào tạo nào')).toBeNull()
    })
    unmount2()

    mockGetCourses
      .mockRejectedValueOnce({ status: 401, detail: 'Phiên đăng nhập hết hạn.' })
      .mockRejectedValueOnce({ status: 403, detail: 'Không có quyền quản trị môn học.' })
      .mockRejectedValueOnce({ status: 429, detail: 'Giới hạn tần suất gọi API môn học.' })

    render(
      <MemoryRouter>
        <AdminCoursesPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getByRole('link', { name: 'Đăng nhập lại' })).toHaveAttribute('href', '/login')
      expect(screen.queryByText('Chưa có môn học nào trong hệ thống')).toBeNull()
    })

    fireEvent.click(screen.getAllByRole('button', { name: 'Thử lại' })[0])
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Không có quyền quản trị môn học.')
    })

    fireEvent.click(screen.getAllByRole('button', { name: 'Thử lại' })[0])
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Giới hạn tần suất gọi API môn học.')
    })
  })

  it('creates and updates Academic Programs, filters courses by program, and passes academicProgramId when creating/editing courses', async () => {
    const entHead = {
      id: 'dh-ent',
      email: 'headent@fe.edu.vn',
      fullName: 'Trưởng Bộ Môn ENT',
      role: 'department_head',
      isActive: true,
      mustChangePassword: false,
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
    }
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
      entHead,
    ])
    mockGetAcademicPrograms.mockResolvedValue([
      {
        id: 'prog-1',
        code: 'SE',
        name: 'Kỹ thuật phần mềm',
        departmentHeadUserId: 'dh-1',
        departmentHeadName: 'Trưởng BM CNTT',
        isActive: true,
        coursesCount: 1,
      },
      {
        id: 'prog-ent',
        code: 'ENT',
        name: 'Bộ môn Tiếng Anh (ENT)',
        departmentHeadUserId: 'dh-ent',
        departmentHeadName: 'Trưởng Bộ Môn ENT',
        isActive: true,
        coursesCount: 1,
      },
    ])
    mockGetCourses.mockResolvedValue([
      {
        id: 'course-1',
        code: 'PRN231',
        name: 'Building Cross-Platform Apps',
        credits: 3,
        academicProgramId: 'prog-1',
        departmentHeadUserId: 'dh-1',
        departmentHeadName: 'Trưởng BM CNTT',
        semesterId: 'sem-1',
        semesterCode: 'FA26',
        maxMockExamsPerDay: 3,
        mockExamDurationMinutes: 30,
        mockExamTotalQuestions: 5,
        hasFollowUp: true,
        examInputMode: 'VoiceWithTranscriptEdit',
        isActive: true,
      },
      {
        id: 'course-ent',
        code: 'ENT104',
        name: 'English 104',
        credits: 3,
        academicProgramId: 'prog-ent',
        departmentHeadUserId: 'dh-ent',
        departmentHeadName: 'Trưởng Bộ Môn ENT',
        semesterId: 'sem-1',
        semesterCode: 'FA26',
        maxMockExamsPerDay: 3,
        mockExamDurationMinutes: 30,
        mockExamTotalQuestions: 5,
        hasFollowUp: false,
        examInputMode: 'VoiceWithTranscriptEdit',
        isActive: true,
      },
    ])
    mockCreateAcademicProgram.mockResolvedValue({
      id: 'prog-ai',
      code: 'AI',
      name: 'Trí tuệ nhân tạo',
      departmentHeadUserId: 'dh-ent',
      departmentHeadName: 'Trưởng Bộ Môn ENT',
      isActive: true,
      coursesCount: 0,
    })
    mockUpdateAcademicProgram.mockResolvedValue({
      id: 'prog-ent',
      code: 'ENT',
      name: 'Tiếng Anh Chuyên Ngành',
      departmentHeadUserId: null,
      departmentHeadName: null,
      isActive: true,
      coursesCount: 1,
    })
    mockCreateCourse.mockResolvedValue({
      id: 'course-ent-new',
      code: 'ENT601',
      name: 'Academic Writing',
      credits: 3,
      academicProgramId: 'prog-ent',
      departmentHeadUserId: 'dh-ent',
      departmentHeadName: 'Trưởng Bộ Môn ENT',
      semesterId: 'sem-1',
      maxMockExamsPerDay: 3,
      mockExamDurationMinutes: 30,
      mockExamTotalQuestions: 5,
      hasFollowUp: false,
      examInputMode: 'VoiceWithTranscriptEdit',
      isActive: true,
    })

    render(
      <MemoryRouter initialEntries={['/admin/courses?program=prog-ent']}>
        <AdminCoursesPage />
      </MemoryRouter>,
    )

    // URL program filter isolates ENT104 and hides PRN231
    await waitFor(() => {
      expect(screen.getAllByText('ENT104').length).toBeGreaterThan(0)
      expect(screen.queryByText('PRN231')).toBeNull()
    })

    // 1. Create new Academic Program (validates invalid code first, then submits uppercase)
    fireEvent.click(screen.getByRole('button', { name: /Thêm chương trình ĐT/i }))
    fireEvent.change(screen.getByLabelText(/Mã chương trình ĐT/i), { target: { value: 'A!' } })
    fireEvent.change(screen.getByLabelText(/Tên chương trình đào tạo/i), {
      target: { value: 'Trí tuệ nhân tạo' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Tạo chương trình ĐT' }))
    expect(mockCreateAcademicProgram).not.toHaveBeenCalled()

    fireEvent.change(screen.getByLabelText(/Mã chương trình ĐT/i), { target: { value: 'ai' } })
    fireEvent.change(screen.getByLabelText(/Trưởng bộ môn phụ trách ngành/i), {
      target: { value: 'dh-ent' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Tạo chương trình ĐT' }))
    await waitFor(() => {
      expect(mockCreateAcademicProgram).toHaveBeenCalledWith({
        code: 'AI',
        name: 'Trí tuệ nhân tạo',
        departmentHeadUserId: 'dh-ent',
        isActive: true,
      })
    })

    // 2. Edit existing Academic Program and unassign head via Guid.Empty
    fireEvent.click(screen.getByRole('button', { name: 'Sửa chương trình ĐT ENT' }))
    fireEvent.change(screen.getByLabelText(/Tên chương trình đào tạo/i), {
      target: { value: 'Tiếng Anh Chuyên Ngành' },
    })
    fireEvent.change(screen.getByLabelText(/Trưởng bộ môn phụ trách ngành/i), {
      target: { value: '' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Cập nhật chương trình ĐT' }))
    await waitFor(() => {
      expect(mockUpdateAcademicProgram).toHaveBeenCalledWith('prog-ent', {
        code: 'ENT',
        name: 'Tiếng Anh Chuyên Ngành',
        departmentHeadUserId: '00000000-0000-0000-0000-000000000000',
        isActive: true,
      })
    })

    // 3. Create Course while filtered to ENT -> preselects ENT and inherits headent (departmentHeadUserId: undefined)
    fireEvent.click(screen.getByRole('button', { name: /Thêm môn học mới/i }))
    expect(screen.getByLabelText(/^Chương trình đào tạo$/i)).toHaveValue('prog-ent')
    fireEvent.change(screen.getByLabelText(/^Mã môn$/i), { target: { value: 'ent601' } })
    fireEvent.change(screen.getByLabelText(/Tên môn học/i), {
      target: { value: 'Academic Writing' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Tạo môn học' }))
    await waitFor(() => {
      expect(mockCreateCourse).toHaveBeenCalledWith(
        expect.objectContaining({
          code: 'ENT601',
          academicProgramId: 'prog-ent',
          departmentHeadUserId: undefined,
        }),
      )
    })
  })

  it('handles 409 conflict on Program modal, skips inactive program prefill on Create Course, allows unassigning program on Edit Course, and preserves missing/inactive head option', async () => {
    mockGetDepartmentHeads.mockResolvedValue([
      {
        id: 'dh-active',
        email: 'active@fpt.edu.vn',
        fullName: 'Trưởng BM Active',
        role: 'department_head',
        isActive: true,
        mustChangePassword: false,
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
      },
    ])
    mockGetAcademicPrograms.mockResolvedValue([
      {
        id: 'prog-inactive',
        code: 'OLD',
        name: 'Ngành cũ tạm khóa',
        departmentHeadUserId: 'dh-locked',
        departmentHeadName: 'Trưởng BM Đã Khóa',
        isActive: false,
        coursesCount: 1,
      },
    ])
    mockGetCourses.mockResolvedValue([
      {
        id: 'course-old',
        code: 'OLD101',
        name: 'Legacy Course',
        credits: 3,
        academicProgramId: 'prog-inactive',
        departmentHeadUserId: 'dh-locked',
        departmentHeadName: 'Trưởng BM Đã Khóa',
        semesterId: 'sem-1',
        semesterCode: 'FA26',
        maxMockExamsPerDay: 3,
        mockExamDurationMinutes: 30,
        mockExamTotalQuestions: 5,
        hasFollowUp: false,
        examInputMode: 'VoiceWithTranscriptEdit',
        isActive: true,
      },
    ])
    mockCreateAcademicProgram.mockRejectedValueOnce({
      status: 409,
      detail: "CONFLICT: Mã chương trình đào tạo 'OLD' đã tồn tại trong hệ thống.",
    })
    mockUpdateCourse.mockResolvedValue({
      id: 'course-old',
      code: 'OLD101',
      name: 'Legacy Course',
      credits: 3,
      academicProgramId: null,
      departmentHeadUserId: null,
      departmentHeadName: null,
      semesterId: 'sem-1',
      maxMockExamsPerDay: 3,
      mockExamDurationMinutes: 30,
      mockExamTotalQuestions: 5,
      hasFollowUp: false,
      examInputMode: 'VoiceWithTranscriptEdit',
      isActive: true,
    })

    render(
      <MemoryRouter initialEntries={['/admin/courses?program=prog-inactive']}>
        <AdminCoursesPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getAllByText('OLD101').length).toBeGreaterThan(0))

    // 1. Create Program surfaces 409 Conflict alert in dialog
    fireEvent.click(screen.getByRole('button', { name: /Thêm chương trình ĐT/i }))
    fireEvent.change(screen.getByLabelText(/Mã chương trình ĐT/i), { target: { value: 'OLD' } })
    fireEvent.change(screen.getByLabelText(/Tên chương trình đào tạo/i), { target: { value: 'Trùng ngành cũ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Tạo chương trình ĐT' }))
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent("Mã chương trình đào tạo 'OLD' đã tồn tại")
    })
    fireEvent.click(screen.getByRole('button', { name: 'Hủy' }))

    // 2. Edit Program with locked head renders fallback <option> so select matches state and can be cleared
    fireEvent.click(screen.getByRole('button', { name: 'Sửa chương trình ĐT OLD' }))
    expect(screen.getByLabelText(/Trưởng bộ môn phụ trách ngành/i)).toHaveValue('dh-locked')
    fireEvent.click(screen.getByRole('button', { name: 'Hủy' }))

    // 3. Create Course while filtered to inactive program does NOT prefill inactive program
    fireEvent.click(screen.getByRole('button', { name: /Thêm môn học mới/i }))
    expect(screen.getByLabelText(/^Chương trình đào tạo$/i)).toHaveValue('')
    fireEvent.click(screen.getByRole('button', { name: 'Hủy' }))

    // 4. Edit Course preserves locked head option and allows unassigning both program and head via EMPTY_GUID
    fireEvent.click(screen.getByRole('button', { name: 'Sửa môn học OLD101' }))
    expect(screen.getByLabelText(/Trưởng bộ môn phụ trách/i)).toHaveValue('dh-locked')
    expect(screen.getByLabelText(/^Chương trình đào tạo$/i)).toHaveValue('prog-inactive')
    fireEvent.change(screen.getByLabelText(/Trưởng bộ môn phụ trách/i), { target: { value: '' } })
    fireEvent.change(screen.getByLabelText(/^Chương trình đào tạo$/i), { target: { value: '' } })
    fireEvent.click(screen.getByRole('button', { name: 'Cập nhật môn học' }))

    await waitFor(() => {
      expect(mockUpdateCourse).toHaveBeenCalledWith(
        'course-old',
        expect.objectContaining({
          academicProgramId: '00000000-0000-0000-0000-000000000000',
          departmentHeadUserId: '00000000-0000-0000-0000-000000000000',
        }),
      )
    })
  })
})
