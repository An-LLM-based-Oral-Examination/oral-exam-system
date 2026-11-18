import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ExamHistoryPage } from '../ExamHistoryPage'
import type { InClassShift, StudentExamResult } from '@/types/inClassShift.types'

const mocks = vi.hoisted(() => ({
  getStudentHistory: vi.fn(),
  getMockHistory: vi.fn(),
  listStudentShifts: vi.fn(),
  listStudentResults: vi.fn(),
  apiGet: vi.fn(),
  appealsList: vi.fn(),
}))

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({
    user: {
      id: 'student-1',
      role: 'student',
      fullName: 'Nguyễn Văn Sinh Viên',
    },
  }),
}))

vi.mock('@/services/practice.service', () => ({
  practiceService: {
    getStudentHistory: mocks.getStudentHistory,
    getSession: vi.fn(),
  },
}))

vi.mock('@/services/mockExam.service', () => ({
  mockExamService: {
    getHistory: mocks.getMockHistory,
    getHistoryDetail: vi.fn(),
  },
}))

vi.mock('@/services/inClassShift.service', () => ({
  inClassShiftService: {
    listStudentShifts: mocks.listStudentShifts,
    listStudentResults: mocks.listStudentResults,
  },
}))

vi.mock('@/services/api.client', () => ({
  default: {
    get: mocks.apiGet,
  },
  apiClient: {
    get: mocks.apiGet,
  },
}))

vi.mock('@/services/appeals.service', () => ({
  appealsService: {
    list: mocks.appealsList,
    create: vi.fn(),
  },
}))

function makeShift(overrides: Partial<InClassShift>): InClassShift {
  return {
    id: 'shift-default',
    classId: 'cls-1',
    className: 'SE1801',
    courseCode: 'PRN231',
    courseName: 'Back-End .NET',
    title: 'Kiểm tra tiến độ 1',
    description: '',
    status: 'CLOSED',
    isLocked: false,
    startTime: '2026-11-18T07:00:00Z',
    endTime: '2026-11-18T08:00:00Z',
    durationMinutes: 60,
    examDurationMinutes: 15,
    questionCount: 2,
    hasFollowUp: true,
    maxFollowUpQuestions: 1,
    isAutoPublishGrades: false,
    questionPoolMode: 'SELECTED_CLOS',
    selectedDifficulties: ['easy', 'medium'],
    selectedCloIds: ['clo-1'],
    passwordRotationSeconds: 300,
    gracePeriodSeconds: 60,
    joinedCount: 20,
    submittedCount: 20,
    createdAt: '2026-11-18T06:30:00Z',
    myParticipationStatus: 'GRADED',
    myJoinedAt: '2026-11-18T07:05:00Z',
    myRemainingSeconds: 0,
    myFinalScore: null,
    ...overrides,
  }
}

