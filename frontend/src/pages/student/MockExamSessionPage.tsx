import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { ArrowLeft, ArrowRight, Clock3, Flag, LoaderCircle, Mic, Settings, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import { MockExamScorecard } from '@/components/mock-exam/MockExamScorecard'
import { useAudioRecorder } from '@/hooks/useAudioRecorder'
import {
  getMockExamErrorDetail,
  getMockExamErrorStatus,
  mockExamService,
} from '@/services/mockExam.service'
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

function speakQuestion(title: string, content: string) {
  if (!('speechSynthesis' in window) || !('SpeechSynthesisUtterance' in window)) return
  const start = () => {
    const utterance = new SpeechSynthesisUtterance(`${title}. ${content}`)
    utterance.lang = 'vi-VN'
    utterance.rate = 0.96
    const voice = window.speechSynthesis
      .getVoices()
      .find((item) => item.lang.toLocaleLowerCase().startsWith('vi'))
    if (voice) utterance.voice = voice
    window.speechSynthesis.cancel()
    window.speechSynthesis.speak(utterance)
  }
  if (window.speechSynthesis.getVoices().length > 0) {
    start()
    return
  }
  const onVoices = () => {
    window.speechSynthesis.removeEventListener('voiceschanged', onVoices)
    start()
  }
  window.speechSynthesis.addEventListener('voiceschanged', onVoices)
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
    <div className="flex min-h-dvh items-center justify-center bg-[#f4f7fc] px-4 text-[#10203d]">
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
          if ('speechSynthesis' in window) window.speechSynthesis.cancel()
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
        if ('speechSynthesis' in window) window.speechSynthesis.cancel()
        setScorecard(result)
        setPhase('scorecard')
        runtimeRef.current.phase = 'scorecard'
      } catch (error) {
        finalizeLock.current = false
        closingRef.current = false
        setPhase('exam')
        runtimeRef.current.phase = 'exam'
        setSubmitError(getMockExamErrorDetail(error, 'Không thể nộp bài thi thử.'))
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
      if ('speechSynthesis' in window) window.speechSynthesis.cancel()
    }
  }, [])

  useEffect(() => {
    if (phase !== 'exam') return
    const tick = () => {
      const next = secondsUntilExpiry(session.expiresAt)
      setSecondsLeft(next)
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
      if (shouldIgnoreSpace(event.target)) return
      event.preventDefault()
      void requestHold(true)
    }
    const onKeyUp = (event: KeyboardEvent) => {
      if (event.code !== 'Space' || phase !== 'exam') return
      if (shouldIgnoreSpace(event.target)) return
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
    speakQuestion(question.title, question.content)
    return () => {
      if ('speechSynthesis' in window) window.speechSynthesis.cancel()
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
      const confirmed = window.confirm('Bản ghi chưa gửi sẽ bị bỏ khi chuyển câu. Tiếp tục?')
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

  const handleSubmit = () => {
    if (isRecording || isSendingAnswer) return
    const savedCount = session.questions.filter((item) => answers[item.id]).length
    const unanswered = session.questions.length - savedCount
    if (unanswered > 0) {
      const confirmed = window.confirm(
        `Còn ${unanswered} câu chưa trả lời. Những câu này sẽ được tính 0 điểm. Nộp bài?`,
      )
      if (!confirmed) return
    }
    void finalizeExam('manual')
  }

  const leaveRoom = () => {
    const confirmed = window.confirm(
      'Đồng hồ máy chủ vẫn tiếp tục chạy. Bạn có thể quay lại phòng thi trước khi hết giờ.',
    )
    if (confirmed) {
      if ('speechSynthesis' in window) window.speechSynthesis.cancel()
      navigate('/mock-exam')
    }
  }

  if (phase === 'submitting-exam') {
    return (
      <div className="flex min-h-dvh items-center justify-center bg-[#f4f7fc] px-4 text-center text-[#10203d]">
        <div>
          <LoaderCircle className="mx-auto size-8 animate-spin text-[#315fc4]" />
          <h1 className="mt-4 text-xl font-bold">AI đang chấm bài thi thử</h1>
          <p className="mx-auto mt-2 max-w-md text-sm leading-relaxed text-[#5f6f87]">
            Bảng điểm theo rubric thường trả về trong vài giây. Vui lòng giữ trang này mở.
          </p>
        </div>
      </div>
    )
  }

  if (phase === 'scorecard' && scorecard) {
    return (
      <div className="h-dvh overflow-y-auto overscroll-y-contain bg-[#f4f7fc] px-4 py-6 text-[#10203d] sm:px-6">
        <div className="mx-auto max-w-3xl space-y-4">
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

  return (
    <div className="h-dvh overflow-y-auto overscroll-y-contain bg-[#f4f7fc] text-[#10203d]">
      <div className="sticky top-0 z-20 bg-[#f4f7fc] pb-4">
        <header className="flex items-center justify-between gap-3 px-4 py-4 sm:px-8">
          <div className="inline-flex min-h-10 items-center gap-2 rounded-full px-1 text-sm font-semibold text-[#10203d]">
            <span className="flex size-7 items-center justify-center rounded-full bg-[#3d4fff] text-xs font-bold text-white">
              {session.courseCode.slice(0, 1)}
            </span>
            <span className="max-w-[180px] truncate sm:max-w-xs">{session.courseCode}</span>
          </div>
          <div className="flex items-center gap-2">
            <div
              role="timer"
              aria-live="off"
              aria-label={`Thời gian còn lại ${formatClock(secondsLeft)}`}
              className={cn(
                'inline-flex min-h-10 items-center gap-2 rounded-full px-4 text-sm font-bold',
                isUrgent
                  ? 'animate-pulse bg-[#ff5d73] text-white motion-reduce:animate-none'
                  : 'bg-[#f5c518] text-[#1b1600]',
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
                className="flex size-10 items-center justify-center rounded-full text-[#52627a] transition hover:bg-[#e7edf6]"
              >
                <Settings className="size-5" />
              </button>
              {settingsOpen && (
                <div
                  data-space-ignore="true"
                  className="absolute top-12 right-0 z-10 w-64 rounded-2xl border border-[#dce5f1] bg-white p-3 text-left shadow-[0_18px_50px_rgba(31,61,105,0.12)]"
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
                      <span className="mt-1 block text-xs leading-relaxed text-[#5f6f87]">
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
              className="flex size-10 items-center justify-center rounded-full text-[#52627a] transition hover:bg-[#e7edf6]"
            >
              <X className="size-5" />
            </button>
          </div>
        </header>

        <div className="mx-auto flex w-full max-w-3xl items-center gap-3 px-6">
          <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-[#3dce7a] text-sm font-bold text-[#06210f]">
            {session.questions.length === 0 ? 0 : questionIndex + 1}
          </span>
          <div className="flex h-1.5 min-w-0 flex-1 gap-1.5">
            {session.questions.map((item, index) => (
              <span
                key={item.id}
                className={cn(
                  'h-full flex-1 rounded-full',
                  answers[item.id]
                    ? 'bg-[#6f7de8]'
                    : index === questionIndex
                      ? 'bg-[#8d98ea]'
                      : 'bg-[#d5deea]',
                )}
              />
            ))}
          </div>
          <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-[#e7edf6] text-xs font-bold text-[#52627a]">
            {session.questions.length}
          </span>
        </div>
      </div>

      <main className="mx-auto w-full max-w-3xl px-4 pb-8 sm:px-6">
        {question ? (
          <section className="w-full rounded-[28px] border border-[#dce5f1] bg-white px-5 py-7 shadow-[0_16px_40px_rgba(31,61,105,0.06)] sm:px-8 sm:py-8">
            <p className="text-sm text-[#6b7c99]">
              {question.bloomLevel} · {difficultyLabel(question.difficulty)}
            </p>
            <h1 className="mt-4 text-[22px] leading-snug font-semibold text-[#10203d] sm:text-[26px]">
              Câu {question.orderIndex}. {question.content}
            </h1>


            <div className="mt-8">
              <button
                type="button"
                disabled={isSendingAnswer}
                onPointerDown={(event) => {
                  if (event.button !== 0 || isSendingAnswer) return
                  event.preventDefault()
                  event.currentTarget.setPointerCapture(event.pointerId)
                  void requestHold(true)
                }}
                onPointerUp={() => void requestHold(false)}
                onPointerCancel={() => void requestHold(false)}
                className={cn(
                  'flex min-h-16 w-full touch-none items-center justify-center gap-3 rounded-2xl border text-sm font-bold transition select-none disabled:opacity-60',
                  isRecording
                    ? 'border-[#ff8fa0] bg-[#ff5d73] text-white'
                    : 'border-[#d5deea] bg-[#f7f9fc] text-[#10203d] hover:border-[#8ea0c8]',
                )}
              >
                <Mic
                  className={cn(
                    'size-5',
                    isRecording && 'animate-pulse motion-reduce:animate-none',
                  )}
                />
                {isRecording
                  ? `Đang nói ${formatClock(recordingSeconds)} · thả ra để kết thúc`
                  : 'Nhấn giữ để trả lời'}
              </button>
              <p className="mt-3 text-center text-xs text-[#6b7c99]">
                Giữ nút này hoặc giữ phím{' '}
                <kbd className="rounded bg-[#eef2f7] px-1.5 py-0.5 text-[#10203d]">Space</kbd> khi
                nói. Thả ra để xem transcript.
              </p>
            </div>

            {showTranscript && (
              <div className="mt-6">
                <label htmlFor="mock-transcript" className="text-sm font-semibold text-[#10203d]">
                  Transcript để kiểm tra và chỉnh sửa
                </label>
                {isSendingAnswer && !savedAnswer ? (
                  <div className="mt-3 flex items-center gap-2 rounded-2xl bg-[#f7f9fc] px-4 py-6 text-sm text-[#52627a]">
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
                    className="mt-3 min-h-32 w-full resize-y rounded-2xl border border-[#dce5f1] bg-white px-4 py-3 text-sm leading-relaxed text-[#10203d] outline-none focus:border-[#6f7de8]"
                  />
                )}
                {transcriptDirty && (
                  <button
                    type="button"
                    data-space-ignore="true"
                    onClick={() => void handleUpdateTranscript()}
                    disabled={isSendingAnswer}
                    className="mt-3 inline-flex min-h-10 items-center rounded-full bg-[#eef2f7] px-4 text-sm font-bold text-[#10203d] disabled:opacity-50"
                  >
                    Lưu chỉnh sửa
                  </button>
                )}
              </div>
            )}

            {(answerError || recorderError) && (
              <p className="mt-4 text-sm leading-relaxed text-[#c4475c]">
                {answerError || recorderError}
              </p>
            )}
            {submitError && (
              <p className="mt-3 text-sm leading-relaxed text-[#c4475c]">{submitError}</p>
            )}

            <div className="mt-8 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <button
                type="button"
                data-space-ignore="true"
                onClick={handleSubmit}
                disabled={isRecording || isSendingAnswer}
                className="inline-flex min-h-10 items-center justify-center rounded-full px-3 text-sm font-semibold text-[#52627a] disabled:opacity-40"
              >
                Nộp bài
              </button>
              <div className="flex flex-wrap items-center justify-end gap-2">
                {!savedAnswer && questionIndex < session.questions.length - 1 && (
                  <button
                    type="button"
                    data-space-ignore="true"
                    onClick={() => void moveToQuestion(questionIndex + 1)}
                    disabled={isRecording || isSendingAnswer}
                    className="inline-flex min-h-10 items-center gap-2 rounded-full px-3 text-sm font-semibold text-[#315fc4] disabled:opacity-40"
                  >
                    <Flag className="size-4" />
                    Bạn không biết?
                  </button>
                )}
                {savedAnswer && questionIndex < session.questions.length - 1 && (
                  <button
                    type="button"
                    data-space-ignore="true"
                    onClick={() => void moveToQuestion(questionIndex + 1)}
                    disabled={isRecording || isSendingAnswer}
                    className="inline-flex min-h-10 items-center gap-2 rounded-full bg-[#f5c518] px-4 text-sm font-bold text-[#1b1600] disabled:opacity-40"
                  >
                    Câu tiếp <ArrowRight className="size-4" />
                  </button>
                )}
              </div>
            </div>
          </section>
        ) : (
          <section className="w-full rounded-[28px] border border-[#dce5f1] bg-white px-6 py-10 text-center shadow-[0_16px_40px_rgba(31,61,105,0.06)]">
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
      </main>
    </div>
  )
}
