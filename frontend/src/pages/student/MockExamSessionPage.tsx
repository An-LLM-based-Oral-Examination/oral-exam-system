import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import {
  ArrowLeft,
  ArrowRight,
  Clock3,
  LoaderCircle,
  Mic,
  Settings,
  Volume2,
  X,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { MockExamScorecard } from '@/components/mock-exam/MockExamScorecard'
import { QuestionImage } from '@/components/question/QuestionImage'
import { useAudioRecorder } from '@/hooks/useAudioRecorder'
import {
  getMockExamErrorDetail,
  getMockExamErrorStatus,
  mockExamService,
} from '@/services/mockExam.service'
import { DEFAULT_TTS_VOICE, playNaturalSpeech, stopSpeechPlayback } from '@/services/speech.service'
import type {
  MockExamScorecard as MockExamScorecardData,
  StoredMockExamAnswer,
  StoredMockExamSession,
} from '@/types/mockExam.types'
import {
  clearMockExamSession,
  readMockExamSession,
  saveMockExamAnswer,
  secondsUntilExpiry,
} from '@/utils/mockExamSessionStorage'

type RoomPhase = 'exam' | 'submitting-exam' | 'scorecard'

interface ExamWarning {
  title: string
  message: string
  confirmLabel: string
  cancelLabel?: string
  tone: 'warning' | 'danger'
}

interface ExamRuntime {
  phase: RoomPhase
  questionId: string
  audioBlob: Blob | null
  recordedSeconds: number
  answers: Record<string, StoredMockExamAnswer>
}

function formatClock(totalSeconds: number) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
}

function difficultyLabel(difficulty: string) {
  if (difficulty === 'easy') return 'Dễ'
  if (difficulty === 'medium') return 'Trung bình'
  if (difficulty === 'hard') return 'Khó'
  return difficulty
}

function shouldIgnoreSpace(target: EventTarget | null) {
  if (!(target instanceof HTMLElement)) return false
  const tag = target.tagName
  if (tag === 'TEXTAREA' || tag === 'INPUT' || tag === 'SELECT') return true
  if (target.isContentEditable) return true
  return Boolean(target.closest('[data-space-ignore="true"]'))
}

function speakQuestion(content: string) {
  void playNaturalSpeech(content, DEFAULT_TTS_VOICE, 0.96).catch((error: unknown) => {
    if (error instanceof Error && error.name === 'AbortError') return
    console.warn('Không thể phát câu hỏi bằng giọng AI.', error)
  })
}

export function MockExamSessionPage() {
  const [searchParams] = useSearchParams()
  const sessionId = searchParams.get('session')
  const [session] = useState<StoredMockExamSession | null>(() => {
    const stored = readMockExamSession()
    return stored && stored.sessionId === sessionId ? stored : null
  })

  if (!session) return <MissingSession />
  return <MockExamRoom session={session} />
}

function MissingSession() {
  return (
    <div className="flex min-h-dvh items-center justify-center bg-white px-4 text-[#0F2854]">
      <section className="max-w-md text-center">
        <h1 className="text-xl font-bold">Không tìm thấy ca thi đang mở</h1>
        <p className="mt-2 text-sm leading-relaxed text-[#5f6f87]">
          Ca thi chỉ được khôi phục trên trình duyệt đã bắt đầu bài. Hãy quay lại phòng chờ để tạo
          ca mới.
        </p>
        <Link
          to="/mock-exam"
          className="mt-5 inline-flex min-h-11 items-center justify-center gap-2 rounded-full bg-[#f5c518] px-5 text-sm font-bold text-[#1b1600]"
        >
          <ArrowLeft className="size-4" /> Về phòng chờ
        </Link>
      </section>
    </div>
  )
}

