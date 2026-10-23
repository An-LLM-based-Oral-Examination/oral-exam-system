import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import {
  ArrowLeft,
  ArrowRight,
  Clock3,
  LoaderCircle,
  Mic,
  Settings,
  Volume2,
  VolumeX,
  X,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { MockExamScorecard } from '@/components/mock-exam/MockExamScorecard'
import { AutoFitQuestionText } from '@/components/question/AutoFitQuestionText'
import { QuestionImage } from '@/components/question/QuestionImage'
import { useAudioRecorder } from '@/hooks/useAudioRecorder'
import {
  getMockExamErrorDetail,
  getMockExamErrorStatus,
  mockExamService,
} from '@/services/mockExam.service'
import {
  getTtsVoiceForCourse,
  isSpeechPlaybackCancellation,
  playNaturalSpeech,
  stopSpeechPlayback,
  transcribeSpeech,
} from '@/services/speech.service'
import type {
  MockExamActiveFollowUp,
  MockExamScorecard as MockExamScorecardData,
  StoredMockExamAnswer,
  StoredMockExamSession,
} from '@/types/mockExam.types'
import {
  clearMockExamSession,
  readMockExamSessionById,
  removeMockExamAnswer,
  saveMockExamAnswer,
  saveMockExamSession,
  secondsUntilExpiry,
} from '@/utils/mockExamSessionStorage'

type RoomPhase = 'exam' | 'submitting-exam' | 'scorecard'

/** Đang ghi âm lại đáp án nào khi xem lại (null = lần ghi đầu / chưa mở mic). */
type ReRecordTarget =
  | { kind: 'main'; questionId: string }
  | { kind: 'followUp'; answerId: string; questionId: string }

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

/** Cao theo nội dung, tối đa 3 dòng; vượt thì scroll. */
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

/** Chỉ lấy thuật ngữ ngắn — không gửi nguyên đề (tránh STT bịa câu trả lời khi im lặng). */
function buildVocabularyHint(question: { title: string }) {
  const title = question.title.trim()
  return title || undefined
}

function isLikelySilentOrInvalidTranscript(transcript: string, recordingSeconds: number) {
  const text = transcript.trim()
  if (!text) return true
  // Ghi quá ngắn mà transcript dài → thường là ảo giác khi không nói.
  if (recordingSeconds > 0 && recordingSeconds < 2 && text.split(/\s+/).length > 12) return true
  return false
}

function normalizeForCompare(value: string) {
  return value
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^\p{L}\p{N}\s]/gu, ' ')
    .replace(/\s+/g, ' ')
    .trim()
}

/** STT đôi khi “đọc” lại đề khi im lặng — không phải lời học sinh. */
function looksLikeQuestionEcho(
  transcript: string,
  question: { title: string; content: string },
) {
  const t = normalizeForCompare(transcript)
  if (t.length < 24) return false
  const title = normalizeForCompare(question.title)
  const content = normalizeForCompare(question.content)
  if (title.length >= 16 && t.includes(title)) return true
  if (content.length >= 40) {
    const snippet = content.slice(0, Math.min(80, content.length))
    if (snippet.length >= 40 && t.includes(snippet)) return true
  }
  return false
}

function initialQuestionIndex(session: StoredMockExamSession) {
  const answeredIds = new Set(session.answers.map((answer) => answer.questionId))
  const firstUnanswered = session.questions.findIndex((question) => !answeredIds.has(question.id))
  if (firstUnanswered >= 0) return firstUnanswered
  return Math.max(0, session.questions.length - 1)
}

function initialFurthestIndex(session: StoredMockExamSession) {
  const start = initialQuestionIndex(session)
  let maxAnswered = -1
  for (let index = 0; index < session.questions.length; index += 1) {
    const question = session.questions[index]
    if (session.answers.some((answer) => answer.questionId === question.id)) {
      maxAnswered = index
    }
  }
  const stored =
    typeof session.furthestQuestionIndex === 'number' &&
    Number.isFinite(session.furthestQuestionIndex)
      ? Math.max(0, Math.min(session.questions.length - 1, Math.floor(session.furthestQuestionIndex)))
      : -1
  return Math.max(0, start, maxAnswered, stored)
}

export function MockExamSessionPage() {
  const [searchParams] = useSearchParams()
  const sessionId = searchParams.get('session')
  const [session, setSession] = useState<StoredMockExamSession | null>(() =>
    sessionId ? readMockExamSessionById(sessionId) : null,
  )
  const [isRestoring, setIsRestoring] = useState(() => Boolean(sessionId && !session))
  const [restoreError, setRestoreError] = useState('')

  useEffect(() => {
    if (!sessionId || session) return

    let cancelled = false
    // isRestoring đã true từ initial state — chỉ gọi API, cập nhật state trong callback.
    void mockExamService
      .resumeSession(sessionId)
      .then((resumed) => {
        if (cancelled) return
        const stored: StoredMockExamSession = {
          ...resumed,
          answers: resumed.answers,
          activeFollowUp: resumed.activeFollowUp ?? null,
        }
        saveMockExamSession(stored)
        setSession(stored)
      })
      .catch((error: unknown) => {
        if (cancelled) return
        setRestoreError(
          getMockExamErrorDetail(error, 'Không thể khôi phục ca thi. Ca có thể đã hết hạn hoặc đã nộp.'),
        )
      })
      .finally(() => {
        if (!cancelled) setIsRestoring(false)
      })

    return () => {
      cancelled = true
    }
  }, [session, sessionId])

  if (!sessionId) return <MissingSession message="Thiếu mã ca thi trên đường dẫn." />
  if (isRestoring) {
    return (
      <div className="flex min-h-dvh items-center justify-center bg-white px-4 text-[#0F2854]">
        <div className="flex items-center gap-2 text-sm font-semibold text-[#5b6b84]">
          <LoaderCircle className="size-4 animate-spin" />
          Đang khôi phục ca thi…
        </div>
      </div>
    )
  }
  if (!session) {
    return (
      <MissingSession
        message={
          restoreError ||
          'Không tìm thấy ca thi đang mở. Hãy quay lại phòng chờ để tiếp tục hoặc tạo ca mới.'
        }
      />
    )
  }
  return <MockExamRoom session={session} />
}

