import { useCallback, useEffect, useRef, useState } from 'react'
import { useBlocker, useNavigate } from 'react-router-dom'
import {
  ArrowLeft,
  ArrowRight,
  Clock3,
  LoaderCircle,
  MessageSquarePlus,
  Mic,
  Settings,
  Volume2,
  VolumeX,
} from 'lucide-react'
import { toast } from '@/components/common/toastStore'
import { useSpeechRecognition } from '@/hooks/useSpeechRecognition'
import { scheduleUi } from '@/lib/scheduleUi'
import { cn } from '@/lib/utils'
import { inClassShiftService } from '@/services/inClassShift.service'
import {
  getSpeechRecognitionLangForCourse,
  getTtsVoiceForCourse,
  playNaturalSpeech,
  stopSpeechPlayback,
} from '@/services/speech.service'
import type { InClassShift, StudentExamQuestion } from '@/types/inClassShift.types'
import {
  clearInClassExamDrafts,
  clearInClassExamSession,
  loadInClassExamDrafts,
  loadInClassExamSession,
  markInClassShiftCompleted,
  saveInClassExamDrafts,
  saveInClassExamSession,
} from '@/utils/inClassExamSessionStorage'

interface FollowUpTurnState {
  followUpIndex: number
  parentSubmissionId: string | null
  followUpPrompt: string | null
}

interface PendingAnswer {
  turnKey: string
  questionId: string
  transcript: string
  isFollowUp: boolean
  parentSubmissionId: string | null
  followUpIndex: number
}

function getTurnKey(questionId: string, followUpIndex: number) {
  return followUpIndex > 0 ? `${questionId}:fu:${followUpIndex}` : questionId
}

function formatClock(totalSeconds: number) {
  const safe = Math.max(0, totalSeconds)
  const minutes = Math.floor(safe / 60)
  const seconds = safe % 60
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
}

function fitTranscriptTextarea(el: HTMLTextAreaElement | null) {
  if (!el) return
  el.style.height = 'auto'
  const styles = window.getComputedStyle(el)
  const lineHeight = Number.parseFloat(styles.lineHeight) || 22
  const paddingY =
    (Number.parseFloat(styles.paddingTop) || 0) + (Number.parseFloat(styles.paddingBottom) || 0)
  const maxHeight = lineHeight * 3 + paddingY
  const contentHeight = el.scrollHeight
  el.style.height = `${Math.min(Math.max(contentHeight, lineHeight + paddingY), maxHeight)}px`
  el.style.overflowY = contentHeight > maxHeight + 1 ? 'auto' : 'hidden'
}

function getErrorMessage(error: unknown) {
  if (typeof error === 'object' && error !== null && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail
    if (typeof detail === 'string' && detail.trim()) return detail
  }
  if (error instanceof Error && error.message) return error.message
  return 'Không thể xử lý ca thi trên lớp.'
}

