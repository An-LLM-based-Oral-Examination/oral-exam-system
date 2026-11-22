import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter, Route, RouterProvider, Routes, createMemoryRouter } from 'react-router-dom'
import { PracticeSessionPage } from '../PracticeSessionPage'
import { practiceService } from '@/services/practice.service'
import { storageService } from '@/services/storage.service'
import { toast } from '@/components/common/toastStore'
import { clearPracticeRuntimes, loadPracticeRuntime, savePracticeRuntime } from '@/utils/practiceSessionStorage'
import type { PracticeAnswerDetail, PracticeSessionDetail } from '@/types/practice.types'
import type { PracticeGradingError, PracticeHubStatus } from '@/hooks/useSignalR'

const STUDENT_ID = '00000000-0000-4000-8000-000000000002'
const SESSION_ID = '11111111-1111-4111-8111-111111111111'
const COURSE_ID = '22222222-2222-4222-8222-222222222222'
const QUESTION_ID = '33333333-3333-4333-8333-333333333333'
const ANSWER_ID = '44444444-4444-4444-8444-444444444444'

const mockSignalRState = vi.hoisted(() => ({
  status: 'offline' as PracticeHubStatus,
  isConnected: false,
  gradingResults: [] as PracticeAnswerDetail[],
  gradingErrors: [] as PracticeGradingError[],
  reconnectVersion: 0,
}))

const mockRecorderState = vi.hoisted(() => ({
  error: null as string | null,
  startRecording: vi.fn<() => Promise<void>>(),
  stopRecording: vi.fn<() => Promise<Blob>>(),
  cancelRecording: vi.fn(),
  clearError: vi.fn(),
  onSegment: undefined as ((blob: Blob, index: number) => void) | undefined,
}))

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({
    user: {
      id: STUDENT_ID,
      fullName: 'Sinh viên Test',
      email: 'sv@fpt.edu.vn',
      role: 'student',
    },
    isAuthenticated: true,
    isLoading: false,
    mustChangePassword: false,
    loginWithGoogle: vi.fn(),
    loginWithPassword: vi.fn(),
    logout: vi.fn(),
    refreshProfile: vi.fn(),
  }),
}))

vi.mock('@/hooks/useSignalR', () => ({
  useSignalR: () => ({
    status: mockSignalRState.status,
    isConnected: mockSignalRState.isConnected,
    gradingResults: mockSignalRState.gradingResults,
    gradingErrors: mockSignalRState.gradingErrors,
    reconnectVersion: mockSignalRState.reconnectVersion,
    clearGradingResult: vi.fn(),
  }),
}))

vi.mock('@/hooks/useAudioRecorder', async () => {
  const React = await import('react')
  return {
    useAudioRecorder: (options?: { onSegment?: (blob: Blob, index: number) => void }) => {
      const [isRecording, setIsRecording] = React.useState(false)
      React.useEffect(() => {
        mockRecorderState.onSegment = options?.onSegment
      }, [options?.onSegment])
      return {
        isRecording,
        error: mockRecorderState.error,
        startRecording: async () => {
          await mockRecorderState.startRecording()
          setIsRecording(true)
        },
        stopRecording: async () => {
          try {
            return await mockRecorderState.stopRecording()
          } finally {
            setIsRecording(false)
          }
        },
        cancelRecording: () => {
          mockRecorderState.cancelRecording()
          setIsRecording(false)
        },
        clearError: mockRecorderState.clearError,
      }
    },
  }
})

vi.mock('@/services/practice.service', () => ({
  practiceService: {
    getSession: vi.fn(),
    submitAnswer: vi.fn(),
    getNextQuestion: vi.fn(),
    completeSession: vi.fn(),
    submitBatchAnswers: vi.fn(),
  },
}))

vi.mock('@/services/storage.service', () => ({
  storageService: {
    uploadPracticeAudio: vi.fn(),
  },
}))

vi.mock('@/services/speech.service', async () => {
  const actual = await vi.importActual<typeof import('@/services/speech.service')>(
    '@/services/speech.service',
  )
  return {
    getTtsVoiceForCourse: () => 'vi-VN-HoaiMyNeural',
    playNaturalSpeech: vi.fn().mockResolvedValue(undefined),
    stopSpeechPlayback: vi.fn(),
    isSpeechPlaybackCancellation: () => false,
    sanitizeTextForTts: actual.sanitizeTextForTts,
  }
})

function buildSessionDetail(answers: PracticeAnswerDetail[] = []): PracticeSessionDetail {
  return {
    sessionId: SESSION_ID,
    courseId: COURSE_ID,
    courseCode: 'SWT301',
    courseName: 'Software Testing',
    studentId: STUDENT_ID,
    studentName: 'Sinh viên Test',
    practiceMode: 'per_question',
    status: 'in_progress',
    transcriptBufferSeconds: 60,
    startedAt: '2026-11-21T10:00:00.000Z',
    endedAt: null,
    questions: [
      {
        questionId: QUESTION_ID,
        content: 'Phân biệt Black-box testing và White-box testing.',
        rubricCriteria: ['SOLID', 'Clean Architecture'],
      },
    ],
    answers,
  }
}

