import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  AlertCircle,
  ArrowLeft,
  AudioLines,
  BookOpenText,
  CheckCircle2,
  Clock,
  Keyboard,
  ListChecks,
  LoaderCircle,
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
  X,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { BufferScreen } from '@/components/practice/BufferScreen'
import { ScorecardModal } from '@/components/practice/ScorecardModal'
import { useAuth } from '@/hooks/useAuth'
import { useAudioRecorder } from '@/hooks/useAudioRecorder'
import { useSignalR } from '@/hooks/useSignalR'
import { practiceService } from '@/services/practice.service'
import {
  speechService,
  TTS_RATE_OPTIONS,
  TTS_VOICE_OPTIONS,
  type NeuralVoiceId,
} from '@/services/speech.service'
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
  easy: { label: 'Dễ', className: 'border-emerald-200 bg-emerald-50 text-[#16A34A]' },
  medium: { label: 'Trung bình', className: 'border-[#FED7AA] bg-[#FFF7ED] text-[#C2410C]' },
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
  const [speechPrefs, setSpeechPrefs] = useState(() => speechService.getPreferences())
  const [isReadingQuestion, setIsReadingQuestion] = useState(false)
  const processedAnswerIds = useRef(new Set<string>())
  const isTransitioningRef = useRef(false)
  const syncInFlightRef = useRef(false)
  const submissionRecoveryInFlightRef = useRef(false)
  const submissionRequestInFlightRef = useRef(false)
  const stopDialogRef = useRef<HTMLElement>(null)
  const isCompletingRef = useRef(isCompleting)

  const {
    isRecording,
    liveTranscript,
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
    (
      text: string,
      inputMode: AnswerInputMode,
      bufferVisible = showBuffer,
      deadline = bufferDeadlineTimestamp,
    ) => {
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
      speechService.stopSpeaking()
    }
  }, [activePrompt])

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
    isCompletingRef.current = isCompleting
  }, [isCompleting])

  useEffect(() => {
    if (!showStopConfirmation) return
    const previouslyFocusedElement =
      document.activeElement instanceof HTMLElement ? document.activeElement : null
    const previousBodyOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    stopDialogRef.current?.focus()

    const handleDialogKeyboard = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !isCompletingRef.current) {
        setShowStopConfirmation(false)
        return
      }
      if (event.key !== 'Tab') return

      const focusableElements = Array.from(
        stopDialogRef.current?.querySelectorAll<HTMLElement>(
          'button:not(:disabled), [href], [tabindex]:not([tabindex="-1"])',
        ) ?? [],
      ).filter((element) => element.getAttribute('aria-hidden') !== 'true')

      if (focusableElements.length === 0) {
        event.preventDefault()
        stopDialogRef.current?.focus()
        return
      }

      const firstElement = focusableElements[0]
      const lastElement = focusableElements[focusableElements.length - 1]
      if (event.shiftKey && document.activeElement === firstElement) {
        event.preventDefault()
        lastElement.focus()
      } else if (!event.shiftKey && document.activeElement === lastElement) {
        event.preventDefault()
        firstElement.focus()
      }
    }
    window.addEventListener('keydown', handleDialogKeyboard)
    return () => {
      window.removeEventListener('keydown', handleDialogKeyboard)
      document.body.style.overflow = previousBodyOverflow
      previouslyFocusedElement?.focus()
    }
  }, [showStopConfirmation])

  const handleReadQuestion = () => {
    if (!activePrompt) return
    if (isReadingQuestion) {
      speechService.stopSpeaking()
      setIsReadingQuestion(false)
      return
    }

    setIsReadingQuestion(true)
    void speechService.speakText(activePrompt, {
      voiceName: speechPrefs.voiceName,
      speakingRate: speechPrefs.speakingRate,
      onStart: () => setIsReadingQuestion(true),
      onEnd: () => setIsReadingQuestion(false),
      onError: () => setIsReadingQuestion(false),
    })
  }

  const handleSelectVoice = (voiceName: NeuralVoiceId) => {
    const updated = speechService.savePreferences({ voiceName })
    setSpeechPrefs(updated)
    if (isReadingQuestion && activePrompt) {
      void speechService.speakText(activePrompt, {
        voiceName: updated.voiceName,
        speakingRate: updated.speakingRate,
        onStart: () => setIsReadingQuestion(true),
        onEnd: () => setIsReadingQuestion(false),
        onError: () => setIsReadingQuestion(false),
      })
    }
  }

  const handleSelectRate = (speakingRate: number) => {
    const updated = speechService.savePreferences({ speakingRate })
    setSpeechPrefs(updated)
    if (isReadingQuestion && activePrompt) {
      void speechService.speakText(activePrompt, {
        voiceName: updated.voiceName,
        speakingRate: updated.speakingRate,
        onStart: () => setIsReadingQuestion(true),
        onEnd: () => setIsReadingQuestion(false),
        onError: () => setIsReadingQuestion(false),
      })
    }
  }

  const handleRecording = async () => {
    if (!runtime) return
    setPageError('')
    setBufferError('')

    if (!isRecording) {
      speechService.stopSpeaking()
      setIsReadingQuestion(false)
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
      const capturedClientTranscript =
        (audio as { clientTranscript?: string }).clientTranscript || liveTranscript
      const questionVocabularyHint = [
        runtime.config.courseCode,
        runtime.config.courseName,
        activePrompt,
        ...(currentQuestion?.rubricCriteria ?? []),
      ]
        .filter(Boolean)
        .join('. ')
      const response = await storageService.uploadPracticeAudio(
        audio,
        capturedClientTranscript,
        questionVocabularyHint,
      )
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
        speechService.stopSpeaking()
        setIsReadingQuestion(false)

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
      speechService.stopSpeaking()
      setIsReadingQuestion(false)
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
    speechService.stopSpeaking()
    setIsReadingQuestion(false)

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
      <div className="flex min-h-dvh items-center justify-center bg-[#F8FAFC] px-4 text-[#1E293B]">
        <section className="w-full max-w-lg overflow-hidden rounded-2xl border border-[#E2E8F0] bg-white text-center shadow-sm">
          <div className="bg-[#FFF7ED] px-6 py-8">
            <span className="mx-auto flex size-14 items-center justify-center rounded-2xl bg-white text-[#F26F21] shadow-xs">
              <AlertCircle className="size-7" />
            </span>
            <div className="mt-4">
              <h1 className="text-lg font-bold text-[#1E293B] sm:text-xl">
                Không thể mở phiên luyện tập
              </h1>
              <p className="mx-auto mt-2 max-w-md text-sm leading-relaxed text-[#475569]">
                {pageError || runtimeIssue || 'Dữ liệu phiên luyện tập không đầy đủ hoặc đã bị xóa.'}
              </p>
            </div>
          </div>
          <div className="border-t border-[#E2E8F0] bg-white p-4">
            <button
              type="button"
              onClick={() => navigate('/practice')}
              className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#F26F21] px-5 text-sm font-bold text-white shadow-xs transition hover:bg-[#D95B16] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21]"
            >
              <ArrowLeft className="size-4" /> Tạo phiên mới
            </button>
          </div>
        </section>
      </div>
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
  const completedMainQuestions =
    mode === 'FULL_SESSION'
      ? Math.min(runtime.mainQuestionsCompleted, totalMainQuestions)
      : runtime.mainQuestionsCompleted
  const modeLabel = mode === 'PER_QUESTION' ? 'Luyện từng câu' : 'Theo bộ câu hỏi'
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
    <div className="min-h-dvh bg-[#F8FAFC] text-[#1E293B]">
      <header className="sticky top-0 z-20 border-b border-[#E2E8F0] bg-white/95 backdrop-blur">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-3 px-4 py-3 sm:px-6">
          <div className="flex min-w-0 items-center gap-2.5">
            <button
              type="button"
              onClick={openStopConfirmation}
              disabled={isTranscribing || isVerifyingSubmission}
              className="inline-flex size-9 shrink-0 items-center justify-center rounded-xl border border-[#E2E8F0] bg-white text-[#475569] transition hover:border-[#FED7AA] hover:bg-[#FFF7ED] hover:text-[#C2410C] disabled:opacity-50"
              aria-label="Quay lại trang thiết lập luyện tập"
            >
              <ArrowLeft className="size-4" />
            </button>
            <span className="shrink-0 rounded-full border border-[#FED7AA] bg-[#FFF7ED] px-3 py-1 text-xs font-bold text-[#C2410C]">
              {runtime.config.courseCode}
            </span>
            <span className="hidden max-w-56 truncate text-sm font-bold text-[#1E293B] sm:inline md:max-w-xs">
              {runtime.config.courseName}
            </span>
          </div>

          {mode === 'PER_QUESTION' ? (
            <div
              className="order-3 flex w-full items-center justify-center gap-2.5 md:order-2 md:w-auto"
              role="status"
              aria-label="Tiến độ phiên luyện tập từng câu"
            >
              <span className="rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-1 text-xs font-bold text-emerald-700">
                Câu {currentQuestionNumber}
              </span>
              <span className="text-xs font-semibold text-[#475569]">
                Đã làm {completedMainQuestions} câu
              </span>
            </div>
          ) : (
            <div
              className="order-3 flex w-full items-center gap-3 md:order-2 md:w-auto md:max-w-md md:flex-1"
              role="progressbar"
              aria-label="Tiến độ phiên luyện tập"
              aria-valuemin={0}
              aria-valuemax={totalMainQuestions}
              aria-valuenow={completedMainQuestions}
            >
              <span className="shrink-0 text-xs font-bold text-[#1E293B]">
                Câu {currentQuestionNumber} / {totalMainQuestions}
              </span>
              <div className="flex h-2 min-w-0 flex-1 gap-1.5">
                {Array.from({ length: totalMainQuestions }).map((_, index) => {
                  const isDone = index < completedMainQuestions
                  const isCurrent = index === currentQuestionNumber - 1
                  return (
                    <span
                      key={index}
                      className={cn(
                        'h-full flex-1 rounded-full transition-colors',
                        isDone
                          ? 'bg-[#16A34A]'
                          : isCurrent
                            ? 'bg-[#F26F21]'
                            : 'bg-[#E2E8F0]',
                      )}
                    />
                  )
                })}
              </div>
              <span className="shrink-0 text-xs font-semibold text-[#475569]">
                {completedMainQuestions}/{totalMainQuestions} hoàn thành
              </span>
            </div>
          )}

          <div className="order-2 flex items-center gap-2 md:order-3">
            <span
              role="status"
              aria-live="polite"
              className={cn(
                'inline-flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-xs font-semibold',
                hubStatus === 'connected'
                  ? 'border-emerald-200 bg-emerald-50 text-[#16A34A]'
                  : 'border-amber-200 bg-amber-50 text-amber-700',
              )}
            >
              {hubStatus === 'connected' ? (
                <Wifi className="size-3.5" />
              ) : (
                <WifiOff className="size-3.5" />
              )}
              <span className="hidden sm:inline">
                {hubStatus === 'connected'
                  ? 'Kết nối ổn định'
                  : hubStatus === 'reconnecting'
                    ? 'Đang kết nối lại…'
                    : hubStatus === 'offline'
                      ? 'Mất kết nối · đang thử lại…'
                      : 'Đang kết nối…'}
              </span>
            </span>

            <span className="hidden items-center gap-1.5 rounded-full border border-[#FED7AA] bg-[#FFF7ED] px-3 py-1.5 text-xs font-semibold text-[#C2410C] lg:inline-flex">
              <Radio className="size-3.5 text-[#F26F21]" />
              {modeLabel}
            </span>

            <button
              type="button"
              onClick={openStopConfirmation}
              disabled={isTranscribing || isVerifyingSubmission}
              title={
                isVerifyingSubmission ? 'Hãy xác minh lần nộp trước khi kết thúc phiên.' : undefined
              }
              className="inline-flex min-h-9 shrink-0 items-center justify-center gap-1.5 rounded-xl border border-rose-200 bg-rose-50 px-3 text-xs font-bold text-rose-700 transition hover:bg-rose-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-rose-500 disabled:cursor-not-allowed disabled:opacity-50"
            >
              <Square className="size-3.5 fill-current" /> Kết thúc phiên
            </button>
          </div>
        </div>
      </header>

      <main className="mx-auto flex w-full max-w-7xl flex-1 flex-col px-4 py-3 sm:px-6 lg:h-[calc(100dvh-4.5rem)] lg:overflow-hidden">
        {(pageError || recorderError) && (
          <div
            className="mb-2.5 flex shrink-0 items-start gap-3 rounded-2xl border border-rose-200 bg-rose-50 px-4 py-2.5 text-xs font-semibold text-rose-700 sm:text-sm"
            role="alert"
          >
            <span className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-white text-rose-600 shadow-2xs">
              <AlertCircle className="size-4" />
            </span>
            <span className="pt-0.5 leading-relaxed">{pageError || recorderError}</span>
          </div>
        )}

        {sessionNotice && (
          <div
            className="mb-2.5 flex shrink-0 items-start gap-3 rounded-2xl border border-[#FED7AA] bg-[#FFF7ED] px-4 py-2.5 text-xs font-semibold text-[#C2410C] sm:text-sm"
            role="status"
            aria-live="polite"
          >
            <span className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-white text-[#F26F21] shadow-2xs">
              {isSubmitting || isTranscribing || isAnswerLocked ? (
                <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
              ) : (
                <CheckCircle2 className="size-4" />
              )}
            </span>
            <span className="pt-0.5 leading-relaxed">{sessionNotice}</span>
          </div>
        )}

        {runtime.pendingSubmission && (
          <div className="mb-2.5 flex shrink-0 justify-center">
            <button
              type="button"
              onClick={() => void reconcilePendingSubmission(runtime)}
              disabled={isSubmitting}
              className="inline-flex min-h-9 items-center gap-2 rounded-xl border border-amber-200 bg-white px-4 text-xs font-bold text-amber-700 shadow-xs transition hover:bg-amber-50 focus-visible:outline-2 focus-visible:outline-amber-500 disabled:cursor-wait disabled:opacity-60"
            >
              <RefreshCw
                className={`size-3.5 ${isSubmitting ? 'animate-spin motion-reduce:animate-none' : ''}`}
              />
              {isSubmitting ? 'Đang xác minh lần nộp…' : 'Xác minh câu trả lời đã nộp'}
            </button>
          </div>
        )}

        {/* Khung 2 cột Zero-Scroll: Nhìn câu hỏi bên trái, bấm trả lời bên phải ngay lập tức */}
        <div className="grid min-h-0 flex-1 grid-cols-1 gap-4 lg:grid-cols-12 lg:gap-5">
          {/* Cột trái: Thẻ Câu hỏi & Tiêu chí Rubric */}
          <article className="flex h-full flex-col overflow-hidden rounded-2xl border border-[#E2E8F0] bg-white shadow-xs lg:col-span-6 xl:col-span-7">
            <div className="shrink-0 border-b border-[#E2E8F0] bg-white px-5 py-3.5">
              <div className="flex flex-wrap items-center justify-between gap-2.5">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="inline-flex items-center gap-1.5 rounded-lg border border-[#FED7AA] bg-[#FFF7ED] px-2.5 py-1 text-xs font-bold text-[#C2410C]">
                    <BookOpenText className="size-3.5 text-[#F26F21]" />
                    {runtime.activeFollowUp
                      ? `Câu hỏi đào sâu của câu ${currentQuestionNumber}`
                      : mode === 'PER_QUESTION'
                        ? `Câu ${currentQuestionNumber}`
                        : `Câu ${currentQuestionNumber}/${totalMainQuestions}`}
                  </span>
                  {runtime.activeFollowUp && (
                    <span className="rounded-lg border border-amber-200 bg-amber-50 px-2.5 py-1 text-xs font-bold text-amber-700">
                      Đào sâu
                    </span>
                  )}
                  {currentDifficulty && (
                    <span
                      className={`rounded-full border px-2.5 py-1 text-xs font-bold ${DIFFICULTY_META[currentDifficulty].className}`}
                    >
                      {DIFFICULTY_META[currentDifficulty].label}
                    </span>
                  )}
                </div>

                <div className="flex flex-wrap items-center gap-1.5">
                  <div
                    role="group"
                    aria-label="Chọn giọng đọc AI"
                    className="inline-flex items-center rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-0.5"
                  >
                    {TTS_VOICE_OPTIONS.map((option) => {
                      const isSelected = speechPrefs.voiceName === option.voiceId
                      return (
                        <button
                          key={option.voiceId}
                          type="button"
                          aria-pressed={isSelected}
                          disabled={isRecording}
                          onClick={() => handleSelectVoice(option.voiceId)}
                          className={cn(
                            'rounded-lg px-2 py-0.5 text-xs font-bold transition focus-visible:outline-2 focus-visible:outline-[#F26F21] disabled:cursor-not-allowed disabled:opacity-50',
                            isSelected
                              ? 'bg-white text-[#C2410C] shadow-2xs ring-1 ring-[#FED7AA]'
                              : 'text-[#475569] hover:text-[#1E293B]',
                          )}
                        >
                          {option.voiceId === 'vi-VN-HoaiMyNeural' ? 'Nữ' : 'Nam'}
                        </button>
                      )
                    })}
                  </div>

                  <div
                    role="group"
                    aria-label="Chọn tốc độ đọc"
                    className="inline-flex items-center rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-0.5"
                  >
                    {TTS_RATE_OPTIONS.map((rateOption) => {
                      const isSelected = Math.abs(speechPrefs.speakingRate - rateOption.value) < 0.01
                      return (
                        <button
                          key={rateOption.value}
                          type="button"
                          aria-pressed={isSelected}
                          disabled={isRecording}
                          onClick={() => handleSelectRate(rateOption.value)}
                          className={cn(
                            'rounded-lg px-1.5 py-0.5 font-mono text-xs font-bold transition focus-visible:outline-2 focus-visible:outline-[#F26F21] disabled:cursor-not-allowed disabled:opacity-50',
                            isSelected
                              ? 'bg-white text-[#C2410C] shadow-2xs ring-1 ring-[#FED7AA]'
                              : 'text-[#475569] hover:text-[#1E293B]',
                          )}
                        >
                          {rateOption.value}x
                        </button>
                      )
                    })}
                  </div>

                  <button
                    type="button"
                    onClick={handleReadQuestion}
                    disabled={isRecording}
                    className={cn(
                      'inline-flex min-h-8 items-center justify-center gap-1.5 rounded-xl border px-3 text-xs font-bold transition focus-visible:outline-2 focus-visible:outline-[#F26F21] disabled:cursor-not-allowed disabled:opacity-50',
                      isReadingQuestion
                        ? 'border-[#F26F21] bg-[#F26F21] text-white hover:bg-[#D95B16]'
                        : 'border-[#FED7AA] bg-[#FFF7ED] text-[#C2410C] hover:bg-[#FFEDD5]',
                    )}
                  >
                    {isReadingQuestion ? (
                      <>
                        <AudioLines className="size-3.5 animate-pulse motion-reduce:animate-none" />
                        <span>Dừng đọc</span>
                      </>
                    ) : (
                      <>
                        <Volume2 className="size-3.5 text-[#F26F21]" />
                        <span>Nghe đọc</span>
                      </>
                    )}
                  </button>
                </div>
              </div>
            </div>

            <div className="flex-1 space-y-4 overflow-y-auto p-5 sm:p-6">
              <h1 className="text-lg leading-relaxed font-bold text-[#1E293B] sm:text-xl md:text-2xl">
                {activePrompt}
              </h1>

              <div className="rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3.5 sm:p-4">
                <div className="flex items-center gap-2 text-xs font-bold tracking-wider text-[#475569] uppercase">
                  <ListChecks className="size-4 text-[#F26F21]" /> Tiêu chí Rubric cần thể hiện
                </div>
                <div className="mt-2.5 space-y-2">
                  {currentQuestion.rubricCriteria.length > 0 ? (
                    currentQuestion.rubricCriteria.map((criterion, index) => (
                      <div
                        key={`${criterion}-${index}`}
                        className="flex items-start gap-2.5 rounded-xl border border-[#E2E8F0] bg-white px-3 py-2 text-xs text-[#1E293B] sm:text-sm"
                      >
                        <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-[#16A34A]" />
                        <span className="leading-relaxed">{criterion}</span>
                      </div>
                    ))
                  ) : (
                    <span className="text-xs text-[#475569] sm:text-sm">
                      Trả lời đầy đủ, đúng trọng tâm và trình bày mạch lạc theo cấu trúc Định nghĩa – Cơ
                      chế – Ví dụ.
                    </span>
                  )}
                </div>
              </div>
            </div>

            <div className="flex shrink-0 items-center justify-between border-t border-[#E2E8F0] bg-[#F8FAFC] px-5 py-2.5 text-xs text-[#64748B]">
              <span className="inline-flex items-center gap-1.5">
                <Clock className="size-3.5 text-[#F26F21]" />
                Thời gian hiệu đính: {runtime.transcriptBufferSeconds}s
              </span>
              <span>Cấu trúc: Định nghĩa &rarr; Cơ chế &rarr; Ví dụ thực tiễn</span>
            </div>
          </article>

          {/* Cột phải: Khu vực Trả lời (Voice / Text) — Trực diện trong cùng một tầm mắt */}
          <section className="flex h-full flex-col overflow-hidden rounded-2xl border border-[#E2E8F0] bg-white shadow-xs lg:col-span-6 xl:col-span-5">
            <div className="flex shrink-0 items-center justify-between border-b border-[#E2E8F0] bg-white px-4 py-3 sm:px-5">
              <div
                role="group"
                aria-label="Chọn hình thức trả lời"
                className="inline-flex rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-1"
              >
                <button
                  type="button"
                  aria-pressed={answerInputMode === 'voice'}
                  onClick={() => {
                    setAnswerInputMode('voice')
                    persistAnswerDraft(transcript, 'voice')
                    setPageError('')
                    setBufferError('')
                  }}
                  disabled={isInputModeLocked}
                  className={cn(
                    'inline-flex items-center gap-1.5 rounded-lg px-3 py-1 text-xs font-bold transition disabled:opacity-50',
                    answerInputMode === 'voice'
                      ? 'bg-white text-[#C2410C] shadow-2xs ring-1 ring-[#FED7AA]'
                      : 'text-[#475569] hover:text-[#1E293B]',
                  )}
                >
                  <Mic className="size-3.5" /> Giọng nói
                </button>
                <button
                  type="button"
                  aria-pressed={answerInputMode === 'text'}
                  onClick={() => {
                    setAnswerInputMode('text')
                    persistAnswerDraft(textAnswer, 'text')
                    setPageError('')
                    setBufferError('')
                  }}
                  disabled={isInputModeLocked}
                  className={cn(
                    'inline-flex items-center gap-1.5 rounded-lg px-3 py-1 text-xs font-bold transition disabled:opacity-50',
                    answerInputMode === 'text'
                      ? 'bg-white text-[#C2410C] shadow-2xs ring-1 ring-[#FED7AA]'
                      : 'text-[#475569] hover:text-[#1E293B]',
                  )}
                >
                  <Keyboard className="size-3.5" /> Văn bản
                </button>
              </div>

              <span className="inline-flex items-center gap-1 rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-0.5 text-xs font-bold text-[#16A34A]">
                <ShieldCheck className="size-3.5" />
                {answerInputMode === 'voice' ? 'Không lưu audio' : 'Chỉ lưu text'}
              </span>
            </div>

            <div className="flex flex-1 flex-col justify-between space-y-3 overflow-y-auto p-4 sm:p-5">
              {answerInputMode === 'voice' ? (
                <>
                  <div
                    className={cn(
                      'rounded-2xl border p-4 text-center transition-colors',
                      isRecording
                        ? 'border-rose-200 bg-rose-50/70'
                        : isTranscribing || isAnswerLocked
                          ? 'border-[#FED7AA] bg-[#FFF7ED]/70'
                          : 'border-[#E2E8F0] bg-[#F8FAFC]',
                    )}
                  >
                    <div
                      className={cn(
                        'mx-auto flex size-12 items-center justify-center rounded-2xl shadow-xs transition',
                        isRecording
                          ? 'bg-rose-600 text-white'
                          : 'border border-[#FED7AA] bg-white text-[#F26F21]',
                      )}
                    >
                      {isTranscribing || isAnswerLocked ? (
                        <LoaderCircle className="size-6 animate-spin motion-reduce:animate-none" />
                      ) : isRecording ? (
                        <MicOff className="size-6" />
                      ) : (
                        <Mic className="size-6" />
                      )}
                    </div>

                    <p className="mt-2 text-sm font-bold text-[#1E293B]" aria-live="polite">
                      {answerState}
                    </p>
                    <p className="mx-auto mt-0.5 text-xs text-[#64748B]">
                      {isRecording
                        ? 'Đang ghi âm · Trình bày rõ ràng từng ý chính'
                        : isTranscribing
                          ? 'Hệ thống đang chuyển đổi giọng nói sang văn bản…'
                          : 'Bấm nút bên dưới để bắt đầu trả lời'}
                    </p>

                    <div
                      className="mt-2 flex h-6 items-center justify-center gap-1"
                      aria-hidden="true"
                    >
                      {[10, 18, 24, 15, 22, 13, 26, 19, 10].map((height, index) => (
                        <span
                          key={`${height}-${index}`}
                          className={cn(
                            'w-1 rounded-full transition-all',
                            isRecording
                              ? 'animate-pulse bg-rose-600 motion-reduce:animate-none'
                              : 'bg-[#CBD5E1]',
                          )}
                          style={{
                            height: `${isRecording ? height : Math.max(4, Math.round(height / 3))}px`,
                            animationDelay: `${index * 90}ms`,
                          }}
                        />
                      ))}
                    </div>

                    {isRecording && liveTranscript.trim() && (
                      <div
                        className="mt-2.5 rounded-xl border border-rose-200 bg-white/90 px-3 py-2 text-left text-xs leading-relaxed text-[#1E293B] shadow-2xs"
                        aria-live="polite"
                      >
                        <span className="font-bold text-rose-700">Đang nhận diện trực tiếp: </span>
                        <span>{liveTranscript}</span>
                      </div>
                    )}

                    <button
                      type="button"
                      onClick={() => void handleRecording()}
                      disabled={isTranscribing || isSubmitting || isAnswerLocked}
                      className={cn(
                        'mt-3 inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-xl px-4 text-sm font-bold text-white shadow-xs transition',
                        isRecording
                          ? 'bg-rose-600 hover:bg-rose-700'
                          : 'bg-[#F26F21] hover:bg-[#D95B16]',
                        'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] disabled:cursor-not-allowed disabled:opacity-55',
                      )}
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

                  <div className="flex min-h-24 flex-1 flex-col rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3">
                    <div className="flex items-center justify-between gap-2">
                      <span className="text-xs font-bold text-[#1E293B]">
                        Bản trả lời nhận diện
                      </span>
                      {currentAnswerDraft.trim() && (
                        <span className="inline-flex items-center gap-1 rounded-full border border-emerald-200 bg-emerald-50 px-2 py-0.5 text-xs font-bold text-[#16A34A]">
                          <CheckCircle2 className="size-3" /> Đã nhận diện ({currentAnswerDraft.trim().length} ký tự)
                        </span>
                      )}
                    </div>
                    <textarea
                      aria-label="Bản trả lời được nhận diện từ giọng nói"
                      readOnly
                      rows={3}
                      placeholder="Nội dung câu trả lời sẽ xuất hiện tại đây sau khi bạn dừng ghi âm…"
                      value={currentAnswerDraft}
                      className="mt-2 w-full flex-1 resize-none rounded-lg border border-[#E2E8F0] bg-white p-2.5 text-xs leading-relaxed text-[#1E293B] outline-none placeholder:text-[#94A3B8]"
                    />
                  </div>
                </>
              ) : (
                <div className="flex min-h-0 flex-1 flex-col rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3.5">
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-xs font-bold text-[#1E293B]">Soạn câu trả lời văn bản</span>
                    <span
                      className={
                        textAnswer.trim().length >= 10
                          ? 'text-xs font-bold text-[#16A34A]'
                          : 'text-xs font-semibold text-amber-700'
                      }
                    >
                      {textAnswer.trim().length}/10 ký tự tối thiểu
                    </span>
                  </div>
                  <textarea
                    aria-label="Nhập câu trả lời bằng văn bản"
                    rows={8}
                    placeholder="Nhập câu trả lời của bạn tại đây…"
                    value={currentAnswerDraft}
                    onChange={(event) => {
                      setTextAnswer(event.target.value)
                      persistAnswerDraft(event.target.value, 'text')
                      setPageError('')
                      setBufferError('')
                    }}
                    disabled={isInputModeLocked}
                    className="mt-2 w-full flex-1 resize-none rounded-lg border border-[#E2E8F0] bg-white p-3 text-xs leading-relaxed text-[#1E293B] outline-none focus:border-[#F26F21] focus:ring-2 focus:ring-[#F26F21]/20 disabled:cursor-not-allowed disabled:bg-[#F8FAFC]"
                  />
                </div>
              )}
            </div>

            <div className="flex shrink-0 flex-col gap-2.5 border-t border-[#E2E8F0] bg-[#F8FAFC] px-4 py-3 sm:flex-row sm:items-center sm:justify-between sm:px-5">
              <div className="flex min-w-0 items-center gap-1.5 text-xs text-[#475569]">
                <Sparkles className="size-3.5 shrink-0 text-[#F26F21]" />
                <span className="truncate">{reviewHint}</span>
              </div>
              <div className="flex items-center gap-2">
                {runtime.fullSessionSubmitted && !scorecard && (
                  <button
                    type="button"
                    onClick={() => void synchronizeSession(true)}
                    disabled={isManualSyncing}
                    className="inline-flex min-h-10 items-center justify-center gap-1.5 rounded-xl border border-[#FED7AA] bg-[#FFF7ED] px-3 text-xs font-bold text-[#C2410C] transition hover:bg-[#FFEDD5] disabled:opacity-60"
                  >
                    <RefreshCw className={`size-3.5 ${isManualSyncing ? 'animate-spin' : ''}`} />
                    Kiểm tra tiến độ
                  </button>
                )}
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
                  className="inline-flex min-h-11 w-full shrink-0 items-center justify-center gap-2 rounded-xl bg-[#F26F21] px-5 text-sm font-bold text-white shadow-xs transition hover:bg-[#D95B16] focus-visible:outline-2 focus-visible:outline-[#F26F21] disabled:cursor-not-allowed disabled:opacity-45 sm:w-auto"
                >
                  {isSubmitting ? (
                    <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                  ) : (
                    <Send className="size-4" />
                  )}
                  {isSubmitting
                    ? 'Đang gửi…'
                    : answerInputMode === 'voice'
                      ? 'Rà soát và gửi'
                      : 'Gửi câu trả lời'}
                </button>
              </div>
            </div>
          </section>
        </div>
      </main>

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
          onClose={() => void completeAndExit()}
          onNextQuestion={mode === 'PER_QUESTION' ? () => void moveAfterScorecard() : undefined}
          nextActionLabel={
            mode === 'FULL_SESSION'
              ? 'Hoàn tất phiên'
              : latestResult?.needsFollowUp
                ? 'Trả lời câu hỏi đào sâu'
                : 'Câu chính tiếp theo'
          }
          errorMessage={pageError}
          isActionPending={isSubmitting || isCompleting}
        />
      )}

      {showStopConfirmation && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center bg-slate-900/35 p-4 backdrop-blur-xs">
          <section
            ref={stopDialogRef}
            role="dialog"
            aria-modal="true"
            aria-labelledby="stop-practice-title"
            tabIndex={-1}
            className="w-full max-w-md overflow-hidden rounded-2xl border border-[#FED7AA] border-t-4 border-t-[#F26F21] bg-white shadow-2xl outline-none"
          >
            <div className="flex items-start justify-between gap-4 border-b border-[#E2E8F0] p-5 sm:p-6">
              <div className="flex gap-3">
                <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-rose-50 text-rose-600">
                  <Square className="size-4 fill-current" />
                </span>
                <div>
                  <p className="text-xs font-bold tracking-wider text-rose-600 uppercase">
                    Xác nhận kết thúc
                  </p>
                  <h2 id="stop-practice-title" className="mt-1 text-base font-bold text-[#1E293B]">
                    Kết thúc phiên luyện tập?
                  </h2>
                  <p className="mt-2 text-xs leading-relaxed text-[#475569] sm:text-sm">
                    {isRecording
                      ? 'Bản ghi đang thực hiện sẽ bị xóa. Các câu trả lời đã nộp trước đó vẫn được lưu.'
                      : mode === 'FULL_SESSION' && !runtime.fullSessionSubmitted
                        ? 'Các câu đã xác nhận trước đó vẫn được lưu. Bản trả lời chưa nộp của câu hiện tại sẽ không được ghi nhận.'
                        : 'Các câu đã nộp vẫn được lưu. Bản trả lời chưa nộp của câu hiện tại sẽ không được ghi nhận.'}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setShowStopConfirmation(false)}
                className="flex size-9 shrink-0 items-center justify-center rounded-xl text-[#475569] transition hover:bg-[#F8FAFC] focus-visible:outline-2 focus-visible:outline-[#F26F21]"
                aria-label="Đóng xác nhận dừng"
              >
                <X className="size-4" />
              </button>
            </div>
            <div className="flex flex-col-reverse gap-2 bg-[#F8FAFC] p-4 sm:flex-row sm:justify-end">
              <button
                type="button"
                onClick={() => setShowStopConfirmation(false)}
                disabled={isCompleting}
                className="inline-flex min-h-11 items-center justify-center rounded-xl border border-[#E2E8F0] bg-white px-4 text-sm font-bold text-[#475569] transition hover:bg-[#F8FAFC] focus-visible:outline-2 focus-visible:outline-[#F26F21] disabled:opacity-50"
              >
                Tiếp tục luyện
              </button>
              <button
                type="button"
                onClick={() => void completeAndExit()}
                disabled={isCompleting || isVerifyingSubmission}
                className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-rose-600 px-4 text-sm font-bold text-white shadow-xs transition hover:bg-rose-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-rose-600 disabled:cursor-wait disabled:opacity-60"
              >
                {isCompleting && (
                  <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                )}
                Kết thúc phiên
              </button>
            </div>
          </section>
        </div>
      )}
    </div>
  )
}
