import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { StudentDashboardPage } from '../StudentDashboardPage'
import { studentOverviewService } from '@/services/studentOverview.service'
import { classService } from '@/services/class.service'
import { mockExamService } from '@/services/mockExam.service'
import { inClassShiftService } from '@/services/inClassShift.service'
import type { CourseSummary, PracticeHistoryItem } from '@/services/studentOverview.service'
import type { StudentClassItem } from '@/types/class.types'

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({
    user: {
      id: 'student-uuid-1',
      fullName: 'Trần Sinh Viên',
      email: 'sinhvien@fpt.edu.vn',
      role: 'student',
    },
  }),
}))

vi.mock('@/services/studentOverview.service', () => ({
  studentOverviewService: {
    getCourses: vi.fn(),
    getPracticeHistory: vi.fn(),
    getOfficialExamTickets: vi.fn(),
    getMockExamQuota: vi.fn(),
  },
}))

vi.mock('@/services/class.service', () => ({
  classService: {
    listMyClasses: vi.fn(),
  },
}))

vi.mock('@/services/mockExam.service', () => ({
  mockExamService: {
    getHistory: vi.fn(),
  },
}))

vi.mock('@/services/inClassShift.service', () => ({
  inClassShiftService: {
    listStudentShifts: vi.fn(),
  },
}))

vi.mock('../StudentOverviewThreeScene', () => ({
  StudentOverviewThreeScene: () => <div data-testid="student-three-scene-mock" />,
}))

