import { MemoryRouter } from 'react-router-dom'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
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
    batchOpenShifts: vi.fn(),
    previewCandidateImport: vi.fn(),
    commitCandidateImport: vi.fn(),
    autoAllocateStudents: vi.fn(),
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
const batchOpenShifts = vi.mocked(officialExamService.batchOpenShifts)
const previewCandidateImport = vi.mocked(officialExamService.previewCandidateImport)
const commitCandidateImport = vi.mocked(officialExamService.commitCandidateImport)
const autoAllocateStudents = vi.mocked(officialExamService.autoAllocateStudents)

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

  const goToStep2 = () => {
    fireEvent.click(
      within(screen.getByRole('dialog', { name: 'Tạo ca thi' })).getByRole('button', {
        name: /Tiếp tục: Cấu hình ca & phòng/i,
      }),
    )
  }

  const goToStep3AndSubmit = () => {
    const dialog = screen.getByRole('dialog', { name: 'Tạo ca thi' })
    fireEvent.click(within(dialog).getByRole('button', { name: /Tiếp tục: Xem lại/i }))
    fireEvent.click(within(dialog).getByRole('button', { name: 'Tạo ca thi' }))
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
    batchOpenShifts.mockResolvedValue({
      sessionId: 'session-1',
      shiftGroupName: 'Ca 1',
      createdRoomCount: 2,
      totalCapacityCreated: 51,
      shifts: [],
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
    goToStep2()
    await waitFor(() =>
      expect(screen.getAllByRole('option', { name: /Nguyễn Giám thị/ })).not.toHaveLength(0),
    )
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

    goToStep2()
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
    goToStep3AndSubmit()

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
    goToStep2()
    fireEvent.change(screen.getByLabelText(/Tên ca/), { target: { value: 'Ca 1' } })
    fireEvent.change(screen.getByLabelText(/Phòng Lab/), { target: { value: 'Lab 301' } })
    fireEvent.change(screen.getByLabelText(/Bắt đầu/), { target: { value: '2026-11-20T08:00' } })
    fireEvent.change(screen.getByLabelText(/Kết thúc/), { target: { value: '2026-11-20T09:30' } })
    await waitFor(() =>
      expect(screen.getAllByRole('option', { name: /Nguyễn Giám thị/ })).not.toHaveLength(0),
    )
    fireEvent.change(screen.getByLabelText(/Giám thị/), { target: { value: 'proctor-1' } })
    fireEvent.change(screen.getByLabelText(/Giám khảo/), { target: { value: 'examiner-1' } })
    goToStep3AndSubmit()

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
    goToStep2()
    fireEvent.change(screen.getByLabelText(/Tên ca/), { target: { value: 'Ca 1' } })
    fireEvent.change(screen.getByLabelText(/Phòng Lab/), { target: { value: 'Lab 301' } })
    fireEvent.change(screen.getByLabelText(/Bắt đầu/), { target: { value: '2026-11-20T08:00' } })
    fireEvent.change(screen.getByLabelText(/Kết thúc/), { target: { value: '2026-11-20T09:30' } })
    await waitFor(() =>
      expect(screen.getAllByRole('option', { name: /Nguyễn Giám thị/ })).not.toHaveLength(0),
    )
    fireEvent.change(screen.getByLabelText(/Giám thị/), { target: { value: 'proctor-1' } })
    fireEvent.change(screen.getByLabelText(/Giám khảo/), { target: { value: 'examiner-1' } })
    goToStep3AndSubmit()

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
    goToStep2()
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
      fireEvent.click(
        within(screen.getByRole('dialog', { name: 'Tạo ca thi' })).getByRole('button', {
          name: /Tiếp tục: Xem lại/i,
        }),
      )
      expect(createShift).not.toHaveBeenCalled()
    }
  })

  it('displays unified FE/RE exam session cards with opened room counts and lab names', async () => {
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

    render(
      <MemoryRouter>
        <ExaminationOfficerExamManagementPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText('PRN231 · Fall 2026 · Thi thật')).toBeInTheDocument()
    expect(screen.getByText('1 phòng')).toBeInTheDocument()
    expect(screen.getByText(/Phòng Lab: Lab 305/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Chi tiết/ })).toHaveAttribute(
      'href',
      '/examination-officer/exams/session-1',
    )
  })

  it('creates multiple rooms in one shift via batchOpenShifts when adding a second room', async () => {
    getCourseLecturers.mockResolvedValueOnce([
      ...courseLecturers,
      {
        lecturerId: 'proctor-2',
        fullName: 'Trần Giám thị 2',
        email: 'proctor2@example.com',
        studentCode: 'GV004',
        isActive: true,
        semesterId: 'semester-1',
        semesterCode: 'FA26',
        courseOfferingId: 'offering-1',
      },
      {
        lecturerId: 'examiner-2',
        fullName: 'Trần Giám khảo 2',
        email: 'examiner2@example.com',
        studentCode: 'GV005',
        isActive: true,
        semesterId: 'semester-1',
        semesterCode: 'FA26',
        courseOfferingId: 'offering-1',
      },
    ])

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
    goToStep2()
    await waitFor(() =>
      expect(screen.getAllByRole('option', { name: /Nguyễn Giám thị/ })).not.toHaveLength(0),
    )

    fireEvent.change(screen.getByLabelText(/Tên ca/), { target: { value: 'Ca 1' } })
    fireEvent.change(screen.getByLabelText(/Bắt đầu/), { target: { value: '2026-11-20T08:00' } })
    fireEvent.change(screen.getByLabelText(/Kết thúc/), { target: { value: '2026-11-20T09:30' } })

    fireEvent.click(screen.getByRole('button', { name: /Thêm phòng/i }))

    const labInputs = screen.getAllByLabelText(/Phòng Lab/)
    const seatInputs = screen.getAllByLabelText(/Sức chứa tối đa/)
    const proctorSelects = screen.getAllByLabelText(/Giám thị/)
    const examinerSelects = screen.getAllByLabelText(/Giám khảo/)

    expect(labInputs).toHaveLength(2)

    fireEvent.change(labInputs[0], { target: { value: 'AL-R201' } })
    fireEvent.change(seatInputs[0], { target: { value: '24' } })
    fireEvent.change(proctorSelects[0], { target: { value: 'proctor-1' } })
    fireEvent.change(examinerSelects[0], { target: { value: 'examiner-1' } })

    fireEvent.change(labInputs[1], { target: { value: 'AL-R202' } })
    fireEvent.change(seatInputs[1], { target: { value: '24' } })
    fireEvent.change(proctorSelects[1], { target: { value: 'proctor-2' } })
    fireEvent.change(examinerSelects[1], { target: { value: 'examiner-2' } })

    goToStep3AndSubmit()

    await waitFor(() =>
      expect(batchOpenShifts).toHaveBeenCalledWith('session-1', {
        shiftGroupName: 'Ca 1',
        startTime: new Date('2026-11-20T08:00').toISOString(),
        endTime: new Date('2026-11-20T09:30').toISOString(),
        rooms: [
          {
            roomLab: 'AL-R201',
            maxSeats: 24,
            proctorUserId: 'proctor-1',
            examinerUserId: 'examiner-1',
          },
          {
            roomLab: 'AL-R202',
            maxSeats: 24,
            proctorUserId: 'proctor-2',
            examinerUserId: 'examiner-2',
          },
        ],
      }),
    )
    expect(createShift).not.toHaveBeenCalled()
  })

  it('creates multiple shifts in one submission and supports copying rooms/staff from Ca 1', async () => {
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
    goToStep2()
    await waitFor(() =>
      expect(screen.getAllByRole('option', { name: /Nguyễn Giám thị/ })).not.toHaveLength(0),
    )

    // Điền Ca 1
    fireEvent.change(screen.getByLabelText(/Tên ca/), { target: { value: 'Ca 1' } })
    fireEvent.change(screen.getByLabelText(/Bắt đầu/), { target: { value: '2026-11-20T08:00' } })
    fireEvent.change(screen.getByLabelText(/Kết thúc/), { target: { value: '2026-11-20T09:00' } })
    fireEvent.change(screen.getByLabelText(/Phòng Lab/), { target: { value: 'Lab 402' } })
    fireEvent.change(screen.getByLabelText(/Giám thị/), { target: { value: 'proctor-1' } })
    fireEvent.change(screen.getByLabelText(/Giám khảo/), { target: { value: 'examiner-1' } })

    // Thêm Ca 2 (tự nối giờ sau Ca 1 + 15 phút) và copy phòng/nhân sự từ Ca 1
    fireEvent.click(screen.getByRole('button', { name: /Thêm ca/i }))
    expect(screen.getByLabelText(/Tên ca/)).toHaveValue('Ca 2')
    expect(screen.getByLabelText(/Bắt đầu/)).toHaveValue('2026-11-20T09:15')
    expect(screen.getByLabelText(/Kết thúc/)).toHaveValue('2026-11-20T10:15')

    fireEvent.click(screen.getByRole('button', { name: /Dùng lại phòng & nhân sự từ Ca 1/i }))
    expect(screen.getByLabelText(/Phòng Lab/)).toHaveValue('Lab 402')

    goToStep3AndSubmit()

    await waitFor(() => expect(createShift).toHaveBeenCalledTimes(2))
    expect(createShift).toHaveBeenNthCalledWith(
      1,
      expect.objectContaining({
        shiftName: 'Ca 1',
        roomLab: 'Lab 402',
        startTime: new Date('2026-11-20T08:00').toISOString(),
        endTime: new Date('2026-11-20T09:00').toISOString(),
      }),
    )
    expect(createShift).toHaveBeenNthCalledWith(
      2,
      expect.objectContaining({
        shiftName: 'Ca 2',
        roomLab: 'Lab 402',
        startTime: new Date('2026-11-20T09:15').toISOString(),
        endTime: new Date('2026-11-20T10:15').toISOString(),
      }),
    )
  })

  it('previews Excel candidates, recommends rooms by Excel count, commits and auto-allocates on submit', async () => {
    previewCandidateImport.mockResolvedValueOnce({
      sessionId: 'session-1',
      examAttemptType: 'FE',
      mode: 'REPLACE',
      totalRows: 20,
      validCount: 20,
      invalidCount: 0,
      duplicateCount: 0,
      canCommit: true,
      existingCandidateCount: 0,
      projectedTotalCandidates: 20,
      maxStudentsPerRoom: 24,
      recommendedTotalRooms: 1,
      issues: [],
      rows: [],
    })
    commitCandidateImport.mockResolvedValueOnce({
      sessionId: 'session-1',
      examAttemptType: 'FE',
      mode: 'REPLACE',
      importedCount: 20,
      totalCandidateCount: 20,
      recommendedTotalRooms: 1,
      importedAt: '2026-11-20T07:00:00Z',
    })
    autoAllocateStudents.mockResolvedValueOnce({
      sessionId: 'session-1',
      dryRun: false,
      totalEligibleStudents: 20,
      previouslyAllocatedCount: 0,
      newlyAllocatedCount: 20,
      shiftGroups: [
        {
          startTime: '2026-11-20T08:00:00Z',
          endTime: '2026-11-20T09:00:00Z',
          isFinalBalancedShift: true,
          totalAssignedInGroup: 20,
          rooms: [
            {
              shiftId: 'shift-1',
              roomLab: 'Lab 402',
              maxSeats: 20,
              assignedBefore: 0,
              assignedNow: 20,
              totalAfter: 20,
            },
          ],
        },
      ],
    })

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

    const excelFile = new File(['student_code\nSE190001'], 'candidates.xlsx', {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    })
    const fileInput = screen.getByLabelText('Chọn tệp Excel danh sách thí sinh')
    fireEvent.change(fileInput, { target: { files: [excelFile] } })

    await waitFor(() =>
      expect(previewCandidateImport).toHaveBeenCalledWith('session-1', excelFile, 'REPLACE'),
    )
    expect(await screen.findByText(/Hợp lệ: 20 thí sinh/)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Áp dụng toàn bộ 1 ca/i }))
    await waitFor(() =>
      expect(screen.getAllByRole('option', { name: /Nguyễn Giám thị/ })).not.toHaveLength(0),
    )
    fireEvent.change(screen.getByLabelText(/Bắt đầu/), { target: { value: '2026-11-20T08:00' } })
    fireEvent.change(screen.getByLabelText(/Kết thúc/), { target: { value: '2026-11-20T09:00' } })
    fireEvent.change(screen.getByLabelText(/Phòng Lab/), { target: { value: 'Lab 402' } })
    fireEvent.change(screen.getByLabelText(/Giám thị/), { target: { value: 'proctor-1' } })
    fireEvent.change(screen.getByLabelText(/Giám khảo/), { target: { value: 'examiner-1' } })

    goToStep3AndSubmit()

    await waitFor(() =>
      expect(commitCandidateImport).toHaveBeenCalledWith('session-1', excelFile, 'REPLACE'),
    )
    await waitFor(() =>
      expect(autoAllocateStudents).toHaveBeenCalledWith('session-1', {
        shiftIds: ['shift-1'],
        assignHandedOverExamSets: true,
        dryRun: false,
      }),
    )
    expect(await screen.findByText(/chia ngẫu nhiên 20 thí sinh/)).toBeInTheDocument()
  })

  it('allows toggling assignHandedOverExamSets off on Step 3 and expands Step 1 campaign setup on demand', async () => {
    const previewCandidateImport = vi.mocked(officialExamService.previewCandidateImport)
    const commitCandidateImport = vi.mocked(officialExamService.commitCandidateImport)
    const autoAllocateStudents = vi.mocked(officialExamService.autoAllocateStudents)

    previewCandidateImport.mockResolvedValue({
      sessionId: 'session-1',
      examAttemptType: 'FE',
      mode: 'REPLACE',
      totalRows: 10,
      validCount: 10,
      invalidCount: 0,
      duplicateCount: 0,
      canCommit: true,
      existingCandidateCount: 0,
      projectedTotalCandidates: 10,
      maxStudentsPerRoom: 24,
      recommendedTotalRooms: 1,
      issues: [],
      rows: [],
    })
    commitCandidateImport.mockResolvedValue({
      sessionId: 'session-1',
      examAttemptType: 'FE',
      mode: 'REPLACE',
      importedCount: 10,
      totalCandidateCount: 10,
      recommendedTotalRooms: 1,
      importedAt: '2026-11-20T07:00:00Z',
    })
    autoAllocateStudents.mockResolvedValue({
      sessionId: 'session-1',
      dryRun: false,
      totalEligibleStudents: 10,
      previouslyAllocatedCount: 0,
      newlyAllocatedCount: 10,
      shiftGroups: [],
    })

    render(
      <MemoryRouter>
        <ExaminationOfficerExamManagementPage />
      </MemoryRouter>,
    )

    fireEvent.click(await screen.findByRole('button', { name: /Khởi tạo đợt thi \(Bước 1\)/i }))
    expect(await screen.findByText(/Bước 1 · Chọn Đợt thi & Môn thi/i)).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: /Ẩn khởi tạo đợt thi/i }))

    await openCreateDialog()
    await waitFor(() => expect(screen.getByLabelText(/Môn thi trong học kỳ/)).toBeEnabled())
    fireEvent.change(screen.getByLabelText(/Môn thi trong học kỳ/), {
      target: { value: 'course-1' },
    })

    const excelFile = new File(['student_code\nSE190001'], 'candidates.xlsx', {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    })
    fireEvent.change(screen.getByLabelText('Chọn tệp Excel danh sách thí sinh'), {
      target: { files: [excelFile] },
    })
    expect(await screen.findByText(/Hợp lệ: 10 thí sinh/)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Áp dụng toàn bộ 1 ca/i }))
    await waitFor(() =>
      expect(screen.getAllByRole('option', { name: /Nguyễn Giám thị/ })).not.toHaveLength(0),
    )
    fireEvent.change(screen.getByLabelText(/Bắt đầu/), { target: { value: '2026-11-20T08:00' } })
    fireEvent.change(screen.getByLabelText(/Kết thúc/), { target: { value: '2026-11-20T09:00' } })
    fireEvent.change(screen.getByLabelText(/Phòng Lab/), { target: { value: 'Lab 402' } })
    fireEvent.change(screen.getByLabelText(/Giám thị/), { target: { value: 'proctor-1' } })
    fireEvent.change(screen.getByLabelText(/Giám khảo/), { target: { value: 'examiner-1' } })

    const dialog = screen.getByRole('dialog', { name: 'Tạo ca thi' })
    fireEvent.click(within(dialog).getByRole('button', { name: /Tiếp tục: Xem lại/i }))
    const assignSetsToggle = within(dialog).getByRole('checkbox', {
      name: /Phát bộ đề HANDED_OVER không trùng ngay khi chia phòng/i,
    })
    expect(assignSetsToggle).toBeChecked()
    fireEvent.click(assignSetsToggle)
    expect(assignSetsToggle).not.toBeChecked()

    fireEvent.click(within(dialog).getByRole('button', { name: 'Tạo ca thi' }))

    await waitFor(() =>
      expect(autoAllocateStudents).toHaveBeenCalledWith('session-1', {
        shiftIds: ['shift-1'],
        assignHandedOverExamSets: false,
        dryRun: false,
      }),
    )
  })

  it('skips already-created shifts on retry after a partial failure so Ca 1 is never duplicated', async () => {
    createShift
      .mockResolvedValueOnce({
        id: 'shift-ca1',
        sessionId: 'session-1',
        shiftName: 'Ca 1',
        roomLab: 'Lab 402',
        startTime: '2026-11-20T08:00:00Z',
        endTime: '2026-11-20T09:00:00Z',
        proctorUserId: 'proctor-1',
        examinerUserId: 'examiner-1',
        examinerName: 'Examiner',
        proctorName: 'Proctor',
        status: 'SCHEDULED',
      })
      .mockRejectedValueOnce({ status: 422, detail: 'Giám thị ở Ca 2 đang bị khóa.' })
      .mockResolvedValueOnce({
        id: 'shift-ca2',
        sessionId: 'session-1',
        shiftName: 'Ca 2',
        roomLab: 'Lab 402',
        startTime: '2026-11-20T09:15:00Z',
        endTime: '2026-11-20T10:15:00Z',
        proctorUserId: 'proctor-1',
        examinerUserId: 'examiner-1',
        examinerName: 'Examiner',
        proctorName: 'Proctor',
        status: 'SCHEDULED',
      })

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
    goToStep2()
    await waitFor(() =>
      expect(screen.getAllByRole('option', { name: /Nguyễn Giám thị/ })).not.toHaveLength(0),
    )

    fireEvent.change(screen.getByLabelText(/Tên ca/), { target: { value: 'Ca 1' } })
    fireEvent.change(screen.getByLabelText(/Bắt đầu/), { target: { value: '2026-11-20T08:00' } })
    fireEvent.change(screen.getByLabelText(/Kết thúc/), { target: { value: '2026-11-20T09:00' } })
    fireEvent.change(screen.getByLabelText(/Phòng Lab/), { target: { value: 'Lab 402' } })
    fireEvent.change(screen.getByLabelText(/Giám thị/), { target: { value: 'proctor-1' } })
    fireEvent.change(screen.getByLabelText(/Giám khảo/), { target: { value: 'examiner-1' } })

    fireEvent.click(screen.getByRole('button', { name: /Thêm ca/i }))
    fireEvent.click(screen.getByRole('button', { name: /Dùng lại phòng & nhân sự từ Ca 1/i }))

    const dialog = screen.getByRole('dialog', { name: 'Tạo ca thi' })
    fireEvent.click(within(dialog).getByRole('button', { name: /Tiếp tục: Xem lại/i }))
    const submitBtn = () => within(dialog).getByRole('button', { name: 'Tạo ca thi' })

    // Lần 1: Ca 1 thành công, Ca 2 lỗi
    fireEvent.click(submitBtn())
    await waitFor(() => expect(createShift).toHaveBeenCalledTimes(2))

    // Lần 2: Retry — chỉ tạo Ca 2, tuyệt đối không gọi lại Ca 1
    fireEvent.click(submitBtn())
    await waitFor(() => expect(createShift).toHaveBeenCalledTimes(3))
    expect(createShift).toHaveBeenNthCalledWith(3, expect.objectContaining({ shiftName: 'Ca 2' }))
  })
})