function setupRuntime(rubricCriteria: string[] = ['SOLID', 'Clean Architecture']) {
  savePracticeRuntime({
    version: 1,
    sessionId: SESSION_ID,
    studentId: STUDENT_ID,
    transcriptBufferSeconds: 60,
    config: {
      courseId: COURSE_ID,
      courseCode: 'SWT301',
      courseName: 'Software Testing',
      mode: 'PER_QUESTION',
      selectedDifficulties: ['medium'],
      questionCount: null,
      microphoneName: 'Micro mặc định',
    },
    questions: [
      {
        questionId: QUESTION_ID,
        content: 'Phân biệt Black-box testing và White-box testing.',
        rubricCriteria,
      },
    ],
    drafts: [],
    currentQuestionIndex: 0,
    mainQuestionsCompleted: 0,
    questionDifficulties: { [QUESTION_ID]: 'medium' },
    activeFollowUp: null,
    pendingSubmission: null,
    pendingAnswerId: null,
    resolvedAnswer: null,
    submittedAnswerIdsByQuestion: {},
    fullSessionSubmitted: false,
    draftAnswerText: '',
    voiceBaselineTranscript: '',
    draftAnswerMode: 'voice',
    showBuffer: false,
    bufferDeadlineTimestamp: null,
  })
}

function renderPracticeSession() {
  const router = createMemoryRouter(
    [
      { path: '/practice/session', element: <PracticeSessionPage /> },
      { path: '/practice', element: <div>Danh sách luyện tập</div> },
    ],
    { initialEntries: [`/practice/session?session=${SESSION_ID}`] },
  )
  return render(<RouterProvider router={router} />)
}

