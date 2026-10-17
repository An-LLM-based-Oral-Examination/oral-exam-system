import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import {
  ArrowLeft,
  ArrowRight,
  Clock3,
  Flag,
  LoaderCircle,
  Mic,
  Settings,
  ShieldCheck,
  Volume2,
  X,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { MockExamScorecard } from '@/components/mock-exam/MockExamScorecard'
import { useAudioRecorder } from '@/hooks/useAudioRecorder'
import {
  getMockExamErrorDetail,
  getMockExamErrorStatus,
  mockExamService,
} from '@/services/mockExam.service'
import {
  speechService,
  TTS_RATE_OPTIONS,
  TTS_VOICE_OPTIONS,
  type NeuralVoiceId,
} from '@/services/speech.service'
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
  drafts: Record<string, string>
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

function difficultyBadgeClassName(difficulty: string) {
  if (difficulty === 'easy') return 'border-emerald-200 bg-emerald-50 text-[#16A34A]'
  if (difficulty === 'medium') return 'border-[#FED7AA] bg-[#FFF7ED] text-[#C2410C]'
  if (difficulty === 'hard') return 'border-rose-200 bg-rose-50 text-rose-700'
  return 'border-[#E2E8F0] bg-[#F8FAFC] text-[#475569]'
}

function shouldIgnoreSpace(target: EventTarget | null) {
  if (!(target instanceof HTMLElement)) return false
  const tag = target.tagName
  if (tag === 'TEXTAREA' || tag === 'INPUT' || tag === 'SELECT') return true
  if (target.isContentEditable) return true
  return Boolean(target.closest('[data-space-ignore="true"]'))
}

function speakQuestion(title: string, content: string) {
  void speechService.speakText(`${title}. ${content}`)
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
    <div className="flex min-h-dvh items-center justify-center bg-[#F8FAFC] px-4 text-[#1E293B]">
      <section className="max-w-md rounded-2xl border border-[#E2E8F0] bg-white p-6 text-center shadow-xs">
        <h1 className="text-xl font-bold text-[#1E293B]">Không tìm thấy ca thi đang mở</h1>
        <p className="mt-2 text-sm leading-relaxed text-[#475569]">
          Ca thi chỉ được khôi phục trên trình duyệt đã bắt đầu bài. Hãy quay lại phòng chờ để tạo
          ca mới.
        </p>
        <Link
          to="/mock-exam"
          className="mt-5 inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#F26F21] px-5 text-sm font-bold text-white shadow-xs transition hover:bg-[#D95B16]"
        >
          <ArrowLeft className="size-4" /> Về phòng chờ
        </Link>
      </section>
    </div>
  )
}