function MockExamRoom({ session }: { session: StoredMockExamSession }) {
  const navigate = useNavigate()
  const { isRecording, startRecording, stopRecording, error: recorderError } = useAudioRecorder()
  const [phase, setPhase] = useState<RoomPhase>('exam')
  const [questionIndex, setQuestionIndex] = useState(0)
  const [secondsLeft, setSecondsLeft] = useState(() => secondsUntilExpiry(session.expiresAt))
  const [answers, setAnswers] = useState<Record<string, StoredMockExamAnswer>>(() =>
    Object.fromEntries(session.answers.map((answer) => [answer.questionId, answer])),
  )
  const [drafts, setDrafts] = useState<Record<string, string>>(() =>
    Object.fromEntries(session.answers.map((answer) => [answer.questionId, answer.transcript])),
  )
  const [audioBlob, setAudioBlob] = useState<Blob | null>(null)
  const [recordedSeconds, setRecordedSeconds] = useState(0)
  const [recordingSeconds, setRecordingSeconds] = useState(0)
  const [isSendingAnswer, setIsSendingAnswer] = useState(false)
  const [answerError, setAnswerError] = useState('')
  const [submitError, setSubmitError] = useState('')
  const [scorecard, setScorecard] = useState<MockExamScorecardData | null>(null)
  const [autoRead, setAutoRead] = useState(false)
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [warning, setWarning] = useState<ExamWarning | null>(null)
  const warningResolveRef = useRef<((confirmed: boolean) => void) | null>(null)
  const warningOpenRef = useRef(false)
  const urgentWarnedRef = useRef(false)
  const playbackUrlRef = useRef<string | null>(null)
  const recordingSecondsRef = useRef(0)
  const finalizeLock = useRef(false)
  const didAutoSubmit = useRef(false)
  const stopPromiseRef = useRef<Promise<Blob> | null>(null)
  const holdWantedRef = useRef(false)
  const recorderLiveRef = useRef(false)
  const inHoldChain = useRef(false)
  const sendingRef = useRef(false)
  const forceSubmitRef = useRef(false)
  const closingRef = useRef(false)
  const holdChainRef = useRef(Promise.resolve())
  const applyHoldRef = useRef<() => Promise<void>>(async () => {})
  const finalizeExamRef = useRef<(reason: 'manual' | 'timeout') => Promise<void>>(async () => {})
  const settingsRef = useRef<HTMLDivElement>(null)
  const runtimeRef = useRef<ExamRuntime>({
    phase: 'exam',
    questionId: session.questions[0]?.id ?? '',
    audioBlob: null,
    recordedSeconds: 0,
    answers: Object.fromEntries(session.answers.map((answer) => [answer.questionId, answer])),
  })

  const question = session.questions[questionIndex] ?? null
  const savedAnswer = question ? answers[question.id] : undefined
  const draft = question ? (drafts[question.id] ?? '') : ''
  const transcriptDirty = Boolean(
    savedAnswer && draft.trim() && draft.trim() !== savedAnswer.transcript,
  )
  const isUrgent = secondsLeft <= 60
  const showTranscript =
    Boolean(question) && !isRecording && (isSendingAnswer || Boolean(savedAnswer))

  const replacePlayback = useCallback((blob: Blob | null) => {
    if (playbackUrlRef.current) URL.revokeObjectURL(playbackUrlRef.current)
    playbackUrlRef.current = blob ? URL.createObjectURL(blob) : null
    setAudioBlob(blob)
    runtimeRef.current.audioBlob = blob
  }, [])

  const uploadAnswer = useCallback(
    async (
      questionId: string,
      file: Blob | null,
      timeTakenSeconds: number,
      transcript?: string,
    ) => {
      const response = await mockExamService.submitAnswer(session.sessionId, {
        questionId,
        file,
        timeTakenSeconds,
        transcript,
      })
      const stored: StoredMockExamAnswer = {
        questionId: response.questionId,
        transcript: response.transcript,
        timeTakenSeconds,
        answerId: response.answerId,
        answeredAt: response.answeredAt,
      }
      setAnswers((current) => {
        const next = { ...current, [stored.questionId]: stored }
        runtimeRef.current.answers = next
        return next
      })
      setDrafts((current) => ({ ...current, [stored.questionId]: stored.transcript }))
      saveMockExamAnswer(stored)
      if (file) replacePlayback(null)
      return stored
    },
    [replacePlayback, session.sessionId],
  )

  const finishRecording = useCallback(() => {
    if (stopPromiseRef.current) return stopPromiseRef.current
    const stopping = (async () => {
      const blob = await stopRecording()
      const seconds = Math.max(1, recordingSecondsRef.current)
      setRecordedSeconds(seconds)
      runtimeRef.current.recordedSeconds = seconds
      replacePlayback(blob)
      return blob
    })().finally(() => {
      stopPromiseRef.current = null
    })
    stopPromiseRef.current = stopping
    return stopping
  }, [replacePlayback, stopRecording])

  const transcribeReleased = useCallback(
    async (blob: Blob) => {
      const questionId = runtimeRef.current.questionId
      if (!questionId) return
      sendingRef.current = true
      setIsSendingAnswer(true)
      setAnswerError('')
      try {
        await uploadAnswer(questionId, blob, Math.max(1, recordingSecondsRef.current))
      } catch (error) {
        if (getMockExamErrorStatus(error) === 410) {
          holdWantedRef.current = false
          forceSubmitRef.current = true
          return
        }
        setAnswerError(getMockExamErrorDetail(error, 'Không thể tạo transcript từ bản ghi.'))
      } finally {
        sendingRef.current = false
        setIsSendingAnswer(false)
      }
    },
    [uploadAnswer],
  )

  const applyHold = useCallback(async () => {
    while (holdWantedRef.current !== recorderLiveRef.current) {
      if (holdWantedRef.current) {
        while (sendingRef.current && holdWantedRef.current) {
          await new Promise((resolve) => window.setTimeout(resolve, 80))
        }
        if (!holdWantedRef.current || runtimeRef.current.phase !== 'exam') continue
        try {
          stopSpeechPlayback()
          setAnswerError('')
          recordingSecondsRef.current = 0
          setRecordingSeconds(0)
          await startRecording()
          recorderLiveRef.current = true
        } catch (error) {
          holdWantedRef.current = false
          recorderLiveRef.current = false
          setAnswerError(
            error instanceof Error
              ? error.message
              : 'Không thể bắt đầu ghi âm. Hãy kiểm tra micro.',
          )
        }
      } else {
        try {
          const blob = await finishRecording()
          recorderLiveRef.current = false
          if (!closingRef.current && blob) await transcribeReleased(blob)
        } catch (error) {
          recorderLiveRef.current = false
          setAnswerError(error instanceof Error ? error.message : 'Không thể dừng bản ghi âm.')
        }
      }
    }
    if (forceSubmitRef.current) {
      forceSubmitRef.current = false
      await finalizeExamRef.current('timeout')
    }
  }, [finishRecording, startRecording, transcribeReleased])

  const requestHold = useCallback((wanted: boolean) => {
    if (wanted && (sendingRef.current || runtimeRef.current.phase !== 'exam'))
      return Promise.resolve()
    holdWantedRef.current = wanted
    const run = holdChainRef.current.then(async () => {
      inHoldChain.current = true
      try {
        await applyHoldRef.current()
      } finally {
        inHoldChain.current = false
      }
    })
    holdChainRef.current = run.catch(() => undefined)
    return run
  }, [])

  const finalizeExam = useCallback(
    async (reason: 'manual' | 'timeout') => {
      if (finalizeLock.current || runtimeRef.current.phase !== 'exam') return
      if (inHoldChain.current) {
        holdWantedRef.current = false
        forceSubmitRef.current = true
        return
      }

      finalizeLock.current = true
      closingRef.current = true
      holdWantedRef.current = false
      setPhase('submitting-exam')
      runtimeRef.current.phase = 'submitting-exam'
      setSubmitError('')

      try {
        if (recorderLiveRef.current) {
          try {
            const blob = await finishRecording()
            recorderLiveRef.current = false
            if (blob && runtimeRef.current.questionId) {
              try {
                await uploadAnswer(
                  runtimeRef.current.questionId,
                  blob,
                  runtimeRef.current.recordedSeconds,
                )
              } catch (error) {
                if (reason === 'manual' && getMockExamErrorStatus(error) !== 410) throw error
              }
            }
          } catch {
            recorderLiveRef.current = false
          }
        } else if (runtimeRef.current.audioBlob && runtimeRef.current.questionId) {
          try {
            await uploadAnswer(
              runtimeRef.current.questionId,
              runtimeRef.current.audioBlob,
              runtimeRef.current.recordedSeconds,
            )
          } catch (error) {
            if (reason === 'manual' && getMockExamErrorStatus(error) !== 410) throw error
          }
        }

        const result = await mockExamService.submitSession(session.sessionId)
        clearMockExamSession()
        stopSpeechPlayback()
        setScorecard(result)
        setPhase('scorecard')
        runtimeRef.current.phase = 'scorecard'
      } catch (error) {
        finalizeLock.current = false
        closingRef.current = false
        setPhase('exam')
        runtimeRef.current.phase = 'exam'
        const message = getMockExamErrorDetail(error, 'Không thể nộp bài thi thử.')
        setSubmitError(message)
        warningOpenRef.current = true
        setWarning({
          title: 'Không nộp được bài',
          message,
          confirmLabel: 'Đã hiểu',
          tone: 'danger',
        })
      }
    },
    [finishRecording, session.sessionId, uploadAnswer],
  )

  useEffect(() => {
    applyHoldRef.current = applyHold
  }, [applyHold])

  useEffect(() => {
    finalizeExamRef.current = finalizeExam
  }, [finalizeExam])

  useEffect(() => {
    runtimeRef.current = {
      phase,
      questionId: question?.id ?? '',
      audioBlob,
      recordedSeconds,
      answers,
    }
  }, [answers, audioBlob, phase, question?.id, recordedSeconds])

  useEffect(() => {
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = previousOverflow
      if (playbackUrlRef.current) URL.revokeObjectURL(playbackUrlRef.current)
      stopSpeechPlayback()
    }
  }, [])

  useEffect(() => {
    if (phase !== 'exam') return
    const tick = () => {
      const next = secondsUntilExpiry(session.expiresAt)
      setSecondsLeft(next)
      if (
        next > 0 &&
        next <= 60 &&
        !urgentWarnedRef.current &&
        !warningOpenRef.current
      ) {
        urgentWarnedRef.current = true
        warningOpenRef.current = true
        setWarning({
          title: 'Sắp hết giờ',
          message:
            'Còn dưới 1 phút. Hết giờ hệ thống tự nộp bài và câu chưa trả lời được tính 0 điểm.',
          confirmLabel: 'Tiếp tục làm bài',
          tone: 'warning',
        })
      }
      if (next === 0 && !didAutoSubmit.current) {
        didAutoSubmit.current = true
        void finalizeExam('timeout')
      }
    }
    const timer = window.setTimeout(tick, 0)
    const interval = window.setInterval(tick, 1000)
    return () => {
      window.clearTimeout(timer)
      window.clearInterval(interval)
    }
  }, [finalizeExam, phase, session.expiresAt])

  useEffect(() => {
    if (!isRecording) return
    const startedAt = Date.now()
    let stopped = false
    const interval = window.setInterval(() => {
      const elapsed = Math.floor((Date.now() - startedAt) / 1000)
      recordingSecondsRef.current = elapsed
      setRecordingSeconds(elapsed)
      if (elapsed >= 180 && !stopped) {
        stopped = true
        void requestHold(false)
      }
    }, 250)
    return () => window.clearInterval(interval)
  }, [isRecording, requestHold])

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.code !== 'Space' || event.repeat || phase !== 'exam') return
      if (warningOpenRef.current || shouldIgnoreSpace(event.target)) return
      event.preventDefault()
      void requestHold(true)
    }
    const onKeyUp = (event: KeyboardEvent) => {
      if (event.code !== 'Space' || phase !== 'exam') return
      if (warningOpenRef.current || shouldIgnoreSpace(event.target)) return
      event.preventDefault()
      void requestHold(false)
    }
    window.addEventListener('keydown', onKeyDown)
    window.addEventListener('keyup', onKeyUp)
    return () => {
      window.removeEventListener('keydown', onKeyDown)
      window.removeEventListener('keyup', onKeyUp)
    }
  }, [phase, requestHold])

  useEffect(() => {
    if (!autoRead || !question || phase !== 'exam') return
    speakQuestion(question.content)
    return () => {
      stopSpeechPlayback()
    }
  }, [autoRead, phase, question])

  useEffect(() => {
    if (!settingsOpen) return
    const onPointerDown = (event: PointerEvent) => {
      if (!settingsRef.current?.contains(event.target as Node)) setSettingsOpen(false)
    }
    window.addEventListener('pointerdown', onPointerDown)
    return () => window.removeEventListener('pointerdown', onPointerDown)
  }, [settingsOpen])

  const closeWarning = useCallback((confirmed: boolean) => {
    warningOpenRef.current = false
    const resolve = warningResolveRef.current
    warningResolveRef.current = null
    setWarning(null)
    resolve?.(confirmed)
  }, [])

  const askWarning = useCallback((nextWarning: ExamWarning) => {
    if (warningOpenRef.current) return Promise.resolve(false)
    warningOpenRef.current = true
    return new Promise<boolean>((resolve) => {
      warningResolveRef.current = resolve
      setWarning(nextWarning)
    })
  }, [])

  useEffect(() => {
    if (!warning) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      event.preventDefault()
      closeWarning(false)
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [closeWarning, warning])

  const moveToQuestion = async (nextIndex: number) => {
    if (isRecording || isSendingAnswer || nextIndex === questionIndex) return
    if (nextIndex < 0 || nextIndex >= session.questions.length) return
    if (transcriptDirty && question && savedAnswer) {
      const nextTranscript = draft.trim()
      if (!nextTranscript) {
        setAnswerError('Transcript không được để trống. Hãy giữ nội dung hoặc nói lại.')
        return
      }
      setIsSendingAnswer(true)
      try {
        await uploadAnswer(question.id, null, savedAnswer.timeTakenSeconds, nextTranscript)
      } catch (error) {
        if (getMockExamErrorStatus(error) === 410) {
          void finalizeExam('timeout')
          return
        }
        setAnswerError(
          getMockExamErrorDetail(error, 'Không thể lưu transcript trước khi chuyển câu.'),
        )
        return
      } finally {
        setIsSendingAnswer(false)
      }
    } else if (audioBlob) {
      const confirmed = await askWarning({
        title: 'Bỏ bản ghi chưa gửi?',
        message: 'Bản ghi này chưa được gửi. Chuyển câu sẽ bỏ bản ghi và không chấm câu hiện tại.',
        confirmLabel: 'Vẫn chuyển câu',
        cancelLabel: 'Ở lại',
        tone: 'warning',
      })
      if (!confirmed) return
    }
    const nextQuestion = session.questions[nextIndex]
    if (nextQuestion) runtimeRef.current.questionId = nextQuestion.id
    replacePlayback(null)
    setAnswerError('')
    setRecordedSeconds(0)
    setQuestionIndex(nextIndex)
  }

  const handleUpdateTranscript = async () => {
    if (!question || !savedAnswer || !transcriptDirty || isSendingAnswer) return
    setIsSendingAnswer(true)
    setAnswerError('')
    try {
      await uploadAnswer(question.id, null, savedAnswer.timeTakenSeconds, draft.trim())
    } catch (error) {
      if (getMockExamErrorStatus(error) === 410) {
        void finalizeExam('timeout')
        return
      }
      setAnswerError(getMockExamErrorDetail(error, 'Không thể cập nhật transcript.'))
    } finally {
      setIsSendingAnswer(false)
    }
  }

  const handleSubmit = async () => {
    if (isRecording || isSendingAnswer) return
    const savedCount = session.questions.filter((item) => answers[item.id]).length
    const unanswered = session.questions.length - savedCount
    if (unanswered > 0) {
      const confirmed = await askWarning({
        title: 'Nộp khi còn câu chưa làm?',
        message: `Còn ${unanswered} câu chưa trả lời. Những câu này được tính 0 điểm.`,
        confirmLabel: 'Vẫn nộp bài',
        cancelLabel: 'Quay lại',
        tone: 'danger',
      })
      if (!confirmed) return
    }
    void finalizeExam('manual')
  }

  const leaveRoom = async () => {
    const confirmed = await askWarning({
      title: 'Rời phòng thi?',
      message:
        'Đồng hồ máy chủ vẫn tiếp tục chạy. Bạn có thể quay lại phòng thi trước khi hết giờ.',
      confirmLabel: 'Rời phòng',
      cancelLabel: 'Ở lại',
      tone: 'warning',
    })
    if (!confirmed) return
    stopSpeechPlayback()
    navigate('/mock-exam')
  }

  const warningDialog = warning ? (
    <ConfirmDialog
      title={warning.title}
      message={warning.message}
      tone={warning.tone}
      confirmLabel={warning.confirmLabel}
      cancelLabel={warning.cancelLabel}
      onConfirm={() => closeWarning(true)}
      onCancel={() => closeWarning(false)}
    />
  ) : null

  if (phase === 'submitting-exam') {
    return (
      <div className="fixed inset-0 z-[80] flex items-center justify-center bg-[#0F2854]/45 px-4">
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="mock-grading-title"
          className="w-full max-w-md rounded-2xl border border-[#4988C4]/45 bg-white px-6 py-8 text-center text-[#0F2854] shadow-[0_24px_60px_rgba(16,32,61,0.22)]"
        >
          <LoaderCircle className="mx-auto size-8 animate-spin text-[#1C4D8D]" />
          <h1 id="mock-grading-title" className="mt-4 text-xl font-bold">
            AI đang chấm bài thi thử
          </h1>
          <p className="mx-auto mt-2 max-w-md text-sm leading-relaxed text-[#5f6f87]">
            Bảng điểm theo rubric thường trả về trong vài giây. Vui lòng giữ trang này mở.
          </p>
        </div>
      </div>
    )
  }

  if (phase === 'scorecard' && scorecard) {
    return (
      <div className="fixed inset-0 z-[80] flex items-end justify-center bg-[#0F2854]/45 px-4 py-6 sm:items-center">
        <div
          role="dialog"
          aria-modal="true"
          className="max-h-[90dvh] w-full max-w-3xl space-y-4 overflow-y-auto rounded-2xl border border-[#4988C4]/45 bg-white p-4 text-[#0F2854] shadow-[0_24px_60px_rgba(16,32,61,0.22)] sm:p-5"
        >
          <MockExamScorecard scorecard={scorecard} />
          <div className="flex justify-end">
            <button
              type="button"
              onClick={() => navigate('/mock-exam')}
              className="inline-flex min-h-11 items-center justify-center gap-2 rounded-full bg-[#f5c518] px-5 text-sm font-bold text-[#1b1600]"
            >
              Về phòng chờ <ArrowRight className="size-4" />
            </button>
          </div>
        </div>
      </div>
    )
  }

  const isLastQuestion = questionIndex >= session.questions.length - 1

  return (
    <div className="fixed inset-0 z-[80] flex items-center justify-center bg-[#0F2854]/45 p-3 text-[#0F2854] sm:p-4">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="mock-session-title"
        className="flex max-h-[calc(100dvh-1.5rem)] w-full max-w-5xl flex-col overflow-y-auto rounded-[22px] border border-[#4988C4]/45 bg-white shadow-[0_24px_60px_rgba(16,32,61,0.22)] sm:max-h-[calc(100dvh-2rem)]"
      >
        <header className="relative z-20 shrink-0 bg-[linear-gradient(118deg,#0F2854_0%,#1C4D8D_58%,#4988C4_100%)] text-white">
          <div className="flex items-center justify-between gap-3 px-5 py-4 sm:px-6">
          <div className="min-w-0">
            <p className="text-xs font-bold tracking-[0.08em] text-white/75 uppercase">
              {session.courseCode}
            </p>
            <h1 id="mock-session-title" className="truncate text-base font-bold sm:text-lg">
              {session.courseName}
            </h1>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <div
              role="timer"
              aria-live="off"
              aria-label={`Thời gian còn lại ${formatClock(secondsLeft)}`}
              className={cn(
                'inline-flex min-h-10 items-center gap-2 rounded-full px-3.5 text-sm font-bold',
                isUrgent
                  ? 'animate-pulse bg-[#c2415b] text-white motion-reduce:animate-none'
                  : 'bg-white text-[#0F2854]',
              )}
            >
              <Clock3 className="size-4" />
              {formatClock(secondsLeft)}
            </div>
            <div className="relative" ref={settingsRef}>
              <button
                type="button"
                data-space-ignore="true"
                aria-label="Cài đặt phòng thi"
                aria-expanded={settingsOpen}
                onClick={() => setSettingsOpen((current) => !current)}
                className="flex size-10 items-center justify-center rounded-full border border-white/30 bg-white/10 text-white transition hover:bg-white/20"
              >
                <Settings className="size-5" />
              </button>
              {settingsOpen && (
                <div
                  data-space-ignore="true"
                  className="absolute top-12 right-0 z-30 w-72 rounded-2xl border border-[#4988C4]/45 bg-white p-3 text-left text-[#0F2854] shadow-[0_18px_50px_rgba(15,40,84,0.18)]"
                >
                  <button
                    type="button"
                    data-space-ignore="true"
                    aria-pressed={autoRead}
                    onClick={() => setAutoRead((current) => !current)}
                    className="flex w-full items-center justify-between gap-3 rounded-xl px-2 py-2 text-left"
                  >
                    <span>
                      <span className="block text-sm font-semibold">Tự động đọc câu</span>
                      <span className="mt-1 block text-xs leading-relaxed text-[#5b6b84]">
                        Đọc câu hỏi khi chuyển câu. Tắt thì không phát giọng nói.
                      </span>
                    </span>
                    <span
                      className={cn(
                        'relative h-6 w-11 shrink-0 rounded-full transition',
                        autoRead ? 'bg-[#3dce7a]' : 'bg-[#d5deea]',
                      )}
                    >
                      <span
                        className={cn(
                          'absolute top-0.5 size-5 rounded-full bg-white transition',
                          autoRead ? 'left-5' : 'left-0.5',
                        )}
                      />
                    </span>
                  </button>
                </div>
              )}
            </div>
            <button
              type="button"
              data-space-ignore="true"
              aria-label="Rời phòng thi"
              onClick={leaveRoom}
              className="flex size-10 items-center justify-center rounded-full border border-white/30 bg-white/10 text-white transition hover:bg-white/20"
            >
              <X className="size-5" />
            </button>
          </div>
          </div>
          <div className="flex items-center gap-3 border-t border-white/15 px-5 py-3 sm:px-6">
            <span className="text-xs font-bold text-white/90">
              Câu {session.questions.length === 0 ? 0 : questionIndex + 1}/{session.questions.length}
            </span>
            <div className="flex h-1.5 min-w-0 flex-1 gap-1.5">
              {session.questions.map((item, index) => (
                <span
                  key={item.id}
                  className={cn(
                    'h-full flex-1 rounded-full',
                    answers[item.id]
                      ? 'bg-white'
                      : index === questionIndex
                        ? 'bg-[#BDE8F5]'
                        : 'bg-white/25',
                  )}
                />
              ))}
            </div>
          </div>
        </header>

        {question ? (
          <section className="flex flex-col gap-4 px-5 py-5 sm:px-6">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <p className="text-sm font-semibold text-[#5b6b84]">
                {question.bloomLevel} · {difficultyLabel(question.difficulty)}
              </p>
              <button
                type="button"
                data-space-ignore="true"
                onClick={() => speakQuestion(question.content)}
                className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-[#4988C4] bg-white px-3 text-sm font-bold text-[#1C4D8D] transition hover:bg-[#F4FBFD]"
              >
                <Volume2 className="size-4" />
                Đọc câu hỏi
              </button>
            </div>
            <QuestionImage imageUrl={question.imageUrl} imageAlt={question.imageAlt} />
            <h2 className="text-[24px] leading-snug font-semibold text-[#0F2854]">
              Câu {question.orderIndex}. {question.content}
            </h2>

            <div className="flex flex-col items-center gap-3 py-1">
              <button
                type="button"
                disabled={isSendingAnswer}
                aria-label={isRecording ? 'Đang nói, thả ra để kết thúc' : 'Nhấn giữ để trả lời'}
                onPointerDown={(event) => {
                  if (event.button !== 0 || isSendingAnswer) return
                  event.preventDefault()
                  event.currentTarget.setPointerCapture(event.pointerId)
                  void requestHold(true)
                }}
                onPointerUp={() => void requestHold(false)}
                onPointerCancel={() => void requestHold(false)}
                className={cn(
                  'flex size-32 touch-none flex-col items-center justify-center gap-1 rounded-full border text-sm font-bold shadow-[0_12px_28px_rgba(28,77,141,0.16)] transition select-none disabled:opacity-60',
                  isRecording
                    ? 'border-[#ff8fa0] bg-[#ff5d73] text-white shadow-[0_12px_28px_rgba(255,93,115,0.28)]'
                    : 'border-[#4988C4] bg-[#F4FBFD] text-[#0F2854] hover:bg-[#BDE8F5]',
                )}
              >
                <Mic
                  className={cn(
                    'size-8',
                    isRecording && 'animate-pulse motion-reduce:animate-none',
                  )}
                />
                <span className="text-xs font-bold">
                  {isRecording ? formatClock(recordingSeconds) : 'Giữ để nói'}
                </span>
              </button>
              <p className="text-center text-sm font-bold text-[#0F2854]">
                {isRecording ? 'Thả ra để kết thúc' : 'Nhấn giữ để trả lời'}
              </p>
              <p className="text-center text-xs leading-relaxed text-[#5b6b84]">
                Giữ nút tròn hoặc phím{' '}
                <kbd className="rounded bg-[#F4FBFD] px-1.5 py-0.5 font-bold text-[#0F2854]">Space</kbd>
                . Thả ra để xem transcript.
              </p>
            </div>

            {showTranscript && (
              <div className="flex flex-col gap-2">
                <label htmlFor="mock-transcript" className="text-sm font-semibold text-[#0F2854]">
                  Transcript để kiểm tra và chỉnh sửa
                </label>
                {isSendingAnswer && !savedAnswer ? (
                  <div className="flex h-28 items-center gap-2 rounded-2xl border border-[#4988C4]/45 bg-[#F4FBFD] px-4 text-sm text-[#5b6b84]">
                    <LoaderCircle className="size-4 animate-spin" />
                    Đang nhận diện giọng nói tiếng Việt…
                  </div>
                ) : (
                  <textarea
                    id="mock-transcript"
                    value={draft}
                    onChange={(event) =>
                      setDrafts((current) => ({ ...current, [question.id]: event.target.value }))
                    }
                    className="h-28 w-full resize-none rounded-2xl border border-[#4988C4] bg-white px-4 py-3 text-sm leading-relaxed text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30"
                  />
                )}
                {transcriptDirty && (
                  <button
                    type="button"
                    data-space-ignore="true"
                    onClick={() => void handleUpdateTranscript()}
                    disabled={isSendingAnswer}
                    className="inline-flex min-h-10 w-fit items-center rounded-xl border border-[#4988C4] bg-white px-4 text-sm font-bold text-[#1C4D8D] disabled:opacity-50"
                  >
                    Lưu chỉnh sửa
                  </button>
                )}
              </div>
            )}

            {(answerError || recorderError) && (
              <p className="text-sm leading-relaxed text-[#c2415b]">
                {answerError || recorderError}
              </p>
            )}
            {submitError && (
              <p className="text-sm leading-relaxed text-[#c2415b]">{submitError}</p>
            )}

            <div className="flex flex-wrap items-center justify-end gap-2">
              {!isLastQuestion && !savedAnswer && (
                <button
                  type="button"
                  data-space-ignore="true"
                  onClick={() => void moveToQuestion(questionIndex + 1)}
                  disabled={isRecording || isSendingAnswer}
                  className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-[#4988C4]/45 bg-white px-4 text-sm font-bold text-[#0F2854] shadow-[0_3px_8px_rgba(23,48,96,0.05)] hover:border-[#4988C4]/45 disabled:opacity-40"
                >
                  Bỏ qua
                  <ArrowRight className="size-4" />
                </button>
              )}
              {!isLastQuestion && savedAnswer && (
                <button
                  type="button"
                  data-space-ignore="true"
                  onClick={() => void moveToQuestion(questionIndex + 1)}
                  disabled={isRecording || isSendingAnswer}
                  className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[#f5c518] px-4 text-sm font-bold text-[#1b1600] disabled:opacity-40"
                >
                  Câu tiếp
                  <ArrowRight className="size-4" />
                </button>
              )}
              {isLastQuestion && (
                <button
                  type="button"
                  data-space-ignore="true"
                  onClick={handleSubmit}
                  disabled={isRecording || isSendingAnswer}
                  className="inline-flex min-h-11 items-center justify-center rounded-xl bg-[#1C4D8D] px-5 text-sm font-bold text-white shadow-[0_8px_16px_rgba(36,94,234,0.22)] disabled:opacity-40"
                >
                  Nộp bài
                </button>
              )}
            </div>
          </section>
        ) : (
          <section className="flex w-full flex-1 flex-col items-center justify-center rounded-2xl border border-[#4988C4]/45 bg-white px-6 py-8 text-center shadow-[0_16px_40px_rgba(31,61,105,0.06)]">
            <p className="text-sm text-[#3d4e6b]">
              Ca thi không có câu hỏi. Bạn có thể nộp để đóng phiên.
            </p>
            <button
              type="button"
              data-space-ignore="true"
              onClick={handleSubmit}
              className="mt-5 inline-flex min-h-11 items-center rounded-full bg-[#f5c518] px-5 text-sm font-bold text-[#1b1600]"
            >
              Nộp bài
            </button>
          </section>
        )}
      </div>
      {warningDialog}
    </div>
  )
}
