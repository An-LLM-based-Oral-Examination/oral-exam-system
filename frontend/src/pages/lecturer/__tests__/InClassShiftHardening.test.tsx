import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter, Route, RouterProvider, Routes, createMemoryRouter, useLocation } from 'react-router-dom'
import { apiClient } from '@/services/api.client'
import { inClassShiftService } from '@/services/inClassShift.service'
import { InClassShiftResultsPage } from '../InClassShiftResultsPage'
import { StudentInClassExamPage } from '@/pages/student/StudentInClassExamPage'
import { InClassExamSessionPage } from '@/pages/student/InClassExamSessionPage'
import {
  saveInClassExamDrafts,
  saveInClassExamSession,
} from '@/utils/inClassExamSessionStorage'

vi.mock('@/services/class.service', () => ({
  classService: {
    listMyClasses: vi.fn().mockResolvedValue([]),
  },
}))

vi.mock('@/services/signalr.service', () => ({
  createInClassShiftHubConnection: () => ({
    on: vi.fn(),
    off: vi.fn(),
    onreconnected: vi.fn(),
    start: vi.fn().mockResolvedValue(undefined),
    stop: vi.fn().mockResolvedValue(undefined),
    invoke: vi.fn().mockResolvedValue(undefined),
  }),
}))

vi.mock('@/hooks/useSpeechRecognition', () => ({
  useSpeechRecognition: () => ({
    isSupported: true,
    isListening: false,
    transcript: '',
    error: null,
    startListening: vi.fn(),
    stopListening: vi.fn(),
    resetTranscript: vi.fn(),
  }),
}))

vi.mock('@/services/speech.service', () => ({
  getSpeechRecognitionLangForCourse: () => 'vi-VN',
  getTtsVoiceForCourse: () => 'vi-VN',
  playNaturalSpeech: vi.fn().mockResolvedValue(undefined),
  stopSpeechPlayback: vi.fn(),
  transcribeSpeech: vi.fn(),
}))

