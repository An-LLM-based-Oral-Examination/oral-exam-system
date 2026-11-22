import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { RouterProvider, createMemoryRouter } from 'react-router-dom'
import { MockExamSessionPage } from '../MockExamSessionPage'
import { mockExamService } from '@/services/mockExam.service'
import { transcribeSpeech } from '@/services/speech.service'
import { toast } from '@/components/common/toastStore'
import { clearMockExamSession, saveMockExamSession } from '@/utils/mockExamSessionStorage'
import type { StoredMockExamSession } from '@/types/mockExam.types'

const mockRecorderState = vi.hoisted(() => ({
  error: null as string | null,
  startRecording: vi.fn<() => Promise<void>>(),
  stopRecording: vi.fn<() => Promise<Blob>>(),
  cancelRecording: vi.fn(),
  clearError: vi.fn(),
  onSegment: undefined as ((blob: Blob, index: number) => void) | undefined,
}))

vi.mock('@/services/mockExam.service', () => ({
  mockExamService: {
    resumeSession: vi.fn(),
    submitAnswer: vi.fn(),
    submitSession: vi.fn(),
    pauseSession: vi.fn(),
  },
  getMockExamErrorDetail: (error: unknown, fallback: string) =>
    typeof error === 'object' &&
    error !== null &&
    'detail' in error &&
    typeof (error as { detail?: unknown }).detail === 'string' &&
    (error as { detail: string }).detail.trim()
      ? (error as { detail: string }).detail
      : fallback,
  getMockExamErrorStatus: (error: unknown) =>
    typeof error === 'object' &&
    error !== null &&
    'status' in error &&
    typeof (error as { status?: unknown }).status === 'number'
      ? (error as { status: number }).status
      : null,
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

vi.mock('@/services/speech.service', () => ({
  getTtsVoiceForCourse: () => 'vi-VN-HoaiMyNeural',
  playNaturalSpeech: vi.fn().mockResolvedValue(undefined),
  stopSpeechPlayback: vi.fn(),
  isSpeechPlaybackCancellation: () => false,
  transcribeSpeech: vi.fn(),
}))

function buildSession(): StoredMockExamSession {
  const now = Date.now()
  return {
    sessionId: 'mock-sess-1',
    courseId: 'course-1',
    courseCode: 'SWT301',
    courseName: 'Software Testing',
    hasFollowUp: false,
    durationMinutes: 30,
    startedAt: new Date(now - 60_000).toISOString(),
    expiresAt: new Date(now + 29 * 60_000).toISOString(),
    usedQuota: 1,
    remainingQuota: 2,
    maxDailyQuota: 3,
    questions: [
      {
        id: 'mq-1',
        orderIndex: 1,
        title: 'Boundary Value Analysis',
        content: 'Giải thích kỹ thuật Boundary Value Analysis và ví dụ áp dụng.',
        bloomLevel: 'Understand',
        difficulty: 'medium',
        rubricCriteria: [{ name: 'Định nghĩa', maxScore: 5 }, { name: 'Ví dụ', maxScore: 5 }],
      },
      {
        id: 'mq-2',
        orderIndex: 2,
        title: 'Equivalence Partitioning',
        content: 'Phân vùng tương đương giúp giảm số lượng test case như thế nào?',
        bloomLevel: 'Apply',
        difficulty: 'medium',
        rubricCriteria: [{ name: 'Khái niệm', maxScore: 10 }],
      },
    ],
    answers: [],
  }
}

function renderMockExam() {
  const session = buildSession()
  saveMockExamSession(session)
  vi.mocked(mockExamService.resumeSession).mockResolvedValue({
    ...session,
    answers: [],
    activeFollowUp: null,
  })
  vi.mocked(mockExamService.pauseSession).mockResolvedValue(1700)
  vi.mocked(mockExamService.submitAnswer).mockImplementation(async (_sessionId, req) => ({
    answerId: 'ans-mock-1',
    questionId: req.questionId,
    transcript: req.transcript ?? '',
    status: 'SUBMITTED',
    answeredAt: new Date().toISOString(),
    isFollowUp: false,
    parentAnswerId: null,
    needsFollowUp: false,
    followUpPrompt: null,
    followUpAnswerId: null,
    followUpIndex: 0,
    maxFollowUps: 0,
  }))

  const router = createMemoryRouter(
    [
      { path: '/mock-exam/session', element: <MockExamSessionPage /> },
      { path: '/mock-exam', element: <div>Phòng chờ thi thử</div> },
    ],
    { initialEntries: ['/mock-exam/session?session=mock-sess-1'] },
  )
  return render(<RouterProvider router={router} />)
}

describe('MockExamSessionPage UX hardening (M3)', () => {
  beforeEach(() => {
    sessionStorage.clear()
    localStorage.clear()
    vi.mocked(transcribeSpeech).mockReset()
    mockRecorderState.error = null
    mockRecorderState.startRecording.mockResolvedValue(undefined)
    mockRecorderState.stopRecording.mockResolvedValue(
      new Blob([new Uint8Array(3000)], { type: 'audio/webm' }),
    )
  })

  afterEach(() => {
    cleanup()
    clearMockExamSession()
    vi.clearAllMocks()
  })

  it('ignores Space shortcut on interactive elements, nested children, modifiers, and open ConfirmDialog while body triggers recording', async () => {
    renderMockExam()
    await screen.findByText(/Giải thích kỹ thuật Boundary Value Analysis/i)

    const readBtn = screen.getByRole('button', { name: /Đọc câu hỏi/i })
    fireEvent.keyDown(readBtn, { code: 'Space', key: ' ', bubbles: true })
    expect(mockRecorderState.startRecording).not.toHaveBeenCalled()

    const selectEl = document.createElement('select')
    const anchorEl = document.createElement('a')
    anchorEl.href = '#top'
    const roleBtn = document.createElement('div')
    roleBtn.setAttribute('role', 'button')
    const nestedSpan = document.createElement('span')
    roleBtn.appendChild(nestedSpan)
    const roleSwitch = document.createElement('div')
    roleSwitch.setAttribute('role', 'switch')
    const editable = document.createElement('div')
    editable.setAttribute('contenteditable', 'true')
    document.body.append(selectEl, anchorEl, roleBtn, roleSwitch, editable)

    for (const target of [selectEl, anchorEl, roleBtn, nestedSpan, roleSwitch, editable]) {
      const ev = new KeyboardEvent('keydown', { code: 'Space', key: ' ', bubbles: true, cancelable: true })
      target.dispatchEvent(ev)
      expect(ev.defaultPrevented).toBe(false)
    }
    fireEvent.keyDown(document.body, { code: 'Space', key: ' ', ctrlKey: true, bubbles: true })
    fireEvent.keyDown(document.body, { code: 'Space', key: ' ', altKey: true, bubbles: true })
    expect(mockRecorderState.startRecording).not.toHaveBeenCalled()

    // Open ConfirmDialog ("Nộp khi còn câu chưa làm?") -> Space must not trigger recording
    fireEvent.click(screen.getByRole('button', { name: /^Nộp bài$/i }))
    expect(await screen.findByText(/Nộp khi còn câu chưa làm\?/i)).toBeInTheDocument()
    fireEvent.keyDown(document.body, { code: 'Space', key: ' ', bubbles: true })
    expect(mockRecorderState.startRecording).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole('button', { name: /Quay lại/i }))
    await waitFor(() => {
      expect(screen.queryByText(/Nộp khi còn câu chưa làm\?/i)).not.toBeInTheDocument()
    })

    // Pressing Space on document.body triggers startRecording
    fireEvent.keyDown(document.body, { code: 'Space', key: ' ', bubbles: true })
    await waitFor(() => expect(mockRecorderState.startRecording).toHaveBeenCalledTimes(1))

    selectEl.remove()
    anchorEl.remove()
    roleBtn.remove()
    roleSwitch.remove()
    editable.remove()
  })

  it('surfaces chunk failure warning without poisoning answerError so leaveRoom still calls pauseSession', async () => {
    const warnSpy = vi.spyOn(toast, 'warning').mockImplementation(() => '')
    vi.mocked(transcribeSpeech)
      .mockRejectedValueOnce({ status: 502, detail: 'Bad Gateway' })
      .mockResolvedValueOnce({ transcript: 'Boundary values test min and max.' })
      .mockResolvedValueOnce({ transcript: 'For example age 18 and 65.' })

    renderMockExam()
    await screen.findByText(/Giải thích kỹ thuật Boundary Value Analysis/i)

    fireEvent.click(screen.getByRole('button', { name: /Bắt đầu ghi âm/i }))
    await waitFor(() => expect(mockRecorderState.startRecording).toHaveBeenCalledTimes(1))

    act(() => {
      mockRecorderState.onSegment?.(new Blob([new Uint8Array(2500)], { type: 'audio/webm' }), 0)
      mockRecorderState.onSegment?.(new Blob([new Uint8Array(2500)], { type: 'audio/webm' }), 1)
    })

    fireEvent.click(screen.getByRole('button', { name: /Dừng ghi âm/i }))

    const transcriptBox = await screen.findByLabelText(/Câu trả lời chính/i)
    expect((transcriptBox as HTMLTextAreaElement).value).toBe(
      'Boundary values test min and max. For example age 18 and 65.',
    )
    expect(
      screen.getByText(/Có 1 đoạn ghi âm \(20 giây\) gặp lỗi kết nối khi nhận diện giọng nói/i),
    ).toBeInTheDocument()
    expect(warnSpy).toHaveBeenCalledTimes(1)

    // Leaving room must still pauseSession (answerError was NOT polluted)
    fireEvent.click(screen.getByRole('button', { name: /Rời phòng thi/i }))
    expect(await screen.findByText(/Thời gian làm bài sẽ được tạm dừng/i)).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: /^Rời phòng$/i }))

    await waitFor(() => {
      expect(mockExamService.pauseSession).toHaveBeenCalledWith('mock-sess-1')
    })
    expect(await screen.findByText(/Phòng chờ thi thử/i)).toBeInTheDocument()
  })

  it('does not show chunk warning for 422 silence or Whisper hallucination chunks, and warns if tail fails with 500', async () => {
    const warnSpy = vi.spyOn(toast, 'warning').mockImplementation(() => '')
    vi.mocked(transcribeSpeech)
      .mockResolvedValueOnce({ transcript: 'Thank you for watching' })
      .mockResolvedValueOnce({ transcript: 'Valid boundary value explanation from segment one.' })
      .mockRejectedValueOnce({ status: 422, detail: 'Im lặng' })

    renderMockExam()
    await screen.findByText(/Giải thích kỹ thuật Boundary Value Analysis/i)

    fireEvent.click(screen.getByRole('button', { name: /Bắt đầu ghi âm/i }))
    await waitFor(() => expect(mockRecorderState.startRecording).toHaveBeenCalledTimes(1))
    act(() => {
      mockRecorderState.onSegment?.(new Blob([new Uint8Array(2500)], { type: 'audio/webm' }), 0)
      mockRecorderState.onSegment?.(new Blob([new Uint8Array(2500)], { type: 'audio/webm' }), 1)
    })
    fireEvent.click(screen.getByRole('button', { name: /Dừng ghi âm/i }))

    const transcriptBox = await screen.findByLabelText(/Câu trả lời chính/i)
    expect((transcriptBox as HTMLTextAreaElement).value).toBe(
      'Valid boundary value explanation from segment one.',
    )
    expect(screen.queryByText(/đoạn ghi âm \(20 giây\) gặp lỗi/i)).not.toBeInTheDocument()
    expect(warnSpy).not.toHaveBeenCalled()

    // Now test tail chunk 500 failure on question 2
    fireEvent.click(screen.getByRole('button', { name: /Câu tiếp/i }))
    await screen.findByText(/Phân vùng tương đương giúp giảm số lượng test case/i)

    vi.mocked(transcribeSpeech)
      .mockResolvedValueOnce({ transcript: 'Partitioning divides input into valid and invalid classes.' })
      .mockRejectedValueOnce({ status: 500, detail: 'Internal Server Error' })

    fireEvent.click(screen.getByRole('button', { name: /Bắt đầu ghi âm/i }))
    await waitFor(() => expect(mockRecorderState.startRecording).toHaveBeenCalledTimes(2))
    act(() => {
      mockRecorderState.onSegment?.(new Blob([new Uint8Array(2500)], { type: 'audio/webm' }), 0)
    })
    fireEvent.click(screen.getByRole('button', { name: /Dừng ghi âm/i }))

    await waitFor(() => {
      expect(
        screen.getByText(/Có 1 đoạn ghi âm \(20 giây\) gặp lỗi kết nối khi nhận diện giọng nói/i),
      ).toBeInTheDocument()
    })
    expect((screen.getByLabelText(/Câu trả lời chính/i) as HTMLTextAreaElement).value).toBe(
      'Partitioning divides input into valid and invalid classes.',
    )
    expect(warnSpy).toHaveBeenCalledTimes(1)
  })
})