describe('ExamHistoryPage MF-05 In-Class Exam integration & Invariant #9', () => {
  beforeEach(() => {
    mocks.getStudentHistory.mockResolvedValue([])
    mocks.getMockHistory.mockResolvedValue({
      items: [],
      pageIndex: 1,
      pageSize: 50,
      totalCount: 0,
      totalPages: 0,
    })
    mocks.apiGet.mockResolvedValue({ data: { officialExamTickets: [] } })
    mocks.appealsList.mockResolvedValue([])
  })

  afterEach(() => {
    cleanup()
    vi.clearAllMocks()
  })

  it('hides scores & AI feedback before FINALIZED and displays them once FINALIZED', async () => {
    const unfinalizedShift = makeShift({
      id: 'shift-pending',
      title: 'Ca kiểm tra chưa chốt sổ',
      status: 'CLOSED',
      isLocked: false,
      myParticipationStatus: 'GRADED',
      // Even if a stale object carried a number, UI must guard Invariant #9:
      myFinalScore: 9.25,
    })
    const finalizedShift = makeShift({
      id: 'shift-finalized',
      title: 'Ca kiểm tra đã chốt sổ',
      status: 'FINALIZED',
      isLocked: true,
      myParticipationStatus: 'FINALIZED',
      finalizedAt: '2026-11-18T09:00:00Z',
      myFinalScore: 8.5,
    })
    const unjoinedShift = makeShift({
      id: 'shift-not-joined',
      title: 'Ca sinh viên chưa tham gia',
      myParticipationStatus: null,
    })

    mocks.listStudentShifts.mockResolvedValue([
      unfinalizedShift,
      finalizedShift,
      unjoinedShift,
    ])

    const sampleResults: Record<string, StudentExamResult[]> = {
      'shift-pending': [
        {
          submissionId: 'sub-p-1',
          questionId: 'q-p-1',
          mainOrderIndex: 1,
          title: 'Dependency Injection',
          content: 'Phân biệt Scoped và Transient trong .NET.',
          transcript: 'Scoped sống theo từng HTTP request còn Transient tạo mới mỗi lần inject.',
          aiScore: 9.25,
          aiFeedback: 'Nhận xét nội bộ chưa được phép lộ trước khi chốt sổ.',
          finalScore: 9.25,
          lecturerNotes: 'Ghi chú hậu kiểm nội bộ.',
          status: 'AUDITED',
          isFollowUp: false,
        },
      ],
      'shift-finalized': [
        {
          submissionId: 'sub-f-1',
          questionId: 'q-f-1',
          mainOrderIndex: 1,
          title: 'Clean Architecture',
          content: 'Trình bày vai trò tầng Application.',
          transcript: 'Tầng Application điều phối use cases thông qua CQRS MediatR.',
          aiScore: 8.0,
          aiFeedback: 'Nắm vững vai trò điều phối use case.',
          finalScore: 8.5,
          lecturerNotes: 'Bổ sung ví dụ tốt khi trả lời.',
          status: 'AUDITED',
          isFollowUp: false,
        },
      ],
    }
    mocks.listStudentResults.mockImplementation((shiftId: string) =>
      Promise.resolve(sampleResults[shiftId] ?? []),
    )

    render(
      <MemoryRouter initialEntries={['/results?type=in_class']}>
        <ExamHistoryPage />
      </MemoryRouter>,
    )

    // Chỉ hiện 2 ca đã tham gia, loại bỏ ca chưa tham gia
    expect(await screen.findByText('Ca kiểm tra chưa chốt sổ')).toBeInTheDocument()
    expect(screen.getByText('Ca kiểm tra đã chốt sổ')).toBeInTheDocument()
    expect(screen.queryByText('Ca sinh viên chưa tham gia')).toBeNull()

    // Ca đã FINALIZED hiện điểm 8.50 / 10; ca chưa FINALIZED tuyệt đối KHÔNG hiện 9.25
    expect(screen.getByText('8.50 / 10')).toBeInTheDocument()
    expect(screen.queryByText(/9\.25/)).toBeNull()
    expect(screen.getByText('Đã nộp · Chờ GV chốt điểm')).toBeInTheDocument()

    // Bấm xem chi tiết ca CHƯA FINALIZED -> hiện transcript nhưng ẩn sạch điểm & nhận xét
    const detailButtons = screen.getAllByRole('button', { name: /Xem câu hỏi & nhận xét/i })
    // Sắp xếp theo thời gian mới nhất: shift-finalized (09:00) đứng trên, shift-pending (07:05) đứng dưới
    fireEvent.click(detailButtons[1])

    expect(
      await screen.findByText(
        'Điểm số và nhận xét chi tiết sẽ được hiển thị sau khi Giảng viên chốt bảng điểm của ca kiểm tra.',
      ),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        'Scoped sống theo từng HTTP request còn Transient tạo mới mỗi lần inject.',
      ),
    ).toBeInTheDocument()
    expect(screen.queryByText('Nhận xét nội bộ chưa được phép lộ trước khi chốt sổ.')).toBeNull()
    expect(screen.queryByText(/Ghi chú hậu kiểm nội bộ/)).toBeNull()
    expect(screen.queryByText('9.3/10')).toBeNull()

    // Đóng modal và mở ca ĐÃ FINALIZED -> hiện đầy đủ điểm và nhận xét
    fireEvent.click(screen.getAllByRole('button', { name: 'Đóng' })[0])
    await waitFor(() =>
      expect(
        screen.queryByText(
          'Điểm số và nhận xét chi tiết sẽ được hiển thị sau khi Giảng viên chốt bảng điểm của ca kiểm tra.',
        ),
      ).toBeNull(),
    )

    fireEvent.click(screen.getAllByRole('button', { name: /Xem câu hỏi & nhận xét/i })[0])

    expect(
      await screen.findByText('Tầng Application điều phối use cases thông qua CQRS MediatR.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Nắm vững vai trò điều phối use case.')).toBeInTheDocument()
    expect(screen.getByText(/Bổ sung ví dụ tốt khi trả lời/)).toBeInTheDocument()
    expect(screen.getByText('8.5/10')).toBeInTheDocument()
  })

  it('renders continue link with ?shiftId= param when shift is ACTIVE and student is IN_PROGRESS', async () => {
    const activeShift = makeShift({
      id: 'shift-live-42',
      title: 'Ca đang làm bài dở',
      status: 'ACTIVE',
      isLocked: false,
      myParticipationStatus: 'IN_PROGRESS',
      myRemainingSeconds: 420,
    })
    mocks.listStudentShifts.mockResolvedValue([activeShift])

    render(
      <MemoryRouter initialEntries={['/results']}>
        <ExamHistoryPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Ca đang làm bài dở')).toBeInTheDocument()
    const continueLink = screen.getByRole('link', { name: /Tiếp tục làm/i })
    expect(continueLink).toHaveAttribute('href', '/in-class-exam/session?shiftId=shift-live-42')
  })
})
