import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { RouterProvider, createMemoryRouter } from 'react-router-dom'
import { InClassExamSessionPage } from '../InClassExamSessionPage'
import { inClassShiftService } from '@/services/inClassShift.service'
import { transcribeSpeech } from '@/services/speech.service'
import {
  clearInClassExamDrafts,
  clearInClassExamSession,
  saveInClassExamDrafts,
  saveInClassExamSession,
} from '@/utils/inClassExamSessionStorage'
import type { InClassShift, StudentExamQuestion } from '@/types/inClassShift.types'

const mockSpeechState = vi.hoisted(() => ({
  isSupported: false,
  isListening: false,
  transcript: '',
  error: null as string | null,
  startListening: vi.fn(),
  stopListening: vi.fn(),
  resetTranscript: vi.fn(),
  listeners: new Set<() => void>(),
}))

function updateMockSpeech(
  patch: Partial<Pick<typeof mockSpeechState, 'isSupported' | 'isListening' | 'transcript' | 'error'>>,
) {
  Object.assign(mockSpeechState, patch)
  mockSpeechState.listeners.forEach((notify) => notify())
}

const mockRecorderState = vi.hoisted(() => ({
  error: null as string | null,
  startRecording: vi.fn<() => Promise<void>>(),
  stopRecording: vi.fn<() => Promise<Blob>>(),
  cancelRecording: vi.fn(),
  clearError: vi.fn(),
  onSegment: undefined as ((blob: Blob, index: number) => void) | undefined,
}))

vi.mock('@/services/inClassShift.service', () => ({
  inClassShiftService: {
    getStudentShift: vi.fn(),
    resumeStudentShift: vi.fn(),
    listStudentResults: vi.fn(),
    submitStudentAnswer: vi.fn(),
    requestNextFollowUp: vi.fn(),
    finishStudentExam: vi.fn(),
  },
}))

vi.mock('@/hooks/useSpeechRecognition', async () => {
  const React = await import('react')
  return {
    useSpeechRecognition: () => {
      const [, setRevision] = React.useState(0)
      React.useEffect(() => {
        const notify = () => setRevision((value) => value + 1)
        mockSpeechState.listeners.add(notify)
        return () => {
          mockSpeechState.listeners.delete(notify)
        }
      }, [])
      return {
        isSupported: mockSpeechState.isSupported,
        isListening: mockSpeechState.isListening,
        transcript: mockSpeechState.transcript,
        error: mockSpeechState.error,
        startListening: mockSpeechState.startListening,
        stopListening: mockSpeechState.stopListening,
        resetTranscript: mockSpeechState.resetTranscript,
      }
    },
  }
})

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

vi.mock('@/services/speech.service', () => ({
  getSpeechRecognitionLangForCourse: () => 'vi-VN',
  getTtsVoiceForCourse: () => 'vi-VN',
  playNaturalSpeech: vi.fn().mockResolvedValue(undefined),
  stopSpeechPlayback: vi.fn(),
  transcribeSpeech: vi.fn(),
}))

function buildShift(overrides: Partial<InClassShift> = {}): InClassShift {
  return {
    id: 'shift-m2-1',
    classId: 'cls-1',
    className: 'PRN231 - SE1801',
    courseCode: 'PRN231',
    title: 'Ca thi vấn đáp Safari/Firefox',
    description: '',
    status: 'ACTIVE',
    isLocked: false,
    startTime: '2026-11-18T05:00:00Z',
    endTime: '2026-11-18T06:00:00Z',
    durationMinutes: 60,
    examDurationMinutes: 20,
    questionCount: 1,
    hasFollowUp: false,
    maxFollowUpQuestions: 0,
    isAutoPublishGrades: false,
    questionPoolMode: 'ALL_COURSE_BANK',
    selectedDifficulties: ['easy'],
    selectedCloIds: [],
    passwordRotationSeconds: 300,
    gracePeriodSeconds: 60,
    joinedCount: 1,
    submittedCount: 0,
    createdAt: '2026-11-18T04:50:00Z',
    ...overrides,
  }
}

function setupSession(questions: StudentExamQuestion[], shiftOverrides: Partial<InClassShift> = {}) {
  const shift = buildShift({ questionCount: questions.length, ...shiftOverrides })
  const nowMs = Date.now()
  const endsAtMs = nowMs + 15 * 60_000
  saveInClassExamSession({
    shift,
    questions,
    startedAt: nowMs,
    examEndsAt: endsAtMs,
    joinedAt: new Date(nowMs).toISOString(),
  })
  vi.mocked(inClassShiftService.getStudentShift).mockResolvedValue(shift)
  vi.mocked(inClassShiftService.resumeStudentShift).mockResolvedValue({
    questions,
    joinedAt: new Date(nowMs).toISOString(),
    examEndsAt: new Date(endsAtMs).toISOString(),
    remainingSeconds: 900,
    timedOut: false,
    status: 'IN_PROGRESS',
  })
  vi.mocked(inClassShiftService.listStudentResults).mockResolvedValue([])
  vi.mocked(inClassShiftService.submitStudentAnswer).mockResolvedValue({
    submissionId: 'sub-m2-1',
    shiftId: shift.id,
    questionId: questions[0].questionId,
    status: 'SUBMITTED',
    isFollowUp: false,
    parentSubmissionId: null,
    followUpIndex: 0,
  })
  vi.mocked(inClassShiftService.finishStudentExam).mockResolvedValue(undefined)

  const router = createMemoryRouter(
    [
      { path: '/in-class-exam/session', element: <InClassExamSessionPage /> },
      { path: '/in-class-exam', element: <div>Danh sách ca thi</div> },
    ],
    { initialEntries: [`/in-class-exam/session?shiftId=${shift.id}`] },
  )
  return { shift, router }
}

