import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { LecturerDashboardPage } from '../LecturerDashboardPage'
import { classService } from '@/services/class.service'
import { questionService } from '@/services/question.service'
import type { LecturerCourseOffering, ManagedClass, SemesterOption } from '@/types/class.types'
import type { QuestionSummary } from '@/types/rubric.types'

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({
    user: {
      id: 'lecturer-uuid-1',
      fullName: 'Thầy Nguyễn Văn Giảng',
      email: 'giangnv@fpt.edu.vn',
      role: 'Instructor',
    },
  }),
}))

vi.mock('@/services/class.service', () => ({
  classService: {
    listMyOfferings: vi.fn(),
    listLecturerClasses: vi.fn(),
    listSemesters: vi.fn(),
  },
}))

vi.mock('@/services/question.service', () => ({
  questionService: {
    getQuestions: vi.fn(),
  },
}))

vi.mock('../LecturerOverviewThreeScene', () => ({
  LecturerOverviewThreeScene: () => <div data-testid="lecturer-three-scene-mock" />,
}))

describe('LecturerDashboardPage (Shared Portal UI)', () => {
  const mockSemester: SemesterOption = {
    id: 'sem-1',
    name: 'Fall 2026',
    code: 'FA26',
    startDate: '2026-09-01',
    endDate: '2026-12-31',
    isActive: true,
  }

  const mockOffering: LecturerCourseOffering = {
    courseOfferingId: 'offering-1',
    courseId: 'course-1',
    courseCode: 'PRN231',
    courseName: 'Xây dựng dịch vụ Web với .NET',
    semesterId: 'sem-1',
    semesterCode: 'FA26',
  }

  const mockClass: ManagedClass = {
    id: 'cls-1',
    code: 'SE1701',
    name: 'PRN231 - SE1701',
    courseCode: 'PRN231',
    courseName: 'Xây dựng dịch vụ Web với .NET',
    enrolledCount: 32,
    maxStudents: 40,
    semesterId: 'sem-1',
    semesterCode: 'FA26',
    joinCode: 'CLS01',
    joinPassword: 'secretpassword',
    shiftCount: 1,
    activeShiftCount: 0,
    hasPassword: true,
    isJoinEnabled: true,
  }

  const mockQuestions: QuestionSummary[] = [
    {
      id: 'q-1',
      title: 'Trình bày cơ chế Dependency Injection trong ASP.NET Core',
      courseId: 'course-1',
      courseCode: 'PRN231',
      approvalStatus: 'APPROVED',
      submittedBy: 'lecturer-uuid-1',
      approvedBy: 'head-uuid-1',
      totalRubricScore: 10,
      difficulty: 'medium',
      bloomLevel: 'Understand',
      source: 'manual',
      createdAt: '2026-11-01T00:00:00Z',
    },
  ]

  beforeEach(() => {
    vi.mocked(classService.listMyOfferings).mockResolvedValue([mockOffering])
    vi.mocked(classService.listLecturerClasses).mockResolvedValue([mockClass])
    vi.mocked(classService.listSemesters).mockResolvedValue([mockSemester])
    vi.mocked(questionService.getQuestions).mockResolvedValue({
      items: mockQuestions,
      pageNumber: 1,
      pageSize: 100,
      totalCount: 1,
      totalPages: 1,
    })
  })

  afterEach(() => {
    cleanup()
    vi.resetAllMocks()
  })

  it('renders lecturer welcome banner and faculty title', async () => {
    render(
      <MemoryRouter>
        <LecturerDashboardPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText(/Thầy Nguyễn Văn Giảng/)).toBeInTheDocument()
    expect(screen.getByText(/Soạn câu hỏi luyện tập, mở ca vấn đáp trên lớp/)).toBeInTheDocument()
  })

  it('renders 4 teaching summary metric cards', async () => {
    render(
      <MemoryRouter>
        <LecturerDashboardPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Môn phân công')).toBeInTheDocument()
    expect(screen.getByText('Lớp phụ trách')).toBeInTheDocument()
    expect(screen.getByText('Sinh viên')).toBeInTheDocument()
    expect(screen.getByText('Câu đã duyệt')).toBeInTheDocument()
  })

  it('renders assigned courses list and class management link', async () => {
    render(
      <MemoryRouter>
        <LecturerDashboardPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Môn được phân công')).toBeInTheDocument()
    expect(screen.getByText('PRN231')).toBeInTheDocument()
    expect(screen.getByText('Xây dựng dịch vụ Web với .NET')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Quản lý lớp/ })).toHaveAttribute('href', '/lecturer/classes')
  })
})
