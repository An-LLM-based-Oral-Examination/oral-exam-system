import { classService } from '@/services/class.service'
import type { StudentClassItem } from '@/types/class.types'
import { useCallback, useEffect, useRef, useState, type ClipboardEvent, type KeyboardEvent } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, Clock3, ClipboardCheck, KeyRound, LoaderCircle, Lock, Mic2, RefreshCw, TimerReset, UserRound, Users, UsersRound } from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass } from '@/components/common/formFieldClasses'
import { StatusToast } from '@/components/common/toast'
import { toast } from '@/components/common/toastStore'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { BannerFact, PageBanner } from '@/design/ui'
import { ds } from '@/design/system'
import { scheduleUi } from '@/lib/scheduleUi'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import { inClassShiftService } from '@/services/inClassShift.service'
import { createInClassShiftHubConnection } from '@/services/signalr.service'
import type { EnterInClassExamResult, InClassShift, InClassShiftStatus, StudentExamResult } from '@/types/inClassShift.types'
import {
  applyCompletedInClassShifts,
  loadInClassExamSession,
  markInClassShiftCompleted,
  reconcileCompletedInClassShifts,
  saveInClassExamSession,
} from '@/utils/inClassExamSessionStorage'

type MicState = 'idle' | 'checking' | 'testing' | 'ready' | 'error' | 'unsupported'
type MicIssue = '' | 'permission' | 'silent'

const MIC_VOICE_THRESHOLD = 0.012
const MIC_TEST_LIMIT_MS = 10000
const MIC_VOICE_HOLD_MS = 1500
const OTP_LENGTH = 6

function sanitizeOtp(value: string) {
  return value.replace(/\D/g, '').slice(0, OTP_LENGTH)
}

function readMicrophoneLevel(samples: Uint8Array) {
  let squareSum = 0
  for (const sample of samples) {
    const normalized = (sample - 128) / 128
    squareSum += normalized * normalized
  }
  return Math.sqrt(squareSum / samples.length)
}

const MIC_STATUS: Record<MicState, { title: string; helper: string; color: string; dot: string }> = {
  idle: {
    title: 'Chưa kiểm tra micro',
    helper: 'Nói thử một câu trước khi vào ca.',
    color: 'text-[#5d6d83]',
    dot: 'bg-[#9aa9bc]',
  },
  checking: {
    title: 'Đang kết nối micro…',
    helper: 'Vui lòng cho phép trình duyệt sử dụng micro.',
    color: 'text-[#1C4D8D]',
    dot: 'bg-[#1C4D8D] animate-pulse motion-reduce:animate-none',
  },
  testing: {
    title: 'Hãy nói thử một câu',
    helper: 'Thanh mức nhích lên khi micro thu được giọng.',
    color: 'text-[#1C4D8D]',
    dot: 'bg-[#1C4D8D] animate-pulse motion-reduce:animate-none',
  },
  ready: {
    title: 'Micro đã sẵn sàng',
    helper: 'Đã thu được giọng nói.',
    color: 'text-[#16836a]',
    dot: 'bg-[#33c391] shadow-[0_0_8px_rgba(51,195,145,0.65)]',
  },
  error: {
    title: 'Không thể truy cập micro',
    helper: 'Hãy cấp quyền micro rồi kiểm tra lại.',
    color: 'text-[#cf4b61]',
    dot: 'bg-[#ed6075]',
  },
  unsupported: {
    title: 'Trình duyệt chưa hỗ trợ kiểm tra',
    helper: 'Thử Chrome hoặc Edge phiên bản mới.',
    color: 'text-[#a26812]',
    dot: 'bg-[#e1a13f]',
  },
}

/** Thời lượng làm bài của SV (phút). Không lấy khung giờ mở/đóng ca. */
function examDurationMinutesOf(shift: InClassShift) {
  const minutes = shift.examDurationMinutes
  if (typeof minutes === 'number' && Number.isFinite(minutes) && minutes > 0) {
    return Math.min(60, Math.max(1, Math.round(minutes)))
  }
  return 20
}

const SHIFT_STATUS = {
  SCHEDULED: { label: 'Sắp mở', chip: 'border-[#f3e0bf] bg-[#fff6e8] text-[#c26a12]' },
  ACTIVE: { label: 'Đang mở', chip: 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]' },
  FINALIZED: { label: 'Đã có kết quả', chip: 'border-[#82aedb]/55 bg-[#F4FBFD] text-[#1C4D8D]' },
  CLOSED: { label: 'Đã kết thúc', chip: 'border-[#f3c2c8] bg-[#fdecec] text-[#c2415b]' },
} as const

const MY_PARTICIPATION_STATUS = {
  JOINED: { label: 'Đang làm bài', chip: 'border-[#82aedb]/55 bg-[#F4FBFD] text-[#1C4D8D]' },
  IN_PROGRESS: { label: 'Đang làm bài', chip: 'border-[#82aedb]/55 bg-[#F4FBFD] text-[#1C4D8D]' },
  SUBMITTED: { label: 'Đã nộp bài', chip: 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]' },
  GRADED: { label: 'Đã nộp bài', chip: 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]' },
  FINALIZED: { label: 'Đã nộp bài', chip: 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]' },
  COMPLETED: { label: 'Đã nộp bài', chip: 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]' },
} as const

