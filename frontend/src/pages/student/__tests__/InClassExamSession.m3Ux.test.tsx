import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { RouterProvider, createMemoryRouter, useLocation } from 'react-router-dom'
import { InClassExamSessionPage } from '../InClassExamSessionPage'
import { inClassShiftService } from '@/services/inClassShift.service'
import {
  clearInClassExamDrafts,
  clearInClassExamSession,
  loadInClassExamDrafts,
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
        const notify = () => setRevision((v) => v + 1)
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
  getTtsVoiceForCourse: () => 'vi-VN-HoaiMyNeural',
  playNaturalSpeech: vi.fn().mockResolvedValue(undefined),
  stopSpeechPlayback: vi.fn(),
  isSpeechPlaybackCancellation: () => false,
  transcribeSpeech: vi.fn(),
}))

function buildShift(overrides: Partial<InClassShift> = {}): InClassShift {
  return {
    id: 'shift-m3-1',
    classId: 'cls-1',
    className: 'SWT301 - SE1801',
    courseCode: 'SWT301',
    title: 'Ca kiểm tra giữa kỳ',
    description: '',
    status: 'ACTIVE',
    isLocked: false,
    startTime: '2026-11-21T08:00:00Z',
    endTime: '2026-11-21T09:00:00Z',
    durationMinutes: 60,
    examDurationMinutes: 20,
    questionCount: 2,
    hasFollowUp: false,
    maxFollowUpQuestions: 0,
    isAutoPublishGrades: false,
    questionPoolMode: 'ALL_COURSE_BANK',
    selectedDifficulties: ['medium'],
    selectedCloIds: [],
    passwordRotationSeconds: 300,
    gracePeriodSeconds: 60,
    joinedCount: 1,
    submittedCount: 0,
    createdAt: '2026-11-21T07:50:00Z',
    ...overrides,
  }
}

const QUESTIONS: StudentExamQuestion[] = [
  {
    questionId: 'iq-1',
    title: 'CLO1 · Equivalence Partitioning',
    content: 'Phân vùng tương đương là gì?',
  },
  {
    questionId: 'iq-2',
    title: 'CLO2 · Regression Testing',
    content: 'Khi nào cần thực hiện kiểm thử hồi quy?',
  },
]

function PortalProbe() {
  const location = useLocation()
  const state = location.state as { notice?: string; finishedShiftId?: string } | null
  return (
    <div>
      <span>Trang danh sách ca</span>
      {state?.notice && <p data-testid="portal-notice">{state.notice}</p>}
      {state?.finishedShiftId && <p data-testid="finished-shift">{state.finishedShiftId}</p>}
    </div>
  )
}

function renderInClassExam(shiftOverrides: Partial<InClassShift> = {}, withSessionStorage = true) {
  const shift = buildShift(shiftOverrides)
  const nowMs = Date.now()
  const endsAtMs = nowMs + 15 * 60_000

  if (withSessionStorage) {
    saveInClassExamSession({
      shift,
      questions: QUESTIONS,
      startedAt: nowMs,
      examEndsAt: endsAtMs,
      joinedAt: new Date(nowMs).toISOString(),
    })
  }

  vi.mocked(inClassShiftService.getStudentShift).mockResolvedValue(shift)
  vi.mocked(inClassShiftService.resumeStudentShift).mockResolvedValue({
    questions: QUESTIONS,
    joinedAt: new Date(nowMs).toISOString(),
    examEndsAt: new Date(endsAtMs).toISOString(),
    remainingSeconds: 900,
    timedOut: false,
    status: 'IN_PROGRESS',
  })
  vi.mocked(inClassShiftService.listStudentResults).mockResolvedValue([])
  vi.mocked(inClassShiftService.submitStudentAnswer).mockResolvedValue({
    submissionId: 'sub-1',
    shiftId: shift.id,
    questionId: QUESTIONS[0].questionId,
    status: 'SUBMITTED',
    isFollowUp: false,
    parentSubmissionId: null,
    followUpIndex: 0,
  })
  vi.mocked(inClassShiftService.finishStudentExam).mockResolvedValue(undefined)

  const router = createMemoryRouter(
    [
      { path: '/in-class-exam/session', element: <InClassExamSessionPage /> },
      { path: '/in-class-exam', element: <PortalProbe /> },
    ],
    { initialEntries: [`/in-class-exam/session?shiftId=${shift.id}`] },
  )
  return render(<RouterProvider router={router} />)
}

