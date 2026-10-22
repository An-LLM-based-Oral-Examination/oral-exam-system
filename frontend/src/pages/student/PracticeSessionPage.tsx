import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  AlertCircle,
  AudioLines,
  ArrowLeft,
  BookOpenText,
  CheckCircle2,
  Headphones,
  Keyboard,
  Layers,
  ListChecks,
  LoaderCircle,
  LockKeyhole,
  Mic,
  MicOff,
  RefreshCw,
  Settings,
  Send,
  ShieldCheck,
  Sparkles,
  Square,
  Volume2,
  Wifi,
  WifiOff,
  X,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { ds } from '@/design/system'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { BufferScreen } from '@/components/practice/BufferScreen'
import { ScorecardModal } from '@/components/practice/ScorecardModal'
import { QuestionImage } from '@/components/question/QuestionImage'
import { useAuth } from '@/hooks/useAuth'
import { useAudioRecorder } from '@/hooks/useAudioRecorder'
import { useSpeechRecognition } from '@/hooks/useSpeechRecognition'
import { useSignalR } from '@/hooks/useSignalR'
import { practiceService } from '@/services/practice.service'
import { getTtsVoiceForCourse, playNaturalSpeech, stopSpeechPlayback } from '@/services/speech.service'
import { storageService } from '@/services/storage.service'
import type {
  PracticeAnswerDetail,
  PracticeDifficulty,
  PracticeQuestion,
  ScorecardPayload,
} from '@/types/practice.types'
import {
  loadPracticeRuntime,
  removePracticeRuntime,
  savePracticeRuntime,
  type PendingPracticeSubmission,
  type StoredPracticeRuntime,
} from '@/utils/practiceSessionStorage'

const DIFFICULTY_META: Record<PracticeDifficulty, { label: string; className: string }> = {
  easy: { label: 'Dễ', className: 'border-emerald-200 bg-emerald-50 text-emerald-700' },
  medium: { label: 'Trung bình', className: 'border-amber-200 bg-amber-50 text-amber-700' },
  hard: { label: 'Khó', className: 'border-rose-200 bg-rose-50 text-rose-700' },
}

type AnswerInputMode = 'voice' | 'text'

function findStoredQuestionAnswer(
  runtime: StoredPracticeRuntime,
  questionId: string,
  answers: PracticeAnswerDetail[],
  transientDrafts: Record<string, string>,
) {
  const transientDraft = transientDrafts[questionId]
  if (transientDraft !== undefined) return transientDraft

  const savedDraft = [...runtime.drafts]
    .reverse()
    .find((answer) => answer.questionId === questionId && !answer.isFollowUp)
  if (savedDraft) return savedDraft.answerText

  const submittedAnswer = [...answers]
    .reverse()
    .find((answer) => answer.questionId === questionId && !answer.isFollowUp)
  return submittedAnswer?.answerText ?? ''
}

function getErrorInfo(error: unknown) {
  if (typeof error !== 'object' || error === null) {
    return { status: 500, message: 'Không thể xử lý yêu cầu. Vui lòng thử lại.' }
  }
  const candidate = error as Record<string, unknown>
  const status = typeof candidate.status === 'number' ? candidate.status : 500
  const rawMessage = candidate.detail ?? candidate.error ?? candidate.message ?? candidate.title
  const message =
    typeof rawMessage === 'string' && rawMessage.trim()
      ? rawMessage.trim()
      : 'Không thể xử lý yêu cầu. Vui lòng thử lại.'

  if ([502, 503, 504].includes(status) || /status code (502|503|504)/i.test(message)) {
    return {
      status,
      message:
        'Máy chủ đang tạm thời không phản hồi. Nội dung hiện tại vẫn được giữ; vui lòng thử lại sau giây lát.',
    }
  }

  return {
    status,
    message,
  }
}

function toScorecard(result: PracticeAnswerDetail): ScorecardPayload {
  return {
    submissionId: result.answerId,
    totalScore: result.totalScore ?? 0,
    criteriaScores: result.criteriaScores.map((criterion) => ({
      criterionId: criterion.criterionId,
      criterionName: criterion.criterionName,
      score: criterion.score,
      comment: criterion.comment ?? 'Không có nhận xét bổ sung.',
    })),
    feedback: result.feedback ?? 'Hệ thống chưa trả về nhận xét tổng thể.',
    needsFollowUp: result.needsFollowUp,
    followUpPrompt: result.followUpPrompt,
  }
}

function toFullSessionScorecard(
  sessionId: string,
  questions: PracticeQuestion[],
  results: PracticeAnswerDetail[],
): ScorecardPayload {
  const gradedResults = questions
    .map((question, index) => ({
      questionNumber: index + 1,
      result: results.find((result) => result.questionId === question.questionId),
    }))
    .filter(
      (item): item is { questionNumber: number; result: PracticeAnswerDetail } =>
        Boolean(item.result && item.result.status === 'graded' && item.result.totalScore !== null),
    )
  const totalScore =
    gradedResults.reduce((sum, item) => sum + (item.result.totalScore ?? 0), 0) /
    gradedResults.length

  const criteriaScores = gradedResults.flatMap(({ questionNumber, result }) =>
    result.criteriaScores.map((criterion, criterionIndex) => ({
      criterionId: `${result.questionId}-${criterion.criterionId}-${criterionIndex}`,
      criterionName: criterion.criterionName,
      score: criterion.score,
      comment: criterion.comment ?? 'Không có nhận xét bổ sung.',
      questionNumber,
    })),
  )

  return {
    submissionId: sessionId,
    totalScore,
    criteriaScores,
    feedback: gradedResults
      .map(({ questionNumber, result }) => `Câu ${questionNumber}: ${result.feedback ?? 'Không có nhận xét.'}`)
      .join('\n'),
  }
}

function mergeResults(...collections: PracticeAnswerDetail[][]) {
  const resultMap = new Map<string, PracticeAnswerDetail>()
  for (const collection of collections) {
    for (const result of collection) resultMap.set(result.answerId, result)
  }
  return [...resultMap.values()]
}

function compareSubmittedAtDescending(left: PracticeAnswerDetail, right: PracticeAnswerDetail) {
  return Date.parse(right.submittedAt) - Date.parse(left.submittedAt)
}

function matchesPendingSubmission(
  answer: PracticeAnswerDetail,
  pendingSubmission: PendingPracticeSubmission,
) {
  if (pendingSubmission.knownAnswerIds.includes(answer.answerId)) return false

  const attemptedAt = Date.parse(pendingSubmission.attemptedAt)
  const submittedAt = Date.parse(answer.submittedAt)
  const clockSkewAllowanceMs = 5 * 60 * 1_000

  return (
    answer.questionId === pendingSubmission.questionId &&
    answer.answerText.trim() === pendingSubmission.answerText.trim() &&
    answer.isFollowUp === pendingSubmission.isFollowUp &&
    answer.parentAnswerId === pendingSubmission.parentAnswerId &&
    submittedAt >= attemptedAt - clockSkewAllowanceMs
  )
}

function findRecoveredAnswer(
  answers: PracticeAnswerDetail[],
  pendingSubmission: PendingPracticeSubmission,
) {
  return answers
    .filter((answer) => matchesPendingSubmission(answer, pendingSubmission))
    .sort(compareSubmittedAtDescending)[0]
}

function selectFullSessionTerminalResults(
  questions: PracticeQuestion[],
  results: PracticeAnswerDetail[],
  submittedAnswerIdsByQuestion: Record<string, string>,
) {
  return questions
    .map((question) => {
      const submittedAnswerId = submittedAnswerIdsByQuestion[question.questionId]
      if (submittedAnswerId) {
        return results.find(
          (result) =>
            result.answerId === submittedAnswerId &&
            !result.isFollowUp &&
            (result.status === 'graded' || result.status === 'failed'),
        )
      }

      return results
        .filter(
          (result) =>
            result.questionId === question.questionId &&
            !result.isFollowUp &&
            (result.status === 'graded' || result.status === 'failed'),
        )
        .sort(compareSubmittedAtDescending)[0]
    })
    .filter((result): result is PracticeAnswerDetail => Boolean(result))
}

function waitForRecovery(delayMs: number) {
  return new Promise<void>((resolve) => window.setTimeout(resolve, delayMs))
}

