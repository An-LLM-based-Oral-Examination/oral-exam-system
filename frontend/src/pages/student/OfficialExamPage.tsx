import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import {
  AlertTriangle,
  CheckCircle2,
  Clock3,
  Info,
  Laptop,
  LoaderCircle,
  LockKeyhole,
  MessageSquarePlus,
  Mic2,
  Send,
  ShieldAlert,
  UploadCloud,
  Volume2,
} from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'
import { useKioskLockdown, type KioskViolationEvent } from '@/hooks/useKioskLockdown'
import { useAudioRecorder } from '@/hooks/useAudioRecorder'
import { MicrophoneRecoveryBanner } from '@/components/audio/MicrophoneRecoveryBanner'
import { getActiveKioskBridge } from '@/lib/kioskNativeBridge'
import {
  checkTranscriptRevision,
  isLikelySilentOrInvalidTranscript,
  looksLikeQuestionEcho,
} from '@/lib/transcriptGuards'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import { EmptyState } from '@/components/common/EmptyState'
import { BannerFact, PageBanner } from '@/design/ui'
import { ds } from '@/design/system'
import { kioskExamService } from '@/services/kioskExam.service'
import { transcribeSpeech } from '@/services/speech.service'
import type { KioskWorkstationTicket, WorkstationFollowUpItem } from '@/types/kiosk.types'

interface FollowUpTurnState {
  followUpIndex: number
  parentSubmissionId: string
  prompt: string
  submissionId: string | null
  recorded: boolean
}

const GENERIC_ERROR_CODES = new Set([
  'ERR_BAD_REQUEST',
  'ERR_NETWORK',
  'CONFLICT',
  'BAD_REQUEST',
  'UNPROCESSABLE',
  'FORBIDDEN',
])

function extractErrorCode(error: unknown): string {
  if (typeof error === 'object' && error !== null) {
    const rec = error as { code?: unknown; errorCode?: unknown; detail?: unknown }
    const rawCode =
      typeof rec.code === 'string' && rec.code.trim()
        ? rec.code.trim().toUpperCase()
        : typeof rec.errorCode === 'string' && rec.errorCode.trim()
          ? rec.errorCode.trim().toUpperCase()
          : ''
    if (rawCode && !GENERIC_ERROR_CODES.has(rawCode)) {
      return rawCode
    }
    if (typeof rec.detail === 'string') {
      if (/TICKET_LOCKED/i.test(rec.detail)) return 'TICKET_LOCKED'
      if (/DEVICE_IN_USE_OTHER_TICKET/i.test(rec.detail)) return 'DEVICE_IN_USE_OTHER_TICKET'
      if (/DEVICE_MISMATCH/i.test(rec.detail)) return 'DEVICE_MISMATCH'
      if (/ACTIVE_SESSION_CONFLICT/i.test(rec.detail)) return 'ACTIVE_SESSION_CONFLICT'
      if (/TranscriptEditLimitExceeded/i.test(rec.detail)) return 'TRANSCRIPT_EDIT_LIMIT_EXCEEDED'
      if (/TranscriptEditNotAllowedInVoiceOnlyMode/i.test(rec.detail)) {
        return 'VOICE_ONLY_EDIT_FORBIDDEN'
      }
    }
    if (rawCode) return rawCode
  }
  return ''
}

function mf04ErrorMessage(error: unknown) {
  const code = extractErrorCode(error)
  if (code === 'TICKET_LOCKED') {
    return 'Vé thi đã bị khóa (LOCKED). Bạn không thể tiếp tục ghi âm hoặc nộp bài. Vui lòng liên hệ Giám thị hoặc Cán bộ Khảo thí.'
  }
  if (code === 'DEVICE_MISMATCH') {
    return 'Vé thi này đã được khóa với một thiết bị BYOD khác. Vui lòng liên hệ Giám thị hoặc Cán bộ Khảo thí để mở khóa chuyển máy.'
  }
  if (code === 'ACTIVE_SESSION_CONFLICT') {
    return 'Vé thi đang có một phiên hoạt động trên thiết bị hoặc cửa sổ khác. Vui lòng đóng phiên cũ hoặc liên hệ Giám thị.'
  }
  if (code === 'DEVICE_IN_USE_OTHER_TICKET') {
    return 'Thiết bị BYOD này đang được gắn với một vé thi khác trong cùng ca thi. Vui lòng dùng đúng laptop cá nhân của bạn hoặc báo Giám thị.'
  }
  if (code === 'VOICE_ONLY_EDIT_FORBIDDEN') {
    return 'Kỳ thi ở chế độ VoiceOnly, không cho phép chỉnh sửa thủ công bản transcript.'
  }
  if (code === 'TRANSCRIPT_EDIT_LIMIT_EXCEEDED') {
    return 'Tỷ lệ chỉnh sửa transcript vượt quá ngưỡng tối đa 35% (HTTP 422). Vui lòng giữ nguyên hoặc chỉnh sửa nhỏ thuật ngữ.'
  }
  return userMessage(error, 'Không hoàn tất được thao tác phòng thi. Vui lòng thử lại.')
}

async function calculateSha256Hashes(blob: Blob): Promise<{ hex: string; base64: string }> {
  const buffer = await blob.arrayBuffer()
  const digest = await crypto.subtle.digest('SHA-256', buffer)
  const bytes = new Uint8Array(digest)
  const hex = Array.from(bytes)
    .map((byte) => byte.toString(16).padStart(2, '0'))
    .join('')
  let binary = ''
  for (let i = 0; i < bytes.length; i += 1) {
    binary += String.fromCharCode(bytes[i])
  }
  const base64 = typeof btoa === 'function' ? btoa(binary) : ''
  return { hex, base64 }
}

function formatRecordingDuration(seconds: number) {
  const safeSeconds = Math.max(0, Math.floor(seconds))
  return `${Math.floor(safeSeconds / 60).toString().padStart(2, '0')}:${(safeSeconds % 60).toString().padStart(2, '0')}`
}