function formatExamWindow(shift: InClassShift) {
  const start = new Date(shift.startTime)
  if (!Number.isFinite(start.getTime())) return 'Chưa xếp giờ'
  const startDate = start.toLocaleDateString('vi-VN')
  const startClock = start.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
  const end = shift.endTime ? new Date(shift.endTime) : null
  if (!end || !Number.isFinite(end.getTime())) return `${startDate} · ${startClock}`
  const endDate = end.toLocaleDateString('vi-VN')
  const endClock = end.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
  if (startDate === endDate) return `${startDate} · ${startClock}–${endClock}`
  return `${startDate} ${startClock} – ${endDate} ${endClock}`
}

function questionLabel(shift: InClassShift) {
  const followUp = shift.hasFollowUp && shift.maxFollowUpQuestions > 0 ? ` · phụ ${shift.maxFollowUpQuestions}` : ''
  return `${shift.questionCount} câu${followUp}`
}

function getErrorMessage(error: unknown) {
  return userMessage(error, 'Không xử lý được ca thi trên lớp. Vui lòng thử lại.')
}

function participationDone(status?: string | null) {
  const normalized = (status ?? '').toUpperCase()
  return normalized === 'SUBMITTED' || normalized === 'GRADED' || normalized === 'FINALIZED' || normalized === 'COMPLETED'
}

type StatusFilter = 'ALL' | InClassShiftStatus

const STATUS_FILTERS: Array<{
  value: StatusFilter
  label: string
  countIdleClass: string
  dotClass: string
}> = [
  {
    value: 'ALL',
    label: 'Tất cả',
    countIdleClass: 'bg-[#e8eef6] text-[#1C4D8D]',
    dotClass: 'bg-[#1C4D8D]',
  },
  {
    value: 'SCHEDULED',
    label: 'Sắp mở',
    countIdleClass: 'bg-[#fff4df] text-[#8d6118]',
    dotClass: 'bg-[#d4a017]',
  },
  {
    value: 'ACTIVE',
    label: 'Đang mở',
    countIdleClass: 'bg-[#e8f7f1] text-[#197158]',
    dotClass: 'bg-[#1a9a72]',
  },
  {
    value: 'CLOSED',
    label: 'Đã kết thúc',
    countIdleClass: 'bg-[#fff4df] text-[#8d6118]',
    dotClass: 'bg-[#d4a017]',
  },
  {
    value: 'FINALIZED',
    label: 'Đã có kết quả',
    countIdleClass: 'bg-[#eef1f5] text-[#52647d]',
    dotClass: 'bg-[#7a8aa0]',
  },
]

