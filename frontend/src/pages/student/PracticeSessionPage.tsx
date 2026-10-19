import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  AlertCircle,
  AudioLines,
  ArrowLeft,
  BookOpenText,
  CheckCircle2,
  Clock,
  Headphones,
  Keyboard,
  Layers,
  ListChecks,
  LoaderCircle,
  LockKeyhole,
  Mic,
  MicOff,
  Radio,
  RefreshCw,
  Send,
  ShieldCheck,
  Sparkles,
  Square,
  Volume2,
  Wifi,
  WifiOff,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { ds } from '@/design/system'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { BufferScreen } from '@/components/practice/BufferScreen'
import { ScorecardModal } from '@/components/practice/ScorecardModal'
import { useAuth } from '@/hooks/useAuth'
import { useAudioRecorder } from '@/hooks/useAudioRecorder'
import { useSignalR } from '@/hooks/useSignalR'
import { practiceService } from '@/services/practice.service'
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

function aggregateCriteria(results: PracticeAnswerDetail[]) {
  const groups = new Map<
    string,
    { criterionId: string; criterionName: string; scores: number[]; comments: string[] }
  >()

  for (const result of results) {
    for (const criterion of result.criteriaScores) {
      const current = groups.get(criterion.criterionId) ?? {
        criterionId: criterion.criterionId,
        criterionName: criterion.criterionName,
        scores: [],
        comments: [],
      }
      current.scores.push(criterion.score)
      if (criterion.comment) current.comments.push(criterion.comment)
      groups.set(criterion.criterionId, current)
    }
  }

  return [...groups.values()].map((group) => ({
    criterionId: group.criterionId,
    criterionName: group.criterionName,
    score: group.scores.reduce((sum, score) => sum + score, 0) / group.scores.length,
    comment: group.comments.join(' '),
  }))
}