function getExamQuestionImageUrl(url: string | null | undefined) {
  if (!url) return null
  if (/^https?:\/\//i.test(url)) return url
  return url.startsWith('/') ? url : `/${url}`
}

function getCurrentTimestampMs() {
  return Date.now()
}

function officialExamSessionDraftKey(userId: string, ticketId: string) {
  return `mf04_kiosk_session_draft_${userId}_${ticketId}`
}

function turnKey(questionId: string, followUpIndex: number) {
  return `${questionId}::${followUpIndex}`
}

function getBrowserAudioContextCtor(): typeof AudioContext | undefined {
  if (typeof window === 'undefined') return undefined
  return (
    window.AudioContext ??
    (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
  )
}

function OfficialExamLivePage() {
  const { user } = useAuth()
  const bridge = useMemo(() => getActiveKioskBridge(), [])
  const [ticket, setTicket] = useState<KioskWorkstationTicket | null>(null)
  const [answers, setAnswers] = useState<Record<string, string>>({})
  const [voiceBaselinesByQuestion, setVoiceBaselinesByQuestion] = useState<Record<string, string>>(
    {},
  )
  const [bufferDeadlinesByQuestion, setBufferDeadlinesByQuestion] = useState<
    Record<string, number>
  >({})
  const [revisionErrorsByQuestion, setRevisionErrorsByQuestion] = useState<
    Record<string, string | null>
  >({})
  const [revisionPercentByQuestion, setRevisionPercentByQuestion] = useState<
    Record<string, number>
  >({})
  const [nowTimestamp, setNowTimestamp] = useState<number>(() => Date.now())
  const [timestampsByQuestion, setTimestampsByQuestion] = useState<Record<string, string | null>>(
    {},
  )
  const [turnDurationsByKey, setTurnDurationsByKey] = useState<Record<string, number>>({})
  const [recordedTurnKeys, setRecordedTurnKeys] = useState<Record<string, boolean>>({})
  const [mainSubmissionIdsByQuestion, setMainSubmissionIdsByQuestion] = useState<
    Record<string, string>
  >({})
  const [followUpsByQuestion, setFollowUpsByQuestion] = useState<
    Record<string, FollowUpTurnState[]>
  >({})
  const [submittingTurnKey, setSubmittingTurnKey] = useState<string | null>(null)
  const [transcribingTurnKey, setTranscribingTurnKey] = useState<string | null>(null)
  const [recordingTurnKey, setRecordingTurnKey] = useState<string | null>(null)

  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [recordingSeconds, setRecordingSeconds] = useState(0)
  const [sealed, setSealed] = useState(false)
  const [submission, setSubmission] = useState<{
    ticketId: string
    status: string
    submittedAt: string | null
    totalSubmissions: number
    pendingSubmissions: number
    gradedSubmissions: number
    failedSubmissions: number
  } | null>(null)
  const [isSuspended, setIsSuspended] = useState(false)
  const [needsSessionRebind, setNeedsSessionRebind] = useState(false)
  const [micTested, setMicTested] = useState(false)
  const [micPeakDb, setMicPeakDb] = useState(0)
  const [isTestingMic, setIsTestingMic] = useState(false)

  const micTestTimerRef = useRef<number | null>(null)
  const micTestStreamRef = useRef<MediaStream | null>(null)
  const micTestContextRef = useRef<AudioContext | null>(null)
  const recordingStartedAtRef = useRef<number | null>(null)
  const questionRecordingStartedAtRef = useRef<number | null>(null)
  const recordedExamAudioRef = useRef<Blob | null>(null)
  const recordedTurnBlobsRef = useRef<Map<string, Blob>>(new Map())
  const [hasRecordedAudioBlob, setHasRecordedAudioBlob] = useState(false)
  const mountedRef = useRef(true)
  const questionSegmentPromisesRef = useRef<Array<Promise<string>>>([])
  const recordingVocabularyHintRef = useRef<string | undefined>(undefined)

  const stopMicTest = useCallback(() => {
    if (micTestTimerRef.current !== null) {
      window.clearInterval(micTestTimerRef.current)
      micTestTimerRef.current = null
    }
    if (micTestContextRef.current) {
      const ctx = micTestContextRef.current
      micTestContextRef.current = null
      void ctx.close().catch(() => undefined)
    }
    if (micTestStreamRef.current) {
      micTestStreamRef.current.getTracks().forEach((track) => track.stop())
      micTestStreamRef.current = null
    }
    setIsTestingMic(false)
  }, [])

  const handleQuestionSegment = useCallback((segmentBlob: Blob) => {
    const segmentPromise = transcribeSpeech(segmentBlob, {
      fileName: `question-segment-${Date.now()}.webm`,
      vocabularyHint: recordingVocabularyHintRef.current,
    })
      .then((res) => res.transcript.trim())
      .catch(() => '')
    questionSegmentPromisesRef.current.push(segmentPromise)
  }, [])

  const audioRecorder = useAudioRecorder()
  const questionRecorder = useAudioRecorder({
    segmentDurationMs: 20_000,
    onSegment: handleQuestionSegment,
  })

  const ticketRef = useRef<KioskWorkstationTicket | null>(null)
  useLayoutEffect(() => {
    ticketRef.current = ticket
  }, [ticket])

  const isInProgress = ticket?.status === 'IN_PROGRESS'
  const isSubmitted = Boolean(
    ticket && ['SUBMITTED', 'AI_GRADED', 'AUDITED', 'PUBLISHED'].includes(ticket.status),
  )
  const isTicketLockedServer = Boolean(
    ticket && (ticket.status.toUpperCase() === 'LOCKED' || ticket.isKioskLocked || isSuspended),
  )
  const isVoiceOnly = ticket?.examInputMode === 'VoiceOnly'
  const hasPerTurnFollowUp = Boolean(ticket?.hasFollowUp)
  const questions = useMemo(() => ticket?.questions ?? [], [ticket?.questions])

  const syncFromServerRef = useRef<
    | ((state: {
        violationCount: number
        maxViolations?: number
        lockThreshold?: number
        remainingStrikes?: number
        isLocked: boolean
      }) => void)
    | null
  >(null)

  const handleKioskViolation = useCallback(
    async (event: KioskViolationEvent) => {
      const activeTicket = ticketRef.current
      if (!activeTicket) return
      try {
        const result = await kioskExamService.logAntiCheat(
          {
            ticketId: activeTicket.id,
            shiftId: activeTicket.shiftId,
            eventType: event.eventType,
            severity: event.severity,
            details: event.details,
            clientEventId: event.clientEventId,
          },
          bridge.getKioskHeaders(activeTicket.id),
        )
        syncFromServerRef.current?.({
          violationCount: result.violationCount,
          maxViolations: result.maxViolations,
          lockThreshold: result.lockThreshold,
          remainingStrikes: result.remainingStrikes,
          isLocked: result.isLocked,
        })
        if (result.isLocked) {
          audioRecorder.cancelRecording()
          setIsSuspended(true)
          setTicket((current) =>
            current
              ? {
                  ...current,
                  status: 'LOCKED',
                  isKioskLocked: true,
                  violationCount: result.violationCount,
                  maxViolations: result.maxViolations,
                  lockThreshold: result.lockThreshold,
                  remainingStrikes: 0,
                }
              : current,
          )
          setError(
            `Vé thi đã bị khóa tự động do vượt quá giới hạn vi phạm Kiosk (${result.violationCount}/${result.lockThreshold}). Vui lòng liên hệ Giám thị hoặc Cán bộ Khảo thí.`,
          )
        } else {
          setTicket((current) =>
            current
              ? {
                  ...current,
                  violationCount: result.violationCount,
                  maxViolations: result.maxViolations,
                  lockThreshold: result.lockThreshold,
                  remainingStrikes: result.remainingStrikes,
                }
              : current,
          )
          setNotice(
            `Cảnh báo Kiosk (${result.violationCount}/${result.maxViolations}): ${event.details} Còn ${result.remainingStrikes} lần trước khi tự động khóa vé.`,
          )
        }
      } catch (requestError) {
        const code = extractErrorCode(requestError)
        if (code === 'ACTIVE_SESSION_CONFLICT' || code === 'DEVICE_MISMATCH') {
          setNeedsSessionRebind(true)
        }
        setError(`Không thể ghi nhận sự kiện chống gian lận: ${mf04ErrorMessage(requestError)}`)
      }
    },
    [audioRecorder, bridge],
  )

  const kiosk = useKioskLockdown({
    enabled: Boolean(isInProgress && audioRecorder.isRecording && !isSuspended),
    onViolation: handleKioskViolation,
  })

  const isTicketLocked = isTicketLockedServer || kiosk.isLocked
  const requiresByodCheckIn = Boolean(
    ticket &&
      !isSubmitted &&
      !isTicketLocked &&
      (!isInProgress ||
        !ticket.deviceId ||
        (ticket.isExamPublished && questions.length === 0) ||
        needsSessionRebind),
  )
  const canViewQuestions = Boolean(
    !requiresByodCheckIn &&
      !isTicketLocked &&
      (audioRecorder.isRecording || hasRecordedAudioBlob || isSubmitted),
  )

  useLayoutEffect(() => {
    syncFromServerRef.current = kiosk.syncFromServer
  }, [kiosk.syncFromServer])

  const transcribeQuestionAudio = useCallback(
    async (questionAudio: Blob, fileName: string, vocabularyHint?: string) => {
      await new Promise<void>((resolve) => window.setTimeout(resolve, 50))
      const preTranscribedPromises = [...questionSegmentPromisesRef.current]
      questionSegmentPromisesRef.current = []

      let text = ''
      let timestampsJson: string | null = null
      if (preTranscribedPromises.length > 0) {
        const tailPromise =
          questionAudio.size >= 2_048
            ? transcribeSpeech(questionAudio, { fileName, vocabularyHint })
                .then((res) => {
                  timestampsJson = res.timestampsJson ?? null
                  return res.transcript.trim()
                })
                .catch(() => '')
            : Promise.resolve('')
        const allParts = await Promise.all([...preTranscribedPromises, tailPromise])
        text = allParts.filter((part) => part.length > 0).join(' ').trim()
      }

      if (!text && questionAudio.size > 0) {
        const sttResponse = await transcribeSpeech(questionAudio, { fileName, vocabularyHint })
        text = sttResponse.transcript.trim()
        timestampsJson = sttResponse.timestampsJson ?? null
      }

      return { text, timestampsJson }
    },
    [],
  )

  const buildQuestionVocabularyHint = useCallback(
    (questionId?: string | null): string | undefined => {
      const targetQuestion = questionId
        ? questions.find((q) => q.questionId === questionId)
        : questions[0]
      const parts = [
        ticket?.sessionTitle?.trim(),
        ticket?.examSetCode?.trim(),
        targetQuestion?.title?.trim(),
        targetQuestion?.content?.trim(),
      ].filter((part): part is string => Boolean(part))
      return parts.length > 0 ? parts.join(' - ').slice(0, 320) : undefined
    },
    [questions, ticket?.examSetCode, ticket?.sessionTitle],
  )

  const { syncFromServer } = kiosk

  const hydrateTicketWithServerState = useCallback(
    async (baseTicket: KioskWorkstationTicket): Promise<KioskWorkstationTicket> => {
      let hydrated = baseTicket
      if (typeof kioskExamService.getTicketState === 'function') {
        try {
          const state = await kioskExamService.getTicketState(
            baseTicket.id,
            bridge.getKioskHeaders(baseTicket.id),
          )
          hydrated = {
            ...baseTicket,
            status: state.status,
            isExamPublished: state.isExamPublished,
            isKioskLocked: state.isKioskLocked,
            violationCount: state.violationCount,
            maxViolations: state.maxViolations,
            lockThreshold: state.lockThreshold,
            remainingStrikes: state.remainingStrikes,
            serverNowUtc: state.serverNowUtc,
            deviceId: state.deviceId ?? baseTicket.deviceId,
            deviceLockedAt: state.deviceLockedAt ?? baseTicket.deviceLockedAt,
            deviceUnlockCount: state.deviceUnlockCount,
            followUps: state.followUps.length > 0 ? state.followUps : baseTicket.followUps,
          }
        } catch {
          // Giữ dữ liệu đã có từ getCurrentTicket nếu endpoint state chưa sẵn sàng.
        }
      }

      const lockedFromServer =
        hydrated.isKioskLocked || hydrated.status.toUpperCase() === 'LOCKED'
      syncFromServer({
        violationCount: hydrated.violationCount,
        maxViolations: hydrated.maxViolations,
        lockThreshold: hydrated.lockThreshold,
        remainingStrikes: hydrated.remainingStrikes,
        isLocked: lockedFromServer,
      })
      setIsSuspended(lockedFromServer)

      if (hydrated.followUps.length > 0) {
        const nextRecordedKeys: Record<string, boolean> = {}
        const nextMainSubmissionIds: Record<string, string> = {}
        const byQuestion = new Map<string, WorkstationFollowUpItem[]>()

        for (const item of hydrated.followUps) {
          const list = byQuestion.get(item.examQuestionId) ?? []
          list.push(item)
          byQuestion.set(item.examQuestionId, list)
        }

        const groupedFollowUps: Record<string, FollowUpTurnState[]> = {}
        for (const [questionId, items] of byQuestion.entries()) {
          const sortedItems = [...items].sort((a, b) => a.followUpIndex - b.followUpIndex)
          const recordedIndices = new Set<number>()
          const turns: FollowUpTurnState[] = []

          for (const item of sortedItems) {
            const isSaved = Boolean(
              item.audioObjectKey ||
                item.audioR2Url ||
                item.audioHashSha256 ||
                item.transcriptWhisper ||
                item.submissionId,
            )
            if (item.followUpIndex === 0) {
              if (isSaved) {
                nextRecordedKeys[turnKey(questionId, 0)] = true
                nextMainSubmissionIds[questionId] = item.submissionId
                recordedIndices.add(0)
              }
            } else if (item.followUpIndex > 0) {
              if (isSaved) {
                nextRecordedKeys[turnKey(questionId, item.followUpIndex)] = true
                recordedIndices.add(item.followUpIndex)
              }
              turns.push({
                followUpIndex: item.followUpIndex,
                parentSubmissionId:
                  item.parentSubmissionId ?? nextMainSubmissionIds[questionId] ?? '',
                prompt: item.followUpPrompt ?? '',
                submissionId: item.submissionId,
                recorded: isSaved,
              })
            }
          }

          for (const item of sortedItems) {
            const generatedPrompt = item.generatedFollowUpPrompt?.trim()
            if (!generatedPrompt) continue
            const nextIndex = item.followUpIndex + 1
            if (recordedIndices.has(nextIndex)) continue
            if (turns.some((turn) => turn.followUpIndex === nextIndex)) continue
            turns.push({
              followUpIndex: nextIndex,
              parentSubmissionId: item.submissionId,
              prompt: generatedPrompt,
              submissionId: null,
              recorded: false,
            })
          }

          if (turns.length > 0) {
            groupedFollowUps[questionId] = turns.sort(
              (a, b) => a.followUpIndex - b.followUpIndex,
            )
          }
        }

        if (Object.keys(nextRecordedKeys).length > 0) {
          setRecordedTurnKeys((current) => ({ ...current, ...nextRecordedKeys }))
        }
        if (Object.keys(nextMainSubmissionIds).length > 0) {
          setMainSubmissionIdsByQuestion((current) => ({ ...current, ...nextMainSubmissionIds }))
        }
        if (Object.keys(groupedFollowUps).length > 0) {
          setFollowUpsByQuestion((current) => ({ ...current, ...groupedFollowUps }))
        }
      }

      return hydrated
    },
    [bridge, syncFromServer],
  )

  useEffect(() => {
    mountedRef.current = true
    const timer = window.setTimeout(() => {
      void (async () => {
        setLoading(true)
        setError(null)
        try {
          const currentTicket = await kioskExamService.getCurrentTicket(bridge.getKioskHeaders())
          const hydrated = await hydrateTicketWithServerState(currentTicket)
          if (!mountedRef.current) return
          setTicket(hydrated)

          if (hydrated.status === 'IN_PROGRESS' && user?.id) {
            const parsedDraft = bridge.loadDraft<{
              answers?: Record<string, string>
              timestamps?: Record<string, string | null>
              voiceBaselines?: Record<string, string>
              bufferDeadlines?: Record<string, number>
            }>(officialExamSessionDraftKey(user.id, hydrated.id))
            if (parsedDraft) {
              if (parsedDraft.answers && typeof parsedDraft.answers === 'object') {
                setAnswers(parsedDraft.answers)
              }
              if (parsedDraft.timestamps && typeof parsedDraft.timestamps === 'object') {
                setTimestampsByQuestion(parsedDraft.timestamps)
              }
              if (parsedDraft.voiceBaselines && typeof parsedDraft.voiceBaselines === 'object') {
                setVoiceBaselinesByQuestion(parsedDraft.voiceBaselines)
              }
              if (parsedDraft.bufferDeadlines && typeof parsedDraft.bufferDeadlines === 'object') {
                setBufferDeadlinesByQuestion(parsedDraft.bufferDeadlines)
              }
            }
          }
        } catch (requestError) {
          if (mountedRef.current) setError(mf04ErrorMessage(requestError))
        } finally {
          if (mountedRef.current) setLoading(false)
        }
      })()
    }, 0)

    return () => {
      window.clearTimeout(timer)
      mountedRef.current = false
      stopMicTest()
    }
  }, [bridge, hydrateTicketWithServerState, stopMicTest, user?.id])

  useLayoutEffect(() => {
    if (!ticket || ticket.status !== 'IN_PROGRESS' || !user?.id) return
    bridge.saveDraft(officialExamSessionDraftKey(user.id, ticket.id), {
      answers,
      timestamps: timestampsByQuestion,
      voiceBaselines: voiceBaselinesByQuestion,
      bufferDeadlines: bufferDeadlinesByQuestion,
    })
  }, [
    answers,
    bridge,
    bufferDeadlinesByQuestion,
    timestampsByQuestion,
    ticket,
    user?.id,
    voiceBaselinesByQuestion,
  ])

  useEffect(() => {
    if (!audioRecorder.isRecording) return
    recordingStartedAtRef.current ??= Date.now()
    const timer = window.setInterval(() => {
      const now = Date.now()
      setNowTimestamp(now)
      if (recordingStartedAtRef.current) {
        setRecordingSeconds(Math.floor((now - recordingStartedAtRef.current) / 1000))
      }
    }, 1000)
    return () => window.clearInterval(timer)
  }, [audioRecorder.isRecording])

  useEffect(() => {
    if (!isInProgress || isSubmitted || isVoiceOnly) return
    const hasActiveBuffer = Object.values(bufferDeadlinesByQuestion).some(
      (deadline) => deadline > Date.now(),
    )
    if (!hasActiveBuffer) return
    const timer = window.setInterval(() => {
      setNowTimestamp(Date.now())
    }, 1000)
    return () => window.clearInterval(timer)
  }, [bufferDeadlinesByQuestion, isInProgress, isSubmitted, isVoiceOnly])

  const refreshTicket = async () => {
    setBusy(true)
    setError(null)
    try {
      const headers = bridge.getKioskHeaders(ticketRef.current?.id)
      const currentTicket = await kioskExamService.getCurrentTicket(headers)
      const hydrated = await hydrateTicketWithServerState(currentTicket)
      setTicket(hydrated)
      if (hydrated.status !== 'IN_PROGRESS') setRecordingSeconds(0)
    } catch (requestError) {
      setError(mf04ErrorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  const handleCheckIn = async () => {
    if (!ticket) return
    setBusy(true)
    setError(null)
    try {
      const checkInRes = await kioskExamService.checkInTicket(
        ticket.id,
        {
          deviceId: bridge.getDeviceId(),
          kioskVersion: bridge.getKioskVersion(),
        },
        bridge.getKioskHeaders(ticket.id),
      )
      if (checkInRes.activeSessionToken) {
        bridge.setSessionToken(ticket.id, checkInRes.activeSessionToken)
      }
      setNeedsSessionRebind(false)
      await refreshTicket()
      setNotice(
        'Điểm danh nhận máy BYOD thành công. Hãy kiểm tra microphone (≥ 60dB) và bắt đầu ghi âm bài thi.',
      )
    } catch (requestError) {
      const code = extractErrorCode(requestError)
      if (code === 'ACTIVE_SESSION_CONFLICT' || code === 'DEVICE_MISMATCH') {
        setNeedsSessionRebind(true)
      }
      setError(mf04ErrorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  const handleTestMicrophone = async () => {
    if (!ticket || !isInProgress || isSubmitted || busy) return
    if (isTestingMic) {
      stopMicTest()
      return
    }
    setError(null)
    const AudioContextCtor = getBrowserAudioContextCtor()

    if (!AudioContextCtor || !navigator.mediaDevices?.getUserMedia) {
      setMicTested(false)
      setMicPeakDb(0)
      setError(
        'Trình duyệt hoặc thiết bị không hỗ trợ Web Audio API / Microphone (AudioContext không khả dụng). Không thể bỏ qua bước kiểm tra microphone ≥ 60dB — vui lòng kiểm tra thiết bị hoặc liên hệ Giám thị.',
      )
      return
    }

    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          channelCount: 1,
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true,
        },
      })
      micTestStreamRef.current = stream
      const context = new AudioContextCtor()
      micTestContextRef.current = context
      if (context.state === 'suspended') {
        await context.resume().catch(() => undefined)
      }
      const source = context.createMediaStreamSource(stream)
      const analyser = context.createAnalyser()
      analyser.fftSize = 1024
      analyser.smoothingTimeConstant = 0.25
      source.connect(analyser)
      const buffer = new Float32Array(
        new ArrayBuffer(analyser.fftSize * Float32Array.BYTES_PER_ELEMENT),
      )

      setIsTestingMic(true)
      setNotice('Hãy nói thử vào microphone trong 30 giây để kiểm tra ngưỡng âm thanh ≥ 60dB...')
      const startedAt = Date.now()

      micTestTimerRef.current = window.setInterval(() => {
        analyser.getFloatTimeDomainData(buffer)
        let sumSquares = 0
        for (let i = 0; i < buffer.length; i += 1) {
          const sample = buffer[i]
          sumSquares += sample * sample
        }
        const rms = Math.sqrt(sumSquares / buffer.length)
        const estimatedDb =
          rms > 0.0001
            ? Math.min(100, Math.max(30, Math.round(94 + 20 * Math.log10(rms / 0.25))))
            : 30
        setMicPeakDb((prev) => Math.max(prev, estimatedDb))

        if (estimatedDb >= 60) {
          setMicTested(true)
          stopMicTest()
          setNotice(
            `Kiểm tra Microphone đạt chuẩn (${estimatedDb}dB ≥ 60dB). Hãy bấm "Bắt đầu ghi âm & Vào chế độ Kiosk Lockdown" để mở đề thi.`,
          )
          return
        }

        if (Date.now() - startedAt >= 30_000) {
          stopMicTest()
          setError(
            'Hết 30 giây kiểm tra nhưng cường độ âm thanh chưa đạt 60dB. Vui lòng kiểm tra lại microphone và nói rõ hơn.',
          )
        }
      }, 100)
    } catch (micError) {
      stopMicTest()
      setError(mf04ErrorMessage(micError))
    }
  }

  const handleStartRecording = async () => {
    if (!ticket || !isInProgress || isSubmitted || busy || kiosk.isLocked) return
    const AudioContextCtor = getBrowserAudioContextCtor()
    if (!AudioContextCtor || !navigator.mediaDevices?.getUserMedia) {
      setError(
        'Thiết bị không hỗ trợ ghi âm Web Audio (AudioContext/getUserMedia không khả dụng). Vui lòng liên hệ Giám thị.',
      )
      return
    }
    if (!micTested) {
      setError(
        'Vui lòng bấm "Kiểm tra Microphone (≥ 60dB)" và đạt ngưỡng âm thanh trước khi bắt đầu ghi âm bài thi.',
      )
      return
    }
    stopMicTest()
    setError(null)
    try {
      await bridge.enterFullscreen()
      await audioRecorder.startRecording()
      recordingStartedAtRef.current = Date.now()
      setRecordingSeconds(0)
      setNotice(
        'Đang ghi âm toàn bộ bài thi trong chế độ Kiosk Lockdown. Không rời khỏi màn hình phòng thi.',
      )
    } catch (recordingError) {
      setError(mf04ErrorMessage(recordingError))
    }
  }

  const handleToggleTurnVoice = async (
    questionId: string,
    questionNumber: number,
    followUpIndex: number,
  ) => {
    if (!ticket || !isInProgress || !audioRecorder.isRecording || isSubmitted || busy) return
    const key = turnKey(questionId, followUpIndex)
    const label =
      followUpIndex === 0
        ? `Câu ${questionNumber}`
        : `Câu ${questionNumber} (Câu phụ #${followUpIndex})`
    setError(null)

    if (recordingTurnKey === key && questionRecorder.isRecording) {
      setTranscribingTurnKey(key)
      const startedAtMs = questionRecordingStartedAtRef.current
      questionRecordingStartedAtRef.current = null
      try {
        const questionAudio = await questionRecorder.stopRecording()
        const stoppedAtMs = getCurrentTimestampMs()
        const questionRecSeconds = startedAtMs
          ? Math.max(1, Math.round((stoppedAtMs - startedAtMs) / 1000))
          : 1
        setRecordingTurnKey(null)
        recordedTurnBlobsRef.current.set(key, questionAudio)
        setTurnDurationsByKey((current) => ({ ...current, [key]: questionRecSeconds }))

        setNotice(`Đang bóc băng giọng nói ${label}...`)
        const { text, timestampsJson } = await transcribeQuestionAudio(
          questionAudio,
          `question-${questionNumber}-fu${followUpIndex}-${questionId}.webm`,
          buildQuestionVocabularyHint(questionId),
        )
        const targetQuestion = questions.find((q) => q.questionId === questionId) ?? {
          title: '',
          content: '',
        }
        if (isLikelySilentOrInvalidTranscript(text, questionRecSeconds)) {
          setError(
            `Không nhận diện được giọng nói hợp lệ cho ${label}. Vui lòng nói rõ vào microphone và thu âm lại.`,
          )
          return
        }
        if (
          followUpIndex === 0 &&
          looksLikeQuestionEcho(text, {
            title: targetQuestion.title || '',
            content: targetQuestion.content || '',
          })
        ) {
          setError(
            `Bản thu âm ${label} chỉ lặp lại nội dung đề thi hoặc tiếng vọng. Vui lòng thu âm lại câu trả lời của bạn.`,
          )
          return
        }

        const storageKey = followUpIndex === 0 ? questionId : key
        setVoiceBaselinesByQuestion((current) => ({ ...current, [storageKey]: text }))
        setAnswers((current) => ({ ...current, [storageKey]: text }))
        setRevisionErrorsByQuestion((current) => ({ ...current, [storageKey]: null }))
        setRevisionPercentByQuestion((current) => ({ ...current, [storageKey]: 0 }))
        setTimestampsByQuestion((current) => ({
          ...current,
          [storageKey]: timestampsJson,
        }))
        if (!isVoiceOnly) {
          const bufferSeconds = Math.min(300, Math.max(10, ticket.transcriptBufferSeconds || 60))
          const currentNow = getCurrentTimestampMs()
          const deadline = currentNow + bufferSeconds * 1000
          setBufferDeadlinesByQuestion((current) => ({ ...current, [storageKey]: deadline }))
          setNowTimestamp(currentNow)
        }
        setNotice(`Đã ghi nhận bản bóc băng giọng nói cho ${label}.`)
      } catch (sttError) {
        setRecordingTurnKey(null)
        setError(mf04ErrorMessage(sttError))
      } finally {
        setTranscribingTurnKey(null)
      }
      return
    }

    if (questionRecorder.isRecording) {
      questionRecorder.cancelRecording()
    }
    questionSegmentPromisesRef.current = []
    recordingVocabularyHintRef.current = buildQuestionVocabularyHint(questionId)
    try {
      await questionRecorder.startRecording()
      questionRecordingStartedAtRef.current = getCurrentTimestampMs()
      setRecordingTurnKey(key)
      setNotice(`Đang thu âm trả lời cho ${label}...`)
    } catch (recError) {
      setError(mf04ErrorMessage(recError))
    }
  }

  const handleSubmitPerTurnAnswer = async (
    questionId: string,
    questionNumber: number,
    followUpIndex: number,
    parentSubmissionId: string | null,
  ) => {
    if (!ticket || !isInProgress || isSubmitted || submittingTurnKey) return
    const key = turnKey(questionId, followUpIndex)
    const storageKey = followUpIndex === 0 ? questionId : key
    const turnAudio = recordedTurnBlobsRef.current.get(key)
    const baseline = (voiceBaselinesByQuestion[storageKey] ?? '').trim()
    const currentText = (answers[storageKey] ?? baseline).trim()

    if (!turnAudio) {
      setError('Vui lòng thu âm câu trả lời trước khi tải lên R2 và lưu lượt trả lời.')
      return
    }

    const isManuallyEdited = !isVoiceOnly && Boolean(baseline && currentText !== baseline)
    if (!isVoiceOnly && baseline) {
      const check = checkTranscriptRevision(baseline, currentText)
      if (!check.isAllowed) {
        const pct = Math.round(check.wordChangeRatio * 100)
        setRevisionErrorsByQuestion((current) => ({
          ...current,
          [storageKey]: `Tỷ lệ chỉnh sửa (${pct}%) vượt giới hạn tối đa 35%. Vui lòng giảm mức chỉnh sửa trước khi lưu.`,
        }))
        setError(
          `Tỷ lệ chỉnh sửa transcript (${pct}%) vượt quá ngưỡng tối đa 35%. Không thể lưu lượt trả lời.`,
        )
        return
      }
    }

    setSubmittingTurnKey(key)
    setError(null)
    const headers = bridge.getKioskHeaders(ticket.id)
    try {
      const { hex, base64 } = await calculateSha256Hashes(turnAudio)
      const authorization = await kioskExamService.authorizeQuestionAudioUpload(
        ticket.id,
        questionId,
        {
          followUpIndex,
          expectedSha256Base64: base64 || null,
        },
        headers,
      )
      await kioskExamService.uploadAudio(authorization, turnAudio)

      const saved = await kioskExamService.submitQuestionAnswer(
        ticket.id,
        questionId,
        {
          followUpIndex,
          parentSubmissionId,
          audioObjectKey: authorization.objectKey,
          audioHashSha256: hex,
          initialVoiceTranscript: baseline || null,
          transcriptWhisper: isVoiceOnly ? baseline : currentText,
          timestampsWhisper: timestampsByQuestion[storageKey] ?? null,
          timeSpentSeconds: turnDurationsByKey[key] ?? 1,
          isManuallyEdited,
        },
        headers,
      )

      setRecordedTurnKeys((current) => ({ ...current, [key]: true }))
      if (followUpIndex === 0) {
        setMainSubmissionIdsByQuestion((current) => ({
          ...current,
          [questionId]: saved.submissionId,
        }))
      } else {
        setFollowUpsByQuestion((current) => ({
          ...current,
          [questionId]: (current[questionId] ?? []).map((item) =>
            item.followUpIndex === followUpIndex
              ? { ...item, submissionId: saved.submissionId, recorded: true }
              : item,
          ),
        }))
      }

      if (saved.canRequestFollowUp && ticket.hasFollowUp) {
        const nextFollowUp = await kioskExamService.requestNextFollowUp(
          ticket.id,
          saved.submissionId,
          headers,
        )
        if (nextFollowUp.hasMoreFollowUp && nextFollowUp.followUpPrompt) {
          setFollowUpsByQuestion((current) => {
            const existing = current[questionId] ?? []
            if (existing.some((item) => item.followUpIndex === nextFollowUp.followUpIndex)) {
              return current
            }
            return {
              ...current,
              [questionId]: [
                ...existing,
                {
                  followUpIndex: nextFollowUp.followUpIndex,
                  parentSubmissionId: nextFollowUp.parentSubmissionId || saved.submissionId,
                  prompt: nextFollowUp.followUpPrompt!,
                  submissionId: null,
                  recorded: false,
                },
              ],
            }
          })
          setNotice(
            `Đã niêm phong audio Câu ${questionNumber} và sinh câu hỏi phụ #${nextFollowUp.followUpIndex}. Hãy trả lời câu hỏi phụ ngay bên dưới.`,
          )
        } else {
          setNotice(
            `Đã niêm phong audio Câu ${questionNumber}. Không có câu hỏi phụ bổ sung — bạn có thể chuyển sang câu tiếp theo.`,
          )
        }
      } else {
        setNotice(`Đã niêm phong audio lượt trả lời Câu ${questionNumber} lên Cloudflare R2.`)
      }
    } catch (submitError) {
      const code = extractErrorCode(submitError)
      if (code === 'TICKET_LOCKED') {
        if (audioRecorder.isRecording) audioRecorder.cancelRecording()
        if (questionRecorder.isRecording) questionRecorder.cancelRecording()
        setIsSuspended(true)
        setTicket((current) =>
          current
            ? {
                ...current,
                status: 'LOCKED',
                isKioskLocked: true,
                remainingStrikes: 0,
              }
            : current,
        )
      } else if (code === 'ACTIVE_SESSION_CONFLICT' || code === 'DEVICE_MISMATCH') {
        setNeedsSessionRebind(true)
      }
      setError(mf04ErrorMessage(submitError))
    } finally {
      setSubmittingTurnKey(null)
    }
  }

  const handleSubmit = useCallback(async () => {
    if (
      !ticket ||
      !isInProgress ||
      (!audioRecorder.isRecording && !recordedExamAudioRef.current) ||
      busy
    ) {
      return
    }
    setBusy(true)
    setError(null)
    const headers = bridge.getKioskHeaders(ticket.id)
    try {
      const effectiveAnswers = { ...answers }
      const effectiveBaselines = { ...voiceBaselinesByQuestion }
      const effectiveTimestamps = { ...timestampsByQuestion }

      if (recordingTurnKey && questionRecorder.isRecording) {
        const [activeQId, rawFu] = recordingTurnKey.split('::')
        const activeFuIndex = Number(rawFu) || 0
        const storageKey = activeFuIndex === 0 ? activeQId : recordingTurnKey
        const questionRecSeconds = questionRecordingStartedAtRef.current
          ? Math.max(1, Math.round((Date.now() - questionRecordingStartedAtRef.current) / 1000))
          : 0
        questionRecordingStartedAtRef.current = null
        const questionAudio = await questionRecorder.stopRecording()
        setRecordingTurnKey(null)
        const { text, timestampsJson } = await transcribeQuestionAudio(
          questionAudio,
          `question-${activeQId}.webm`,
          buildQuestionVocabularyHint(activeQId),
        )
        const targetQuestion = questions.find((q) => q.questionId === activeQId) ?? {
          title: '',
          content: '',
        }
        const isValidVoice =
          !isLikelySilentOrInvalidTranscript(text, questionRecSeconds) &&
          !looksLikeQuestionEcho(text, {
            title: targetQuestion.title || '',
            content: targetQuestion.content || '',
          })
        if (isValidVoice) {
          effectiveBaselines[storageKey] = text
          effectiveAnswers[storageKey] = text
          effectiveTimestamps[storageKey] = timestampsJson
          setVoiceBaselinesByQuestion((current) => ({ ...current, [storageKey]: text }))
          setAnswers((current) => ({ ...current, [storageKey]: text }))
          setTimestampsByQuestion((current) => ({
            ...current,
            [storageKey]: timestampsJson,
          }))
        }
      }

      const manuallyEditedMap: Record<string, boolean> = {}
      for (const question of questions) {
        const qId = question.questionId
        const baseline = effectiveBaselines[qId]?.trim() ?? ''
        const candidateText = effectiveAnswers[qId]?.trim() ?? ''
        if (isVoiceOnly) {
          effectiveAnswers[qId] = baseline || candidateText
          manuallyEditedMap[qId] = false
        } else if (!baseline) {
          effectiveAnswers[qId] = ''
          manuallyEditedMap[qId] = false
        } else {
          const revisionCheck = checkTranscriptRevision(baseline, candidateText)
          effectiveAnswers[qId] = revisionCheck.isAllowed ? candidateText : baseline
          manuallyEditedMap[qId] = revisionCheck.isAllowed && candidateText !== baseline
        }
      }

      const audio = audioRecorder.isRecording
        ? await audioRecorder.stopRecording()
        : recordedExamAudioRef.current!
      recordedExamAudioRef.current = audio
      setHasRecordedAudioBlob(true)

      const hasPerTurnRecords = Object.values(recordedTurnKeys).some(Boolean)
      if (!hasPerTurnFollowUp || !hasPerTurnRecords) {
        const { hex, base64 } = await calculateSha256Hashes(audio)
        const authorization = await kioskExamService.createAudioUploadAuthorization(
          ticket.id,
          base64 || null,
          headers,
        )
        await kioskExamService.uploadAudio(authorization, audio)
        await kioskExamService.sealAudio(
          ticket.id,
          {
            objectKey: authorization.objectKey,
            sha256: hex,
          },
          headers,
        )

        const hasAnyQuestionTranscript = questions.some((question) =>
          Boolean(effectiveAnswers[question.questionId]?.trim()),
        )
        let sttTranscript: string | null = null
        let sttTimestamps: string | null = null
        if (!hasAnyQuestionTranscript && questions.length > 0) {
          setNotice('Đang bóc băng giọng nói trước khi niêm phong nộp bài...')
          const fileName = authorization.objectKey.split('/').pop() || 'exam-audio.webm'
          const sttResponse = await transcribeSpeech(audio, {
            fileName,
            vocabularyHint: buildQuestionVocabularyHint(),
          })
          sttTranscript = sttResponse.transcript.trim()
          sttTimestamps = sttResponse.timestampsJson ?? null
          const firstQuestionId = questions[0]?.questionId
          if (firstQuestionId) {
            setVoiceBaselinesByQuestion((current) => ({
              ...current,
              [firstQuestionId]: sttTranscript ?? '',
            }))
            setAnswers((current) => ({
              ...current,
              [firstQuestionId]: sttTranscript ?? '',
            }))
          }
        }

        const payload = questions.map((question, index) => ({
          examQuestionId: question.questionId,
          initialVoiceTranscript:
            effectiveBaselines[question.questionId]?.trim() ||
            (!hasAnyQuestionTranscript && index === 0 ? sttTranscript : null) ||
            null,
          transcriptWhisper:
            effectiveAnswers[question.questionId]?.trim() ||
            (!hasAnyQuestionTranscript && index === 0 ? sttTranscript : null) ||
            '',
          timestampsWhisper:
            effectiveTimestamps[question.questionId] ??
            (!hasAnyQuestionTranscript && index === 0 ? sttTimestamps : null),
          timeSpentSeconds: recordingSeconds,
          isManuallyEdited: Boolean(manuallyEditedMap[question.questionId]),
        }))
        const submitted = await kioskExamService.submitExam(ticket.id, payload, headers)
        setSealed(true)
        recordedExamAudioRef.current = null
        setHasRecordedAudioBlob(false)
        if (user?.id) {
          bridge.clearDraft(officialExamSessionDraftKey(user.id, ticket.id))
        }
        bridge.clearSessionToken(ticket.id)
        setSubmission({
          ticketId: submitted.ticketId,
          status: submitted.status,
          submittedAt: submitted.submittedAt,
          totalSubmissions: submitted.submissionCount,
          pendingSubmissions: submitted.submissionCount,
          gradedSubmissions: 0,
          failedSubmissions: 0,
        })
        setTicket((current) => (current ? { ...current, status: submitted.status } : current))
        setNotice('Bài thi đã được lưu an toàn và chuyển vào hệ thống chấm điểm.')
      } else {
        const submitted = await kioskExamService.submitExam(ticket.id, [], headers, {
          finalizePerTurn: true,
        })
        setSealed(true)
        recordedExamAudioRef.current = null
        setHasRecordedAudioBlob(false)
        if (user?.id) {
          bridge.clearDraft(officialExamSessionDraftKey(user.id, ticket.id))
        }
        bridge.clearSessionToken(ticket.id)
        setSubmission({
          ticketId: submitted.ticketId,
          status: submitted.status,
          submittedAt: submitted.submittedAt,
          totalSubmissions: submitted.submissionCount,
          pendingSubmissions: submitted.submissionCount,
          gradedSubmissions: 0,
          failedSubmissions: 0,
        })
        setTicket((current) => (current ? { ...current, status: submitted.status } : current))
        setNotice('Toàn bộ các lượt trả lời & câu phụ đã được chốt sổ và niêm phong an toàn.')
      }
    } catch (requestError) {
      const code = extractErrorCode(requestError)
      if (code === 'TICKET_LOCKED') {
        if (audioRecorder.isRecording) audioRecorder.cancelRecording()
        if (questionRecorder.isRecording) questionRecorder.cancelRecording()
        setIsSuspended(true)
        setTicket((current) =>
          current
            ? {
                ...current,
                status: 'LOCKED',
                isKioskLocked: true,
                remainingStrikes: 0,
              }
            : current,
        )
        setError(mf04ErrorMessage(requestError))
      } else {
        if (code === 'ACTIVE_SESSION_CONFLICT' || code === 'DEVICE_MISMATCH') {
          setNeedsSessionRebind(true)
        }
        setError(
          `${mf04ErrorMessage(requestError)} (Bản ghi âm và bài làm đã được giữ tạm trong phiên trình duyệt; hệ thống sẽ tự động nộp lại ngay khi có mạng hoặc khi bấm Nộp bài).`,
        )
      }
    } finally {
      setBusy(false)
    }
  }, [
    answers,
    audioRecorder,
    bridge,
    buildQuestionVocabularyHint,
    busy,
    hasPerTurnFollowUp,
    isInProgress,
    isVoiceOnly,
    questionRecorder,
    questions,
    recordedTurnKeys,
    recordingSeconds,
    recordingTurnKey,
    ticket,
    timestampsByQuestion,
    transcribeQuestionAudio,
    user?.id,
    voiceBaselinesByQuestion,
  ])

  useEffect(() => {
    const onOnline = () => {
      if (recordedExamAudioRef.current && ticket?.status === 'IN_PROGRESS' && !busy) {
        setNotice('Đã kết nối mạng trở lại. Đang tự động nộp bản ghi âm đã lưu...')
        void handleSubmit()
      }
    }
    window.addEventListener('online', onOnline)
    return () => window.removeEventListener('online', onOnline)
  }, [busy, handleSubmit, ticket?.status])

  const refreshSubmission = async () => {
    if (!ticket) return
    setBusy(true)
    try {
      setSubmission(
        await kioskExamService.getSubmissionStatus(ticket.id, bridge.getKioskHeaders(ticket.id)),
      )
    } catch (requestError) {
      setError(mf04ErrorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  if (loading) {
    return (
      <div className={cn(ds.page, 'flex min-h-[420px] items-center justify-center')}>
        <LoaderCircle className="size-7 animate-spin text-[#1C4D8D]" />
      </div>
    )
  }

  if (!ticket) {
    return (
      <div className={cn(ds.page, 'pb-8')}>
        <div
          role="note"
          className="flex items-start gap-2.5 rounded-2xl border border-[#82aedb]/55 bg-[#F4FBFD] px-4 py-3 text-xs text-[#245997]"
        >
          <Info className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          <p>
            <strong>Web Kiosk Prototype / Dự phòng:</strong> Màn hình này phục vụ kiểm thử và dự
            phòng. Kỳ thi chính thức BYOD trên laptop cá nhân sử dụng ứng dụng{' '}
            <code>OralExamination.DesktopKiosk</code> có Giám thị trực tiếp giám sát.
          </p>
        </div>
        <EmptyState
          icon={ShieldAlert}
          title="Chưa có vé thi tại máy trạm này"
          description="Kiểm tra đúng ca thi được phân bổ, thiết bị BYOD đã check-in và thời gian mở đề, sau đó thử tải lại vé thi."
          action={
            <div className="flex flex-col items-center gap-3">
              {error ? (
                <div
                  role="alert"
                  className="rounded-xl border border-[#f3c8cf] bg-[#fff5f6] px-4 py-2.5 text-sm font-medium text-[#b33d52]"
                >
                  {error}
                </div>
              ) : null}
              <button
                type="button"
                onClick={() => void refreshTicket()}
                className={ds.primaryButton}
              >
                Tải lại vé thi
              </button>
            </div>
          }
        />
      </div>
    )
  }

  const statusLabel = isTicketLocked
    ? 'Vé đã bị khóa'
    : isSubmitted
      ? 'Đã nộp bài'
      : isInProgress
        ? 'Ca thi đang diễn ra'
        : 'Chờ nhận máy'
  const studentName = user?.fullName ?? 'Sinh viên'
  const inputModeLabel = isVoiceOnly
    ? 'Chỉ thu âm giọng nói (VoiceOnly)'
    : 'Thu âm kèm hiệu đính thuật ngữ (≤ 35%)'

  return (
    <div className={cn(ds.page, 'pb-8')}>
      <div
        role="note"
        className="flex items-start gap-2.5 rounded-2xl border border-[#82aedb]/55 bg-[#F4FBFD] px-4 py-3 text-xs text-[#245997]"
      >
        <Laptop className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
        <p>
          <strong>Web Kiosk Prototype / Dự phòng:</strong> Vé thi được khóa theo thiết bị BYOD (1
          thiết bị · 1 phiên hoạt động · <code>{bridge.getDeviceId().slice(0, 22)}…</code>). Nếu cần
          đổi máy, vui lòng báo Giám thị hoặc Cán bộ Khảo thí.
        </p>
      </div>

      <PageBanner
        icon={Mic2}
        eyebrow={`Kỳ thi vấn đáp chính thức (${ticket.examAttemptType}) · ${statusLabel}`}
        title={ticket.sessionTitle || ticket.shiftName}
        description={`Phòng ${ticket.roomLab} · Ghế #${ticket.seatNumber} · ${studentName}`}
        extras={
          <>
            <BannerFact icon={Clock3}>Theo giờ ca Lab</BannerFact>
            <BannerFact icon={Mic2} tone="green">
              {hasPerTurnFollowUp
                ? `Có Follow-up (tối đa ${ticket.maxFollowUpQuestions} câu/lượt)`
                : 'Ghi âm toàn bộ phiên'}
            </BannerFact>
            <BannerFact icon={LockKeyhole} tone="violet">
              Vi phạm Kiosk: {kiosk.violations}/{kiosk.maxViolations} (khóa ở lần{' '}
              {kiosk.lockThreshold})
            </BannerFact>
          </>
        }
      />

      {notice && (
        <div
          role="status"
          className="flex items-start gap-3 rounded-2xl border border-[#bcebdc] bg-[#effbf6] p-4 text-sm font-medium text-[#16785f]"
        >
          <CheckCircle2 className="mt-0.5 size-5 shrink-0" />
          <span>{notice}</span>
        </div>
      )}

      {error && !kiosk.isLocked && !isSuspended && (
        <div
          role="alert"
          className="flex items-start gap-3 rounded-2xl border border-[#f3c8cf] bg-[#fff5f6] p-4 text-sm font-medium text-[#b33d52]"
        >
          <AlertTriangle className="mt-0.5 size-5 shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {(kiosk.isLocked || isSuspended || audioRecorder.error) && (
        <div
          role="alert"
          className="flex items-start gap-3 rounded-2xl border border-[#f3c8cf] bg-[#fff5f6] p-4 text-sm text-[#b33d52]"
        >
          <AlertTriangle className="mt-0.5 size-5 shrink-0" />
          <div>
            <strong>
              {kiosk.isLocked || isSuspended
                ? 'Vé thi đã bị khóa do vi phạm quy chế Kiosk.'
                : 'Lỗi microphone.'}
            </strong>
            <p className="mt-1">
              {kiosk.isLocked || isSuspended
                ? error ||
                  `Số lần vi phạm đã đạt ngưỡng máy chủ (${kiosk.violations}/${kiosk.lockThreshold}). Vui lòng liên hệ Giám thị hoặc Cán bộ Khảo thí.`
                : audioRecorder.error}
            </p>
          </div>
        </div>
      )}

      <MicrophoneRecoveryBanner
        issue={questionRecorder.micIssue ?? audioRecorder.micIssue}
        connectionState={
          questionRecorder.connectionState !== 'idle'
            ? questionRecorder.connectionState
            : audioRecorder.connectionState
        }
        deviceLabel={questionRecorder.activeDeviceLabel || audioRecorder.activeDeviceLabel}
        preserveWorkNotice
        onRetry={async () => {
          if (questionRecorder.isRecording || questionRecorder.connectionState === 'disconnected') {
            return questionRecorder.reconnectMicrophone()
          }
          return audioRecorder.reconnectMicrophone()
        }}
      />

      <section className={cn(ds.section, 'overflow-hidden')}>
        <div className={cn(ds.sectionHeader, 'flex flex-wrap items-center justify-between gap-3')}>
          <div className={ds.sectionHeading}>
            <div className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}>
                <Mic2 className={ds.sectionIcon} />
              </span>
              <h2 className={ds.sectionTitle}>Bài thi vấn đáp chính thức</h2>
            </div>
            <p className={ds.sectionHint}>
              Chế độ {inputModeLabel} · đệm hiệu đính {ticket.transcriptBufferSeconds} giây
            </p>
          </div>
          {!isSubmitted && !isTicketLocked && (
            <div className="flex flex-wrap gap-2">
              {requiresByodCheckIn ? (
                <button
                  type="button"
                  disabled={busy || kiosk.isLocked || isSuspended}
                  onClick={() => void handleCheckIn()}
                  className="inline-flex h-10 items-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-sm font-bold text-white transition duration-200 hover:-translate-y-0.5 hover:bg-[#0F2854] hover:shadow-[0_12px_22px_rgba(15,40,84,0.28)] disabled:opacity-50 motion-reduce:transform-none"
                >
                  <CheckCircle2 className="size-4" />
                  {!isInProgress
                    ? 'Xác nhận nhận máy (Check-in)'
                    : 'Khôi phục phiên / Gắn lại thiết bị BYOD'}
                </button>
              ) : !audioRecorder.isRecording && !hasRecordedAudioBlob ? (
                <>
                  <button
                    type="button"
                    disabled={busy || kiosk.isLocked || isSuspended}
                    onClick={() => void handleTestMicrophone()}
                    className={cn(
                      'inline-flex h-10 items-center gap-2 rounded-xl border px-4 text-sm font-bold transition disabled:opacity-50',
                      micTested
                        ? 'border-[#16836a] bg-[#effbf6] text-[#16785f]'
                        : 'border-[#82aedb]/55 bg-[#F4FBFD] text-[#1C4D8D] hover:bg-[#4988C4]/16',
                    )}
                  >
                    <Volume2 className="size-4" />
                    {isTestingMic
                      ? `Đang đo âm lượng (${micPeakDb}dB)...`
                      : micTested
                        ? `Mic đạt chuẩn (${micPeakDb}dB ≥ 60dB)`
                        : 'Kiểm tra Microphone (≥ 60dB)'}
                  </button>
                  <button
                    type="button"
                    disabled={busy || kiosk.isLocked || isSuspended}
                    onClick={() => void handleStartRecording()}
                    className="inline-flex h-10 items-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-sm font-bold text-white transition duration-200 hover:-translate-y-0.5 hover:bg-[#0F2854] hover:shadow-[0_12px_22px_rgba(15,40,84,0.28)] disabled:opacity-50 motion-reduce:transform-none"
                  >
                    <Mic2 className="size-4" />
                    Bắt đầu ghi âm &amp; Vào chế độ Kiosk Lockdown
                  </button>
                </>
              ) : (
                <button
                  type="button"
                  disabled={busy || kiosk.isLocked || isSuspended}
                  onClick={() => void handleSubmit()}
                  className="inline-flex h-10 items-center gap-2 rounded-xl bg-[#b33d52] px-4 text-sm font-bold text-white transition duration-200 hover:-translate-y-0.5 hover:bg-[#932c3f] disabled:opacity-50 motion-reduce:transform-none"
                >
                  <UploadCloud className="size-4" />
                  {busy ? 'Đang lưu...' : 'Nộp bài và niêm phong'}
                </button>
              )}
            </div>
          )}
        </div>

        <div className="p-5 sm:p-6">
          {isSubmitted && !isTicketLocked && (
            <div className="mb-4 rounded-2xl border border-[#bcebdc] bg-[#effbf6] p-4 text-sm text-[#16785f]">
              <strong>Bài thi đã được tiếp nhận và niêm phong an toàn.</strong> Bạn có thể ký biên
              bản phòng thi và theo dõi kết quả sau khi Cán bộ Khảo thí công bố điểm.{' '}
              {submission && `Trạng thái: ${submission.status}.`}
            </div>
          )}
          {audioRecorder.isRecording && (
            <div className="mb-4 flex items-center gap-2 rounded-xl bg-[#fff4f5] px-3.5 py-2.5 text-sm font-semibold text-[#b33d52]">
              <span className="size-2 animate-pulse rounded-full bg-[#d9455d]" />
              Đang ghi âm · {formatRecordingDuration(recordingSeconds)} · Vi phạm:{' '}
              {kiosk.violations}/{kiosk.maxViolations} · Còn {kiosk.remainingStrikes} lần trước khi
              khóa
            </div>
          )}
          {!canViewQuestions ? (
            <div className="rounded-2xl border border-dashed border-[#82aedb]/55 bg-[#F4FBFD] p-8 text-center">
              <LockKeyhole className="mx-auto size-10 text-[#1C4D8D]" />
              <h3 className="mt-3 text-base font-bold text-[#0F2854]">
                {isTicketLocked
                  ? 'Phiên thi đang bị khóa tại máy trạm'
                  : 'Nội dung đề thi đang được niêm phong bảo mật'}
              </h3>
              <p className="mx-auto mt-2 max-w-xl text-sm leading-relaxed text-[#596b84]">
                {isTicketLocked
                  ? 'Phiên làm bài đã dừng do trạng thái khóa từ máy chủ. Vui lòng liên hệ Giám thị hoặc Cán bộ Khảo thí để được hướng dẫn.'
                  : !isInProgress
                    ? 'Vui lòng bấm "Xác nhận nhận máy (Check-in)" để đối chiếu thiết bị BYOD trước khi bước vào ca thi.'
                    : requiresByodCheckIn
                      ? 'Thiết bị BYOD vừa được Khảo thí mở khóa hoặc phiên Kiosk cần khôi phục sau sự cố. Vui lòng bấm "Khôi phục phiên / Gắn lại thiết bị BYOD" để nhận lại token phiên mới và tiếp tục thi.'
                      : 'Để mở đề thi, vui lòng thực hiện bước Kiểm tra Microphone (≥ 60dB), sau đó bấm "Bắt đầu ghi âm & Vào chế độ Kiosk Lockdown".'}
              </p>
              {isInProgress && !requiresByodCheckIn && !isTicketLocked && (
                <div className="mt-4 inline-flex items-center gap-2 rounded-full bg-white px-4 py-1.5 text-xs font-semibold text-[#1C4D8D] shadow-xs">
                  <Volume2 className="size-3.5" />
                  Trạng thái Microphone:{' '}
                  {micTested
                    ? `Đạt chuẩn (${micPeakDb}dB ≥ 60dB)`
                    : `Chưa kiểm tra hoặc chưa đạt 60dB (cực đại: ${micPeakDb}dB)`}
                </div>
              )}
            </div>
          ) : (
            <div className="space-y-4">
              {questions.length === 0 ? (
                <EmptyState
                  icon={Mic2}
                  title="Ca thi chưa được gán bộ câu hỏi"
                  description="Hãy liên hệ giám thị phòng thi."
                />
              ) : (
                questions.map((question, index) => {
                  const mainTurnKey = turnKey(question.questionId, 0)
                  const baselineTranscript = voiceBaselinesByQuestion[question.questionId] ?? ''
                  const hasVoiceBaseline = Boolean(baselineTranscript.trim())
                  const bufferDeadline = bufferDeadlinesByQuestion[question.questionId] ?? 0
                  const bufferRemainingSeconds =
                    !isVoiceOnly && hasVoiceBaseline && bufferDeadline > nowTimestamp
                      ? Math.ceil((bufferDeadline - nowTimestamp) / 1000)
                      : 0
                  const isMainSaved = Boolean(recordedTurnKeys[mainTurnKey])
                  const isTextareaLocked =
                    isVoiceOnly ||
                    !audioRecorder.isRecording ||
                    isSubmitted ||
                    isMainSaved ||
                    !hasVoiceBaseline ||
                    bufferRemainingSeconds <= 0
                  const revisionError = revisionErrorsByQuestion[question.questionId]
                  const editPercent = revisionPercentByQuestion[question.questionId] ?? 0
                  const followUps = followUpsByQuestion[question.questionId] ?? []

                  return (
                    <article
                      key={question.questionId}
                      className="rounded-2xl border border-[#82aedb]/55 bg-[#F4FBFD] p-4"
                    >
                      <div className="flex items-start gap-3">
                        <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-[#1C4D8D] text-sm font-bold text-white">
                          {index + 1}
                        </span>
                        <div className="min-w-0 flex-1">
                          <div className="flex flex-wrap items-center justify-between gap-2">
                            <p className="text-xs font-bold text-[#1C4D8D]">
                              {question.title || `Câu hỏi ${index + 1}`} · {question.difficulty}
                              {isMainSaved ? ' · Đã lưu R2' : ''}
                            </p>
                            {audioRecorder.isRecording && !isSubmitted && !isMainSaved && (
                              <div className="flex flex-wrap gap-2">
                                <button
                                  type="button"
                                  disabled={busy || transcribingTurnKey === mainTurnKey}
                                  onClick={() =>
                                    void handleToggleTurnVoice(question.questionId, index + 1, 0)
                                  }
                                  className={cn(
                                    'inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-bold transition disabled:opacity-50',
                                    recordingTurnKey === mainTurnKey
                                      ? 'bg-[#b33d52] text-white'
                                      : 'bg-[#1C4D8D] text-white',
                                  )}
                                >
                                  <Mic2 className="size-3.5" />
                                  {transcribingTurnKey === mainTurnKey
                                    ? 'Đang bóc băng...'
                                    : recordingTurnKey === mainTurnKey
                                      ? 'Dừng & Bóc băng câu này'
                                      : 'Thu âm trả lời câu này'}
                                </button>
                                {hasPerTurnFollowUp && hasVoiceBaseline && (
                                  <button
                                    type="button"
                                    disabled={
                                      busy ||
                                      submittingTurnKey === mainTurnKey ||
                                      Boolean(revisionError)
                                    }
                                    onClick={() =>
                                      void handleSubmitPerTurnAnswer(
                                        question.questionId,
                                        index + 1,
                                        0,
                                        null,
                                      )
                                    }
                                    className="inline-flex items-center gap-1.5 rounded-lg bg-[#16836a] px-3 py-1.5 text-xs font-bold text-white transition hover:bg-[#106c55] disabled:opacity-50"
                                  >
                                    {submittingTurnKey === mainTurnKey ? (
                                      <LoaderCircle className="size-3.5 animate-spin" />
                                    ) : (
                                      <Send className="size-3.5" />
                                    )}
                                    Lưu câu trả lời &amp; Kiểm tra câu phụ
                                  </button>
                                )}
                              </div>
                            )}
                          </div>
                          <h3 className="mt-1 text-sm font-bold leading-relaxed text-[#0F2854]">
                            {question.content}
                          </h3>
                          {getExamQuestionImageUrl(question.imageUrl) && (
                            <img
                              src={getExamQuestionImageUrl(question.imageUrl) ?? undefined}
                              alt={question.imageDescription || question.title || 'Hình ảnh câu hỏi'}
                              className="mt-3 max-h-72 w-full rounded-xl border border-[#82aedb]/55 bg-white object-contain"
                            />
                          )}
                          {question.imageDescription && (
                            <p className="mt-2 text-xs italic text-[#60718a]">
                              {question.imageDescription}
                            </p>
                          )}

                          {isVoiceOnly ? (
                            <p className="mt-3 text-xs font-semibold text-[#52657d]">
                              Chế độ VoiceOnly: khóa hoàn toàn chỉnh sửa transcript thủ công.
                            </p>
                          ) : !isSubmitted ? (
                            <div className="mt-3 flex flex-wrap items-center justify-between gap-2 text-xs">
                              {!hasVoiceBaseline ? (
                                <span className="font-semibold text-[#60718a]">
                                  Voice-First Gate: Hãy thu âm giọng nói trước để mở khóa hiệu đính
                                  thuật ngữ.
                                </span>
                              ) : bufferRemainingSeconds > 0 ? (
                                <span className="font-semibold text-[#16785f]">
                                  Thời gian đệm hiệu đính còn lại: {bufferRemainingSeconds}s · Mức
                                  sửa hiện tại: {editPercent}% / 35%
                                </span>
                              ) : (
                                <span className="font-semibold text-[#b33d52]">
                                  Đã hết thời gian đệm hiệu đính ({ticket.transcriptBufferSeconds}s)
                                  — bản trả lời đã khóa.
                                </span>
                              )}
                            </div>
                          ) : null}

                          <textarea
                            aria-label={`Transcript câu ${index + 1}`}
                            value={answers[question.questionId] ?? ''}
                            readOnly={isTextareaLocked}
                            disabled={isTextareaLocked}
                            onChange={(event) => {
                              if (isTextareaLocked) return
                              const nextValue = event.target.value
                              setAnswers((current) => ({
                                ...current,
                                [question.questionId]: nextValue,
                              }))
                              const check = checkTranscriptRevision(baselineTranscript, nextValue)
                              setRevisionPercentByQuestion((current) => ({
                                ...current,
                                [question.questionId]: Math.round(check.wordChangeRatio * 100),
                              }))
                              setRevisionErrorsByQuestion((current) => ({
                                ...current,
                                [question.questionId]: check.isAllowed ? null : check.reason,
                              }))
                            }}
                            placeholder={
                              isVoiceOnly
                                ? 'Chế độ Chỉ Giọng Nói (VoiceOnly): Hệ thống tự động bóc băng âm thanh, không cho phép gõ phím.'
                                : !hasVoiceBaseline
                                  ? 'Bắt buộc thu âm giọng nói trước khi hiệu đính thuật ngữ chuyên ngành...'
                                  : 'Kiểm tra và hiệu đính các thuật ngữ chuyên ngành trong thời gian đệm (≤ 35%)...'
                            }
                            className="mt-2 min-h-24 w-full resize-y rounded-xl border border-[#4988C4] bg-white p-3 text-sm text-[#0F2854] outline-none focus:border-[#1C4D8D] disabled:bg-[#f0f3f7]"
                          />
                          {revisionError && (
                            <p role="alert" className="mt-1.5 text-xs font-semibold text-[#b33d52]">
                              {revisionError}
                            </p>
                          )}

                          {followUps.length > 0 && (
                            <div className="mt-4 space-y-3 border-t border-[#d9e8f2] pt-3">
                              {followUps.map((followUp) => {
                                const fuKey = turnKey(question.questionId, followUp.followUpIndex)
                                const fuBaseline = voiceBaselinesByQuestion[fuKey] ?? ''
                                const fuHasBaseline = Boolean(fuBaseline.trim())
                                const fuDeadline = bufferDeadlinesByQuestion[fuKey] ?? 0
                                const fuRemaining =
                                  !isVoiceOnly && fuHasBaseline && fuDeadline > nowTimestamp
                                    ? Math.ceil((fuDeadline - nowTimestamp) / 1000)
                                    : 0
                                const fuLocked =
                                  isVoiceOnly ||
                                  !audioRecorder.isRecording ||
                                  isSubmitted ||
                                  followUp.recorded ||
                                  !fuHasBaseline ||
                                  fuRemaining <= 0
                                const fuRevError = revisionErrorsByQuestion[fuKey]

                                return (
                                  <div
                                    key={fuKey}
                                    className="rounded-xl border border-[#bcd6ef] bg-white p-3.5"
                                  >
                                    <div className="flex flex-wrap items-center justify-between gap-2">
                                      <p className="flex items-center gap-1.5 text-xs font-bold text-[#1C4D8D]">
                                        <MessageSquarePlus className="size-3.5" />
                                        Câu hỏi phụ #{followUp.followUpIndex}
                                        {followUp.recorded ? ' · Đã lưu R2' : ''}
                                      </p>
                                      {audioRecorder.isRecording &&
                                        !isSubmitted &&
                                        !followUp.recorded && (
                                          <div className="flex flex-wrap gap-2">
                                            <button
                                              type="button"
                                              disabled={busy || transcribingTurnKey === fuKey}
                                              onClick={() =>
                                                void handleToggleTurnVoice(
                                                  question.questionId,
                                                  index + 1,
                                                  followUp.followUpIndex,
                                                )
                                              }
                                              className={cn(
                                                'inline-flex items-center gap-1.5 rounded-lg px-2.5 py-1 text-xs font-bold text-white transition disabled:opacity-50',
                                                recordingTurnKey === fuKey
                                                  ? 'bg-[#b33d52]'
                                                  : 'bg-[#1C4D8D]',
                                              )}
                                            >
                                              <Mic2 className="size-3" />
                                              {transcribingTurnKey === fuKey
                                                ? 'Đang bóc băng...'
                                                : recordingTurnKey === fuKey
                                                  ? 'Dừng & Bóc băng câu phụ'
                                                  : `Thu âm câu phụ #${followUp.followUpIndex}`}
                                            </button>
                                            {fuHasBaseline && (
                                              <button
                                                type="button"
                                                disabled={
                                                  busy ||
                                                  submittingTurnKey === fuKey ||
                                                  Boolean(fuRevError)
                                                }
                                                onClick={() =>
                                                  void handleSubmitPerTurnAnswer(
                                                    question.questionId,
                                                    index + 1,
                                                    followUp.followUpIndex,
                                                    followUp.parentSubmissionId ||
                                                      mainSubmissionIdsByQuestion[
                                                        question.questionId
                                                      ] ||
                                                      null,
                                                  )
                                                }
                                                className="inline-flex items-center gap-1 rounded-lg bg-[#16836a] px-2.5 py-1 text-xs font-bold text-white transition hover:bg-[#106c55] disabled:opacity-50"
                                              >
                                                <Send className="size-3" /> Lưu câu phụ #
                                                {followUp.followUpIndex}
                                              </button>
                                            )}
                                          </div>
                                        )}
                                    </div>
                                    <p className="mt-1.5 text-sm font-semibold text-[#0F2854]">
                                      {followUp.prompt}
                                    </p>
                                    <textarea
                                      aria-label={`Transcript câu ${index + 1} phụ ${followUp.followUpIndex}`}
                                      value={answers[fuKey] ?? ''}
                                      readOnly={fuLocked}
                                      disabled={fuLocked}
                                      onChange={(event) => {
                                        if (fuLocked) return
                                        const nextVal = event.target.value
                                        setAnswers((current) => ({ ...current, [fuKey]: nextVal }))
                                        const check = checkTranscriptRevision(fuBaseline, nextVal)
                                        setRevisionErrorsByQuestion((current) => ({
                                          ...current,
                                          [fuKey]: check.isAllowed ? null : check.reason,
                                        }))
                                      }}
                                      placeholder={
                                        isVoiceOnly
                                          ? 'Chế độ VoiceOnly: chỉ thu âm giọng nói.'
                                          : 'Thu âm câu trả lời phụ trước để mở khóa hiệu đính (≤ 35%)...'
                                      }
                                      className="mt-2 min-h-20 w-full resize-y rounded-lg border border-[#82aedb] bg-[#F8FBFD] p-2.5 text-xs text-[#0F2854] outline-none disabled:bg-[#f0f3f7]"
                                    />
                                    {fuRevError && (
                                      <p
                                        role="alert"
                                        className="mt-1 text-xs font-semibold text-[#b33d52]"
                                      >
                                        {fuRevError}
                                      </p>
                                    )}
                                  </div>
                                )
                              })}
                            </div>
                          )}
                        </div>
                      </div>
                    </article>
                  )
                })
              )}
            </div>
          )}
        </div>
      </section>

      {sealed && !isTicketLocked && (
        <p className="flex items-center gap-2 text-xs font-semibold text-[#16836a]">
          <CheckCircle2 className="size-4" />
          Bản ghi âm đã được tải lên và niêm phong mã băm SHA-256 an toàn.
        </p>
      )}

      {isSubmitted && !isTicketLocked && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-[#82aedb]/55 bg-white p-4">
          <p className="text-xs font-medium text-[#0F2854]">
            Theo dõi trạng thái xử lý bài thi sau khi nộp.
          </p>
          <button
            type="button"
            disabled={busy}
            onClick={() => void refreshSubmission()}
            className="rounded-xl border border-[#82aedb]/55 bg-[#F4FBFD] px-3.5 py-2 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#4988C4]/16"
          >
            Cập nhật trạng thái
          </button>
        </div>
      )}

      <p className="flex items-start gap-2 text-xs leading-relaxed text-[#5b6b84]">
        <ShieldAlert className="mt-0.5 size-4 shrink-0 text-[#1C4D8D]" />
        Bản thu âm ca thi chính thức được niêm phong bảo mật. Kết quả điểm thi sẽ hiển thị tại mục
        Kết quả sau khi Cán bộ Khảo thí công bố điểm.
      </p>
    </div>
  )
}

export function OfficialExamPage() {
  return <OfficialExamLivePage />
}