export const PracticeSessionPage: React.FC = () => {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const { user } = useAuth()
  const sessionId = searchParams.get('session')
  const [runtime, setRuntime] = useState<StoredPracticeRuntime | null>(() =>
    sessionId ? loadPracticeRuntime(sessionId) : null,
  )
  const [answerInputMode, setAnswerInputMode] = useState<AnswerInputMode>(
    () => runtime?.draftAnswerMode ?? 'voice',
  )
  const [transcript, setTranscript] = useState(() =>
    runtime?.draftAnswerMode === 'voice' ? (runtime.draftAnswerText ?? '') : '',
  )
  const [textAnswer, setTextAnswer] = useState(() =>
    runtime?.draftAnswerMode === 'text' ? (runtime.draftAnswerText ?? '') : '',
  )
  const [showBuffer, setShowBuffer] = useState(
    () => runtime?.showBuffer === true && runtime?.draftAnswerMode !== 'text',
  )
  const [bufferDeadlineTimestamp, setBufferDeadlineTimestamp] = useState<number | null>(
    () => runtime?.bufferDeadlineTimestamp ?? null,
  )
  const [bufferError, setBufferError] = useState('')
  const [pageError, setPageError] = useState('')
  const [sessionNotice, setSessionNotice] = useState('')
  const [isTranscribing, setIsTranscribing] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [isCompleting, setIsCompleting] = useState(false)
  const [isManualSyncing, setIsManualSyncing] = useState(false)
  const [showStopConfirmation, setShowStopConfirmation] = useState(false)
  const [showSettings, setShowSettings] = useState(false)
  const [fullSessionSubmitConfirmation, setFullSessionSubmitConfirmation] = useState<{
    answerText: string
    unansweredCount: number
  } | null>(null)
  const [scorecard, setScorecard] = useState<ScorecardPayload | null>(null)
  const [scorecardTitle, setScorecardTitle] = useState('Bảng điểm câu trả lời')
  const [latestResult, setLatestResult] = useState<PracticeAnswerDetail | null>(null)
  const [syncedResults, setSyncedResults] = useState<PracticeAnswerDetail[]>([])
  const processedAnswerIds = useRef(new Set<string>())
  const transientDraftsRef = useRef<Record<string, string>>({})
  const isTransitioningRef = useRef(false)
  const syncInFlightRef = useRef(false)
  const submissionRecoveryInFlightRef = useRef(false)
  const submissionRequestInFlightRef = useRef(false)
  const {
    isRecording,
    error: recorderError,
    startRecording,
    stopRecording,
    cancelRecording,
    clearError: clearRecorderError,
  } = useAudioRecorder()
  const {
    startListening: startClientTranscription,
    stopListening: stopClientTranscription,
    resetTranscript: resetClientTranscript,
    getCurrentTranscript,
  } = useSpeechRecognition()
  const {
    status: hubStatus,
    isConnected: isHubConnected,
    gradingResults,
    gradingErrors,
    reconnectVersion,
  } = useSignalR(sessionId)

  const updateRuntime = useCallback(
    (updater: (current: StoredPracticeRuntime) => StoredPracticeRuntime) => {
      setRuntime((current) => {
        if (!current) return current
        const next = updater(current)
        savePracticeRuntime(next)
        return next
      })
    },
    [],
  )

  const persistAnswerDraft = useCallback(
    (text: string, inputMode: AnswerInputMode, bufferVisible = showBuffer, deadline = bufferDeadlineTimestamp) => {
      updateRuntime((current) => ({
        ...current,
        draftAnswerText: text,
        draftAnswerMode: inputMode,
        showBuffer: bufferVisible,
        bufferDeadlineTimestamp: deadline,
      }))
    },
    [bufferDeadlineTimestamp, showBuffer, updateRuntime],
  )

  const mode = runtime?.config.mode ?? null
  const currentQuestion = runtime?.questions[runtime.currentQuestionIndex] ?? null
  const currentDifficulty = currentQuestion
    ? runtime?.questionDifficulties[currentQuestion.questionId]
    : undefined
  const activePrompt = runtime?.activeFollowUp?.prompt ?? currentQuestion?.content ?? ''
  const hasQuestionImage = Boolean(currentQuestion?.imageUrl?.trim())
  const isTextLayout = answerInputMode === 'text'
  const isImageTextLayout = hasQuestionImage && isTextLayout
  const currentQuestionNumber = (runtime?.currentQuestionIndex ?? 0) + 1
  const allResults = useMemo(
    () => mergeResults(syncedResults, gradingResults),
    [gradingResults, syncedResults],
  )
  const runtimeIssue = !sessionId
    ? 'Đường dẫn phiên luyện tập thiếu mã phiên.'
    : !runtime
      ? 'Không tìm thấy cấu hình phiên trong trình duyệt. Hãy quay lại và tạo phiên luyện tập mới.'
      : !user
        ? 'Không tìm thấy tài khoản sinh viên đang đăng nhập.'
        : runtime.studentId !== user.id
          ? 'Phiên luyện tập này thuộc về một tài khoản sinh viên khác.'
          : ''

  const synchronizeSession = useCallback(
    async (notifyUser = false) => {
      if (!sessionId || !runtime) return
      if (syncInFlightRef.current) {
        if (notifyUser) {
          setSessionNotice('Hệ thống đang đồng bộ trạng thái chấm. Vui lòng đợi trong giây lát.')
        }
        return
      }
      syncInFlightRef.current = true
      if (notifyUser) setIsManualSyncing(true)
      try {
        if (notifyUser && runtime.fullSessionSubmitted && user) {
          await practiceService.completeSession(sessionId, user.id)
        }
        const session = await practiceService.getSession(sessionId)
        setSyncedResults(session.answers)
        if (notifyUser) {
          setPageError('')
          setSessionNotice('Đã đồng bộ trạng thái chấm mới nhất từ máy chủ.')
        }
      } catch (syncError) {
        const problem = getErrorInfo(syncError)
        if (problem.status === 404 || problem.status === 410 || notifyUser) {
          setPageError(problem.message)
        }
      } finally {
        syncInFlightRef.current = false
        if (notifyUser) setIsManualSyncing(false)
      }
    },
    [runtime, sessionId, user],
  )

  useEffect(() => {
    if (!runtime?.pendingAnswerId && !runtime?.fullSessionSubmitted) return
    let cancelled = false
    let pollTimer: number | undefined

    const poll = async () => {
      await synchronizeSession()
      if (!cancelled) pollTimer = window.setTimeout(() => void poll(), 3_000)
    }

    pollTimer = window.setTimeout(() => void poll(), 0)
    return () => {
      cancelled = true
      if (pollTimer !== undefined) window.clearTimeout(pollTimer)
    }
  }, [runtime?.fullSessionSubmitted, runtime?.pendingAnswerId, synchronizeSession])

  useEffect(() => {
    if (reconnectVersion <= 0) return
    const syncTimer = window.setTimeout(() => void synchronizeSession(), 0)
    return () => window.clearTimeout(syncTimer)
  }, [reconnectVersion, synchronizeSession])

  useEffect(() => {
    if (!runtime?.resolvedAnswer || mode !== 'PER_QUESTION') return
    const result = runtime.resolvedAnswer
    processedAnswerIds.current.add(result.answerId)
    const restoreTimer = window.setTimeout(() => {
      setLatestResult(result)
      setScorecard(toScorecard(result))
      setScorecardTitle(result.isFollowUp ? 'Bảng điểm câu hỏi đào sâu' : 'Bảng điểm câu trả lời')
      setIsSubmitting(false)
      setPageError('')
    }, 0)
    return () => window.clearTimeout(restoreTimer)
  }, [mode, runtime?.resolvedAnswer])

  useEffect(() => {
    if (!runtime || mode !== 'PER_QUESTION' || !runtime.pendingAnswerId) return
    const liveResult = gradingResults.find((item) => item.answerId === runtime.pendingAnswerId)
    const result =
      liveResult ?? allResults.find((item) => item.answerId === runtime.pendingAnswerId)
    if (!result || result.status !== 'graded' || processedAnswerIds.current.has(result.answerId)) {
      return
    }

    const isBorderlineResult =
      result.totalScore !== null && result.totalScore >= 4 && result.totalScore <= 8
    if (!liveResult && isBorderlineResult) {
      const warningTimer = window.setTimeout(() => {
        setIsSubmitting(false)
        setPageError(
          'Bài đã được chấm nhưng kết nối thời gian thực bị gián đoạn nên chưa nhận được câu hỏi đào sâu. Vui lòng giữ phiên này và thử kết nối lại; không nộp lại câu trả lời.',
        )
        setSessionNotice('Câu trả lời đã được lưu an toàn. Hệ thống đang đồng bộ câu hỏi đào sâu.')
      }, 0)
      return () => window.clearTimeout(warningTimer)
    }

    const resultTimer = window.setTimeout(() => {
      if (processedAnswerIds.current.has(result.answerId)) return
      processedAnswerIds.current.add(result.answerId)
      setLatestResult(result)
      setScorecard(toScorecard(result))
      setScorecardTitle(result.isFollowUp ? 'Bảng điểm câu hỏi đào sâu' : 'Bảng điểm câu trả lời')
      setIsSubmitting(false)
      setPageError('')
      setSessionNotice('AI đã chấm xong câu trả lời.')
      updateRuntime((current) => ({ ...current, resolvedAnswer: result }))
    }, 0)
    return () => window.clearTimeout(resultTimer)
  }, [allResults, gradingResults, mode, runtime, updateRuntime])

  useEffect(() => {
    if (!runtime?.pendingAnswerId) return
    const gradingError = gradingErrors.find(
      (item) => item.answerId === runtime.pendingAnswerId || item.answerId === 'unknown',
    )
    if (!gradingError) return
    const errorTimer = window.setTimeout(() => {
      setIsSubmitting(false)
      setPageError(gradingError.message)
      setSessionNotice('Câu trả lời đã được lưu và đang chờ hệ thống xử lý lại.')
    }, 0)
    return () => window.clearTimeout(errorTimer)
  }, [gradingErrors, runtime?.pendingAnswerId])

  useEffect(() => {
    if (!runtime?.pendingAnswerId || mode !== 'PER_QUESTION') return
    const failedAnswer = syncedResults.find(
      (result) => result.answerId === runtime.pendingAnswerId && result.status === 'failed',
    )
    if (!failedAnswer) return

    const failedTimer = window.setTimeout(() => {
      setIsSubmitting(false)
      setPageError(
        'Dịch vụ AI chưa chấm được câu trả lời. Bài làm đã được lưu và đang chờ xử lý lại.',
      )
      setSessionNotice('Không nộp lại câu trả lời này để tránh tạo dữ liệu trùng lặp.')
    }, 0)
    return () => window.clearTimeout(failedTimer)
  }, [mode, runtime?.pendingAnswerId, syncedResults])

  useEffect(() => {
    if (!runtime?.fullSessionSubmitted || mode !== 'FULL_SESSION' || scorecard) return
    const terminalResults = selectFullSessionTerminalResults(
      runtime.questions,
      allResults,
      runtime.submittedAnswerIdsByQuestion ?? {},
    )
    if (terminalResults.length < runtime.questions.length) return

    const gradedResults = terminalResults.filter((result) => result.status === 'graded')
    if (gradedResults.length !== runtime.questions.length) {
      const failedTimer = window.setTimeout(
        () =>
          setPageError(
            'Một số câu đang chờ chấm lại do dịch vụ AI tạm thời quá tải. Bài làm đã được lưu an toàn.',
          ),
        0,
      )
      return () => window.clearTimeout(failedTimer)
    }

    const scorecardTimer = window.setTimeout(() => {
      setScorecard(toFullSessionScorecard(runtime.sessionId, runtime.questions, gradedResults))
      setScorecardTitle('Bảng điểm tổng kết phiên luyện trọn gói')
      setIsSubmitting(false)
      setPageError('')
      setSessionNotice('Đã chấm xong toàn bộ câu trả lời trong phiên.')
    }, 0)
    return () => window.clearTimeout(scorecardTimer)
  }, [allResults, mode, runtime, scorecard])

  useEffect(() => {
    return () => {
      stopSpeechPlayback()
    }
  }, [])

  useEffect(() => {
    if (!runtime) return
    const preventAccidentalExit = (event: BeforeUnloadEvent) => {
      event.preventDefault()
      event.returnValue = ''
    }
    window.addEventListener('beforeunload', preventAccidentalExit)
    return () => window.removeEventListener('beforeunload', preventAccidentalExit)
  }, [runtime])

  useEffect(() => {
    if (!showStopConfirmation) return
    const previouslyFocusedElement =
      document.activeElement instanceof HTMLElement ? document.activeElement : null
    const previousBodyOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = previousBodyOverflow
      previouslyFocusedElement?.focus()
    }
  }, [showStopConfirmation])

  const handleReadQuestion = () => {
    if (!activePrompt) return
    void playNaturalSpeech(
      activePrompt,
      getTtsVoiceForCourse(runtime?.config.courseCode, runtime?.config.courseName),
      0.95,
    ).catch((error: unknown) => {
      if (error instanceof Error && error.name === 'AbortError') return
      setPageError('Không thể phát câu hỏi bằng giọng AI. Vui lòng thử lại.')
    })
  }

  const handleRecording = async () => {
    if (!runtime) return
    setPageError('')
    setBufferError('')

    if (!isRecording) {
      stopSpeechPlayback()
      setTranscript('')
      resetClientTranscript()
      setBufferDeadlineTimestamp(null)
      setShowBuffer(false)
      persistAnswerDraft('', 'voice', false, null)
      clearRecorderError()
      try {
        await startRecording()
        startClientTranscription()
        setSessionNotice('Microphone đang ghi âm câu trả lời của bạn.')
      } catch (recordingError) {
        setPageError(getErrorInfo(recordingError).message)
      }
      return
    }

    setIsTranscribing(true)
    setSessionNotice('Đang chuyển giọng nói thành văn bản…')
    try {
      const audio = await stopRecording()
      stopClientTranscription()
      await new Promise<void>((resolve) => window.setTimeout(resolve, 50))
      const response = await storageService.uploadPracticeAudio(audio, {
        clientTranscript: getCurrentTranscript(),
        vocabularyHint: currentQuestion?.rubricCriteria.join(', '),
      })
      const nextDeadline = Date.now() + runtime.transcriptBufferSeconds * 1_000
      setTranscript(response.transcript)
      setBufferDeadlineTimestamp(nextDeadline)
      setShowBuffer(true)
      persistAnswerDraft(response.transcript, 'voice', true, nextDeadline)
      setSessionNotice('Đã bóc băng xong. Hãy rà soát thuật ngữ trước khi nộp.')
    } catch (uploadError) {
      setPageError(getErrorInfo(uploadError).message)
      setSessionNotice('')
    } finally {
      setIsTranscribing(false)
    }
  }

  const openStopConfirmation = () => {
    if (runtime?.pendingSubmission) {
      setSessionNotice(
        'Câu trả lời đang được xác minh. Hãy hoàn tất xác minh trước khi kết thúc phiên.',
      )
      return
    }
    setShowStopConfirmation(true)
  }

  const applyConfirmedSubmission = useCallback(
    async (
      runtimeAtSubmission: StoredPracticeRuntime,
      pendingSubmission: PendingPracticeSubmission,
      answerId: string,
    ) => {
      const questionIndex = runtimeAtSubmission.questions.findIndex(
        (question) => question.questionId === pendingSubmission.questionId,
      )
      if (questionIndex < 0) {
        setIsSubmitting(false)
        setPageError('Không thể đối chiếu câu trả lời với câu hỏi trong phiên hiện tại.')
        return
      }

      if (runtimeAtSubmission.config.mode === 'FULL_SESSION') {
        const currentDraft = {
          questionId: pendingSubmission.questionId,
          answerText: pendingSubmission.answerText,
          isFollowUp: false,
          parentAnswerId: null,
        }
        const drafts = [
          ...runtimeAtSubmission.drafts.filter(
            (answer) => answer.questionId !== pendingSubmission.questionId,
          ),
          currentDraft,
        ]
        const isLastQuestion = questionIndex >= runtimeAtSubmission.questions.length - 1

        updateRuntime((current) => ({
          ...current,
          drafts: [
            ...current.drafts.filter(
              (answer) => answer.questionId !== pendingSubmission.questionId,
            ),
            currentDraft,
          ],
          currentQuestionIndex: isLastQuestion
            ? current.currentQuestionIndex
            : Math.max(current.currentQuestionIndex, questionIndex + 1),
          mainQuestionsCompleted: Math.max(current.mainQuestionsCompleted, questionIndex + 1),
          pendingSubmission: null,
          draftAnswerText: '',
          showBuffer: false,
          bufferDeadlineTimestamp: null,
          submittedAnswerIdsByQuestion: {
            ...(current.submittedAnswerIdsByQuestion ?? {}),
            [pendingSubmission.questionId]: answerId,
          },
          fullSessionSubmitted: isLastQuestion || current.fullSessionSubmitted,
        }))
        setShowBuffer(false)
        setBufferDeadlineTimestamp(null)
        setTranscript('')
        setTextAnswer('')

        if (!isLastQuestion) {
          setSessionNotice(
            `Đã lưu an toàn câu ${questionIndex + 1}/${runtimeAtSubmission.questions.length} trên máy chủ. Tiếp tục câu kế tiếp.`,
          )
          setIsSubmitting(false)
          return
        }

        setSessionNotice(
          `Đã tiếp nhận ${drafts.length} câu trả lời. Đang chờ hoàn tất bảng điểm tổng kết…`,
        )

        try {
          await practiceService.completeSession(
            runtimeAtSubmission.sessionId,
            runtimeAtSubmission.studentId,
          )
        } catch (completeError) {
          let completedOnServer = false
          try {
            const session = await practiceService.getSession(runtimeAtSubmission.sessionId)
            setSyncedResults(session.answers)
            completedOnServer = session.status === 'completed' || session.status === 'abandoned'
          } catch {
            // Giữ trạng thái chưa xác nhận khi không thể đọc lại phiên từ backend.
          }
          if (!completedOnServer) {
            setPageError(getErrorInfo(completeError).message)
            setSessionNotice(
              'Các câu trả lời đã được lưu nhưng chưa xác nhận được trạng thái kết thúc phiên. Hãy dùng nút kiểm tra tiến độ để thử lại.',
            )
          }
        } finally {
          setIsSubmitting(false)
        }
        return
      }

      updateRuntime((current) => ({
        ...current,
        pendingSubmission: null,
        pendingAnswerId: answerId,
        resolvedAnswer: null,
        draftAnswerText: '',
        showBuffer: false,
        bufferDeadlineTimestamp: null,
      }))
      setShowBuffer(false)
      setBufferDeadlineTimestamp(null)
      setTranscript('')
      setTextAnswer('')
      setSessionNotice('Câu trả lời đã được lưu. Đang chờ AI chấm điểm…')
    },
    [updateRuntime],
  )

  const reconcilePendingSubmission = useCallback(
    async (
      runtimeAtSubmission: StoredPracticeRuntime,
      originalErrorMessage?: string,
    ): Promise<'recovered' | 'not_found' | 'unavailable' | 'in_progress'> => {
      const pendingSubmission = runtimeAtSubmission.pendingSubmission
      if (!pendingSubmission) return 'not_found'
      if (submissionRecoveryInFlightRef.current) return 'in_progress'

      submissionRecoveryInFlightRef.current = true
      setIsSubmitting(true)
      let latestAnswers: PracticeAnswerDetail[] | null = null
      let lastReadSucceeded = false
      let lastReadError: unknown = null

      try {
        const recoveryDelays = [0, 750, 1_500]
        for (const delayMs of recoveryDelays) {
          if (delayMs > 0) await waitForRecovery(delayMs)
          try {
            const session = await practiceService.getSession(runtimeAtSubmission.sessionId)
            latestAnswers = session.answers
            lastReadSucceeded = true
            lastReadError = null
            setSyncedResults(session.answers)

            const recoveredAnswer = findRecoveredAnswer(session.answers, pendingSubmission)
            if (recoveredAnswer) {
              await applyConfirmedSubmission(
                runtimeAtSubmission,
                pendingSubmission,
                recoveredAnswer.answerId,
              )
              setPageError('')
              setBufferError('')
              setSessionNotice(
                'Máy chủ đã lưu câu trả lời dù phản hồi xác nhận bị gián đoạn. Không cần nộp lại.',
              )
              return 'recovered'
            }
          } catch (readError) {
            lastReadSucceeded = false
            lastReadError = readError
          }
        }

        if (lastReadSucceeded && latestAnswers) {
          updateRuntime((current) =>
            current.pendingSubmission?.attemptedAt === pendingSubmission.attemptedAt
              ? { ...current, pendingSubmission: null }
              : current,
          )
          setTranscript(pendingSubmission.answerText)
          setTextAnswer(pendingSubmission.answerText)
          if (answerInputMode === 'voice') {
            setBufferDeadlineTimestamp(
              Date.now() + runtimeAtSubmission.transcriptBufferSeconds * 1_000,
            )
            setShowBuffer(true)
          } else {
            setBufferDeadlineTimestamp(null)
            setShowBuffer(false)
          }
          const message =
            originalErrorMessage ??
            'Máy chủ chưa ghi nhận câu trả lời. Nội dung đã được khôi phục để bạn nộp lại.'
          setBufferError(message)
          setPageError(message)
          setSessionNotice('Đã xác minh không có bản ghi trùng cho lần nộp vừa rồi.')
          return 'not_found'
        }

        setShowBuffer(false)
        setBufferDeadlineTimestamp(null)
        setTranscript('')
        setTextAnswer('')
        setPageError(
          lastReadError
            ? getErrorInfo(lastReadError).message
            : 'Chưa thể xác minh trạng thái câu trả lời với máy chủ.',
        )
        setSessionNotice(
          'Chưa xác định được máy chủ đã lưu câu trả lời hay chưa. Không nộp lại; hãy bấm “Xác minh lần nộp” khi kết nối ổn định.',
        )
        return 'unavailable'
      } finally {
        submissionRecoveryInFlightRef.current = false
        setIsSubmitting(false)
      }
    },
    [answerInputMode, applyConfirmedSubmission, updateRuntime],
  )

  useEffect(() => {
    if (!runtime?.pendingSubmission || submissionRequestInFlightRef.current) return
    const runtimeAtRecovery = runtime
    const recoveryTimer = window.setTimeout(() => {
      if (!submissionRequestInFlightRef.current) {
        void reconcilePendingSubmission(runtimeAtRecovery)
      }
    }, 250)
    return () => window.clearTimeout(recoveryTimer)
  }, [reconcilePendingSubmission, runtime])

  const requestFullSessionSubmitConfirmation = (answerText = currentAnswerDraft) => {
    if (
      !runtime ||
      mode !== 'FULL_SESSION' ||
      runtime.currentQuestionIndex < runtime.questions.length - 1
    ) {
      return
    }

    const normalizedAnswer = answerText.trim()
    const answeredQuestionIds = new Set(
      runtime.drafts
        .filter((answer) => answer.answerText.trim().length >= 10 && !answer.isFollowUp)
        .map((answer) => answer.questionId),
    )
    if (normalizedAnswer.length >= 10 && currentQuestion) {
      answeredQuestionIds.add(currentQuestion.questionId)
    }

    setFullSessionSubmitConfirmation({
      answerText: normalizedAnswer,
      unansweredCount: Math.max(runtime.questions.length - answeredQuestionIds.size, 0),
    })
  }

  const submitConfirmedFullSession = async () => {
    if (!runtime || !user || !fullSessionSubmitConfirmation || mode !== 'FULL_SESSION') return

    setIsSubmitting(true)
    setBufferError('')
    setPageError('')

    const answersByQuestionId = new Map(
      runtime.drafts
        .filter((answer) => !answer.isFollowUp)
        .map((answer) => [answer.questionId, answer.answerText.trim()]),
    )
    const finalAnswer = fullSessionSubmitConfirmation.answerText.trim()
    if (finalAnswer.length >= 10 && currentQuestion) {
      answersByQuestionId.set(currentQuestion.questionId, finalAnswer)
    }

    const drafts = runtime.questions.map((question) => ({
      questionId: question.questionId,
      answerText: answersByQuestionId.get(question.questionId) ?? '',
      isFollowUp: false,
      parentAnswerId: null,
    }))
    const batchRuntime: StoredPracticeRuntime = {
      ...runtime,
      drafts,
      mainQuestionsCompleted: runtime.questions.length,
      pendingSubmission: null,
      submittedAnswerIdsByQuestion: {},
      draftAnswerText: '',
      showBuffer: false,
      bufferDeadlineTimestamp: null,
    }

    savePracticeRuntime(batchRuntime)
    setRuntime(batchRuntime)
    try {
      await practiceService.submitBatchAnswers(runtime.sessionId, {
        studentId: user.id,
        answers: drafts,
      })
      updateRuntime((current) => ({
        ...current,
        drafts,
        mainQuestionsCompleted: current.questions.length,
        pendingSubmission: null,
        draftAnswerText: '',
        showBuffer: false,
        bufferDeadlineTimestamp: null,
        fullSessionSubmitted: true,
      }))
      setFullSessionSubmitConfirmation(null)
      setShowBuffer(false)
      setBufferDeadlineTimestamp(null)
      setTranscript('')
      setTextAnswer('')
      setSessionNotice(
        'Đã nộp bài. Những câu chưa trả lời sẽ được tính 0 điểm; AI đang tiến hành chấm điểm.',
      )
    } catch (batchError) {
      const problem = getErrorInfo(batchError)
      setBufferError(problem.message)
      setPageError(problem.message)
      setSessionNotice('Chưa nộp được bài. Nội dung vẫn được giữ để bạn thử lại.')
      setIsSubmitting(false)
    }
  }

  const handleConfirmSubmit = async (finalText: string) => {
    if (
      !runtime ||
      !currentQuestion ||
      !user ||
      isSubmitting ||
      submissionRequestInFlightRef.current
    ) {
      return
    }
    const normalizedAnswer = finalText.trim()
    if (
      mode === 'FULL_SESSION' &&
      runtime.currentQuestionIndex >= runtime.questions.length - 1
    ) {
      requestFullSessionSubmitConfirmation(normalizedAnswer)
      return
    }
    if (normalizedAnswer.length < 10) {
      const validationMessage = 'Câu trả lời cần ít nhất 10 ký tự trước khi nộp.'
      setBufferError(validationMessage)
      if (answerInputMode === 'text') setPageError(validationMessage)
      return
    }
    if (runtime.pendingSubmission) {
      const pendingMessage =
        'Lần nộp trước đang được xác minh. Không nộp lại để tránh tạo câu trả lời trùng.'
      setBufferError(pendingMessage)
      if (answerInputMode === 'text') setPageError(pendingMessage)
      return
    }
    if (mode === 'PER_QUESTION' && !isHubConnected) {
      const connectionMessage =
        'Chưa kết nối được máy chấm thời gian thực. Vui lòng đợi trạng thái kết nối chuyển sang màu xanh rồi nộp lại.'
      setBufferError(connectionMessage)
      if (answerInputMode === 'text') setPageError(connectionMessage)
      return
    }
    setIsSubmitting(true)
    setBufferError('')
    setPageError('')

    if (mode === 'FULL_SESSION') {
      const currentDraft = {
        questionId: currentQuestion.questionId,
        answerText: normalizedAnswer,
        isFollowUp: false,
        parentAnswerId: null,
      }
      const drafts = [
        ...runtime.drafts.filter((answer) => answer.questionId !== currentQuestion.questionId),
        currentDraft,
      ]
      const isLastQuestion = runtime.currentQuestionIndex >= runtime.questions.length - 1
      const nextIndex = Math.min(runtime.currentQuestionIndex + 1, runtime.questions.length - 1)

      if (!isLastQuestion) {
        updateRuntime((current) => ({
          ...current,
          drafts,
          currentQuestionIndex: nextIndex,
          mainQuestionsCompleted: Math.max(current.mainQuestionsCompleted, drafts.length),
          draftAnswerText: '',
          showBuffer: false,
          bufferDeadlineTimestamp: null,
        }))
        setShowBuffer(false)
        setBufferDeadlineTimestamp(null)
        setTranscript('')
        setTextAnswer('')
        setIsSubmitting(false)
        setSessionNotice(
          `Đã lưu cục bộ câu ${drafts.length}/${runtime.questions.length}. Tiếp tục câu kế tiếp.`,
        )
        return
      }

      const batchRuntime: StoredPracticeRuntime = {
        ...runtime,
        drafts,
        mainQuestionsCompleted: runtime.questions.length,
        pendingSubmission: null,
        submittedAnswerIdsByQuestion: {},
      }
      savePracticeRuntime(batchRuntime)
      setRuntime(batchRuntime)
      try {
        await practiceService.submitBatchAnswers(runtime.sessionId, {
          studentId: user.id,
          answers: drafts,
        })
        updateRuntime((current) => ({
          ...current,
          drafts,
          mainQuestionsCompleted: current.questions.length,
          pendingSubmission: null,
          draftAnswerText: '',
          showBuffer: false,
          bufferDeadlineTimestamp: null,
          fullSessionSubmitted: true,
        }))
        setShowBuffer(false)
        setBufferDeadlineTimestamp(null)
        setTranscript('')
        setTextAnswer('')
        setSessionNotice(
          `Đã nhận ${drafts.length} câu trả lời. Đang chờ hoàn tất bảng điểm tổng kết…`,
        )
      } catch (batchError) {
        const problem = getErrorInfo(batchError)
        setBufferError(problem.message)
        setPageError(problem.message)
        setSessionNotice('Chưa gửi được trọn gói. Nội dung vẫn được giữ để bạn thử lại.')
        setIsSubmitting(false)
      }
      return
    }

    const activeFollowUpAtSubmission = runtime.activeFollowUp
    const knownAnswerIds = new Set(allResults.map((answer) => answer.answerId))
    for (const answerId of Object.values(runtime.submittedAnswerIdsByQuestion ?? {})) {
      knownAnswerIds.add(answerId)
    }
    if (runtime.pendingAnswerId) knownAnswerIds.add(runtime.pendingAnswerId)
    if (runtime.resolvedAnswer) knownAnswerIds.add(runtime.resolvedAnswer.answerId)

    const pendingSubmission: PendingPracticeSubmission = {
      questionId: currentQuestion.questionId,
      answerText: normalizedAnswer,
      isFollowUp: Boolean(activeFollowUpAtSubmission),
      parentAnswerId: activeFollowUpAtSubmission?.parentAnswerId ?? null,
      attemptedAt: new Date().toISOString(),
      knownAnswerIds: [...knownAnswerIds],
    }
    const runtimeAtSubmission: StoredPracticeRuntime = {
      ...runtime,
      pendingSubmission,
    }
    savePracticeRuntime(runtimeAtSubmission)
    setRuntime(runtimeAtSubmission)
    submissionRequestInFlightRef.current = true

    try {
      const response = await practiceService.submitAnswer(runtime.sessionId, {
        questionId: currentQuestion.questionId,
        studentId: user.id,
        answerText: normalizedAnswer,
        isFollowUp: pendingSubmission.isFollowUp,
        parentAnswerId: pendingSubmission.parentAnswerId,
      })
      submissionRequestInFlightRef.current = false
      await applyConfirmedSubmission(runtimeAtSubmission, pendingSubmission, response.answerId)
    } catch (submitError) {
      submissionRequestInFlightRef.current = false
      const problem = getErrorInfo(submitError)
      if (problem.status === 410) {
        updateRuntime((current) => ({ ...current, pendingSubmission: null }))
        setBufferError(problem.message)
        setPageError(problem.message)
        setIsSubmitting(false)
        setShowBuffer(false)
        setBufferDeadlineTimestamp(null)
        setSessionNotice('Phiên đã hết hạn do không có tương tác. Hãy tạo phiên mới.')
        return
      }

      await reconcilePendingSubmission(runtimeAtSubmission, problem.message)
    }
  }

  const handleFinalFullSessionSubmit = () => {
    if (
      !runtime ||
      mode !== 'FULL_SESSION' ||
      runtime.currentQuestionIndex < runtime.questions.length - 1
    ) {
      return
    }

    requestFullSessionSubmitConfirmation()
  }

  const completeAndExit = useCallback(async () => {
    if (!runtime || !user || isCompleting) return
    if (runtime.pendingSubmission) {
      setSessionNotice(
        'Câu trả lời đang được xác minh. Hãy hoàn tất xác minh trước khi kết thúc phiên.',
      )
      setShowStopConfirmation(false)
      return
    }
    if (runtime.config.mode === 'FULL_SESSION' && runtime.fullSessionSubmitted) {
      cancelRecording()
      removePracticeRuntime(runtime.sessionId)
      navigate('/practice')
      return
    }
    setIsCompleting(true)
    setPageError('')
    try {
      await practiceService.completeSession(runtime.sessionId, user.id)
      cancelRecording()
      removePracticeRuntime(runtime.sessionId)
      navigate('/practice')
    } catch (completeError) {
      try {
        const session = await practiceService.getSession(runtime.sessionId)
        if (session.status === 'completed' || session.status === 'abandoned') {
          cancelRecording()
          removePracticeRuntime(runtime.sessionId)
          navigate('/practice')
          return
        }
      } catch {
        // Hiển thị lỗi kết thúc ban đầu nếu không thể xác minh trạng thái phiên.
      }
      setPageError(getErrorInfo(completeError).message)
      setIsCompleting(false)
      setShowStopConfirmation(false)
    }
  }, [cancelRecording, isCompleting, navigate, runtime, user])

  const moveAfterScorecard = async () => {
    if (!runtime || !latestResult || mode !== 'PER_QUESTION' || isTransitioningRef.current) {
      return
    }
    isTransitioningRef.current = true

    setPageError('')

    if (latestResult.needsFollowUp && latestResult.followUpPrompt) {
      const parentAnswerId = latestResult.isFollowUp
        ? (latestResult.parentAnswerId ?? latestResult.answerId)
        : latestResult.answerId
      updateRuntime((current) => ({
        ...current,
        activeFollowUp: {
          questionId: latestResult.questionId,
          prompt: latestResult.followUpPrompt as string,
          parentAnswerId,
        },
        pendingAnswerId: null,
        resolvedAnswer: null,
      }))
      setScorecard(null)
      setTranscript('')
      setTextAnswer('')
      setLatestResult(null)
      setSessionNotice('AI yêu cầu một câu hỏi đào sâu trước khi chuyển sang câu chính tiếp theo.')
      isTransitioningRef.current = false
      return
    }

    const completedQuestions = runtime.mainQuestionsCompleted + 1

    setIsSubmitting(true)
    try {
      const response = await practiceService.getNextQuestion(runtime.sessionId, user?.id)
      if (!response.hasMoreQuestions || !response.question) {
        setSessionNotice(response.message ?? 'Kho câu hỏi phù hợp đã hết.')
        await completeAndExit()
        return
      }

      const nextQuestion: PracticeQuestion = {
        questionId: response.question.id,
        content: response.question.content,
        imageUrl: response.question.imageUrl,
        imageAlt: response.question.imageAlt,
        rubricCriteria: response.question.rubricCriteria,
      }
      updateRuntime((current) => ({
        ...current,
        questions: [...current.questions, nextQuestion],
        currentQuestionIndex: current.questions.length,
        mainQuestionsCompleted: completedQuestions,
        activeFollowUp: null,
        pendingAnswerId: null,
        resolvedAnswer: null,
        questionDifficulties: {
          ...current.questionDifficulties,
          [nextQuestion.questionId]: response.question!.difficulty,
        },
      }))
      setScorecard(null)
      setTranscript('')
      setTextAnswer('')
      setLatestResult(null)
      setSessionNotice('Đã bốc câu hỏi tiếp theo từ ngân hàng đề luyện tập.')
    } catch (nextError) {
      const problem = getErrorInfo(nextError)
      setPageError(problem.message)
      setSessionNotice(
        problem.status === 410
          ? 'Phiên đã hết hạn. Hãy tạo phiên mới.'
          : 'Chưa thể bốc câu tiếp theo. Bảng điểm được giữ nguyên để bạn thử lại.',
      )
    } finally {
      setIsSubmitting(false)
      isTransitioningRef.current = false
    }
  }

  const handlePreviousQuestion = () => {
    if (
      !runtime ||
      !user ||
      runtime.currentQuestionIndex <= 0 ||
      isRecording ||
      isTranscribing ||
      isSubmitting ||
      showBuffer ||
      runtime.pendingSubmission ||
      runtime.pendingAnswerId ||
      runtime.fullSessionSubmitted ||
      runtime.activeFollowUp ||
      isCompleting ||
      isTransitioningRef.current
    ) {
      return
    }

    const currentQuestionId = runtime.questions[runtime.currentQuestionIndex]?.questionId
    if (currentQuestionId && currentAnswerDraft.trim()) {
      transientDraftsRef.current[currentQuestionId] = currentAnswerDraft
    }

    const previousIndex = runtime.currentQuestionIndex - 1
    const previousQuestionId = runtime.questions[previousIndex]?.questionId
    if (!previousQuestionId) return

    const nextDrafts =
      mode === 'FULL_SESSION' && currentQuestionId && currentAnswerDraft.trim()
        ? [
            ...runtime.drafts.filter((answer) => answer.questionId !== currentQuestionId),
            {
              questionId: currentQuestionId,
              answerText: currentAnswerDraft,
              isFollowUp: false,
              parentAnswerId: null,
            },
          ]
        : runtime.drafts
    const previousAnswerText = findStoredQuestionAnswer(
      { ...runtime, drafts: nextDrafts },
      previousQuestionId,
      allResults,
      transientDraftsRef.current,
    )
    const nextRuntime: StoredPracticeRuntime = {
      ...runtime,
      drafts: nextDrafts,
      currentQuestionIndex: previousIndex,
      activeFollowUp: null,
      pendingAnswerId: null,
      resolvedAnswer: null,
      draftAnswerText: previousAnswerText,
      draftAnswerMode: answerInputMode,
      showBuffer: false,
      bufferDeadlineTimestamp: null,
    }

    isTransitioningRef.current = true
    savePracticeRuntime(nextRuntime)
    setRuntime(nextRuntime)
    setShowBuffer(false)
    setBufferDeadlineTimestamp(null)
    setTranscript(answerInputMode === 'voice' ? previousAnswerText : '')
    setTextAnswer(answerInputMode === 'text' ? previousAnswerText : '')
    setScorecard(null)
    setLatestResult(null)
    setPageError('')
    setBufferError('')
    setSessionNotice(`Đã quay lại câu ${previousIndex + 1}.`)
    isTransitioningRef.current = false
  }

  const handleSkipQuestion = async () => {
    if (
      !runtime ||
      !user ||
      isRecording ||
      isSubmitting ||
      showBuffer ||
      runtime.pendingSubmission ||
      runtime.pendingAnswerId ||
      runtime.fullSessionSubmitted ||
      isTransitioningRef.current
    ) {
      return
    }

    isTransitioningRef.current = true
    setPageError('')
    setSessionNotice('')

    try {
      if (mode === 'FULL_SESSION') {
        const nextIndex = runtime.currentQuestionIndex + 1
        if (nextIndex >= runtime.questions.length) {
          await completeAndExit()
          return
        }

        updateRuntime((current) => ({
          ...current,
          currentQuestionIndex: nextIndex,
          activeFollowUp: null,
          pendingAnswerId: null,
          resolvedAnswer: null,
          draftAnswerText: '',
          showBuffer: false,
          bufferDeadlineTimestamp: null,
        }))
      } else {
        const loadedNextIndex = runtime.currentQuestionIndex + 1
        if (loadedNextIndex < runtime.questions.length) {
          const nextQuestionId = runtime.questions[loadedNextIndex]?.questionId
          const nextAnswerText = nextQuestionId
            ? findStoredQuestionAnswer(runtime, nextQuestionId, allResults, transientDraftsRef.current)
            : ''
          updateRuntime((current) => ({
            ...current,
            currentQuestionIndex: loadedNextIndex,
            activeFollowUp: null,
            pendingAnswerId: null,
            resolvedAnswer: null,
            draftAnswerText: nextAnswerText,
            draftAnswerMode: answerInputMode,
            showBuffer: false,
            bufferDeadlineTimestamp: null,
          }))
          setShowBuffer(false)
          setBufferDeadlineTimestamp(null)
          setTranscript(answerInputMode === 'voice' ? nextAnswerText : '')
          setTextAnswer(answerInputMode === 'text' ? nextAnswerText : '')
          setScorecard(null)
          setLatestResult(null)
          return
        }

        setIsSubmitting(true)
        const response = await practiceService.getNextQuestion(runtime.sessionId, user.id)
        if (!response.hasMoreQuestions || !response.question) {
          await completeAndExit()
          return
        }

        const nextQuestion: PracticeQuestion = {
          questionId: response.question.id,
          content: response.question.content,
          imageUrl: response.question.imageUrl,
          imageAlt: response.question.imageAlt,
          rubricCriteria: response.question.rubricCriteria,
        }
        updateRuntime((current) => ({
          ...current,
          questions: [...current.questions, nextQuestion],
          currentQuestionIndex: current.questions.length,
          activeFollowUp: null,
          pendingAnswerId: null,
          resolvedAnswer: null,
          draftAnswerText: '',
          showBuffer: false,
          bufferDeadlineTimestamp: null,
          questionDifficulties: {
            ...current.questionDifficulties,
            [nextQuestion.questionId]: response.question!.difficulty,
          },
        }))
      }

      setScorecard(null)
      setTranscript('')
      setTextAnswer('')
      setLatestResult(null)
    } catch (skipError) {
      setPageError(getErrorInfo(skipError).message)
    } finally {
      setIsSubmitting(false)
      isTransitioningRef.current = false
    }
  }

  if (!runtime || !sessionId || !currentQuestion || !user || runtime.studentId !== user.id) {
    return (
      <section className="mx-auto mt-12 max-w-xl overflow-hidden rounded-[22px] border border-[#efcbd1] bg-white text-center shadow-[0_18px_48px_rgba(31,61,105,0.09)]">
        <div className="bg-[linear-gradient(135deg,#fff8f9,#BDE8F5)] px-6 py-8">
          <span className="mx-auto flex size-14 items-center justify-center rounded-2xl bg-white text-[#d3485f] shadow-[0_10px_28px_rgba(211,72,95,0.12)]">
            <AlertCircle className="size-7" />
          </span>
          <div className="mt-4">
            <h1 className="text-xl font-bold text-[#0F2854]">Không thể mở phiên luyện tập</h1>
            <p className="mx-auto mt-2 max-w-md text-sm leading-relaxed text-[#68778b]">
              {pageError || runtimeIssue || 'Dữ liệu phiên luyện tập không đầy đủ hoặc đã bị xóa.'}
            </p>
          </div>
        </div>
        <div className="border-t border-[#4988C4]/45 bg-white p-4">
          <button
            type="button"
            onClick={() => navigate('/practice')}
            className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-5 text-sm font-bold text-white shadow-[0_10px_24px_rgba(36,94,234,0.2)] transition hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
          >
            <ArrowLeft className="size-4" /> Tạo phiên mới
          </button>
        </div>
      </section>
    )
  }

  const isVerifyingSubmission = Boolean(runtime.pendingSubmission)
  const isWaitingForGrade = Boolean(runtime.pendingAnswerId || runtime.fullSessionSubmitted)
  const isAnswerLocked = isVerifyingSubmission || isWaitingForGrade
  const currentAnswerDraft = answerInputMode === 'voice' ? transcript : textAnswer
  const isFinalFullSessionQuestion =
    mode === 'FULL_SESSION' && runtime.currentQuestionIndex >= runtime.questions.length - 1
  const isInputModeLocked =
    isRecording || isTranscribing || isSubmitting || isAnswerLocked || showBuffer
  const canSubmitCurrentAnswer =
    currentAnswerDraft.trim().length >= 10 &&
    !isSubmitting &&
    !isAnswerLocked &&
    (mode !== 'PER_QUESTION' || isHubConnected)
  const totalMainQuestions = mode === 'FULL_SESSION' ? runtime.questions.length : null
  const completedMainQuestions = runtime.mainQuestionsCompleted
  const progressPercentage = totalMainQuestions
    ? Math.round((completedMainQuestions / totalMainQuestions) * 100)
    : 0
  const modeLabel = mode === 'PER_QUESTION' ? 'Luyện từng câu' : 'Luyện trọn gói · Tăng dần độ khó'
  const answerState = isVerifyingSubmission
    ? 'Đang xác minh lần nộp câu trả lời'
    : isRecording
      ? 'Đang ghi câu trả lời'
      : isTranscribing
        ? 'Đang chuyển giọng nói thành văn bản'
        : isWaitingForGrade
          ? 'Câu trả lời đã được lưu · Đang chấm điểm'
          : currentAnswerDraft.trim().length >= 10
            ? 'Bản trả lời đã sẵn sàng để rà soát'
            : answerInputMode === 'voice'
              ? 'Sẵn sàng ghi câu trả lời'
              : 'Sẵn sàng nhập câu trả lời'
  const reviewHint = isVerifyingSubmission
    ? 'Chưa xác định được máy chủ đã nhận câu trả lời hay chưa. Không nộp lại để tránh trùng dữ liệu.'
    : isWaitingForGrade
      ? 'Vui lòng chờ hệ thống hoàn tất chấm điểm.'
      : mode === 'PER_QUESTION' && !isHubConnected
        ? 'Đang khôi phục kết nối với máy chấm điểm.'
        : currentAnswerDraft.trim().length < 10
          ? answerInputMode === 'voice'
            ? 'Hãy ghi âm câu trả lời trước khi chuyển sang bước rà soát.'
            : 'Câu trả lời cần ít nhất 10 ký tự trước khi chuyển sang bước rà soát.'
          : answerInputMode === 'voice'
            ? `Bạn có ${runtime.transcriptBufferSeconds} giây để hiệu đính trước khi nộp.`
            : 'Nội dung sẽ được gửi trực tiếp để AI chấm sau khi bạn xác nhận.'

  return (
    <div className="fixed inset-0 z-[70] flex items-center justify-center overflow-hidden bg-[#6f8099]/70 p-3 font-sans text-[#0F2854] sm:p-4">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="practice-session-title"
        className="flex h-[min(695px,calc(100dvh-3rem))] max-h-[calc(100dvh-3rem)] w-full max-w-7xl flex-col overflow-hidden rounded-[28px] border border-[#4988C4]/45 bg-white shadow-[0_24px_60px_rgba(16,32,61,0.22)]"
      >
      <section
        className={cn(
          ds.banner,
          'shrink-0 rounded-none border-x-0 border-t-0 px-3 py-2.5 text-white shadow-none sm:px-5 sm:py-3',
        )}
      >
        <div className="pointer-events-none absolute inset-0 [background-image:radial-gradient(rgba(255,255,255,0.35)_0.8px,transparent_0.8px)] [background-size:22px_22px] opacity-35" />
        <div className="pointer-events-none absolute -top-24 right-[-8%] size-72 rounded-full bg-[#4988C4]/16/20 blur-3xl" />
        <div className="pointer-events-none absolute -bottom-32 left-[38%] size-72 rounded-full bg-[#4988C4]/30 blur-3xl" />

        <div className="relative z-10">
          <div className="flex items-center justify-between gap-4">
            <div className="min-w-0">
              <p className="text-xs font-bold tracking-[0.08em] text-[#BDE8F5] uppercase">
                {runtime.config.courseCode}
              </p>
              <h1
                id="practice-session-title"
                className="mt-0.5 truncate text-sm font-bold text-white sm:text-base"
              >
                {runtime.config.courseName}
              </h1>
            </div>
            <div className="flex shrink-0 items-center gap-2">
              <span
                role="status"
                aria-live="polite"
                  className="hidden items-center gap-1.5 text-xs font-semibold text-white/80"
              >
                {hubStatus === 'connected' ? (
                  <Wifi className="size-3.5 text-[#BDE8F5]" />
                ) : (
                  <WifiOff className="size-3.5 text-amber-200" />
                )}
                {hubStatus === 'connected' ? 'Kết nối ổn định' : 'Đang kết nối lại…'}
              </span>
              <button
                type="button"
                aria-label="Cài đặt phiên luyện tập"
                aria-expanded={showSettings}
                onClick={() => setShowSettings((current) => !current)}
                className="flex size-9 items-center justify-center rounded-full border border-white/30 bg-white/10 text-white transition hover:bg-white/20 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
              >
                <Settings className="size-4" />
              </button>
              <button
                type="button"
                aria-label="Kết thúc phiên luyện tập"
                onClick={openStopConfirmation}
                disabled={isTranscribing || isVerifyingSubmission}
                className="flex size-9 items-center justify-center rounded-full border border-white/30 bg-white/10 text-white transition hover:bg-white/20 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white disabled:cursor-not-allowed disabled:opacity-50"
              >
                <X className="size-4" />
              </button>
            </div>
          </div>

          {showSettings && (
            <div className="mt-3 rounded-2xl border border-white/25 bg-white/12 p-3 backdrop-blur-sm">
              <div className="flex flex-wrap items-center justify-between gap-3 text-xs text-white/85">
                <span className="inline-flex items-center gap-1.5">
                  <LockKeyhole className="size-3.5 text-[#BDE8F5]" /> {modeLabel}
                </span>
                <span className="inline-flex items-center gap-1.5">
                  <Sparkles className="size-3.5 text-[#BDE8F5]" /> Buffer hiệu đính{' '}
                  {runtime.transcriptBufferSeconds} giây
                </span>
              </div>
            </div>
          )}

          <div className="mt-2 flex items-center gap-2 border-t border-white/15 pt-2">
            <span className="shrink-0 text-xs font-bold text-white/90">
              {mode === 'FULL_SESSION'
                ? `Câu ${currentQuestionNumber}/${totalMainQuestions}`
                : `Câu ${currentQuestionNumber}`}
            </span>
            <div
              className="flex h-1.5 min-w-0 flex-1 gap-1.5"
              role="progressbar"
              aria-label="Tiến độ phiên luyện tập"
              aria-valuemin={0}
              aria-valuemax={totalMainQuestions ?? undefined}
              aria-valuenow={completedMainQuestions}
            >
              {Array.from({
                length:
                  mode === 'FULL_SESSION'
                    ? Math.max(totalMainQuestions ?? 1, 1)
                    : Math.max(Math.min(currentQuestionNumber, 5), 1),
              }).map((_, index) => (
                <span
                  key={index}
                  className={cn(
                    'h-full flex-1 rounded-full transition-colors',
                    index < completedMainQuestions
                      ? 'bg-white'
                      : index === currentQuestionNumber - 1
                        ? 'bg-[#BDE8F5]'
                        : 'bg-white/25',
                  )}
                />
              ))}
            </div>
            {mode === 'FULL_SESSION' && (
              <span className="hidden shrink-0 text-xs font-bold text-white/80 sm:inline">
                {progressPercentage}%
              </span>
            )}
          </div>
        </div>
      </section>

      <main
        className={cn(
          'flex min-h-0 flex-1 flex-col gap-2 overflow-hidden bg-white px-5 py-5 sm:px-8 sm:py-7',
          hasQuestionImage && 'gap-1 px-3 py-3 sm:px-6 sm:py-4',
        )}
      >
      {(pageError || recorderError) && (
        <div
          className="flex items-start gap-3 rounded-2xl border border-[#efc4ca] bg-[#fff5f6] px-4 py-3.5 text-sm font-semibold text-[#b6384e] shadow-[0_8px_24px_rgba(182,56,78,0.05)]"
          role="alert"
        >
          <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-white text-[#d94c63] shadow-sm">
            <AlertCircle className="size-4" />
          </span>
          <span className="pt-1 leading-relaxed">{pageError || recorderError}</span>
        </div>
      )}

      {sessionNotice && (
        <div
          className="sr-only"
          role="status"
          aria-live="polite"
        >
          <span className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-white shadow-sm">
            {isSubmitting || isTranscribing || isAnswerLocked ? (
              <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
            ) : (
              <CheckCircle2 className="size-4" />
            )}
          </span>
          <span className="pt-1 leading-relaxed">{sessionNotice}</span>
        </div>
      )}

      {runtime.pendingSubmission && (
        <button
          type="button"
          onClick={() => void reconcilePendingSubmission(runtime)}
          disabled={isSubmitting}
          className="mx-auto flex min-h-11 items-center gap-2 rounded-xl border border-amber-200 bg-white px-4 text-sm font-bold text-amber-700 shadow-sm transition hover:bg-amber-50 focus-visible:outline-2 focus-visible:outline-amber-500 disabled:cursor-wait disabled:opacity-60"
        >
          <RefreshCw
            className={`size-4 ${isSubmitting ? 'animate-spin motion-reduce:animate-none' : ''}`}
          />
          {isSubmitting ? 'Đang xác minh lần nộp…' : 'Xác minh câu trả lời đã nộp'}
        </button>
      )}

      {runtime.fullSessionSubmitted && !scorecard && (
        <button
          type="button"
          onClick={() => void synchronizeSession(true)}
          disabled={isManualSyncing}
          className="mx-auto flex min-h-10 items-center gap-2 rounded-xl border border-[#4988C4]/45 bg-white px-4 text-sm font-bold text-[#1C4D8D] shadow-sm transition hover:bg-[#f1f6ff] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] disabled:cursor-wait disabled:opacity-60"
        >
          <RefreshCw
            className={cn('size-3.5', isManualSyncing && 'animate-spin motion-reduce:animate-none')}
          />
          {isManualSyncing ? 'Đang kiểm tra…' : 'Kiểm tra tiến độ chấm'}
        </button>
      )}

      <div className="mx-auto flex min-h-0 w-full max-w-6xl flex-1 flex-col gap-1">
        <div
          className={cn(
            isTextLayout
              ? 'grid min-h-0 flex-1 grid-rows-[auto_minmax(0,1fr)_auto] gap-1'
              : 'flex min-h-0 flex-1 flex-col gap-1',
          )}
        >
          <article className="shrink-0 overflow-hidden rounded-[22px] border-0 bg-white shadow-none">
            <div
              className={cn(
                'border-b border-[#4988C4]/45 px-4 py-3 sm:px-5 sm:py-4',
                hasQuestionImage &&
                  (isImageTextLayout
                    ? 'grid items-stretch gap-3 sm:gap-4 md:grid-cols-[minmax(13rem,34%)_minmax(0,1fr)]'
                    : 'grid items-stretch gap-4 sm:gap-5 md:grid-cols-[minmax(16rem,40%)_minmax(0,1fr)]'),
              )}
            >
              <QuestionImage
                imageUrl={currentQuestion.imageUrl}
                imageAlt={currentQuestion.imageAlt}
                className={cn(
                  'mt-2 max-h-[14vh] [&>div]:min-h-0 [&>div]:p-1 [&_img]:max-h-[12vh]',
                  hasQuestionImage &&
                    (isImageTextLayout
                      ? 'mt-0 h-[clamp(180px,22vh,230px)] max-h-[clamp(180px,22vh,230px)] md:h-[clamp(210px,26vh,270px)] md:max-h-[clamp(210px,26vh,270px)] p-1 [&>div]:h-full [&>div]:!min-h-0 [&>div]:p-1 [&_img]:!max-h-[clamp(11rem,26vh,18rem)]'
                      : 'mt-0 h-[clamp(180px,24vh,240px)] max-h-[clamp(180px,24vh,240px)] md:h-[clamp(230px,30vh,300px)] md:max-h-[clamp(230px,30vh,300px)] p-1 [&>div]:h-full [&>div]:!min-h-0 [&>div]:p-1 [&_img]:!max-h-[clamp(12rem,30vh,19rem)]'),
                )}
              />

              <div className={cn(hasQuestionImage && 'min-w-0')}>
                <div
                  className={cn(
                    'flex flex-wrap items-center justify-between gap-3',
                    isTextLayout && 'gap-2',
                  )}
                >
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="inline-flex items-center gap-1.5 text-sm font-semibold text-[#5b6b84]">
                      <BookOpenText className="size-4 text-[#5276bc]" />
                      {runtime.activeFollowUp
                        ? `Câu hỏi đào sâu của câu ${currentQuestionNumber}`
                        : currentDifficulty
                          ? `Luyện tập · ${DIFFICULTY_META[currentDifficulty].label}`
                          : 'Câu hỏi luyện tập'}
                    </span>
                    {runtime.activeFollowUp && (
                      <span className="rounded-lg border border-violet-200 bg-violet-50 px-2.5 py-1.5 text-sm font-semibold text-violet-700">
                        Đào sâu theo câu trả lời
                      </span>
                    )}
                  </div>
                  {currentDifficulty && (
                    <span
                      className={`rounded-full border px-2.5 py-1 text-sm font-semibold ${DIFFICULTY_META[currentDifficulty].className}`}
                    >
                      {DIFFICULTY_META[currentDifficulty].label}
                    </span>
                  )}
                </div>

                <h2
                  className={cn(
                    isTextLayout
                      ? 'mt-2 text-[clamp(17px,1.4vw,20px)] leading-[1.15]'
                      : hasQuestionImage
                        ? 'mt-3 text-[clamp(19px,1.7vw,24px)] leading-[1.25]'
                        : 'mt-3 text-[24px] leading-snug',
                    'font-semibold text-[#0F2854]',
                  )}
                >
                  {activePrompt}
                </h2>

                <button
                  type="button"
                  onClick={handleReadQuestion}
                  disabled={isRecording}
                  className={cn(
                    isTextLayout
                      ? 'mt-2 min-h-9'
                      : 'mt-3 min-h-10',
                    'inline-flex items-center justify-center gap-2 rounded-xl border border-[#4988C4] bg-white px-3 text-sm font-bold text-[#1C4D8D] transition hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-50',
                  )}
                >
                  <Volume2 className="size-4" /> Nghe đọc câu hỏi
                </button>
              </div>
            </div>

            <details className="hidden">
              <summary className="flex cursor-pointer list-none items-center gap-2 text-xs font-bold tracking-[0.05em] text-[#5276bc] uppercase">
                <ListChecks className="size-4 text-[#5276bc]" /> Tiêu chí cần thể hiện
              </summary>
              <div className="mt-3 flex flex-wrap gap-2">
                {currentQuestion.rubricCriteria.length > 0 ? (
                  currentQuestion.rubricCriteria.map((criterion, index) => (
                    <span
                      key={`${criterion}-${index}`}
                      className="inline-flex items-start gap-1.5 rounded-xl border border-[#4988C4]/45 bg-white px-3 py-2 text-xs leading-relaxed font-medium text-[#53657d] shadow-[0_2px_8px_rgba(31,61,105,0.025)]"
                    >
                      <CheckCircle2 className="mt-0.5 size-3.5 shrink-0 text-[#299b7d]" />
                      {criterion}
                    </span>
                  ))
                ) : (
                  <span className="text-sm text-[#78879a]">
                    Trả lời đầy đủ, đúng trọng tâm và trình bày mạch lạc.
                  </span>
                )}
              </div>
            </details>
          </article>

          <section
            className={cn(
              'min-h-0 flex-1 overflow-hidden rounded-[22px] border-0 bg-white shadow-none',
              isTextLayout && 'flex flex-col',
            )}
          >
            <div className="hidden flex-col gap-3 border-b border-[#4988C4]/45 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6">
              <div className="flex items-center gap-3">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-[#4988C4]/16 text-[#1C4D8D]">
                  {answerInputMode === 'voice' ? (
                    <Mic className="size-5" />
                  ) : (
                    <Keyboard className="size-5" />
                  )}
                </span>
                <div>
                  <h2 className="text-sm font-semibold text-[#0F2854]">Khu vực trả lời</h2>
                  <p className="mt-0.5 text-xs leading-relaxed text-[#7a899c]">
                    {answerInputMode === 'voice'
                      ? 'Nói tự nhiên; bạn sẽ được hiệu đính bản nhận diện trước khi nộp.'
                      : 'Nhập câu trả lời trực tiếp và kiểm tra nội dung trước khi gửi.'}
                  </p>
                </div>
              </div>
              <span className="inline-flex items-center gap-1.5 self-start rounded-full border border-[#d9e8e3] bg-[#f1faf7] px-3 py-1.5 text-xs font-bold text-[#19785f] sm:self-auto">
                <ShieldCheck className="size-3.5" />
                {answerInputMode === 'voice' ? 'Không lưu bản ghi âm' : 'Chỉ lưu nội dung văn bản'}
              </span>
            </div>

            <div
              className={cn(
                'space-y-2 p-2 sm:p-3',
                hasQuestionImage && 'space-y-0 p-1 sm:p-2',
                isTextLayout && 'flex min-h-0 flex-1 flex-col overflow-hidden',
              )}
            >
              {answerInputMode === 'voice' && (
                  <div
                    className={cn(
                      'relative overflow-hidden border-0 bg-transparent p-0 text-center',
                      hasQuestionImage &&
                        'flex flex-col items-center justify-center gap-0 text-center',
                    )}
                  >
                  <div className="hidden" />
                  <div
                    className={`hidden mx-auto size-16 items-center justify-center rounded-2xl shadow-[0_12px_30px_rgba(31,61,105,0.12)] transition ${
                      isRecording
                        ? 'bg-[#e94f68] text-white shadow-[0_12px_34px_rgba(233,79,104,0.28)]'
                        : 'bg-white text-[#1C4D8D]'
                    }`}
                  >
                    {isTranscribing || isAnswerLocked ? (
                      <LoaderCircle className="size-7 animate-spin motion-reduce:animate-none" />
                    ) : isRecording ? (
                      <MicOff className="size-7" />
                    ) : (
                      <Mic className="size-7" />
                    )}
                  </div>

                  <p
                    className={cn(
                      'mt-1 text-sm font-bold text-[#21324a]',
                      hasQuestionImage &&
                        'mt-0 text-center',
                    )}
                    aria-live="polite"
                  >
                    {answerState}
                  </p>
                  <p
                    className={cn(
                      'mx-auto mt-0.5 max-w-md text-xs leading-relaxed text-[#728197]',
                      hasQuestionImage &&
                        'mt-0 max-w-none text-center',
                    )}
                  >
                    {isVerifyingSubmission
                      ? 'Không nộp lại hoặc kết thúc phiên cho tới khi trạng thái được xác minh.'
                      : isRecording
                        ? 'Hãy giữ khoảng cách ổn định với micro và nói rõ từng ý.'
                        : isTranscribing
                          ? 'Hệ thống đang nhận diện nội dung. Vui lòng không tải lại trang.'
                          : isWaitingForGrade
                            ? 'Câu trả lời đã được khóa để tránh gửi trùng trong lúc chấm.'
                            : 'Kiểm tra môi trường yên tĩnh trước khi bắt đầu.'}
                  </p>

                  <div
                    className="hidden mt-4 h-7 items-center justify-center gap-1"
                    aria-hidden="true"
                  >
                    {[12, 20, 28, 17, 25, 14, 30, 21, 12].map((height, index) => (
                      <span
                        key={`${height}-${index}`}
                        className={`w-1 rounded-full transition-all ${
                          isRecording
                            ? 'animate-pulse bg-[#e94f68] motion-reduce:animate-none'
                            : 'bg-[#b9c9df]'
                        }`}
                        style={{
                          height: `${isRecording ? height : Math.max(5, Math.round(height / 3))}px`,
                          animationDelay: `${index * 90}ms`,
                        }}
                      />
                    ))}
                  </div>

                  <button
                    type="button"
                    onClick={() => void handleRecording()}
                    disabled={isTranscribing || isSubmitting || isAnswerLocked}
                    className={`mx-auto flex ${hasQuestionImage ? 'mt-2 size-[clamp(5.5rem,14vh,7rem)]' : 'mt-1 size-[clamp(6rem,16vh,8rem)]'} flex-col items-center justify-center gap-1 rounded-full border-2 px-3 text-center text-sm font-bold shadow-[0_14px_34px_rgba(31,61,105,0.16)] transition ${
                      isRecording
                        ? 'border-[#df405b] bg-[#df405b] text-white shadow-[#df405b]/20 hover:bg-[#c9344d]'
                        : 'border-[#4988C4] bg-[#F4FBFD] text-[#0F2854] shadow-[#4988C4]/20 hover:-translate-y-0.5 hover:bg-[#BDE8F5] hover:shadow-xl'
                    } focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-55 motion-reduce:transform-none`}
                  >
                    {isTranscribing ? (
                      <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                    ) : isRecording ? (
                      <Square className="size-3.5 fill-current" />
                    ) : (
                      <Mic className="size-4" />
                    )}
                    {isTranscribing
                      ? 'Đang nhận diện nội dung…'
                      : isRecording
                        ? 'Dừng ghi và tạo bản trả lời'
                        : transcript.trim()
                          ? 'Ghi lại câu trả lời'
                          : 'Bắt đầu trả lời'}
                  </button>
                </div>
              )}

              <div
                className={cn(
                  'rounded-2xl border p-3 sm:p-4',
                  answerInputMode === 'text'
                    ? 'border-[#4988C4]/65 bg-[#f8fbff] shadow-[0_8px_22px_rgba(31,61,105,0.06)]'
                    : 'border-[#4988C4]/45 bg-white',
                  answerInputMode === 'voice' && !currentAnswerDraft.trim() && 'hidden',
                  isTextLayout && 'flex min-h-0 flex-1 flex-col overflow-hidden p-2.5 sm:p-3',
                )}
              >
                <div
                  className={cn(
                    'flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between',
                    isTextLayout && 'shrink-0 gap-1',
                  )}
                >
                  <div className="flex min-w-0 items-start gap-2.5">
                    <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-[#e8f2ff] text-[#1C4D8D]">
                      {answerInputMode === 'voice' ? (
                        <Mic className="size-4" />
                      ) : (
                        <Keyboard className="size-4" />
                      )}
                    </span>
                    <div className="min-w-0">
                      <p className="text-sm font-semibold text-[#33445b]">
                        {answerInputMode === 'voice'
                          ? 'Bản trả lời nhận diện từ giọng nói'
                          : 'Nhập câu trả lời bằng text'}
                      </p>
                    <p className="mt-0.5 text-xs text-[#8390a2]">
                      {answerInputMode === 'voice'
                        ? 'Bạn có thể hiệu đính thuật ngữ kỹ thuật ở bước tiếp theo.'
                        : 'Trình bày đầy đủ ý chính bằng bàn phím; nội dung sẽ được gửi để AI chấm.'}
                    </p>
                    </div>
                  </div>
                  {currentAnswerDraft.trim() && (
                    <span className="inline-flex items-center gap-1.5 self-start rounded-full bg-[#eaf8f4] px-2.5 py-1 text-xs font-bold text-[#188267] sm:self-auto">
                      <CheckCircle2 className="size-3" />
                      {answerInputMode === 'voice'
                        ? 'Đã nhận diện'
                        : `${textAnswer.trim().length} ký tự`}
                    </span>
                  )}
                </div>
                <textarea
                  aria-label={
                    answerInputMode === 'voice'
                      ? 'Bản trả lời được nhận diện từ giọng nói'
                      : 'Nhập câu trả lời bằng văn bản'
                  }
                  className={cn(
                    'w-full rounded-xl border border-[#4988C4]/45 bg-white text-sm leading-relaxed text-[#33445b] outline-none placeholder:text-[#9aa7b8] disabled:cursor-not-allowed disabled:bg-[#f3f6fa]',
                    isTextLayout
                      ? 'mt-2 min-h-0 flex-1 resize-none overflow-y-auto p-3 focus:border-[#7fa4ff] focus:ring-3 focus:ring-[#1C4D8D]/10'
                      : 'mt-3 min-h-28 max-h-32 p-3.5',
                    answerInputMode === 'text'
                      ? 'resize-none overflow-y-auto focus:border-[#7fa4ff] focus:ring-3 focus:ring-[#1C4D8D]/10'
                      : 'resize-none',
                  )}
                  value={currentAnswerDraft}
                  onChange={(event) => {
                    if (answerInputMode !== 'text') return
                    setTextAnswer(event.target.value)
                    persistAnswerDraft(event.target.value, 'text')
                    setPageError('')
                    setBufferError('')
                  }}
                  readOnly={answerInputMode === 'voice'}
                  disabled={answerInputMode === 'text' && isInputModeLocked}
                  placeholder={
                    answerInputMode === 'voice'
                      ? 'Nội dung câu trả lời sẽ xuất hiện tại đây sau khi bạn dừng ghi âm…'
                      : 'Nhập câu trả lời của bạn tại đây…'
                  }
                />
                {answerInputMode === 'text' && (
                  <div
                    className={cn(
                      'mt-2 flex flex-wrap items-center justify-between gap-2 text-xs',
                      isTextLayout && 'mt-1 shrink-0',
                    )}
                  >
                    <span className="text-[#5b6b84]">Tối thiểu 10 ký tự để gửi câu trả lời.</span>
                    <span
                      className={
                        textAnswer.trim().length >= 10
                          ? 'font-bold text-[#188267]'
                          : 'font-semibold text-[#9b7b32]'
                      }
                    >
                      {textAnswer.trim().length}/10 ký tự tối thiểu
                    </span>
                  </div>
                )}
              </div>
            </div>

            <div
              className={cn(
                'flex flex-col gap-2 border-t border-[#4988C4]/45 bg-white px-2 py-1.5 sm:flex-row sm:items-center sm:justify-between sm:px-3',
                !currentAnswerDraft.trim() && 'hidden',
                'shrink-0',
              )}
            >
              <div className="flex min-w-0 items-start gap-2 text-xs leading-relaxed text-[#6d7d92]">
                <Sparkles className="mt-0.5 size-3.5 shrink-0 text-[#1C4D8D]" />
                <span>{reviewHint}</span>
              </div>
              <button
                type="button"
                onClick={() => {
                  if (answerInputMode === 'text') {
                    void handleConfirmSubmit(currentAnswerDraft)
                    return
                  }

                  const nextDeadline =
                    bufferDeadlineTimestamp ?? Date.now() + runtime.transcriptBufferSeconds * 1_000
                  setBufferDeadlineTimestamp(nextDeadline)
                  setShowBuffer(true)
                  persistAnswerDraft(currentAnswerDraft, 'voice', true, nextDeadline)
                }}
                disabled={!canSubmitCurrentAnswer}
                className={cn(
                  'inline-flex min-h-9 w-full shrink-0 items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-3 text-xs font-bold text-white shadow-[0_10px_24px_rgba(36,94,234,0.2)] transition hover:-translate-y-0.5 hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-45 motion-reduce:transform-none sm:w-auto',
                )}
              >
                {isSubmitting ? (
                  <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                ) : (
                  <Send className="size-4" />
                )}
                {isSubmitting
                  ? 'Đang gửi câu trả lời…'
                  : answerInputMode === 'voice'
                    ? 'Rà soát và gửi câu trả lời'
                    : 'Gửi câu trả lời bằng văn bản'}
              </button>
            </div>
          </section>

          {mode === 'PER_QUESTION' &&
            answerInputMode === 'text' &&
            !scorecard &&
            (isSubmitting || isWaitingForGrade) && (
              <div
                className="flex shrink-0 items-center justify-center gap-2 rounded-xl border border-[#c9d9f2] bg-[#f2f7ff] px-3 py-2 text-xs font-semibold text-[#1C4D8D]"
                role="status"
                aria-live="polite"
              >
                <LoaderCircle className="size-3.5 animate-spin motion-reduce:animate-none" />
                <span>Đang chờ AI chấm điểm câu trả lời của bạn…</span>
              </div>
            )}

          <div className="flex shrink-0 flex-wrap items-center justify-between gap-2">
            <button
              type="button"
              aria-label={
                answerInputMode === 'voice'
                  ? 'Chuyển sang trả lời bằng text'
                  : 'Chuyển sang trả lời bằng micro'
              }
              onClick={() => {
                const nextMode: AnswerInputMode = answerInputMode === 'voice' ? 'text' : 'voice'
                setAnswerInputMode(nextMode)
                const nextDraft = nextMode === 'voice' ? transcript : textAnswer
                persistAnswerDraft(nextDraft, nextMode)
                setPageError('')
                setBufferError('')
                setSessionNotice(
                  nextMode === 'voice'
                    ? 'Đã chuyển sang trả lời bằng giọng nói.'
                    : 'Đã chuyển sang nhập câu trả lời bằng văn bản.',
                )
              }}
              disabled={isInputModeLocked}
              className={cn(
                'inline-flex min-h-9 items-center justify-center gap-1.5 rounded-xl border border-[#9ec4ee] bg-[#f7fbff] px-2.5 text-xs font-bold text-[#1C4D8D] shadow-[0_4px_12px_rgba(31,61,105,0.06)] transition hover:bg-[#eaf4ff] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-45 sm:px-3',
              )}
            >
              {answerInputMode === 'voice' ? (
                <Keyboard className="size-3.5 shrink-0" />
              ) : (
                <Mic className="size-3.5 shrink-0" />
              )}
              {answerInputMode === 'voice'
                ? 'Chuyển sang trả lời bằng text'
                : 'Chuyển sang trả lời bằng micro'}
            </button>
            <div className="ml-auto flex items-center gap-2">
              <button
                type="button"
                aria-label="Quay lại câu trước"
                onClick={handlePreviousQuestion}
                disabled={
                  !runtime ||
                  runtime.currentQuestionIndex <= 0 ||
                  Boolean(runtime.activeFollowUp) ||
                  isRecording ||
                  isSubmitting ||
                  isTranscribing ||
                  isAnswerLocked ||
                  showBuffer ||
                  isCompleting
                }
                className={cn(
                  'inline-flex min-h-9 items-center justify-center gap-1.5 rounded-xl border border-[#9ec4ee] bg-white px-2.5 text-xs font-bold text-[#0F2854] shadow-[0_4px_12px_rgba(31,61,105,0.06)] transition hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-45 sm:px-3',
                )}
              >
                <ArrowLeft className="size-3.5" /> Quay lại
              </button>
              <button
                type="button"
                onClick={() => {
                  if (isFinalFullSessionQuestion) {
                    handleFinalFullSessionSubmit()
                  } else {
                    void handleSkipQuestion()
                  }
                }}
                disabled={
                  isRecording ||
                  isSubmitting ||
                  isTranscribing ||
                  isAnswerLocked ||
                  showBuffer ||
                  isCompleting
                }
                className={cn(
                  'inline-flex min-h-9 items-center justify-center gap-1.5 rounded-xl border border-[#9ec4ee] bg-white px-3 text-xs font-bold text-[#0F2854] shadow-[0_4px_12px_rgba(31,61,105,0.06)] transition hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-45',
                )}
              >
                {isFinalFullSessionQuestion ? 'Nộp bài' : 'Bỏ qua'}{' '}
                <ArrowLeft className="size-3.5 rotate-180" />
              </button>
            </div>
          </div>
        </div>

        <aside className="hidden">
          <section className="overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-white shadow-[0_10px_30px_rgba(31,61,105,0.055)]">
            <div className="border-b border-[#4988C4]/45 px-4 py-3.5">
              <div className="flex items-center gap-2">
                <Layers className="size-4 text-[#1C4D8D]" />
                <h2 className="text-base font-bold text-[#0F2854] sm:text-lg">Tóm tắt phiên luyện tập</h2>
              </div>
            </div>
            <div className="space-y-4 p-4">
              <div className="grid grid-cols-2 gap-2">
                <div className="rounded-xl border border-[#e2e8f1] bg-[#4988C4]/16 p-3">
                  <p className="text-xs font-semibold text-[#8190a3]">Câu hiện tại</p>
                  <p className="mt-1 text-sm font-black text-[#1C4D8D]">
                    {mode === 'PER_QUESTION'
                      ? currentQuestionNumber
                      : `${currentQuestionNumber}/${totalMainQuestions}`}
                  </p>
                </div>
                <div className="rounded-xl border border-[#e2e8f1] bg-[#4988C4]/16 p-3">
                  <p className="text-xs font-semibold text-[#8190a3]">Đã hoàn thành</p>
                  <p className="mt-1 text-sm font-black text-[#168367]">
                    {mode === 'PER_QUESTION'
                      ? completedMainQuestions
                      : `${completedMainQuestions}/${totalMainQuestions}`}
                  </p>
                </div>
              </div>

              <div>
                <p className="text-xs font-bold tracking-[0.06em] text-[#7a889b] uppercase">
                  Chế độ
                </p>
                <p className="mt-1.5 text-sm font-bold text-[#304159]">{modeLabel}</p>
                <p className="mt-1 text-xs leading-relaxed text-[#7b899c]">
                  {mode === 'PER_QUESTION'
                    ? 'Nhận phản hồi sau từng câu không giới hạn; phiên dừng khi hết câu phù hợp hoặc bạn kết thúc.'
                    : 'Hoàn thành toàn bộ câu hỏi trước khi nhận bảng điểm tổng kết.'}
                </p>
              </div>

              <div>
                <p className="text-xs font-bold tracking-[0.06em] text-[#7a889b] uppercase">
                  Mức độ trong phiên
                </p>
                <div className="mt-2 flex flex-wrap gap-1.5">
                  {runtime.config.selectedDifficulties.map((difficulty) => (
                    <span
                      key={difficulty}
                      className={`rounded-full border px-2.5 py-1 text-xs font-bold ${DIFFICULTY_META[difficulty].className}`}
                    >
                      {DIFFICULTY_META[difficulty].label}
                    </span>
                  ))}
                </div>
              </div>
            </div>
          </section>

          <section className="rounded-2xl border border-[#4988C4]/45 bg-white p-4 shadow-[0_10px_30px_rgba(31,61,105,0.05)]">
            <div className="flex items-center gap-2">
              <AudioLines className="size-4 text-[#1C4D8D]" />
              <h2 className="text-base font-bold text-[#0F2854] sm:text-lg">Ba bước trả lời</h2>
            </div>
            <ol className="mt-4 space-y-3">
              {(answerInputMode === 'voice'
                ? [
                    ['1', 'Trả lời bằng giọng nói', 'Nói rõ ràng và đúng trọng tâm.'],
                    [
                      '2',
                      'Rà soát nội dung',
                      `Hiệu đính trong ${runtime.transcriptBufferSeconds} giây.`,
                    ],
                    ['3', 'Nhận phản hồi', 'Hệ thống chấm theo tiêu chí của câu hỏi.'],
                  ]
                : [
                    ['1', 'Nhập câu trả lời', 'Soạn nội dung trực tiếp bằng bàn phím.'],
                    ['2', 'Kiểm tra và gửi', 'Đảm bảo câu trả lời đầy đủ và đúng trọng tâm.'],
                    ['3', 'Nhận phản hồi', 'Hệ thống chấm theo tiêu chí của câu hỏi.'],
                  ]
              ).map(([step, title, description]) => (
                <li key={step} className="flex gap-3">
                  <span className="flex size-6 shrink-0 items-center justify-center rounded-full border border-[#cbd9ef] bg-[#4988C4]/16 text-xs font-black text-[#1C4D8D]">
                    {step}
                  </span>
                  <div>
                    <p className="text-xs font-bold text-[#33445b]">{title}</p>
                    <p className="mt-0.5 text-xs leading-relaxed text-[#7e8c9e]">
                      {description}
                    </p>
                  </div>
                </li>
              ))}
            </ol>
          </section>

          <section className="rounded-2xl border border-[#cfe7e5] bg-[linear-gradient(145deg,#f6fcfc,#eff9f8)] p-4">
            <div className="flex gap-3">
              <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-white text-[#168c91] shadow-sm">
                {answerInputMode === 'voice' ? (
                  <Headphones className="size-4" />
                ) : (
                  <Keyboard className="size-4" />
                )}
              </span>
              <div>
                <p className="text-xs font-bold text-[#266368]">Quyền riêng tư được bảo vệ</p>
                <p className="mt-1 text-xs leading-relaxed text-[#627d82]">
                  {answerInputMode === 'voice'
                    ? 'Bản ghi chỉ dùng để nhận diện giọng nói và được xóa sau khi tạo văn bản.'
                    : 'Hệ thống chỉ lưu nội dung văn bản cuối cùng mà bạn gửi.'}
                </p>
              </div>
            </div>
          </section>

          {runtime.fullSessionSubmitted && !scorecard && (
            <button
              type="button"
              onClick={() => void synchronizeSession(true)}
              disabled={isManualSyncing}
              className="flex min-h-11 w-full items-center justify-center gap-2 rounded-xl border border-[#4988C4]/45 bg-white px-4 text-sm font-bold text-[#1C4D8D] shadow-sm transition hover:bg-[#f1f6ff] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] disabled:cursor-wait disabled:opacity-60"
            >
              <RefreshCw
                className={`size-3.5 ${isManualSyncing ? 'animate-spin motion-reduce:animate-none' : ''}`}
              />
              {isManualSyncing ? 'Đang kiểm tra…' : 'Kiểm tra tiến độ chấm'}
            </button>
          )}
        </aside>
      </div>

      </main>

      {showBuffer && answerInputMode === 'voice' && (
        <BufferScreen
          key={`${answerInputMode}-${bufferDeadlineTimestamp ?? 'practice-buffer'}`}
          initialTranscript={currentAnswerDraft}
          durationSeconds={runtime.transcriptBufferSeconds}
          deadlineTimestamp={bufferDeadlineTimestamp}
          onConfirmSubmit={(finalText) => void handleConfirmSubmit(finalText)}
          onTranscriptChange={(text) => {
            if (answerInputMode === 'voice') setTranscript(text)
            else setTextAnswer(text)
            persistAnswerDraft(text, answerInputMode, true, bufferDeadlineTimestamp)
          }}
          isSubmitting={isSubmitting}
          errorMessage={bufferError}
        />
      )}

      {scorecard && (
        <ScorecardModal
          scorecard={scorecard}
          title={scorecardTitle}
          onClose={() => {
            if (mode === 'FULL_SESSION') {
              void completeAndExit()
            } else {
              void moveAfterScorecard()
            }
          }}
          onNextQuestion={mode === 'PER_QUESTION' ? () => void moveAfterScorecard() : undefined}
          nextActionLabel={
            latestResult?.needsFollowUp
              ? 'Trả lời câu hỏi đào sâu'
              : mode === 'FULL_SESSION' &&
                  runtime.mainQuestionsCompleted + 1 >= (runtime.config.questionCount ?? 0)
                ? 'Hoàn tất phiên'
                : 'Câu chính tiếp theo'
          }
          errorMessage={pageError}
          isActionPending={isSubmitting || isCompleting}
        />
      )}

      {fullSessionSubmitConfirmation && (
        <ConfirmDialog
          tone="warning"
          icon={Send}
          eyebrow="Xác nhận nộp bài"
          title="Bạn có chắc muốn nộp bài không?"
          message={
            fullSessionSubmitConfirmation.unansweredCount > 0
              ? `Bạn còn ${fullSessionSubmitConfirmation.unansweredCount} câu chưa trả lời. Những câu này sẽ được tính 0 điểm. Nếu xác nhận, bài làm sẽ được lưu và AI bắt đầu chấm điểm.`
              : 'Bạn đã trả lời tất cả câu hỏi. Nếu xác nhận, bài làm sẽ được lưu và AI bắt đầu chấm điểm.'
          }
          confirmLabel="Xác nhận nộp bài"
          cancelLabel="Hủy và tiếp tục"
          pending={isSubmitting}
          onCancel={() => setFullSessionSubmitConfirmation(null)}
          onConfirm={() => void submitConfirmedFullSession()}
        />
      )}

      {showStopConfirmation && (
        <ConfirmDialog
          tone="danger"
          icon={Square}
          eyebrow="Xác nhận kết thúc"
          title="Kết thúc phiên luyện tập?"
          message={
            isRecording
              ? 'Bản ghi đang thực hiện sẽ bị xóa. Các câu trả lời đã nộp trước đó vẫn được lưu.'
              : mode === 'FULL_SESSION' && !runtime.fullSessionSubmitted
                ? 'Các câu đã xác nhận trước đó vẫn được lưu. Bản trả lời chưa nộp của câu hiện tại sẽ không được ghi nhận.'
                : 'Các câu đã nộp vẫn được lưu. Bản trả lời chưa nộp của câu hiện tại sẽ không được ghi nhận.'
          }
          confirmLabel={isCompleting ? 'Đang kết thúc…' : 'Kết thúc phiên'}
          cancelLabel="Tiếp tục luyện"
          pending={isCompleting || isVerifyingSubmission}
          onCancel={() => setShowStopConfirmation(false)}
          onConfirm={() => void completeAndExit()}
        />
      )}
      </div>
    </div>
  )
}