export function StudentInClassExamPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const { classId: routeClassId } = useParams()
  const [classDetails, setClassDetails] = useState<StudentClassItem[] | null>(null)

  useEffect(() => {
    if (!routeClassId) return
    let active = true
    void classService.listMyClasses()
      .then((items) => { if (active) setClassDetails(items) })
      .catch(() => { if (active) setClassDetails([]) })
    return () => { active = false }
  }, [routeClassId])

  const [shifts, setShifts] = useState<InClassShift[]>([])
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('ALL')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [joinTarget, setJoinTarget] = useState<InClassShift | null>(null)
  const [joinMode, setJoinMode] = useState<'enter' | 'resume'>('enter')
  const [otp, setOtp] = useState('')
  const [otpError, setOtpError] = useState('')
  const [micError, setMicError] = useState('')
  const [entering, setEntering] = useState(false)
  const [micState, setMicState] = useState<MicState>('idle')
  const [micIssue, setMicIssue] = useState<MicIssue>('')
  const [micName, setMicName] = useState('')
  const [micLevel, setMicLevel] = useState(0)
  const micTestRef = useRef<{ cancelled: boolean; stop: () => void } | null>(null)
  const otpInputRefs = useRef<Array<HTMLInputElement | null>>([])
  const [resultsShift, setResultsShift] = useState<InClassShift | null>(null)
  const [results, setResults] = useState<StudentExamResult[]>([])
  const [resultsLoading, setResultsLoading] = useState(false)
  const micStatus = MIC_STATUS[micState]

  const load = async () => {
    setLoading(true)
    setError('')
    try {
      const list = await inClassShiftService.listStudentShifts()
      await reconcileCompletedInClassShifts(list, (shiftId) =>
        inClassShiftService.finishStudentExam(shiftId),
      )
      const latest = await inClassShiftService.listStudentShifts()
      setShifts(applyCompletedInClassShifts(latest))
    } catch (loadError) {
      setError(getErrorMessage(loadError))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (loadInClassExamSession()) {
      navigate('/in-class-exam/session', { replace: true })
      return
    }
    const cancelLoad = scheduleUi(() => {
      void load()
    })
    const timer = window.setInterval(() => {
      void inClassShiftService
        .listStudentShifts()
        .then((list) => setShifts(applyCompletedInClassShifts(list)))
        .catch(() => undefined)
    }, 15000)
    return () => {
      cancelLoad()
      window.clearInterval(timer)
    }
  }, [navigate])

  useEffect(() => {
    const state = location.state as { notice?: string; finishedShiftId?: string } | null
    if (!state?.notice && !state?.finishedShiftId) return
    return scheduleUi(() => {
      if (state.notice) setNotice(state.notice)
      if (state.finishedShiftId) {
        markInClassShiftCompleted(state.finishedShiftId)
        setShifts((current) =>
          applyCompletedInClassShifts(
            current.map((item) =>
              item.id === state.finishedShiftId ? { ...item, myParticipationStatus: 'SUBMITTED' } : item,
            ),
          ),
        )
      }
      navigate(location.pathname, { replace: true, state: null })
      void load()
    })
  }, [location.pathname, location.state, navigate])

  const stopMicTest = useCallback(() => {
    micTestRef.current?.stop()
    micTestRef.current = null
  }, [])

  const closeJoin = useCallback(() => {
    if (entering) return
    stopMicTest()
    setMicLevel(0)
    setMicState('idle')
    setMicIssue('')
    setMicName('')
    setOtp('')
    setOtpError('')
    setMicError('')
    setJoinTarget(null)
  }, [entering, stopMicTest])

  useEffect(() => {
    if (!joinTarget) return
    const timer = window.setTimeout(() => otpInputRefs.current[0]?.focus(), 40)
    return () => window.clearTimeout(timer)
  }, [joinTarget])

  const otpDigits = Array.from({ length: OTP_LENGTH }, (_, index) => otp[index] ?? '')
  const otpComplete = otp.length === OTP_LENGTH

  const focusOtpAt = (index: number) => {
    const clamped = Math.max(0, Math.min(OTP_LENGTH - 1, index))
    otpInputRefs.current[clamped]?.focus()
    otpInputRefs.current[clamped]?.select()
  }

  const writeOtp = (next: string) => {
    setOtp(sanitizeOtp(next))
    setOtpError('')
  }

  const handleOtpDigitChange = (index: number, value: string) => {
    const cleaned = sanitizeOtp(value)
    if (cleaned.length > 1) {
      writeOtp(cleaned)
      focusOtpAt(Math.min(cleaned.length, OTP_LENGTH - 1))
      return
    }
    const chars = otpDigits.map((digit, digitIndex) => (digitIndex === index ? cleaned : digit))
    writeOtp(chars.join(''))
    if (cleaned) focusOtpAt(index + 1)
  }

  const handleOtpKeyDown = (index: number, event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Backspace') {
      event.preventDefault()
      if (otpDigits[index]) {
        const chars = [...otpDigits]
        chars[index] = ''
        writeOtp(chars.join(''))
        return
      }
      if (index > 0) {
        const chars = [...otpDigits]
        chars[index - 1] = ''
        writeOtp(chars.join(''))
        focusOtpAt(index - 1)
      }
      return
    }
    if (event.key === 'ArrowLeft') {
      event.preventDefault()
      focusOtpAt(index - 1)
      return
    }
    if (event.key === 'ArrowRight') {
      event.preventDefault()
      focusOtpAt(index + 1)
      return
    }
  }

  const handleOtpPaste = (event: ClipboardEvent<HTMLInputElement>) => {
    event.preventDefault()
    const pasted = sanitizeOtp(event.clipboardData.getData('text'))
    if (!pasted) return
    writeOtp(pasted)
    focusOtpAt(Math.min(pasted.length, OTP_LENGTH) - 1)
  }

  useEffect(
    () => () => {
      micTestRef.current?.stop()
    },
    [],
  )

  const openJoin = (shift: InClassShift, mode: 'enter' | 'resume' = 'enter') => {
    if (participationDone(shift.myParticipationStatus)) {
      setError('Bạn đã hoàn thành ca này và không thể tham gia lại.')
      return
    }
    stopMicTest()
    setJoinMode(mode)
    setJoinTarget(shift)
    setOtp('')
    setOtpError('')
    setMicError('')
    setError('')
    setNotice('')
    setMicState('idle')
    setMicIssue('')
    setMicName('')
    setMicLevel(0)
  }

  const beginSessionFromResult = (shift: InClassShift, result: EnterInClassExamResult) => {
    if (result.timedOut || participationDone(result.status)) {
      markInClassShiftCompleted(shift.id)
      setShifts((current) =>
        applyCompletedInClassShifts(
          current.map((item) =>
            item.id === shift.id ? { ...item, myParticipationStatus: 'SUBMITTED' } : item,
          ),
        ),
      )
      setJoinTarget(null)
      setNotice(
        result.timedOut
          ? 'Hết thời gian làm bài khi bạn vắng mặt. Bài đã được nộp tự động để AI chấm.'
          : 'Bạn đã nộp bài ca này và không thể tham gia lại.',
      )
      return false
    }

    const endsMs = Date.parse(result.examEndsAt)
    const joinedMs = Date.parse(result.joinedAt)
    const durationMs = examDurationMinutesOf(shift) * 60 * 1000
    const examEndsAt = Number.isFinite(endsMs)
      ? endsMs
      : Number.isFinite(joinedMs)
        ? joinedMs + Math.max(durationMs, Math.max(0, result.remainingSeconds) * 1000)
        : 0
    const startedAt = Number.isFinite(joinedMs)
      ? joinedMs
      : examEndsAt - durationMs

    saveInClassExamSession({
      shift,
      questions: result.questions,
      startedAt,
      examEndsAt,
      joinedAt: result.joinedAt,
    })
    setResultsShift(null)
    setResults([])
    setJoinTarget(null)
    setOtp('')
    setMicState('idle')
    setMicIssue('')
    setMicName('')
    setMicLevel(0)
    navigate('/in-class-exam/session')
    return true
  }

  const handleMicCheck = async () => {
    const AudioContextConstructor =
      window.AudioContext ??
      (window as typeof window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext

    if (!navigator.mediaDevices?.getUserMedia || !AudioContextConstructor) {
      setMicIssue('')
      setMicState('unsupported')
      return
    }

    stopMicTest()
    setMicError('')
    setMicLevel(0)
    setMicIssue('')
    setMicName('')
    setMicState('checking')

    const session = {
      cancelled: false,
      stop: () => undefined as void,
    }
    micTestRef.current = session

    let stream: MediaStream | null = null
    let audioContext: AudioContext | null = null
    let sourceNode: MediaStreamAudioSourceNode | null = null

    session.stop = () => {
      session.cancelled = true
      sourceNode?.disconnect()
      sourceNode = null
      if (audioContext) void audioContext.close().catch(() => undefined)
      audioContext = null
      stream?.getTracks().forEach((track) => track.stop())
      stream = null
    }

    try {
      stream = await navigator.mediaDevices.getUserMedia({
        audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
      })
      if (session.cancelled) return

      const audioTrack = stream.getAudioTracks()[0]
      if (!audioTrack || audioTrack.readyState !== 'live') {
        throw new Error('Microphone is unavailable.')
      }

      audioContext = new AudioContextConstructor()
      if (audioContext.state === 'suspended') await audioContext.resume()
      if (session.cancelled) return

      const analyser = audioContext.createAnalyser()
      analyser.fftSize = 2048
      analyser.smoothingTimeConstant = 0.35
      sourceNode = audioContext.createMediaStreamSource(stream)
      sourceNode.connect(analyser)

      setMicName(audioTrack.label || 'Microphone mặc định')
      setMicState('testing')

      const samples = new Uint8Array(analyser.fftSize)
      const startedAt = performance.now()
      let peak = 0
      let heardAt = 0

      while (!session.cancelled && performance.now() - startedAt < MIC_TEST_LIMIT_MS) {
        analyser.getByteTimeDomainData(samples)
        const level = readMicrophoneLevel(samples)
        peak = Math.max(peak, level)
        setMicLevel(Math.min(1, level / 0.08))
        if (level >= MIC_VOICE_THRESHOLD && heardAt === 0) heardAt = performance.now()
        if (heardAt > 0 && performance.now() - heardAt >= MIC_VOICE_HOLD_MS) break
        await new Promise((resolve) => window.setTimeout(resolve, 80))
      }

      if (session.cancelled) return

      const heardVoice = !audioTrack.muted && (heardAt > 0 || peak >= MIC_VOICE_THRESHOLD)
      if (!heardVoice) {
        setMicLevel(0)
        setMicIssue('silent')
        setMicState('error')
        return
      }

      setMicLevel(0)
      setMicIssue('')
      setMicState('ready')
    } catch {
      if (session.cancelled) return
      setMicLevel(0)
      setMicIssue('permission')
      setMicState('error')
    } finally {
      session.stop()
      if (micTestRef.current === session) micTestRef.current = null
    }
  }

  const enter = async () => {
    if (!joinTarget || entering) return
    const code = sanitizeOtp(otp)
    const nextOtpError =
      joinMode === 'enter' && code.length !== OTP_LENGTH
        ? `Nhập đủ ${OTP_LENGTH} số mã đang chiếu trên lớp.`
        : ''
    const nextMicError = micState !== 'ready' ? 'Hãy kiểm tra micro trước khi vào làm bài.' : ''
    setOtpError(nextOtpError)
    setMicError(nextMicError)
    if (nextOtpError) {
      focusOtpAt(Math.min(code.length, OTP_LENGTH - 1))
      return
    }
    if (nextMicError) return
    setEntering(true)
    setError('')
    setNotice('')
    try {
      const duration = examDurationMinutesOf(joinTarget)
      const result =
        joinMode === 'resume'
          ? await inClassShiftService.resumeStudentShift(joinTarget.id, duration)
          : await inClassShiftService.enterStudentShift(joinTarget.id, code, duration)
      stopMicTest()
      beginSessionFromResult(joinTarget, result)
    } catch (enterError) {
      const message = getErrorMessage(enterError)
      toast.error(message)
      if (/hoàn thành|không thể tham gia lại|đã nộp|hết thời gian làm bài/i.test(message)) {
        markInClassShiftCompleted(joinTarget.id)
        setShifts((current) =>
          applyCompletedInClassShifts(
            current.map((item) =>
              item.id === joinTarget.id ? { ...item, myParticipationStatus: 'SUBMITTED' } : item,
            ),
          ),
        )
        setJoinTarget(null)
        setNotice(
          /hết thời gian làm bài/i.test(message)
            ? 'Hết thời gian làm bài. Bài đã được nộp tự động để AI chấm.'
            : 'Bạn đã nộp bài ca này và không thể tham gia lại.',
        )
      }
    } finally {
      setEntering(false)
    }
  }

  const openResults = async (shift: InClassShift) => {
    if (shift.status !== 'FINALIZED') return
    setResultsLoading(true)
    setError('')
    setResultsShift(shift)
    try {
      setResults(await inClassShiftService.listStudentResults(shift.id))
    } catch (resultError) {
      setResults([])
      setError(getErrorMessage(resultError))
    } finally {
      setResultsLoading(false)
    }
  }

  useEffect(() => {
    const params = new URLSearchParams(location.search)
    const resultId = params.get('result')
    const shiftId = params.get('shift')
    const target = shifts.find((shift) => shift.id === (resultId ?? shiftId) && (!routeClassId || shift.classId === routeClassId))
    if (!target) return

    const timer = window.setTimeout(() => {
      if (resultId && target.status === 'FINALIZED' && participationDone(target.myParticipationStatus)) {
        void openResults(target)
      } else if (shiftId && target.status === 'ACTIVE' && !participationDone(target.myParticipationStatus)) {
        const status = target.myParticipationStatus?.toUpperCase()
        openJoin(target, status === 'JOINED' || status === 'IN_PROGRESS' ? 'resume' : 'enter')
      }
      navigate(location.pathname, { replace: true, state: location.state })
    }, 0)
    return () => window.clearTimeout(timer)
    // Consume the dashboard link once when the matching shift has loaded.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [location.search, location.pathname, location.state, navigate, routeClassId, shifts])

  useEffect(() => {
    if (!resultsShift) return
    const activeShiftId = resultsShift.id
    let disposed = false
    const hubUrl = import.meta.env.VITE_IN_CLASS_SIGNALR_HUB_URL || '/hubs/in-class-shift'
    const connection = createInClassShiftHubConnection(
      hubUrl,
      () => sessionStorage.getItem('access_token') ?? '',
    )

    const refreshResults = async () => {
      try {
        const latest = await inClassShiftService.listStudentResults(activeShiftId)
        if (!disposed) {
          setResults(latest)
        }
      } catch {
        // Không làm gián đoạn cửa sổ kết quả nếu một lượt đồng bộ thất bại
      }
    }

    connection.on('InClassSubmissionGraded', () => {
      void refreshResults()
    })
    connection.on('ReceiveGradingResult', () => {
      void refreshResults()
    })
    connection.on('InClassShiftGradesFinalized', () => {
      void refreshResults()
      void load()
    })
    connection.onreconnected(() => {
      if (disposed) return
      void connection.invoke('JoinShift', activeShiftId)
        .then(() => Promise.all([refreshResults(), load()]))
        .catch(() => undefined)
    })

    void connection
      .start()
      .then(() => {
        if (disposed) return
        return connection.invoke('JoinShift', activeShiftId)
      })
      .catch(() => undefined)

    const pollTimer = window.setInterval(() => {
      void refreshResults()
    }, 4000)

    return () => {
      disposed = true
      window.clearInterval(pollTimer)
      connection.off('InClassSubmissionGraded')
      connection.off('ReceiveGradingResult')
      connection.off('InClassShiftGradesFinalized')
      void connection.invoke('LeaveShift', activeShiftId)
        .catch(() => undefined)
        .finally(() => connection.stop().catch(() => undefined))
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [resultsShift?.id])

  const classId = routeClassId ?? new URLSearchParams(location.search).get('classId')
  const classDetail = classDetails?.find((item) => item.classId === classId)
  const orderedShifts = shifts.filter((shift) => !classId || shift.classId === classId).sort((left, right) => {
    const rank = { ACTIVE: 0, SCHEDULED: 1, FINALIZED: 2, CLOSED: 3 }
    return rank[left.status] - rank[right.status]
  })
  const filteredShifts = statusFilter === 'ALL'
    ? orderedShifts
    : orderedShifts.filter((shift) => shift.status === statusFilter)
  const scoredResults = results.filter((item) => item.finalScore != null || item.aiScore != null)
  const examScoreLabel =
    scoredResults.length === 0
      ? '—'
      : (
          scoredResults.reduce((sum, item) => sum + (item.finalScore ?? item.aiScore ?? 0), 0) /
          scoredResults.length
        ).toFixed(1)
  const examLecturerNote = [
    ...new Set(results.map((item) => item.lecturerNotes?.trim()).filter((note): note is string => Boolean(note))),
  ].join(' ')

  return (
    <div lang="vi" className={cn(ds.page, routeClassId && 'pb-8 [&>a:first-child]:mb-3')}>
      {classId ? (
        <Link
          to="/classes"
          className="inline-flex min-h-8 w-fit items-center gap-1.5 rounded-lg text-base font-bold text-[#1C4D8D] transition hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
        >
          <ArrowLeft className="size-5" aria-hidden="true" />
          Về danh sách
        </Link>
      ) : (
        <PageBanner
          icon={ClipboardCheck}
          eyebrow="Làm kiểm tra"
          title="Kiểm tra trên lớp"
          description="Nhấn Tham gia trên ca đang mở, nhập mã đang chiếu và kiểm tra micro trước khi vào làm bài."
          extras={
            <>
              <BannerFact icon={KeyRound}>
                Nhập mã OTP trong popup
              </BannerFact>
              <BannerFact icon={Mic2} tone="green">
                Kiểm tra micro trước khi vào
              </BannerFact>
              <BannerFact icon={TimerReset} tone="violet">
                Hết giờ thì bài tự nộp
              </BannerFact>
            </>
          }
        />
      )}

      {routeClassId && (
        <section className={cn(ds.section, 'overflow-hidden')}>
          <div className={cn(ds.sectionHeader, 'space-y-3')}>
            <div className={ds.sectionHeading}>
              <div className={ds.sectionTitleRow}>
                <span className={ds.sectionIconWrap}>
                  <UsersRound className={ds.sectionIcon} aria-hidden="true" />
                </span>
                <h1 className={ds.sectionTitle}>{classDetail?.className || orderedShifts[0]?.className || 'Chi tiết lớp học'}</h1>
              </div>
              {classDetail && (
                <p className={cn(ds.sectionHint, 'flex flex-wrap items-center gap-x-2 gap-y-1')}>
                  <span>{classDetail.courseName || classDetail.classCode}</span>
                  {[classDetail.courseCode, classDetail.semesterCode].filter(Boolean).map((value) => (
                    <span key={value} className="inline-flex items-center gap-2 font-bold text-[#1C4D8D]">
                      <span className="text-[#82aedb]" aria-hidden="true">·</span>
                      {value}
                    </span>
                  ))}
                </p>
              )}
            </div>
            {classDetail ? (
              <div className="flex flex-wrap items-center gap-2">
                <span className={ds.infoChip}>
                  <UserRound className="size-3.5 shrink-0" aria-hidden="true" />
                  <span>Giảng viên · {classDetail.lecturerName || 'Chưa phân công'}</span>
                </span>
                {classDetail.enrolledCount != null && (
                  <span className={ds.infoChip}>
                    <Users className="size-3.5 shrink-0" aria-hidden="true" />
                    {classDetail.enrolledCount}{classDetail.maxStudents != null ? `/${classDetail.maxStudents}` : ''} SV
                  </span>
                )}
              </div>
            ) : (
              <p role="status" className={ds.sectionHint}>
                {classDetails === null ? 'Đang tải thông tin lớp…' : 'Không tìm thấy thông tin lớp.'}
              </p>
            )}
          </div>
        </section>
      )}

      <StatusToast
        notice={notice}
        error={error}
        onDismiss={() => {
          setError('')
          setNotice('')
        }}
      />

      <section className={ds.section}>
        <div className={cn(ds.sectionHeader, 'flex flex-wrap items-center justify-between gap-3')}>
          <div className={ds.sectionHeading}>
            <div className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}>
                <ClipboardCheck className={ds.sectionIcon} aria-hidden="true" />
              </span>
              <h2 className={ds.sectionTitle}>{classId ? 'Danh sách ca' : 'Ca kiểm tra của tôi'}</h2>
            </div>
            <p className={ds.sectionHint}>Chọn ca đang mở để nhập mã và vào làm bài</p>
          </div>
          <button type="button" onClick={() => void load()} disabled={loading} className={ds.outlineButton}>
            <RefreshCw className={cn('size-3.5', loading && 'animate-spin motion-reduce:animate-none')} aria-hidden="true" />
            Làm mới
          </button>
        </div>

        <div className="bg-white">
          <div className="px-4 pt-3 sm:px-6">
            <div
              className="flex flex-wrap items-end gap-0 border-b-2 border-[#1C4D8D]"
              role="tablist"
              aria-label="Lọc trạng thái ca"
            >
              {STATUS_FILTERS.map((tab) => {
                const count = tab.value === 'ALL' ? orderedShifts.length : orderedShifts.filter((shift) => shift.status === tab.value).length
                const active = statusFilter === tab.value
                return (
                  <button
                    key={tab.value}
                    type="button"
                    role="tab"
                    aria-selected={active}
                    onClick={() => setStatusFilter(tab.value)}
                    className={cn(
                      'relative inline-flex items-center gap-1.5 border px-3.5 py-2.5 text-[14px] font-bold whitespace-nowrap transition sm:px-4',
                      active
                        ? '-mb-0.5 rounded-t-xl border-[#1C4D8D] border-b-white bg-[linear-gradient(165deg,#BDE8F5_0%,#EAF6FB_42%,#ffffff_100%)] text-[#1C4D8D]'
                        : 'mb-0 border-transparent text-[#6f7f93] hover:bg-[#F4FBFD] hover:text-[#1C4D8D]',
                    )}
                  >
                    <span
                      className={cn(
                        'size-1.5 rounded-full',
                        active ? 'bg-[#1C4D8D]' : tab.dotClass,
                      )}
                      aria-hidden="true"
                    />
                    <span>{tab.label}</span>
                    <span
                      className={cn(
                        'rounded-full px-1.5 py-0.5 text-[12px] font-bold tabular-nums',
                        active ? 'bg-[#1C4D8D]/12 text-[#1C4D8D]' : tab.countIdleClass,
                      )}
                    >
                      {count}
                    </span>
                  </button>
                )
              })}
            </div>
          </div>
        </div>

        <div className={cn(ds.sectionBody, 'space-y-4 border-t-0 pt-4')}>
          {loading ? (
            <div className="flex items-center gap-2 text-sm font-semibold text-[#5b6b84]">
              <LoaderCircle className="size-4 animate-spin text-[#1C4D8D]" />
              Đang tải ca kiểm tra…
            </div>
          ) : filteredShifts.length === 0 ? (
            <EmptyState
              icon={ClipboardCheck}
              title={orderedShifts.length ? 'Chưa có ca phù hợp' : 'Chưa có ca kiểm tra'}
              description={orderedShifts.length ? "Chọn trạng thái khác để xem danh sách ca." : classId ? "Các ca do giảng viên tạo cho lớp sẽ hiển thị tại đây." : "Tham gia lớp học phần để xem các ca kiểm tra."}
            />
          ) : (
            <ul className="divide-y divide-[#4988C4]/25 overflow-hidden rounded-2xl border border-[#82aedb]/55">
              {filteredShifts.map((shift) => {
                const alreadyDone = participationDone(shift.myParticipationStatus)
                const myStatusKey = (shift.myParticipationStatus ?? '').toUpperCase() as keyof typeof MY_PARTICIPATION_STATUS
                const myStatus = MY_PARTICIPATION_STATUS[myStatusKey]
                const status = alreadyDone && shift.status === 'FINALIZED' ? SHIFT_STATUS.FINALIZED : myStatus ?? SHIFT_STATUS[shift.status]
                const inProgress = myStatusKey === 'JOINED' || myStatusKey === 'IN_PROGRESS'
                const factClass = 'inline-flex items-center gap-1.5 rounded-lg bg-[#D1EFF8] px-2.5 py-1.5 text-xs font-semibold text-[#1C4D8D]'
                return (
                  <li key={shift.id} className="flex flex-col gap-3 bg-white px-4 py-4 transition hover:bg-[#F8FBFD] sm:flex-row sm:items-center sm:justify-between sm:px-5">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className={cn('inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 text-[11px] font-bold', status.chip)}>
                          <TimerReset className="size-3 shrink-0" aria-hidden="true" />
                          {status.label}
                        </span>
                        {!classId && <span className={ds.infoChip}>{shift.className}</span>}
                      </div>
                      <h3 className="mt-2 text-xl font-bold tracking-[-0.02em] text-[#0F2854]">{shift.title}</h3>
                      <div className="mt-2 flex flex-wrap items-center gap-1.5">
                        <span className={factClass}><Clock3 className="size-3.5 shrink-0" aria-hidden="true" />{formatExamWindow(shift)}</span>
                        <span className={factClass}>Làm bài {examDurationMinutesOf(shift)} phút</span>
                        <span className={factClass}>{questionLabel(shift)}</span>
                      </div>
                    </div>
                    <div className="flex shrink-0 flex-wrap items-center gap-2">
                    {alreadyDone ? (
                      <div className="flex flex-wrap gap-2">
                        {shift.status === 'FINALIZED' ? (
                          <button type="button" onClick={() => void openResults(shift)} className={ds.outlineButton}>
                            <Lock className="size-3.5" />
                            Xem kết quả
                          </button>
                        ) : (
                          <div className="inline-flex min-h-10 w-full items-center justify-center gap-2 rounded-xl border border-amber-200 bg-amber-50 px-4 text-sm font-bold text-amber-800">
                            <Clock3 className="size-3.5 shrink-0" />
                            Chưa có điểm
                          </div>
                        )}
                      </div>
                    ) : shift.status === 'ACTIVE' ? (
                      <div>
                        <button
                          type="button"
                          onClick={() => openJoin(shift, inProgress ? 'resume' : 'enter')}
                          className={ds.outlineButton}
                        >
                          <KeyRound className="size-3.5" />
                          {inProgress ? 'Tiếp tục làm bài' : 'Tham gia'}
                        </button>
                      </div>
                    ) : shift.status === 'FINALIZED' ? (
                      <p className="text-xs font-semibold text-[#8b97a8]">
                        Bạn không tham gia ca kiểm tra này
                      </p>
                    ) : (
                      <p className="text-xs font-semibold text-[#8b97a8]">
                        {shift.status === 'SCHEDULED' ? 'Ca chưa mở' : 'Ca đã kết thúc'}
                      </p>
                    )}
                    </div>
                  </li>
                )
              })}
            </ul>
          )}
        </div>
      </section>

      {joinTarget && (
        <FormDialog
          titleId="join-inclass-title"
          eyebrow="Chi tiết lớp học"
          title={joinMode === 'resume' ? 'Tiếp tục làm bài' : 'Vào ca kiểm tra'}
          icon={KeyRound}
          onClose={closeJoin}
          busy={entering}
          size="md"
          align="center"
          footer={(
            <DialogFooter className="bg-white">
              <button type="button" disabled={entering} onClick={closeJoin} className={ds.outlineButton}>
                Hủy
              </button>
              <button
                type="button"
                disabled={entering}
                onClick={() => void enter()}
                className={ds.primaryButton}
              >
                {entering ? <LoaderCircle className="size-4 animate-spin" /> : <ClipboardCheck className="size-4" />}
                {entering
                  ? joinMode === 'resume'
                    ? 'Đang vào lại...'
                    : 'Đang vào...'
                  : micState === 'ready'
                    ? joinMode === 'resume'
                      ? 'Làm tiếp'
                      : 'Vào làm bài'
                    : 'Kiểm tra micro để tiếp tục'}
              </button>
            </DialogFooter>
          )}
        >
          <div className="space-y-4">
            {joinMode === 'enter' && (
            <div
              className={cn(
                'rounded-2xl border px-4 py-4',
                otpError
                  ? 'border-[#c2415b] bg-[#fff7f8]'
                  : otpComplete
                  ? 'border-[#b7e4cf] bg-[linear-gradient(180deg,#f3fbf7_0%,#e8f8f1_100%)]'
                  : 'border-[#82aedb]/55 bg-[linear-gradient(180deg,#f7fbfe_0%,#eef5fb_100%)]',
              )}
            >
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="text-[11px] font-bold tracking-[0.12em] text-[#1C4D8D] uppercase">
                    Mã vào ca · {OTP_LENGTH} số
                  </p>
                  <p className="mt-1 text-sm font-semibold text-[#5b6b84]">
                    Nhập đúng mã đang chiếu trên màn hình giảng viên.
                  </p>
                </div>
                <span
                  className={cn(
                    'inline-flex shrink-0 items-center gap-1.5 rounded-full border px-2.5 py-1 text-[11px] font-bold',
                    otpComplete
                      ? 'border-[#b7e4cf] bg-white text-[#16836a]'
                      : 'border-[#82aedb]/55 bg-white text-[#1C4D8D]',
                  )}
                >
                  <KeyRound className="size-3" aria-hidden="true" />
                  {otp.length}/{OTP_LENGTH}
                </span>
              </div>

              <div
                className="mt-4 flex items-center justify-center gap-2 sm:gap-2.5"
                role="group"
                aria-label={`Nhập mã OTP ${OTP_LENGTH} số`}
              >
                {otpDigits.map((digit, index) => (
                  <input
                    key={`otp-${index}`}
                    ref={(node) => {
                      otpInputRefs.current[index] = node
                    }}
                    value={digit}
                    onChange={(event) => handleOtpDigitChange(index, event.target.value)}
                    onKeyDown={(event) => handleOtpKeyDown(index, event)}
                    onPaste={handleOtpPaste}
                    onFocus={(event) => event.currentTarget.select()}
                    inputMode="numeric"
                    autoComplete={index === 0 ? 'one-time-code' : 'off'}
                    aria-label={`Chữ số ${index + 1}`}
                    aria-invalid={otpError ? true : undefined}
                    maxLength={1}
                    disabled={entering}
                    className={cn(
                      'size-11 rounded-xl border bg-white text-center font-mono text-xl font-black tracking-wide text-[#0F2854] outline-none transition sm:size-12 sm:text-2xl',
                      'shadow-[0_1px_0_#fff_inset,0_4px_10px_rgba(15,40,84,0.06)]',
                      'focus:border-[#1C4D8D] focus:ring-2 focus:ring-[#4988C4]/25',
                      digit
                        ? 'border-[#4988C4] bg-[#f4f9fd]'
                        : 'border-[#82aedb]/70',
                      otpError && fieldInvalidClass,
                      entering && 'opacity-60',
                    )}
                  />
                ))}
              </div>
              <FieldError message={otpError} className="text-center" />
            </div>
            )}

            <div
              className={cn(
                'rounded-2xl border p-3.5',
                micState === 'ready'
                  ? 'border-[#b7e4cf] bg-[#e8f8f1]'
                  : micError
                    ? 'border-[#c2415b] bg-[#fff7f8]'
                    : micState === 'error'
                    ? 'border-[#f3c2c8] bg-[#fdecec]'
                    : 'border-[#82aedb]/55 bg-[#F4FBFD]',
              )}
            >
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <div className="min-w-0">
                  <p className={cn('flex items-center gap-2 text-sm font-bold', micStatus.color)}>
                    <span className={cn('size-2 shrink-0 rounded-full', micStatus.dot)} />
                    {micState === 'error' && micIssue === 'silent'
                      ? 'Chưa nghe thấy giọng nói'
                      : micStatus.title}
                  </p>
                  <p className="mt-1 text-xs leading-relaxed text-[#5b6b84]">
                    {micState === 'error' && micIssue === 'silent'
                      ? 'Hãy nói gần micro một câu ngắn rồi thử lại.'
                      : micState === 'ready' && micName
                        ? `${micName}. ${micStatus.helper}`
                        : micStatus.helper}
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => void handleMicCheck()}
                  disabled={entering || micState === 'checking' || micState === 'testing'}
                  className="inline-flex min-h-10 shrink-0 items-center justify-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#0F2854] shadow-[0_3px_8px_rgba(23,48,96,0.05)] hover:-translate-y-0.5 hover:border-[#4988C4] disabled:cursor-wait disabled:opacity-60 motion-reduce:transform-none"
                >
                  {micState === 'checking' || micState === 'testing' ? (
                    <LoaderCircle className="size-3.5 animate-spin" />
                  ) : (
                    <Mic2 className="size-3.5" />
                  )}
                  {micState === 'testing'
                    ? 'Đang nghe…'
                    : micState === 'ready'
                      ? 'Kiểm tra lại'
                      : 'Kiểm tra micro'}
                </button>
              </div>
              {micState === 'testing' && (
                <div className="mt-3 flex h-8 items-end gap-1" aria-hidden="true">
                  {Array.from({ length: 16 }, (_, index) => (
                    <span
                      key={index}
                      className={cn(
                        'w-1.5 flex-1 rounded-full transition-colors',
                        micLevel > index / 16 ? 'bg-[#1C4D8D]' : 'bg-[#e4ebf5]',
                      )}
                      style={{ height: `${10 + index}px` }}
                    />
                  ))}
                </div>
              )}
              <FieldError message={micError} />
            </div>
          </div>
        </FormDialog>
      )}

      {resultsShift && (
        <FormDialog
          titleId="in-class-results-title"
          eyebrow="Chi tiết lớp học"
          title={resultsShift.title}
          icon={ClipboardCheck}
          onClose={() => setResultsShift(null)}
          size="md"
          align="center"
          maxHeight="max-h-[min(40rem,92dvh)]"
        >
          {resultsLoading ? (
            <p className="flex items-center gap-2 text-sm text-[#5b6b84]"><LoaderCircle className="size-4 animate-spin text-[#1C4D8D]" /> Đang tải kết quả...</p>
          ) : results.length === 0 ? (
            <EmptyState compact icon={ClipboardCheck} title="Chưa có bài nộp trong ca này" />
          ) : (
            <div className="space-y-3">
              <div className="rounded-2xl border border-[#82aedb]/55 bg-white px-4 py-3">
                <p className="text-[11px] font-bold tracking-[0.06em] text-[#5b6b84] uppercase">Điểm cả bài</p>
                <p className="mt-1 font-mono text-2xl font-black text-[#0F2854]">{examScoreLabel}</p>
                {examLecturerNote ? (
                  <p className="mt-2 text-sm leading-relaxed text-[#1C4D8D]">Giảng viên: {examLecturerNote}</p>
                ) : null}
              </div>
              <ul className="space-y-3">
                {results.map((item, index) => (
                  <li key={item.submissionId} className="rounded-2xl border border-[#82aedb]/55 bg-white p-3.5">
                    <p className="text-[11px] font-bold tracking-[0.04em] text-[#1C4D8D] uppercase">Câu {index + 1}</p>
                    <h3 className="mt-1 text-sm font-bold text-[#0F2854]">{item.title}</h3>
                    {item.transcript && <p className="mt-2 text-sm leading-relaxed text-[#33445f]">{item.transcript}</p>}
                    {item.aiFeedback && <p className="mt-2 text-sm leading-relaxed text-[#5b6b84]">AI: {item.aiFeedback}</p>}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </FormDialog>
      )}
    </div>
  )
}
