import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter, RouterProvider, createMemoryRouter } from 'react-router-dom'
import { StudentInClassExamPage } from '../StudentInClassExamPage'
import { InClassExamSessionPage } from '../InClassExamSessionPage'
import { inClassShiftService } from '@/services/inClassShift.service'
import {
  clearInClassExamDrafts,
  clearInClassExamSession,
  saveInClassExamDrafts,
} from '@/utils/inClassExamSessionStorage'
import type { InClassShift } from '@/types/inClassShift.types'

vi.mock('@/services/inClassShift.service', () => ({
  inClassShiftService: {
    listStudentShifts: vi.fn(),
    getStudentShift: vi.fn(),
    enterStudentShift: vi.fn(),
    resumeStudentShift: vi.fn(),
    listStudentResults: vi.fn(),
    submitStudentAnswer: vi.fn(),
    requestNextFollowUp: vi.fn(),
    finishStudentExam: vi.fn(),
  },
}))

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

function buildShift(overrides: Partial<InClassShift>): InClassShift {
  return {
    id: 'shift-1',
    classId: 'cls-1',
    className: 'PRN231 - SE1801',
    courseCode: 'PRN231',
    title: 'Ca kiểm tra mặc định',
    description: '',
    status: 'ACTIVE',
    isLocked: false,
    startTime: '2026-11-16T07:00:00Z',
    endTime: '2026-11-16T09:00:00Z',
    durationMinutes: 120,
    examDurationMinutes: 20,
    questionCount: 2,
    hasFollowUp: false,
    maxFollowUpQuestions: 0,
    isAutoPublishGrades: false,
    questionPoolMode: 'SELECTED_CLOS',
    selectedDifficulties: ['easy', 'medium'],
    selectedCloIds: ['clo-1', 'clo-2'],
    selectedCloCodes: ['CLO1', 'CLO2'],
    passwordRotationSeconds: 300,
    gracePeriodSeconds: 60,
    joinedCount: 5,
    submittedCount: 2,
    createdAt: '2026-11-16T06:30:00Z',
    ...overrides,
  }
}