describe('StudentDashboardPage (Shared Portal UI)', () => {
  const mockCourse: CourseSummary = {
    id: 'course-1',
    code: 'PRN231',
    name: 'Xây dựng dịch vụ Web với .NET',
    credits: 3,
    isActive: true,
  }

  const mockMyClass: StudentClassItem = {
    classId: 'class-1',
    classCode: 'SE1701',
    className: 'PRN231 - SE1701',
    courseId: 'course-1',
    courseCode: 'PRN231',
    courseName: 'Xây dựng dịch vụ Web với .NET',
    semesterId: 'sem-1',
    semesterCode: 'FA26',
    lecturerName: 'Thầy Nguyễn Văn Giảng',
    enrolledAt: '2026-11-01T00:00:00Z',
  }

  const mockPracticeHistory: PracticeHistoryItem[] = [
    {
      sessionId: 'sess-1',
      courseId: 'course-1',
      courseCode: 'PRN231',
      courseName: 'Xây dựng dịch vụ Web với .NET',
      practiceMode: 'on_demand',
      status: 'completed',
      averageScore: 8.5,
      startedAt: '2026-11-15T10:00:00Z',
      endedAt: '2026-11-15T10:20:00Z',
      totalQuestions: 5,
      answeredQuestions: 5,
    },
  ]

  beforeEach(() => {
    vi.mocked(studentOverviewService.getCourses).mockResolvedValue([mockCourse])
    vi.mocked(classService.listMyClasses).mockResolvedValue([mockMyClass])
    vi.mocked(studentOverviewService.getPracticeHistory).mockResolvedValue(mockPracticeHistory)
    vi.mocked(studentOverviewService.getOfficialExamTickets).mockResolvedValue([])
    vi.mocked(inClassShiftService.listStudentShifts).mockResolvedValue([])
    vi.mocked(studentOverviewService.getMockExamQuota).mockResolvedValue({
      courseId: 'course-1',
      usedCount: 1,
      remainingCount: 2,
      maxDailyQuota: 3,
      canStartExam: true,
    })
    vi.mocked(mockExamService.getHistory).mockResolvedValue({
      items: [],
      pageIndex: 1,
      pageSize: 10,
      totalCount: 0,
      totalPages: 0,
    })
  })

  afterEach(() => {
    cleanup()
    vi.resetAllMocks()
  })

  it('renders student welcome banner and 3 overview KPI cards', async () => {
    render(
      <MemoryRouter>
        <StudentDashboardPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText(/Trần Sinh Viên/)).toBeInTheDocument()
    expect(screen.getByText('Lớp đã tham gia')).toBeInTheDocument()
    expect(screen.getByText('Phiên luyện tập hoàn tất')).toBeInTheDocument()
    expect(screen.getByText('Điểm trung bình tích lũy')).toBeInTheDocument()
    expect(screen.getByText('8.5')).toBeInTheDocument()
  })

  it('renders recent completed activities and result link', async () => {
    render(
      <MemoryRouter>
        <StudentDashboardPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Bài đã làm gần đây')).toBeInTheDocument()
    expect(screen.getByText('Luyện từng câu')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Xem chi tiết cho/ })).toBeInTheDocument()
  })

  it('renders active upcoming shifts with entry CTA and in-progress mock exam resume card', async () => {
    vi.mocked(inClassShiftService.listStudentShifts).mockResolvedValue([
      {
        id: 'shift-1',
        classId: 'class-1',
        className: 'PRN231 - SE1701',
        courseCode: 'PRN231',
        title: 'Ca kiểm tra giữa kỳ Sprint 2',
        description: 'Kiểm tra vấn đáp Web API',
        status: 'ACTIVE',
        isLocked: false,
        startTime: '2026-11-16T08:00:00Z',
        durationMinutes: 45,
        examDurationMinutes: 15,
        questionCount: 3,
        hasFollowUp: true,
        maxFollowUpQuestions: 2,
        isAutoPublishGrades: false,
        questionPoolMode: 'ALL_COURSE_BANK',
        selectedDifficulties: ['easy', 'medium', 'hard'],
        selectedCloIds: [],
        passwordRotationSeconds: 300,
        gracePeriodSeconds: 60,
        joinedCount: 12,
        submittedCount: 5,
        createdAt: '2026-11-15T00:00:00Z',
        myParticipationStatus: null,
        myJoinedAt: null,
      },
    ])
    vi.mocked(mockExamService.getHistory).mockResolvedValue({
      items: [
        {
          sessionId: 'mock-live-1',
          courseId: 'course-1',
          courseCode: 'PRN231',
          courseName: 'Xây dựng dịch vụ Web với .NET',
          startedAt: '2026-11-16T06:00:00Z',
          durationMinutes: 20,
          totalScore: null,
          status: 'in_progress',
          answeredQuestions: 1,
          totalQuestions: 4,
        },
      ],
      pageIndex: 1,
      pageSize: 10,
      totalCount: 1,
      totalPages: 1,
    })

    render(
      <MemoryRouter>
        <StudentDashboardPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Ca kiểm tra giữa kỳ Sprint 2')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Vào ca' })).toHaveAttribute(
      'href',
      '/classes/class-1?shift=shift-1',
    )
    expect(screen.getByRole('link', { name: /Tiếp tục/ })).toHaveAttribute(
      'href',
      '/mock-exam/session?session=mock-live-1',
    )
  })

  it('directs unenrolled students to /classes in the learning panel', async () => {
    vi.mocked(classService.listMyClasses).mockResolvedValue([])
    vi.mocked(studentOverviewService.getCourses).mockResolvedValue([mockCourse])

    render(
      <MemoryRouter>
        <StudentDashboardPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Chưa tham gia lớp')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Tìm lớp' })).toHaveAttribute('href', '/classes')
    expect(screen.queryByLabelText('Lịch thi chính thức')).not.toBeInTheDocument()
  })

  it('renders official exam card with /exam link when student has an active official ticket', async () => {
    vi.mocked(studentOverviewService.getOfficialExamTickets).mockResolvedValue([
      {
        ticketId: 'ticket-official-1',
        courseCode: 'PRN231',
        courseName: 'Xây dựng dịch vụ Web với .NET',
        shiftName: 'Ca 1 · Thi cuối kỳ FE',
        roomLab: 'AL-R301',
        startTime: '2026-11-25T08:00:00Z',
        seatNumber: 7,
        status: 'SCHEDULED',
      },
      {
        ticketId: 'ticket-official-old',
        courseCode: 'SWP391',
        courseName: 'Software Project',
        shiftName: 'Ca cũ đã công bố',
        roomLab: 'AL-R102',
        startTime: '2026-10-01T08:00:00Z',
        seatNumber: 2,
        status: 'PUBLISHED',
      },
    ])

    render(
      <MemoryRouter>
        <StudentDashboardPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Phòng thi chính thức')).toBeInTheDocument()
    expect(screen.getByText('Ca 1 · Thi cuối kỳ FE')).toBeInTheDocument()
    expect(screen.getByText(/Phòng AL-R301 · Ghế #07/)).toBeInTheDocument()
    expect(screen.queryByText('Ca cũ đã công bố')).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Vào phòng thi \(Web dự phòng\)/ })).toHaveAttribute(
      'href',
      '/exam',
    )
  })

  it('allows resuming in_progress practice sessions from Tiếp tục học card', async () => {
    vi.mocked(studentOverviewService.getPracticeHistory).mockResolvedValue([
      {
        sessionId: 'prac-live-1',
        courseId: 'course-1',
        courseCode: 'PRN231',
        courseName: 'Xây dựng dịch vụ Web với .NET',
        practiceMode: 'on_demand',
        status: 'in_progress',
        averageScore: null,
        startedAt: '2026-11-16T07:00:00Z',
        endedAt: null,
        totalQuestions: 5,
        answeredQuestions: 2,
      },
    ])

    render(
      <MemoryRouter>
        <StudentDashboardPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Tiếp tục học')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Tiếp tục/ })).toHaveAttribute(
      'href',
      '/practice/session?session=prac-live-1',
    )
  })
})