describe('MF-05 End-to-End Frontend Hardening (Follow-up Ordering, Score Compensation & Mandatory Audit Reason)', () => {
  beforeEach(() => {
    sessionStorage.clear()
    localStorage.clear()
  })

  afterEach(() => {
    cleanup()
    vi.restoreAllMocks()
  })

  it('orders follow-ups directly after their parent question, resolves parent followUpPrompt, and enforces mandatory audit reason on InClassShiftResultsPage', async () => {
    const shiftPayload = {
      id: 'shift-gv-1',
      classId: 'cls-1',
      classCode: 'SE1801',
      className: 'PRN231 - SE1801',
      courseCode: 'PRN231',
      shiftName: 'Kiểm tra Vấn đáp Chương 2',
      status: 'CLOSED',
      isLocked: false,
      startTime: '2026-11-16T07:00:00Z',
      endTime: '2026-11-16T08:00:00Z',
      totalQuestionsPerStudent: 2,
      hasFollowUp: true,
      maxFollowUpQuestions: 2,
      examDurationMinutes: 20,
      participants: [
        {
          id: 'part-1',
          shiftId: 'shift-gv-1',
          studentId: 'stu-1',
          studentCode: 'SE180001',
          studentName: 'Nguyễn Văn A',
          status: 'GRADED',
          joinedAt: '2026-11-16T07:05:00Z',
          assignedQuestions: [
            {
              questionId: 'q-1',
              title: 'Dependency Inversion Principle',
              content: 'Trình bày nguyên lý Dependency Inversion trong Clean Architecture?',
            },
            {
              questionId: 'q-2',
              title: 'CQRS & MediatR',
              content: 'Phân tích lợi ích của việc tách biệt Command và Query?',
            },
          ],
        },
      ],
    }

    const submissionsPayload = [
      {
        id: 'sub-main-1',
        shiftId: 'shift-gv-1',
        studentId: 'stu-1',
        studentCode: 'SE180001',
        studentName: 'Nguyễn Văn A',
        questionId: 'q-1',
        questionTitle: 'Dependency Inversion Principle',
        questionContent: 'Trình bày nguyên lý Dependency Inversion trong Clean Architecture?',
        transcript: 'Module cấp cao không phụ thuộc module cấp thấp mà phụ thuộc abstraction.',
        aiScore: 5.0,
        finalScore: 5.0,
        aiFeedback: 'Nêu đúng định nghĩa nhưng thiếu ví dụ DI container.',
        status: 'AI_GRADED',
        isFollowUp: false,
        followUpIndex: 0,
        // Trong CSDL 3NF, FollowUpPrompt trên bản ghi cha lưu câu hỏi phụ hỏi ở lượt kế tiếp (#1)
        followUpPrompt: 'Hãy cho ví dụ cụ thể cách đăng ký interface ở tầng Infrastructure?',
        submittedAt: '2026-11-16T07:10:00Z',
      },
      {
        id: 'sub-main-2',
        shiftId: 'shift-gv-1',
        studentId: 'stu-1',
        studentCode: 'SE180001',
        studentName: 'Nguyễn Văn A',
        questionId: 'q-2',
        questionTitle: 'CQRS & MediatR',
        questionContent: 'Phân tích lợi ích của việc tách biệt Command và Query?',
        transcript: 'Giúp tối ưu mô hình đọc và mô hình ghi độc lập, dễ kiểm soát side-effects.',
        aiScore: 9.0,
        finalScore: 9.0,
        aiFeedback: 'Trình bày đầy đủ và rõ ràng.',
        status: 'AI_GRADED',
        isFollowUp: false,
        followUpIndex: 0,
        followUpPrompt: null,
        submittedAt: '2026-11-16T07:14:00Z',
      },
      {
        id: 'sub-fu-1',
        shiftId: 'shift-gv-1',
        studentId: 'stu-1',
        studentCode: 'SE180001',
        studentName: 'Nguyễn Văn A',
        questionId: 'q-1',
        questionTitle: 'Dependency Inversion Principle',
        questionContent: 'Trình bày nguyên lý Dependency Inversion trong Clean Architecture?',
        transcript: 'Application khai báo IApplicationDbContext, còn Infrastructure hiện thực OralExamDbContext.',
        aiScore: 8.0,
        finalScore: 8.0,
        aiFeedback: 'Ví dụ chính xác, bù điểm tốt cho câu chính.',
        status: 'AI_GRADED',
        isFollowUp: true,
        parentSubmissionId: 'sub-main-1',
        followUpIndex: 1,
        followUpPrompt: null,
        submittedAt: '2026-11-16T07:12:00Z',
      },
    ]

    vi.spyOn(apiClient, 'get').mockImplementation(async (url: string) => {
      if (url.endsWith('/submissions')) {
        return { data: submissionsPayload } as never
      }
      return { data: shiftPayload } as never
    })
    const putSpy = vi.spyOn(apiClient, 'put').mockResolvedValue({ data: {} } as never)

    render(
      <MemoryRouter initialEntries={['/lecturer/classes/cls-1/in-class-shifts/shift-gv-1/results']}>
        <Routes>
          <Route
            path="/lecturer/classes/:classId/in-class-shifts/:shiftId/results"
            element={<InClassShiftResultsPage />}
          />
        </Routes>
      </MemoryRouter>,
    )

    expect(await screen.findByText('Nguyễn Văn A')).toBeInTheDocument()

    // Mở ngăn xem bài chi tiết của sinh viên
    fireEvent.click(screen.getByRole('button', { name: /^Xem bài$/i }))

    // Kiểm tra thứ tự các tab: Câu 1 -> Câu 1 · Phụ #1 -> Câu 2 (bù điểm: (max(5,8)+9)/2 = 8.5)
    const tabMain1 = await screen.findByRole('button', { name: 'Câu 1' })
    const tabFollowUp1 = screen.getByRole('button', { name: 'Câu 1 · Phụ #1' })
    const tabMain2 = screen.getByRole('button', { name: 'Câu 2' })
    expect(tabMain1.compareDocumentPosition(tabFollowUp1) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(tabFollowUp1.compareDocumentPosition(tabMain2) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()

    // Bấm sang tab Câu 1 · Phụ #1 -> hiển thị đúng câu hỏi phụ AI đã hỏi từ bản ghi cha
    fireEvent.click(tabFollowUp1)
    expect(await screen.findByText('Câu hỏi phụ AI #1')).toBeInTheDocument()
    expect(
      screen.getByText('Hãy cho ví dụ cụ thể cách đăng ký interface ở tầng Infrastructure?'),
    ).toBeInTheDocument()

    // Mở hộp thoại Chỉnh điểm cả bài -> để trống lý do phải bị chặn ngay tại client
    fireEvent.click(screen.getByRole('button', { name: /Chỉnh điểm cả bài/i }))
    const reasonInput = await screen.findByLabelText(/Nhận xét \/ lý do hậu kiểm/i)
    fireEvent.change(reasonInput, { target: { value: '   ' } })
    fireEvent.click(screen.getByRole('button', { name: /Lưu điểm cả bài/i }))

    expect(
      await screen.findByText('Vui lòng nhập nhận xét hoặc lý do hậu kiểm trước khi lưu điểm.'),
    ).toBeInTheDocument()
    expect(putSpy).not.toHaveBeenCalled()

    // Nhập lý do hợp lệ -> cho phép gọi API hậu kiểm
    fireEvent.change(reasonInput, {
      target: { value: 'Sinh viên trả lời câu phụ rõ bản chất Clean Architecture' },
    })
    fireEvent.click(screen.getByRole('button', { name: /Lưu điểm cả bài/i }))

    await waitFor(() => {
      expect(putSpy).toHaveBeenCalled()
    })
  })

  it('computes compensated overall score (Max per question) and renders resolved follow-up prompt on StudentInClassExamPage after FINALIZED', async () => {
    vi.spyOn(inClassShiftService, 'listStudentShifts').mockResolvedValue([
      {
        id: 'shift-fin-1',
        classId: 'cls-1',
        className: 'PRN231 - SE1801',
        courseCode: 'PRN231',
        title: 'Ca Đã Chốt Bảng Điểm',
        description: '',
        status: 'FINALIZED',
        isLocked: true,
        startTime: '2026-11-16T07:00:00Z',
        endTime: '2026-11-16T08:00:00Z',
        durationMinutes: 60,
        examDurationMinutes: 20,
        questionCount: 2,
        hasFollowUp: true,
        maxFollowUpQuestions: 2,
        isAutoPublishGrades: false,
        questionPoolMode: 'SELECTED_CLOS',
        selectedDifficulties: ['easy', 'medium'],
        selectedCloIds: ['clo-1', 'clo-2'],
        passwordRotationSeconds: 300,
        gracePeriodSeconds: 60,
        joinedCount: 1,
        submittedCount: 1,
        createdAt: '2026-11-16T06:50:00Z',
        myParticipationStatus: 'FINALIZED',
      },
    ])

    // Mô phỏng mảng trả về theo thứ tự giảm dần của SubmittedAt (câu nộp muộn nhất nằm trên đầu)
    vi.spyOn(apiClient, 'get').mockResolvedValue({
      data: [
        {
          id: 's-2',
          questionId: 'q-2',
          questionTitle: 'Câu hỏi 2',
          questionContent: 'Nội dung câu 2',
          transcript: 'Trả lời chính câu 2',
          aiScore: 9.0,
          finalScore: 9.0,
          status: 'AI_GRADED',
          isFollowUp: false,
          followUpIndex: 0,
          followUpPrompt: null,
          submittedAt: '2026-11-16T07:14:00Z',
        },
        {
          id: 's-1-fu',
          questionId: 'q-1',
          questionTitle: 'Câu hỏi 1',
          questionContent: 'Nội dung câu 1',
          transcript: 'Trả lời phụ câu 1 rất tốt',
          aiScore: 8.0,
          finalScore: 8.0,
          status: 'AI_GRADED',
          isFollowUp: true,
          parentSubmissionId: 's-1',
          followUpIndex: 1,
          followUpPrompt: null,
          submittedAt: '2026-11-16T07:12:00Z',
        },
        {
          id: 's-1',
          questionId: 'q-1',
          questionTitle: 'Câu hỏi 1',
          questionContent: 'Nội dung câu 1',
          transcript: 'Trả lời chính câu 1',
          aiScore: 5.0,
          finalScore: 5.0,
          status: 'AI_GRADED',
          isFollowUp: false,
          followUpIndex: 0,
          followUpPrompt: 'Câu hỏi phụ bù điểm cho câu 1?',
          submittedAt: '2026-11-16T07:10:00Z',
        },
      ],
    } as never)

    render(
      <MemoryRouter initialEntries={['/in-class-exam']}>
        <StudentInClassExamPage />
      </MemoryRouter>,
    )

    const viewBtn = await screen.findByRole('button', { name: /Xem kết quả chốt/i })
    fireEvent.click(viewBtn)

    // Điểm bù chuẩn: (max(5.0, 8.0) + 9.0) / 2 = 8.5 (KHÔNG phải trung bình phẳng (5+8+9)/3 = 7.3)
    expect(await screen.findByText('8.5')).toBeInTheDocument()
    expect(screen.queryByText('7.3')).not.toBeInTheDocument()
    expect(screen.queryByText(/Công bố thời gian thực/i)).not.toBeInTheDocument()
    const headingMain1 = screen.getByText('Câu 1')
    const headingFu1 = screen.getByText('Câu 1 · Phụ #1')
    const headingMain2 = screen.getByText('Câu 2')
    expect(headingMain1.compareDocumentPosition(headingFu1) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(headingFu1.compareDocumentPosition(headingMain2) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(screen.getByText(/Câu hỏi phụ AI: Câu hỏi phụ bù điểm cho câu 1\?/i)).toBeInTheDocument()
  })

  it('shows honest post-submission message on InClassExamSessionPage stating scores appear after lecturer finalization', async () => {
    const nowMs = Date.now()
    const endsAtMs = nowMs + 15 * 60_000
    const endsAtIso = new Date(endsAtMs).toISOString()
    const activeShift = {
      id: 'shift-sess-1',
      classId: 'cls-1',
      className: 'PRN231 - SE1801',
      courseCode: 'PRN231',
      title: 'Ca Thi Ẩn Điểm Trước Khi Chốt',
      description: '',
      status: 'ACTIVE' as const,
      startTime: new Date(nowMs - 60_000).toISOString(),
      durationMinutes: 60,
      examDurationMinutes: 20,
      questionCount: 1,
      hasFollowUp: false,
      maxFollowUpQuestions: 0,
      isAutoPublishGrades: false,
      questionPoolMode: 'ALL_COURSE_BANK' as const,
      selectedDifficulties: ['easy' as const],
      selectedCloIds: [],
      passwordRotationSeconds: 300,
      gracePeriodSeconds: 60,
      joinedCount: 1,
      submittedCount: 0,
      createdAt: new Date(nowMs - 120_000).toISOString(),
    }
    saveInClassExamSession({
      shift: activeShift,
      questions: [
        {
          questionId: 'q-only-1',
          title: 'RESTful Idempotency',
          content: 'So sánh tính idempotent giữa PUT và POST?',
        },
      ],
      startedAt: nowMs,
      examEndsAt: endsAtMs,
      joinedAt: new Date(nowMs).toISOString(),
    })

    vi.spyOn(inClassShiftService, 'getStudentShift').mockResolvedValue(activeShift)
    vi.spyOn(inClassShiftService, 'resumeStudentShift').mockResolvedValue({
      questions: [
        {
          questionId: 'q-only-1',
          title: 'RESTful Idempotency',
          content: 'So sánh tính idempotent giữa PUT và POST?',
        },
      ],
      joinedAt: new Date(nowMs).toISOString(),
      examEndsAt: endsAtIso,
      remainingSeconds: 900,
      timedOut: false,
      status: 'IN_PROGRESS',
    })
    vi.spyOn(inClassShiftService, 'listStudentResults').mockResolvedValue([])
    vi.spyOn(inClassShiftService, 'submitStudentAnswer').mockResolvedValue({
      submissionId: 'sub-new-1',
      shiftId: 'shift-sess-1',
      questionId: 'q-only-1',
      status: 'SUBMITTED',
      isFollowUp: false,
      parentSubmissionId: null,
      followUpIndex: 0,
    })
    vi.spyOn(inClassShiftService, 'finishStudentExam').mockResolvedValue(undefined)

    function NoticeProbe() {
      const location = useLocation()
      const state = location.state as { notice?: string } | null
      return <div>{state?.notice ?? 'Đã quay về danh sách ca'}</div>
    }

    const router = createMemoryRouter(
      [
        { path: '/in-class-exam/session', element: <InClassExamSessionPage /> },
        { path: '/in-class-exam', element: <NoticeProbe /> },
      ],
      { initialEntries: ['/in-class-exam/session?shiftId=shift-sess-1'] },
    )

    saveInClassExamDrafts('shift-sess-1', {
      drafts: {
        'q-only-1': 'PUT có tính idempotent còn POST tạo mới tài nguyên mỗi lần gọi.',
      },
      questionIndex: 0,
    })

    render(<RouterProvider router={router} />)

    const textarea = await screen.findByLabelText(/Transcript để kiểm tra và chỉnh sửa/i)
    expect((textarea as HTMLTextAreaElement).value).toContain('PUT có tính idempotent')

    fireEvent.click(screen.getByRole('button', { name: /^Nộp bài$/i }))
    expect(await screen.findByText('Xác nhận nộp bài')).toBeInTheDocument()
    const submitButtons = screen.getAllByRole('button', { name: /^Nộp bài$/i })
    fireEvent.click(submitButtons[submitButtons.length - 1])

    expect(
      await screen.findByText(
        /Đã nộp bài\. Điểm số và nhận xét sẽ hiển thị sau khi giảng viên chốt bảng điểm ca thi\./i,
      ),
    ).toBeInTheDocument()
  })
})