export function InClassExamSessionPage() {
  const navigate = useNavigate()
  const boot = loadInClassExamSession()
  const draftBoot = boot ? loadInClassExamDrafts(boot.shift.id) : null
  const [shift] = useState<InClassShift | null>(boot?.shift ?? null)
  const [questions] = useState<StudentExamQuestion[]>(boot?.questions ?? [])
  const [startedAt] = useState(() => boot?.startedAt ?? Date.now())
  const [examEndsAt] = useState(() => boot?.examEndsAt ?? Date.now())
  const [questionIndex, setQuestionIndex] = useState(() => {
    if (!boot || !draftBoot) return 0
    return Math.max(0, Math.min(boot.questions.length - 1, draftBoot.questionIndex))
  })
  const [drafts, setDrafts] = useState<Record<string, string>>(() => draftBoot?.drafts ?? {})
  const [submitted, setSubmitted] = useState<Record<string, boolean>>({})
  const [followUpTurns, setFollowUpTurns] = useState<Record<string, FollowUpTurnState>>({})
  const [busy, setBusy] = useState(false)
  const [isReadingQuestion, setIsReadingQuestion] = useState(false)
  const [autoRead, setAutoRead] = useState(false)
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [reRecording, setReRecording] = useState(false)
  const [recordingSeconds, setRecordingSeconds] = useState(0)
  const [secondsLeft, setSecondsLeft] = useState(() =>
    Math.max(0, Math.floor((examEndsAt - Date.now()) / 1000)),
  )
  const [answerError, setAnswerError] = useState('')
  const speech = useSpeechRecognition()
  const transcriptAreaRef = useRef<HTMLTextAreaElement | null>(null)
  const settingsRef = useRef<HTMLDivElement | null>(null)
  const endedRef = useRef(false)
  const speechRef = useRef(speech)
  const draftsRef = useRef(drafts)
  const submittedRef = useRef(submitted)
  const questionsRef = useRef(questions)
  const questionIndexRef = useRef(questionIndex)
  const followUpTurnsRef = useRef(followUpTurns)
  const readingGenerationRef = useRef(0)

  const question = questions[questionIndex]
  const currentTurn: FollowUpTurnState = question
    ? followUpTurns[question.questionId] ?? {
        followUpIndex: 0,
        parentSubmissionId: null,
        followUpPrompt: null,
      }
    : { followUpIndex: 0, parentSubmissionId: null, followUpPrompt: null }
  const turnKey = question ? getTurnKey(question.questionId, currentTurn.followUpIndex) : ''
  const draft = turnKey ? drafts[turnKey] ?? '' : ''
  const isLast = questionIndex >= questions.length - 1
  const maxFollowUps = shift?.hasFollowUp ? Math.max(0, shift.maxFollowUpQuestions || 0) : 0
  const hasPendingFollowUpTurns = maxFollowUps > 0 && currentTurn.followUpIndex < maxFollowUps
  const activePromptText =
    currentTurn.followUpIndex > 0 && currentTurn.followUpPrompt
      ? currentTurn.followUpPrompt
      : question?.content || question?.title || ''
  const isUrgent = secondsLeft <= 60
  const hasTranscript = Boolean(draft.trim()) || Boolean(turnKey && submitted[turnKey])
  const showTranscript = !speech.isListening && hasTranscript
  const showFirstMic =
    Boolean(question) &&
    !submitted[turnKey] &&
    !reRecording &&
    (speech.isListening || !hasTranscript)

  const leaveWithMessage = useCallback(
    (message: string, finishedShiftId?: string) => {
      if (finishedShiftId) {
        markInClassShiftCompleted(finishedShiftId)
        clearInClassExamDrafts(finishedShiftId)
      }
      clearInClassExamSession()
      navigate('/in-class-exam', {
        replace: true,
        state: { notice: message, finishedShiftId },
      })
    },
    [navigate],
  )

  useEffect(() => {
    if (!shift || questions.length === 0) {
      navigate('/in-class-exam', { replace: true })
    }
  }, [navigate, questions.length, shift])

  useEffect(() => {
    if (!shift) return
    const tick = () => setSecondsLeft(Math.max(0, Math.floor((examEndsAt - Date.now()) / 1000)))
    tick()
    const timer = window.setInterval(tick, 1000)
    return () => window.clearInterval(timer)
  }, [examEndsAt, shift])

  useEffect(() => {
    if (!showTranscript) return
    fitTranscriptTextarea(transcriptAreaRef.current)
  }, [showTranscript, draft, turnKey])

  const stopReadingQuestion = useCallback(() => {
    readingGenerationRef.current += 1
    stopSpeechPlayback()
    setIsReadingQuestion(false)
  }, [])

  const startReadingQuestion = useCallback(
    (text: string) => {
      if (!shift || !text.trim()) return
      const generation = ++readingGenerationRef.current
      setIsReadingQuestion(true)
      setAnswerError('')
      void playNaturalSpeech(text, getTtsVoiceForCourse(shift.courseCode, shift.className, text))
        .catch(() => {
          if (generation === readingGenerationRef.current) {
            setAnswerError('Không đọc được câu hỏi trên trình duyệt này.')
          }
        })
        .finally(() => {
          if (generation === readingGenerationRef.current) setIsReadingQuestion(false)
        })
    },
    [shift],
  )

  const collectPendingAnswers = useCallback(() => {
    const pendingDrafts = { ...draftsRef.current }
    const speechNow = speechRef.current
    const currentQuestion = questionsRef.current[questionIndexRef.current]
    if (currentQuestion && speechNow.transcript.trim()) {
      const turn = followUpTurnsRef.current[currentQuestion.questionId]
      const key = getTurnKey(currentQuestion.questionId, turn?.followUpIndex ?? 0)
      pendingDrafts[key] = speechNow.transcript
    }
    draftsRef.current = pendingDrafts

    const pendingMainAnswers: PendingAnswer[] = questionsRef.current
      .filter((item) => !submittedRef.current[item.questionId])
      .map((item) => ({
        turnKey: item.questionId,
        questionId: item.questionId,
        transcript: (pendingDrafts[item.questionId] ?? '').trim(),
        isFollowUp: false,
        parentSubmissionId: null,
        followUpIndex: 0,
      }))
      .filter((item) => item.transcript.length > 0)

    if (currentQuestion) {
      const currentTurn = followUpTurnsRef.current[currentQuestion.questionId]
      if (currentTurn?.followUpIndex > 0) {
        const currentTurnKey = getTurnKey(currentQuestion.questionId, currentTurn.followUpIndex)
        const currentTranscript = (pendingDrafts[currentTurnKey] ?? '').trim()
        if (currentTranscript && !submittedRef.current[currentTurnKey]) {
          pendingMainAnswers.push({
            turnKey: currentTurnKey,
            questionId: currentQuestion.questionId,
            transcript: currentTranscript,
            isFollowUp: true,
            parentSubmissionId: currentTurn.parentSubmissionId,
            followUpIndex: currentTurn.followUpIndex,
          })
        }
      }
    }

    return pendingMainAnswers
  }, [])

  const persistDrafts = useCallback(() => {
    if (!shift || endedRef.current) return
    const pendingDrafts = { ...draftsRef.current }
    const speechNow = speechRef.current
    const currentQuestion = questionsRef.current[questionIndexRef.current]
    if (currentQuestion && speechNow.transcript.trim()) {
      const turn = followUpTurnsRef.current[currentQuestion.questionId]
      const key = getTurnKey(currentQuestion.questionId, turn?.followUpIndex ?? 0)
      pendingDrafts[key] = speechNow.transcript
    }
    saveInClassExamDrafts(shift.id, {
      drafts: pendingDrafts,
      questionIndex: questionIndexRef.current,
    })
    if (boot) {
      saveInClassExamSession({
        ...boot,
        startedAt,
        examEndsAt,
      })
    }
  }, [boot, examEndsAt, shift, startedAt])

  const submitExamForGrading = useCallback(async () => {
    if (!shift) return false
    stopReadingQuestion()
    const speechNow = speechRef.current
    if (speechNow.isListening) speechNow.stopListening()
    const answers = collectPendingAnswers()
    for (const item of answers) {
      try {
        await inClassShiftService.submitStudentAnswer(
          shift.id,
          item.questionId,
          item.transcript,
          {
            isFollowUp: item.isFollowUp,
            parentSubmissionId: item.parentSubmissionId,
            followUpIndex: item.followUpIndex,
          },
        )
        submittedRef.current = {
          ...submittedRef.current,
          [item.turnKey]: true,
          ...(!item.isFollowUp ? { [item.questionId]: true } : {}),
        }
        setSubmitted(submittedRef.current)
      } catch (submitError) {
        saveInClassExamDrafts(shift.id, {
          drafts: draftsRef.current,
          questionIndex: questionIndexRef.current,
        })
        setAnswerError(getErrorMessage(submitError))
        return false
      }
    }
    try {
      await inClassShiftService.finishStudentExam(shift.id)
      markInClassShiftCompleted(shift.id)
      clearInClassExamDrafts(shift.id)
      clearInClassExamSession()
      return true
    } catch (finishError) {
      saveInClassExamDrafts(shift.id, {
        drafts: draftsRef.current,
        questionIndex: questionIndexRef.current,
      })
      setAnswerError(getErrorMessage(finishError))
      return false
    }
  }, [collectPendingAnswers, shift, stopReadingQuestion])

  const endExam = useCallback(
    (message: string) => {
      if (!shift || endedRef.current) return
      endedRef.current = true
      void (async () => {
        const finishedOk = await submitExamForGrading()
        if (finishedOk) {
          leaveWithMessage(message, shift.id)
          return
        }
        endedRef.current = false
        toast.error('Không nộp được bài lên máy chủ. Hãy thử nộp lại.')
        setBusy(false)
      })()
    },
    [leaveWithMessage, shift, submitExamForGrading],
  )
  const endExamRef = useRef(endExam)
  const submitExamForGradingRef = useRef(submitExamForGrading)
  const persistDraftsRef = useRef(persistDrafts)

  useEffect(() => {
    speechRef.current = speech
    draftsRef.current = drafts
    submittedRef.current = submitted
    questionsRef.current = questions
    questionIndexRef.current = questionIndex
    followUpTurnsRef.current = followUpTurns
    endExamRef.current = endExam
    submitExamForGradingRef.current = submitExamForGrading
    persistDraftsRef.current = persistDrafts
  })

  useEffect(() => {
    if (!shift) return
    const timer = window.setInterval(() => persistDraftsRef.current(), 5000)
    return () => window.clearInterval(timer)
  }, [shift])

  useEffect(() => {
    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      persistDraftsRef.current()
      event.preventDefault()
      event.returnValue = 'Thời gian vẫn chạy. Bạn có thể vào lại để làm tiếp trước khi hết giờ.'
    }
    const onPageHide = (event: PageTransitionEvent) => {
      if (event.persisted) return
      persistDraftsRef.current()
    }
    window.addEventListener('beforeunload', onBeforeUnload)
    window.addEventListener('pagehide', onPageHide)
    return () => {
      window.removeEventListener('beforeunload', onBeforeUnload)
      window.removeEventListener('pagehide', onPageHide)
    }
  }, [])

  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      Boolean(shift) &&
      !endedRef.current &&
      currentLocation.pathname !== nextLocation.pathname,
  )

  const leaveSubmitInFlightRef = useRef(false)

  useEffect(() => {
    if (blocker.state !== 'blocked') return
    if (!shift) {
      blocker.proceed?.()
      return
    }
    if (leaveSubmitInFlightRef.current) return
    if (endedRef.current) {
      blocker.proceed?.()
      return
    }

    const shouldSubmit = window.confirm(
      'Nộp bài và thoát phòng?\n\nChọn OK để nộp bài (AI chấm). Chọn Cancel để ở lại làm tiếp.',
    )
    if (!shouldSubmit) {
      persistDraftsRef.current()
      blocker.reset?.()
      return
    }

    leaveSubmitInFlightRef.current = true
    endedRef.current = true
    void (async () => {
      const finishedOk = await submitExamForGradingRef.current()
      leaveSubmitInFlightRef.current = false
      if (finishedOk) {
        toast.success('Đã nộp bài. AI đang chấm nền.')
        blocker.reset?.()
        navigate('/in-class-exam', {
          replace: true,
          state: {
            notice: 'Đã nộp bài. AI đang chấm nền; điểm hiện sau khi giảng viên chốt.',
            finishedShiftId: shift.id,
          },
        })
        return
      }
      endedRef.current = false
      toast.error('Không nộp được bài lên máy chủ. Hãy ở lại phòng và thử nộp lại.')
      blocker.reset?.()
    })()
  }, [blocker, navigate, shift])

  useEffect(() => {
    if (secondsLeft !== 0) return
    endExamRef.current('Hết thời gian làm bài. Bài chưa nộp được gửi để AI chấm.')
  }, [secondsLeft])

  useEffect(() => {
    if (!shift) return
    let cancelled = false
    const watch = async () => {
      try {
        const latest = await inClassShiftService.getStudentShift(shift.id)
        if (!cancelled && (latest.status === 'CLOSED' || latest.status === 'FINALIZED')) {
          endExamRef.current('Ca đã được đóng. Bài làm được nộp tự động để AI chấm.')
        }
      } catch {
        // Giữ phòng làm bài nếu lần kiểm tra trạng thái bị lỗi mạng.
      }
    }
    void watch()
    const timer = window.setInterval(() => void watch(), 3000)
    return () => {
      cancelled = true
      window.clearInterval(timer)
    }
  }, [shift])

  useEffect(() => {
    if (!speech.isListening) {
      return scheduleUi(() => setRecordingSeconds(0))
    }
    const timer = window.setInterval(() => setRecordingSeconds((current) => current + 1), 1000)
    return () => {
      window.clearInterval(timer)
      setRecordingSeconds(0)
    }
  }, [speech.isListening])

  useEffect(() => {
    if (!speech.isListening || !turnKey) return
    const latestTranscript = speech.transcript
    const timer = window.setTimeout(() => {
      setDrafts((current) => ({ ...current, [turnKey]: latestTranscript }))
    }, 0)
    return () => window.clearTimeout(timer)
  }, [speech.isListening, speech.transcript, turnKey])

  const wasListeningRef = useRef(false)
  useEffect(() => {
    const shouldExitReRecording = wasListeningRef.current && !speech.isListening
    wasListeningRef.current = speech.isListening
    if (!shouldExitReRecording) return
    const timer = window.setTimeout(() => {
      setReRecording(false)
    }, 0)
    return () => window.clearTimeout(timer)
  }, [speech.isListening])

  useEffect(() => {
    if (!question || !autoRead) return
    const text = activePromptText
    const timer = window.setTimeout(() => startReadingQuestion(text), 0)
    return () => {
      window.clearTimeout(timer)
      stopReadingQuestion()
    }
  }, [activePromptText, autoRead, question, startReadingQuestion, stopReadingQuestion, turnKey])

  useEffect(() => {
    if (!settingsOpen) return
    const onPointerDown = (event: PointerEvent) => {
      if (!settingsRef.current?.contains(event.target as Node)) setSettingsOpen(false)
    }
    window.addEventListener('pointerdown', onPointerDown)
    return () => window.removeEventListener('pointerdown', onPointerDown)
  }, [settingsOpen])

  useEffect(() => {
    if (!shift) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.code !== 'Space' || event.repeat) return
      const target = event.target
      if (target instanceof HTMLElement) {
        if (target.closest('[data-space-ignore="true"]')) return
        if (target.tagName === 'TEXTAREA' || target.tagName === 'INPUT') return
      }
      event.preventDefault()
      if (busy || !question || submitted[turnKey]) return
      if (speech.isListening) {
        speech.stopListening()
        return
      }
      if (!showFirstMic && hasTranscript && !reRecording) {
        setReRecording(true)
        return
      }
      speech.startListening(
        getSpeechRecognitionLangForCourse(shift.courseCode, shift.className, activePromptText),
      )
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [activePromptText, busy, hasTranscript, question, reRecording, shift, showFirstMic, speech, submitted, turnKey])

  const toggleReadQuestion = () => {
    if (!question) return
    if (isReadingQuestion) {
      stopReadingQuestion()
      return
    }
    startReadingQuestion(activePromptText)
  }

  const toggleRecording = () => {
    if (!shift || !question || submitted[turnKey] || busy) return
    setAnswerError('')
    if (speech.isListening) {
      speech.stopListening()
      return
    }
    speech.startListening(
      getSpeechRecognitionLangForCourse(shift.courseCode, shift.className, activePromptText),
    )
  }

  const beginReRecord = () => {
    if (!question || submitted[turnKey] || busy || secondsLeft <= 0) return
    if (speech.isListening) speech.stopListening()
    stopReadingQuestion()
    setAnswerError('')
    setReRecording(true)
  }

  const cancelReRecord = () => {
    if (speech.isListening) speech.stopListening()
    setReRecording(false)
  }

  const submitCurrent = async (): Promise<'DONE' | 'FOLLOW_UP_READY' | 'FAILED'> => {
    if (!shift || !question || submitted[turnKey]) return 'DONE'
    const transcript = (drafts[turnKey] ?? '').trim()
    if (!transcript) return 'FAILED'
    if (speech.isListening) speech.stopListening()
    setBusy(true)
    setAnswerError('')
    try {
      const response = await inClassShiftService.submitStudentAnswer(
        shift.id,
        question.questionId,
        transcript,
        {
          isFollowUp: currentTurn.followUpIndex > 0,
          parentSubmissionId: currentTurn.parentSubmissionId,
          followUpIndex: currentTurn.followUpIndex,
        },
      )
      setSubmitted((current) => ({
        ...current,
        [turnKey]: true,
        [question.questionId]: true,
      }))

      if (hasPendingFollowUpTurns && response.submissionId) {
        try {
          const nextFu = await inClassShiftService.requestNextFollowUp(shift.id, response.submissionId)
          if (nextFu.hasMoreFollowUp && nextFu.followUpPrompt) {
            setFollowUpTurns((current) => ({
              ...current,
              [question.questionId]: {
                followUpIndex: nextFu.followUpIndex,
                parentSubmissionId: nextFu.parentSubmissionId,
                followUpPrompt: nextFu.followUpPrompt,
              },
            }))
            setReRecording(false)
            return 'FOLLOW_UP_READY'
          }
        } catch {
          // Nếu không sinh được câu hỏi phụ tiếp theo, cho phép chuyển câu bình thường
        }
      }

      return 'DONE'
    } catch (submitError) {
      setAnswerError(getErrorMessage(submitError))
      return 'FAILED'
    } finally {
      setBusy(false)
    }
  }

  const goNext = async () => {
    if (speech.isListening || busy) return
    const hasText = Boolean((drafts[turnKey] ?? '').trim())
    if (hasText && !submitted[turnKey]) {
      const status = await submitCurrent()
      if (status === 'FAILED' || status === 'FOLLOW_UP_READY') return
    }
    setReRecording(false)
    setQuestionIndex((current) => Math.min(questions.length - 1, current + 1))
  }

  const skipFollowUpOrQuestion = () => {
    if (speech.isListening || busy || secondsLeft <= 0) return
    setReRecording(false)
    if (!isLast) {
      setQuestionIndex((current) => Math.min(questions.length - 1, current + 1))
      return
    }
    endExam('Đã nộp bài. Điểm và nhận xét AI được cập nhật theo thời gian thực.')
  }

  const finish = async () => {
    if (speech.isListening || busy || endedRef.current) return
    const hasText = Boolean((drafts[turnKey] ?? '').trim())
    if (hasText && question && !submitted[turnKey]) {
      const status = await submitCurrent()
      if (status === 'FAILED' || status === 'FOLLOW_UP_READY') return
    }
    endExam('Đã nộp bài. Điểm và nhận xét AI được cập nhật theo thời gian thực.')
  }

  if (!shift) {
    return (
      <div className="flex min-h-dvh items-center justify-center bg-[#F4FBFD] text-[#0F2854]">
        <LoaderCircle className="size-8 animate-spin text-[#1C4D8D]" />
      </div>
    )
  }

  return (
    <div className="fixed inset-0 z-[80] flex items-center justify-center bg-[#0F2854]/45 p-3 text-[#0F2854] sm:p-4">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="in-class-session-title"
        className="flex max-h-[calc(100dvh-1.5rem)] w-full max-w-5xl flex-col overflow-y-auto rounded-[22px] border border-[#82aedb]/55 bg-white shadow-[0_24px_60px_rgba(16,32,61,0.22)] sm:max-h-[calc(100dvh-2rem)]"
      >
        <header className="relative z-20 shrink-0 bg-[linear-gradient(118deg,#0F2854_0%,#1C4D8D_58%,#4988C4_100%)] text-white">
          <div className="flex items-center justify-between gap-2.5 px-4 py-3.5 sm:px-5">
            <div className="min-w-0">
              <p className="text-xs font-bold tracking-[0.08em] text-white/75 uppercase">
                {shift.courseCode || shift.className}
              </p>
              <h1 id="in-class-session-title" className="truncate text-base font-bold sm:text-lg">
                {shift.title}
              </h1>
            </div>
            <div className="flex shrink-0 items-center gap-2">
              <div
                role="timer"
                aria-label={`Thời gian làm bài còn lại ${formatClock(secondsLeft)}`}
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
                    className="absolute top-12 right-0 z-30 w-72 rounded-2xl border border-[#82aedb]/55 bg-white p-3 text-left text-[#0F2854] shadow-[0_18px_50px_rgba(15,40,84,0.18)]"
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
            </div>
          </div>
          <div className="flex items-center gap-2.5 border-t border-white/15 px-4 py-2.5 sm:px-5">
            <span className="text-xs font-bold text-white/90">
              Câu {questions.length === 0 ? 0 : questionIndex + 1}/{questions.length}
              {maxFollowUps > 0 && currentTurn.followUpIndex > 0
                ? ` · Phụ ${currentTurn.followUpIndex}/${maxFollowUps}`
                : ''}
            </span>
            <div className="flex h-2.5 min-w-0 flex-1 gap-1.5">
              {questions.map((item, index) => (
                <button
                  key={item.questionId}
                  type="button"
                  data-space-ignore="true"
                  aria-label={`Chuyển tới câu ${index + 1}`}
                  aria-current={index === questionIndex ? 'step' : undefined}
                  disabled={speech.isListening || busy}
                  onClick={() => {
                    setReRecording(false)
                    setQuestionIndex(index)
                  }}
                  className={cn(
                    'h-full min-w-0 flex-1 rounded-full transition enabled:hover:opacity-90 disabled:cursor-not-allowed',
                    submitted[item.questionId] || (drafts[item.questionId] ?? '').trim()
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

        {questions.length === 0 ? (
          <p className="px-5 py-8 text-sm text-[#5b6b84]">
            Ca chưa phân câu hỏi. Báo giảng viên kiểm tra ngân hàng câu đã duyệt.
          </p>
        ) : (
          question && (
            <section className="flex flex-col gap-3 px-4 py-4 sm:px-5">
              <div className="flex flex-wrap items-center justify-between gap-2.5">
                <div className="flex flex-wrap items-center gap-2">
                  <p className="text-sm font-semibold text-[#5b6b84]">{question.title}</p>
                  {maxFollowUps > 0 && (
                    <span className="inline-flex items-center gap-1 rounded-full border border-[#82aedb]/65 bg-[#F4FBFD] px-2.5 py-0.5 text-xs font-bold text-[#1C4D8D]">
                      <MessageSquarePlus className="size-3.5" />
                      {currentTurn.followUpIndex > 0
                        ? `Câu hỏi phụ ${currentTurn.followUpIndex}/${maxFollowUps}`
                        : `Có tới ${maxFollowUps} câu phụ`}
                    </span>
                  )}
                </div>
                <button
                  type="button"
                  data-space-ignore="true"
                  aria-pressed={isReadingQuestion}
                  onClick={toggleReadQuestion}
                  className={cn(
                    'inline-flex min-h-10 items-center gap-2 rounded-xl border px-3 text-sm font-bold transition',
                    isReadingQuestion
                      ? 'border-[#ff8fa0] bg-[#fff1f3] text-[#c43b52] hover:bg-[#ffe4e9]'
                      : 'border-[#4988C4] bg-white text-[#1C4D8D] hover:bg-[#F4FBFD]',
                  )}
                >
                  {isReadingQuestion ? <VolumeX className="size-4" /> : <Volume2 className="size-4" />}
                  {isReadingQuestion ? 'Tắt đọc' : 'Đọc câu hỏi'}
                </button>
              </div>

              <div className="overflow-hidden rounded-2xl border border-[#82aedb]/55 bg-white">
                <div className="p-3.5">
                  <h2 className="text-[18px] leading-snug font-semibold whitespace-pre-line text-[#0F2854]">
                    <span className="font-semibold">Câu {questionIndex + 1}. </span>
                    {question.content || question.title}
                  </h2>
                </div>
              </div>

              {currentTurn.followUpIndex > 0 && currentTurn.followUpPrompt && (
                <div className="rounded-2xl border border-[#1C4D8D]/40 bg-[#F4FBFD] p-3.5">
                  <p className="text-xs font-bold tracking-[0.06em] text-[#1C4D8D] uppercase">
                    Câu hỏi phụ #{currentTurn.followUpIndex}/{maxFollowUps} (Phát vấn theo ngữ cảnh)
                  </p>
                  <p className="mt-1.5 text-base leading-snug font-semibold text-[#0F2854]">
                    {currentTurn.followUpPrompt}
                  </p>
                </div>
              )}

              {showFirstMic && (
                <div className="flex flex-col items-center gap-2 py-1">
                  <button
                    type="button"
                    disabled={busy || (!speech.isListening && secondsLeft <= 0)}
                    aria-pressed={speech.isListening}
                    aria-label={speech.isListening ? 'Dừng ghi âm' : 'Bắt đầu ghi âm'}
                    onClick={toggleRecording}
                    className={cn(
                      'flex size-[7.5rem] flex-col items-center justify-center gap-1 rounded-full border text-sm font-bold shadow-[0_12px_28px_rgba(28,77,141,0.16)] transition select-none disabled:opacity-60',
                      speech.isListening
                        ? 'border-[#ff8fa0] bg-[#ff5d73] text-white shadow-[0_12px_28px_rgba(255,93,115,0.28)]'
                        : 'border-[#82aedb]/55 bg-[#F4FBFD] text-[#0F2854] hover:bg-[#BDE8F5]',
                    )}
                  >
                    <Mic className={cn('size-7', speech.isListening && 'animate-pulse motion-reduce:animate-none')} />
                    <span className="text-xs font-bold">
                      {speech.isListening ? formatClock(recordingSeconds) : 'Ghi âm'}
                    </span>
                  </button>
                  <p className="text-center text-sm font-bold text-[#0F2854]">
                    {speech.isListening
                      ? 'Nhấn để dừng ghi âm'
                      : currentTurn.followUpIndex > 0
                        ? `Nhấn để ghi âm câu trả lời phụ #${currentTurn.followUpIndex}`
                        : 'Nhấn để bắt đầu ghi âm'}
                  </p>
                  {!speech.isListening && (
                    <p className="text-center text-xs leading-relaxed text-[#5b6b84]">
                      Nhấn nút hoặc phím{' '}
                      <kbd className="rounded bg-[#F4FBFD] px-1.5 py-0.5 font-bold text-[#0F2854]">Space</kbd>{' '}
                      để bật/tắt. Dừng để xem transcript.
                    </p>
                  )}
                </div>
              )}

              {(showTranscript || reRecording) && (
                <div className="flex flex-col gap-2">
                  <label htmlFor="in-class-transcript" className="text-sm font-semibold text-[#0F2854]">
                    {currentTurn.followUpIndex > 0
                      ? `Transcript câu trả lời phụ #${currentTurn.followUpIndex}`
                      : 'Transcript để kiểm tra và chỉnh sửa'}
                  </label>
                  <p className="text-xs leading-relaxed text-[#5b6b84]">
                    Nội dung chỉnh sửa được lưu khi chuyển câu hoặc nộp bài.
                  </p>
                  {(showTranscript || (reRecording && hasTranscript)) && (
                    <textarea
                      id="in-class-transcript"
                      ref={transcriptAreaRef}
                      data-transcript-fit
                      data-space-ignore="true"
                      rows={1}
                      value={draft}
                      disabled={
                        speech.isListening ||
                        submitted[turnKey] ||
                        busy ||
                        secondsLeft <= 0 ||
                        reRecording
                      }
                      onChange={(event) => {
                        fitTranscriptTextarea(event.currentTarget)
                        setDrafts((current) => ({
                          ...current,
                          [turnKey]: event.target.value,
                        }))
                      }}
                      className="min-h-10 w-full resize-none rounded-2xl border border-[#4988C4] bg-white px-3.5 py-2 text-sm leading-relaxed text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30 disabled:opacity-60"
                    />
                  )}

                  {showTranscript &&
                    !reRecording &&
                    !submitted[turnKey] &&
                    secondsLeft > 0 && (
                      <button
                        type="button"
                        data-space-ignore="true"
                        onClick={beginReRecord}
                        disabled={busy}
                        className="inline-flex min-h-10 w-fit items-center gap-2 rounded-xl border border-[#4988C4] bg-white px-3.5 text-sm font-bold text-[#1C4D8D] transition hover:-translate-y-0.5 hover:border-[#1C4D8D] hover:bg-[#E7F3FB] hover:shadow-[0_8px_16px_rgba(15,40,84,0.14)] disabled:opacity-50 motion-reduce:transform-none"
                      >
                        <Mic className="size-4" />
                        Ghi âm lại câu trả lời
                      </button>
                    )}

                  {reRecording && (
                    <div className="flex flex-col items-center gap-2 pt-1">
                      <button
                        type="button"
                        disabled={busy || (!speech.isListening && secondsLeft <= 0)}
                        aria-pressed={speech.isListening}
                        aria-label={speech.isListening ? 'Dừng ghi âm' : 'Bắt đầu ghi âm'}
                        onClick={toggleRecording}
                        className={cn(
                          'flex size-[7.5rem] flex-col items-center justify-center gap-1 rounded-full border text-sm font-bold shadow-[0_12px_28px_rgba(28,77,141,0.16)] transition select-none disabled:opacity-60',
                          speech.isListening
                            ? 'border-[#ff8fa0] bg-[#ff5d73] text-white shadow-[0_12px_28px_rgba(255,93,115,0.28)]'
                            : 'border-[#82aedb]/55 bg-[#F4FBFD] text-[#0F2854] hover:bg-[#BDE8F5]',
                        )}
                      >
                        <Mic
                          className={cn(
                            'size-7',
                            speech.isListening && 'animate-pulse motion-reduce:animate-none',
                          )}
                        />
                        <span className="text-xs font-bold">
                          {speech.isListening ? formatClock(recordingSeconds) : 'Ghi âm'}
                        </span>
                      </button>
                      <p className="text-center text-sm font-bold text-[#0F2854]">
                        {speech.isListening ? 'Nhấn để dừng ghi âm' : 'Ghi âm lại câu trả lời'}
                      </p>
                      {!speech.isListening && (
                        <button
                          type="button"
                          data-space-ignore="true"
                          onClick={cancelReRecord}
                          className="text-sm font-bold text-[#1C4D8D] hover:underline"
                        >
                          Hủy ghi âm lại
                        </button>
                      )}
                    </div>
                  )}
                </div>
              )}

              {(answerError || speech.error) && (
                <p className="text-sm leading-relaxed text-[#c2415b]">{answerError || speech.error}</p>
              )}

              <div className="flex flex-wrap items-center justify-between gap-2 pt-0.5">
                <button
                  type="button"
                  data-space-ignore="true"
                  onClick={() => {
                    setReRecording(false)
                    setQuestionIndex((current) => Math.max(0, current - 1))
                  }}
                  disabled={questionIndex <= 0 || speech.isListening || busy || secondsLeft <= 0}
                  className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-[#4988C4] bg-white px-4 text-sm font-bold text-[#0F2854] shadow-[0_3px_8px_rgba(23,48,96,0.05)] hover:-translate-y-0.5 hover:border-[#4988C4] disabled:opacity-40 motion-reduce:transform-none"
                >
                  <ArrowLeft className="size-4" />
                  Câu trước
                </button>
                <div className="flex flex-wrap items-center justify-end gap-2">
                  {currentTurn.followUpIndex > 0 && !draft.trim() && !submitted[turnKey] && (
                    <button
                      type="button"
                      data-space-ignore="true"
                      onClick={skipFollowUpOrQuestion}
                      disabled={speech.isListening || busy || secondsLeft <= 0}
                      className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-[#4988C4] bg-white px-4 text-sm font-bold text-[#0F2854] shadow-[0_3px_8px_rgba(23,48,96,0.05)] hover:-translate-y-0.5 hover:border-[#4988C4] disabled:opacity-40 motion-reduce:transform-none"
                    >
                      {isLast ? 'Bỏ qua câu phụ & Nộp bài' : 'Bỏ qua câu phụ'}
                      <ArrowRight className="size-4" />
                    </button>
                  )}
                  {!isLast && currentTurn.followUpIndex === 0 && !draft.trim() && !submitted[turnKey] && (
                    <button
                      type="button"
                      data-space-ignore="true"
                      onClick={() => {
                        setReRecording(false)
                        setQuestionIndex((current) => current + 1)
                      }}
                      disabled={speech.isListening || busy || secondsLeft <= 0}
                      className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-[#4988C4] bg-white px-4 text-sm font-bold text-[#0F2854] shadow-[0_3px_8px_rgba(23,48,96,0.05)] hover:-translate-y-0.5 hover:border-[#4988C4] disabled:opacity-40 motion-reduce:transform-none"
                    >
                      Bỏ qua
                      <ArrowRight className="size-4" />
                    </button>
                  )}
                  {!isLast && (Boolean(draft.trim()) || submitted[turnKey]) && (
                    <button
                      type="button"
                      data-space-ignore="true"
                      onClick={() => void goNext()}
                      disabled={speech.isListening || busy || secondsLeft <= 0}
                      className="inline-flex min-h-10 items-center gap-2 rounded-xl bg-[#f5c518] px-4 text-sm font-bold text-[#1b1600] disabled:opacity-40"
                    >
                      {busy
                        ? 'Đang lưu...'
                        : hasPendingFollowUpTurns && !submitted[turnKey]
                          ? `Gửi & Câu phụ (${currentTurn.followUpIndex + 1}/${maxFollowUps})`
                          : 'Câu tiếp'}
                      <ArrowRight className="size-4" />
                    </button>
                  )}
                  {isLast && (
                    <button
                      type="button"
                      data-space-ignore="true"
                      onClick={() => void finish()}
                      disabled={speech.isListening || busy || secondsLeft <= 0}
                      className="inline-flex min-h-10 items-center justify-center rounded-xl bg-[#1C4D8D] px-5 text-sm font-bold text-white shadow-[0_8px_16px_rgba(36,94,234,0.22)] transition duration-200 hover:-translate-y-0.5 hover:bg-[#0F2854] hover:shadow-[0_12px_22px_rgba(15,40,84,0.28)] disabled:opacity-40 motion-reduce:transform-none"
                    >
                      {busy
                        ? 'Đang nộp...'
                        : hasPendingFollowUpTurns && Boolean(draft.trim()) && !submitted[turnKey]
                          ? `Gửi & Câu phụ (${currentTurn.followUpIndex + 1}/${maxFollowUps})`
                          : 'Nộp bài'}
                    </button>
                  )}
                </div>
              </div>
            </section>
          )
        )}
      </div>
    </div>
  )
}
