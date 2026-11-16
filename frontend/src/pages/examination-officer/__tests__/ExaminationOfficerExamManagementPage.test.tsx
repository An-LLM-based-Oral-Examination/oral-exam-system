import { MemoryRouter } from 'react-router-dom'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ExaminationOfficerExamManagementPage } from '../ExaminationOfficerExamManagementPage'
import { classService } from '@/services/class.service'
import { courseService } from '@/services/course.service'
import { officialExamService } from '@/services/officialExam.service'

vi.mock('@/services/class.service', () => ({
  classService: {
    listSemesters: vi.fn(),
    listSemesterCourseOfferings: vi.fn(),
  },
}))

vi.mock('@/services/course.service', () => ({
  courseService: {
    getConfiguration: vi.fn(),
    getCourseLecturers: vi.fn(),
    getShiftStaffCandidates: vi.fn(),
  },
}))

vi.mock('@/services/officialExam.service', () => ({
  officialExamService: {
    getSessions: vi.fn(),
    getShifts: vi.fn(),
    getExamStructures: vi.fn(),
    createSession: vi.fn(),
    createShift: vi.fn(),
    updateShift: vi.fn(),
  },
}))

const listSemesters = vi.mocked(classService.listSemesters)
const listSemesterCourseOfferings = vi.mocked(classService.listSemesterCourseOfferings)
const getConfiguration = vi.mocked(courseService.getConfiguration)
const getCourseLecturers = vi.mocked(courseService.getCourseLecturers)
const getShiftStaffCandidates = vi.mocked(courseService.getShiftStaffCandidates)
const getSessions = vi.mocked(officialExamService.getSessions)
const getShifts = vi.mocked(officialExamService.getShifts)
const getExamStructures = vi.mocked(officialExamService.getExamStructures)
const createSession = vi.mocked(officialExamService.createSession)
const createShift = vi.mocked(officialExamService.createShift)
const updateShift = vi.mocked(officialExamService.updateShift)

const semester = {
  id: 'semester-1',
  code: 'FA26',
  name: 'Fall 2026',
  startDate: '2026-09-01',
  endDate: '2026-12-31',
  isActive: true,
}

const offering = {
  courseOfferingId: 'offering-1',
  courseId: 'course-1',
  courseCode: 'PRN231',
  courseName: 'Building Cross-Platform Back-End Applications',
  description: null,
  credits: 3,
  academicProgramId: null,
  academicProgramCode: null,
  isCourseActive: true,
  isSelected: true,
}

const existingSession = {
  id: 'session-1',
  courseId: 'course-1',
  examStructureId: 'structure-1',
  title: 'PRN231 · Fall 2026 · Thi thật',
  examDate: '2026-11-20',
  hasFollowUp: true,
  maxFollowUpQuestions: 2,
  examInputMode: 'VoiceWithTranscriptEdit',
  transcriptBufferSeconds: 60,
  status: 'SCHEDULED',
}

const courseLecturers = [
  {
    lecturerId: 'proctor-1',
    fullName: 'Nguyễn Giám thị',
    email: 'proctor@example.com',
    studentCode: 'GV001',
    isActive: true,
    semesterId: 'semester-1',
    semesterCode: 'FA26',
    courseOfferingId: 'offering-1',
  },
  {
    lecturerId: 'examiner-1',
    fullName: 'Nguyễn Giám khảo',
    email: 'examiner@example.com',
    studentCode: 'GV002',
    isActive: true,
    semesterId: 'semester-1',
    semesterCode: 'FA26',
    courseOfferingId: 'offering-1',
  },
  {
    lecturerId: 'other-semester-lecturer',
    fullName: 'Giảng viên học kỳ khác',
    email: 'other-semester@example.com',
    studentCode: 'GV003',
    isActive: true,
    semesterId: 'semester-2',
    semesterCode: 'SU26',
    courseOfferingId: 'offering-2',
  },
]

