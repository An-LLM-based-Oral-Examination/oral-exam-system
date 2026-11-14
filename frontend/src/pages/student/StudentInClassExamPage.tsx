import { useCallback, useEffect, useRef, useState, type ClipboardEvent, type KeyboardEvent } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { CheckCircle2, ClipboardCheck, KeyRound, LoaderCircle, Lock, Mic2, TimerReset, UsersRound } from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass } from '@/components/common/formFieldClasses'
import { StatusToast } from '@/components/common/toast'
import { toast } from '@/components/common/toastStore'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { studentCardShellClass } from '@/design/studentCardTones'
import { BannerFact, PageBanner, StudentColorCardAccent } from '@/design/ui'
import { ds } from '@/design/system'
import { scheduleUi } from '@/lib/scheduleUi'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import { inClassShiftService } from '@/services/inClassShift.service'
import { createInClassShiftHubConnection } from '@/services/signalr.service'
import type { EnterInClassExamResult, InClassShift, StudentExamResult } from '@/types/inClassShift.types'
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

const COURSE_CODE_TONES = [
  'border border-[#4988C4]/35 bg-[#4988C4]/16 text-[#1C4D8D]',
  'border border-[#16836a]/30 bg-[#e8f8f3] text-[#16836a]',
  'border border-[#c26a12]/30 bg-[#fff4e8] text-[#c26a12]',
  'border border-[#6d4cc4]/30 bg-[#f3eefe] text-[#6d4cc4]',
  'border border-[#0e7490]/30 bg-[#e7f6fb] text-[#0e7490]',
  'border border-[#c2415b]/30 bg-[#fdecef] text-[#c2415b]',
  'border border-[#3d7a2a]/30 bg-[#eef8e8] text-[#3d7a2a]',
  'border border-[#be185d]/30 bg-[#fff0f6] text-[#be185d]',
]