describe('InClassExamSessionPage M3 UX hardening', () => {
  beforeEach(() => {
    sessionStorage.clear()
    localStorage.clear()
    mockSpeechState.isSupported = false
    mockSpeechState.isListening = false
    mockSpeechState.transcript = ''
    mockSpeechState.error = null
    mockRecorderState.error = null
    mockRecorderState.startRecording.mockResolvedValue(undefined)
    mockRecorderState.stopRecording.mockResolvedValue(
      new Blob([new Uint8Array(2500)], { type: 'audio/webm' }),
    )
  })

  afterEach(() => {
    vi.useRealTimers()
    cleanup()
    clearInClassExamSession()
    clearInClassExamDrafts('shift-m3-1')
    vi.clearAllMocks()
  })

  it('ignores Space shortcut on buttons, select, anchor, role=button/switch, nested spans, contenteditable, and ConfirmDialog while body works', async () => {
    renderInClassExam()
    await screen.findByText(/Phân vùng tương đương là gì/i)

    const readBtn = screen.getByRole('button', { name: /Đọc câu hỏi/i })
    fireEvent.keyDown(readBtn, { code: 'Space', key: ' ', bubbles: true })
    expect(mockRecorderState.startRecording).not.toHaveBeenCalled()

    const selectEl = document.createElement('select')
    const linkEl = document.createElement('a')
    linkEl.href = '#help'
    const roleButton = document.createElement('div')
    roleButton.setAttribute('role', 'button')
    const nestedSpan = document.createElement('span')
    roleButton.appendChild(nestedSpan)
    const roleSwitch = document.createElement('div')
    roleSwitch.setAttribute('role', 'switch')
    const editable = document.createElement('div')
    editable.setAttribute('contenteditable', 'true')
    document.body.append(selectEl, linkEl, roleButton, roleSwitch, editable)

    for (const target of [selectEl, linkEl, roleButton, nestedSpan, roleSwitch, editable]) {
      const ev = new KeyboardEvent('keydown', { code: 'Space', key: ' ', bubbles: true, cancelable: true })
      target.dispatchEvent(ev)
      expect(ev.defaultPrevented).toBe(false)
    }
    expect(mockRecorderState.startRecording).not.toHaveBeenCalled()

    // ConfirmDialog open -> Space ignored
    fireEvent.click(screen.getByRole('button', { name: /^Nộp bài$/i }))
    expect(await screen.findByText(/Xác nhận nộp bài/i)).toBeInTheDocument()
    fireEvent.keyDown(document.body, { code: 'Space', key: ' ', bubbles: true })
    expect(mockRecorderState.startRecording).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('button', { name: /Làm tiếp/i }))

    // Space on body triggers recording
    fireEvent.keyDown(document.body, { code: 'Space', key: ' ', bubbles: true })
    await waitFor(() => expect(mockRecorderState.startRecording).toHaveBeenCalledTimes(1))

    selectEl.remove()
    linkEl.remove()
    roleButton.remove()
    roleSwitch.remove()
    editable.remove()
  })

  it('flushes unsent local drafts best-effort when 3s polling detects remote CLOSED within grace period', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    saveInClassExamDrafts('shift-m3-1', {
      drafts: { 'iq-1': 'Bản nháp câu 1 chưa bấm nộp khi giảng viên đóng ca' },
      questionIndex: 0,
    })

    const activeShift = buildShift({ status: 'ACTIVE' })
    const closedShift = buildShift({ status: 'CLOSED' })

    renderInClassExam()
    await screen.findByText(/Phân vùng tương đương là gì/i)

    vi.mocked(inClassShiftService.getStudentShift).mockResolvedValue(closedShift)
    expect(activeShift.status).toBe('ACTIVE')

    await act(async () => {
      await vi.advanceTimersByTimeAsync(3_100)
    })

    await waitFor(() => {
      expect(inClassShiftService.submitStudentAnswer).toHaveBeenCalledWith(
        'shift-m3-1',
        'iq-1',
        'Bản nháp câu 1 chưa bấm nộp khi giảng viên đóng ca',
        { isFollowUp: false, parentSubmissionId: null, followUpIndex: 0 },
      )
    })
    expect(inClassShiftService.finishStudentExam).toHaveBeenCalledWith('shift-m3-1')
    expect(await screen.findByText('Trang danh sách ca')).toBeInTheDocument()
  })

  it('stops 3s retry loop, preserves local drafts, and shows blocking warning modal when remote CLOSED past grace rejects (400)', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    saveInClassExamDrafts('shift-m3-1', {
      drafts: {
        'iq-1': 'Câu trả lời 1 chưa nộp kịp',
        'iq-2': 'Câu trả lời 2 chưa nộp kịp',
      },
      questionIndex: 0,
    })

    renderInClassExam()
    await screen.findByText(/Phân vùng tương đương là gì/i)

    vi.mocked(inClassShiftService.submitStudentAnswer).mockRejectedValue({
      status: 400,
      detail: 'Ca thi đã hết thời gian tiếp nhận bài nộp muộn.',
    })
    vi.mocked(inClassShiftService.getStudentShift).mockResolvedValue(buildShift({ status: 'CLOSED' }))

    await act(async () => {
      await vi.advanceTimersByTimeAsync(3_100)
    })

    expect(await screen.findByText(/Ca thi đã đóng/i)).toBeInTheDocument()
    expect(screen.getByText('Câu 1', { selector: 'li' })).toBeInTheDocument()
    expect(screen.getByText('Câu 2', { selector: 'li' })).toBeInTheDocument()
    expect(screen.getByText(/Ca thi đã hết thời gian tiếp nhận bài nộp muộn/i)).toBeInTheDocument()

    // Attempts both pending items once and does NOT loop every 3s afterwards
    expect(inClassShiftService.submitStudentAnswer).toHaveBeenCalledTimes(2)
    await act(async () => {
      await vi.advanceTimersByTimeAsync(9_500)
    })
    expect(inClassShiftService.submitStudentAnswer).toHaveBeenCalledTimes(2)

    // Drafts preserved in localStorage
    expect(loadInClassExamDrafts('shift-m3-1')?.drafts['iq-1']).toBe('Câu trả lời 1 chưa nộp kịp')

    fireEvent.click(screen.getByRole('button', { name: /Đã hiểu, về danh sách ca/i }))
    expect(await screen.findByText('Trang danh sách ca')).toBeInTheDocument()
    expect(loadInClassExamDrafts('shift-m3-1')?.drafts['iq-2']).toBe('Câu trả lời 2 chưa nộp kịp')
  })

  it('syncSessionFromServer (F5 recovery) flushes local draft when CLOSED has empty auto-submitted server rows', async () => {
    saveInClassExamDrafts('shift-m3-1', {
      drafts: { 'iq-2': 'Bản nháp câu 2 cần cứu sau F5 khi ca vừa CLOSED' },
      questionIndex: 1,
    })

    const closedShift = buildShift({ status: 'CLOSED' })
    vi.mocked(inClassShiftService.getStudentShift).mockResolvedValue(closedShift)
    vi.mocked(inClassShiftService.resumeStudentShift).mockResolvedValue({
      questions: QUESTIONS,
      joinedAt: new Date(Date.now() - 300_000).toISOString(),
      examEndsAt: new Date(Date.now() + 600_000).toISOString(),
      remainingSeconds: 600,
      timedOut: false,
      status: 'SUBMITTED',
    })
    // Backend SubmitMissingAsync creates empty transcript rows ("") for unanswered questions
    vi.mocked(inClassShiftService.listStudentResults).mockResolvedValue([
      {
        submissionId: 'sub-empty-1',
        questionId: 'iq-1',
        title: QUESTIONS[0].title,
        content: QUESTIONS[0].content,
        transcript: 'Đã nộp câu 1 trước đó.',
        aiScore: null,
        aiFeedback: null,
        finalScore: null,
        lecturerNotes: null,
        status: 'SUBMITTED',
      },
      {
        submissionId: 'sub-empty-2',
        questionId: 'iq-2',
        title: QUESTIONS[1].title,
        content: QUESTIONS[1].content,
        transcript: '',
        aiScore: null,
        aiFeedback: null,
        finalScore: null,
        lecturerNotes: null,
        status: 'SUBMITTED',
      },
    ])
    vi.mocked(inClassShiftService.submitStudentAnswer).mockResolvedValue({
      submissionId: 'sub-saved-2',
      shiftId: 'shift-m3-1',
      questionId: 'iq-2',
      status: 'SUBMITTED',
      isFollowUp: false,
      parentSubmissionId: null,
      followUpIndex: 0,
    })
    vi.mocked(inClassShiftService.finishStudentExam).mockResolvedValue(undefined)

    const router = createMemoryRouter(
      [
        { path: '/in-class-exam/session', element: <InClassExamSessionPage /> },
        { path: '/in-class-exam', element: <PortalProbe /> },
      ],
      { initialEntries: ['/in-class-exam/session?shiftId=shift-m3-1'] },
    )
    render(<RouterProvider router={router} />)

    await waitFor(() => {
      expect(inClassShiftService.submitStudentAnswer).toHaveBeenCalledWith(
        'shift-m3-1',
        'iq-2',
        'Bản nháp câu 2 cần cứu sau F5 khi ca vừa CLOSED',
        { isFollowUp: false, parentSubmissionId: null, followUpIndex: 0 },
      )
    })
    expect(inClassShiftService.submitStudentAnswer).toHaveBeenCalledTimes(1)
    expect(await screen.findByText('Trang danh sách ca')).toBeInTheDocument()
  })

  it('syncSessionFromServer preserves local draft and shows warning modal when shift is already FINALIZED', async () => {
    saveInClassExamDrafts('shift-m3-1', {
      drafts: { 'iq-1': 'Bản nháp câu 1 chưa kịp nộp trước khi FINALIZED' },
      questionIndex: 0,
    })

    const finalizedShift = buildShift({ status: 'FINALIZED', isLocked: true })
    vi.mocked(inClassShiftService.getStudentShift).mockResolvedValue(finalizedShift)
    vi.mocked(inClassShiftService.resumeStudentShift).mockResolvedValue({
      questions: QUESTIONS,
      joinedAt: new Date(Date.now() - 300_000).toISOString(),
      examEndsAt: new Date(Date.now() + 600_000).toISOString(),
      remainingSeconds: 600,
      timedOut: false,
      status: 'FINALIZED',
    })
    vi.mocked(inClassShiftService.listStudentResults).mockResolvedValue([])

    const router = createMemoryRouter(
      [
        { path: '/in-class-exam/session', element: <InClassExamSessionPage /> },
        { path: '/in-class-exam', element: <PortalProbe /> },
      ],
      { initialEntries: ['/in-class-exam/session?shiftId=shift-m3-1'] },
    )
    render(<RouterProvider router={router} />)

    expect(await screen.findByRole('heading', { name: /Ca thi đã chốt sổ điểm/i })).toBeInTheDocument()
    expect(inClassShiftService.submitStudentAnswer).not.toHaveBeenCalled()
    expect(loadInClassExamDrafts('shift-m3-1')?.drafts['iq-1']).toBe(
      'Bản nháp câu 1 chưa kịp nộp trước khi FINALIZED',
    )

    fireEvent.click(screen.getByRole('button', { name: /Đã hiểu, về danh sách ca/i }))
    expect(await screen.findByText('Trang danh sách ca')).toBeInTheDocument()
    expect(loadInClassExamDrafts('shift-m3-1')?.drafts['iq-1']).toBe(
      'Bản nháp câu 1 chưa kịp nộp trước khi FINALIZED',
    )
  })
})