function MissingSession({ message }: { message?: string }) {
  return (
    <div className="flex min-h-dvh items-center justify-center bg-white px-4 text-[#0F2854]">
      <section className="max-w-md text-center">
        <h1 className="text-xl font-bold">Không tìm thấy ca thi đang mở</h1>
        <p className="mt-2 text-sm leading-relaxed text-[#5f6f87]">
          {message ||
            'Ca thi chỉ được khôi phục trên trình duyệt đã bắt đầu bài. Hãy quay lại phòng chờ để tạo ca mới.'}
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
  const [questionIndex, setQuestionIndex] = useState(() => initialQuestionIndex(session))
  const [furthestIndex, setFurthestIndex] = useState(() => initialFurthestIndex(session))
  const [secondsLeft, setSecondsLeft] = useState(() => secondsUntilExpiry(session.expiresAt))
  const [activeFollowUp, setActiveFollowUp] = useState<MockExamActiveFollowUp | null>(
    () => session.activeFollowUp ?? null,
  )
  /** Câu phụ tiếp theo (đã tạo phía BE) — chỉ mở sau khi SV chỉnh transcript câu phụ hiện tại. */
  const [queuedFollowUp, setQueuedFollowUp] = useState<MockExamActiveFollowUp | null>(null)
  /** Xem lại: mic chỉ mở dưới đúng câu (chính / phụ) được chọn ghi lại. */
  const [reRecordTarget, setReRecordTarget] = useState<ReRecordTarget | null>(null)
  const reRecordTargetRef = useRef<ReRecordTarget | null>(null)
  const [answers, setAnswers] = useState<Record<string, StoredMockExamAnswer>>(() =>
    Object.fromEntries(
      session.answers
        .filter((answer) => !answer.isFollowUp)
        .map((answer) => [answer.questionId, answer]),
    ),
  )
  const [drafts, setDrafts] = useState<Record<string, string>>(() =>
    Object.fromEntries(
      session.answers
        .filter((answer) => !answer.isFollowUp)
        .map((answer) => [answer.questionId, answer.transcript]),
    ),
  )
  const [followUpAnswers, setFollowUpAnswers] = useState<Record<string, StoredMockExamAnswer>>(() =>
    Object.fromEntries(
      session.answers
        .filter((answer) => answer.isFollowUp && answer.answerId)
        .map((answer) => [answer.answerId, answer]),
    ),
  )
  const [followUpDrafts, setFollowUpDrafts] = useState<Record<string, string>>(() =>
    Object.fromEntries(
      session.answers
        .filter((answer) => answer.isFollowUp && answer.answerId)
        .map((answer) => [answer.answerId, answer.transcript]),
    ),
  )
  const draftsRef = useRef(drafts)
  const answersRef = useRef<Record<string, StoredMockExamAnswer>>(
    Object.fromEntries(
      session.answers
        .filter((answer) => !answer.isFollowUp)
        .map((answer) => [answer.questionId, answer]),
    ),
  )
  const followUpDraftsRef = useRef(followUpDrafts)
  const followUpAnswersRef = useRef(followUpAnswers)
  const activeFollowUpRef = useRef(activeFollowUp)
  /** Cache đề câu phụ theo answerId — tránh mất promptText khi xem lại. */
  const [followUpPromptByAnswerId, setFollowUpPromptByAnswerId] = useState<Record<string, string>>(
    () =>
      Object.fromEntries(
        session.answers
          .filter((answer) => answer.isFollowUp && answer.answerId && answer.promptText?.trim())
          .map((answer) => [answer.answerId, answer.promptText!.trim()]),
      ),
  )
  const followUpPromptByAnswerIdRef = useRef(followUpPromptByAnswerId)
  const draftSaveTimerRef = useRef<number | null>(null)
  const transcriptAreaRef = useRef<HTMLTextAreaElement | null>(null)
  const transcriptFitRootRef = useRef<HTMLElement | null>(null)
  const [audioBlob, setAudioBlob] = useState<Blob | null>(null)
  const [recordedSeconds, setRecordedSeconds] = useState(0)
  const [recordingSeconds, setRecordingSeconds] = useState(0)
  const [isSendingAnswer, setIsSendingAnswer] = useState(false)
  const [answerError, setAnswerError] = useState('')
  const [submitError, setSubmitError] = useState('')
  const [scorecard, setScorecard] = useState<MockExamScorecardData | null>(null)
  const [autoRead, setAutoRead] = useState(false)
  const [isReadingQuestion, setIsReadingQuestion] = useState(false)
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [warning, setWarning] = useState<ExamWarning | null>(null)
  const warningResolveRef = useRef<((confirmed: boolean) => void) | null>(null)
  const warningOpenRef = useRef(false)
  const urgentWarnedRef = useRef(false)
  const speechRequestRef = useRef(0)
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
    questionId: session.questions[initialQuestionIndex(session)]?.id ?? session.questions[0]?.id ?? '',
    audioBlob: null,
    recordedSeconds: 0,
    answers: Object.fromEntries(
      session.answers
        .filter((answer) => !answer.isFollowUp)
        .map((answer) => [answer.questionId, answer]),
    ),
  })

  const rememberFollowUpPrompt = useCallback((answerId: string, prompt: string) => {
    const trimmed = prompt.trim()
    if (!answerId || !trimmed) return
    setFollowUpPromptByAnswerId((current) => {
      if (current[answerId] === trimmed) return current
      const next = { ...current, [answerId]: trimmed }
      followUpPromptByAnswerIdRef.current = next
      return next
    })
  }, [])

  const persistActiveFollowUp = useCallback(
    (next: MockExamActiveFollowUp | null) => {
      if (next?.answerId && next.prompt) {
        rememberFollowUpPrompt(next.answerId, next.prompt)
      }
      setActiveFollowUp(next)
      const stored = readMockExamSessionById(session.sessionId)
      if (stored) {
        saveMockExamSession({ ...stored, activeFollowUp: next })
      }
    },
    [rememberFollowUpPrompt, session.sessionId],
  )

  const question = session.questions[questionIndex] ?? null
  const savedAnswer = question ? answers[question.id] : undefined
  const savedFollowUpAnswer = activeFollowUp
    ? followUpAnswers[activeFollowUp.answerId]
    : undefined
  /** Câu phụ đã trả lời của câu chính đang xem (để hiện khi quay lại). */
  const reviewedFollowUps = question
    ? Object.values(followUpAnswers)
        .filter(
          (item) =>
            item.isFollowUp &&
            item.questionId === question.id &&
            (item.transcript.trim().length > 0 ||
              (reRecordTarget?.kind === 'followUp' &&
                reRecordTarget.answerId === item.answerId)) &&
            (!activeFollowUp || item.answerId !== activeFollowUp.answerId),
        )
        .map((item) => ({
          ...item,
          promptText:
            item.promptText?.trim() || followUpPromptByAnswerId[item.answerId] || null,
        }))
        .sort((left, right) => left.answeredAt.localeCompare(right.answeredAt))
    : []
  const isReviewingAnsweredMain =
    !activeFollowUp &&
    (Boolean(savedAnswer) ||
      reviewedFollowUps.length > 0 ||
      (reRecordTarget?.kind === 'main' && reRecordTarget.questionId === question?.id))
  const draft = activeFollowUp
    ? (followUpDrafts[activeFollowUp.answerId] ?? '')
    : question
      ? (drafts[question.id] ?? '')
      : ''
  const isUrgent = secondsLeft <= 60
  /** Lần ghi đầu (chưa có đáp án) — mic dùng chung phía dưới câu hỏi. */
  const isFirstRecordingPass = activeFollowUp
    ? !savedFollowUpAnswer && !isSendingAnswer
    : !savedAnswer && reviewedFollowUps.length === 0 && !isSendingAnswer
  const showFirstPassMic =
    isFirstRecordingPass ||
    (Boolean(activeFollowUp) && isSendingAnswer && !savedFollowUpAnswer) ||
    (!activeFollowUp && isSendingAnswer && !savedAnswer && !isReviewingAnsweredMain)
  const micAllowed =
    showFirstPassMic ||
    reRecordTarget !== null ||
    isRecording ||
    (isSendingAnswer && reRecordTarget !== null)
  const showMicSectionRef = useRef(micAllowed)
  const isMicOpenForMain = Boolean(
    question && reRecordTarget?.kind === 'main' && reRecordTarget.questionId === question.id,
  )
  const isMicOpenForFollowUp = (answerId: string) =>
    Boolean(reRecordTarget?.kind === 'followUp' && reRecordTarget.answerId === answerId)

  const reviewContextKey = `${question?.id ?? ''}:${activeFollowUp?.answerId ?? ''}`
  const [reviewContextSeen, setReviewContextSeen] = useState(reviewContextKey)
  if (reviewContextSeen !== reviewContextKey) {
    setReviewContextSeen(reviewContextKey)
    setReRecordTarget(null)
  }

  useEffect(() => {
    draftsRef.current = drafts
  }, [drafts])

  useEffect(() => {
    answersRef.current = answers
  }, [answers])

  useEffect(() => {
    followUpDraftsRef.current = followUpDrafts
  }, [followUpDrafts])

  useEffect(() => {
    followUpAnswersRef.current = followUpAnswers
  }, [followUpAnswers])

  useEffect(() => {
    activeFollowUpRef.current = activeFollowUp
  }, [activeFollowUp])

  useEffect(() => {
    reRecordTargetRef.current = reRecordTarget
  }, [reRecordTarget])

  useEffect(() => {
    showMicSectionRef.current = micAllowed
  }, [micAllowed])

  useEffect(() => {
    followUpPromptByAnswerIdRef.current = followUpPromptByAnswerId
  }, [followUpPromptByAnswerId])

  useLayoutEffect(() => {
    const root = transcriptFitRootRef.current
    if (!root) return
    root.querySelectorAll<HTMLTextAreaElement>('[data-transcript-fit]').forEach(fitTranscriptTextarea)
  }, [
    drafts,
    followUpDrafts,
    draft,
    isRecording,
    isSendingAnswer,
    isReviewingAnsweredMain,
    activeFollowUp?.answerId,
    question?.id,
    reviewedFollowUps.length,
    reRecordTarget,
  ])

  const replacePlayback = useCallback((blob: Blob | null) => {
    if (playbackUrlRef.current) URL.revokeObjectURL(playbackUrlRef.current)
    playbackUrlRef.current = blob ? URL.createObjectURL(blob) : null
    setAudioBlob(blob)
    runtimeRef.current.audioBlob = blob
  }, [])

  const stopReadingQuestion = useCallback(() => {
    speechRequestRef.current += 1
    stopSpeechPlayback()
    setIsReadingQuestion(false)
  }, [])

  const startReadingQuestion = useCallback(
    (content: string) => {
      const text = content.trim()
      if (!text) return

      const requestId = ++speechRequestRef.current
      setIsReadingQuestion(true)
      void playNaturalSpeech(
        text,
        getTtsVoiceForCourse(session?.courseCode, session?.courseName),
        0.96,
      )
        .catch((error: unknown) => {
          if (speechRequestRef.current !== requestId || isSpeechPlaybackCancellation(error)) return
          console.warn('Không thể phát câu hỏi bằng giọng AI.', error)
        })
        .finally(() => {
          if (speechRequestRef.current === requestId) setIsReadingQuestion(false)
        })
    },
    [session.courseCode, session.courseName],
  )

  const displayPrompt = activeFollowUp?.prompt ?? question?.content ?? ''

  const toggleReadQuestion = useCallback(() => {
    if (!displayPrompt.trim()) return
    if (isReadingQuestion) {
      stopReadingQuestion()
      return
    }
    startReadingQuestion(displayPrompt)
  }, [displayPrompt, isReadingQuestion, startReadingQuestion, stopReadingQuestion])

  const uploadAnswer = useCallback(
    async (
      questionId: string,
      file: Blob | null,
      timeTakenSeconds: number,
      transcript?: string,
      answerId?: string | null,
    ) => {
      // Luôn gắn AnswerId khi đang trả lời câu phụ — thiếu thì BE ghi đè transcript câu chính.
      const activeFu = activeFollowUpRef.current
      const reRecord = reRecordTargetRef.current
      const resolvedAnswerId =
        answerId?.trim() ||
        (reRecord?.kind === 'followUp' && reRecord.questionId === questionId
          ? reRecord.answerId
          : null) ||
        (activeFu && activeFu.questionId === questionId ? activeFu.answerId : null) ||
        null
      const submittingFollowUp = Boolean(resolvedAnswerId)

      const response = await mockExamService.submitAnswer(session.sessionId, {
        questionId,
        answerId: resolvedAnswerId,
        file,
        timeTakenSeconds,
        transcript,
      })

      // Nếu FE đang nộp câu phụ mà BE trả về không phải follow-up → coi như lỗi binding;
      // vẫn lưu vào follow-up local, không ghi đè câu chính trên UI.
      const treatAsFollowUp = response.isFollowUp || submittingFollowUp

      if (!treatAsFollowUp) {
        const stored: StoredMockExamAnswer = {
          questionId: response.questionId,
          transcript: response.transcript,
          timeTakenSeconds,
          answerId: response.answerId,
          answeredAt: response.answeredAt,
          isFollowUp: false,
        }
        setAnswers((current) => {
          const next = { ...current, [stored.questionId]: stored }
          runtimeRef.current.answers = next
          answersRef.current = next
          return next
        })
        setDrafts((current) => {
          const next = { ...current, [stored.questionId]: stored.transcript }
          draftsRef.current = next
          return next
        })
        saveMockExamAnswer(stored, session.sessionId)

        replacePlayback(null)
        // Xếp hàng câu phụ — chỉ mở sau khi SV chỉnh xong transcript câu chính.
        // Không xóa hàng đợi khi chỉ chỉnh sửa transcript (needsFollowUp = false).
        if (
          response.needsFollowUp &&
          response.followUpAnswerId &&
          response.followUpPrompt?.trim()
        ) {
          rememberFollowUpPrompt(response.followUpAnswerId, response.followUpPrompt)
          setQueuedFollowUp({
            answerId: response.followUpAnswerId,
            questionId: response.questionId,
            parentAnswerId: response.answerId,
            prompt: response.followUpPrompt.trim(),
            followUpIndex: response.followUpIndex > 0 ? response.followUpIndex : 1,
            maxFollowUps: response.maxFollowUps > 0 ? response.maxFollowUps : 1,
          })
        }
        setReRecordTarget(null)
        return response
      }

      const followUpAnswerId = resolvedAnswerId ?? response.answerId
      const promptForAnswer =
        followUpPromptByAnswerIdRef.current[followUpAnswerId] ??
        (activeFu?.answerId === followUpAnswerId ? activeFu.prompt : null) ??
        followUpAnswersRef.current[followUpAnswerId]?.promptText ??
        null
      if (promptForAnswer) {
        rememberFollowUpPrompt(followUpAnswerId, promptForAnswer)
      }
      const storedFollowUp: StoredMockExamAnswer = {
        questionId: response.questionId,
        transcript: response.transcript,
        timeTakenSeconds,
        answerId: followUpAnswerId,
        answeredAt: response.answeredAt,
        isFollowUp: true,
        parentAnswerId: response.parentAnswerId ?? activeFu?.parentAnswerId ?? null,
        promptText: promptForAnswer,
      }
      setFollowUpAnswers((current) => {
        const next = { ...current, [storedFollowUp.answerId]: storedFollowUp }
        followUpAnswersRef.current = next
        return next
      })
      setFollowUpDrafts((current) => {
        const next = { ...current, [storedFollowUp.answerId]: storedFollowUp.transcript }
        followUpDraftsRef.current = next
        return next
      })
      saveMockExamAnswer(storedFollowUp, session.sessionId)
      replacePlayback(null)

      if (
        response.needsFollowUp &&
        response.followUpAnswerId &&
        response.followUpPrompt?.trim()
      ) {
        // Giữ câu phụ vừa trả lời để SV chỉnh transcript; câu phụ tiếp xếp hàng.
        rememberFollowUpPrompt(response.followUpAnswerId, response.followUpPrompt)
        setQueuedFollowUp({
          answerId: response.followUpAnswerId,
          questionId: response.questionId,
          parentAnswerId: response.parentAnswerId ?? response.answerId,
          prompt: response.followUpPrompt.trim(),
          followUpIndex: response.followUpIndex > 0 ? response.followUpIndex : 1,
          maxFollowUps: response.maxFollowUps > 0 ? response.maxFollowUps : 1,
        })
      } else {
        setQueuedFollowUp(null)
      }

      setReRecordTarget(null)
      return response
    },
    [rememberFollowUpPrompt, replacePlayback, session.sessionId],
  )

  const persistQuestionTranscript = useCallback(
    async (questionId: string) => {
      const answer = answersRef.current[questionId]
      const nextTranscript = (draftsRef.current[questionId] ?? '').trim()
      if (!answer) return true
      if (!nextTranscript) return false
      if (nextTranscript === answer.transcript.trim()) return true
      await uploadAnswer(questionId, null, answer.timeTakenSeconds, nextTranscript)
      return true
    },
    [uploadAnswer],
  )

  const persistFollowUpTranscript = useCallback(
    async (answerId: string) => {
      const answer = followUpAnswersRef.current[answerId]
      const nextTranscript = (followUpDraftsRef.current[answerId] ?? '').trim()
      if (!answer) return true
      if (!nextTranscript) return false
      if (nextTranscript === answer.transcript.trim()) return true
      await uploadAnswer(
        answer.questionId,
        null,
        answer.timeTakenSeconds,
        nextTranscript,
        answerId,
      )
      return true
    },
    [uploadAnswer],
  )

  const flushDirtyTranscripts = useCallback(async () => {
    if (draftSaveTimerRef.current !== null) {
      window.clearTimeout(draftSaveTimerRef.current)
      draftSaveTimerRef.current = null
    }
    for (const item of session.questions) {
      const answer = answersRef.current[item.id]
      const nextTranscript = (draftsRef.current[item.id] ?? '').trim()
      if (!answer || !nextTranscript || nextTranscript === answer.transcript.trim()) continue
      await uploadAnswer(item.id, null, answer.timeTakenSeconds, nextTranscript)
    }
  }, [session.questions, uploadAnswer])

  const scheduleDraftPersist = useCallback(
    (questionId: string) => {
      if (draftSaveTimerRef.current !== null) {
        window.clearTimeout(draftSaveTimerRef.current)
      }
      draftSaveTimerRef.current = window.setTimeout(() => {
        draftSaveTimerRef.current = null
        void persistQuestionTranscript(questionId).catch((error: unknown) => {
          if (getMockExamErrorStatus(error) === 410) {
            void finalizeExamRef.current('timeout')
            return
          }
          setAnswerError(getMockExamErrorDetail(error, 'Không thể lưu transcript đã chỉnh sửa.'))
        })
      }, 450)
    },
    [persistQuestionTranscript],
  )

  const scheduleFollowUpDraftPersist = useCallback(
    (answerId: string) => {
      if (draftSaveTimerRef.current !== null) {
        window.clearTimeout(draftSaveTimerRef.current)
      }
      draftSaveTimerRef.current = window.setTimeout(() => {
        draftSaveTimerRef.current = null
        void persistFollowUpTranscript(answerId).catch((error: unknown) => {
          if (getMockExamErrorStatus(error) === 410) {
            void finalizeExamRef.current('timeout')
            return
          }
          setAnswerError(getMockExamErrorDetail(error, 'Không thể lưu transcript đã chỉnh sửa.'))
        })
      }, 450)
    },
    [persistFollowUpTranscript],
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
      const currentQuestion =
        session.questions.find((item) => item.id === questionId) ?? null
      const recordedSeconds = Math.max(1, recordingSecondsRef.current)
      sendingRef.current = true
      setIsSendingAnswer(true)
      setAnswerError('')
      try {
        // Chỉ STT qua /speech/stt — không gửi nguyên đề, không fallback sang BE
        // (tránh model bịa câu trả lời / nhầm với sample answer khi im lặng).
        const speech = await transcribeSpeech(blob, {
          vocabularyHint: currentQuestion
            ? buildVocabularyHint(currentQuestion)
            : undefined,
          fileName: `mock-answer-${questionId}.webm`,
        })

        const transcript = speech.transcript.trim()
        if (
          isLikelySilentOrInvalidTranscript(transcript, recordedSeconds) ||
          (typeof speech.confidenceScore === 'number' &&
            speech.confidenceScore > 0 &&
            speech.confidenceScore < 0.25) ||
          (currentQuestion != null && looksLikeQuestionEcho(transcript, currentQuestion))
        ) {
          replacePlayback(null)
          setAnswerError(
            'Không nhận diện được giọng nói rõ ràng. Hãy nói gần micro hơn và ghi âm lại.',
          )
          return
        }

        const reRecord = reRecordTargetRef.current
        const followUp = activeFollowUpRef.current
        const answerIdForUpload =
          reRecord?.kind === 'followUp' && reRecord.questionId === questionId
            ? reRecord.answerId
            : followUp && followUp.questionId === questionId
              ? followUp.answerId
              : null
        await uploadAnswer(questionId, null, recordedSeconds, transcript, answerIdForUpload)
      } catch (error) {
        if (getMockExamErrorStatus(error) === 410) {
          holdWantedRef.current = false
          forceSubmitRef.current = true
          return
        }
        replacePlayback(null)
        setAnswerError(
          getMockExamErrorDetail(
            error,
            'Không nhận diện được giọng nói rõ ràng. Hãy nói gần micro hơn và ghi âm lại.',
          ),
        )
      } finally {
        sendingRef.current = false
        setIsSendingAnswer(false)
      }
    },
    [replacePlayback, session.questions, uploadAnswer],
  )

  const applyHold = useCallback(async () => {
    while (holdWantedRef.current !== recorderLiveRef.current) {
      if (holdWantedRef.current) {
        while (sendingRef.current && holdWantedRef.current) {
          await new Promise((resolve) => window.setTimeout(resolve, 80))
        }
        if (!holdWantedRef.current || runtimeRef.current.phase !== 'exam') continue
        try {
          speechRequestRef.current += 1
          stopSpeechPlayback()
          setIsReadingQuestion(false)
          setAnswerError('')
          recordingSecondsRef.current = 0
          setRecordingSeconds(0)
          // Ghi âm lại: chỉ xóa transcript đúng target (main / một câu phụ).
          const questionId = runtimeRef.current.questionId
          const reRecord = reRecordTargetRef.current
          const recordingFollowUp = activeFollowUpRef.current
          const followUpIdToClear =
            reRecord?.kind === 'followUp'
              ? reRecord.answerId
              : recordingFollowUp?.questionId === questionId
                ? recordingFollowUp.answerId
                : null
          const clearingMainOnly =
            reRecord?.kind === 'main' ||
            (!followUpIdToClear && Boolean(questionId) && !recordingFollowUp)

          if (questionId && followUpIdToClear) {
            setFollowUpDrafts((current) => {
              const next = { ...current, [followUpIdToClear]: '' }
              followUpDraftsRef.current = next
              return next
            })
            // Giữ slot câu phụ (prompt) khi ghi lại — chỉ xóa transcript.
            setFollowUpAnswers((current) => {
              const existing = current[followUpIdToClear]
              if (!existing) return current
              const next = {
                ...current,
                [followUpIdToClear]: { ...existing, transcript: '' },
              }
              followUpAnswersRef.current = next
              return next
            })
          } else if (questionId && clearingMainOnly) {
            setDrafts((current) => ({ ...current, [questionId]: '' }))
            if (reRecord?.kind === 'main') {
              // Ghi lại câu chính khi xem lại: chỉ xóa transcript, giữ slot + câu phụ.
              setAnswers((current) => {
                const existing = current[questionId]
                if (!existing) return current
                const next = {
                  ...current,
                  [questionId]: { ...existing, transcript: '' },
                }
                runtimeRef.current.answers = next
                answersRef.current = next
                return next
              })
            } else if (!reRecord) {
              setAnswers((current) => {
                if (!current[questionId]) return current
                const next = { ...current }
                delete next[questionId]
                runtimeRef.current.answers = next
                answersRef.current = next
                return next
              })
              const removedFollowUpIds = Object.entries(followUpAnswersRef.current)
                .filter(([, item]) => item.questionId === questionId)
                .map(([id]) => id)
              if (removedFollowUpIds.length > 0) {
                setFollowUpAnswers((current) => {
                  const next = { ...current }
                  for (const id of removedFollowUpIds) delete next[id]
                  followUpAnswersRef.current = next
                  return next
                })
                setFollowUpDrafts((current) => {
                  const next = { ...current }
                  for (const id of removedFollowUpIds) delete next[id]
                  followUpDraftsRef.current = next
                  return next
                })
              }
              setQueuedFollowUp(null)
              if (activeFollowUpRef.current?.questionId === questionId) {
                persistActiveFollowUp(null)
              }
              removeMockExamAnswer(questionId, session.sessionId)
            }
          }
          replacePlayback(null)
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
  }, [
    finishRecording,
    persistActiveFollowUp,
    replacePlayback,
    session.sessionId,
    startRecording,
    transcribeReleased,
  ])

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

      const submitPendingAudio = async (blob: Blob, questionId: string, seconds: number) => {
        const currentQuestion =
          session.questions.find((item) => item.id === questionId) ?? null
        const speech = await transcribeSpeech(blob, {
          vocabularyHint: currentQuestion
            ? buildVocabularyHint(currentQuestion)
            : undefined,
          fileName: `mock-answer-${questionId}.webm`,
        })
        const transcript = speech.transcript.trim()
        if (
          isLikelySilentOrInvalidTranscript(transcript, seconds) ||
          (typeof speech.confidenceScore === 'number' &&
            speech.confidenceScore > 0 &&
            speech.confidenceScore < 0.25) ||
          (currentQuestion != null && looksLikeQuestionEcho(transcript, currentQuestion))
        ) {
          return
        }
        const followUp = activeFollowUpRef.current
        await uploadAnswer(
          questionId,
          null,
          seconds,
          transcript,
          followUp && followUp.questionId === questionId ? followUp.answerId : null,
        )
      }

      try {
        if (recorderLiveRef.current) {
          try {
            const blob = await finishRecording()
            recorderLiveRef.current = false
            if (blob && runtimeRef.current.questionId) {
              try {
                await submitPendingAudio(
                  blob,
                  runtimeRef.current.questionId,
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
            await submitPendingAudio(
              runtimeRef.current.audioBlob,
              runtimeRef.current.questionId,
              runtimeRef.current.recordedSeconds,
            )
          } catch (error) {
            if (reason === 'manual' && getMockExamErrorStatus(error) !== 410) throw error
          }
        }

        // Luôn đẩy transcript đã chỉnh sửa lên server trước khi nộp bài.
        try {
          await flushDirtyTranscripts()
        } catch (error) {
          if (reason === 'manual' && getMockExamErrorStatus(error) !== 410) throw error
        }

        const result = await mockExamService.submitSession(session.sessionId)
        clearMockExamSession(session.sessionId)
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
    [finishRecording, flushDirtyTranscripts, session.questions, session.sessionId, uploadAnswer],
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
      if (draftSaveTimerRef.current !== null) {
        window.clearTimeout(draftSaveTimerRef.current)
        draftSaveTimerRef.current = null
      }
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

  const toggleRecording = useCallback(() => {
    if (sendingRef.current || runtimeRef.current.phase !== 'exam') return
    if (isRecording || holdWantedRef.current) {
      void requestHold(false)
      return
    }
    // Không cho Space/ghi âm khi đang xem lại (mic đang ẩn).
    if (!showMicSectionRef.current) return
    void requestHold(true)
  }, [isRecording, requestHold])

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.code !== 'Space' || event.repeat || phase !== 'exam') return
      if (warningOpenRef.current || shouldIgnoreSpace(event.target)) return
      event.preventDefault()
      toggleRecording()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [phase, toggleRecording])

  useEffect(() => {
    if (!autoRead || !question || phase !== 'exam') return
    const prompt = activeFollowUp?.prompt ?? question.content
    const timer = window.setTimeout(() => {
      startReadingQuestion(prompt)
    }, 0)
    return () => {
      window.clearTimeout(timer)
      stopReadingQuestion()
    }
  }, [activeFollowUp?.prompt, autoRead, phase, question, startReadingQuestion, stopReadingQuestion])

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

  const canNavigateTo = (targetIndex: number) => {
    if (targetIndex < 0 || targetIndex >= session.questions.length) return false
    // Đã xem (làm / bỏ qua) → được quay lại
    if (targetIndex <= furthestIndex) return true
    // Chỉ được tiến đúng 1 câu kế tiếp từ câu đang mở xa nhất
    return targetIndex === questionIndex + 1 && questionIndex === furthestIndex
  }

  const moveToQuestion = async (
    nextIndex: number,
    options?: { skipFollowUpWarning?: boolean },
  ) => {
    if (isRecording || isSendingAnswer || nextIndex === questionIndex) return
    if (!canNavigateTo(nextIndex)) return
    if (activeFollowUp) {
      if (savedFollowUpAnswer) {
        const nextTranscript = draft.trim()
        if (!nextTranscript) {
          setAnswerError('Transcript không được để trống. Hãy giữ nội dung hoặc nói lại.')
          return
        }
        if (nextTranscript !== savedFollowUpAnswer.transcript.trim()) {
          setIsSendingAnswer(true)
          try {
            await persistFollowUpTranscript(activeFollowUp.answerId)
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
        }
      } else if (!options?.skipFollowUpWarning) {
        const confirmed = await askWarning({
          title: 'Bỏ qua câu hỏi phụ?',
          message:
            'Bạn đang có câu hỏi phụ chưa trả lời. Chuyển câu sẽ bỏ qua câu phụ này (tính chưa trả lời).',
          confirmLabel: 'Bỏ qua câu phụ',
          cancelLabel: 'Ở lại',
          tone: 'warning',
        })
        if (!confirmed) return
      }
      persistActiveFollowUp(null)
      setQueuedFollowUp(null)
    }
    if (question && savedAnswer) {
      const mainDraft = (draftsRef.current[question.id] ?? '').trim()
      if (!mainDraft) {
        setAnswerError('Transcript không được để trống. Hãy giữ nội dung hoặc nói lại.')
        return
      }
      if (mainDraft !== savedAnswer.transcript.trim()) {
        setIsSendingAnswer(true)
        try {
          await persistQuestionTranscript(question.id)
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
      }
    } else if (audioBlob && !savedAnswer) {
      // Chỉ cảnh báo khi còn blob local mà câu chưa có transcript đã nộp.
      const confirmed = await askWarning({
        title: 'Bỏ bản ghi chưa gửi?',
        message:
          'Bản ghi này chưa tạo được transcript. Chuyển câu sẽ bỏ bản ghi và không chấm câu hiện tại.',
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
    setFurthestIndex((current) => {
      const nextFurthest = Math.max(current, nextIndex)
      if (nextFurthest !== current) {
        const stored = readMockExamSessionById(session.sessionId)
        if (stored) {
          saveMockExamSession({ ...stored, furthestQuestionIndex: nextFurthest })
        }
      }
      return nextFurthest
    })
  }

  const advanceToNextMainQuestion = async () => {
    persistActiveFollowUp(null)
    setQueuedFollowUp(null)
    replacePlayback(null)
    setAnswerError('')
    setRecordedSeconds(0)
    if (!isLastQuestion) {
      await moveToQuestion(questionIndex + 1, { skipFollowUpWarning: true })
    }
  }

  /** Sau khi chỉnh transcript câu chính → mở câu phụ đang xếp hàng. */
  const openQueuedFollowUp = async () => {
    if (isRecording || isSendingAnswer || !queuedFollowUp || !question) return
    if (savedAnswer) {
      const mainDraft = (draftsRef.current[question.id] ?? '').trim()
      if (!mainDraft) {
        setAnswerError('Transcript không được để trống. Hãy giữ nội dung hoặc nói lại.')
        return
      }
      if (mainDraft !== savedAnswer.transcript.trim()) {
        setIsSendingAnswer(true)
        try {
          await persistQuestionTranscript(question.id)
        } catch (error) {
          if (getMockExamErrorStatus(error) === 410) {
            void finalizeExam('timeout')
            return
          }
          setAnswerError(
            getMockExamErrorDetail(error, 'Không thể lưu transcript trước khi sang câu phụ.'),
          )
          return
        } finally {
          setIsSendingAnswer(false)
        }
      }
    }
    const next = queuedFollowUp
    setQueuedFollowUp(null)
    persistActiveFollowUp(next)
    replacePlayback(null)
    setAnswerError('')
    setRecordedSeconds(0)
  }

  const continueAfterFollowUp = async () => {
    if (isRecording || isSendingAnswer || !activeFollowUp) return
    if (savedFollowUpAnswer) {
      const nextTranscript = draft.trim()
      if (!nextTranscript) {
        setAnswerError('Transcript không được để trống. Hãy giữ nội dung hoặc nói lại.')
        return
      }
      if (nextTranscript !== savedFollowUpAnswer.transcript.trim()) {
        setIsSendingAnswer(true)
        try {
          await persistFollowUpTranscript(activeFollowUp.answerId)
        } catch (error) {
          if (getMockExamErrorStatus(error) === 410) {
            void finalizeExam('timeout')
            return
          }
          setAnswerError(
            getMockExamErrorDetail(error, 'Không thể lưu transcript trước khi tiếp tục.'),
          )
          return
        } finally {
          setIsSendingAnswer(false)
        }
      }
    }

    if (queuedFollowUp) {
      persistActiveFollowUp(queuedFollowUp)
      setQueuedFollowUp(null)
      replacePlayback(null)
      setAnswerError('')
      setRecordedSeconds(0)
      return
    }

    await advanceToNextMainQuestion()
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
    setIsSendingAnswer(true)
    try {
      await flushDirtyTranscripts()
    } catch (error) {
      if (getMockExamErrorStatus(error) === 410) {
        void finalizeExam('timeout')
        return
      }
      setAnswerError(getMockExamErrorDetail(error, 'Không thể lưu transcript đã chỉnh sửa trước khi nộp.'))
      return
    } finally {
      setIsSendingAnswer(false)
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
          <div className="flex items-center justify-between gap-2.5 px-4 py-3.5 sm:px-5">
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
          <div className="flex items-center gap-2.5 border-t border-white/15 px-4 py-2.5 sm:px-5">
            <span className="text-xs font-bold text-white/90">
              Câu {session.questions.length === 0 ? 0 : questionIndex + 1}/{session.questions.length}
            </span>
            <div className="flex h-2.5 min-w-0 flex-1 gap-1.5">
              {session.questions.map((item, index) => {
                const unlocked = index <= furthestIndex
                return (
                  <button
                    key={item.id}
                    type="button"
                    data-space-ignore="true"
                    aria-label={
                      unlocked
                        ? `Chuyển tới câu ${index + 1}`
                        : `Câu ${index + 1} chưa mở`
                    }
                    aria-current={index === questionIndex ? 'step' : undefined}
                    disabled={
                      !unlocked ||
                      isRecording ||
                      isSendingAnswer ||
                      secondsLeft <= 0
                    }
                    onClick={() => void moveToQuestion(index)}
                    className={cn(
                      'h-full min-w-0 flex-1 rounded-full transition enabled:hover:opacity-90 disabled:cursor-not-allowed',
                      !unlocked
                        ? 'bg-white/10'
                        : answers[item.id]
                          ? 'bg-white'
                          : index === questionIndex
                            ? 'bg-[#BDE8F5]'
                            : 'bg-white/25',
                    )}
                  />
                )
              })}
            </div>
          </div>
        </header>

        {question ? (
          <section
            ref={transcriptFitRootRef}
            className="flex flex-col gap-3 px-4 py-4 sm:px-5"
          >
            <div className="flex flex-wrap items-center justify-between gap-2.5">
              <p className="text-sm font-semibold text-[#5b6b84]">
                {activeFollowUp
                  ? `Câu hỏi phụ ${activeFollowUp.followUpIndex}/${activeFollowUp.maxFollowUps}`
                  : `${question.bloomLevel} · ${difficultyLabel(question.difficulty)}`}
              </p>
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
            {activeFollowUp && (
              <p className="rounded-xl border border-[#4988C4]/35 bg-[#F4FBFD] px-3.5 py-2.5 text-sm leading-relaxed text-[#1C4D8D]">
                AI yêu cầu làm rõ thêm trước khi sang câu chính tiếp theo.
              </p>
            )}
            {!activeFollowUp && savedAnswer && queuedFollowUp && (
              <p className="rounded-xl border border-[#4988C4]/35 bg-[#F4FBFD] px-3.5 py-2.5 text-sm leading-relaxed text-[#1C4D8D]">
                Hãy kiểm tra và chỉnh transcript câu chính, rồi chọn <span className="font-bold">Câu hỏi phụ</span> hoặc bỏ qua để sang câu kế.
              </p>
            )}
            <div className="overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-white">
              {!activeFollowUp && question.imageUrl?.trim() ? (
                <div data-question-panel className="grid grid-cols-1 md:grid-cols-2">
                  {/* Text absolute on md: row height = image; font auto-fits that height */}
                  <div className="relative p-3.5 md:p-0">
                    <h2 className="text-[18px] leading-snug font-semibold whitespace-pre-line text-[#0F2854] md:hidden">
                      <span className="font-semibold">Câu {question.orderIndex}. </span>
                      {question.content}
                    </h2>
                    <div className="hidden md:absolute md:inset-0 md:block md:p-3.5">
                      <AutoFitQuestionText
                        orderIndex={question.orderIndex}
                        content={question.content}
                        minPx={12}
                        maxPx={24}
                      />
                    </div>
                  </div>
                  <div className="p-3.5">
                    <QuestionImage
                      imageUrl={question.imageUrl}
                      framed={false}
                      showCaption={false}
                      className="w-full overflow-hidden rounded-xl"
                    />
                  </div>
                </div>
              ) : (
                <div className="p-3.5">
                  <h2 className="text-[18px] leading-snug font-semibold whitespace-pre-line text-[#0F2854]">
                    <span className="font-semibold">
                      {activeFollowUp
                        ? `Câu hỏi phụ ${activeFollowUp.followUpIndex}. `
                        : `Câu ${question.orderIndex}. `}
                    </span>
                    {activeFollowUp ? activeFollowUp.prompt : question.content}
                  </h2>
                </div>
              )}
            </div>

            {/* Xem lại: mỗi câu (chính/phụ) = input + nút ghi lại; mic chỉ dưới đúng câu đó */}
            {isReviewingAnsweredMain && (
              <div className="flex flex-col gap-4">
                <div className="flex flex-col gap-2">
                  <label htmlFor="mock-transcript-main" className="text-sm font-semibold text-[#0F2854]">
                    Câu trả lời chính
                  </label>
                  <p className="text-xs leading-relaxed text-[#5b6b84]">
                    Nội dung chỉnh sửa được lưu tự động và dùng khi nộp bài.
                  </p>
                  {isSendingAnswer && isMicOpenForMain && !savedAnswer ? (
                    <div className="flex min-h-10 items-center gap-2 rounded-2xl border border-[#4988C4]/45 bg-[#F4FBFD] px-3.5 py-2 text-sm text-[#5b6b84]">
                      <LoaderCircle className="size-4 animate-spin" />
                      Đang nhận diện giọng nói tiếng Việt…
                    </div>
                  ) : savedAnswer || isMicOpenForMain ? (
                    <textarea
                      id="mock-transcript-main"
                      data-transcript-fit
                      rows={1}
                      value={drafts[question.id] ?? ''}
                      data-space-ignore="true"
                      disabled={isRecording && isMicOpenForMain}
                      onChange={(event) => {
                        fitTranscriptTextarea(event.currentTarget)
                        const value = event.target.value
                        setDrafts((current) => {
                          const next = { ...current, [question.id]: value }
                          draftsRef.current = next
                          return next
                        })
                        scheduleDraftPersist(question.id)
                      }}
                      onBlur={() => {
                        if (draftSaveTimerRef.current !== null) {
                          window.clearTimeout(draftSaveTimerRef.current)
                          draftSaveTimerRef.current = null
                        }
                        void persistQuestionTranscript(question.id).catch((error: unknown) => {
                          if (getMockExamErrorStatus(error) === 410) {
                            void finalizeExam('timeout')
                            return
                          }
                          setAnswerError(
                            getMockExamErrorDetail(error, 'Không thể lưu transcript đã chỉnh sửa.'),
                          )
                        })
                      }}
                      className="min-h-10 w-full resize-none rounded-2xl border border-[#4988C4] bg-white px-3.5 py-2 text-sm leading-relaxed text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30 disabled:opacity-60"
                    />
                  ) : (
                    <p className="rounded-2xl border border-[#4988C4]/35 bg-[#F4FBFD] px-3.5 py-3 text-sm text-[#5b6b84]">
                      Chưa có transcript câu chính trên máy này. Hãy ghi âm lại hoặc tiếp tục câu khác.
                    </p>
                  )}
                  {savedAnswer &&
                    !isMicOpenForMain &&
                    !isRecording &&
                    !isSendingAnswer && (
                      <button
                        type="button"
                        data-space-ignore="true"
                        onClick={() =>
                          setReRecordTarget({ kind: 'main', questionId: question.id })
                        }
                        className="inline-flex min-h-10 w-fit items-center gap-2 rounded-xl border border-[#4988C4] bg-white px-3.5 text-sm font-bold text-[#1C4D8D] hover:bg-[#F4FBFD]"
                      >
                        <Mic className="size-4" />
                        Ghi âm lại câu trả lời
                      </button>
                    )}
                  {isMicOpenForMain && (
                    <div className="flex flex-col items-center gap-2 pt-1">
                      <button
                        type="button"
                        disabled={isSendingAnswer}
                        aria-pressed={isRecording}
                        aria-label={isRecording ? 'Dừng ghi âm' : 'Bắt đầu ghi âm'}
                        onClick={toggleRecording}
                        className={cn(
                          'flex size-[7.5rem] flex-col items-center justify-center gap-1 rounded-full border text-sm font-bold shadow-[0_12px_28px_rgba(28,77,141,0.16)] transition select-none disabled:opacity-60',
                          isRecording
                            ? 'border-[#ff8fa0] bg-[#ff5d73] text-white shadow-[0_12px_28px_rgba(255,93,115,0.28)]'
                            : 'border-[#4988C4] bg-[#F4FBFD] text-[#0F2854] hover:bg-[#BDE8F5]',
                        )}
                      >
                        <Mic
                          className={cn(
                            'size-7',
                            isRecording && 'animate-pulse motion-reduce:animate-none',
                          )}
                        />
                        <span className="text-xs font-bold">
                          {isRecording ? formatClock(recordingSeconds) : 'Ghi âm'}
                        </span>
                      </button>
                      <p className="text-center text-sm font-bold text-[#0F2854]">
                        {isRecording
                          ? 'Nhấn để dừng ghi âm'
                          : isSendingAnswer
                            ? 'Đang nhận diện giọng nói…'
                            : 'Ghi âm lại câu trả lời chính'}
                      </p>
                      {!isRecording && !isSendingAnswer && (
                        <button
                          type="button"
                          data-space-ignore="true"
                          onClick={() => setReRecordTarget(null)}
                          className="text-sm font-bold text-[#1C4D8D] hover:underline"
                        >
                          Hủy ghi âm lại
                        </button>
                      )}
                    </div>
                  )}
                </div>

                {reviewedFollowUps.length > 0 && (
                  <div className="space-y-3">
                    <p className="text-sm font-semibold text-[#0F2854]">Câu hỏi phụ đã trả lời</p>
                    {reviewedFollowUps.map((item, index) => {
                      const micOpen = isMicOpenForFollowUp(item.answerId)
                      const fuDraft = followUpDrafts[item.answerId] ?? item.transcript
                      return (
                        <div
                          key={item.answerId}
                          className="space-y-2 rounded-2xl border border-[#4988C4]/35 bg-[#F4FBFD] px-3.5 py-3"
                        >
                          <p className="text-xs font-bold text-[#1C4D8D]">
                            Câu hỏi phụ {index + 1}
                          </p>
                          <p className="text-sm leading-relaxed font-semibold whitespace-pre-line text-[#0F2854]">
                            {item.promptText?.trim() || 'Không có nội dung câu hỏi phụ.'}
                          </p>
                          <label
                            htmlFor={`mock-transcript-fu-${item.answerId}`}
                            className="text-xs font-bold text-[#5b6b84]"
                          >
                            Câu trả lời phụ
                          </label>
                          <textarea
                            id={`mock-transcript-fu-${item.answerId}`}
                            data-transcript-fit
                            rows={1}
                            value={fuDraft}
                            data-space-ignore="true"
                            disabled={isRecording && micOpen}
                            onChange={(event) => {
                              fitTranscriptTextarea(event.currentTarget)
                              const value = event.target.value
                              setFollowUpDrafts((current) => {
                                const next = { ...current, [item.answerId]: value }
                                followUpDraftsRef.current = next
                                return next
                              })
                              scheduleFollowUpDraftPersist(item.answerId)
                            }}
                            onBlur={() => {
                              if (draftSaveTimerRef.current !== null) {
                                window.clearTimeout(draftSaveTimerRef.current)
                                draftSaveTimerRef.current = null
                              }
                              void persistFollowUpTranscript(item.answerId).catch(
                                (error: unknown) => {
                                  if (getMockExamErrorStatus(error) === 410) {
                                    void finalizeExam('timeout')
                                    return
                                  }
                                  setAnswerError(
                                    getMockExamErrorDetail(
                                      error,
                                      'Không thể lưu transcript đã chỉnh sửa.',
                                    ),
                                  )
                                },
                              )
                            }}
                            className="min-h-10 w-full resize-none rounded-xl border border-[#4988C4]/45 bg-white px-3 py-2 text-sm leading-relaxed text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30 disabled:opacity-60"
                          />
                          {!micOpen && !isRecording && !isSendingAnswer && (
                            <button
                              type="button"
                              data-space-ignore="true"
                              onClick={() =>
                                setReRecordTarget({
                                  kind: 'followUp',
                                  answerId: item.answerId,
                                  questionId: question.id,
                                })
                              }
                              className="inline-flex min-h-10 w-fit items-center gap-2 rounded-xl border border-[#4988C4] bg-white px-3.5 text-sm font-bold text-[#1C4D8D] hover:bg-[#F4FBFD]"
                            >
                              <Mic className="size-4" />
                              Ghi âm lại câu trả lời
                            </button>
                          )}
                          {micOpen && (
                            <div className="flex flex-col items-center gap-2 pt-1">
                              <button
                                type="button"
                                disabled={isSendingAnswer}
                                aria-pressed={isRecording}
                                aria-label={isRecording ? 'Dừng ghi âm' : 'Bắt đầu ghi âm'}
                                onClick={toggleRecording}
                                className={cn(
                                  'flex size-[7.5rem] flex-col items-center justify-center gap-1 rounded-full border text-sm font-bold shadow-[0_12px_28px_rgba(28,77,141,0.16)] transition select-none disabled:opacity-60',
                                  isRecording
                                    ? 'border-[#ff8fa0] bg-[#ff5d73] text-white shadow-[0_12px_28px_rgba(255,93,115,0.28)]'
                                    : 'border-[#4988C4] bg-[#F4FBFD] text-[#0F2854] hover:bg-[#BDE8F5]',
                                )}
                              >
                                <Mic
                                  className={cn(
                                    'size-7',
                                    isRecording && 'animate-pulse motion-reduce:animate-none',
                                  )}
                                />
                                <span className="text-xs font-bold">
                                  {isRecording ? formatClock(recordingSeconds) : 'Ghi âm'}
                                </span>
                              </button>
                              <p className="text-center text-sm font-bold text-[#0F2854]">
                                {isRecording
                                  ? 'Nhấn để dừng ghi âm'
                                  : isSendingAnswer
                                    ? 'Đang nhận diện câu trả lời phụ…'
                                    : 'Ghi âm lại câu trả lời phụ'}
                              </p>
                              {!isRecording && !isSendingAnswer && (
                                <button
                                  type="button"
                                  data-space-ignore="true"
                                  onClick={() => setReRecordTarget(null)}
                                  className="text-sm font-bold text-[#1C4D8D] hover:underline"
                                >
                                  Hủy ghi âm lại
                                </button>
                              )}
                            </div>
                          )}
                        </div>
                      )
                    })}
                  </div>
                )}
              </div>
            )}

            {/* Đang làm câu phụ: sau khi có đáp án — input + nút ghi lại + mic dưới câu đó */}
            {activeFollowUp && (savedFollowUpAnswer || isSendingAnswer) && (
              <div className="flex flex-col gap-2">
                <label
                  htmlFor="mock-transcript-active-fu"
                  className="text-sm font-semibold text-[#0F2854]"
                >
                  Câu trả lời phụ
                </label>
                {isSendingAnswer && !savedFollowUpAnswer ? (
                  <div className="flex min-h-10 items-center gap-2 rounded-2xl border border-[#4988C4]/45 bg-[#F4FBFD] px-3.5 py-2 text-sm text-[#5b6b84]">
                    <LoaderCircle className="size-4 animate-spin" />
                    Đang nhận diện câu trả lời phụ…
                  </div>
                ) : (
                  <>
                    <p className="text-xs leading-relaxed text-[#5b6b84]">
                      Nội dung chỉnh sửa được lưu tự động và dùng khi nộp bài.
                    </p>
                    <textarea
                      id="mock-transcript-active-fu"
                      data-transcript-fit
                      ref={transcriptAreaRef}
                      rows={1}
                      value={draft}
                      data-space-ignore="true"
                      disabled={
                        isRecording && isMicOpenForFollowUp(activeFollowUp.answerId)
                      }
                      onChange={(event) => {
                        fitTranscriptTextarea(event.currentTarget)
                        const value = event.target.value
                        const answerId = activeFollowUp.answerId
                        setFollowUpDrafts((current) => {
                          const next = { ...current, [answerId]: value }
                          followUpDraftsRef.current = next
                          return next
                        })
                        scheduleFollowUpDraftPersist(answerId)
                      }}
                      onBlur={() => {
                        if (draftSaveTimerRef.current !== null) {
                          window.clearTimeout(draftSaveTimerRef.current)
                          draftSaveTimerRef.current = null
                        }
                        void persistFollowUpTranscript(activeFollowUp.answerId).catch(
                          (error: unknown) => {
                            if (getMockExamErrorStatus(error) === 410) {
                              void finalizeExam('timeout')
                              return
                            }
                            setAnswerError(
                              getMockExamErrorDetail(
                                error,
                                'Không thể lưu transcript đã chỉnh sửa.',
                              ),
                            )
                          },
                        )
                      }}
                      className="min-h-10 w-full resize-none rounded-2xl border border-[#4988C4] bg-white px-3.5 py-2 text-sm leading-relaxed text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30 disabled:opacity-60"
                    />
                    {!isMicOpenForFollowUp(activeFollowUp.answerId) &&
                      !isRecording &&
                      !isSendingAnswer && (
                        <button
                          type="button"
                          data-space-ignore="true"
                          onClick={() =>
                            setReRecordTarget({
                              kind: 'followUp',
                              answerId: activeFollowUp.answerId,
                              questionId: question.id,
                            })
                          }
                          className="inline-flex min-h-10 w-fit items-center gap-2 rounded-xl border border-[#4988C4] bg-white px-3.5 text-sm font-bold text-[#1C4D8D] hover:bg-[#F4FBFD]"
                        >
                          <Mic className="size-4" />
                          Ghi âm lại câu trả lời
                        </button>
                      )}
                    {isMicOpenForFollowUp(activeFollowUp.answerId) && (
                      <div className="flex flex-col items-center gap-2 pt-1">
                        <button
                          type="button"
                          disabled={isSendingAnswer}
                          aria-pressed={isRecording}
                          aria-label={isRecording ? 'Dừng ghi âm' : 'Bắt đầu ghi âm'}
                          onClick={toggleRecording}
                          className={cn(
                            'flex size-[7.5rem] flex-col items-center justify-center gap-1 rounded-full border text-sm font-bold shadow-[0_12px_28px_rgba(28,77,141,0.16)] transition select-none disabled:opacity-60',
                            isRecording
                              ? 'border-[#ff8fa0] bg-[#ff5d73] text-white shadow-[0_12px_28px_rgba(255,93,115,0.28)]'
                              : 'border-[#4988C4] bg-[#F4FBFD] text-[#0F2854] hover:bg-[#BDE8F5]',
                          )}
                        >
                          <Mic
                            className={cn(
                              'size-7',
                              isRecording && 'animate-pulse motion-reduce:animate-none',
                            )}
                          />
                          <span className="text-xs font-bold">
                            {isRecording ? formatClock(recordingSeconds) : 'Ghi âm'}
                          </span>
                        </button>
                        <p className="text-center text-sm font-bold text-[#0F2854]">
                          {isRecording ? 'Nhấn để dừng ghi âm' : 'Ghi âm lại câu trả lời phụ'}
                        </p>
                        {!isRecording && !isSendingAnswer && (
                          <button
                            type="button"
                            data-space-ignore="true"
                            onClick={() => setReRecordTarget(null)}
                            className="text-sm font-bold text-[#1C4D8D] hover:underline"
                          >
                            Hủy ghi âm lại
                          </button>
                        )}
                      </div>
                    )}
                  </>
                )}
              </div>
            )}

            {/* Lần ghi đầu (chưa có đáp án): mic dưới câu hỏi */}
            {showFirstPassMic && (
              <div className="flex flex-col items-center gap-2">
                <button
                  type="button"
                  disabled={isSendingAnswer}
                  aria-pressed={isRecording}
                  aria-label={isRecording ? 'Dừng ghi âm' : 'Bắt đầu ghi âm'}
                  onClick={toggleRecording}
                  className={cn(
                    'flex size-[7.5rem] flex-col items-center justify-center gap-1 rounded-full border text-sm font-bold shadow-[0_12px_28px_rgba(28,77,141,0.16)] transition select-none disabled:opacity-60',
                    isRecording
                      ? 'border-[#ff8fa0] bg-[#ff5d73] text-white shadow-[0_12px_28px_rgba(255,93,115,0.28)]'
                      : 'border-[#4988C4] bg-[#F4FBFD] text-[#0F2854] hover:bg-[#BDE8F5]',
                  )}
                >
                  <Mic
                    className={cn(
                      'size-7',
                      isRecording && 'animate-pulse motion-reduce:animate-none',
                    )}
                  />
                  <span className="text-xs font-bold">
                    {isRecording ? formatClock(recordingSeconds) : 'Ghi âm'}
                  </span>
                </button>
                <p className="text-center text-sm font-bold text-[#0F2854]">
                  {isRecording
                    ? 'Nhấn để dừng ghi âm'
                    : isSendingAnswer
                      ? activeFollowUp
                        ? 'Đang nhận diện câu trả lời phụ…'
                        : 'Đang nhận diện giọng nói tiếng Việt…'
                      : 'Nhấn để bắt đầu ghi âm'}
                </p>
                <p className="text-center text-xs leading-relaxed text-[#5b6b84]">
                  Nhấn nút hoặc phím{' '}
                  <kbd className="rounded bg-[#F4FBFD] px-1.5 py-0.5 font-bold text-[#0F2854]">
                    Space
                  </kbd>{' '}
                  để bật/tắt. Dừng để xem transcript.
                </p>
              </div>
            )}

            {/* Lần ghi đầu câu chính xong (chưa sang xem lại đầy đủ): transcript dưới mic */}
            {!isReviewingAnsweredMain &&
              !activeFollowUp &&
              !isRecording &&
              (isSendingAnswer || Boolean(savedAnswer)) && (
                <div className="flex flex-col gap-2">
                  <label
                    htmlFor="mock-transcript-live"
                    className="text-sm font-semibold text-[#0F2854]"
                  >
                    Transcript để kiểm tra và chỉnh sửa
                  </label>
                  <p className="text-xs leading-relaxed text-[#5b6b84]">
                    Nội dung chỉnh sửa được lưu tự động và dùng khi nộp bài.
                  </p>
                  {isSendingAnswer && !savedAnswer ? (
                    <div className="flex min-h-10 items-center gap-2 rounded-2xl border border-[#4988C4]/45 bg-[#F4FBFD] px-3.5 py-2 text-sm text-[#5b6b84]">
                      <LoaderCircle className="size-4 animate-spin" />
                      Đang nhận diện giọng nói tiếng Việt…
                    </div>
                  ) : (
                    <textarea
                      id="mock-transcript-live"
                      data-transcript-fit
                      ref={transcriptAreaRef}
                      rows={1}
                      value={draft}
                      data-space-ignore="true"
                      onChange={(event) => {
                        fitTranscriptTextarea(event.currentTarget)
                        const value = event.target.value
                        setDrafts((current) => {
                          const next = { ...current, [question.id]: value }
                          draftsRef.current = next
                          return next
                        })
                        scheduleDraftPersist(question.id)
                      }}
                      onBlur={() => {
                        if (draftSaveTimerRef.current !== null) {
                          window.clearTimeout(draftSaveTimerRef.current)
                          draftSaveTimerRef.current = null
                        }
                        void persistQuestionTranscript(question.id).catch((error: unknown) => {
                          if (getMockExamErrorStatus(error) === 410) {
                            void finalizeExam('timeout')
                            return
                          }
                          setAnswerError(
                            getMockExamErrorDetail(
                              error,
                              'Không thể lưu transcript đã chỉnh sửa.',
                            ),
                          )
                        })
                      }}
                      className="min-h-10 w-full resize-none rounded-2xl border border-[#4988C4] bg-white px-3.5 py-2 text-sm leading-relaxed text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30"
                    />
                  )}
                  {savedAnswer &&
                    !isSendingAnswer &&
                    !(
                      reRecordTarget?.kind === 'main' &&
                      reRecordTarget.questionId === question.id
                    ) && (
                      <button
                        type="button"
                        data-space-ignore="true"
                        onClick={() =>
                          setReRecordTarget({ kind: 'main', questionId: question.id })
                        }
                        className="inline-flex min-h-10 w-fit items-center gap-2 rounded-xl border border-[#4988C4] bg-white px-3.5 text-sm font-bold text-[#1C4D8D] hover:bg-[#F4FBFD]"
                      >
                        <Mic className="size-4" />
                        Ghi âm lại câu trả lời
                      </button>
                    )}
                  {isMicOpenForMain && (
                    <div className="flex flex-col items-center gap-2 pt-1">
                      <button
                        type="button"
                        disabled={isSendingAnswer}
                        aria-pressed={isRecording}
                        onClick={toggleRecording}
                        className={cn(
                          'flex size-[7.5rem] flex-col items-center justify-center gap-1 rounded-full border text-sm font-bold shadow-[0_12px_28px_rgba(28,77,141,0.16)] transition select-none disabled:opacity-60',
                          isRecording
                            ? 'border-[#ff8fa0] bg-[#ff5d73] text-white shadow-[0_12px_28px_rgba(255,93,115,0.28)]'
                            : 'border-[#4988C4] bg-[#F4FBFD] text-[#0F2854] hover:bg-[#BDE8F5]',
                        )}
                      >
                        <Mic
                          className={cn(
                            'size-7',
                            isRecording && 'animate-pulse motion-reduce:animate-none',
                          )}
                        />
                        <span className="text-xs font-bold">
                          {isRecording ? formatClock(recordingSeconds) : 'Ghi âm'}
                        </span>
                      </button>
                      {!isRecording && !isSendingAnswer && (
                        <button
                          type="button"
                          data-space-ignore="true"
                          onClick={() => setReRecordTarget(null)}
                          className="text-sm font-bold text-[#1C4D8D] hover:underline"
                        >
                          Hủy ghi âm lại
                        </button>
                      )}
                    </div>
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

            <div className="flex flex-wrap items-center justify-between gap-2 pt-0.5">
              <button
                type="button"
                data-space-ignore="true"
                onClick={() => void moveToQuestion(questionIndex - 1)}
                disabled={questionIndex <= 0 || isRecording || isSendingAnswer || secondsLeft <= 0}
                className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-[#4988C4]/45 bg-white px-4 text-sm font-bold text-[#0F2854] shadow-[0_3px_8px_rgba(23,48,96,0.05)] hover:border-[#4988C4]/45 disabled:opacity-40"
              >
                <ArrowLeft className="size-4" />
                Câu trước
              </button>
              <div className="flex flex-wrap items-center justify-end gap-2">
                {activeFollowUp && !savedFollowUpAnswer && (
                  <button
                    type="button"
                    data-space-ignore="true"
                    onClick={() => void advanceToNextMainQuestion()}
                    disabled={isRecording || isSendingAnswer || secondsLeft <= 0}
                    className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-[#4988C4]/45 bg-white px-4 text-sm font-bold text-[#0F2854] shadow-[0_3px_8px_rgba(23,48,96,0.05)] hover:border-[#4988C4]/45 disabled:opacity-40"
                  >
                    Bỏ qua câu phụ
                    <ArrowRight className="size-4" />
                  </button>
                )}
                {activeFollowUp && savedFollowUpAnswer && (
                  <button
                    type="button"
                    data-space-ignore="true"
                    onClick={() => void continueAfterFollowUp()}
                    disabled={isRecording || isSendingAnswer || secondsLeft <= 0}
                    className="inline-flex min-h-10 items-center gap-2 rounded-xl bg-[#f5c518] px-4 text-sm font-bold text-[#1b1600] disabled:opacity-40"
                  >
                    {queuedFollowUp
                      ? 'Câu phụ tiếp'
                      : isLastQuestion
                        ? 'Xong câu phụ'
                        : 'Câu tiếp'}
                    <ArrowRight className="size-4" />
                  </button>
                )}
                {!activeFollowUp && !isLastQuestion && !savedAnswer && (
                  <button
                    type="button"
                    data-space-ignore="true"
                    onClick={() => void moveToQuestion(questionIndex + 1)}
                    disabled={isRecording || isSendingAnswer || secondsLeft <= 0}
                    className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-[#4988C4]/45 bg-white px-4 text-sm font-bold text-[#0F2854] shadow-[0_3px_8px_rgba(23,48,96,0.05)] hover:border-[#4988C4]/45 disabled:opacity-40"
                  >
                    Bỏ qua
                    <ArrowRight className="size-4" />
                  </button>
                )}
                {!activeFollowUp && savedAnswer && queuedFollowUp && (
                  <>
                    <button
                      type="button"
                      data-space-ignore="true"
                      onClick={() => void advanceToNextMainQuestion()}
                      disabled={isRecording || isSendingAnswer || secondsLeft <= 0}
                      className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-[#4988C4]/45 bg-white px-4 text-sm font-bold text-[#0F2854] shadow-[0_3px_8px_rgba(23,48,96,0.05)] hover:border-[#4988C4]/45 disabled:opacity-40"
                    >
                      Bỏ qua câu phụ
                      <ArrowRight className="size-4" />
                    </button>
                    <button
                      type="button"
                      data-space-ignore="true"
                      onClick={() => void openQueuedFollowUp()}
                      disabled={isRecording || isSendingAnswer || secondsLeft <= 0}
                      className="inline-flex min-h-10 items-center gap-2 rounded-xl bg-[#f5c518] px-4 text-sm font-bold text-[#1b1600] disabled:opacity-40"
                    >
                      Câu hỏi phụ
                      <ArrowRight className="size-4" />
                    </button>
                  </>
                )}
                {!activeFollowUp && savedAnswer && !queuedFollowUp && !isLastQuestion && (
                  <button
                    type="button"
                    data-space-ignore="true"
                    onClick={() => void moveToQuestion(questionIndex + 1)}
                    disabled={isRecording || isSendingAnswer || secondsLeft <= 0}
                    className="inline-flex min-h-10 items-center gap-2 rounded-xl bg-[#f5c518] px-4 text-sm font-bold text-[#1b1600] disabled:opacity-40"
                  >
                    Câu tiếp
                    <ArrowRight className="size-4" />
                  </button>
                )}
                {!activeFollowUp && savedAnswer && !queuedFollowUp && isLastQuestion && (
                  <button
                    type="button"
                    data-space-ignore="true"
                    onClick={handleSubmit}
                    disabled={isRecording || isSendingAnswer}
                    className="inline-flex min-h-10 items-center justify-center rounded-xl bg-[#1C4D8D] px-5 text-sm font-bold text-white shadow-[0_8px_16px_rgba(36,94,234,0.22)] disabled:opacity-40"
                  >
                    Nộp bài
                  </button>
                )}
                {!activeFollowUp && !savedAnswer && isLastQuestion && (
                  <button
                    type="button"
                    data-space-ignore="true"
                    onClick={handleSubmit}
                    disabled={isRecording || isSendingAnswer}
                    className="inline-flex min-h-10 items-center justify-center rounded-xl bg-[#1C4D8D] px-5 text-sm font-bold text-white shadow-[0_8px_16px_rgba(36,94,234,0.22)] disabled:opacity-40"
                  >
                    Nộp bài
                  </button>
                )}
              </div>
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