describe('ExaminationOfficerExamManagementPage', () => {
  const openCreateDialog = async () => {
    fireEvent.click(screen.getByRole('button', { name: 'Tạo ca thi' }))
    await waitFor(() => expect(screen.getByRole('dialog', { name: 'Tạo ca thi' })).toBeInTheDocument())
  }

  beforeEach(() => {
    vi.clearAllMocks()
    listSemesters.mockResolvedValue([semester])
    listSemesterCourseOfferings.mockResolvedValue([offering])
    getSessions.mockResolvedValue([existingSession])
    getShifts.mockResolvedValue([])
    getExamStructures.mockResolvedValue([])
    getCourseLecturers.mockResolvedValue(courseLecturers)
    getShiftStaffCandidates.mockResolvedValue({
      proctors: [],
      examiners: [],
      departmentHeadIds: [],
    })
    getConfiguration.mockResolvedValue({
      courseId: 'course-1',
      courseCode: 'PRN231',
      courseName: offering.courseName,
      transcriptBufferSeconds: 60,
      maxFollowUpQuestions: 2,
      hasFollowUp: true,
      examInputMode: 'VoiceWithTranscriptEdit',
      isActive: true,
    })
    createSession.mockResolvedValue(existingSession)
    createShift.mockResolvedValue({
      id: 'shift-1',
      sessionId: 'session-1',
      shiftName: 'Ca 1',
      roomLab: 'Lab 301',
      startTime: '2026-11-20T08:00',
      endTime: '2026-11-20T09:30',
      proctorUserId: 'proctor-1',
      examinerUserId: 'examiner-1',
      examinerName: 'Examiner',
      proctorName: 'Proctor',
      status: 'SCHEDULED',
    })
  })

  it('loads the academic semester and the selected course offerings', async () => {
    render(
      <MemoryRouter>
        <ExaminationOfficerExamManagementPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getByRole('button', { name: 'Tạo ca thi' })).toBeInTheDocument())
    expect(screen.queryByLabelText(/Học kỳ/)).not.toBeInTheDocument()
    await openCreateDialog()
    await waitFor(() => expect(screen.getByLabelText(/Học kỳ/)).toHaveValue('semester-1'))
    expect(screen.getByLabelText(/Môn thi trong học kỳ/)).toHaveValue('')
    expect(listSemesters).toHaveBeenCalledWith()
    expect(listSemesterCourseOfferings).toHaveBeenCalledWith('semester-1')
    expect(screen.queryByText('Tạo kỳ thi')).not.toBeInTheDocument()
  })

  it('opens the create-shift dialog and closes it without submitting', async () => {
    render(
      <MemoryRouter>
        <ExaminationOfficerExamManagementPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getByRole('button', { name: 'Tạo ca thi' })).toBeInTheDocument())
    await openCreateDialog()
    fireEvent.click(screen.getByRole('button', { name: 'Hủy' }))

    expect(screen.queryByRole('dialog', { name: 'Tạo ca thi' })).not.toBeInTheDocument()
    expect(createShift).not.toHaveBeenCalled()
  })

  it('resets the course when the examination officer changes semester', async () => {
    listSemesters.mockResolvedValueOnce([
      semester,
      { ...semester, id: 'semester-2', code: 'SU26', name: 'Summer 2026' },
    ])
    listSemesterCourseOfferings.mockResolvedValueOnce([offering]).mockResolvedValueOnce([])

    render(
      <MemoryRouter>
        <ExaminationOfficerExamManagementPage />
      </MemoryRouter>,
    )

    await openCreateDialog()
    await waitFor(() => expect(screen.getByLabelText(/Học kỳ/)).toHaveValue('semester-1'))
    fireEvent.change(screen.getByLabelText(/Học kỳ/), { target: { value: 'semester-2' } })

    await waitFor(() => expect(screen.getByLabelText(/Môn thi trong học kỳ/)).toHaveValue(''))
    expect(listSemesterCourseOfferings).toHaveBeenLastCalledWith('semester-2')
  })

  it('loads eligible lecturers for the selected course and semester', async () => {
    render(
      <MemoryRouter>
        <ExaminationOfficerExamManagementPage />
      </MemoryRouter>,
    )

    await openCreateDialog()
    await waitFor(() => expect(screen.getByLabelText(/Môn thi trong học kỳ/)).toBeEnabled())
    fireEvent.change(screen.getByLabelText(/Môn thi trong học kỳ/), {
      target: { value: 'course-1' },
    })

    await waitFor(() => expect(getCourseLecturers).toHaveBeenCalledWith('course-1'))
    expect(screen.getAllByRole('option', { name: /Nguyễn Giám thị/ })).not.toHaveLength(0)
    expect(screen.getAllByRole('option', { name: /Nguyễn Giám khảo/ })).not.toHaveLength(0)
    expect(screen.queryByRole('option', { name: /Giảng viên học kỳ khác/ })).not.toBeInTheDocument()
  })

  it('falls back to accessible examination-officer staff endpoints when /courses/{id}/lecturers returns 403', async () => {
    getCourseLecturers.mockRejectedValueOnce({
      status: 403,
      detail: 'FORBIDDEN: Không có quyền xem phân công của môn học này.',
    })
    getShifts.mockResolvedValueOnce([
      {
        id: 'existing-shift-1',
        sessionId: 'session-1',
        shiftName: 'Ca mẫu',
        roomLab: 'Lab 201',
        startTime: '2026-11-19T08:00:00.000Z',
        endTime: '2026-11-19T09:30:00.000Z',
        proctorUserId: 'pure-proctor-1',
        examinerUserId: 'lecturer-2',
        proctorName: 'Nguyen Dang Hai',
        examinerName: 'Lê Thu Hà',
        status: 'SCHEDULED',
      },
    ])
    getShiftStaffCandidates.mockResolvedValueOnce({
      proctors: [
        {
          userId: 'lecturer-1',
          fullName: 'Nguyen Trong Tot',
          detail: 'Giảng viên phụ trách môn',
        },
      ],
      examiners: [
        {
          userId: 'lecturer-1',
          fullName: 'Nguyen Trong Tot',
          detail: 'Giảng viên phụ trách môn',
        },
        {
          userId: 'head-1',
          fullName: 'Tran Van Truong',
          detail: 'Trưởng bộ môn SE',
        },
      ],
      departmentHeadIds: ['head-1'],
    })

    render(
      <MemoryRouter>
        <ExaminationOfficerExamManagementPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getByRole('button', { name: 'Tạo ca thi' })).toBeInTheDocument())
    await openCreateDialog()
    await waitFor(() => expect(screen.getByLabelText(/Môn thi trong học kỳ/)).toBeEnabled())
    fireEvent.change(screen.getByLabelText(/Môn thi trong học kỳ/), {
      target: { value: 'course-1' },
    })

    await waitFor(() =>
      expect(getShiftStaffCandidates).toHaveBeenCalledWith({
        courseId: 'course-1',
        semesterId: 'semester-1',
        academicProgramId: null,
      }),
    )

    const proctorSelect = screen.getByLabelText(/Giám thị/)
    const examinerSelect = screen.getByLabelText(/Giám khảo/)
    await waitFor(() => expect(proctorSelect).toBeEnabled())
    expect(examinerSelect).toBeEnabled()
    expect(
      screen.queryByText(/Không có quyền xem phân công của môn học này/),
    ).not.toBeInTheDocument()

    // Trưởng bộ môn chỉ được phép làm Giám khảo, không xuất hiện trong Giám thị; Giám thị thuần chỉ nằm ở Giám thị
    expect(proctorSelect.textContent).toContain('Nguyen Dang Hai')
    expect(proctorSelect.textContent).toContain('Nguyen Trong Tot')
    expect(proctorSelect.textContent).not.toContain('Tran Van Truong')

    expect(examinerSelect.textContent).toContain('Nguyen Trong Tot')
    expect(examinerSelect.textContent).toContain('Lê Thu Hà')
    expect(examinerSelect.textContent).toContain('Tran Van Truong')
    expect(examinerSelect.textContent).not.toContain('Nguyen Dang Hai')

    fireEvent.change(screen.getByLabelText(/Tên ca/), { target: { value: 'Ca 2' } })
    fireEvent.change(screen.getByLabelText(/Phòng Lab/), { target: { value: 'Lab 302' } })
    fireEvent.change(screen.getByLabelText(/Bắt đầu/), { target: { value: '2026-11-20T13:00' } })
    fireEvent.change(screen.getByLabelText(/Kết thúc/), { target: { value: '2026-11-20T14:30' } })
    fireEvent.change(proctorSelect, { target: { value: 'pure-proctor-1' } })
    fireEvent.change(examinerSelect, { target: { value: 'head-1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Tạo ca thi cho PRN231' }))

    await waitFor(() =>
      expect(createShift).toHaveBeenCalledWith(
        expect.objectContaining({
          sessionId: 'session-1',
          proctorUserId: 'pure-proctor-1',
          examinerUserId: 'head-1',
          maxSeats: 24,
        }),
      ),
    )
  })

  it('reuses the matching technical session before creating a shift', async () => {
    render(
      <MemoryRouter>
        <ExaminationOfficerExamManagementPage />
      </MemoryRouter>,
    )

    await openCreateDialog()
    await waitFor(() => expect(screen.getByLabelText(/Môn thi trong học kỳ/)).toBeEnabled())
    fireEvent.change(screen.getByLabelText(/Môn thi trong học kỳ/), {
      target: { value: 'course-1' },
    })
    fireEvent.change(screen.getByLabelText(/Tên ca/), { target: { value: 'Ca 1' } })
    fireEvent.change(screen.getByLabelText(/Phòng Lab/), { target: { value: 'Lab 301' } })
    fireEvent.change(screen.getByLabelText(/Bắt đầu/), { target: { value: '2026-11-20T08:00' } })
    fireEvent.change(screen.getByLabelText(/Kết thúc/), { target: { value: '2026-11-20T09:30' } })
    await waitFor(() =>
      expect(screen.getAllByRole('option', { name: /Nguyễn Giám thị/ })).not.toHaveLength(0),
    )
    fireEvent.change(screen.getByLabelText(/Giám thị/), { target: { value: 'proctor-1' } })
    fireEvent.change(screen.getByLabelText(/Giám khảo/), { target: { value: 'examiner-1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Tạo ca thi cho PRN231' }))

    await waitFor(() =>
      expect(createShift).toHaveBeenCalledWith(
        expect.objectContaining({
          sessionId: 'session-1',
          examinerUserId: 'examiner-1',
          startTime: new Date('2026-11-20T08:00').toISOString(),
          endTime: new Date('2026-11-20T09:30').toISOString(),
        }),
      ),
    )
    expect(screen.getByRole('dialog', { name: 'Tạo ca thi thành công' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Đã hiểu' })).toBeInTheDocument()
    expect(createSession).not.toHaveBeenCalled()
  })

  it('creates a technical session from the backend course configuration when no matching session exists', async () => {
    getSessions.mockResolvedValue([])

    render(
      <MemoryRouter>
        <ExaminationOfficerExamManagementPage />
      </MemoryRouter>,
    )

    await openCreateDialog()
    await waitFor(() => expect(screen.getByLabelText(/Môn thi trong học kỳ/)).toBeEnabled())
    fireEvent.change(screen.getByLabelText(/Môn thi trong học kỳ/), {
      target: { value: 'course-1' },
    })
    fireEvent.change(screen.getByLabelText(/Tên ca/), { target: { value: 'Ca 1' } })
    fireEvent.change(screen.getByLabelText(/Phòng Lab/), { target: { value: 'Lab 301' } })
    fireEvent.change(screen.getByLabelText(/Bắt đầu/), { target: { value: '2026-11-20T08:00' } })
    fireEvent.change(screen.getByLabelText(/Kết thúc/), { target: { value: '2026-11-20T09:30' } })
    await waitFor(() =>
      expect(screen.getAllByRole('option', { name: /Nguyễn Giám thị/ })).not.toHaveLength(0),
    )
    fireEvent.change(screen.getByLabelText(/Giám thị/), { target: { value: 'proctor-1' } })
    fireEvent.change(screen.getByLabelText(/Giám khảo/), { target: { value: 'examiner-1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Tạo ca thi cho PRN231' }))

    await waitFor(() =>
      expect(createSession).toHaveBeenCalledWith(
        expect.objectContaining({
          courseId: 'course-1',
          examStructureId: null,
          examDate: '2026-11-20',
        }),
      ),
    )
    expect(createShift).toHaveBeenCalledWith(
      expect.objectContaining({
        sessionId: 'session-1',
        maxSeats: 24,
      }),
    )
  })

  it('rejects 0, negative, decimal, or > 40 capacity values when creating a shift', async () => {
    render(
      <MemoryRouter>
        <ExaminationOfficerExamManagementPage />
      </MemoryRouter>,
    )

    await openCreateDialog()
    await waitFor(() => expect(screen.getByLabelText(/Môn thi trong học kỳ/)).toBeEnabled())
    fireEvent.change(screen.getByLabelText(/Môn thi trong học kỳ/), {
      target: { value: 'course-1' },
    })
    fireEvent.change(screen.getByLabelText(/Tên ca/), { target: { value: 'Ca 1' } })
    fireEvent.change(screen.getByLabelText(/Phòng Lab/), { target: { value: 'Lab 301' } })
    fireEvent.change(screen.getByLabelText(/Bắt đầu/), { target: { value: '2026-11-20T08:00' } })
    fireEvent.change(screen.getByLabelText(/Kết thúc/), { target: { value: '2026-11-20T09:30' } })
    await waitFor(() =>
      expect(screen.getAllByRole('option', { name: /Nguyễn Giám thị/ })).not.toHaveLength(0),
    )
    fireEvent.change(screen.getByLabelText(/Giám thị/), { target: { value: 'proctor-1' } })
    fireEvent.change(screen.getByLabelText(/Giám khảo/), { target: { value: 'examiner-1' } })

    const maxSeatsInput = screen.getByLabelText(/Sức chứa tối đa \(maxSeats\)/)
    for (const invalidValue of ['0', '-5', '12.5', '41']) {
      fireEvent.change(maxSeatsInput, { target: { value: invalidValue } })
      fireEvent.click(screen.getByRole('button', { name: 'Tạo ca thi cho PRN231' }))
      expect(createShift).not.toHaveBeenCalled()
    }
  })

  it('displays shift capacity metrics from API and shows exact message for SHIFT_CAPACITY_BELOW_ALLOCATION on update', async () => {
    getShifts.mockResolvedValue([
      {
        id: 'shift-30',
        sessionId: 'session-1',
        shiftName: 'Ca 30 ghế',
        roomLab: 'Lab 305',
        startTime: '2026-11-20T08:00:00.000Z',
        endTime: '2026-11-20T09:30:00.000Z',
        proctorUserId: 'proctor-1',
        examinerUserId: 'examiner-1',
        examinerName: 'Nguyễn Giám khảo',
        proctorName: 'Nguyễn Giám thị',
        status: 'SCHEDULED',
        maxSeats: 30,
        assignedTicketsCount: 10,
        remainingSeats: 20,
      },
    ])
    updateShift.mockRejectedValueOnce({
      status: 409,
      code: 'SHIFT_CAPACITY_BELOW_ALLOCATION',
      detail: 'Sức chứa mới không được thấp hơn số vé hoặc số ghế cao nhất đã cấp.',
    })

    render(
      <MemoryRouter>
        <ExaminationOfficerExamManagementPage />
      </MemoryRouter>,
    )

    expect((await screen.findAllByText(/Sức chứa tối đa: 30/)).length).toBeGreaterThan(0)
    expect(screen.getByText(/Đã phát: 10 · Còn lại: 20/)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Cập nhật ca' }))
    const dialog = await screen.findByRole('dialog', { name: 'Cập nhật ca thi' })
    expect(dialog).toBeInTheDocument()

    const maxSeatsInput = screen.getByLabelText(/Sức chứa tối đa \(maxSeats\)/)
    expect(maxSeatsInput).toHaveValue(30)

    fireEvent.change(maxSeatsInput, { target: { value: '15' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu cập nhật ca thi' }))

    await waitFor(() =>
      expect(updateShift).toHaveBeenCalledWith(
        'shift-30',
        expect.objectContaining({ maxSeats: 15 }),
      ),
    )
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Không thể giảm sức chứa thấp hơn số vé hoặc số ghế cao nhất đã cấp.',
    )
  })
})