describe('PracticeSessionPage M3 fixes', () => {
  beforeEach(() => {
    sessionStorage.clear()
    localStorage.clear()
    mockSignalRState.status = 'offline'
    mockSignalRState.isConnected = false
    mockSignalRState.gradingResults = []
    mockSignalRState.gradingErrors = []
    mockSignalRState.reconnectVersion = 0
    mockRecorderState.error = null
    mockRecorderState.startRecording.mockResolvedValue(undefined)
    mockRecorderState.stopRecording.mockResolvedValue(new Blob([new Uint8Array(3000)], { type: 'audio/webm' }))
    vi.mocked(practiceService.submitAnswer).mockResolvedValue({ answerId: ANSWER_ID })
    vi.mocked(practiceService.getSession).mockResolvedValue(buildSessionDetail())
  })

  afterEach(() => {
    vi.useRealTimers()
    cleanup()
    clearPracticeRuntimes()
    vi.clearAllMocks()
  })

  it('unblocks PER_QUESTION text submit when SignalR is offline, polls every 3s until graded, and stops polling once resolvedAnswer is set', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    setupRuntime()
    const pendingAnswer: PracticeAnswerDetail = {
      answerId: ANSWER_ID,
      questionId: QUESTION_ID,
      answerText: 'Black-box kiểm thử theo đặc tả còn White-box kiểm thử dựa trên cấu trúc mã nguồn.',
      isFollowUp: false,
      parentAnswerId: null,
      status: 'pending',
      submittedAt: '2026-11-21T10:01:00.000Z',
      totalScore: null,
      feedback: null,
      confidenceScore: null,
      isSuspicious: null,
      needsFollowUp: false,
      followUpPrompt: null,
      criteriaScores: [],
    }
    const gradedAnswer: PracticeAnswerDetail = {
      ...pendingAnswer,
      status: 'graded',
      totalScore: 8.5,
      feedback: 'Trả lời rõ ràng và chính xác.',
      confidenceScore: 0.94,
      isSuspicious: false,
      criteriaScores: [
        {
          criterionId: '55555555-5555-4555-8555-555555555555',
          criterionName: 'Phân biệt chính xác',
          score: 8.5,
          maxScore: 10,
          comment: 'Tốt.',
        },
      ],
    }
    vi.mocked(practiceService.getSession)
      .mockResolvedValueOnce(buildSessionDetail([pendingAnswer]))
      .mockResolvedValue(buildSessionDetail([gradedAnswer]))

    renderPracticeSession()

    expect(
      await screen.findByText(/Kết nối thời gian thực đang ngoại tuyến — bạn vẫn có thể nộp bài bình thường/i),
    ).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Chuyển sang trả lời bằng text/i }))
    const textarea = screen.getByLabelText(/Nhập câu trả lời bằng văn bản/i)
    fireEvent.change(textarea, {
      target: { value: 'Black-box kiểm thử theo đặc tả còn White-box kiểm thử dựa trên cấu trúc mã nguồn.' },
    })

    const submitButton = screen.getByRole('button', { name: /Gửi câu trả lời bằng văn bản/i })
    expect(submitButton).toBeEnabled()
    fireEvent.click(submitButton)

    await waitFor(() => {
      expect(practiceService.submitAnswer).toHaveBeenCalledWith(SESSION_ID, {
        questionId: QUESTION_ID,
        studentId: STUDENT_ID,
        answerText: 'Black-box kiểm thử theo đặc tả còn White-box kiểm thử dựa trên cấu trúc mã nguồn.',
        isFollowUp: false,
        parentAnswerId: null,
      })
    })

    expect(
      await screen.findByText(/Đang đồng bộ kết quả chấm định kỳ mỗi 3 giây \(chế độ dự phòng HTTP\)/i),
    ).toBeInTheDocument()

    await act(async () => {
      await vi.advanceTimersByTimeAsync(3_100)
    })
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1_000)
    })

    expect((await screen.findAllByText(/8\.5/)).length).toBeGreaterThanOrEqual(1)
    const callsAfterResolved = vi.mocked(practiceService.getSession).mock.calls.length

    await act(async () => {
      await vi.advanceTimersByTimeAsync(9_500)
    })
    expect(vi.mocked(practiceService.getSession).mock.calls.length).toBe(callsAfterResolved)
  })

  it('unblocks PER_QUESTION voice BufferScreen submit while SignalR is reconnecting', async () => {
    mockSignalRState.status = 'reconnecting'
    mockSignalRState.isConnected = false
    setupRuntime()
    vi.mocked(storageService.uploadPracticeAudio).mockResolvedValue({
      transcript: 'Black-box kiểm thử hành vi dựa trên đặc tả.',
    })

    renderPracticeSession()

    expect(
      await screen.findByText(/Đang kết nối lại kênh chấm điểm thời gian thực — bạn vẫn có thể nộp bài bình thường/i),
    ).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Bắt đầu ghi âm/i }))
    await waitFor(() => expect(mockRecorderState.startRecording).toHaveBeenCalledTimes(1))

    fireEvent.click(screen.getByRole('button', { name: /Dừng ghi âm/i }))

    const confirmBtn = await screen.findByRole('button', { name: /Xác nhận nộp ngay/i })
    expect(confirmBtn).toBeEnabled()
    fireEvent.click(confirmBtn)

    await waitFor(() => {
      expect(practiceService.submitAnswer).toHaveBeenCalledTimes(1)
    })
    expect(screen.queryByText(/Chưa kết nối được máy chấm thời gian thực/i)).not.toBeInTheDocument()
  })

  it('surfaces chunk failure warning in BufferScreen when a 20s STT segment returns 502 while preserving valid chunks', async () => {
    setupRuntime()
    const warnSpy = vi.spyOn(toast, 'warning').mockImplementation(() => '')
    vi.mocked(storageService.uploadPracticeAudio)
      .mockRejectedValueOnce({ status: 502, detail: 'Bad Gateway' })
      .mockResolvedValueOnce({ transcript: 'Dependency Inversion separates high-level policy.' })
      .mockResolvedValueOnce({ transcript: 'From low-level details.' })

    renderPracticeSession()
    fireEvent.click(screen.getByRole('button', { name: /Bắt đầu ghi âm/i }))
    await waitFor(() => expect(mockRecorderState.startRecording).toHaveBeenCalledTimes(1))

    act(() => {
      mockRecorderState.onSegment?.(new Blob([new Uint8Array(2500)], { type: 'audio/webm' }), 0)
      mockRecorderState.onSegment?.(new Blob([new Uint8Array(2500)], { type: 'audio/webm' }), 1)
    })

    fireEvent.click(screen.getByRole('button', { name: /Dừng ghi âm/i }))

    const bufferTextarea = await screen.findByLabelText(/Bản trả lời của bạn/i)
    expect((bufferTextarea as HTMLTextAreaElement).value).toBe(
      'Dependency Inversion separates high-level policy. From low-level details.',
    )
    expect(
      screen.getAllByText(/Có 1 đoạn ghi âm \(20 giây\) gặp lỗi nhận diện giọng nói/i).length,
    ).toBeGreaterThanOrEqual(1)
    expect(warnSpy).toHaveBeenCalledTimes(1)
  })

  it('does NOT trigger a false chunk warning when the tail segment returns 422 silence', async () => {
    setupRuntime()
    const warnSpy = vi.spyOn(toast, 'warning').mockImplementation(() => '')
    vi.mocked(storageService.uploadPracticeAudio)
      .mockResolvedValueOnce({ transcript: 'Equivalence partitioning reduces redundant test cases.' })
      .mockRejectedValueOnce({ status: 422, detail: 'Không nhận diện được giọng nói' })

    renderPracticeSession()
    fireEvent.click(screen.getByRole('button', { name: /Bắt đầu ghi âm/i }))
    await waitFor(() => expect(mockRecorderState.startRecording).toHaveBeenCalledTimes(1))

    act(() => {
      mockRecorderState.onSegment?.(new Blob([new Uint8Array(2500)], { type: 'audio/webm' }), 0)
    })

    fireEvent.click(screen.getByRole('button', { name: /Dừng ghi âm/i }))

    const bufferTextarea = await screen.findByLabelText(/Bản trả lời của bạn/i)
    expect((bufferTextarea as HTMLTextAreaElement).value).toBe(
      'Equivalence partitioning reduces redundant test cases.',
    )
    expect(screen.queryByText(/đoạn ghi âm \(20 giây\) gặp lỗi/i)).not.toBeInTheDocument()
    expect(warnSpy).not.toHaveBeenCalled()
  })

  it('strips (Y1)/(Y2) and deduplicates rubricCriteria in practiceVocabularyHint and omits hidden rubric DOM', async () => {
    setupRuntime([
      '(Y1) SOLID',
      'Clean Architecture',
      '(Y1) SOLID',
      'Câu hỏi quá dài trên năm mươi ký tự để kiểm tra bộ lọc?',
      'Short question?',
    ])
    vi.mocked(storageService.uploadPracticeAudio).mockResolvedValue({
      transcript: 'SOLID principles improve maintainability in Clean Architecture.',
    })

    renderPracticeSession()
    expect(screen.queryByText(/Tiêu chí cần thể hiện/i)).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Bắt đầu ghi âm/i }))
    await waitFor(() => expect(mockRecorderState.startRecording).toHaveBeenCalledTimes(1))
    fireEvent.click(screen.getByRole('button', { name: /Dừng ghi âm/i }))

    await waitFor(() => {
      expect(storageService.uploadPracticeAudio).toHaveBeenCalled()
    })
    const hint = vi.mocked(storageService.uploadPracticeAudio).mock.calls[0]?.[1]?.vocabularyHint ?? ''
    expect(hint).toContain('SOLID, Clean Architecture')
    expect(hint).not.toContain('(Y1)')
    expect(hint).not.toContain('?')
    expect(hint.match(/SOLID/g)?.length).toBe(1)
  })

  it('ignores Space shortcut on nested interactive controls and open ConfirmDialog', async () => {
    setupRuntime()
    renderPracticeSession()

    const stopButton = await screen.findByRole('button', { name: /Kết thúc phiên luyện tập/i })
    const iconSpan = document.createElement('span')
    iconSpan.setAttribute('role', 'switch')
    const nested = document.createElement('span')
    iconSpan.appendChild(nested)
    document.body.appendChild(iconSpan)

    fireEvent.keyDown(nested, { code: 'Space', key: ' ', bubbles: true })
    expect(mockRecorderState.startRecording).not.toHaveBeenCalled()
    iconSpan.remove()

    fireEvent.click(stopButton)
    expect(await screen.findByText(/Kết thúc phiên luyện tập\?/i)).toBeInTheDocument()
    fireEvent.keyDown(document.body, { code: 'Space', key: ' ', bubbles: true })
    expect(mockRecorderState.startRecording).not.toHaveBeenCalled()
  })

  it('closes ScorecardModal, clears resolvedAnswer/pendingAnswerId, shows 410 error, and exits cleanly when getNextQuestion returns 410 Gone', async () => {
    const resolvedAnswer: PracticeAnswerDetail = {
      answerId: ANSWER_ID,
      questionId: QUESTION_ID,
      answerText: 'Black-box kiểm thử theo đặc tả còn White-box kiểm thử cấu trúc mã nguồn.',
      isFollowUp: false,
      parentAnswerId: null,
      status: 'graded',
      submittedAt: '2026-11-21T10:01:00.000Z',
      totalScore: 8.5,
      feedback: 'Trả lời rõ ràng.',
      confidenceScore: 0.92,
      isSuspicious: false,
      needsFollowUp: false,
      followUpPrompt: null,
      criteriaScores: [
        {
          criterionId: '55555555-5555-4555-8555-555555555555',
          criterionName: 'Phân biệt chính xác',
          score: 8.5,
          maxScore: 10,
          comment: 'Tốt.',
        },
      ],
    }

    setupRuntime()
    const baseRuntime = loadPracticeRuntime(SESSION_ID)!
    savePracticeRuntime({
      ...baseRuntime,
      pendingAnswerId: ANSWER_ID,
      resolvedAnswer,
    })

    vi.mocked(practiceService.getSession).mockResolvedValue(buildSessionDetail([resolvedAnswer]))
    vi.mocked(practiceService.getNextQuestion).mockRejectedValueOnce({
      status: 410,
      detail: 'Phiên luyện tập đã hết hạn do quá 5 phút không tương tác.',
    })
    vi.mocked(practiceService.completeSession).mockRejectedValueOnce({
      status: 410,
      detail: 'Phiên luyện tập đã hết hạn.',
    })

    renderPracticeSession()

    expect(await screen.findByText(/Bảng điểm câu trả lời/i)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Câu chính tiếp theo/i }))

    await waitFor(() => {
      expect(screen.queryByText(/Bảng điểm câu trả lời/i)).not.toBeInTheDocument()
    })

    expect(
      await screen.findByText(/Phiên luyện tập đã hết hạn do quá 5 phút không tương tác\./i),
    ).toBeInTheDocument()

    const persisted = loadPracticeRuntime(SESSION_ID)
    expect(persisted).not.toBeNull()
    expect(persisted?.resolvedAnswer).toBeNull()
    expect(persisted?.pendingAnswerId).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: /Kết thúc phiên luyện tập/i }))
    fireEvent.click(await screen.findByRole('button', { name: /^Kết thúc phiên$/i }))

    expect(await screen.findByText('Danh sách luyện tập')).toBeInTheDocument()
  })

  it('keeps ScorecardModal open with errorMessage on transient 500 and closes cleanly on X without looping getNextQuestion', async () => {
    const resolvedAnswer: PracticeAnswerDetail = {
      answerId: ANSWER_ID,
      questionId: QUESTION_ID,
      answerText: 'Black-box kiểm thử theo đặc tả còn White-box kiểm thử cấu trúc mã nguồn.',
      isFollowUp: false,
      parentAnswerId: null,
      status: 'graded',
      submittedAt: '2026-11-21T10:01:00.000Z',
      totalScore: 8.5,
      feedback: 'Trả lời rõ ràng.',
      confidenceScore: 0.92,
      isSuspicious: false,
      needsFollowUp: false,
      followUpPrompt: null,
      criteriaScores: [
        {
          criterionId: '55555555-5555-4555-8555-555555555555',
          criterionName: 'Phân biệt chính xác',
          score: 8.5,
          maxScore: 10,
          comment: 'Tốt.',
        },
      ],
    }

    setupRuntime()
    const baseRuntime = loadPracticeRuntime(SESSION_ID)!
    savePracticeRuntime({
      ...baseRuntime,
      resolvedAnswer,
    })

    vi.mocked(practiceService.getNextQuestion).mockRejectedValueOnce({
      status: 500,
      detail: 'Dịch vụ bốc câu hỏi tạm thời gặp sự cố.',
    })

    renderPracticeSession()

    expect(await screen.findByText(/Bảng điểm câu trả lời/i)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Câu chính tiếp theo/i }))

    expect(
      await screen.findByText(/Dịch vụ bốc câu hỏi tạm thời gặp sự cố\./i),
    ).toBeInTheDocument()
    expect(screen.getByText(/Bảng điểm câu trả lời/i)).toBeInTheDocument()
    expect(practiceService.getNextQuestion).toHaveBeenCalledTimes(1)

    fireEvent.click(screen.getByRole('button', { name: /Đóng bảng điểm/i }))

    await waitFor(() => {
      expect(screen.queryByText(/Bảng điểm câu trả lời/i)).not.toBeInTheDocument()
    })
    expect(practiceService.getNextQuestion).toHaveBeenCalledTimes(1)
  })

  it('never resurrects ScorecardModal after 410 Gone in live pendingAnswerId flow or during the 900ms reveal window', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const gradedAnswer: PracticeAnswerDetail = {
      answerId: ANSWER_ID,
      questionId: QUESTION_ID,
      answerText: 'Black-box kiểm thử theo đặc tả còn White-box kiểm thử cấu trúc mã nguồn.',
      isFollowUp: false,
      parentAnswerId: null,
      status: 'graded',
      submittedAt: '2026-11-21T10:01:00.000Z',
      totalScore: 8.5,
      feedback: 'Trả lời rõ ràng.',
      confidenceScore: 0.94,
      isSuspicious: false,
      needsFollowUp: false,
      followUpPrompt: null,
      criteriaScores: [
        {
          criterionId: '55555555-5555-4555-8555-555555555555',
          criterionName: 'Phân biệt chính xác',
          score: 8.5,
          maxScore: 10,
          comment: 'Tốt.',
        },
      ],
    }

    setupRuntime()
    const baseRuntime = loadPracticeRuntime(SESSION_ID)!
    savePracticeRuntime({
      ...baseRuntime,
      pendingAnswerId: ANSWER_ID,
      resolvedAnswer: null,
    })

    vi.mocked(practiceService.getSession).mockResolvedValue(buildSessionDetail([gradedAnswer]))
    vi.mocked(practiceService.getNextQuestion).mockRejectedValueOnce({
      status: 410,
      detail: 'Phiên luyện tập đã hết hạn do quá 5 phút không tương tác.',
    })

    renderPracticeSession()

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1_100)
    })

    expect(await screen.findByText(/Bảng điểm câu trả lời/i)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Câu chính tiếp theo/i }))

    await waitFor(() => {
      expect(screen.queryByText(/Bảng điểm câu trả lời/i)).not.toBeInTheDocument()
    })
    expect(
      await screen.findByText(/Phiên luyện tập đã hết hạn do quá 5 phút không tương tác\./i),
    ).toBeInTheDocument()

    await act(async () => {
      await vi.advanceTimersByTimeAsync(5_000)
    })

    expect(document.querySelector('#scorecard-modal-title')).toBeNull()
    expect(screen.queryByText(/Bảng điểm câu trả lời/i)).not.toBeInTheDocument()
  })

  it('cancels the armed 900ms reveal timer if synchronizeSession receives 410 Gone before the modal opens', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const gradedAnswer: PracticeAnswerDetail = {
      answerId: ANSWER_ID,
      questionId: QUESTION_ID,
      answerText: 'Black-box kiểm thử theo đặc tả còn White-box kiểm thử cấu trúc mã nguồn.',
      isFollowUp: false,
      parentAnswerId: null,
      status: 'graded',
      submittedAt: '2026-11-21T10:01:00.000Z',
      totalScore: 8.5,
      feedback: 'Trả lời rõ ràng.',
      confidenceScore: 0.94,
      isSuspicious: false,
      needsFollowUp: false,
      followUpPrompt: null,
      criteriaScores: [
        {
          criterionId: '55555555-5555-4555-8555-555555555555',
          criterionName: 'Phân biệt chính xác',
          score: 8.5,
          maxScore: 10,
          comment: 'Tốt.',
        },
      ],
    }

    setupRuntime()
    const baseRuntime = loadPracticeRuntime(SESSION_ID)!
    savePracticeRuntime({
      ...baseRuntime,
      pendingAnswerId: ANSWER_ID,
      resolvedAnswer: null,
    })

    vi.mocked(practiceService.getSession)
      .mockResolvedValueOnce(buildSessionDetail([gradedAnswer]))
      .mockRejectedValueOnce({
        status: 410,
        detail: 'Phiên luyện tập đã hết hạn.',
      })

    const ui = (
      <MemoryRouter initialEntries={[`/practice/session?session=${SESSION_ID}`]}>
        <Routes>
          <Route path="/practice/session" element={<PracticeSessionPage />} />
          <Route path="/practice" element={<div>Danh sách luyện tập</div>} />
        </Routes>
      </MemoryRouter>
    )
    const { rerender } = render(ui)

    await act(async () => {
      await vi.advanceTimersByTimeAsync(100)
    })
    expect(await screen.findByText(/Đã chấm xong/i)).toBeInTheDocument()
    expect(document.querySelector('#scorecard-modal-title')).toBeNull()

    mockSignalRState.reconnectVersion = 1
    rerender(
      <MemoryRouter initialEntries={[`/practice/session?session=${SESSION_ID}`]}>
        <Routes>
          <Route path="/practice/session" element={<PracticeSessionPage />} />
          <Route path="/practice" element={<div>Danh sách luyện tập</div>} />
        </Routes>
      </MemoryRouter>,
    )

    await act(async () => {
      await vi.advanceTimersByTimeAsync(100)
    })
    expect(await screen.findByText(/Phiên luyện tập đã hết hạn\./i)).toBeInTheDocument()

    await act(async () => {
      await vi.advanceTimersByTimeAsync(5_000)
    })
    expect(document.querySelector('#scorecard-modal-title')).toBeNull()
    expect(screen.queryByText(/Bảng điểm câu trả lời/i)).not.toBeInTheDocument()
  })

  it('clears pendingAnswerId on grading completion and renders enabled recovery controls after 500 + Đóng bảng điểm', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const nextQuestionId = '66666666-6666-4666-8666-666666666666'
    const gradedAnswer: PracticeAnswerDetail = {
      answerId: ANSWER_ID,
      questionId: QUESTION_ID,
      answerText: 'Black-box kiểm thử theo đặc tả còn White-box kiểm thử cấu trúc mã nguồn.',
      isFollowUp: false,
      parentAnswerId: null,
      status: 'graded',
      submittedAt: '2026-11-21T10:01:00.000Z',
      totalScore: 8.5,
      feedback: 'Trả lời rõ ràng.',
      confidenceScore: 0.94,
      isSuspicious: false,
      needsFollowUp: false,
      followUpPrompt: null,
      criteriaScores: [
        {
          criterionId: '55555555-5555-4555-8555-555555555555',
          criterionName: 'Phân biệt chính xác',
          score: 8.5,
          maxScore: 10,
          comment: 'Tốt.',
        },
      ],
    }

    setupRuntime()
    const baseRuntime = loadPracticeRuntime(SESSION_ID)!
    savePracticeRuntime({
      ...baseRuntime,
      pendingAnswerId: ANSWER_ID,
      resolvedAnswer: null,
    })

    vi.mocked(practiceService.getSession).mockResolvedValue(buildSessionDetail([gradedAnswer]))
    vi.mocked(practiceService.getNextQuestion)
      .mockRejectedValueOnce({
        status: 500,
        detail: 'Dịch vụ bốc câu hỏi tạm thời gặp sự cố.',
      })
      .mockResolvedValueOnce({
        hasMoreQuestions: true,
        message: null,
        question: {
          id: nextQuestionId,
          content: 'Giải thích kỹ thuật Boundary Value Analysis.',
          difficulty: 'medium',
          questionOrder: 2,
          rubricCriteria: ['Boundary Values'],
        },
      })

    renderPracticeSession()

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1_100)
    })

    expect(await screen.findByText(/Bảng điểm câu trả lời/i)).toBeInTheDocument()
    expect(loadPracticeRuntime(SESSION_ID)?.pendingAnswerId).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: /Câu chính tiếp theo/i }))

    expect(
      await screen.findByText(/Dịch vụ bốc câu hỏi tạm thời gặp sự cố\./i),
    ).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Đóng bảng điểm/i }))

    await waitFor(() => {
      expect(screen.queryByText(/Bảng điểm câu trả lời/i)).not.toBeInTheDocument()
    })

    expect(screen.queryByText(/AI đang chấm điểm/i)).not.toBeInTheDocument()

    const reopenBtn = screen.getByRole('button', { name: /Xem lại bảng điểm/i })
    const retryNextBtn = screen.getByRole('button', { name: /Thử chuyển câu tiếp theo/i })
    expect(reopenBtn).toBeEnabled()
    expect(retryNextBtn).toBeEnabled()

    fireEvent.click(reopenBtn)
    expect(await screen.findByText(/Bảng điểm câu trả lời/i)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Đóng bảng điểm/i }))
    await waitFor(() => {
      expect(screen.queryByText(/Bảng điểm câu trả lời/i)).not.toBeInTheDocument()
    })

    fireEvent.click(screen.getByRole('button', { name: /Thử chuyển câu tiếp theo/i }))

    expect(
      await screen.findByText(/Giải thích kỹ thuật Boundary Value Analysis\./i),
    ).toBeInTheDocument()
    expect(practiceService.getNextQuestion).toHaveBeenCalledTimes(2)
    expect(loadPracticeRuntime(SESSION_ID)?.resolvedAnswer).toBeNull()
    expect(screen.queryByRole('button', { name: /Thử chuyển câu tiếp theo/i })).not.toBeInTheDocument()
  })

  it('locks recording, mode switch, Space, and submit after 500 + Đóng bảng điểm and on 410 expiration without wiping pageError or duplicating error toast', async () => {
    const errorToastSpy = vi.spyOn(toast, 'error').mockImplementation(() => '')
    const resolvedAnswer: PracticeAnswerDetail = {
      answerId: ANSWER_ID,
      questionId: QUESTION_ID,
      answerText: 'Black-box kiểm thử theo đặc tả còn White-box kiểm thử cấu trúc mã nguồn.',
      isFollowUp: false,
      parentAnswerId: null,
      status: 'graded',
      submittedAt: '2026-11-21T10:01:00.000Z',
      totalScore: 8.5,
      feedback: 'Trả lời rõ ràng.',
      confidenceScore: 0.92,
      isSuspicious: false,
      needsFollowUp: false,
      followUpPrompt: null,
      criteriaScores: [
        {
          criterionId: '55555555-5555-4555-8555-555555555555',
          criterionName: 'Phân biệt chính xác',
          score: 8.5,
          maxScore: 10,
          comment: 'Tốt.',
        },
      ],
    }

    setupRuntime()
    const baseRuntime = loadPracticeRuntime(SESSION_ID)!
    savePracticeRuntime({
      ...baseRuntime,
      resolvedAnswer,
    })

    vi.mocked(practiceService.getNextQuestion)
      .mockRejectedValueOnce({
        status: 500,
        detail: 'Dịch vụ bốc câu hỏi tạm thời gặp sự cố.',
      })
      .mockRejectedValueOnce({
        status: 410,
        detail: 'Phiên luyện tập đã hết hạn do quá 5 phút không tương tác.',
      })

    renderPracticeSession()
    expect(await screen.findByText(/Bảng điểm câu trả lời/i)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Câu chính tiếp theo/i }))
    expect(
      await screen.findByText(/Dịch vụ bốc câu hỏi tạm thời gặp sự cố\./i),
    ).toBeInTheDocument()
    expect(errorToastSpy).toHaveBeenCalledTimes(1)

    fireEvent.click(screen.getByRole('button', { name: /Đóng bảng điểm/i }))
    await waitFor(() => {
      expect(screen.queryByText(/Bảng điểm câu trả lời/i)).not.toBeInTheDocument()
    })

    const recordBtn = screen.getByRole('button', { name: /Bắt đầu ghi âm/i })
    const switchModeBtn = screen.getByRole('button', { name: /Chuyển sang trả lời bằng text/i })
    expect(recordBtn).toBeDisabled()
    expect(switchModeBtn).toBeDisabled()

    fireEvent.click(recordBtn)
    fireEvent.click(switchModeBtn)
    fireEvent.keyDown(document.body, { code: 'Space', key: ' ', bubbles: true })

    expect(mockRecorderState.startRecording).not.toHaveBeenCalled()
    expect(practiceService.submitAnswer).not.toHaveBeenCalled()
    expect(screen.getByText(/Dịch vụ bốc câu hỏi tạm thời gặp sự cố\./i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Bắt đầu ghi âm/i })).toBeInTheDocument()

    const reopenBtn = screen.getByRole('button', { name: /Xem lại bảng điểm/i })
    const retryNextBtn = screen.getByRole('button', { name: /Thử chuyển câu tiếp theo/i })
    expect(reopenBtn).toBeEnabled()
    expect(retryNextBtn).toBeEnabled()

    fireEvent.click(reopenBtn)
    expect(await screen.findByText(/Bảng điểm câu trả lời/i)).toBeInTheDocument()
    expect(errorToastSpy).toHaveBeenCalledTimes(1)

    fireEvent.click(screen.getByRole('button', { name: /Đóng bảng điểm/i }))
    await waitFor(() => {
      expect(screen.queryByText(/Bảng điểm câu trả lời/i)).not.toBeInTheDocument()
    })

    fireEvent.click(screen.getByRole('button', { name: /Thử chuyển câu tiếp theo/i }))
    expect(
      await screen.findByText(/Phiên luyện tập đã hết hạn do quá 5 phút không tương tác\./i),
    ).toBeInTheDocument()

    expect(screen.getByRole('button', { name: /Bắt đầu ghi âm/i })).toBeDisabled()
    expect(screen.getByRole('button', { name: /Chuyển sang trả lời bằng text/i })).toBeDisabled()
    fireEvent.keyDown(document.body, { code: 'Space', key: ' ', bubbles: true })
    expect(mockRecorderState.startRecording).not.toHaveBeenCalled()
    expect(
      screen.getByText(/Phiên luyện tập đã hết hạn do quá 5 phút không tương tác\./i),
    ).toBeInTheDocument()
  })

  it('stops FULL_SESSION polling once ScorecardModal opens and ignores subsequent reconnectVersion or timer advances', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const secondQuestionId = '66666666-6666-4666-8666-666666666666'
    const fullAnswers: PracticeAnswerDetail[] = [
      {
        answerId: ANSWER_ID,
        questionId: QUESTION_ID,
        answerText: 'Câu trả lời 1 đầy đủ cho Black-box và White-box testing.',
        isFollowUp: false,
        parentAnswerId: null,
        status: 'graded',
        submittedAt: '2026-11-21T10:01:00.000Z',
        totalScore: 8.0,
        feedback: 'Câu 1 tốt.',
        confidenceScore: 0.94,
        isSuspicious: false,
        needsFollowUp: false,
        followUpPrompt: null,
        criteriaScores: [
          {
            criterionId: '55555555-5555-4555-8555-555555555555',
            criterionName: 'Phân biệt chính xác',
            score: 8.0,
            maxScore: 10,
            comment: 'Tốt.',
          },
        ],
      },
      {
        answerId: '77777777-7777-4777-8777-777777777777',
        questionId: secondQuestionId,
        answerText: 'Câu trả lời 2 đầy đủ cho Boundary Value Analysis.',
        isFollowUp: false,
        parentAnswerId: null,
        status: 'graded',
        submittedAt: '2026-11-21T10:02:00.000Z',
        totalScore: 9.0,
        feedback: 'Câu 2 xuất sắc.',
        confidenceScore: 0.96,
        isSuspicious: false,
        needsFollowUp: false,
        followUpPrompt: null,
        criteriaScores: [
          {
            criterionId: '88888888-8888-4888-8888-888888888888',
            criterionName: 'Xác định giá trị biên',
            score: 9.0,
            maxScore: 10,
            comment: 'Rất tốt.',
          },
        ],
      },
    ]

    savePracticeRuntime({
      version: 1,
      sessionId: SESSION_ID,
      studentId: STUDENT_ID,
      transcriptBufferSeconds: 60,
      config: {
        courseId: COURSE_ID,
        courseCode: 'SWT301',
        courseName: 'Software Testing',
        mode: 'FULL_SESSION',
        selectedDifficulties: ['easy', 'medium'],
        questionCount: 2,
        microphoneName: 'Micro mặc định',
      },
      questions: [
        {
          questionId: QUESTION_ID,
          content: 'Phân biệt Black-box testing và White-box testing.',
          rubricCriteria: ['Phân biệt chính xác'],
        },
        {
          questionId: secondQuestionId,
          content: 'Trình bày kỹ thuật Boundary Value Analysis.',
          rubricCriteria: ['Xác định giá trị biên'],
        },
      ],
      drafts: [],
      currentQuestionIndex: 1,
      mainQuestionsCompleted: 2,
      questionDifficulties: {
        [QUESTION_ID]: 'easy',
        [secondQuestionId]: 'medium',
      },
      activeFollowUp: null,
      pendingSubmission: null,
      pendingAnswerId: null,
      resolvedAnswer: null,
      submittedAnswerIdsByQuestion: {
        [QUESTION_ID]: fullAnswers[0].answerId,
        [secondQuestionId]: fullAnswers[1].answerId,
      },
      fullSessionSubmitted: true,
      draftAnswerText: '',
      voiceBaselineTranscript: '',
      draftAnswerMode: 'voice',
      showBuffer: false,
      bufferDeadlineTimestamp: null,
    })

    const fullSessionDetail: PracticeSessionDetail = {
      ...buildSessionDetail(fullAnswers),
      practiceMode: 'full_session',
      status: 'completed',
      questions: [
        {
          questionId: QUESTION_ID,
          content: 'Phân biệt Black-box testing và White-box testing.',
          rubricCriteria: ['Phân biệt chính xác'],
        },
        {
          questionId: secondQuestionId,
          content: 'Trình bày kỹ thuật Boundary Value Analysis.',
          rubricCriteria: ['Xác định giá trị biên'],
        },
      ],
    }

    vi.mocked(practiceService.getSession)
      .mockResolvedValueOnce(fullSessionDetail)
      .mockRejectedValue({
        status: 410,
        detail: 'Phiên luyện tập đã hết hạn.',
      })

    const ui = (
      <MemoryRouter initialEntries={[`/practice/session?session=${SESSION_ID}`]}>
        <Routes>
          <Route path="/practice/session" element={<PracticeSessionPage />} />
          <Route path="/practice" element={<div>Danh sách luyện tập</div>} />
        </Routes>
      </MemoryRouter>
    )
    const { rerender } = render(ui)

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1_100)
    })

    expect(await screen.findByText(/Bảng điểm tổng kết phiên luyện trọn gói/i)).toBeInTheDocument()
    const callsWhenModalOpened = vi.mocked(practiceService.getSession).mock.calls.length

    await act(async () => {
      await vi.advanceTimersByTimeAsync(9_500)
    })
    expect(vi.mocked(practiceService.getSession).mock.calls.length).toBe(callsWhenModalOpened)
    expect(screen.getByText(/Bảng điểm tổng kết phiên luyện trọn gói/i)).toBeInTheDocument()

    mockSignalRState.reconnectVersion = 1
    rerender(
      <MemoryRouter initialEntries={[`/practice/session?session=${SESSION_ID}`]}>
        <Routes>
          <Route path="/practice/session" element={<PracticeSessionPage />} />
          <Route path="/practice" element={<div>Danh sách luyện tập</div>} />
        </Routes>
      </MemoryRouter>,
    )

    await act(async () => {
      await vi.advanceTimersByTimeAsync(500)
    })
    expect(vi.mocked(practiceService.getSession).mock.calls.length).toBe(callsWhenModalOpened)
    expect(screen.getByText(/Bảng điểm tổng kết phiên luyện trọn gói/i)).toBeInTheDocument()
  })

  it('preserves skipFollowUp intent after Bỏ qua câu hỏi đào sâu hits 500 + Đóng bảng điểm when clicking Thử chuyển câu tiếp theo (PROBE 3c-2)', async () => {
    const nextQuestionId = '66666666-6666-4666-8666-666666666666'
    const followUpCandidateAnswer: PracticeAnswerDetail = {
      answerId: ANSWER_ID,
      questionId: QUESTION_ID,
      answerText: 'White-box testing kiểm thử cấu trúc mã nguồn.',
      isFollowUp: false,
      parentAnswerId: null,
      status: 'graded',
      submittedAt: '2026-11-21T10:01:00.000Z',
      totalScore: 6.0,
      feedback: 'Cần nêu ví dụ cụ thể.',
      confidenceScore: 0.9,
      isSuspicious: false,
      needsFollowUp: true,
      followUpPrompt: 'Hãy nêu ví dụ cụ thể về White-box testing?',
      criteriaScores: [
        {
          criterionId: '55555555-5555-4555-8555-555555555555',
          criterionName: 'Phân biệt chính xác',
          score: 6.0,
          maxScore: 10,
          comment: 'Cần bổ sung ví dụ.',
        },
      ],
    }

    setupRuntime()
    const baseRuntime = loadPracticeRuntime(SESSION_ID)!
    savePracticeRuntime({
      ...baseRuntime,
      resolvedAnswer: followUpCandidateAnswer,
    })

    vi.mocked(practiceService.getNextQuestion)
      .mockRejectedValueOnce({
        status: 500,
        detail: 'Dịch vụ bốc câu hỏi tạm thời gặp sự cố.',
      })
      .mockResolvedValueOnce({
        hasMoreQuestions: true,
        message: null,
        question: {
          id: nextQuestionId,
          content: 'Giải thích kỹ thuật Boundary Value Analysis.',
          difficulty: 'medium',
          questionOrder: 2,
          rubricCriteria: ['Boundary Values'],
        },
      })

    renderPracticeSession()
    expect(await screen.findByText(/Bảng điểm câu trả lời/i)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Bỏ qua câu hỏi đào sâu/i }))
    expect(
      await screen.findByText(/Dịch vụ bốc câu hỏi tạm thời gặp sự cố\./i),
    ).toBeInTheDocument()
    expect(practiceService.getNextQuestion).toHaveBeenCalledTimes(1)

    fireEvent.click(screen.getByRole('button', { name: /Đóng bảng điểm/i }))
    await waitFor(() => {
      expect(screen.queryByText(/Bảng điểm câu trả lời/i)).not.toBeInTheDocument()
    })

    fireEvent.click(screen.getByRole('button', { name: /Thử chuyển câu tiếp theo/i }))

    expect(
      await screen.findByText(/Giải thích kỹ thuật Boundary Value Analysis\./i),
    ).toBeInTheDocument()
    expect(practiceService.getNextQuestion).toHaveBeenCalledTimes(2)
    expect(screen.queryByText(/Hãy nêu ví dụ cụ thể về White-box testing\?/i)).not.toBeInTheDocument()
    expect(loadPracticeRuntime(SESSION_ID)?.activeFollowUp).toBeNull()
  })
})