function MockExamRoom({ session }: { session: StoredMockExamSession }) {
  const navigate = useNavigate()
  const {
    isRecording,
    liveTranscript,
    startRecording,
    stopRecording,
    error: recorderError,
  } = useAudioRecorder()
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
  const [speechPrefs, setSpeechPrefs] = useState(() => speechService.getPreferences())
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
    drafts: Object.fromEntries(session.answers.map((answer) => [answer.questionId, answer.transcript])),
  })

  const question = session.questions[questionIndex] ?? null
  const savedAnswer = question ? answers[question.id] : undefined
  const draft = question ? (drafts[question.id] ?? '') : ''
  const transcriptDirty = Boolean(
    savedAnswer && draft.trim() && draft.trim() !== savedAnswer.transcript,
  )
  const isUrgent = secondsLeft < 120
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
      setDrafts((current) => {
        const next = { ...current, [stored.questionId]: stored.transcript }
        runtimeRef.current.drafts = next
        return next
      })
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
          speechService.stopSpeaking()
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
        if (forceSubmitRef.current) {
          forceSubmitRef.current = false
          await finalizeExamRef.current('timeout')
        }
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
        const currentQuestionId = runtimeRef.current.questionId
        const currentSavedAnswer = currentQuestionId
          ? runtimeRef.current.answers[currentQuestionId]
          : undefined
        const currentDraft = currentQuestionId
          ? (runtimeRef.current.drafts[currentQuestionId] ?? '').trim()
          : ''

        if (recorderLiveRef.current) {
          try {
            const blob = await finishRecording()
            recorderLiveRef.current = false
            if (blob && currentQuestionId) {
              try {
                await uploadAnswer(
                  currentQuestionId,
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
        } else if (runtimeRef.current.audioBlob && currentQuestionId) {
          try {
            await uploadAnswer(
              currentQuestionId,
              runtimeRef.current.audioBlob,
              runtimeRef.current.recordedSeconds,
            )
          } catch (error) {
            if (reason === 'manual' && getMockExamErrorStatus(error) !== 410) throw error
          }
        } else if (
          currentQuestionId &&
          currentSavedAnswer &&
          currentDraft &&
          currentDraft !== currentSavedAnswer.transcript
        ) {
          try {
            await uploadAnswer(
              currentQuestionId,
              null,
              currentSavedAnswer.timeTakenSeconds,
              currentDraft,
            )
          } catch (error) {
            if (reason === 'manual' && getMockExamErrorStatus(error) !== 410) throw error
          }
        }

        const result = await mockExamService.submitSession(session.sessionId)
        clearMockExamSession()
        speechService.stopSpeaking()
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
      drafts,
    }
  }, [answers, audioBlob, drafts, phase, question?.id, recordedSeconds])

  useEffect(() => {
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = previousOverflow
      if (playbackUrlRef.current) URL.revokeObjectURL(playbackUrlRef.current)
      speechService.stopSpeaking()
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
      speechService.stopSpeaking()
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

  const handleSelectVoice = (voiceName: NeuralVoiceId) => {
    const updated = speechService.savePreferences({ voiceName })
    setSpeechPrefs(updated)
  }

  const handleSelectRate = (speakingRate: number) => {
    const updated = speechService.savePreferences({ speakingRate })
    setSpeechPrefs(updated)
  }

  const moveToQuestion = async (nextIndex: number) => {
    if (isRecording || isSendingAnswer || nextIndex === questionIndex) return
    if (nextIndex < 0 || nextIndex >= session.questions.length) return
    if (transcriptDirty && question && savedAnswer) {
      const nextTranscript = draft.trim()
      if (!nextTranscript) {
        setAnswerError('Transcript không được để trống. Hãy giữ nội dung hoặc nói lại.')
        return
      }
      sendingRef.current = true
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
        sendingRef.current = false
        setIsSendingAnswer(false)
      }
    } else if (audioBlob) {
      const confirmed = window.confirm('Bản ghi chưa gửi sẽ bị bỏ khi chuyển câu. Tiếp tục?')
      if (!confirmed) return
    }
    const nextQuestion = session.questions[nextIndex]
    if (nextQuestion) runtimeRef.current.questionId = nextQuestion.id
    speechService.stopSpeaking()
    replacePlayback(null)
    setAnswerError('')
    setRecordedSeconds(0)
    setQuestionIndex(nextIndex)
  }

  const handleUpdateTranscript = async () => {
    if (!question || !savedAnswer || !transcriptDirty || isSendingAnswer) return
    sendingRef.current = true
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
      sendingRef.current = false
      setIsSendingAnswer(false)
    }
  }

  const handleSubmit = () => {
    if (isRecording || isSendingAnswer) return
    const savedCount = session.questions.filter((item) => answers[item.id]).length
    const unanswered = session.questions.length - savedCount
    if (unanswered > 0) {
      const confirmed = window.confirm(
        `Còn ${unanswered} câu chưa trả lời. Các câu này sẽ để trống (không gửi AI chấm) và tính 0 điểm trong điểm trung bình ca thi. Nộp bài?`,
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
      speechService.stopSpeaking()
      navigate('/mock-exam')
    }
  }

  if (phase === 'submitting-exam') {
    return (
      <div className="flex min-h-dvh items-center justify-center bg-[#F8FAFC] px-4 text-center text-[#1E293B]">
        <div className="rounded-2xl border border-[#E2E8F0] bg-white p-8 shadow-xs">
          <LoaderCircle className="mx-auto size-8 animate-spin text-[#F26F21]" />
          <h1 className="mt-4 text-xl font-bold text-[#1E293B]">AI đang chấm bài thi thử</h1>
          <p className="mx-auto mt-2 max-w-md text-sm leading-relaxed text-[#475569]">
            Bảng điểm theo rubric thường trả về trong vài giây. Vui lòng giữ trang này mở.
          </p>
        </div>
      </div>
    )
  }

  if (phase === 'scorecard' && scorecard) {
    return (
      <div className="h-dvh overflow-y-auto overscroll-y-contain bg-[#F8FAFC] px-4 py-6 text-[#1E293B] sm:px-6">
        <div className="mx-auto max-w-4xl space-y-4">
          <MockExamScorecard scorecard={scorecard} />
          <div className="flex justify-end">
            <button
              type="button"
              onClick={() => navigate('/mock-exam')}
              className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#F26F21] px-5 text-sm font-bold text-white shadow-xs transition hover:bg-[#D95B16]"
            >
              Về phòng chờ <ArrowRight className="size-4" />
            </button>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="h-dvh overflow-y-auto overscroll-y-contain bg-[#F8FAFC] text-[#1E293B]">
      <header className="sticky top-0 z-20 border-b border-[#E2E8F0] bg-white/95 backdrop-blur">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-3 px-4 py-3 sm:px-6">
          <div className="flex min-w-0 items-center gap-2.5">
            <button
              type="button"
              data-space-ignore="true"
              onClick={leaveRoom}
              aria-label="Quay lại phòng chờ thi thử"
              className="inline-flex size-9 shrink-0 items-center justify-center rounded-xl border border-[#E2E8F0] bg-white text-[#475569] transition hover:border-[#FED7AA] hover:bg-[#FFF7ED] hover:text-[#C2410C]"
            >
              <ArrowLeft className="size-4" />
            </button>
            <span className="shrink-0 rounded-full border border-[#FED7AA] bg-[#FFF7ED] px-3 py-1 text-xs font-bold text-[#C2410C]">
              {session.courseCode}
            </span>
            <span className="hidden max-w-56 truncate text-sm font-bold text-[#1E293B] sm:inline md:max-w-xs">
              {session.courseName}
            </span>
          </div>

          <div className="order-3 flex w-full items-center gap-3 md:order-2 md:w-auto md:max-w-md md:flex-1">
            <span className="shrink-0 text-xs font-bold text-[#1E293B]">
              Câu {session.questions.length === 0 ? 0 : questionIndex + 1} /{' '}
              {session.questions.length}
            </span>
            <div className="flex h-2 min-w-0 flex-1 gap-1.5">
              {session.questions.map((item, index) => (
                <span
                  key={item.id}
                  className={cn(
                    'h-full flex-1 rounded-full transition-colors',
                    answers[item.id]
                      ? 'bg-[#16A34A]'
                      : index === questionIndex
                        ? 'bg-[#F26F21]'
                        : 'bg-[#E2E8F0]',
                  )}
                />
              ))}
            </div>
            <span className="shrink-0 text-xs font-semibold text-[#475569]">
              {Object.keys(answers).length}/{session.questions.length} đã trả lời
            </span>
          </div>

          <div className="order-2 flex items-center gap-2 md:order-3">
            <div
              role="timer"
              aria-live="off"
              aria-label={`Thời gian còn lại ${formatClock(secondsLeft)}`}
              className={cn(
                'inline-flex min-h-9 items-center gap-2 rounded-xl border px-3.5 font-mono text-sm font-bold tabular-nums',
                isUrgent
                  ? 'animate-pulse border-rose-300 bg-rose-600 text-white motion-reduce:animate-none'
                  : 'border-[#FED7AA] bg-[#FFF7ED] text-[#C2410C]',
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
                className="flex size-9 items-center justify-center rounded-xl border border-[#E2E8F0] bg-white text-[#475569] transition hover:border-[#FED7AA] hover:bg-[#FFF7ED] hover:text-[#C2410C]"
              >
                <Settings className="size-4" />
              </button>
              {settingsOpen && (
                <div
                  data-space-ignore="true"
                  className="absolute top-11 right-0 z-10 w-72 space-y-3 rounded-2xl border border-[#E2E8F0] bg-white p-3.5 text-left shadow-xl"
                >
                  <button
                    type="button"
                    data-space-ignore="true"
                    aria-pressed={autoRead}
                    onClick={() => setAutoRead((current) => !current)}
                    className="flex w-full items-center justify-between gap-3 rounded-xl px-2 py-2 text-left transition hover:bg-[#F8FAFC]"
                  >
                    <span>
                      <span className="block text-sm font-semibold text-[#1E293B]">
                        Tự động đọc câu
                      </span>
                      <span className="mt-1 block text-xs leading-relaxed text-[#475569]">
                        Đọc câu hỏi khi chuyển câu. Tắt thì không phát giọng nói.
                      </span>
                    </span>
                    <span
                      className={cn(
                        'relative h-6 w-11 shrink-0 rounded-full transition',
                        autoRead ? 'bg-[#F26F21]' : 'bg-[#CBD5E1]',
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

                  <div className="border-t border-[#E2E8F0] pt-3">
                    <span className="block px-1 text-xs font-bold text-[#1E293B]">
                      Giọng đọc AI Giám khảo
                    </span>
                    <div
                      role="group"
                      aria-label="Chọn giọng đọc AI Giám khảo"
                      className="mt-2 grid grid-cols-1 gap-1.5"
                    >
                      {TTS_VOICE_OPTIONS.map((option) => {
                        const isSelected = speechPrefs.voiceName === option.voiceId
                        return (
                          <button
                            key={option.voiceId}
                            type="button"
                            data-space-ignore="true"
                            aria-pressed={isSelected}
                            onClick={() => handleSelectVoice(option.voiceId)}
                            className={cn(
                              'flex items-center justify-between rounded-xl border px-3 py-2 text-xs font-bold transition',
                              isSelected
                                ? 'border-[#F26F21] bg-[#FFF7ED] text-[#C2410C]'
                                : 'border-[#E2E8F0] bg-[#F8FAFC] text-[#475569] hover:border-[#CBD5E1] hover:text-[#1E293B]',
                            )}
                          >
                            <span>{option.label}</span>
                            {isSelected && (
                              <span className="size-2 rounded-full bg-[#F26F21]" />
                            )}
                          </button>
                        )
                      })}
                    </div>
                  </div>

                  <div className="border-t border-[#E2E8F0] pt-3">
                    <span className="block px-1 text-xs font-bold text-[#1E293B]">
                      Tốc độ đọc câu hỏi
                    </span>
                    <div
                      role="group"
                      aria-label="Chọn tốc độ đọc câu hỏi"
                      className="mt-2 grid grid-cols-3 gap-1.5"
                    >
                      {TTS_RATE_OPTIONS.map((rateOption) => {
                        const isSelected =
                          Math.abs(speechPrefs.speakingRate - rateOption.value) < 0.01
                        return (
                          <button
                            key={rateOption.value}
                            type="button"
                            data-space-ignore="true"
                            aria-pressed={isSelected}
                            onClick={() => handleSelectRate(rateOption.value)}
                            className={cn(
                              'flex items-center justify-center rounded-xl border py-2 font-mono text-xs font-bold transition',
                              isSelected
                                ? 'border-[#F26F21] bg-[#FFF7ED] text-[#C2410C]'
                                : 'border-[#E2E8F0] bg-[#F8FAFC] text-[#475569] hover:border-[#CBD5E1] hover:text-[#1E293B]',
                            )}
                          >
                            {rateOption.value}x
                          </button>
                        )
                      })}
                    </div>
                  </div>
                </div>
              )}
            </div>

            <button
              type="button"
              data-space-ignore="true"
              aria-label="Rời phòng thi"
              onClick={leaveRoom}
              className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-[#E2E8F0] bg-white px-3 text-xs font-bold text-[#475569] transition hover:border-[#FED7AA] hover:bg-[#FFF7ED] hover:text-[#C2410C]"
            >
              <X className="size-4" />
              <span className="hidden sm:inline">Rời phòng</span>
            </button>
          </div>
        </div>
      </header>

      <main className="mx-auto flex w-full max-w-7xl flex-1 flex-col px-4 py-3 sm:px-6 lg:h-[calc(100dvh-4.5rem)] lg:overflow-hidden">
        {question ? (
          <div className="grid min-h-0 flex-1 grid-cols-1 gap-4 lg:grid-cols-12 lg:gap-5">
            {/* Cột trái: Thẻ Câu hỏi & Hướng dẫn */}
            <section className="flex h-full flex-col overflow-hidden rounded-2xl border border-[#E2E8F0] bg-white shadow-xs lg:col-span-6 xl:col-span-7">
              <div className="shrink-0 border-b border-[#E2E8F0] bg-white px-5 py-3.5">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="rounded-lg border border-[#FED7AA] bg-[#FFF7ED] px-2.5 py-1 text-xs font-bold text-[#C2410C]">
                      Câu {question.orderIndex}
                    </span>
                    <span className="rounded-full border border-[#E2E8F0] bg-[#F8FAFC] px-2.5 py-1 text-xs font-bold text-[#1E293B]">
                      {question.bloomLevel}
                    </span>
                    <span
                      className={cn(
                        'rounded-full border px-2.5 py-1 text-xs font-bold',
                        difficultyBadgeClassName(question.difficulty),
                      )}
                    >
                      {difficultyLabel(question.difficulty)}
                    </span>
                  </div>

                  <button
                    type="button"
                    data-space-ignore="true"
                    onClick={() => speakQuestion(question.title, question.content)}
                    disabled={isRecording}
                    className="inline-flex min-h-8 items-center justify-center gap-1.5 rounded-xl border border-[#FED7AA] bg-[#FFF7ED] px-3 text-xs font-bold text-[#C2410C] transition hover:bg-[#FFEDD5] disabled:opacity-50"
                  >
                    <Volume2 className="size-3.5 text-[#F26F21]" /> Nghe đọc câu hỏi
                  </button>
                </div>
              </div>

              <div className="flex-1 space-y-4 overflow-y-auto p-5 sm:p-6">
                <h1 className="text-lg leading-relaxed font-bold text-[#1E293B] sm:text-xl md:text-2xl">
                  Câu {question.orderIndex}. {question.content}
                </h1>

                <div className="rounded-xl border border-[#FED7AA]/80 bg-[#FFF7ED]/35 p-3.5 text-xs leading-relaxed text-[#475569]">
                  <p className="font-bold text-[#C2410C]">Hướng dẫn trả lời:</p>
                  <p className="mt-1">
                    Sử dụng microphone để trình bày câu trả lời của bạn. Bạn có thể kiểm tra và hiệu
                    đính lại văn bản câu trả lời sau khi nói.
                  </p>
                </div>
              </div>

              <div className="flex shrink-0 items-center justify-between border-t border-[#E2E8F0] bg-[#F8FAFC] px-5 py-2.5 text-xs text-[#64748B]">
                <span>Chế độ trả lời bằng giọng nói</span>
                <span>Thang điểm: 10.0</span>
              </div>
            </section>

            {/* Cột phải: Khu vực Thu âm & Hiệu đính Transcript */}
            <section className="flex h-full flex-col overflow-hidden rounded-2xl border border-[#E2E8F0] bg-white shadow-xs lg:col-span-6 xl:col-span-5">
              <div className="flex shrink-0 items-center justify-between border-b border-[#E2E8F0] bg-white px-4 py-3 sm:px-5">
                <span className="text-xs font-bold text-[#1E293B]">
                  Khu vực ghi âm câu trả lời
                </span>
                <span className="inline-flex items-center gap-1 rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-0.5 text-xs font-bold text-[#16A34A]">
                  <ShieldCheck className="size-3.5" /> Không lưu audio R2
                </span>
              </div>

              <div className="flex flex-1 flex-col justify-between space-y-3 overflow-y-auto p-4 sm:p-5">
                <div>
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
                      'flex min-h-14 w-full touch-none items-center justify-center gap-3 rounded-2xl border text-sm font-bold transition select-none disabled:opacity-60',
                      isRecording
                        ? 'border-rose-300 bg-rose-600 text-white'
                        : 'border-[#FED7AA] bg-[#FFF7ED] text-[#C2410C] hover:border-[#F26F21] hover:bg-[#FFEDD5]',
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
                  <p className="mt-2 text-center text-xs text-[#475569]">
                    Giữ nút này hoặc giữ phím{' '}
                    <kbd className="rounded border border-[#E2E8F0] bg-[#F8FAFC] px-1.5 py-0.5 font-semibold text-[#1E293B]">
                      Space
                    </kbd>{' '}
                    khi nói. Thả ra để xem transcript.
                  </p>
                  {isRecording && liveTranscript.trim() && (
                    <div
                      className="mt-2.5 rounded-xl border border-rose-200 bg-rose-50/70 px-3 py-2 text-left text-xs leading-relaxed text-[#1E293B]"
                      aria-live="polite"
                    >
                      <span className="font-bold text-rose-700">Đang nhận diện trực tiếp: </span>
                      <span>{liveTranscript}</span>
                    </div>
                  )}
                </div>

                {showTranscript && (
                  <div className="flex min-h-24 flex-1 flex-col rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3">
                    <div className="flex items-center justify-between gap-2">
                      <label htmlFor="mock-transcript" className="text-xs font-bold text-[#1E293B]">
                        Transcript nhận diện &amp; hiệu đính
                      </label>
                      {transcriptDirty && (
                        <span className="text-xs font-bold text-amber-700">Chưa lưu sửa đổi</span>
                      )}
                    </div>
                    {isSendingAnswer && !savedAnswer ? (
                      <div className="mt-2 flex flex-1 items-center justify-center gap-2 rounded-lg border border-[#E2E8F0] bg-white p-3 text-xs text-[#475569]">
                        <LoaderCircle className="size-4 animate-spin text-[#F26F21]" />
                        Đang nhận diện giọng nói tiếng Việt…
                      </div>
                    ) : (
                      <textarea
                        id="mock-transcript"
                        value={draft}
                        onChange={(event) => {
                          const nextValue = event.target.value
                          setDrafts((current) => {
                            const next = { ...current, [question.id]: nextValue }
                            runtimeRef.current.drafts = next
                            return next
                          })
                        }}
                        className="mt-2 w-full flex-1 resize-none rounded-lg border border-[#E2E8F0] bg-white p-2.5 text-xs leading-relaxed text-[#1E293B] outline-none focus:border-[#F26F21] focus:ring-2 focus:ring-[#F26F21]/20"
                      />
                    )}
                    {transcriptDirty && (
                      <button
                        type="button"
                        data-space-ignore="true"
                        onClick={() => void handleUpdateTranscript()}
                        disabled={isSendingAnswer}
                        className="mt-2 inline-flex min-h-8 self-start items-center rounded-lg border border-[#FED7AA] bg-[#FFF7ED] px-3 text-xs font-bold text-[#C2410C] transition hover:bg-[#F26F21] hover:text-white disabled:opacity-50"
                      >
                        Lưu chỉnh sửa
                      </button>
                    )}
                  </div>
                )}

                {(answerError || recorderError) && (
                  <p className="text-xs font-medium leading-relaxed text-rose-700">
                    {answerError || recorderError}
                  </p>
                )}
                {submitError && (
                  <p className="text-xs font-medium leading-relaxed text-rose-700">
                    {submitError}
                  </p>
                )}
              </div>

              <div className="flex shrink-0 items-center justify-between gap-2.5 border-t border-[#E2E8F0] bg-[#F8FAFC] px-4 py-3 sm:px-5">
                <button
                  type="button"
                  data-space-ignore="true"
                  onClick={handleSubmit}
                  disabled={isRecording || isSendingAnswer}
                  className="inline-flex min-h-10 items-center justify-center rounded-xl border border-[#FED7AA] bg-[#FFF7ED] px-4 text-xs font-bold text-[#C2410C] transition hover:bg-[#FFEDD5] disabled:opacity-40"
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
                      className="inline-flex min-h-10 items-center gap-1.5 rounded-xl border border-[#FED7AA] bg-[#FFF7ED] px-3.5 text-xs font-semibold text-[#C2410C] transition hover:bg-[#FFEDD5] disabled:opacity-40"
                    >
                      <Flag className="size-3.5 text-[#F26F21]" />
                      Bỏ qua câu này
                    </button>
                  )}
                  {savedAnswer && questionIndex < session.questions.length - 1 && (
                    <button
                      type="button"
                      data-space-ignore="true"
                      onClick={() => void moveToQuestion(questionIndex + 1)}
                      disabled={isRecording || isSendingAnswer}
                      className="inline-flex min-h-10 items-center gap-1.5 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white shadow-xs transition hover:bg-[#D95B16] disabled:opacity-40"
                    >
                      Câu tiếp <ArrowRight className="size-3.5" />
                    </button>
                  )}
                </div>
              </div>
            </section>
          </div>
        ) : (
          <section className="w-full rounded-2xl border border-[#E2E8F0] bg-white px-6 py-10 text-center shadow-xs">
            <p className="text-sm text-[#475569]">
              Ca thi không có câu hỏi. Bạn có thể nộp để đóng phiên.
            </p>
            <button
              type="button"
              data-space-ignore="true"
              onClick={handleSubmit}
              className="mt-5 inline-flex min-h-11 items-center rounded-xl bg-[#F26F21] px-5 text-sm font-bold text-white shadow-xs transition hover:bg-[#D95B16]"
            >
              Nộp bài
            </button>
          </section>
        )}
      </main>
    </div>
  )
}