const SHIFT_STATUS = {
  SCHEDULED: { label: 'Chờ kích hoạt', chip: 'border-[#f3e0bf] bg-[#fff6e8] text-[#c26a12]' },
  ACTIVE: { label: 'Đang diễn ra', chip: 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]' },
  FINALIZED: { label: 'Đã chốt điểm', chip: 'border-[#82aedb]/55 bg-[#F4FBFD] text-[#1C4D8D]' },
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

function codeTone(index: number) {
  return COURSE_CODE_TONES[index % COURSE_CODE_TONES.length]
}

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

export function StudentInClassExamPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const [shifts, setShifts] = useState<InClassShift[]>([])
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

  const orderedShifts = [...shifts].sort((left, right) => {
    const rank = { ACTIVE: 0, SCHEDULED: 1, FINALIZED: 2, CLOSED: 3 }
    return rank[left.status] - rank[right.status]
  })
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
    <div lang="vi" className={ds.page}>
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

      <StatusToast
        notice={notice}
        error={error}
        onDismiss={() => {
          setError('')
          setNotice('')
        }}
      />

      <section className={ds.section}>
        <div className={ds.sectionHeader}>
          <div className={ds.sectionHeading}>
            <div className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}>
                <ClipboardCheck className={ds.sectionIcon} aria-hidden="true" />
              </span>
              <h2 className={ds.sectionTitle}>Ca kiểm tra của tôi</h2>
            </div>
            <p className={ds.sectionHint}>Chọn ca đang mở để nhập mã và vào làm bài</p>
          </div>
        </div>

        <div className={cn(ds.sectionBody, 'space-y-4')}>
          {loading ? (
            <div className="flex items-center gap-2 text-sm font-semibold text-[#5b6b84]">
              <LoaderCircle className="size-4 animate-spin text-[#1C4D8D]" />
              Đang tải ca kiểm tra…
            </div>
          ) : orderedShifts.length === 0 ? (
            <EmptyState
              icon={ClipboardCheck}
              title="Chưa có ca kiểm tra"
              description="Tham gia lớp học phần trước. Khi giảng viên mở ca, nhấn Tham gia để nhập mã và kiểm tra micro."
            />
          ) : (
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {orderedShifts.map((shift, index) => {
                const alreadyDone = participationDone(shift.myParticipationStatus)
                const myStatusKey = (shift.myParticipationStatus ?? '').toUpperCase() as keyof typeof MY_PARTICIPATION_STATUS
                const myStatus = MY_PARTICIPATION_STATUS[myStatusKey]
                const status = myStatus ?? SHIFT_STATUS[shift.status]
                const inProgress = myStatusKey === 'JOINED' || myStatusKey === 'IN_PROGRESS'
                const inactive = shift.status === 'CLOSED' || shift.status === 'SCHEDULED' || alreadyDone
                const meta = [
                  `Làm bài ${examDurationMinutesOf(shift)} phút`,
                  questionLabel(shift),
                  `Ca ${formatExamWindow(shift)}`,
                ].join(' · ')
                return (
                  <article
                    key={shift.id}
                    className={cn(
                      inactive
                        ? 'relative flex h-full flex-col overflow-hidden rounded-[22px] border border-[#82aedb]/55 bg-[#f7f8fa] p-3.5 shadow-[0_1px_0_#fff_inset,0_8px_16px_rgba(15,40,84,0.12)]'
                        : studentCardShellClass(shift.courseCode || index),
                    )}
                  >
                    {!inactive && <StudentColorCardAccent seed={shift.courseCode || index} />}
                    <div className={inactive ? 'flex h-full flex-1 flex-col' : 'relative flex h-full flex-1 flex-col p-3.5'}>
                    <div className="flex items-center justify-between gap-2">
                      <div className="flex min-w-0 flex-wrap items-center gap-1.5">
                        <span
                          className={cn(
                            'inline-flex h-6 items-center rounded-md px-2 text-xs font-bold tracking-[0.04em]',
                            inactive
                              ? 'border border-[#8b97a8]/30 bg-[#e8edf3] text-[#8b97a8]'
                              : codeTone(index),
                          )}
                        >
                          {shift.courseCode || 'LỚP'}
                        </span>
                        <span className="inline-flex h-6 max-w-[10.5rem] items-center gap-1 truncate rounded-full border border-[#4988C4]/55 bg-white/85 px-2 text-xs font-semibold text-[#5b6b84] backdrop-blur-[2px]">
                          <UsersRound className="size-3 shrink-0 text-[#1C4D8D]" aria-hidden="true" />
                          <span className="truncate">{shift.className}</span>
                        </span>
                      </div>
                      <span
                        className={cn(
                          'inline-flex h-6 shrink-0 items-center gap-1 rounded-full border px-2 text-xs font-semibold',
                          status.chip,
                        )}
                      >
                        <TimerReset className="size-3 shrink-0" aria-hidden="true" />
                        {status.label}
                      </span>
                    </div>

                    <h3
                      className={cn(
                        'mt-3 line-clamp-2 text-xl leading-snug font-bold',
                        inactive ? 'text-[#6d7888]' : 'text-[#0F2854]',
                      )}
                    >
                      {shift.title}
                    </h3>
                    <p className={cn('mt-1 line-clamp-2 text-xs font-semibold', inactive ? 'text-[#8b97a8]' : 'text-[#5b6b84]')}>
                      {meta}
                    </p>

                    {alreadyDone ? (
                      <div className="mt-auto flex flex-col gap-2 pt-3">
                        {shift.isAutoPublishGrades || shift.status === 'FINALIZED' ? (
                          <button type="button" onClick={() => void openResults(shift)} className={ds.outlineButton}>
                            <Lock className="size-3.5" />
                            Xem kết quả
                          </button>
                        ) : (
                          <div className="inline-flex min-h-10 w-full items-center justify-center gap-2 rounded-xl border border-[#b7e4cf] bg-[#e8f8f1] px-4 text-sm font-bold text-[#16836a]">
                            <CheckCircle2 className="size-3.5 shrink-0" />
                            Đã nộp bài · Chờ giảng viên chốt điểm
                          </div>
                        )}
                      </div>
                    ) : shift.status === 'ACTIVE' ? (
                      <div className="mt-auto pt-3">
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
                      <p className="mt-auto pt-3 text-xs font-semibold text-[#8b97a8]">
                        Bạn không tham gia ca kiểm tra này
                      </p>
                    ) : (
                      <p className="mt-auto pt-3 text-xs font-semibold text-[#8b97a8]">
                        {shift.status === 'SCHEDULED' ? 'Chờ giảng viên kích hoạt ca' : 'Ca đã kết thúc'}
                      </p>
                    )}
                    </div>
                  </article>
                )
              })}
            </div>
          )}
        </div>
      </section>

      {joinTarget && (
        <FormDialog
          titleId="join-inclass-title"
          eyebrow={`${joinTarget.courseCode || 'Ca kiểm tra'} · ${joinTarget.className}`}
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
          eyebrow="Kết quả kiểm tra"
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