describe('Student MF-05 Portal & Session Refresh Resilience (RC-3)', () => {
  beforeEach(() => {
    sessionStorage.clear()
    localStorage.clear()
  })

  afterEach(() => {
    cleanup()
    clearInClassExamSession()
    clearInClassExamDrafts('shift-refresh-1')
    vi.resetAllMocks()
  })

  it('renders distinct shift/grading statuses on StudentInClassExamPage', async () => {
    vi.mocked(inClassShiftService.listStudentShifts).mockResolvedValue([
      buildShift({ id: 'sh-sched', title: 'Ca Chưa Mở', status: 'SCHEDULED' }),
      buildShift({
        id: 'sh-grading',
        title: 'Ca Đang Chấm',
        status: 'ACTIVE',
        myParticipationStatus: 'SUBMITTED',
      }),
      buildShift({
        id: 'sh-audited',
        title: 'Ca Đã Hậu Kiểm',
        status: 'CLOSED',
        myParticipationStatus: 'AUDITED',
      }),
      buildShift({
        id: 'sh-locked',
        title: 'Ca Đã Khóa Sổ',
        status: 'FINALIZED',
        isLocked: true,
        myParticipationStatus: 'FINALIZED',
      }),
    ])

    render(
      <MemoryRouter initialEntries={['/in-class-exam']}>
        <StudentInClassExamPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Ca Chưa Mở')).toBeInTheDocument()
    expect(screen.getAllByText('Ca chưa mở').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText('Đang chấm AI')).toBeInTheDocument()
    expect(screen.getByText('Đã hậu kiểm')).toBeInTheDocument()
    expect(screen.getByText('Đã khóa · Chốt điểm')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Xem kết quả chốt/i })).toBeInTheDocument()
  })

  it('restores fixed BE-assigned questions, submitted state, and unsent local draft on F5 refresh without re-drawing', async () => {
    const activeShift = buildShift({
      id: 'shift-refresh-1',
      title: 'Ca Kiểm Tra Giữ Bộ Câu Cố Định',
      status: 'ACTIVE',
      questionCount: 2,
    })

    // Giả lập trạng thái sau khi F5: sessionStorage bị xóa nhưng URL có ?shiftId=... và localStorage còn nháp câu 2 chưa gửi
    saveInClassExamDrafts('shift-refresh-1', {
      drafts: {
        'q-fixed-2': 'Bản nháp câu 2 đang gõ dở trước khi F5',
      },
      questionIndex: 1,
    })

    const endsIso = new Date(Date.now() + 14 * 60_000).toISOString()
    vi.mocked(inClassShiftService.getStudentShift).mockResolvedValue(activeShift)
    vi.mocked(inClassShiftService.resumeStudentShift).mockResolvedValue({
      questions: [
        {
          questionId: 'q-fixed-1',
          title: 'CLO1 · Clean Architecture',
          content: 'Trình bày chiều phụ thuộc giữa Domain và Infrastructure?',
        },
        {
          questionId: 'q-fixed-2',
          title: 'CLO2 · CQRS Pattern',
          content: 'Khi nào tách riêng Command và Query trong ứng dụng?',
        },
      ],
      joinedAt: new Date(Date.now() - 6 * 60_000).toISOString(),
      examEndsAt: endsIso,
      remainingSeconds: 840,
      timedOut: false,
      status: 'IN_PROGRESS',
    })
    vi.mocked(inClassShiftService.listStudentResults).mockResolvedValue([
      {
        submissionId: 'sub-1',
        questionId: 'q-fixed-1',
        title: 'CLO1 · Clean Architecture',
        content: 'Trình bày chiều phụ thuộc giữa Domain và Infrastructure?',
        transcript: 'Domain nằm ở tâm và không phụ thuộc Infrastructure.',
        aiScore: null,
        aiFeedback: null,
        finalScore: null,
        lecturerNotes: null,
        status: 'SUBMITTED',
      },
    ])

    const router = createMemoryRouter(
      [
        { path: '/in-class-exam/session', element: <InClassExamSessionPage /> },
        { path: '/in-class-exam', element: <div>Trang danh sách ca</div> },
      ],
      { initialEntries: ['/in-class-exam/session?shiftId=shift-refresh-1'] },
    )

    render(<RouterProvider router={router} />)

    // Đã khôi phục đúng Câu 2 cùng bản nháp local chưa gửi mà không bốc lại ở FE
    expect(
      await screen.findByText(/Khi nào tách riêng Command và Query trong ứng dụng/i),
    ).toBeInTheDocument()
    const draftArea = screen.getByLabelText(/Transcript để kiểm tra và chỉnh sửa/i) as HTMLTextAreaElement
    expect(draftArea.value).toBe('Bản nháp câu 2 đang gõ dở trước khi F5')
    expect(draftArea.disabled).toBe(false)

    // Chuyển về Câu 1 (đã nộp trên BE trước khi F5) -> transcript từ BE được khôi phục và bị khóa sửa
    fireEvent.click(screen.getByRole('button', { name: /Câu trước/i }))
    await waitFor(() => {
      expect(
        screen.getByText(/Trình bày chiều phụ thuộc giữa Domain và Infrastructure/i),
      ).toBeInTheDocument()
    })
    const submittedArea = screen.getByLabelText(/Transcript để kiểm tra và chỉnh sửa/i) as HTMLTextAreaElement
    expect(submittedArea.value).toBe('Domain nằm ở tâm và không phụ thuộc Infrastructure.')
    expect(submittedArea.disabled).toBe(true)

    // Mất mạng tạm thời hiển thị cảnh báo bình tĩnh, không làm mất phiên thi
    act(() => {
      window.dispatchEvent(new Event('offline'))
    })
    expect(
      await screen.findByText(/Mất kết nối mạng tạm thời — bản nháp vẫn được lưu trên thiết bị/i),
    ).toBeInTheDocument()

    expect(inClassShiftService.enterStudentShift).not.toHaveBeenCalled()
    expect(inClassShiftService.resumeStudentShift).toHaveBeenCalledWith('shift-refresh-1')
  })
})
