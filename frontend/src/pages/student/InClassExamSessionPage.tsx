import { ds } from '@/design/system'
import { EmptyState } from '@/components/common/EmptyState'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { useCallback, useEffect, useRef, useState } from 'react'
import { useBlocker, useNavigate, useSearchParams } from 'react-router-dom'
import { readDevicePreferences, updateDevicePreferences } from '@/utils/devicePreferences'
import {
  AlertTriangle,
  ArrowLeft,
  ArrowRight,
  Clock3,
  LoaderCircle,
  MessageSquarePlus,
  Mic,
  Settings,
  Volume2,
  VolumeX,
  WifiOff,
} from 'lucide-react'
import { toast } from '@/components/common/toastStore'
import { QuestionImage } from '@/components/question/QuestionImage'
import { useAudioRecorder } from '@/hooks/useAudioRecorder'
import { useSpeechRecognition } from '@/hooks/useSpeechRecognition'
import { shouldIgnoreSpaceShortcut } from '@/lib/keyboardShortcuts'
import { scheduleUi } from '@/lib/scheduleUi'
import {
  classifySegmentError,
  summarizeSegmentOutcomes,
  type SttSegmentOutcome,
} from '@/lib/sttSegments'
import { isLikelySilentOrInvalidTranscript, looksLikeQuestionEcho } from '@/lib/transcriptGuards'
import { userMessage } from '@/lib/userMessage'
import { cn } from '@/lib/utils'
import { inClassShiftService } from '@/services/inClassShift.service'
import {
  getSpeechRecognitionLangForCourse,
  getTtsVoiceForCourse,
  isSpeechPlaybackCancellation,
  playNaturalSpeech,
  stopSpeechPlayback,
  transcribeSpeech,
  type SpeechTranscriptionResponse,
} from '@/services/speech.service'
import type { InClassShift, StudentExamQuestion } from '@/types/inClassShift.types'
import {
  clearInClassExamDrafts,
  clearInClassExamSession,
  loadActiveInClassShiftId,
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

function getRecorderErrorMessage(error: unknown) {
  if (error instanceof DOMException) {
    if (error.name === 'NotAllowedError' || error.name === 'PermissionDeniedError') {
      return 'Trình duyệt chưa được cấp quyền micro. Bạn có thể cấp quyền hoặc nhập câu trả lời trực tiếp vào ô văn bản bên dưới.'
    }
    if (error.name === 'NotFoundError') {
      return 'Không tìm thấy micro trên thiết bị. Bạn có thể nhập câu trả lời trực tiếp vào ô văn bản bên dưới.'
    }
  }
  return getErrorMessage(error)
}

function isParticipationCompleted(status?: string | null) {
  const normalized = (status ?? '').trim().toUpperCase()
  return (
    normalized === 'SUBMITTED' ||
    normalized === 'GRADING' ||
    normalized === 'AI_GRADED' ||
    normalized === 'GRADED' ||
    normalized === 'AUDITED' ||
    normalized === 'FINALIZED' ||
    normalized === 'COMPLETED'
  )
}

const STOP_RECORDING_TIMEOUT_MS = 5_000

function getLatestWebSpeechTranscript(speechNow: {
  transcript: string
  getCurrentTranscript?: () => string
}) {
  const live =
    typeof speechNow.getCurrentTranscript === 'function' ? speechNow.getCurrentTranscript() : ''
  return live.trim() ? live : speechNow.transcript
}

export function InClassExamSessionPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const boot = loadInClassExamSession()
  const targetShiftId =
    searchParams.get('shiftId')?.trim() || boot?.shift.id || loadActiveInClassShiftId() || ''
  const matchingBoot = boot && (!targetShiftId || boot.shift.id === targetShiftId) ? boot : null
  const draftBoot = targetShiftId ? loadInClassExamDrafts(targetShiftId) : null

  const [shift, setShift] = useState<InClassShift | null>(matchingBoot?.shift ?? null)
  const [questions, setQuestions] = useState<StudentExamQuestion[]>(matchingBoot?.questions ?? [])
  const [startedAt, setStartedAt] = useState(() => matchingBoot?.startedAt ?? Date.now())
  const [examEndsAt, setExamEndsAt] = useState(() => matchingBoot?.examEndsAt ?? Date.now() + 20 * 60_000)
  const [hydrating, setHydrating] = useState(
    () => Boolean(targetShiftId) && (!matchingBoot || matchingBoot.questions.length === 0),
  )
  const [isOffline, setIsOffline] = useState(
    () => typeof navigator !== 'undefined' && navigator.onLine === false,
  )
  const [questionIndex, setQuestionIndex] = useState(() => {
    if (!draftBoot) return 0
    if (matchingBoot && matchingBoot.questions.length > 0) {
      return Math.max(0, Math.min(matchingBoot.questions.length - 1, draftBoot.questionIndex))
    }
    return Math.max(0, draftBoot.questionIndex)
  })
  const [drafts, setDrafts] = useState<Record<string, string>>(() => draftBoot?.drafts ?? {})
  const [leaving, setLeaving] = useState(false)
  const [confirmSubmit, setConfirmSubmit] = useState(false)
  const [submitted, setSubmitted] = useState<Record<string, boolean>>(
    () => draftBoot?.submittedKeys ?? {},
  )
  const [followUpTurns, setFollowUpTurns] = useState<Record<string, FollowUpTurnState>>({})
  const [busy, setBusy] = useState(false)
  const [isReadingQuestion, setIsReadingQuestion] = useState(false)
  const [autoRead, setAutoRead] = useState(() => readDevicePreferences().autoReadQuestions)
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [reRecording, setReRecording] = useState(false)
  const [recordingSeconds, setRecordingSeconds] = useState(0)
  const [secondsLeft, setSecondsLeft] = useState(() =>
    Math.max(0, Math.floor((examEndsAt - Date.now()) / 1000)),
  )
  const [answerError, setAnswerError] = useState('')
  const [segmentWarning, setSegmentWarning] = useState('')
  const [remoteCloseWarning, setRemoteCloseWarning] = useState<{
    title: string
    message: string
    failedQuestions: string[]
  } | null>(null)
  const [isTranscribing, setIsTranscribing] = useState(false)
  const [isStartingRecording, setIsStartingRecording] = useState(false)
  const [speechFallbackActive, setSpeechFallbackActive] = useState(false)
  const segmentPromisesRef = useRef<Array<Promise<SttSegmentOutcome>>>([])
  const lastSegmentBlobRef = useRef<Blob | null>(null)
  const vocabularyHintRef = useRef('')
  const recordingSecondsRef = useRef(0)
  const recordingActionInFlightRef = useRef(false)
  const stopTranscribePromiseRef = useRef<Promise<boolean> | null>(null)
  const recorderStartAttemptRef = useRef(0)
  const recordingGenerationRef = useRef(0)
  const webSpeechActiveRef = useRef(false)
  const webSpeechFlushedRef = useRef(false)
  const webSpeechActiveTurnKeyRef = useRef('')
  const manualEditSeqRef = useRef(0)
  const isMountedRef = useRef(true)
  const remoteCloseHandledRef = useRef(false)
  const handleRecorderSegment = useCallback((segmentBlob: Blob) => {
    const segmentGeneration = recordingGenerationRef.current
    lastSegmentBlobRef.current = segmentBlob
    const segmentPromise: Promise<SttSegmentOutcome> = transcribeSpeech(segmentBlob, {
      vocabularyHint: vocabularyHintRef.current,
    })
      .then((result): SttSegmentOutcome => {
        if (recordingGenerationRef.current !== segmentGeneration) {
          return { transcript: null, isSilence: true, failed: false }
        }
        return {
          transcript: result.transcript,
          confidenceScore: result.confidenceScore,
          provider: result.provider,
          usedFallback: result.usedFallback,
          isSilence: false,
          failed: false,
        }
      })
      .catch((error: unknown): SttSegmentOutcome => {
        if (recordingGenerationRef.current !== segmentGeneration) {
          return { transcript: null, isSilence: true, failed: false }
        }
        return classifySegmentError(error)
      })
    if (recordingGenerationRef.current === segmentGeneration) {
      segmentPromisesRef.current.push(segmentPromise)
    }
  }, [])
  const speech = useSpeechRecognition()
  const recorder = useAudioRecorder({ segmentDurationMs: 20_000, onSegment: handleRecorderSegment })
  const transcriptAreaRef = useRef<HTMLTextAreaElement | null>(null)
  const settingsRef = useRef<HTMLDivElement | null>(null)
  const endedRef = useRef(false)
  const shiftRef = useRef(shift)
  const startedAtRef = useRef(startedAt)
  const speechRef = useRef(speech)
  const recorderRef = useRef(recorder)
  const useRecorderFallbackRef = useRef(!speech.isSupported)
  const draftsRef = useRef(drafts)
  const submittedRef = useRef(submitted)
  const questionsRef = useRef(questions)
  const questionIndexRef = useRef(questionIndex)
  const followUpTurnsRef = useRef(followUpTurns)
  const readingGenerationRef = useRef(0)

  useEffect(() => {
    isMountedRef.current = true
    return () => {
      isMountedRef.current = false
      recorderStartAttemptRef.current += 1
      recordingGenerationRef.current += 1
      webSpeechActiveRef.current = false
    }
  }, [])

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
  const useRecorderFallback = !speech.isSupported || speechFallbackActive
  const isRecordingNow = useRecorderFallback ? recorder.isRecording : speech.isListening
  const isInteractionLocked = isRecordingNow || isStartingRecording || isTranscribing || busy
  const hasTranscript = Boolean(draft.trim()) || Boolean(turnKey && submitted[turnKey])
  const showTranscript = !isRecordingNow && hasTranscript
  const showFirstMic =
    Boolean(question) &&
    !submitted[turnKey] &&
    !reRecording &&
    (isRecordingNow || isTranscribing || !hasTranscript)

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
    if (!targetShiftId) {
      navigate('/in-class-exam', { replace: true })
    }
  }, [navigate, targetShiftId])

  useEffect(() => {
    if (!shift || hydrating) return
    const tick = () => setSecondsLeft(Math.max(0, Math.floor((examEndsAt - Date.now()) / 1000)))
    tick()
    const timer = window.setInterval(tick, 1000)
    return () => window.clearInterval(timer)
  }, [examEndsAt, hydrating, shift])

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
        .catch((error: unknown) => {
          const isCancelled =
            typeof isSpeechPlaybackCancellation === 'function'
              ? isSpeechPlaybackCancellation(error)
              : error instanceof Error && error.name === 'AbortError'
          if (generation === readingGenerationRef.current && !isCancelled) {
            setAnswerError(userMessage(error, 'Không đọc được câu hỏi trên trình duyệt này.'))
          }
        })
        .finally(() => {
          if (generation === readingGenerationRef.current) setIsReadingQuestion(false)
        })
    },
    [shift],
  )

  const getActiveTurnKey = useCallback(() => {
    const currentQuestion = questionsRef.current[questionIndexRef.current]
    if (!currentQuestion) return ''
    const turn = followUpTurnsRef.current[currentQuestion.questionId]
    return getTurnKey(currentQuestion.questionId, turn?.followUpIndex ?? 0)
  }, [])

  const collectPendingAnswers = useCallback(() => {
    const pendingDrafts = { ...draftsRef.current }
    const speechNow = speechRef.current
    const currentQuestion = questionsRef.current[questionIndexRef.current]
    if (!useRecorderFallbackRef.current && speechNow.isListening && currentQuestion) {
      const liveTranscript = getLatestWebSpeechTranscript(speechNow)
      if (liveTranscript.trim()) {
        const turn = followUpTurnsRef.current[currentQuestion.questionId]
        const key = getTurnKey(currentQuestion.questionId, turn?.followUpIndex ?? 0)
        pendingDrafts[key] = liveTranscript
      }
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
    if (!useRecorderFallbackRef.current && speechNow.isListening && currentQuestion) {
      const liveTranscript = getLatestWebSpeechTranscript(speechNow)
      if (liveTranscript.trim()) {
        const turn = followUpTurnsRef.current[currentQuestion.questionId]
        const key = getTurnKey(currentQuestion.questionId, turn?.followUpIndex ?? 0)
        pendingDrafts[key] = liveTranscript
      }
    }
    saveInClassExamDrafts(shift.id, {
      drafts: pendingDrafts,
      submittedKeys: submittedRef.current,
      questionIndex: questionIndexRef.current,
    })
    saveInClassExamSession({
      shift,
      questions: questionsRef.current,
      startedAt,
      examEndsAt,
      joinedAt: matchingBoot?.joinedAt,
    })
  }, [examEndsAt, matchingBoot?.joinedAt, shift, startedAt])

  const transcribeAudioWithSegments = useCallback(
    async (
      tailBlob: Blob,
      vocabularyHint?: string,
    ): Promise<{ speech: SpeechTranscriptionResponse; failedChunkCount: number }> => {
      const segmentGeneration = recordingGenerationRef.current
      const preTranscribedPromises = [...segmentPromisesRef.current]
      const fallbackSegmentBlob = lastSegmentBlobRef.current
      segmentPromisesRef.current = []
      lastSegmentBlobRef.current = null

      const uploadBlob = tailBlob.size >= 1_024 ? tailBlob : (fallbackSegmentBlob ?? tailBlob)
      let preFailedCount = 0
      let firstSegmentErrorDetail: string | undefined

      if (preTranscribedPromises.length > 0) {
        const tailPromise: Promise<SttSegmentOutcome> =
          tailBlob.size >= 2_048
            ? transcribeSpeech(tailBlob, { vocabularyHint }).then(
                (result): SttSegmentOutcome => ({
                  transcript: result.transcript,
                  confidenceScore: result.confidenceScore,
                  provider: result.provider,
                  usedFallback: result.usedFallback,
                  isSilence: false,
                  failed: false,
                }),
                classifySegmentError,
              )
            : Promise.resolve({ transcript: null, isSilence: true, failed: false })

        const preResults = await Promise.all(preTranscribedPromises)
        const tailResult = await tailPromise
        if (recordingGenerationRef.current !== segmentGeneration) {
          throw new Error('Đã hủy bản ghi âm.')
        }

        const preSummary = summarizeSegmentOutcomes(preResults)
        preFailedCount = preSummary.failedCount
        const allSummary = summarizeSegmentOutcomes([...preResults, tailResult])
        firstSegmentErrorDetail = allSummary.firstErrorDetail ?? preSummary.firstErrorDetail

        if (allSummary.validOutcomes.length > 0) {
          const joinedTranscript = allSummary.validOutcomes
            .map((item) => (item.transcript ?? '').trim())
            .join(' ')
            .trim()
          const confidences = allSummary.validOutcomes
            .map((item) => item.confidenceScore)
            .filter((score): score is number => typeof score === 'number' && score > 0)
          const avgConfidence =
            confidences.length > 0
              ? confidences.reduce((sum, val) => sum + val, 0) / confidences.length
              : 0.9

          return {
            speech: {
              transcript: joinedTranscript,
              rawText: joinedTranscript,
              provider: allSummary.validOutcomes[0].provider ?? 'CloudflareWhisper',
              usedFallback: allSummary.validOutcomes.some((item) => item.usedFallback),
              confidenceScore: avgConfidence,
            },
            failedChunkCount: allSummary.failedCount,
          }
        }
      }

      if (recordingGenerationRef.current !== segmentGeneration) {
        throw new Error('Đã hủy bản ghi âm.')
      }

      try {
        const singleSpeech = await transcribeSpeech(uploadBlob, { vocabularyHint })
        return { speech: singleSpeech, failedChunkCount: preFailedCount }
      } catch (fallbackError) {
        const fallbackStatus =
          typeof fallbackError === 'object' &&
          fallbackError !== null &&
          'status' in fallbackError &&
          typeof (fallbackError as { status?: unknown }).status === 'number'
            ? (fallbackError as { status: number }).status
            : 500
        if (preFailedCount > 0 && fallbackStatus === 422) {
          throw {
            status: 502,
            detail:
              firstSegmentErrorDetail ||
              'Một số đoạn ghi âm (20 giây) gặp lỗi kết nối khi nhận diện giọng nói. Vui lòng thử ghi âm lại.',
          }
        }
        throw fallbackError
      }
    },
    [],
  )

  const startRecorderFallback = useCallback(async () => {
    if (recordingActionInFlightRef.current) return
    const attemptId = ++recorderStartAttemptRef.current
    const generation = ++recordingGenerationRef.current
    recordingActionInFlightRef.current = true
    setIsStartingRecording(true)
    setAnswerError('')
    setSegmentWarning('')
    recorderRef.current.clearError()
    segmentPromisesRef.current = []
    lastSegmentBlobRef.current = null
    recordingSecondsRef.current = 0
    try {
      await recorderRef.current.startRecording()
      if (!isMountedRef.current || recorderStartAttemptRef.current !== attemptId) {
        recorderRef.current.cancelRecording()
      }
    } catch (recordError) {
      if (recordingGenerationRef.current === generation) {
        segmentPromisesRef.current = []
        lastSegmentBlobRef.current = null
      }
      if (isMountedRef.current && recorderStartAttemptRef.current === attemptId) {
        setAnswerError(getRecorderErrorMessage(recordError))
      }
    } finally {
      recordingActionInFlightRef.current = false
      if (isMountedRef.current) {
        setIsStartingRecording(false)
      }
    }
  }, [])

  const stopAndTranscribeFallback = useCallback(() => {
    if (stopTranscribePromiseRef.current) return stopTranscribePromiseRef.current
    if (recordingActionInFlightRef.current) return Promise.resolve(false)
    const targetShift = shiftRef.current
    const targetQuestion = questionsRef.current[questionIndexRef.current]
    if (!targetShift || !targetQuestion) return Promise.resolve(false)
    const targetTurn = followUpTurnsRef.current[targetQuestion.questionId]
    const targetFollowUpIndex = targetTurn?.followUpIndex ?? 0
    const targetTurnKey = getTurnKey(targetQuestion.questionId, targetFollowUpIndex)
    const targetPrompt =
      targetFollowUpIndex > 0 && targetTurn?.followUpPrompt
        ? targetTurn.followUpPrompt
        : targetQuestion.content || targetQuestion.title || ''
    const recordedSec = Math.max(1, recordingSecondsRef.current)
    const vocabularyHint = vocabularyHintRef.current || undefined

    recordingActionInFlightRef.current = true
    setIsTranscribing(true)
    setAnswerError('')
    setSegmentWarning('')

    let task: Promise<boolean> | null = null
    task = (async (): Promise<boolean> => {
      let stopTimerId: number | undefined
      try {
        const tailBlob = await Promise.race([
          recorderRef.current.stopRecording(),
          new Promise<never>((_, reject) => {
            stopTimerId = window.setTimeout(() => {
              recordingGenerationRef.current += 1
              segmentPromisesRef.current = []
              lastSegmentBlobRef.current = null
              recorderRef.current.cancelRecording()
              reject(new Error('Hết thời gian chờ xử lý bản ghi âm.'))
            }, STOP_RECORDING_TIMEOUT_MS)
          }),
        ]).finally(() => {
          if (stopTimerId !== undefined) window.clearTimeout(stopTimerId)
        })
        const { speech: sttResult, failedChunkCount } = await transcribeAudioWithSegments(
          tailBlob,
          vocabularyHint,
        )
        const transcript = sttResult.transcript.trim()
        const echoTarget =
          targetFollowUpIndex > 0
            ? { title: targetQuestion.title, content: targetPrompt }
            : targetQuestion
        if (
          isLikelySilentOrInvalidTranscript(transcript, recordedSec) ||
          (typeof sttResult.confidenceScore === 'number' &&
            sttResult.confidenceScore > 0 &&
            sttResult.confidenceScore < 0.25) ||
          looksLikeQuestionEcho(transcript, echoTarget)
        ) {
          if (isMountedRef.current) {
            setAnswerError(
              'Không nhận diện được giọng nói rõ ràng. Hãy nói gần micro hơn, ghi âm lại hoặc nhập/chỉnh sửa trực tiếp bên dưới.',
            )
            setReRecording(false)
          }
          return false
        }

        const nextDrafts = { ...draftsRef.current, [targetTurnKey]: transcript }
        draftsRef.current = nextDrafts
        if (isMountedRef.current) {
          setDrafts(nextDrafts)
          setReRecording(false)
          if (failedChunkCount > 0) {
            const chunkWarning = `Có ${failedChunkCount} đoạn ghi âm (20 giây) gặp lỗi kết nối khi nhận diện giọng nói nên transcript có thể chưa đầy đủ. Nội dung nhận diện được vẫn được giữ — vui lòng kiểm tra/chỉnh sửa lại hoặc ghi âm lại.`
            setSegmentWarning(chunkWarning)
            toast.warning(chunkWarning)
          }
        }
        saveInClassExamDrafts(targetShift.id, {
          drafts: nextDrafts,
          submittedKeys: submittedRef.current,
          questionIndex: questionIndexRef.current,
        })
        return true
      } catch (sttError) {
        if (isMountedRef.current) {
          setAnswerError(getErrorMessage(sttError))
          setReRecording(false)
        }
        return false
      } finally {
        if (stopTranscribePromiseRef.current === task) {
          stopTranscribePromiseRef.current = null
        }
        recordingActionInFlightRef.current = false
        if (isMountedRef.current) {
          setIsTranscribing(false)
        }
      }
    })()

    stopTranscribePromiseRef.current = task
    return task
  }, [transcribeAudioWithSegments])

  const startWebSpeechListening = useCallback(
    (lang: string) => {
      webSpeechFlushedRef.current = false
      webSpeechActiveRef.current = true
      webSpeechActiveTurnKeyRef.current = getActiveTurnKey()
      speechRef.current.startListening(lang)
    },
    [getActiveTurnKey],
  )

  const stopWebSpeechAndCommit = useCallback(() => {
    const speechNow = speechRef.current
    const activeKey = webSpeechActiveTurnKeyRef.current || getActiveTurnKey()
    const liveTranscript = getLatestWebSpeechTranscript(speechNow)
    if (!useRecorderFallbackRef.current && activeKey && liveTranscript.trim()) {
      const nextDrafts = { ...draftsRef.current, [activeKey]: liveTranscript }
      draftsRef.current = nextDrafts
      setDrafts(nextDrafts)
    }
    const wasActive = speechNow.isListening || webSpeechActiveRef.current
    webSpeechFlushedRef.current = true
    webSpeechActiveRef.current = false
    webSpeechActiveTurnKeyRef.current = ''
    if (wasActive) {
      speechNow.stopListening()
    }
    speechNow.resetTranscript()
  }, [getActiveTurnKey])

  const cancelActiveRecorder = useCallback(() => {
    recorderStartAttemptRef.current += 1
    recordingGenerationRef.current += 1
    if (recorderRef.current.isRecording) {
      recorderRef.current.cancelRecording()
    }
    if (!useRecorderFallbackRef.current) {
      webSpeechFlushedRef.current = true
      webSpeechActiveTurnKeyRef.current = ''
      if (speechRef.current.isListening || webSpeechActiveRef.current) {
        webSpeechActiveRef.current = false
        speechRef.current.stopListening()
      }
      speechRef.current.resetTranscript()
    }
    segmentPromisesRef.current = []
    lastSegmentBlobRef.current = null
    setIsStartingRecording(false)
  }, [])

  const submitExamForGrading = useCallback(async () => {
    const activeShift = shiftRef.current
    if (!activeShift) return false
    stopReadingQuestion()
    const speechNow = speechRef.current
    if (useRecorderFallbackRef.current) {
      if (stopTranscribePromiseRef.current) {
        try {
          await stopTranscribePromiseRef.current
        } catch {
          // Không chặn nộp bài khi hết giờ nếu bóc băng cuối ca gặp lỗi.
        }
      } else if (recorderRef.current.isRecording) {
        try {
          await stopAndTranscribeFallback()
        } catch {
          // Không chặn nộp bài khi hết giờ nếu bóc băng cuối ca gặp lỗi.
        }
      } else {
        cancelActiveRecorder()
      }
    } else if (speechNow.isListening || webSpeechActiveRef.current) {
      stopWebSpeechAndCommit()
    } else {
      webSpeechActiveTurnKeyRef.current = ''
      speechNow.resetTranscript()
    }
    const answers = collectPendingAnswers()
    for (const item of answers) {
      try {
        await inClassShiftService.submitStudentAnswer(
          activeShift.id,
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
        saveInClassExamDrafts(activeShift.id, {
          drafts: draftsRef.current,
          questionIndex: questionIndexRef.current,
        })
        setAnswerError(getErrorMessage(submitError))
        return false
      }
    }
    try {
      await inClassShiftService.finishStudentExam(activeShift.id)
      markInClassShiftCompleted(activeShift.id)
      clearInClassExamDrafts(activeShift.id)
      clearInClassExamSession()
      return true
    } catch (finishError) {
      saveInClassExamDrafts(activeShift.id, {
        drafts: draftsRef.current,
        questionIndex: questionIndexRef.current,
      })
      setAnswerError(getErrorMessage(finishError))
      return false
    }
  }, [
    cancelActiveRecorder,
    collectPendingAnswers,
    stopAndTranscribeFallback,
    stopReadingQuestion,
    stopWebSpeechAndCommit,
  ])

  const flushPendingDraftsBestEffort = useCallback(
    async (
      targetShift: InClassShift,
      baseQuestions: StudentExamQuestion[],
      existingServerRows?: Array<{
        questionId: string
        isFollowUp?: boolean
        followUpIndex?: number
        transcript?: string | null
      }>,
    ): Promise<{ unsubmitted: PendingAnswer[]; lastError: string }> => {
      if (questionsRef.current.length === 0 && baseQuestions.length > 0) {
        questionsRef.current = baseQuestions
      }
      if (existingServerRows && existingServerRows.length > 0) {
        const nextSubmitted = { ...submittedRef.current }
        for (const row of existingServerRows) {
          if (!row.transcript?.trim()) continue
          const key = getTurnKey(row.questionId, row.isFollowUp ? row.followUpIndex || 1 : 0)
          nextSubmitted[key] = true
          if (!row.isFollowUp) {
            nextSubmitted[row.questionId] = true
          }
        }
        submittedRef.current = nextSubmitted
        setSubmitted(nextSubmitted)
      }

      stopReadingQuestion()
      const speechNow = speechRef.current
      if (useRecorderFallbackRef.current) {
        if (stopTranscribePromiseRef.current) {
          try {
            await stopTranscribePromiseRef.current
          } catch {
            // Giữ các bản nháp còn lại nếu quá trình dừng ghi âm gặp lỗi.
          }
        } else if (recorderRef.current.isRecording) {
          try {
            await stopAndTranscribeFallback()
          } catch {
            // Giữ các bản nháp còn lại nếu quá trình dừng ghi âm gặp lỗi.
          }
        } else {
          cancelActiveRecorder()
        }
      } else if (speechNow.isListening || webSpeechActiveRef.current) {
        stopWebSpeechAndCommit()
      } else {
        webSpeechActiveTurnKeyRef.current = ''
        speechNow.resetTranscript()
      }

      const pending = collectPendingAnswers()
      if (pending.length === 0) {
        if (!targetShift.isLocked && targetShift.status !== 'FINALIZED') {
          await inClassShiftService.finishStudentExam(targetShift.id).catch(() => undefined)
        }
        return { unsubmitted: [], lastError: '' }
      }

      if (targetShift.isLocked || targetShift.status === 'FINALIZED') {
        return {
          unsubmitted: pending,
          lastError: 'Ca thi đã chốt sổ điểm nên không thể tiếp nhận thêm bài nộp.',
        }
      }

      const unsubmitted: PendingAnswer[] = []
      let lastError = ''
      for (const item of pending) {
        try {
          await inClassShiftService.submitStudentAnswer(
            targetShift.id,
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
          const message = getErrorMessage(submitError)
          if (/trùng lặp|duplicate/i.test(message)) {
            submittedRef.current = {
              ...submittedRef.current,
              [item.turnKey]: true,
              ...(!item.isFollowUp ? { [item.questionId]: true } : {}),
            }
            setSubmitted(submittedRef.current)
          } else {
            unsubmitted.push(item)
            if (!lastError) lastError = message
          }
        }
      }

      if (unsubmitted.length === 0) {
        await inClassShiftService.finishStudentExam(targetShift.id).catch(() => undefined)
      }
      return { unsubmitted, lastError }
    },
    [
      cancelActiveRecorder,
      collectPendingAnswers,
      stopAndTranscribeFallback,
      stopReadingQuestion,
      stopWebSpeechAndCommit,
    ],
  )

  const syncSessionFromServer = useCallback(async () => {
    if (!targetShiftId || endedRef.current) return
    try {
      const [latestShift, resumeRes, mySubmissions] = await Promise.all([
        inClassShiftService.getStudentShift(targetShiftId),
        inClassShiftService.resumeStudentShift(targetShiftId),
        inClassShiftService.listStudentResults(targetShiftId).catch(() => []),
      ])
      if (endedRef.current) return
      setIsOffline(false)

      if (
        resumeRes.timedOut ||
        isParticipationCompleted(resumeRes.status) ||
        latestShift.status === 'CLOSED' ||
        latestShift.status === 'FINALIZED'
      ) {
        if (remoteCloseHandledRef.current || endedRef.current) return
        remoteCloseHandledRef.current = true
        endedRef.current = true

        const resolvedQuestions =
          resumeRes.questions.length > 0 ? resumeRes.questions : questionsRef.current
        shiftRef.current = latestShift
        questionsRef.current = resolvedQuestions
        setShift(latestShift)
        if (resolvedQuestions.length > 0) {
          setQuestions(resolvedQuestions)
        }

        const normalMessage = resumeRes.timedOut
          ? 'Đã hết thời gian làm bài. Bài làm đã được nộp tự động để AI chấm.'
          : 'Phiên làm bài đã kết thúc và bài nộp đã được ghi nhận trên hệ thống.'

        const { unsubmitted, lastError } = await flushPendingDraftsBestEffort(
          latestShift,
          resolvedQuestions,
          mySubmissions,
        )
        if (unsubmitted.length === 0) {
          leaveWithMessage(normalMessage, targetShiftId)
          return
        }

        saveInClassExamDrafts(targetShiftId, {
          drafts: draftsRef.current,
          submittedKeys: submittedRef.current,
          questionIndex: questionIndexRef.current,
        })
        const failedQuestions = unsubmitted.map((item) => {
          const idx = resolvedQuestions.findIndex((q) => q.questionId === item.questionId)
          const label = idx >= 0 ? `Câu ${idx + 1}` : 'Câu hỏi'
          return item.isFollowUp ? `${label} (câu phụ #${item.followUpIndex})` : label
        })
        setRemoteCloseWarning({
          title:
            latestShift.isLocked || latestShift.status === 'FINALIZED'
              ? 'Ca thi đã chốt sổ điểm'
              : 'Ca thi đã đóng',
          message:
            lastError ||
            'Một số câu trả lời chưa kịp gửi lên máy chủ. Bản nháp vẫn được giữ an toàn trên trình duyệt này.',
          failedQuestions,
        })
        return
      }

      const joinedMs = Date.parse(resumeRes.joinedAt)
      const endsMs = Date.parse(resumeRes.examEndsAt)
      const resolvedEndsAt = Number.isFinite(endsMs)
        ? endsMs
        : Date.now() + Math.max(0, resumeRes.remainingSeconds) * 1000
      const resolvedStartedAt = Number.isFinite(joinedMs) ? joinedMs : startedAtRef.current

      const nextSubmitted: Record<string, boolean> = { ...submittedRef.current }
      const serverTranscripts: Record<string, string> = {}
      for (const row of mySubmissions) {
        const key = getTurnKey(row.questionId, row.isFollowUp ? row.followUpIndex || 1 : 0)
        if (row.transcript?.trim()) {
          nextSubmitted[key] = true
          if (!row.isFollowUp) {
            nextSubmitted[row.questionId] = true
          }
          serverTranscripts[key] = row.transcript
        }
      }

      shiftRef.current = latestShift
      startedAtRef.current = resolvedStartedAt
      setShift(latestShift)
      if (resumeRes.questions.length > 0) {
        setQuestions(resumeRes.questions)
        setQuestionIndex((current) =>
          Math.max(0, Math.min(resumeRes.questions.length - 1, current)),
        )
      }
      setStartedAt(resolvedStartedAt)
      setExamEndsAt(resolvedEndsAt)
      setSecondsLeft(Math.max(0, Math.floor((resolvedEndsAt - Date.now()) / 1000)))
      setSubmitted(nextSubmitted)
      setDrafts((current) => {
        const merged = { ...serverTranscripts }
        for (const [k, v] of Object.entries(current)) {
          if (v.trim() || !(k in merged)) {
            merged[k] = v
          }
        }
        return merged
      })

      const effectiveQuestions =
        resumeRes.questions.length > 0 ? resumeRes.questions : questionsRef.current
      saveInClassExamSession({
        shift: latestShift,
        questions: effectiveQuestions,
        startedAt: resolvedStartedAt,
        examEndsAt: resolvedEndsAt,
        joinedAt: resumeRes.joinedAt,
      })
      saveInClassExamDrafts(targetShiftId, {
        drafts: { ...serverTranscripts, ...draftsRef.current },
        submittedKeys: nextSubmitted,
        questionIndex: questionIndexRef.current,
      })
    } catch (syncErr) {
      const offlineNow = typeof navigator !== 'undefined' && navigator.onLine === false
      if (offlineNow) {
        setIsOffline(true)
        return
      }
      if (!shiftRef.current || questionsRef.current.length === 0) {
        leaveWithMessage(getErrorMessage(syncErr))
      }
    } finally {
      setHydrating(false)
    }
  }, [flushPendingDraftsBestEffort, leaveWithMessage, targetShiftId])

  useEffect(() => {
    if (!targetShiftId) return
    const cancelSync = scheduleUi(() => {
      void syncSessionFromServer()
    })
    const handleOnline = () => {
      setIsOffline(false)
      void syncSessionFromServer()
    }
    const handleOffline = () => {
      setIsOffline(true)
    }
    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)
    return () => {
      cancelSync()
      window.removeEventListener('online', handleOnline)
      window.removeEventListener('offline', handleOffline)
    }
  }, [syncSessionFromServer, targetShiftId])

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
    shiftRef.current = shift
    startedAtRef.current = startedAt
    speechRef.current = speech
    recorderRef.current = recorder
    useRecorderFallbackRef.current = useRecorderFallback
    draftsRef.current = drafts
    submittedRef.current = submitted
    questionsRef.current = questions
    questionIndexRef.current = questionIndex
    followUpTurnsRef.current = followUpTurns
    endExamRef.current = endExam
    submitExamForGradingRef.current = submitExamForGrading
    persistDraftsRef.current = persistDrafts
    vocabularyHintRef.current = [shift?.courseCode, shift?.className, question?.title, activePromptText]
      .filter((item): item is string => Boolean(item && item.trim()))
      .join(' - ')
      .slice(0, 320)
  })

  useEffect(() => {
    if (!speech.isSupported) return
    if (speech.error !== 'network' && speech.error !== 'service-not-allowed') return
    const timer = window.setTimeout(() => {
      setSpeechFallbackActive(true)
    }, 0)
    return () => window.clearTimeout(timer)
  }, [speech.error, speech.isSupported])

  const shiftId = shift?.id

  useEffect(() => {
    if (!shiftId) return
    const timer = window.setInterval(() => persistDraftsRef.current(), 5000)
    return () => window.clearInterval(timer)
  }, [shiftId])

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

  }, [blocker, shift])

  const submitAndLeave = () => {
    if (blocker.state !== 'blocked' || !shift || leaveSubmitInFlightRef.current || endedRef.current) return
    leaveSubmitInFlightRef.current = true
    setLeaving(true)
    endedRef.current = true
    void (async () => {
      const finishedOk = await submitExamForGradingRef.current()
      leaveSubmitInFlightRef.current = false
      setLeaving(false)
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
  }

  useEffect(() => {
    if (hydrating || !shift || secondsLeft !== 0) return
    endExamRef.current('Hết thời gian làm bài. Bài chưa nộp được gửi để AI chấm.')
  }, [hydrating, secondsLeft, shift])

  useEffect(() => {
    if (!shiftId) return
    let cancelled = false
    const watch = async () => {
      if (remoteCloseHandledRef.current || endedRef.current) return
      try {
        const latest = await inClassShiftService.getStudentShift(shiftId)
        if (
          !cancelled &&
          !remoteCloseHandledRef.current &&
          !endedRef.current &&
          (latest.status === 'CLOSED' || latest.status === 'FINALIZED')
        ) {
          remoteCloseHandledRef.current = true
          endedRef.current = true
          window.clearInterval(timer)
          shiftRef.current = latest
          setShift(latest)
          const resolvedQuestions = questionsRef.current
          const { unsubmitted, lastError } = await flushPendingDraftsBestEffort(
            latest,
            resolvedQuestions,
          )
          if (cancelled) return
          if (unsubmitted.length === 0) {
            leaveWithMessage('Ca đã được đóng. Bài làm được nộp tự động để AI chấm.', latest.id)
            return
          }
          saveInClassExamDrafts(latest.id, {
            drafts: draftsRef.current,
            submittedKeys: submittedRef.current,
            questionIndex: questionIndexRef.current,
          })
          const failedQuestions = unsubmitted.map((item) => {
            const idx = resolvedQuestions.findIndex((q) => q.questionId === item.questionId)
            const label = idx >= 0 ? `Câu ${idx + 1}` : 'Câu hỏi'
            return item.isFollowUp ? `${label} (câu phụ #${item.followUpIndex})` : label
          })
          setRemoteCloseWarning({
            title:
              latest.isLocked || latest.status === 'FINALIZED'
                ? 'Ca thi đã chốt sổ điểm'
                : 'Ca thi đã đóng',
            message:
              lastError ||
              'Một số câu trả lời chưa kịp gửi lên máy chủ. Bản nháp vẫn được giữ an toàn trên trình duyệt này.',
            failedQuestions,
          })
        }
      } catch {
        // Giữ phòng làm bài nếu lần kiểm tra trạng thái bị lỗi mạng.
      }
    }
    const timer = window.setInterval(() => void watch(), 3000)
    void watch()
    return () => {
      cancelled = true
      window.clearInterval(timer)
    }
  }, [flushPendingDraftsBestEffort, leaveWithMessage, shiftId])

  useEffect(() => {
    if (!isRecordingNow) {
      recordingSecondsRef.current = 0
      return scheduleUi(() => setRecordingSeconds(0))
    }
    const timer = window.setInterval(() => {
      recordingSecondsRef.current += 1
      setRecordingSeconds((current) => current + 1)
    }, 1000)
    return () => {
      window.clearInterval(timer)
      recordingSecondsRef.current = 0
      setRecordingSeconds(0)
    }
  }, [isRecordingNow])

  useEffect(() => {
    if (useRecorderFallback || !speech.isListening || !turnKey) return
    const latestTranscript = speech.transcript
    if (!latestTranscript.trim()) return
    const targetKey = webSpeechActiveTurnKeyRef.current || turnKey
    const editSeq = manualEditSeqRef.current
    const timer = window.setTimeout(() => {
      if (manualEditSeqRef.current !== editSeq) return
      draftsRef.current = { ...draftsRef.current, [targetKey]: latestTranscript }
      setDrafts((current) => ({ ...current, [targetKey]: latestTranscript }))
    }, 0)
    return () => window.clearTimeout(timer)
  }, [speech.isListening, speech.transcript, turnKey, useRecorderFallback])

  const wasListeningRef = useRef(false)
  useEffect(() => {
    if (useRecorderFallback) {
      wasListeningRef.current = false
      return
    }
    const shouldExitReRecording = wasListeningRef.current && !speech.isListening
    wasListeningRef.current = speech.isListening
    if (!shouldExitReRecording) return

    webSpeechActiveRef.current = false
    const speechNow = speechRef.current
    const latestTranscript = getLatestWebSpeechTranscript(speechNow)
    const targetKey = webSpeechActiveTurnKeyRef.current || getActiveTurnKey()
    const shouldFlushFinalTranscript =
      !webSpeechFlushedRef.current && Boolean(targetKey) && Boolean(latestTranscript.trim())
    if (shouldFlushFinalTranscript) {
      draftsRef.current = { ...draftsRef.current, [targetKey]: latestTranscript }
    }
    webSpeechFlushedRef.current = true
    webSpeechActiveTurnKeyRef.current = ''
    speechNow.resetTranscript()

    const editSeq = manualEditSeqRef.current
    const timer = window.setTimeout(() => {
      if (shouldFlushFinalTranscript && manualEditSeqRef.current === editSeq) {
        setDrafts((current) => ({ ...current, [targetKey]: latestTranscript }))
      }
      setReRecording(false)
    }, 0)
    return () => window.clearTimeout(timer)
  }, [getActiveTurnKey, speech.isListening, useRecorderFallback])

  useEffect(() => {
    if (useRecorderFallbackRef.current) return
    if (!speechRef.current.isListening && !webSpeechActiveRef.current) {
      webSpeechActiveTurnKeyRef.current = ''
      speechRef.current.resetTranscript()
    }
  }, [turnKey])

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
      if (
        confirmSubmit ||
        blocker.state === 'blocked' ||
        Boolean(remoteCloseWarning) ||
        settingsOpen ||
        shouldIgnoreSpaceShortcut(event)
      ) {
        return
      }
      event.preventDefault()
      if (busy || isStartingRecording || isTranscribing || !question || submitted[turnKey]) return
      if (isRecordingNow) {
        if (useRecorderFallback) {
          void stopAndTranscribeFallback()
        } else {
          stopWebSpeechAndCommit()
        }
        return
      }
      if (!showFirstMic && hasTranscript && !reRecording) {
        setReRecording(true)
        return
      }
      if (useRecorderFallback) {
        void startRecorderFallback()
        return
      }
      startWebSpeechListening(
        getSpeechRecognitionLangForCourse(shift.courseCode, shift.className, activePromptText),
      )
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [
    activePromptText,
    blocker.state,
    busy,
    confirmSubmit,
    hasTranscript,
    isRecordingNow,
    isStartingRecording,
    isTranscribing,
    question,
    reRecording,
    remoteCloseWarning,
    settingsOpen,
    shift,
    showFirstMic,
    startRecorderFallback,
    startWebSpeechListening,
    stopAndTranscribeFallback,
    stopWebSpeechAndCommit,
    submitted,
    turnKey,
    useRecorderFallback,
  ])

  const toggleReadQuestion = () => {
    if (!question) return
    if (isReadingQuestion) {
      stopReadingQuestion()
      return
    }
    startReadingQuestion(activePromptText)
  }

  const toggleRecording = () => {
    if (!shift || !question || submitted[turnKey] || busy || isStartingRecording || isTranscribing) return
    setAnswerError('')
    setSegmentWarning('')
    if (useRecorderFallback) {
      if (recorder.isRecording) {
        void stopAndTranscribeFallback()
      } else {
        void startRecorderFallback()
      }
      return
    }
    if (speech.isListening) {
      stopWebSpeechAndCommit()
      return
    }
    startWebSpeechListening(
      getSpeechRecognitionLangForCourse(shift.courseCode, shift.className, activePromptText),
    )
  }

  const beginReRecord = () => {
    if (!question || submitted[turnKey] || busy || isStartingRecording || isTranscribing || secondsLeft <= 0) return
    cancelActiveRecorder()
    stopReadingQuestion()
    setAnswerError('')
    setSegmentWarning('')
    setReRecording(true)
  }

  const cancelReRecord = () => {
    cancelActiveRecorder()
    setReRecording(false)
  }

  const submitCurrent = async (): Promise<'DONE' | 'FOLLOW_UP_READY' | 'FAILED'> => {
    if (!shift || !question || submitted[turnKey]) return 'DONE'
    const transcript = (drafts[turnKey] ?? '').trim()
    if (!transcript) return 'FAILED'
    if (useRecorderFallback) {
      cancelActiveRecorder()
    } else if (speech.isListening || webSpeechActiveRef.current) {
      stopWebSpeechAndCommit()
    } else {
      speech.resetTranscript()
    }
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
      const nextSubmittedMap = {
        ...submittedRef.current,
        [turnKey]: true,
        [question.questionId]: true,
      }
      submittedRef.current = nextSubmittedMap
      setSubmitted(nextSubmittedMap)
      saveInClassExamDrafts(shift.id, {
        drafts: draftsRef.current,
        submittedKeys: nextSubmittedMap,
        questionIndex: questionIndexRef.current,
      })

      if (hasPendingFollowUpTurns && response.submissionId) {
        try {
          const nextFu = await inClassShiftService.requestNextFollowUp(shift.id, response.submissionId)
          if (nextFu.hasMoreFollowUp && nextFu.followUpPrompt) {
            recordingGenerationRef.current += 1
            webSpeechActiveTurnKeyRef.current = ''
            if (!useRecorderFallbackRef.current) {
              speechRef.current.resetTranscript()
            }
            setFollowUpTurns((current) => ({
              ...current,
              [question.questionId]: {
                followUpIndex: nextFu.followUpIndex,
                parentSubmissionId: nextFu.parentSubmissionId,
                followUpPrompt: nextFu.followUpPrompt,
              },
            }))
            setReRecording(false)
            setSegmentWarning('')
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

  const shouldRequestFollowUp = hasPendingFollowUpTurns && Boolean(draft.trim()) && !submitted[turnKey]

  const goNext = async () => {
    if (isInteractionLocked) return
    const hasText = Boolean((drafts[turnKey] ?? '').trim())
    if (hasText && !submitted[turnKey]) {
      const status = await submitCurrent()
      if (status === 'FAILED' || status === 'FOLLOW_UP_READY') return
    }
    cancelActiveRecorder()
    setReRecording(false)
    setSegmentWarning('')
    setQuestionIndex((current) => Math.min(questions.length - 1, current + 1))
  }

  const skipFollowUpOrQuestion = () => {
    if (isInteractionLocked || secondsLeft <= 0) return
    cancelActiveRecorder()
    setReRecording(false)
    setSegmentWarning('')
    setQuestionIndex((current) => Math.min(questions.length - 1, current + 1))
  }

  const unanswered = questions.filter((item) =>
      !submitted[item.questionId] && !(drafts[item.questionId] ?? '').trim(),
    ).length
  const unansweredFollowUp = currentTurn.followUpIndex > 0 && !draft.trim() && !submitted[turnKey]
  const warnings = [
      unanswered > 0 ? `Bạn còn ${unanswered} câu chính chưa trả lời.` : '',
      unansweredFollowUp ? 'Bạn chưa trả lời câu hỏi phụ hiện tại.' : '',
    ].filter(Boolean)
  const finish = () => {
    setConfirmSubmit(false)
    if (isInteractionLocked || endedRef.current || secondsLeft <= 0) return
    endExam('Đã nộp bài. Điểm số và nhận xét sẽ hiển thị sau khi giảng viên chốt bảng điểm ca thi.')
  }

  if (!shift || hydrating) {
    return (
      <div className="flex min-h-dvh flex-col items-center justify-center gap-3 bg-[#F4FBFD] text-[#0F2854]">
        <LoaderCircle className="size-8 animate-spin text-[#1C4D8D] motion-reduce:animate-none" />
        <p className="text-sm font-semibold text-[#5b6b84]">
          Đang đồng bộ bộ câu hỏi và thời gian làm bài từ máy chủ...
        </p>
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
                  'inline-flex min-h-10 items-center gap-2 rounded-full px-3.5 font-mono text-sm font-bold tabular-nums',
                  isUrgent
                    ? 'bg-[#b4233c] text-white'
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
                      onClick={() => setAutoRead((current) => {
                        const next = !current
                        updateDevicePreferences({ autoReadQuestions: next })
                        return next
                      })}
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
                  disabled={isInteractionLocked}
                  onClick={() => {
                    cancelActiveRecorder()
                    setReRecording(false)
                    setSegmentWarning('')
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
          <div className="p-5">
            <EmptyState icon={Mic} title="Ca chưa phân câu hỏi" description="Báo giảng viên kiểm tra ngân hàng câu đã duyệt." />
          </div>
        ) : (
          question && (
            <section className="flex flex-col gap-3 px-4 py-4 sm:px-5">
              {isOffline && (
                <div
                  role="status"
                  className="flex items-center gap-2.5 rounded-xl border border-[#e6d3ad] bg-[#fdf7ea] px-3.5 py-2.5 text-xs font-semibold text-[#8d6118]"
                >
                  <WifiOff className="size-4 shrink-0" aria-hidden="true" />
                  <span>
                    Mất kết nối mạng tạm thời — bản nháp vẫn được lưu trên thiết bị và sẽ tự đồng bộ lại khi có mạng.
                  </span>
                </div>
              )}
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

              {currentTurn.followUpIndex > 0 && currentTurn.followUpPrompt ? (
                <div className="rounded-2xl border border-[#1C4D8D]/40 bg-[#F4FBFD] p-3.5">
                  <p className="text-xs font-bold tracking-[0.06em] text-[#1C4D8D] uppercase">
                    Câu hỏi phụ #{currentTurn.followUpIndex}/{maxFollowUps} (Phát vấn theo ngữ cảnh)
                  </p>
                  <p className="mt-1.5 text-base leading-snug font-semibold text-[#0F2854]">
                    {currentTurn.followUpPrompt}
                  </p>
                </div>
              ) : (
                <div className="overflow-hidden rounded-2xl border border-[#82aedb]/55 bg-white">
                  <div className="p-3.5">
                    <h2 className="text-[18px] leading-snug font-semibold whitespace-pre-line text-[#0F2854]">
                      <span className="font-semibold">Câu {questionIndex + 1}. </span>
                      {question.content || question.title}
                    </h2>
                    {question.imageUrl?.trim() && (
                      <QuestionImage
                        imageUrl={question.imageUrl}
                        imageAlt={question.imageDescription}
                        showCaption={Boolean(question.imageDescription?.trim())}
                        className="mt-3"
                      />
                    )}
                  </div>
                </div>
              )}

              {showFirstMic && (
                <div className="flex flex-col items-center gap-2 py-1">
                  <button
                    type="button"
                    disabled={busy || isStartingRecording || isTranscribing || (!isRecordingNow && secondsLeft <= 0)}
                    aria-pressed={isRecordingNow}
                    aria-label={isRecordingNow ? 'Dừng ghi âm' : 'Bắt đầu ghi âm'}
                    onClick={toggleRecording}
                    className={cn(
                      'flex size-[7.5rem] flex-col items-center justify-center gap-1 rounded-full border text-sm font-bold shadow-[0_12px_28px_rgba(28,77,141,0.16)] transition select-none disabled:opacity-60',
                      isRecordingNow
                        ? 'border-[#d97788] bg-[#b4233c] text-white shadow-[0_12px_28px_rgba(180,35,60,0.24)]'
                        : 'border-[#82aedb]/55 bg-[#F4FBFD] text-[#0F2854] hover:bg-[#BDE8F5]',
                    )}
                  >
                    <Mic className={cn('size-7', isRecordingNow && 'animate-pulse motion-reduce:animate-none')} />
                    <span className="text-xs font-bold">
                      {isRecordingNow ? formatClock(recordingSeconds) : 'Ghi âm'}
                    </span>
                  </button>
                  <p className="text-center text-sm font-bold text-[#0F2854]">
                    {isTranscribing
                      ? 'Đang nhận diện giọng nói...'
                      : isRecordingNow
                        ? 'Nhấn để dừng ghi âm'
                        : currentTurn.followUpIndex > 0
                          ? `Nhấn để ghi âm câu trả lời phụ #${currentTurn.followUpIndex}`
                          : 'Nhấn để bắt đầu ghi âm'}
                  </p>
                  {!isRecordingNow && !isTranscribing && (
                    <p className="text-center text-xs leading-relaxed text-[#5b6b84]">
                      Nhấn nút hoặc phím{' '}
                      <kbd className="rounded bg-[#F4FBFD] px-1.5 py-0.5 font-bold text-[#0F2854]">Space</kbd>{' '}
                      để bật/tắt. Dừng để xem transcript.
                    </p>
                  )}
                </div>
              )}

              {(showTranscript || reRecording || useRecorderFallback) && (
                <div className="flex flex-col gap-2">
                  <label htmlFor="in-class-transcript" className="text-sm font-semibold text-[#0F2854]">
                    {currentTurn.followUpIndex > 0
                      ? `Transcript câu trả lời phụ #${currentTurn.followUpIndex}`
                      : 'Transcript để kiểm tra và chỉnh sửa'}
                  </label>
                  <p className="text-xs leading-relaxed text-[#5b6b84]">
                    Nội dung chỉnh sửa được lưu khi chuyển câu hoặc nộp bài.
                  </p>
                  {isTranscribing && (
                    <div
                      role="status"
                      aria-live="polite"
                      className="flex items-center gap-2 text-xs font-semibold text-[#1C4D8D]"
                    >
                      <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                      <span>Đang nhận diện giọng nói...</span>
                    </div>
                  )}
                  {(showTranscript || reRecording || useRecorderFallback) && (
                    <textarea
                      id="in-class-transcript"
                      ref={transcriptAreaRef}
                      data-transcript-fit
                      data-space-ignore="true"
                      rows={1}
                      value={draft}
                      disabled={
                        isRecordingNow ||
                        isStartingRecording ||
                        isTranscribing ||
                        submitted[turnKey] ||
                        busy ||
                        secondsLeft <= 0 ||
                        reRecording
                      }
                      onChange={(event) => {
                        fitTranscriptTextarea(event.currentTarget)
                        const nextValue = event.target.value
                        manualEditSeqRef.current += 1
                        const nextDrafts = { ...draftsRef.current, [turnKey]: nextValue }
                        draftsRef.current = nextDrafts
                        setDrafts(nextDrafts)
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
                        disabled={busy || isStartingRecording || isTranscribing}
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
                        disabled={
                          busy ||
                          isStartingRecording ||
                          isTranscribing ||
                          (!isRecordingNow && secondsLeft <= 0)
                        }
                        aria-pressed={isRecordingNow}
                        aria-label={isRecordingNow ? 'Dừng ghi âm' : 'Bắt đầu ghi âm'}
                        onClick={toggleRecording}
                        className={cn(
                          'flex size-[7.5rem] flex-col items-center justify-center gap-1 rounded-full border text-sm font-bold shadow-[0_12px_28px_rgba(28,77,141,0.16)] transition select-none disabled:opacity-60',
                          isRecordingNow
                            ? 'border-[#d97788] bg-[#b4233c] text-white shadow-[0_12px_28px_rgba(180,35,60,0.24)]'
                            : 'border-[#82aedb]/55 bg-[#F4FBFD] text-[#0F2854] hover:bg-[#BDE8F5]',
                        )}
                      >
                        <Mic
                          className={cn(
                            'size-7',
                            isRecordingNow && 'animate-pulse motion-reduce:animate-none',
                          )}
                        />
                        <span className="text-xs font-bold">
                          {isRecordingNow ? formatClock(recordingSeconds) : 'Ghi âm'}
                        </span>
                      </button>
                      <p className="text-center text-sm font-bold text-[#0F2854]">
                        {isTranscribing
                          ? 'Đang nhận diện giọng nói...'
                          : isRecordingNow
                            ? 'Nhấn để dừng ghi âm'
                            : 'Ghi âm lại câu trả lời'}
                      </p>
                      {!isRecordingNow && !isTranscribing && (
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

              {segmentWarning && (
                <div
                  role="status"
                  aria-live="polite"
                  className="flex items-start gap-2.5 rounded-xl border border-amber-300 bg-amber-50 px-3.5 py-2.5 text-xs font-semibold leading-relaxed text-amber-900"
                >
                  <AlertTriangle className="mt-0.5 size-4 shrink-0 text-amber-600" aria-hidden="true" />
                  <span>{segmentWarning}</span>
                </div>
              )}
              {(answerError || (useRecorderFallback ? recorder.error : speech.error)) && (
                <p className="text-sm leading-relaxed text-[#c2415b]">
                  {answerError || (useRecorderFallback ? recorder.error : speech.error)}
                </p>
              )}

              <div className="flex flex-wrap items-center justify-between gap-2 pt-0.5">
                <button
                  type="button"
                  data-space-ignore="true"
                  onClick={() => {
                    cancelActiveRecorder()
                    setReRecording(false)
                    setSegmentWarning('')
                    setQuestionIndex((current) => Math.max(0, current - 1))
                  }}
                  disabled={questionIndex <= 0 || isInteractionLocked || secondsLeft <= 0}
                  className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-[#4988C4] bg-white px-4 text-sm font-bold text-[#0F2854] shadow-[0_3px_8px_rgba(23,48,96,0.05)] hover:-translate-y-0.5 hover:border-[#4988C4] disabled:opacity-40 motion-reduce:transform-none"
                >
                  <ArrowLeft className="size-4" />
                  Câu trước
                </button>
                <div className="flex flex-wrap items-center justify-end gap-2">
                  {!isLast && currentTurn.followUpIndex > 0 && !draft.trim() && !submitted[turnKey] && (
                    <button
                      type="button"
                      data-space-ignore="true"
                      onClick={skipFollowUpOrQuestion}
                      disabled={isInteractionLocked || secondsLeft <= 0}
                      className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-[#4988C4] bg-white px-4 text-sm font-bold text-[#0F2854] shadow-[0_3px_8px_rgba(23,48,96,0.05)] hover:-translate-y-0.5 hover:border-[#4988C4] disabled:opacity-40 motion-reduce:transform-none"
                    >
                      Bỏ qua câu phụ
                      <ArrowRight className="size-4" />
                    </button>
                  )}
                  {!isLast && currentTurn.followUpIndex === 0 && !draft.trim() && !submitted[turnKey] && (
                    <button
                      type="button"
                      data-space-ignore="true"
                      onClick={() => {
                        cancelActiveRecorder()
                        setReRecording(false)
                        setSegmentWarning('')
                        setQuestionIndex((current) => current + 1)
                      }}
                      disabled={isInteractionLocked || secondsLeft <= 0}
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
                      disabled={isInteractionLocked || secondsLeft <= 0}
                      className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-[#1C4D8D] bg-[#EAF4FC] px-4 text-sm font-bold text-[#0F2854] transition hover:bg-[#D7EAF8] disabled:opacity-40"
                    >
                      {busy
                        ? 'Đang lưu...'
                        : hasPendingFollowUpTurns && !submitted[turnKey]
                          ? `Gửi & Câu phụ (${currentTurn.followUpIndex + 1}/${maxFollowUps})`
                          : 'Câu tiếp'}
                      <ArrowRight className="size-4" />
                    </button>
                  )}
                  {isLast && shouldRequestFollowUp && (
                    <button
                      type="button"
                      data-space-ignore="true"
                      onClick={() => void submitCurrent()}
                      disabled={isInteractionLocked || secondsLeft <= 0}
                      className={ds.outlineButton}
                    >
                      Gửi & Câu phụ
                    </button>
                  )}
                  <button
                    type="button"
                    data-space-ignore="true"
                    onClick={() => setConfirmSubmit(true)}
                    disabled={isInteractionLocked || secondsLeft <= 0}
                    className="inline-flex min-h-10 items-center justify-center rounded-xl bg-[#1C4D8D] px-5 text-sm font-bold text-white shadow-[0_8px_16px_rgba(36,94,234,0.22)] transition duration-200 hover:-translate-y-0.5 hover:bg-[#0F2854] hover:shadow-[0_12px_22px_rgba(15,40,84,0.28)] disabled:opacity-40 motion-reduce:transform-none"
                  >
                    Nộp bài
                  </button>
                </div>
              </div>
            </section>
          )
        )}
      </div>
      {(confirmSubmit || blocker.state === 'blocked') && (
        <ConfirmDialog
          title={blocker.state === 'blocked' ? 'Nộp bài và thoát phòng?' : 'Xác nhận nộp bài'}
          eyebrow={warnings.length ? 'Cảnh báo' : 'Ca kiểm tra'}
          tone={warnings.length ? 'warning' : 'info'}
          confirmLabel="Nộp bài"
          cancelLabel="Làm tiếp"
          pending={busy || leaving}
          onConfirm={blocker.state === 'blocked' ? submitAndLeave : finish}
          onCancel={() => {
            setConfirmSubmit(false)
            if (blocker.state === 'blocked') {
              persistDraftsRef.current()
              blocker.reset()
            }
          }}
        >
          <div className="space-y-3 text-sm">
            {warnings.length > 0 && (
              <ul className="list-disc space-y-1 rounded-xl border border-amber-200 bg-amber-50 py-3 pr-3 pl-7 font-medium text-amber-900">
                {warnings.map((warning) => <li key={warning}>{warning}</li>)}
              </ul>
            )}
            <p className="leading-relaxed text-[#5b6b84]">
              {warnings.length ? 'Bạn có thể làm tiếp hoặc nộp bài ngay.' : 'Bạn có chắc muốn nộp bài?'}
            </p>
          </div>
        </ConfirmDialog>
      )}
      {remoteCloseWarning && (
        <ConfirmDialog
          title={remoteCloseWarning.title}
          eyebrow="Thông báo kết thúc ca"
          tone="warning"
          confirmLabel="Đã hiểu, về danh sách ca"
          onConfirm={() => {
            const remainingCount = remoteCloseWarning.failedQuestions.length
            setRemoteCloseWarning(null)
            clearInClassExamSession()
            navigate('/in-class-exam', {
              replace: true,
              state: {
                notice: `${remainingCount} câu trả lời chưa kịp gửi lên máy chủ do ca thi đã kết thúc. Bản nháp vẫn được giữ cục bộ trên trình duyệt này.`,
              },
            })
          }}
        >
          <div className="space-y-3 text-sm">
            <p className="leading-relaxed text-[#5b6b84]">{remoteCloseWarning.message}</p>
            <div className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-amber-900">
              <p className="font-semibold">Các câu chưa kịp nộp lên máy chủ:</p>
              <ul className="mt-1.5 list-disc space-y-1 pl-5">
                {remoteCloseWarning.failedQuestions.map((label) => (
                  <li key={label}>{label}</li>
                ))}
              </ul>
            </div>
            <p className="text-xs leading-relaxed text-[#5b6b84]">
              Bản nháp trên trình duyệt không bị xóa để bạn đối chiếu hoặc báo giảng viên nếu cần.
            </p>
          </div>
        </ConfirmDialog>
      )}
    </div>
  )
}
