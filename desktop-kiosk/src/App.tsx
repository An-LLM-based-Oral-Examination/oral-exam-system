import { Fragment, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { useAntiCheatMonitor } from './hooks/useAntiCheatMonitor'
import { useKioskHeartbeat } from './hooks/useKioskHeartbeat'
import { useKioskRuntime } from './hooks/useKioskRuntime'
import { BlockedScreen } from './screens/BlockedScreen'
import { ExamScreen } from './screens/ExamScreen'
import { LobbyScreen } from './screens/LobbyScreen'
import { PreCheckScreen } from './screens/PreCheckScreen'
import { SignInScreen } from './screens/SignInScreen'
import { SubmittedScreen } from './screens/SubmittedScreen'
import { WaitingTicketScreen } from './screens/WaitingTicketScreen'
import {
  ApiClient,
  ApiProblemError,
  classifyByodConflict,
  describeByodConflict,
} from './services/api'
import {
  buildTurnDraftKey,
  clearTicketDrafts,
  deleteTurnDraft,
  loadTicketTurnDrafts,
  saveTurnDraft,
} from './services/offline-draft'
import type {
  ActiveFollowUpState,
  AntiCheatSeverity,
  BlockReason,
  ByodConflictCode,
  KioskHeartbeatResponse,
  OfficialExamTicketStateResponse,
  ScreenId,
  StudentWorkstationTicket,
  TurnDraft,
  TurnDraftStage,
} from './types/mf04.types'
import { retryOnce, sha256Hex } from './utils/crypto'
import { errorMessage, isStudentRole } from './utils/formatters'
import { evaluateTranscriptEdit } from './utils/transcript-diff'

const stepperSteps: Array<{ id: ScreenId; label: string }> = [
  { id: 'sign-in', label: 'Đăng nhập' },
  { id: 'pre-check', label: 'Thiết bị' },
  { id: 'lobby', label: 'Phòng chờ' },
  { id: 'exam', label: 'Làm bài' },
]

const submittedStatuses = new Set(['SUBMITTED', 'AI_GRADED', 'AUDITED', 'PUBLISHED', 'LOCKED'])

function getStepIndex(screen: ScreenId): number {
  if (screen === 'waiting-ticket') return 0
  if (screen === 'submitted') return stepperSteps.length
  const found = stepperSteps.findIndex((step) => step.id === screen)
  return found < 0 ? 0 : found
}

function resolvePendingFollowUpFromState(
  state: OfficialExamTicketStateResponse,
  maxFollowUpQuestions: number,
): ActiveFollowUpState | null {
  if (maxFollowUpQuestions <= 0 || state.followUps.length === 0) return null

  const byQuestion = new Map<string, typeof state.followUps>()
  for (const item of state.followUps) {
    const list = byQuestion.get(item.questionId) ?? []
    list.push(item)
    byQuestion.set(item.questionId, list)
  }

  for (const [questionId, items] of byQuestion.entries()) {
    const sorted = [...items].sort((a, b) => a.followUpIndex - b.followUpIndex)
    const latest = sorted[sorted.length - 1]
    if (
      latest &&
      latest.generatedFollowUpPrompt &&
      latest.followUpIndex < maxFollowUpQuestions
    ) {
      return {
        questionId,
        parentSubmissionId: latest.submissionId,
        followUpIndex: latest.followUpIndex + 1,
        maxFollowUpQuestions,
        prompt: latest.generatedFollowUpPrompt,
      }
    }
  }

  return null
}

function App() {
  const runtimeInfo = useKioskRuntime()
  const apiClient = useMemo(
    () =>
      new ApiClient({
        baseUrl: runtimeInfo.apiBaseUrl,
        deviceId: runtimeInfo.deviceId,
        kioskVersion: runtimeInfo.kioskVersion,
      }),
    [runtimeInfo.apiBaseUrl, runtimeInfo.deviceId, runtimeInfo.kioskVersion],
  )

  const [screen, setScreen] = useState<ScreenId>('sign-in')
  const [ticket, setTicket] = useState<StudentWorkstationTicket | null>(null)
  const [completedMainIds, setCompletedMainIds] = useState<string[]>([])
  const [questionIndex, setQuestionIndex] = useState(0)
  const [activeFollowUp, setActiveFollowUp] = useState<ActiveFollowUpState | null>(null)
  const [generatingFollowUp, setGeneratingFollowUp] = useState(false)
  const [initialTranscript, setInitialTranscript] = useState('')
  const [transcriptDraft, setTranscriptDraft] = useState('')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [mustChangePassword, setMustChangePassword] = useState(false)
  const [studentName, setStudentName] = useState('')
  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')
  const [microphoneReady, setMicrophoneReady] = useState(false)
  const [micTestSeconds, setMicTestSeconds] = useState(0)
  const [micLevel, setMicLevel] = useState(0)
  const [securitySnapshot, setSecuritySnapshot] = useState<KioskSecuritySnapshot | null>(null)
  const [scanningEnvironment, setScanningEnvironment] = useState(false)
  const [blurCount, setBlurCount] = useState(0)
  const [blockReason, setBlockReason] = useState<BlockReason>('ANTI_CHEAT')
  const [blockDetail, setBlockDetail] = useState('')
  const [checkingBlockStatus, setCheckingBlockStatus] = useState(false)
  const [clockNow, setClockNow] = useState(() => Date.now())
  const [questionStartedAt, setQuestionStartedAt] = useState<number | null>(null)
  const [recording, setRecording] = useState(false)
  const [recordingElapsedSeconds, setRecordingElapsedSeconds] = useState(0)
  const [transcribing, setTranscribing] = useState(false)
  const [transcriptReviewPending, setTranscriptReviewPending] = useState(false)
  const [transcriptBufferRemaining, setTranscriptBufferRemaining] = useState(0)
  const [pendingTurnDraftsCount, setPendingTurnDraftsCount] = useState(0)
  const [isExitPending, setIsExitPending] = useState(false)

  const streamRef = useRef<MediaStream | null>(null)
  const recorderRef = useRef<MediaRecorder | null>(null)
  const currentChunksRef = useRef<Blob[]>([])
  const analyserFrameRef = useRef<number | null>(null)
  const liveAudioContextRef = useRef<AudioContext | null>(null)
  const liveAudioLevelRef = useRef<number | null>(null)
  const blurCountRef = useRef(0)
  const lastBlurAtRef = useRef(0)
  const ticketRef = useRef<StudentWorkstationTicket | null>(null)
  const serverClockOffsetRef = useRef(0)
  const submitInFlightRef = useRef(false)
  const autoSubmitTriggeredRef = useRef(false)
  const submitExamRef = useRef<(allowIncomplete?: boolean) => Promise<void>>(async () => undefined)
  const pendingTranscriptAudioRef = useRef<Blob | null>(null)
  const lastValidTranscriptRef = useRef('')
  const busyRef = useRef(false)

  useEffect(() => {
    ticketRef.current = ticket
  }, [ticket])
  useEffect(() => {
    busyRef.current = busy
  }, [busy])

  useEffect(() => {
    if (!recording) {
      setRecordingElapsedSeconds(0)
      return
    }
    const started = Date.now()
    const timer = window.setInterval(() => {
      setRecordingElapsedSeconds(Math.floor((Date.now() - started) / 1000))
    }, 500)
    return () => window.clearInterval(timer)
  }, [recording])

  function serverNow(): number {
    return Date.now() + serverClockOffsetRef.current
  }

  const stopLiveLevelMonitor = useCallback(() => {
    if (analyserFrameRef.current !== null) {
      cancelAnimationFrame(analyserFrameRef.current)
      analyserFrameRef.current = null
    }
    if (liveAudioContextRef.current) {
      void liveAudioContextRef.current.close().catch(() => undefined)
      liveAudioContextRef.current = null
    }
    liveAudioLevelRef.current = null
  }, [])

  const stopMicrophone = useCallback(() => {
    stopLiveLevelMonitor()
    streamRef.current?.getTracks().forEach((track) => {
      track.onended = null
      track.stop()
    })
    streamRef.current = null
    recorderRef.current = null
    setRecording(false)
  }, [stopLiveLevelMonitor])

  const startLiveLevelMonitor = useCallback(
    (stream: MediaStream) => {
      stopLiveLevelMonitor()
      try {
        const audioContext = new AudioContext()
        const analyser = audioContext.createAnalyser()
        analyser.fftSize = 1024
        audioContext.createMediaStreamSource(stream).connect(analyser)
        liveAudioContextRef.current = audioContext
        const samples = new Uint8Array(analyser.fftSize)
        const measure = () => {
          analyser.getByteTimeDomainData(samples)
          let sum = 0
          for (const sample of samples) {
            const normalized = (sample - 128) / 128
            sum += normalized * normalized
          }
          const rms = Math.sqrt(sum / samples.length)
          liveAudioLevelRef.current =
            rms > 0 ? Math.max(0, Math.min(100, Math.round(20 * Math.log10(rms) + 100))) : 0
          analyserFrameRef.current = requestAnimationFrame(measure)
        }
        measure()
      } catch {
        liveAudioLevelRef.current = null
      }
    },
    [stopLiveLevelMonitor],
  )

  const lockWithReason = useCallback(
    (reason: BlockReason, message: string) => {
      stopMicrophone()
      setBlockReason(reason)
      setBlockDetail(message)
      setScreen('blocked')
      setError(message)
    },
    [stopMicrophone],
  )

  const handleByodConflict = useCallback(
    (_code: ByodConflictCode, message: string) => {
      lockWithReason('BYOD_CONFLICT', message)
    },
    [lockWithReason],
  )

  const appendAntiCheatLog = useCallback(
    async (eventType: string, details: string, severity: AntiCheatSeverity = 'HIGH') => {
      const currentTicket = ticketRef.current
      const storedUser = apiClient.getStoredUser()
      if (!currentTicket || !storedUser) return
      if (submittedStatuses.has(currentTicket.status)) return
      try {
        const payload = {
          ticketId: currentTicket.id,
          shiftId: currentTicket.shiftId,
          studentId: storedUser.id,
          clientEventId: crypto.randomUUID(),
          eventType,
          severity,
          details: details.slice(0, 1900),
        } as const
        await retryOnce(() => apiClient.logAntiCheat(payload))
      } catch (logError) {
        const conflict = classifyByodConflict(logError)
        if (conflict) {
          handleByodConflict(conflict, describeByodConflict(conflict))
          return
        }
        setNotice('Không gửi được nhật ký an ninh; bài thi chưa được xác nhận nộp.')
      }
    },
    [apiClient, handleByodConflict],
  )

  const registerBlur = useCallback(
    async (eventType: string): Promise<void> => {
      const now = serverNow()
      if (now - lastBlurAtRef.current < 750) return
      lastBlurAtRef.current = now
      blurCountRef.current += 1
      const nextCount = blurCountRef.current
      setBlurCount(nextCount)
      await appendAntiCheatLog(eventType, `Mất tiêu điểm lần ${nextCount}`, 'HIGH')
      if (nextCount > 3) {
        lockWithReason(
          'ANTI_CHEAT',
          'Trạm thi đã bị đình chỉ do vượt quá 3 lần mất tiêu điểm. Không thể tiếp tục hoặc tự ý nộp bài từ màn hình này.',
        )
      }
    },
    [appendAntiCheatLog, lockWithReason],
  )

  useAntiCheatMonitor({
    active: screen === 'exam',
    altTabTestMode: runtimeInfo.altTabTestMode,
    onWindowBlur: registerBlur,
    onSecurityEvent: appendAntiCheatLog,
  })

  useKioskHeartbeat({
    apiClient,
    ticketId: ticket?.id ?? null,
    enabled: (screen === 'lobby' || screen === 'exam') && ticket?.status === 'IN_PROGRESS',
    getAudioLevelDb: () => (recording ? liveAudioLevelRef.current : null),
    onServerSync: (response: KioskHeartbeatResponse) => {
      const serverTime = Date.parse(response.serverUtcNow)
      if (Number.isFinite(serverTime)) {
        serverClockOffsetRef.current = serverTime - Date.now()
        setClockNow(serverTime)
      }
      if (response.notice && !response.shouldTerminateExam && !response.isLocked) {
        setNotice(response.notice)
      }
    },
    onTerminate: (response: KioskHeartbeatResponse) => {
      stopMicrophone()
      if (submittedStatuses.has(response.ticketStatus) && response.ticketStatus !== 'LOCKED') {
        void window.kioskApi?.authorizeExitAfterSubmit()
        apiClient.clearSession()
        setScreen('submitted')
        setNotice('Bài làm đã được xác nhận lưu an toàn trên máy chủ. Không hiển thị điểm tại kiosk.')
        return
      }
      if (response.deviceUnlockRequired) {
        lockWithReason(
          'BYOD_CONFLICT',
          response.notice ?? 'Thiết bị cần được Cán bộ Khảo thí mở khóa trước khi tiếp tục.',
        )
        return
      }
      lockWithReason(
        'SERVER_LOCKED',
        response.notice ?? 'Phiên thi đã được khóa hoặc ca thi đã kết thúc từ máy chủ.',
      )
    },
    onByodConflict: handleByodConflict,
  })

  const refreshSecuritySnapshot = useCallback(async () => {
    setScanningEnvironment(true)
    try {
      const snapshot = await window.kioskApi?.getSecuritySnapshot()
      if (snapshot) setSecuritySnapshot(snapshot)
    } finally {
      setScanningEnvironment(false)
    }
  }, [])

  useEffect(() => {
    if (screen === 'pre-check') {
      void refreshSecuritySnapshot()
    }
  }, [refreshSecuritySnapshot, screen])

  useEffect(() => {
    if (screen !== 'exam') return
    const intervalId = window.setInterval(() => setClockNow(serverNow()), 250)
    return () => window.clearInterval(intervalId)
  }, [screen])

  useEffect(() => {
    if (screen !== 'exam' || !ticket || !transcriptReviewPending) return
    const intervalId = window.setInterval(() => {
      setTranscriptBufferRemaining((current) => Math.max(0, current - 1))
    }, 1000)
    return () => window.clearInterval(intervalId)
  }, [screen, ticket, transcriptReviewPending])

  useEffect(() => {
    if (screen !== 'exam' || !ticket || !transcriptReviewPending || transcriptBufferRemaining > 0) return
    if (pendingTranscriptAudioRef.current) {
      const editCheck = evaluateTranscriptEdit(initialTranscript, transcriptDraft)
      if (editCheck.isExceeded) {
        setTranscriptDraft(lastValidTranscriptRef.current || initialTranscript)
      }
      void completeQuestion(true)
    }
  }, [screen, ticket, transcriptReviewPending, transcriptBufferRemaining])

  useEffect(() => () => stopMicrophone(), [stopMicrophone])

  useEffect(() => {
    submitExamRef.current = submitExam
  })

  useEffect(() => {
    if (screen !== 'exam' || !ticket) return
    const deadline = Date.parse(ticket.endTime)
    if (!Number.isFinite(deadline)) return
    const intervalId = window.setInterval(() => {
      const now = serverNow()
      setClockNow(now)
      if (now >= deadline && !autoSubmitTriggeredRef.current && !busyRef.current) {
        autoSubmitTriggeredRef.current = true
        void submitExamRef.current(true)
      }
    }, 250)
    return () => window.clearInterval(intervalId)
  }, [screen, ticket])

  async function uploadAndSubmitTurn(
    currentTicket: StudentWorkstationTicket,
    questionId: string,
    followUpIndex: number,
    parentSubmissionId: string | null,
    followUpPrompt: string | null,
    audio: Blob,
    rawVoiceTranscript: string | null,
    finalTranscript: string,
    isManuallyEdited: boolean,
    timeSpentSeconds: number,
  ) {
    const draftKey = buildTurnDraftKey(currentTicket.id, questionId, followUpIndex)
    let stage: TurnDraftStage = 'RECORDED'
    let objectKey: string | null = null
    let sha256: string | null = null

    const saveStage = async () => {
      const nowIso = new Date(serverNow()).toISOString()
      await saveTurnDraft({
        metadata: {
          draftKey,
          ticketId: currentTicket.id,
          shiftId: currentTicket.shiftId,
          studentId: currentTicket.studentId,
          questionId,
          followUpIndex,
          parentSubmissionId,
          followUpPrompt,
          initialVoiceTranscript: rawVoiceTranscript,
          transcriptWhisper: finalTranscript,
          timestampsWhisper: null,
          timeSpentSeconds,
          isManuallyEdited,
          objectKey,
          sha256,
          stage,
          createdAt: nowIso,
          updatedAt: nowIso,
        },
        audio,
      })
    }

    await saveStage()

    try {
      const authorization = await retryOnce(() =>
        apiClient.createQuestionAudioUploadAuthorization(currentTicket.id, questionId, followUpIndex),
      )
      stage = 'UPLOAD_AUTHORIZED'
      objectKey = authorization.objectKey
      await saveStage()

      await retryOnce(() => apiClient.uploadAudio(authorization, audio))
      stage = 'AUDIO_UPLOADED'
      await saveStage()

      sha256 = await sha256Hex(audio)
      const response = await retryOnce(() =>
        apiClient.submitQuestionAnswer(currentTicket.id, questionId, {
          followUpIndex,
          parentSubmissionId,
          audioObjectKey: authorization.objectKey,
          audioHashSha256: sha256!,
          initialVoiceTranscript: rawVoiceTranscript,
          transcriptWhisper: finalTranscript,
          timestampsWhisper: null,
          timeSpentSeconds,
          isManuallyEdited,
        }),
      )

      stage = 'ANSWER_SUBMITTED'
      await deleteTurnDraft(draftKey)
      return response
    } catch (turnError) {
      await saveStage().catch(() => undefined)
      const remainingDrafts = await loadTicketTurnDrafts(currentTicket.id)
      setPendingTurnDraftsCount(remainingDrafts.length)
      throw turnError
    }
  }

  async function flushPendingTurnDrafts(currentTicket: StudentWorkstationTicket): Promise<void> {
    const drafts: TurnDraft[] = await loadTicketTurnDrafts(currentTicket.id)
    setPendingTurnDraftsCount(drafts.length)
    if (drafts.length === 0) return

    for (const draft of drafts) {
      const { metadata, audio } = draft
      if (audio.size === 0) {
        await deleteTurnDraft(metadata.draftKey)
        continue
      }
      await uploadAndSubmitTurn(
        currentTicket,
        metadata.questionId,
        metadata.followUpIndex,
        metadata.parentSubmissionId,
        metadata.followUpPrompt,
        audio,
        metadata.initialVoiceTranscript,
        metadata.transcriptWhisper,
        metadata.isManuallyEdited,
        metadata.timeSpentSeconds,
      )
    }

    const remaining = await loadTicketTurnDrafts(currentTicket.id)
    setPendingTurnDraftsCount(remaining.length)
  }

  async function handleLogin(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault()
    if (!username.trim() || !password) {
      setError('Vui lòng nhập tên đăng nhập và mật khẩu.')
      return
    }
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const response = await apiClient.login(username.trim(), password)
      if (!isStudentRole(response.user.role)) {
        apiClient.clearSession()
        throw new Error('Tài khoản này không có vai trò sinh viên cho kiosk phòng thi.')
      }
      setStudentName(response.user.fullName)
      setMustChangePassword(response.mustChangePassword)
      if (response.mustChangePassword) {
        setScreen('sign-in')
        setNotice('Tài khoản cần đổi mật khẩu trước khi vào phòng thi.')
        return
      }
      await continueAfterAuthentication()
    } catch (loginError) {
      const conflict = classifyByodConflict(loginError)
      if (conflict) {
        handleByodConflict(conflict, describeByodConflict(conflict))
        return
      }
      setError(errorMessage(loginError))
    } finally {
      setBusy(false)
    }
  }

  async function handleChangePassword(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault()
    if (newPassword.length < 8) {
      setError('Mật khẩu mới phải có ít nhất 8 ký tự.')
      return
    }
    setBusy(true)
    setError('')
    try {
      const response = await apiClient.changePassword(password, newPassword)
      setMustChangePassword(response.mustChangePassword)
      await continueAfterAuthentication()
    } catch (passwordError) {
      setError(errorMessage(passwordError))
    } finally {
      setBusy(false)
    }
  }

  async function continueAfterAuthentication(): Promise<void> {
    try {
      const alreadySubmitted = await loadTicket()
      setScreen(alreadySubmitted ? 'submitted' : 'pre-check')
    } catch (ticketError) {
      const conflict = classifyByodConflict(ticketError)
      if (conflict) {
        handleByodConflict(conflict, describeByodConflict(conflict))
        return
      }
      if (ticketError instanceof ApiProblemError && ticketError.status === 404) {
        setNotice('Đăng nhập thành công. Tài khoản chưa được cấp vé thi cho ca hiện tại; hãy thử lại sau khi Khảo thí phân bổ vé.')
        setScreen('waiting-ticket')
        return
      }
      throw ticketError
    }
  }

  async function retryTicketLoad(): Promise<void> {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await continueAfterAuthentication()
    } catch (ticketError) {
      setError(errorMessage(ticketError))
    } finally {
      setBusy(false)
    }
  }

  async function loadTicket(): Promise<boolean> {
    const loadedTicket = await apiClient.getCurrentTicket()
    setTicket(loadedTicket)
    const serverTime = Date.parse(loadedTicket.serverTimeUtc)
    if (!Number.isFinite(serverTime)) throw new Error('Thời gian máy chủ trong vé thi không hợp lệ.')
    serverClockOffsetRef.current = serverTime - Date.now()
    setClockNow(serverTime)
    setCompletedMainIds([])
    setQuestionIndex(0)
    setActiveFollowUp(null)
    setGeneratingFollowUp(false)
    setInitialTranscript('')
    setTranscriptDraft('')
    setTranscriptReviewPending(false)
    setTranscriptBufferRemaining(0)
    pendingTranscriptAudioRef.current = null
    autoSubmitTriggeredRef.current = false

    const localDrafts = await loadTicketTurnDrafts(loadedTicket.id)
    setPendingTurnDraftsCount(localDrafts.length)

    if (submittedStatuses.has(loadedTicket.status)) {
      if (loadedTicket.status === 'LOCKED') {
        lockWithReason('SERVER_LOCKED', 'Vé thi đã bị khóa từ hệ thống. Vui lòng liên hệ Giám thị hoặc Cán bộ Khảo thí.')
        return false
      }
      try {
        await apiClient.getSubmissionStatus(loadedTicket.id)
        await clearTicketDrafts(loadedTicket.id)
        setPendingTurnDraftsCount(0)
        await window.kioskApi?.authorizeExitAfterSubmit()
        return true
      } catch {
        setNotice('Vé đã có trạng thái nộp; đang chờ xác minh submission từ backend.')
      }
    }

    if (localDrafts.length > 0) {
      setNotice(`Phát hiện ${localDrafts.length} lượt trả lời đã mã hóa cục bộ. Hệ thống sẽ tự động đồng bộ sau khi điểm danh.`)
    }

    return false
  }

  async function syncTicketStateFromServer(currentTicket: StudentWorkstationTicket): Promise<'lobby' | 'submitted' | 'blocked'> {
    const state = await apiClient.getTicketState(currentTicket.id)
    const serverTime = Date.parse(state.serverUtcNow)
    if (Number.isFinite(serverTime)) {
      serverClockOffsetRef.current = serverTime - Date.now()
      setClockNow(serverTime)
    }

    blurCountRef.current = state.violationCount
    setBlurCount(state.violationCount)

    if (state.isKioskLocked) {
      if (submittedStatuses.has(state.status) && state.status !== 'LOCKED') {
        await clearTicketDrafts(currentTicket.id)
        setPendingTurnDraftsCount(0)
        await window.kioskApi?.authorizeExitAfterSubmit()
        apiClient.clearSession()
        return 'submitted'
      }
      lockWithReason(
        'SERVER_LOCKED',
        'Vé thi đã bị khóa trên máy chủ. Không thể tiếp tục hoặc tự ý nộp bài từ màn hình này.',
      )
      return 'blocked'
    }

    const doneMainIds = Array.from(
      new Set(state.followUps.filter((item) => item.followUpIndex === 0).map((item) => item.questionId)),
    )
    setCompletedMainIds(doneMainIds)

    const pendingFollowUp = resolvePendingFollowUpFromState(
      state,
      currentTicket.hasFollowUp ? currentTicket.maxFollowUpQuestions : 0,
    )
    if (pendingFollowUp) {
      const qIdx = currentTicket.questions.findIndex((q) => q.questionId === pendingFollowUp.questionId)
      setQuestionIndex(qIdx >= 0 ? qIdx : 0)
      setActiveFollowUp(pendingFollowUp)
      setNotice('Đã khôi phục câu hỏi phụ đang dang dở từ máy chủ. Bạn có thể tiếp tục trả lời.')
    } else {
      setActiveFollowUp(null)
      const nextIdx = currentTicket.questions.findIndex((q) => !doneMainIds.includes(q.questionId))
      setQuestionIndex(nextIdx < 0 ? Math.max(0, currentTicket.questions.length - 1) : nextIdx)
      if (doneMainIds.length > 0) {
        setNotice(`Đã khôi phục tiến độ ${doneMainIds.length}/${currentTicket.questions.length} câu hỏi từ máy chủ.`)
      }
    }

    return 'lobby'
  }

  async function startMicrophoneTest(): Promise<void> {
    setBusy(true)
    setError('')
    setMicTestSeconds(0)
    setMicLevel(0)
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
      streamRef.current?.getTracks().forEach((track) => track.stop())
      streamRef.current = stream
      const audioContext = new AudioContext()
      const analyser = audioContext.createAnalyser()
      analyser.fftSize = 2048
      audioContext.createMediaStreamSource(stream).connect(analyser)
      const samples = new Uint8Array(analyser.fftSize)
      const startedAt = performance.now()
      const decibelSamples: number[] = []
      const tick = () => {
        const elapsed = Math.min(30, Math.floor((performance.now() - startedAt) / 1000))
        analyser.getByteTimeDomainData(samples)
        let sum = 0
        for (const sample of samples) {
          const normalized = (sample - 128) / 128
          sum += normalized * normalized
        }
        const rms = Math.sqrt(sum / samples.length)
        const decibel = rms > 0 ? Math.max(0, Math.min(100, Math.round(20 * Math.log10(rms) + 100))) : 0
        decibelSamples.push(decibel)
        setMicTestSeconds(elapsed)
        setMicLevel(decibel)
        if (elapsed < 30) {
          analyserFrameRef.current = requestAnimationFrame(tick)
        } else {
          const averageDecibel =
            decibelSamples.length === 0
              ? 0
              : decibelSamples.reduce((acc, value) => acc + value, 0) / decibelSamples.length
          const passingSamples = decibelSamples.filter((value) => value >= 60).length
          const passingRatio = decibelSamples.length === 0 ? 0 : passingSamples / decibelSamples.length
          const isReady = averageDecibel >= 60 && passingRatio >= 0.6
          setMicrophoneReady(isReady)
          setBusy(false)
          if (!isReady) {
            setError(`Mức micro trung bình ${Math.round(averageDecibel)} dB hoặc độ ổn định chưa đạt yêu cầu 60 dB.`)
          } else {
            setNotice(`Micro đạt trung bình ${Math.round(averageDecibel)} dB và ổn định trong 30 giây.`)
          }
          void audioContext.close()
        }
      }
      tick()
    } catch (microphoneError) {
      setBusy(false)
      setError(errorMessage(microphoneError))
    }
  }

  async function handleCheckIn(): Promise<void> {
    if (!ticket || !microphoneReady) {
      setError('Cần hoàn tất kiểm tra micro trước khi điểm danh.')
      return
    }
    setBusy(true)
    setError('')
    try {
      const checkedIn = await apiClient.checkIn(ticket.id)
      const refreshedTicket = await apiClient.getCurrentTicket()
      if (refreshedTicket.questions.length === 0) {
        if (refreshedTicket.shiftStatus.toLowerCase() !== 'in_progress') {
          throw new Error(
            'Ca thi chưa được mở (trạng thái chưa ở "Đang diễn ra"). Vui lòng chờ Khảo thí mở ca thi rồi bấm điểm danh lại.',
          )
        }
        throw new Error('Ca thi chưa có bộ câu hỏi được công bố.')
      }
      const updatedTicket: StudentWorkstationTicket = {
        ...refreshedTicket,
        status: checkedIn.status,
        examSetId: checkedIn.examSetId ?? refreshedTicket.examSetId,
      }
      setTicket(updatedTicket)

      // Replay any pending encrypted turn drafts from previous offline session first.
      await flushPendingTurnDrafts(updatedTicket).catch(() => {
        setNotice('Còn bản nháp cục bộ chưa đồng bộ hết; hệ thống sẽ thử lại khi nộp bài.')
      })

      const targetScreen = await syncTicketStateFromServer(updatedTicket)
      setScreen(targetScreen)
    } catch (checkInError) {
      const conflict = classifyByodConflict(checkInError)
      if (conflict) {
        handleByodConflict(conflict, describeByodConflict(conflict))
        return
      }
      setError(errorMessage(checkInError))
    } finally {
      setBusy(false)
    }
  }

  function attachTrackDisconnectWatcher(stream: MediaStream): void {
    stream.getAudioTracks().forEach((track) => {
      track.onended = () => {
        if (ticketRef.current && !submittedStatuses.has(ticketRef.current.status)) {
          setError('Microphone bị ngắt kết nối đột ngột! Vui lòng kiểm tra lại thiết bị thu âm.')
          void appendAntiCheatLog('DEVICE_DISCONNECTION', 'Microphone bị ngắt kết nối trong giờ thi.', 'HIGH')
        }
      }
    })
  }

  function beginQuestionRecording(): void {
    const stream = streamRef.current
    if (!stream) {
      setError('Micro chưa sẵn sàng.')
      return
    }
    attachTrackDisconnectWatcher(stream)
    startLiveLevelMonitor(stream)
    const recorder = new MediaRecorder(stream, { mimeType: 'audio/webm' })
    currentChunksRef.current = []
    recorder.ondataavailable = (event) => {
      if (event.data.size > 0) currentChunksRef.current.push(event.data)
    }
    recorderRef.current = recorder
    recorder.start(250)
    setRecording(true)
    setQuestionStartedAt(serverNow())
  }

  function stopQuestionRecording(): Promise<Blob> {
    stopLiveLevelMonitor()
    const recorder = recorderRef.current
    if (!recorder || recorder.state === 'inactive') {
      return Promise.resolve(new Blob(currentChunksRef.current, { type: 'audio/webm' }))
    }
    return new Promise((resolve) => {
      recorder.onstop = () => {
        const blob = new Blob(currentChunksRef.current, { type: 'audio/webm' })
        recorderRef.current = null
        setRecording(false)
        resolve(blob)
      }
      recorder.stop()
    })
  }

  async function startExam(): Promise<void> {
    if (!ticket || !microphoneReady) return
    const startAt = Date.parse(ticket.startTime)
    const endAt = Date.parse(ticket.endTime)
    const now = serverNow()
    if (!Number.isFinite(startAt) || !Number.isFinite(endAt) || now < startAt) {
      setError('Ca thi chưa mở theo thời gian do máy chủ cung cấp.')
      return
    }
    if (now >= endAt) {
      setError('Ca thi đã hết thời gian theo máy chủ.')
      return
    }
    setError('')
    setNotice('')
    if (!streamRef.current) {
      try {
        streamRef.current = await navigator.mediaDevices.getUserMedia({ audio: true })
      } catch (streamError) {
        setError(errorMessage(streamError))
        return
      }
    }
    autoSubmitTriggeredRef.current = false
    submitInFlightRef.current = false
    setClockNow(now)
    setScreen('exam')
  }

  function handleToggleRecording(): void {
    if (busy || transcribing || generatingFollowUp || transcriptReviewPending) return
    if (recording) {
      void completeQuestion(false)
    } else {
      setError('')
      setNotice('')
      beginQuestionRecording()
    }
  }

  function handleTranscriptChange(nextValue: string): void {
    setTranscriptDraft(nextValue)
    if (transcriptReviewPending) {
      const evaluation = evaluateTranscriptEdit(initialTranscript, nextValue)
      if (!evaluation.isExceeded) {
        lastValidTranscriptRef.current = nextValue
      }
    }
  }

  function handleRestoreOriginalTranscript(): void {
    setTranscriptDraft(initialTranscript)
    lastValidTranscriptRef.current = initialTranscript
  }

  async function completeQuestion(isAutoBufferFinalize = false): Promise<void> {
    if (!ticket) return
    const question = ticket.questions[questionIndex]
    if (!question) return
    if (transcriptReviewPending && !pendingTranscriptAudioRef.current) {
      setTranscriptReviewPending(false)
      setTranscriptBufferRemaining(0)
      return
    }

    setBusy(true)
    setError('')
    try {
      let audio = pendingTranscriptAudioRef.current
      let rawVoiceTranscript = initialTranscript
      let finalTranscript = transcriptDraft.trim()

      if (!audio) {
        audio = await stopQuestionRecording()
        if (audio.size > 0) {
          setTranscribing(true)
          try {
            const hint = activeFollowUp
              ? `${question.title} ${activeFollowUp.prompt}`
              : `${question.title} ${question.content}`
            const transcription = await apiClient.transcribe(audio, hint, '')
            rawVoiceTranscript = transcription.text.trim()
            finalTranscript = rawVoiceTranscript
          } finally {
            setTranscribing(false)
          }
        } else {
          rawVoiceTranscript = ''
          finalTranscript = ''
        }

        if (ticket.examInputMode === 'VoiceWithTranscriptEdit' && !submitInFlightRef.current) {
          pendingTranscriptAudioRef.current = audio
          const bufferSeconds = Math.max(10, Math.min(300, ticket.transcriptBufferSeconds))
          setInitialTranscript(rawVoiceTranscript)
          setTranscriptDraft(finalTranscript)
          lastValidTranscriptRef.current = finalTranscript
          setTranscriptReviewPending(true)
          setTranscriptBufferRemaining(bufferSeconds)
          setNotice(`Transcript đã sẵn sàng. Bạn có ${bufferSeconds} giây để rà soát thuật ngữ (tối đa 35%).`)
          return
        }
      } else {
        pendingTranscriptAudioRef.current = null
        setTranscriptReviewPending(false)
        setTranscriptBufferRemaining(0)
      }

      const isVoiceOnly = ticket.examInputMode === 'VoiceOnly'
      let isManuallyEdited = false
      if (isVoiceOnly) {
        finalTranscript = rawVoiceTranscript
        isManuallyEdited = false
      } else {
        const editEval = evaluateTranscriptEdit(rawVoiceTranscript, finalTranscript)
        if (editEval.isExceeded) {
          if (isAutoBufferFinalize || submitInFlightRef.current) {
            finalTranscript = (lastValidTranscriptRef.current || rawVoiceTranscript).trim()
            isManuallyEdited = evaluateTranscriptEdit(rawVoiceTranscript, finalTranscript).isManuallyEdited
          } else {
            throw new Error(
              `Mức độ chỉnh sửa (${editEval.percent.toFixed(1)}%) vượt quá giới hạn tối đa 35.0%. Vui lòng giảm chỉnh sửa hoặc khôi phục bản gốc.`,
            )
          }
        } else {
          isManuallyEdited = editEval.isManuallyEdited
        }
      }

      if (audio.size === 0) {
        if (submitInFlightRef.current) return
        throw new Error('Không thu được dữ liệu âm thanh cho lượt trả lời này. Vui lòng kiểm tra micro.')
      }

      const elapsed = questionStartedAt ? Math.max(1, Math.round((serverNow() - questionStartedAt) / 1000)) : 1
      const currentFollowUpIndex = activeFollowUp ? activeFollowUp.followUpIndex : 0
      const currentParentId = activeFollowUp ? activeFollowUp.parentSubmissionId : null
      const currentPrompt = activeFollowUp ? activeFollowUp.prompt : null

      setNotice('Đang tải audio lên kho lưu trữ và niêm phong SHA-256…')
      const answerResult = await uploadAndSubmitTurn(
        ticket,
        question.questionId,
        currentFollowUpIndex,
        currentParentId,
        currentPrompt,
        audio,
        rawVoiceTranscript,
        finalTranscript,
        isManuallyEdited,
        elapsed,
      )

      const updatedCompletedMainIds =
        currentFollowUpIndex === 0 && !completedMainIds.includes(question.questionId)
          ? [...completedMainIds, question.questionId]
          : completedMainIds
      if (updatedCompletedMainIds !== completedMainIds) {
        setCompletedMainIds(updatedCompletedMainIds)
      }

      if (submitInFlightRef.current) return

      // Check AI Follow-Up if enabled and allowed
      if (answerResult.canRequestFollowUp && ticket.hasFollowUp) {
        setGeneratingFollowUp(true)
        setNotice('Đang tạo câu hỏi phụ từ AI…')
        try {
          const followUpResponse = await apiClient.requestNextFollowUp(ticket.id, answerResult.submissionId)
          if (followUpResponse.hasMoreFollowUp && followUpResponse.followUpPrompt) {
            setActiveFollowUp({
              questionId: question.questionId,
              parentSubmissionId: followUpResponse.parentSubmissionId,
              followUpIndex: followUpResponse.followUpIndex,
              maxFollowUpQuestions: followUpResponse.maxFollowUpQuestions,
              prompt: followUpResponse.followUpPrompt,
            })
            setInitialTranscript('')
            setTranscriptDraft('')
            lastValidTranscriptRef.current = ''
            setNotice(`Đã nhận câu hỏi phụ ${followUpResponse.followUpIndex}/${followUpResponse.maxFollowUpQuestions}. Nhấn nút Ghi âm hoặc phím Space khi bạn sẵn sàng trả lời.`)
            return
          }
        } catch (followUpError) {
          const conflict = classifyByodConflict(followUpError)
          if (conflict) {
            handleByodConflict(conflict, describeByodConflict(conflict))
            return
          }
          // Graceful degradation: proceed to next main question if follow-up service degraded.
        } finally {
          setGeneratingFollowUp(false)
        }
      }

      setActiveFollowUp(null)
      const nextUnansweredIndex = ticket.questions.findIndex(
        (q) => !updatedCompletedMainIds.includes(q.questionId),
      )
      if (nextUnansweredIndex < 0) {
        setInitialTranscript('')
        setTranscriptDraft('')
        setNotice('Đã hoàn thành toàn bộ câu hỏi. Nhấn "Nộp bài và khóa kiosk" để kết thúc.')
        return
      }

      setQuestionIndex(nextUnansweredIndex)
      setInitialTranscript('')
      setTranscriptDraft('')
      lastValidTranscriptRef.current = ''
      setNotice('')
    } catch (questionError) {
      const conflict = classifyByodConflict(questionError)
      if (conflict) {
        handleByodConflict(conflict, describeByodConflict(conflict))
        return
      }
      setError(errorMessage(questionError))
    } finally {
      setBusy(false)
    }
  }

  async function submitExam(allowIncomplete = false): Promise<void> {
    if (!ticket || submitInFlightRef.current) return
    submitInFlightRef.current = true
    setBusy(true)
    setError('')
    setNotice('Đang hoàn tất đồng bộ và nộp bài thi…')

    try {
      if (recording || pendingTranscriptAudioRef.current) {
        await completeQuestion(true)
        if (pendingTranscriptAudioRef.current) {
          await completeQuestion(true)
        }
      }

      await flushPendingTurnDrafts(ticket)

      if (!allowIncomplete && completedMainIds.length < ticket.questions.length) {
        const refreshedState = await apiClient.getTicketState(ticket.id)
        const serverMainCount = new Set(
          refreshedState.followUps.filter((item) => item.followUpIndex === 0).map((item) => item.questionId),
        ).size
        if (serverMainCount < ticket.questions.length) {
          throw new Error('Chưa hoàn thành đủ toàn bộ câu hỏi chính.')
        }
      }

      const response = await retryOnce(() => apiClient.submitExam(ticket.id, []))
      if (!submittedStatuses.has(response.status)) {
        throw new Error('Máy chủ chưa xác nhận trạng thái SUBMITTED.')
      }

      await clearTicketDrafts(ticket.id)
      setPendingTurnDraftsCount(0)
      await window.kioskApi?.authorizeExitAfterSubmit()
      apiClient.clearSession()
      setScreen('submitted')
      setNotice('Máy chủ đã xác nhận lưu bài an toàn. Không hiển thị điểm tại kiosk.')
      stopMicrophone()
    } catch (submitError) {
      const conflict = classifyByodConflict(submitError)
      if (conflict) {
        handleByodConflict(conflict, describeByodConflict(conflict))
        return
      }
      setError(errorMessage(submitError))
    } finally {
      submitInFlightRef.current = false
      setBusy(false)
    }
  }

  async function handleCheckBlockedStatus(): Promise<void> {
    if (!ticket) return
    setCheckingBlockStatus(true)
    setError('')
    try {
      const state = await apiClient.getTicketState(ticket.id)
      if (submittedStatuses.has(state.status) && state.status !== 'LOCKED') {
        await clearTicketDrafts(ticket.id)
        await window.kioskApi?.authorizeExitAfterSubmit()
        apiClient.clearSession()
        setScreen('submitted')
        setNotice('Bài thi đã được xác nhận lưu trên máy chủ.')
        return
      }
      if (!state.isKioskLocked && state.status === 'IN_PROGRESS') {
        setNotice('Vé thi đang ở trạng thái mở. Vui lòng đăng nhập lại hoặc liên hệ Giám thị để tiếp tục.')
        return
      }
      setNotice('Vé thi vẫn đang bị khóa trên máy chủ. Vui lòng tiếp tục liên hệ Giám thị hoặc Cán bộ Khảo thí.')
    } catch (checkError) {
      setError(errorMessage(checkError))
    } finally {
      setCheckingBlockStatus(false)
    }
  }

  async function handleExit(): Promise<void> {
    if (!window.kioskApi || isExitPending || screen !== 'submitted') return
    setIsExitPending(true)
    try {
      await window.kioskApi.requestExit()
    } finally {
      setIsExitPending(false)
    }
  }

  const currentQuestion = ticket?.questions[questionIndex] ?? null
  const activeStepIndex = getStepIndex(screen)
  const showStepper = screen === 'pre-check' || screen === 'lobby'
  const isCenteredStage =
    screen === 'sign-in' || screen === 'waiting-ticket' || screen === 'submitted' || screen === 'blocked'
  const containerWidthClass =
    screen === 'sign-in'
      ? 'is-narrow'
      : screen === 'waiting-ticket' || screen === 'submitted' || screen === 'blocked'
        ? 'is-medium'
        : screen === 'exam'
          ? 'is-wide'
          : ''

  return (
    <div className="kiosk-app">
      <header className="topbar">
        <div className="brand-lockup" aria-label="Oral Exam Desktop Kiosk">
          <span className="brand-mark" aria-hidden="true">
            OE
          </span>
          <span className="brand-text">
            <strong>Oral Exam</strong>
            <small>Phòng thi vấn đáp</small>
          </span>
        </div>

        <div>
          {showStepper ? (
            <nav className="stepper" aria-label="Tiến trình vào thi">
              {stepperSteps.map((step, index) => {
                const isDone = index < activeStepIndex
                const isActive = index === activeStepIndex
                return (
                  <Fragment key={step.id}>
                    {index > 0 ? <span className="stepper-sep" aria-hidden="true" /> : null}
                    <span className={`stepper-item ${isActive ? 'is-active' : ''} ${isDone ? 'is-done' : ''}`}>
                      <span className="stepper-dot" aria-hidden="true">
                        {isDone ? '✓' : index + 1}
                      </span>
                      <span>{step.label}</span>
                    </span>
                  </Fragment>
                )
              })}
            </nav>
          ) : null}
        </div>

        <div className="topbar-right">
          {studentName && screen !== 'sign-in' ? <span className="student-chip">{studentName}</span> : null}
          <span className="status-pill" role="status">
            <span className={`status-dot ${runtimeInfo.altTabTestMode ? 'is-test' : ''}`} aria-hidden="true" />
            {runtimeInfo.altTabTestMode
              ? 'Chế độ thử nghiệm'
              : runtimeInfo.isKioskMode
                ? 'Kiosk an toàn'
                : 'Cửa sổ dev'}
          </span>
          {screen === 'submitted' ? (
            <button className="btn btn-ghost" type="button" onClick={handleExit} disabled={isExitPending}>
              {isExitPending ? 'Đang đóng…' : 'Thoát'}
            </button>
          ) : null}
        </div>
      </header>

      <main className={`main-stage ${isCenteredStage ? 'is-centered' : ''}`}>
        <div className={`stage-container ${containerWidthClass}`}>
          {notice || error ? (
            <div className="banner-stack">
              {notice ? (
                <div className="banner banner-info" role="status">
                  <span className="banner-badge" aria-hidden="true">
                    i
                  </span>
                  <span>{notice}</span>
                </div>
              ) : null}
              {error ? (
                <div className="banner banner-error" role="alert">
                  <span className="banner-badge" aria-hidden="true">
                    !
                  </span>
                  <span>{error}</span>
                </div>
              ) : null}
            </div>
          ) : null}

          {screen === 'sign-in' ? (
            <SignInScreen
              username={username}
              password={password}
              newPassword={newPassword}
              mustChangePassword={mustChangePassword}
              busy={busy}
              appVersion={runtimeInfo.appVersion}
              isKioskMode={runtimeInfo.isKioskMode}
              onUsername={setUsername}
              onPassword={setPassword}
              onNewPassword={setNewPassword}
              onLogin={handleLogin}
              onChangePassword={handleChangePassword}
            />
          ) : null}

          {screen === 'waiting-ticket' ? (
            <WaitingTicketScreen studentName={studentName} busy={busy} onRetry={retryTicketLoad} />
          ) : null}

          {screen === 'pre-check' ? (
            <PreCheckScreen
              microphoneReady={microphoneReady}
              micTestSeconds={micTestSeconds}
              micLevel={micLevel}
              displayCount={runtimeInfo.displayCount}
              isKioskMode={runtimeInfo.isKioskMode}
              altTabTestMode={runtimeInfo.altTabTestMode}
              securitySnapshot={securitySnapshot}
              scanningEnvironment={scanningEnvironment}
              busy={busy}
              onTest={startMicrophoneTest}
              onRescanEnvironment={refreshSecuritySnapshot}
              onCheckIn={handleCheckIn}
            />
          ) : null}

          {screen === 'lobby' && ticket ? (
            <LobbyScreen
              ticket={ticket}
              offlineDraftAvailable={pendingTurnDraftsCount > 0 || completedMainIds.length > 0}
              hasCompleteDraft={
                completedMainIds.length >= ticket.questions.length &&
                !activeFollowUp &&
                pendingTurnDraftsCount === 0
              }
              onStart={startExam}
              onResumeSubmit={() => submitExam(false)}
            />
          ) : null}

          {screen === 'exam' && ticket && currentQuestion ? (
            <ExamScreen
              ticket={ticket}
              question={currentQuestion}
              questionIndex={questionIndex}
              completedMainCount={completedMainIds.length}
              activeFollowUp={activeFollowUp}
              generatingFollowUp={generatingFollowUp}
              initialTranscript={initialTranscript}
              transcript={transcriptDraft}
              recording={recording}
              recordingElapsedSeconds={recordingElapsedSeconds}
              transcribing={transcribing}
              busy={busy}
              blurCount={blurCount}
              remainingSeconds={Math.max(0, Math.ceil((Date.parse(ticket.endTime) - clockNow) / 1000))}
              transcriptReviewPending={transcriptReviewPending}
              transcriptBufferRemaining={transcriptBufferRemaining}
              onToggleRecording={handleToggleRecording}
              onTranscript={handleTranscriptChange}
              onRestoreOriginalTranscript={handleRestoreOriginalTranscript}
              onComplete={() => completeQuestion(false)}
              onSubmit={submitExam}
            />
          ) : null}

          {screen === 'submitted' ? (
            <SubmittedScreen isExitPending={isExitPending} onExit={handleExit} />
          ) : null}

          {screen === 'blocked' ? (
            <BlockedScreen
              blurCount={blurCount}
              reason={blockReason}
              detailMessage={blockDetail}
              checkingStatus={checkingBlockStatus}
              onCheckStatus={handleCheckBlockedStatus}
            />
          ) : null}
        </div>
      </main>
    </div>
  )
}

export default App