function toFullSessionScorecard(
  sessionId: string,
  questions: PracticeQuestion[],
  results: PracticeAnswerDetail[],
): ScorecardPayload {
  const gradedResults = questions
    .map((question) => results.find((result) => result.questionId === question.questionId))
    .filter((result): result is PracticeAnswerDetail =>
      Boolean(result && result.status === 'graded' && result.totalScore !== null),
    )
  const totalScore =
    gradedResults.reduce((sum, result) => sum + (result.totalScore ?? 0), 0) / gradedResults.length

  return {
    submissionId: sessionId,
    totalScore,
    criteriaScores: aggregateCriteria(gradedResults),
    feedback: gradedResults
      .map((result, index) => `Câu ${index + 1}: ${result.feedback ?? 'Không có nhận xét.'}`)
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
  const [showBuffer, setShowBuffer] = useState(() => runtime?.showBuffer ?? false)
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
  const [scorecard, setScorecard] = useState<ScorecardPayload | null>(null)
  const [scorecardTitle, setScorecardTitle] = useState('Bảng điểm câu trả lời')
  const [latestResult, setLatestResult] = useState<PracticeAnswerDetail | null>(null)
  const [syncedResults, setSyncedResults] = useState<PracticeAnswerDetail[]>([])
  const processedAnswerIds = useRef(new Set<string>())
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
      if ('speechSynthesis' in window) window.speechSynthesis.cancel()
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
    if (!activePrompt || !('speechSynthesis' in window)) return
    const utterance = new SpeechSynthesisUtterance(activePrompt)
    utterance.lang = 'vi-VN'
    utterance.rate = 0.95
    window.speechSynthesis.cancel()
    window.speechSynthesis.speak(utterance)
  }

  const handleRecording = async () => {
    if (!runtime) return
    setPageError('')
    setBufferError('')

    if (!isRecording) {
      if ('speechSynthesis' in window) window.speechSynthesis.cancel()
      setTranscript('')
      setBufferDeadlineTimestamp(null)
      setShowBuffer(false)
      persistAnswerDraft('', 'voice', false, null)
      clearRecorderError()
      try {
        await startRecording()
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
      const response = await storageService.uploadPracticeAudio(audio)
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

  const completeAndExit = useCallback(async () => {
    if (!runtime || !user || isCompleting) return
    if (runtime.pendingSubmission) {
      setSessionNotice(
        'Câu trả lời đang được xác minh. Hãy hoàn tất xác minh trước khi kết thúc phiên.',
      )
      setShowStopConfirmation(false)
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
    if (completedQuestions >= runtime.config.questionCount) {
      await completeAndExit()
      isTransitioningRef.current = false
      return
    }

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

  if (!runtime || !sessionId || !currentQuestion || !user || runtime.studentId !== user.id) {
    return (
      <section className="mx-auto mt-12 max-w-xl overflow-hidden rounded-[22px] border border-[#efcbd1] bg-white text-center shadow-[0_18px_48px_rgba(31,61,105,0.09)]">
        <div className="bg-[linear-gradient(135deg,#fff8f9,#BDE8F5)] px-6 py-8">
          <span className="mx-auto flex size-14 items-center justify-center rounded-2xl bg-white text-[#d3485f] shadow-[0_10px_28px_rgba(211,72,95,0.12)]">
            <AlertCircle className="size-7" />
          </span>
          <div className="mt-4">
            <h1 className="text-lg font-bold text-[#0F2854]">Không thể mở phiên luyện tập</h1>
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
  const isInputModeLocked =
    isRecording || isTranscribing || isSubmitting || isAnswerLocked || showBuffer
  const canOpenBuffer =
    currentAnswerDraft.trim().length >= 10 &&
    !isSubmitting &&
    !isAnswerLocked &&
    (mode !== 'PER_QUESTION' || isHubConnected)
  const totalMainQuestions =
    mode === 'FULL_SESSION' ? runtime.questions.length : runtime.config.questionCount
  const completedMainQuestions = Math.min(runtime.mainQuestionsCompleted, totalMainQuestions)
  const progressPercentage =
    totalMainQuestions > 0 ? Math.round((completedMainQuestions / totalMainQuestions) * 100) : 0
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
            : 'Kiểm tra nội dung lần cuối; câu trả lời sẽ được gửi trực tiếp để chấm điểm.'

  return (
    <div className={ds.page}>
      <section className={cn(ds.banner, 'px-5 py-5 text-white sm:px-7 sm:py-6')}>
        <div className="pointer-events-none absolute inset-0 [background-image:radial-gradient(rgba(255,255,255,0.35)_0.8px,transparent_0.8px)] [background-size:22px_22px] opacity-35" />
        <div className="pointer-events-none absolute -top-24 right-[-8%] size-72 rounded-full bg-[#4988C4]/16/20 blur-3xl" />
        <div className="pointer-events-none absolute -bottom-32 left-[38%] size-72 rounded-full bg-[#4988C4]/30 blur-3xl" />

        <div className="relative z-10">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <span className="inline-flex items-center gap-1.5 rounded-full border border-white/25 bg-white/12 px-3 py-1 text-xs font-bold tracking-[0.08em] text-white uppercase backdrop-blur-sm">
                  <Radio className="size-3" /> Phiên luyện tập đang diễn ra
                </span>
                <span
                  role="status"
                  aria-live="polite"
                  className={`inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-semibold backdrop-blur-sm ${
                    hubStatus === 'connected'
                      ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
                      : 'border-amber-200 bg-amber-50 text-amber-700'
                  }`}
                >
                  {hubStatus === 'connected' ? (
                    <Wifi className="size-3" />
                  ) : (
                    <WifiOff className="size-3" />
                  )}
                  {hubStatus === 'connected'
                    ? 'Kết nối ổn định'
                    : hubStatus === 'reconnecting'
                      ? 'Đang kết nối lại…'
                      : hubStatus === 'offline'
                        ? 'Mất kết nối · đang thử lại…'
                        : 'Đang kết nối…'}
                </span>
              </div>

              <p className="mt-4 text-xs font-semibold tracking-[0.08em] text-[#BDE8F5] uppercase">
                {runtime.config.courseCode}
              </p>
              <h1 className="mt-1 max-w-3xl text-[clamp(1.35rem,2.5vw,2rem)] leading-tight font-bold tracking-[-0.035em] text-white">
                {runtime.config.courseName}
              </h1>
              <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-2 text-sm text-white/80">
                <span className="inline-flex items-center gap-1.5">
                  <LockKeyhole className="size-3.5 text-[#BDE8F5]" /> {modeLabel}
                </span>
                <span className="inline-flex items-center gap-1.5">
                  <Clock className="size-3.5 text-[#BDE8F5]" /> Hiệu đính trong{' '}
                  {runtime.transcriptBufferSeconds} giây
                </span>
              </div>
            </div>

            <button
              type="button"
              onClick={openStopConfirmation}
              disabled={isTranscribing || isVerifyingSubmission}
              title={
                isVerifyingSubmission ? 'Hãy xác minh lần nộp trước khi kết thúc phiên.' : undefined
              }
              className="inline-flex min-h-11 shrink-0 items-center justify-center gap-2 self-start rounded-xl border border-rose-200 bg-white px-4 text-sm font-bold text-rose-600 shadow-sm transition hover:-translate-y-0.5 hover:bg-rose-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-rose-300 disabled:cursor-not-allowed disabled:opacity-50 motion-reduce:transform-none"
            >
              <Square className="size-3.5 fill-current" /> Kết thúc phiên
            </button>
          </div>

          <div className="mt-5 rounded-2xl border border-white/30 bg-white p-3.5 shadow-sm sm:p-4">
            <div className="flex items-end justify-between gap-4">
              <div>
                <p className="text-xs font-semibold text-[#0F2854]/70">Tiến độ câu hỏi chính</p>
                <p className="mt-0.5 text-sm font-bold text-[#0F2854]">
                  Đã hoàn thành {completedMainQuestions}/{totalMainQuestions} câu
                </p>
              </div>
              <strong className="font-mono text-lg text-[#1C4D8D]">{progressPercentage}%</strong>
            </div>
            <div
              className="mt-3 h-2 overflow-hidden rounded-full bg-[#4988C4]/16"
              role="progressbar"
              aria-label="Tiến độ phiên luyện tập"
              aria-valuemin={0}
              aria-valuemax={totalMainQuestions}
              aria-valuenow={completedMainQuestions}
            >
              <div
                className="h-full rounded-full bg-[#1C4D8D] transition-[width] duration-500 motion-reduce:transition-none"
                style={{ width: `${progressPercentage}%` }}
              />
            </div>
          </div>
        </div>
      </section>

      {(pageError || recorderError) && (
        <div
          className="flex items-start gap-3 rounded-2xl border border-[#efc4ca] bg-[#fff5f6] px-4 py-3.5 text-sm font-semibold text-[#b6384e] shadow-[0_8px_24px_rgba(182,56,78,0.05)]"
          role="alert"
        >
          <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-white text-[#d94c63] shadow-sm">
            <AlertCircle className="size-4" />
          </span>
          <span className="pt-1.5 leading-relaxed">{pageError || recorderError}</span>
        </div>
      )}

      {sessionNotice && (
        <div
          className="flex items-start gap-3 rounded-2xl border border-[#cfe0fb] bg-[#4988C4]/16 px-4 py-3.5 text-sm font-semibold text-[#2859bf]"
          role="status"
          aria-live="polite"
        >
          <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-white shadow-sm">
            {isSubmitting || isTranscribing || isAnswerLocked ? (
              <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
            ) : (
              <CheckCircle2 className="size-4" />
            )}
          </span>
          <span className="pt-1.5 leading-relaxed">{sessionNotice}</span>
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

      <div className="grid items-start gap-4 xl:grid-cols-[minmax(0,1.65fr)_minmax(280px,0.72fr)]">
        <div className="min-w-0 space-y-4">
          <article className="overflow-hidden rounded-[22px] border border-[#4988C4]/45 bg-white shadow-[0_12px_36px_rgba(31,61,105,0.065)]">
            <div className="border-b border-[#4988C4]/45 px-5 py-4 sm:px-6 sm:py-5">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="inline-flex items-center gap-1.5 rounded-lg bg-[#4988C4]/16 px-2.5 py-1.5 text-xs font-bold text-[#1C4D8D]">
                    <BookOpenText className="size-3.5" />
                    {runtime.activeFollowUp
                      ? `Câu hỏi đào sâu của câu ${currentQuestionNumber}`
                      : `Câu ${currentQuestionNumber}/${totalMainQuestions}`}
                  </span>
                  {runtime.activeFollowUp && (
                    <span className="rounded-lg border border-violet-200 bg-violet-50 px-2.5 py-1.5 text-xs font-bold text-violet-700">
                      Đào sâu theo câu trả lời
                    </span>
                  )}
                </div>
                {currentDifficulty && (
                  <span
                    className={`rounded-full border px-2.5 py-1 text-xs font-bold ${DIFFICULTY_META[currentDifficulty].className}`}
                  >
                    {DIFFICULTY_META[currentDifficulty].label}
                  </span>
                )}
              </div>

              <h2 className="mt-5 text-[clamp(1.15rem,2.1vw,1.55rem)] leading-[1.45] font-bold tracking-[-0.025em] text-[#0F2854]">
                {activePrompt}
              </h2>

              <button
                type="button"
                onClick={handleReadQuestion}
                disabled={isRecording}
                className="mt-5 inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[#d8e3f3] bg-[#4988C4]/16 px-4 text-sm font-bold text-[#1C4D8D] transition hover:border-[#b7c9e4] hover:bg-[#4988C4]/18 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-50"
              >
                <Volume2 className="size-4" /> Nghe đọc câu hỏi
              </button>
            </div>

            <div className="bg-white px-5 py-4 sm:px-6">
              <div className="flex items-center gap-2 text-xs font-bold tracking-[0.05em] text-[#6d7d92] uppercase">
                <ListChecks className="size-4 text-[#5276bc]" /> Tiêu chí cần thể hiện
              </div>
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
            </div>
          </article>

          <section className="overflow-hidden rounded-[22px] border border-[#4988C4]/45 bg-white shadow-[0_12px_36px_rgba(31,61,105,0.065)]">
            <div className="flex flex-col gap-3 border-b border-[#4988C4]/45 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6">
              <div className="flex items-center gap-3">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-[#4988C4]/16 text-[#1C4D8D]">
                  {answerInputMode === 'voice' ? (
                    <Mic className="size-5" />
                  ) : (
                    <Keyboard className="size-5" />
                  )}
                </span>
                <div>
                  <h2 className="text-base font-bold text-[#0F2854] sm:text-lg">Khu vực trả lời</h2>
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

            <div className="space-y-4 p-5 sm:p-6">
              <div
                role="group"
                aria-label="Chọn hình thức trả lời"
                className="grid gap-2 rounded-2xl border border-[#dfe7f2] bg-[#4988C4]/16 p-1.5 sm:grid-cols-2"
              >
                {(
                  [
                    {
                      value: 'voice' as const,
                      label: 'Trả lời bằng giọng nói',
                      description: 'Ghi âm và bóc băng bằng Whisper',
                      icon: Mic,
                    },
                    {
                      value: 'text' as const,
                      label: 'Nhập câu trả lời',
                      description: 'Soạn nội dung trực tiếp bằng bàn phím',
                      icon: Keyboard,
                    },
                  ] satisfies Array<{
                    value: AnswerInputMode
                    label: string
                    description: string
                    icon: typeof Mic
                  }>
                ).map((option) => {
                  const OptionIcon = option.icon
                  const isSelected = answerInputMode === option.value
                  return (
                    <button
                      key={option.value}
                      type="button"
                      aria-pressed={isSelected}
                      onClick={() => {
                        setAnswerInputMode(option.value)
                        const nextDraft = option.value === 'voice' ? transcript : textAnswer
                        persistAnswerDraft(nextDraft, option.value)
                        setPageError('')
                        setBufferError('')
                        setSessionNotice(
                          option.value === 'voice'
                            ? 'Đã chọn trả lời bằng giọng nói.'
                            : 'Đã chọn nhập câu trả lời bằng văn bản.',
                        )
                      }}
                      disabled={isInputModeLocked}
                      className={`flex min-h-16 items-center gap-3 rounded-xl border px-3.5 py-3 text-left transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-60 ${
                        isSelected
                          ? 'border-[#8aabff] bg-white text-[#1C4D8D] shadow-[0_6px_18px_rgba(36,94,234,0.1)]'
                          : 'border-transparent text-[#63738a] hover:border-[#d2deef] hover:bg-white/80'
                      }`}
                    >
                      <span
                        className={`flex size-9 shrink-0 items-center justify-center rounded-lg ${
                          isSelected ? 'bg-[#4988C4]/16 text-[#1C4D8D]' : 'bg-white text-[#7b8da6]'
                        }`}
                      >
                        <OptionIcon className="size-4" />
                      </span>
                      <span className="min-w-0">
                        <span className="block text-sm font-bold">{option.label}</span>
                        <span className="mt-0.5 block text-xs leading-relaxed opacity-75">
                          {option.description}
                        </span>
                      </span>
                      <span
                        aria-hidden="true"
                        className={`ml-auto size-3.5 shrink-0 rounded-full border-2 ${
                          isSelected
                            ? 'border-[#4988C4] bg-[#1C4D8D] shadow-[inset_0_0_0_3px_white]'
                            : 'border-[#b9c7da]'
                        }`}
                      />
                    </button>
                  )
                })}
              </div>

              {answerInputMode === 'voice' && (
                <div
                  className={`relative overflow-hidden rounded-2xl border p-5 text-center transition-colors sm:p-6 ${
                    isRecording
                      ? 'border-[#f1a9b5] bg-[linear-gradient(145deg,#fff7f8,#fff1f3)]'
                      : isTranscribing || isAnswerLocked
                        ? 'border-[#cdddfa] bg-[linear-gradient(145deg,#BDE8F5,#BDE8F5)]'
                        : 'border-[#d8e4f3] bg-[linear-gradient(145deg,#f9fbff,#f3f7fc)]'
                  }`}
                >
                  <div className="pointer-events-none absolute inset-x-0 top-0 h-px bg-[linear-gradient(90deg,transparent,#6fcfe3,transparent)] opacity-60" />
                  <div
                    className={`mx-auto flex size-16 items-center justify-center rounded-2xl shadow-[0_12px_30px_rgba(31,61,105,0.12)] transition ${
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

                  <p className="mt-4 text-sm font-bold text-[#21324a]" aria-live="polite">
                    {answerState}
                  </p>
                  <p className="mx-auto mt-1 max-w-md text-xs leading-relaxed text-[#728197]">
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
                    className="mt-4 flex h-7 items-center justify-center gap-1"
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
                    className={`mt-4 inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-xl px-5 text-sm font-bold text-white shadow-lg transition sm:w-auto sm:min-w-52 ${
                      isRecording
                        ? 'bg-[#df405b] shadow-[#df405b]/20 hover:bg-[#c9344d]'
                        : 'bg-[#1C4D8D] shadow-[#4988C4]/20 hover:-translate-y-0.5 hover:bg-[#0F2854] hover:shadow-xl'
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

              <div className="rounded-2xl border border-[#4988C4]/45 bg-white p-4">
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <p className="text-sm font-bold text-[#33445b]">
                      {answerInputMode === 'voice'
                        ? 'Bản trả lời nhận diện từ giọng nói'
                        : 'Câu trả lời bằng văn bản'}
                    </p>
                    <p className="mt-0.5 text-xs text-[#8390a2]">
                      {answerInputMode === 'voice'
                        ? 'Bạn có thể hiệu đính thuật ngữ kỹ thuật ở bước tiếp theo.'
                        : 'Trình bày đầy đủ ý chính và sử dụng thuật ngữ chuyên ngành chính xác.'}
                    </p>
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
                  className={`mt-3 min-h-32 w-full rounded-xl border border-[#4988C4]/45 bg-white p-3.5 text-sm leading-relaxed text-[#33445b] outline-none placeholder:text-[#9aa7b8] disabled:cursor-not-allowed disabled:bg-[#f3f6fa] ${
                    answerInputMode === 'text'
                      ? 'resize-y focus:border-[#7fa4ff] focus:ring-3 focus:ring-[#1C4D8D]/10'
                      : 'resize-none'
                  }`}
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
                  <div className="mt-2 flex flex-wrap items-center justify-between gap-2 text-xs">
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

            <div className="flex flex-col gap-3 border-t border-[#4988C4]/45 bg-white px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6">
              <div className="flex min-w-0 items-start gap-2 text-xs leading-relaxed text-[#6d7d92]">
                <Sparkles className="mt-0.5 size-3.5 shrink-0 text-[#1C4D8D]" />
                <span>{reviewHint}</span>
              </div>
              <button
                type="button"
                onClick={() => {
                  if (answerInputMode === 'text') {
                    void handleConfirmSubmit(textAnswer)
                    return
                  }
                  const nextDeadline =
                    bufferDeadlineTimestamp ?? Date.now() + runtime.transcriptBufferSeconds * 1_000
                  setBufferDeadlineTimestamp(nextDeadline)
                  setShowBuffer(true)
                  persistAnswerDraft(transcript, 'voice', true, nextDeadline)
                }}
                disabled={!canOpenBuffer}
                className="inline-flex min-h-12 w-full shrink-0 items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-5 text-sm font-bold text-white shadow-[0_10px_24px_rgba(36,94,234,0.2)] transition hover:-translate-y-0.5 hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-45 motion-reduce:transform-none sm:w-auto"
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
        </div>

        <aside className="space-y-4 xl:sticky xl:top-24">
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
                    {currentQuestionNumber}/{totalMainQuestions}
                  </p>
                </div>
                <div className="rounded-xl border border-[#e2e8f1] bg-[#4988C4]/16 p-3">
                  <p className="text-xs font-semibold text-[#8190a3]">Đã hoàn thành</p>
                  <p className="mt-1 text-sm font-black text-[#168367]">
                    {completedMainQuestions}/{totalMainQuestions}
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
                    ? 'Nhận phản hồi sau từng câu; câu đào sâu không tính vào số câu chính.'
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

      {showBuffer && (
        <BufferScreen
          key={`${answerInputMode}-${bufferDeadlineTimestamp ?? 'practice-buffer'}`}
          initialTranscript={currentAnswerDraft}
          durationSeconds={runtime.transcriptBufferSeconds}
          deadlineTimestamp={bufferDeadlineTimestamp}
          onConfirmSubmit={(finalText) => void handleConfirmSubmit(finalText)}
          onTranscriptChange={(text) =>
            persistAnswerDraft(text, 'voice', true, bufferDeadlineTimestamp)
          }
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
              : runtime.mainQuestionsCompleted + 1 >= runtime.config.questionCount
                ? 'Hoàn tất phiên'
                : 'Câu chính tiếp theo'
          }
          errorMessage={pageError}
          isActionPending={isSubmitting || isCompleting}
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
  )
}