describe('InClassExamSessionPage M2 cross-device fallback & question images', () => {
  beforeEach(() => {
    sessionStorage.clear()
    localStorage.clear()
    mockSpeechState.isSupported = false
    mockSpeechState.isListening = false
    mockSpeechState.transcript = ''
    mockSpeechState.error = null
    mockSpeechState.startListening = vi.fn()
    mockSpeechState.stopListening = vi.fn()
    mockSpeechState.resetTranscript = vi.fn(() => {
      updateMockSpeech({ transcript: '' })
    })

    mockRecorderState.error = null
    mockRecorderState.startRecording = vi.fn(async () => undefined)
    mockRecorderState.stopRecording = vi.fn(async () => new Blob(['mp4-audio'], { type: 'audio/mp4' }))
    mockRecorderState.cancelRecording = vi.fn()
    mockRecorderState.clearError = vi.fn()
    mockRecorderState.onSegment = undefined
  })

  afterEach(() => {
    vi.useRealTimers()
    cleanup()
    clearInClassExamSession()
    clearInClassExamDrafts('shift-m2-1')
    vi.restoreAllMocks()
  })

  it('keeps textarea visible and editable when Web Speech API is unsupported and allows typing + submitting', async () => {
    const { router } = setupSession([
      {
        questionId: 'q-safari-1',
        title: 'CLO1 · SOLID',
        content: 'Trình bày Dependency Inversion Principle?',
        imageUrl: null,
        imageDescription: null,
      },
    ])

    render(<RouterProvider router={router} />)

    const textarea = (await screen.findByLabelText(/Transcript để kiểm tra và chỉnh sửa/i)) as HTMLTextAreaElement
    expect(textarea).toBeInTheDocument()
    expect(textarea.disabled).toBe(false)
    expect(screen.queryByRole('img')).toBeNull()

    fireEvent.change(textarea, {
      target: { value: 'Module cấp cao phụ thuộc abstraction thay vì implementation.' },
    })
    expect(textarea.value).toBe('Module cấp cao phụ thuộc abstraction thay vì implementation.')

    fireEvent.click(screen.getByRole('button', { name: /^Nộp bài$/i }))
    const submitButtons = await screen.findAllByRole('button', { name: /^Nộp bài$/i })
    fireEvent.click(submitButtons[submitButtons.length - 1])

    await waitFor(() => {
      expect(inClassShiftService.submitStudentAnswer).toHaveBeenCalledWith(
        'shift-m2-1',
        'q-safari-1',
        'Module cấp cao phụ thuộc abstraction thay vì implementation.',
        { isFollowUp: false, parentSubmissionId: null, followUpIndex: 0 },
      )
    })
    expect(await screen.findByText('Danh sách ca thi')).toBeInTheDocument()
  })

  it('records via useAudioRecorder + /speech/stt fallback when !speech.isSupported, preserves Safari blob type, and keeps textarea editable', async () => {
    const { router } = setupSession([
      {
        questionId: 'q-rec-1',
        title: 'CLO2 · Clean Architecture',
        content: 'Vai trò của Domain Layer là gì?',
      },
    ])
    vi.mocked(transcribeSpeech).mockResolvedValue({
      transcript: 'Domain chứa entity và business rules cốt lõi.',
      rawText: 'Domain chứa entity và business rules cốt lõi.',
      provider: 'CloudflareWhisper',
      usedFallback: false,
      confidenceScore: 0.94,
    })

    render(<RouterProvider router={router} />)

    const startBtn = await screen.findByRole('button', { name: 'Bắt đầu ghi âm' })
    await act(async () => {
      fireEvent.click(startBtn)
    })
    expect(mockRecorderState.startRecording).toHaveBeenCalledTimes(1)
    expect(mockSpeechState.startListening).not.toHaveBeenCalled()

    const stopBtn = await screen.findByRole('button', { name: 'Dừng ghi âm' })
    await act(async () => {
      fireEvent.click(stopBtn)
    })

    await waitFor(() => {
      expect(mockRecorderState.stopRecording).toHaveBeenCalledTimes(1)
      expect(transcribeSpeech).toHaveBeenCalledTimes(1)
    })

    const [sentBlob, sentOptions] = vi.mocked(transcribeSpeech).mock.calls[0]
    expect(sentBlob.type).toBe('audio/mp4')
    // Không ép fileName='.webm' để speech.service tự chọn .m4a cho Safari
    expect(sentOptions?.fileName).toBeUndefined()
    expect(sentOptions?.vocabularyHint).toContain('PRN231')

    const textarea = screen.getByLabelText(/Transcript để kiểm tra và chỉnh sửa/i) as HTMLTextAreaElement
    expect(textarea.value).toBe('Domain chứa entity và business rules cốt lõi.')
    expect(textarea.disabled).toBe(false)
  })

  it('handles mic permission denial without locking textarea and preserves existing draft on silent STT failure', async () => {
    saveInClassExamDrafts('shift-m2-1', {
      drafts: { 'q-err-1': 'Bản nháp cũ cần được bảo toàn' },
      questionIndex: 0,
    })
    const { router } = setupSession([
      {
        questionId: 'q-err-1',
        title: 'CLO1 · EF Core',
        content: 'Giải thích cơ chế change tracking?',
      },
    ])
    mockRecorderState.startRecording.mockRejectedValueOnce(
      new DOMException('Permission denied', 'NotAllowedError'),
    )

    render(<RouterProvider router={router} />)

    // Bấm ghi âm lại khi quyền micro bị từ chối
    const reRecordBtn = await screen.findByRole('button', { name: /Ghi âm lại câu trả lời/i })
    fireEvent.click(reRecordBtn)

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Bắt đầu ghi âm' }))
    })
    expect(await screen.findByText(/Trình duyệt chưa được cấp quyền micro/i)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Hủy ghi âm lại/i }))
    const textarea = screen.getByLabelText(/Transcript để kiểm tra và chỉnh sửa/i) as HTMLTextAreaElement
    expect(textarea.disabled).toBe(false)
    expect(textarea.value).toBe('Bản nháp cũ cần được bảo toàn')

    // Nếu cho phép ghi âm nhưng STT trả về im lặng/ảo giác -> giữ nguyên draft cũ
    mockRecorderState.startRecording.mockResolvedValueOnce(undefined)
    vi.mocked(transcribeSpeech).mockResolvedValueOnce({
      transcript: 'Thank you for watching',
      rawText: 'Thank you for watching',
      provider: 'CloudflareWhisper',
      usedFallback: false,
      confidenceScore: 0.9,
    })

    fireEvent.click(screen.getByRole('button', { name: /Ghi âm lại câu trả lời/i }))
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Bắt đầu ghi âm' }))
    })
    const stopBtn = await screen.findByRole('button', { name: 'Dừng ghi âm' })
    await act(async () => {
      fireEvent.click(stopBtn)
    })

    expect(
      await screen.findByText(/Không nhận diện được giọng nói rõ ràng/i),
    ).toBeInTheDocument()
    expect(textarea.value).toBe('Bản nháp cũ cần được bảo toàn')
    expect(textarea.disabled).toBe(false)
  })

  it('unlocks textarea immediately after STT HTTP error during re-record while preserving draft', async () => {
    saveInClassExamDrafts('shift-m2-1', {
      drafts: { 'q-stt-500': 'Nháp trước khi lỗi mạng STT' },
      questionIndex: 0,
    })
    const { router } = setupSession([
      {
        questionId: 'q-stt-500',
        title: 'CLO1 · STT Error Recovery',
        content: 'Trình bày nguyên lý Single Responsibility?',
      },
    ])
    vi.mocked(transcribeSpeech).mockRejectedValueOnce({
      status: 500,
      detail: 'STT service unavailable',
    })

    render(<RouterProvider router={router} />)

    fireEvent.click(await screen.findByRole('button', { name: /Ghi âm lại câu trả lời/i }))
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Bắt đầu ghi âm' }))
    })
    const stopBtn = await screen.findByRole('button', { name: 'Dừng ghi âm' })
    await act(async () => {
      fireEvent.click(stopBtn)
    })

    expect(await screen.findByText('STT service unavailable')).toBeInTheDocument()
    const textarea = screen.getByLabelText(/Transcript để kiểm tra và chỉnh sửa/i) as HTMLTextAreaElement
    expect(textarea.value).toBe('Nháp trước khi lỗi mạng STT')
    expect(textarea.disabled).toBe(false)
  })

  it('does not loop resumeStudentShift or getStudentShift after initial hydration', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const { router } = setupSession([
      {
        questionId: 'q-noloop-1',
        title: 'CLO1 · Sync Stability',
        content: 'Giải thích Dependency Injection?',
      },
    ])

    render(<RouterProvider router={router} />)
    expect(await screen.findByText(/Giải thích Dependency Injection\?/i)).toBeInTheDocument()

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1_000)
    })

    expect(inClassShiftService.resumeStudentShift).toHaveBeenCalledTimes(1)
    // 1 lần trong syncSessionFromServer + 1 lần khởi tạo của status watcher (không re-sync loop)
    expect(inClassShiftService.getStudentShift).toHaveBeenCalledTimes(2)
  })

  it('awaits in-flight transcribeSpeech when countdown reaches 00:00 and submits final transcript', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const { router, shift } = setupSession([
      {
        questionId: 'q-timeout-stt',
        title: 'CLO2 · Race Recovery',
        content: 'Trình bày cơ chế Idempotency?',
      },
    ])
    const nowMs = Date.now()
    const endsAtMs = nowMs + 3_000
    saveInClassExamSession({
      shift,
      questions: [
        {
          questionId: 'q-timeout-stt',
          title: 'CLO2 · Race Recovery',
          content: 'Trình bày cơ chế Idempotency?',
        },
      ],
      startedAt: nowMs,
      examEndsAt: endsAtMs,
      joinedAt: new Date(nowMs).toISOString(),
    })
    vi.mocked(inClassShiftService.resumeStudentShift).mockResolvedValue({
      questions: [
        {
          questionId: 'q-timeout-stt',
          title: 'CLO2 · Race Recovery',
          content: 'Trình bày cơ chế Idempotency?',
        },
      ],
      joinedAt: new Date(nowMs).toISOString(),
      examEndsAt: new Date(endsAtMs).toISOString(),
      remainingSeconds: 3,
      timedOut: false,
      status: 'IN_PROGRESS',
    })

    let resolveStt!: (res: {
      transcript: string
      rawText: string
      provider: string
      usedFallback: boolean
      confidenceScore: number
    }) => void
    vi.mocked(transcribeSpeech).mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveStt = resolve
        }),
    )

    render(<RouterProvider router={router} />)
    await screen.findByText(/Trình bày cơ chế Idempotency\?/i)

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Bắt đầu ghi âm' }))
    })
    // Bấm dừng ghi âm -> recorder.isRecording chuyển false trong khi /speech/stt vẫn đang pending
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Dừng ghi âm' }))
    })
    expect(transcribeSpeech).toHaveBeenCalledTimes(1)

    // Đồng hồ về 00:00 giữa lúc STT đang chạy -> phải chờ STT xong mới được finishStudentExam
    await act(async () => {
      await vi.advanceTimersByTimeAsync(3_500)
    })
    expect(inClassShiftService.finishStudentExam).not.toHaveBeenCalled()

    await act(async () => {
      resolveStt({
        transcript: 'Câu trả lời kịp chốt trước 00:00',
        rawText: 'Câu trả lời kịp chốt trước 00:00',
        provider: 'CloudflareWhisper',
        usedFallback: false,
        confidenceScore: 0.93,
      })
    })

    await waitFor(() => {
      expect(inClassShiftService.submitStudentAnswer).toHaveBeenCalledWith(
        'shift-m2-1',
        'q-timeout-stt',
        'Câu trả lời kịp chốt trước 00:00',
        { isFollowUp: false, parentSubmissionId: null, followUpIndex: 0 },
      )
      expect(inClassShiftService.finishStudentExam).toHaveBeenCalledWith('shift-m2-1')
    })
  })

  it('bounds stopRecording() with a timeout at 00:00 so exam submission never hangs', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const { router, shift } = setupSession([
      {
        questionId: 'q-stop-hang',
        title: 'CLO2 · Bounded Stop',
        content: 'Vai trò của BoundedChannel?',
      },
    ])
    const nowMs = Date.now()
    const endsAtMs = nowMs + 2_000
    saveInClassExamSession({
      shift,
      questions: [
        {
          questionId: 'q-stop-hang',
          title: 'CLO2 · Bounded Stop',
          content: 'Vai trò của BoundedChannel?',
        },
      ],
      startedAt: nowMs,
      examEndsAt: endsAtMs,
      joinedAt: new Date(nowMs).toISOString(),
    })
    vi.mocked(inClassShiftService.resumeStudentShift).mockResolvedValue({
      questions: [
        {
          questionId: 'q-stop-hang',
          title: 'CLO2 · Bounded Stop',
          content: 'Vai trò của BoundedChannel?',
        },
      ],
      joinedAt: new Date(nowMs).toISOString(),
      examEndsAt: new Date(endsAtMs).toISOString(),
      remainingSeconds: 2,
      timedOut: false,
      status: 'IN_PROGRESS',
    })
    mockRecorderState.stopRecording.mockImplementationOnce(() => new Promise(() => {}))

    render(<RouterProvider router={router} />)
    await screen.findByText(/Vai trò của BoundedChannel\?/i)

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Bắt đầu ghi âm' }))
    })

    await act(async () => {
      await vi.advanceTimersByTimeAsync(2_500)
    })
    expect(inClassShiftService.finishStudentExam).not.toHaveBeenCalled()

    await act(async () => {
      await vi.advanceTimersByTimeAsync(5_200)
    })

    await waitFor(() => {
      expect(mockRecorderState.cancelRecording).toHaveBeenCalled()
      expect(inClassShiftService.finishStudentExam).toHaveBeenCalledWith('shift-m2-1')
    })
  })

  it('preserves existing draft on empty Web Speech re-record', async () => {
    mockSpeechState.isSupported = true
    mockSpeechState.startListening = vi.fn(() => {
      updateMockSpeech({ isListening: true, transcript: '' })
    })
    mockSpeechState.stopListening = vi.fn(() => {
      updateMockSpeech({ isListening: false, transcript: '' })
    })

    saveInClassExamDrafts('shift-m2-1', {
      drafts: { 'q-ws-1': 'Câu trả lời cũ đã chuẩn bị kỹ' },
      questionIndex: 0,
    })
    const { router } = setupSession([
      {
        questionId: 'q-ws-1',
        title: 'CLO1 · Web Speech Re-record',
        content: 'Phân biệt Value Object và Entity?',
      },
    ])

    render(<RouterProvider router={router} />)

    fireEvent.click(await screen.findByRole('button', { name: /Ghi âm lại câu trả lời/i }))
    act(() => {
      fireEvent.click(screen.getByRole('button', { name: 'Bắt đầu ghi âm' }))
    })
    act(() => {
      fireEvent.click(screen.getByRole('button', { name: 'Dừng ghi âm' }))
    })

    const textarea = (await screen.findByLabelText(/Transcript để kiểm tra và chỉnh sửa/i)) as HTMLTextAreaElement
    expect(textarea.value).toBe('Câu trả lời cũ đã chuẩn bị kỹ')
    await waitFor(() => {
      expect(textarea.disabled).toBe(false)
    })
  })

  it('locks navigation and submit while getUserMedia is pending and cancels acquired stream on unmount', async () => {
    let resolveStart!: () => void
    mockRecorderState.startRecording.mockImplementationOnce(
      () =>
        new Promise<void>((resolve) => {
          resolveStart = resolve
        }),
    )

    const { router } = setupSession([
      {
        questionId: 'q-gum-1',
        title: 'CLO1 · Câu 1',
        content: 'Câu hỏi 1?',
      },
      {
        questionId: 'q-gum-2',
        title: 'CLO2 · Câu 2',
        content: 'Câu hỏi 2?',
      },
    ])

    const view = render(<RouterProvider router={router} />)
    const startBtn = await screen.findByRole('button', { name: 'Bắt đầu ghi âm' })

    act(() => {
      fireEvent.click(startBtn)
    })

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /^Nộp bài$/i })).toBeDisabled()
      expect(screen.getByRole('button', { name: /^Bỏ qua$/i })).toBeDisabled()
      expect(screen.getByRole('button', { name: 'Chuyển tới câu 2' })).toBeDisabled()
    })

    view.unmount()

    await act(async () => {
      resolveStart()
    })
    expect(mockRecorderState.cancelRecording).toHaveBeenCalledTimes(1)
  })

  it('records follow-up answer into separate turnKey without overwriting main question draft and renders QuestionImage when imageUrl exists', async () => {
    const { router } = setupSession(
      [
        {
          questionId: 'q-fu-img',
          title: 'CLO3 · Kiến trúc phân tầng',
          content: 'Phân tích sơ đồ Clean Architecture sau đây?',
          imageUrl: '/uploads/questions/clean-arch.png',
          imageDescription: 'Sơ đồ Clean Architecture 4 tầng',
        },
      ],
      { hasFollowUp: true, maxFollowUpQuestions: 1 },
    )
    vi.mocked(inClassShiftService.requestNextFollowUp).mockResolvedValue({
      hasMoreFollowUp: true,
      followUpIndex: 1,
      maxFollowUpQuestions: 1,
      parentSubmissionId: 'sub-m2-1',
      questionId: 'q-fu-img',
      followUpPrompt: 'Hãy giải thích vì sao Infrastructure không được nằm ở tâm?',
    })
    vi.mocked(transcribeSpeech).mockResolvedValue({
      transcript: 'Vì Infrastructure dễ thay đổi nên phụ thuộc vào Domain.',
      rawText: 'Vì Infrastructure dễ thay đổi nên phụ thuộc vào Domain.',
      provider: 'CloudflareWhisper',
      usedFallback: false,
      confidenceScore: 0.95,
    })

    render(<RouterProvider router={router} />)

    const img = (await screen.findByRole('img', {
      name: 'Sơ đồ Clean Architecture 4 tầng',
    })) as HTMLImageElement
    expect(img.getAttribute('src')).toBe('/uploads/questions/clean-arch.png')
    expect(screen.getByText('Sơ đồ Clean Architecture 4 tầng')).toBeInTheDocument()

    const mainTextarea = screen.getByLabelText(/Transcript để kiểm tra và chỉnh sửa/i) as HTMLTextAreaElement
    fireEvent.change(mainTextarea, {
      target: { value: 'Domain ở trung tâm và Application điều phối use cases.' },
    })

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Gửi & Câu phụ' }))
    })

    expect(
      await screen.findByText('Hãy giải thích vì sao Infrastructure không được nằm ở tâm?'),
    ).toBeInTheDocument()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Bắt đầu ghi âm' }))
    })
    const stopBtn = await screen.findByRole('button', { name: 'Dừng ghi âm' })
    await act(async () => {
      fireEvent.click(stopBtn)
    })

    const fuTextarea = (await screen.findByLabelText(/Transcript câu trả lời phụ #1/i)) as HTMLTextAreaElement
    await waitFor(() => {
      expect(fuTextarea.value).toBe('Vì Infrastructure dễ thay đổi nên phụ thuộc vào Domain.')
    })
  })

  it('6a: preserves manual textarea edits after stopping Web Speech even if stopListening kept stale transcript', async () => {
    mockSpeechState.isSupported = true
    mockSpeechState.startListening = vi.fn(() => {
      updateMockSpeech({ isListening: true, transcript: '' })
    })
    // Giả lập hook Web Speech thật: stopListening chỉ tắt isListening, KHÔNG tự xóa transcript nếu quên resetTranscript
    mockSpeechState.stopListening = vi.fn(() => {
      updateMockSpeech({ isListening: false })
    })

    const { router } = setupSession([
      {
        questionId: 'q-ws-6a',
        title: 'CLO1 · SOLID',
        content: 'Nêu 5 nguyên lý SOLID?',
      },
    ])

    render(<RouterProvider router={router} />)

    const startBtn = await screen.findByRole('button', { name: 'Bắt đầu ghi âm' })
    await act(async () => {
      fireEvent.click(startBtn)
    })
    await act(async () => {
      updateMockSpeech({ transcript: 'Bản gốc chưa chỉnh sửa' })
    })
    const stopBtn = await screen.findByRole('button', { name: 'Dừng ghi âm' })
    await act(async () => {
      fireEvent.click(stopBtn)
    })

    expect(mockSpeechState.resetTranscript).toHaveBeenCalled()

    const textarea = (await screen.findByLabelText(/Transcript để kiểm tra và chỉnh sửa/i)) as HTMLTextAreaElement
    expect(textarea.value).toBe('Bản gốc chưa chỉnh sửa')

    fireEvent.change(textarea, {
      target: { value: 'Bản gốc chưa chỉnh sửa + bổ sung SRP OCP LSP ISP DIP' },
    })
    expect(textarea.value).toBe('Bản gốc chưa chỉnh sửa + bổ sung SRP OCP LSP ISP DIP')

    fireEvent.click(screen.getByRole('button', { name: /^Nộp bài$/i }))
    const submitButtons = await screen.findAllByRole('button', { name: /^Nộp bài$/i })
    fireEvent.click(submitButtons[submitButtons.length - 1])

    await waitFor(() => {
      expect(inClassShiftService.submitStudentAnswer).toHaveBeenCalledWith(
        'shift-m2-1',
        'q-ws-6a',
        'Bản gốc chưa chỉnh sửa + bổ sung SRP OCP LSP ISP DIP',
        { isFollowUp: false, parentSubmissionId: null, followUpIndex: 0 },
      )
      expect(inClassShiftService.finishStudentExam).toHaveBeenCalledWith('shift-m2-1')
    })
  })

  it('flushes final Web Speech phrase synchronously when isListening turns false in the same macrotask as transcript update', async () => {
    mockSpeechState.isSupported = true
    mockSpeechState.startListening = vi.fn(() => {
      updateMockSpeech({ isListening: true, transcript: '' })
    })
    mockSpeechState.stopListening = vi.fn(() => {
      updateMockSpeech({ isListening: false })
    })

    const { router } = setupSession([
      {
        questionId: 'q-ws-flush',
        title: 'CLO1 · Web Speech Final Flush',
        content: 'Trình bày Open/Closed Principle?',
      },
    ])

    render(<RouterProvider router={router} />)

    const startBtn = await screen.findByRole('button', { name: 'Bắt đầu ghi âm' })
    await act(async () => {
      fireEvent.click(startBtn)
    })
    // Giả lập onresult + onend xảy ra cùng macrotask (isListening thành false ngay khi có cụm từ cuối)
    await act(async () => {
      updateMockSpeech({
        isListening: false,
        transcript: 'Mở rộng nhưng đóng với sửa đổi mã nguồn cũ',
      })
    })

    const textarea = (await screen.findByLabelText(/Transcript để kiểm tra và chỉnh sửa/i)) as HTMLTextAreaElement
    expect(textarea.value).toBe('Mở rộng nhưng đóng với sửa đổi mã nguồn cũ')

    fireEvent.click(screen.getByRole('button', { name: /^Nộp bài$/i }))
    const submitButtons = await screen.findAllByRole('button', { name: /^Nộp bài$/i })
    fireEvent.click(submitButtons[submitButtons.length - 1])

    await waitFor(() => {
      expect(inClassShiftService.submitStudentAnswer).toHaveBeenCalledWith(
        'shift-m2-1',
        'q-ws-flush',
        'Mở rộng nhưng đóng với sửa đổi mã nguồn cũ',
        { isFollowUp: false, parentSubmissionId: null, followUpIndex: 0 },
      )
    })
  })

  it('6b: does not leak Q1 Web Speech transcript onto unanswered Q2 when navigating and submitting', async () => {
    mockSpeechState.isSupported = true
    mockSpeechState.startListening = vi.fn(() => {
      updateMockSpeech({ isListening: true, transcript: '' })
    })
    mockSpeechState.stopListening = vi.fn(() => {
      updateMockSpeech({ isListening: false })
    })

    const { router } = setupSession([
      {
        questionId: 'q-1',
        title: 'CLO1 · Câu 1',
        content: 'Giải thích Dependency Inversion?',
      },
      {
        questionId: 'q-2',
        title: 'CLO2 · Câu 2',
        content: 'Giải thích Liskov Substitution?',
      },
    ])

    render(<RouterProvider router={router} />)

    const startBtn = await screen.findByRole('button', { name: 'Bắt đầu ghi âm' })
    await act(async () => {
      fireEvent.click(startBtn)
    })
    await act(async () => {
      updateMockSpeech({ transcript: 'Đáp án riêng của Câu 1' })
    })
    const stopBtn = await screen.findByRole('button', { name: 'Dừng ghi âm' })
    await act(async () => {
      fireEvent.click(stopBtn)
    })

    const nextBtn = await screen.findByRole('button', { name: 'Câu tiếp' })
    await act(async () => {
      fireEvent.click(nextBtn)
    })

    expect(await screen.findByText(/Giải thích Liskov Substitution\?/i)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /^Nộp bài$/i }))
    const submitButtons = await screen.findAllByRole('button', { name: /^Nộp bài$/i })
    fireEvent.click(submitButtons[submitButtons.length - 1])

    await waitFor(() => {
      expect(inClassShiftService.finishStudentExam).toHaveBeenCalledWith('shift-m2-1')
    })

    const q2Calls = vi
      .mocked(inClassShiftService.submitStudentAnswer)
      .mock.calls.filter(([, questionId]) => questionId === 'q-2')
    expect(q2Calls).toHaveLength(0)
    expect(inClassShiftService.submitStudentAnswer).toHaveBeenCalledTimes(1)
    expect(inClassShiftService.submitStudentAnswer).toHaveBeenCalledWith(
      'shift-m2-1',
      'q-1',
      'Đáp án riêng của Câu 1',
      { isFollowUp: false, parentSubmissionId: null, followUpIndex: 0 },
    )
  })

  it('6c: does not leak Q1 main Web Speech transcript into unanswered follow-up #1 on submit', async () => {
    mockSpeechState.isSupported = true
    mockSpeechState.startListening = vi.fn(() => {
      updateMockSpeech({ isListening: true, transcript: '' })
    })
    mockSpeechState.stopListening = vi.fn(() => {
      updateMockSpeech({ isListening: false })
    })

    const { router } = setupSession(
      [
        {
          questionId: 'q-fu-ws',
          title: 'CLO3 · Follow-up Isolation',
          content: 'Trình bày Clean Architecture?',
        },
      ],
      { hasFollowUp: true, maxFollowUpQuestions: 1 },
    )
    vi.mocked(inClassShiftService.requestNextFollowUp).mockResolvedValue({
      hasMoreFollowUp: true,
      followUpIndex: 1,
      maxFollowUpQuestions: 1,
      parentSubmissionId: 'sub-1',
      questionId: 'q-fu-ws',
      followUpPrompt: 'Vì sao Domain không được phụ thuộc Infrastructure?',
    })

    render(<RouterProvider router={router} />)

    const startBtn = await screen.findByRole('button', { name: 'Bắt đầu ghi âm' })
    await act(async () => {
      fireEvent.click(startBtn)
    })
    await act(async () => {
      updateMockSpeech({ transcript: 'Đáp án câu chính' })
    })
    const stopBtn = await screen.findByRole('button', { name: 'Dừng ghi âm' })
    await act(async () => {
      fireEvent.click(stopBtn)
    })

    const sendFollowUpBtn = await screen.findByRole('button', { name: 'Gửi & Câu phụ' })
    await act(async () => {
      fireEvent.click(sendFollowUpBtn)
    })

    expect(
      await screen.findByText('Vì sao Domain không được phụ thuộc Infrastructure?'),
    ).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /^Nộp bài$/i }))
    const submitButtons = await screen.findAllByRole('button', { name: /^Nộp bài$/i })
    fireEvent.click(submitButtons[submitButtons.length - 1])

    await waitFor(() => {
      expect(inClassShiftService.finishStudentExam).toHaveBeenCalledWith('shift-m2-1')
    })

    const followUpCalls = vi
      .mocked(inClassShiftService.submitStudentAnswer)
      .mock.calls.filter(([, , , opts]) => Boolean(opts?.isFollowUp))
    expect(followUpCalls).toHaveLength(0)
    expect(inClassShiftService.submitStudentAnswer).toHaveBeenCalledTimes(1)
  })

  it('guards 20s segment promises with recordingGenerationRef so cancelled session segments cannot bleed into later recordings', async () => {
    const { router } = setupSession([
      {
        questionId: 'q-seg-1',
        title: 'CLO1 · Segment 1',
        content: 'Câu hỏi 1?',
      },
      {
        questionId: 'q-seg-2',
        title: 'CLO2 · Segment 2',
        content: 'Câu hỏi 2?',
      },
    ])

    let resolveStaleSegment!: (res: {
      transcript: string
      rawText: string
      provider: string
      usedFallback: boolean
      confidenceScore: number
    }) => void
    vi.mocked(transcribeSpeech)
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            resolveStaleSegment = resolve
          }),
      )
      .mockResolvedValueOnce({
        transcript: 'Đáp án sạch của câu 2',
        rawText: 'Đáp án sạch của câu 2',
        provider: 'CloudflareWhisper',
        usedFallback: false,
        confidenceScore: 0.95,
      })

    mockRecorderState.stopRecording.mockRejectedValueOnce(new Error('Mic reset'))

    render(<RouterProvider router={router} />)

    const startBtnQ1 = await screen.findByRole('button', { name: 'Bắt đầu ghi âm' })
    await act(async () => {
      fireEvent.click(startBtnQ1)
    })

    // Phát sinh segment 20s cho Câu 1 (pending)
    act(() => {
      mockRecorderState.onSegment?.(new Blob(['seg-q1'], { type: 'audio/webm' }), 0)
    })

    // Dừng ghi âm ở Câu 1 thất bại -> UI mở khóa để sinh viên có thể chuyển câu
    const stopQ1Btn = await screen.findByRole('button', { name: 'Dừng ghi âm' })
    await act(async () => {
      fireEvent.click(stopQ1Btn)
    })
    expect(await screen.findByText('Mic reset')).toBeInTheDocument()

    // Chuyển sang Câu 2 (gọi cancelActiveRecorder() tăng recordingGenerationRef)
    const goQ2Btn = screen.getByRole('button', { name: 'Chuyển tới câu 2' })
    await act(async () => {
      fireEvent.click(goQ2Btn)
    })
    expect(await screen.findByText(/Câu hỏi 2\?/i)).toBeInTheDocument()

    // Segment cũ từ Câu 1 resolve muộn
    await act(async () => {
      resolveStaleSegment({
        transcript: 'Nội dung cũ của Câu 1 bị trễ',
        rawText: 'Nội dung cũ của Câu 1 bị trễ',
        provider: 'CloudflareWhisper',
        usedFallback: false,
        confidenceScore: 0.9,
      })
    })

    // Ghi âm mới ở Câu 2 không được dính segment của Câu 1
    const startBtnQ2 = await screen.findByRole('button', { name: 'Bắt đầu ghi âm' })
    await act(async () => {
      fireEvent.click(startBtnQ2)
    })
    const stopBtn = await screen.findByRole('button', { name: 'Dừng ghi âm' })
    await act(async () => {
      fireEvent.click(stopBtn)
    })

    const textarea = (await screen.findByLabelText(/Transcript để kiểm tra và chỉnh sửa/i)) as HTMLTextAreaElement
    await waitFor(() => {
      expect(textarea.value).toBe('Đáp án sạch của câu 2')
    })
  })

  it('calls stopListening on submit even when Web Speech startListening is still in pre-onstart window', async () => {
    mockSpeechState.isSupported = true
    // startListening đã gọi nhưng browser chưa kịp bắn onstart -> isListening vẫn là false
    mockSpeechState.startListening = vi.fn()
    mockSpeechState.stopListening = vi.fn(() => {
      updateMockSpeech({ isListening: false })
    })

    const { router } = setupSession([
      {
        questionId: 'q-pre-onstart',
        title: 'CLO1 · Pre-onstart Submit',
        content: 'Giải thích Dependency Injection?',
      },
    ])

    render(<RouterProvider router={router} />)

    const startBtn = await screen.findByRole('button', { name: 'Bắt đầu ghi âm' })
    await act(async () => {
      fireEvent.click(startBtn)
    })
    expect(mockSpeechState.startListening).toHaveBeenCalledTimes(1)
    expect(mockSpeechState.isListening).toBe(false)

    // Nộp bài ngay trong cửa sổ trước khi onstart bắn
    fireEvent.click(screen.getByRole('button', { name: /^Nộp bài$/i }))
    const submitButtons = await screen.findAllByRole('button', { name: /^Nộp bài$/i })
    await act(async () => {
      fireEvent.click(submitButtons[submitButtons.length - 1])
    })

    await waitFor(() => {
      expect(mockSpeechState.stopListening).toHaveBeenCalledTimes(1)
      expect(mockSpeechState.resetTranscript).toHaveBeenCalled()
      expect(inClassShiftService.finishStudentExam).toHaveBeenCalledWith('shift-m2-1')
    })
  })
})
