import { lazy, Suspense, useEffect, useMemo, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import QRCode from 'qrcode'
import {
  AlertTriangle,
  BadgeCheck,
  BookOpenCheck,
  Building2,
  CalendarDays,
  CalendarPlus,
  Check,
  CheckCircle2,
  ChevronDown,
  Clipboard,
  Clock3,
  Copy,
  DoorOpen,
  Headphones,
  Info,
  LoaderCircle,
  MapPinned,
  Mic2,
  Monitor,
  Printer,
  QrCode,
  RefreshCw,
  ScanLine,
  ShieldCheck,
  Sparkles,
  UserRound,
  Wifi,
  XCircle,
  type LucideIcon,
} from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'
import { cn } from '@/lib/utils'
import type { ExamSceneState } from './OfficialExamThreeScene'

const OfficialExamThreeScene = lazy(() =>
  import('./OfficialExamThreeScene').then((module) => ({
    default: module.OfficialExamThreeScene,
  })),
)

type AssignmentStatus = 'CHECK_IN_OPEN' | 'SCHEDULED'
type ExamPhase = 'check-in' | 'scheduled' | 'in-progress' | 'closed'
type MicState = 'idle' | 'checking' | 'ready' | 'error' | 'unsupported'

interface OfficialExamAssignment {
  id: string
  academicTerm: string
  subjectCode: string
  subjectName: string
  track: string
  startAt: Date
  durationMinutes: number
  questionCount: number
  room: string
  building: string
  kioskNumber: number
  workstationId: string
  seatZone: string
  sessionCode: string
  candidateNumber: string
  status: AssignmentStatus
  checkInCode?: string
  qrPayload?: string
  tokenExpiresAt?: Date
}

interface DetailItem {
  label: string
  value: string
  helper: string
  icon: LucideIcon
  accentClassName: string
}

function addMinutes(date: Date, minutes: number) {
  return new Date(date.getTime() + minutes * 60_000)
}

function addDays(date: Date, days: number) {
  return new Date(date.getTime() + days * 86_400_000)
}

function buildPreviewAssignments(now: Date): OfficialExamAssignment[] {
  const firstStart = addMinutes(now, 18)
  const secondStart = addDays(now, 5)
  secondStart.setHours(13, 30, 0, 0)

  return [
    {
      id: 'assignment-int3201',
      academicTerm: 'Học kỳ Fall 2026',
      subjectCode: 'INT3201',
      subjectName: 'Mạng Máy tính & Truyền thông',
      track: 'Học phần chuyên ngành',
      startAt: firstStart,
      durationMinutes: 15,
      questionCount: 3,
      room: 'Lab B402',
      building: 'Tòa nhà Công nghệ',
      kioskNumber: 12,
      workstationId: 'LAB-B402-K12',
      seatZone: 'Dãy B · Ghế 04',
      sessionCode: 'ES-F26-INT3201-04',
      candidateNumber: 'SBD-2026-0456',
      status: 'CHECK_IN_OPEN',
      checkInCode: '749 203',
      qrPayload:
        'oralexam://kiosk-check-in?assignment=assignment-int3201&session=ES-F26-INT3201-04&kiosk=LAB-B402-K12&token=preview-signed-token',
      tokenExpiresAt: addMinutes(now, 15),
    },
    {
      id: 'assignment-swe3003',
      academicTerm: 'Học kỳ Fall 2026',
      subjectCode: 'SWE3003',
      subjectName: 'Kiến trúc & Thiết kế Phần mềm',
      track: 'Học phần chuyên ngành',
      startAt: secondStart,
      durationMinutes: 18,
      questionCount: 4,
      room: 'Lab A305',
      building: 'Tòa nhà Alpha',
      kioskNumber: 8,
      workstationId: 'LAB-A305-K08',
      seatZone: 'Dãy A · Ghế 08',
      sessionCode: 'ES-F26-SWE3003-02',
      candidateNumber: 'SBD-2026-0456',
      status: 'SCHEDULED',
    },
  ]
}

function formatDate(date: Date) {
  return new Intl.DateTimeFormat('vi-VN', {
    weekday: 'long',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(date)
}

function formatShortDate(date: Date) {
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
  }).format(date)
}

function formatTime(date: Date) {
  return new Intl.DateTimeFormat('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(date)
}

function formatCountdown(milliseconds: number) {
  const totalSeconds = Math.max(0, Math.floor(milliseconds / 1000))
  const days = Math.floor(totalSeconds / 86_400)
  const hours = Math.floor((totalSeconds % 86_400) / 3_600)
  const minutes = Math.floor((totalSeconds % 3_600) / 60)
  const seconds = totalSeconds % 60

  if (days > 0) return `${days} ngày ${hours} giờ ${minutes} phút`
  if (hours > 0) return `${hours} giờ ${minutes} phút ${seconds} giây`
  return `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`
}

function getExamPhase(assignment: OfficialExamAssignment, now: Date): ExamPhase {
  const endAt = addMinutes(assignment.startAt, assignment.durationMinutes)
  if (now >= endAt) return 'closed'
  if (now >= assignment.startAt) return 'in-progress'
  if (assignment.status === 'CHECK_IN_OPEN') return 'check-in'
  return 'scheduled'
}

function getInitials(fullName: string) {
  return fullName
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(-2)
    .map((word) => word.charAt(0).toLocaleUpperCase('vi-VN'))
    .join('')
}

function escapeCalendarText(value: string) {
  return value
    .replace(/\\/g, '\\\\')
    .replace(/,/g, '\\,')
    .replace(/;/g, '\\;')
    .replace(/\n/g, '\\n')
}

function toIcsDate(date: Date) {
  return date
    .toISOString()
    .replace(/[-:]/g, '')
    .replace(/\.\d{3}/, '')
}

export function OfficialExamPage() {
  const { user } = useAuth()
  const [searchParams] = useSearchParams()
  const assignments = useMemo(() => buildPreviewAssignments(new Date()), [])
  const [selectedId, setSelectedId] = useState(assignments[0].id)
  const [now, setNow] = useState(() => new Date())
  const [rulesOpen, setRulesOpen] = useState(true)
  const [roomMapOpen, setRoomMapOpen] = useState(false)
  const [devicePanelOpen, setDevicePanelOpen] = useState(
    () => searchParams.get('device-check') === '1',
  )
  const [confirmedAtKiosk, setConfirmedAtKiosk] = useState(false)
  const [micState, setMicState] = useState<MicState>('idle')
  const [micDeviceLabel, setMicDeviceLabel] = useState('')
  const [copyState, setCopyState] = useState<'idle' | 'copied' | 'error'>('idle')
  const [qrDataUrl, setQrDataUrl] = useState('')
  const [qrError, setQrError] = useState(false)
  const devicePanelRef = useRef<HTMLDivElement>(null)
  const copyResetTimerRef = useRef<number | null>(null)

  const selectedExam =
    assignments.find((assignment) => assignment.id === selectedId) ?? assignments[0]
  const phase = getExamPhase(selectedExam, now)
  const tokenTimeLeft = selectedExam.tokenExpiresAt
    ? selectedExam.tokenExpiresAt.getTime() - now.getTime()
    : 0
  const canShowCheckIn =
    phase === 'check-in' &&
    Boolean(selectedExam.qrPayload && selectedExam.checkInCode) &&
    tokenTimeLeft > 0
  const studentName = user?.fullName ?? 'Nguyễn Văn An'
  const studentCode = user?.studentCode ?? 'SE172428'
  const sceneState: ExamSceneState =
    micState === 'checking' ? 'checking' : micState === 'ready' ? 'ready' : 'waiting'

  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 1000)
    return () => {
      window.clearInterval(timer)
      if (copyResetTimerRef.current !== null) {
        window.clearTimeout(copyResetTimerRef.current)
      }
    }
  }, [])

  useEffect(() => {
    if (searchParams.get('device-check') !== '1') return
    const scrollTimer = window.setTimeout(() => {
      devicePanelRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' })
    }, 180)
    return () => window.clearTimeout(scrollTimer)
  }, [searchParams])

  useEffect(() => {
    let cancelled = false

    if (!canShowCheckIn || !selectedExam.qrPayload) return

    QRCode.toDataURL(selectedExam.qrPayload, {
      width: 320,
      margin: 2,
      errorCorrectionLevel: 'M',
      color: {
        dark: '#07172eff',
        light: '#ffffffff',
      },
    })
      .then((dataUrl) => {
        if (!cancelled) {
          setQrDataUrl(dataUrl)
          setQrError(false)
        }
      })
      .catch(() => {
        if (!cancelled) setQrError(true)
      })

    return () => {
      cancelled = true
    }
  }, [canShowCheckIn, selectedExam.qrPayload])

  const phaseConfig = {
    'check-in': {
      label: 'Giảng viên đã mở check-in',
      helper: `Còn ${formatCountdown(selectedExam.startAt.getTime() - now.getTime())} tới giờ thi`,
      className: 'border-[#bcebdc] bg-[#ecfbf5] text-[#16785f]',
      dotClassName: 'bg-[#35c897]',
    },
    scheduled: {
      label: 'Đã công bố · Chờ mở ca',
      helper: `Bắt đầu sau ${formatCountdown(selectedExam.startAt.getTime() - now.getTime())}`,
      className: 'border-[#cfdaf8] bg-[#f1f5ff] text-[#3659b7]',
      dotClassName: 'bg-[#5477db]',
    },
    'in-progress': {
      label: 'Ca thi đang diễn ra',
      helper: 'Chỉ Kiosk đã xác thực mới được truy cập phiên thi',
      className: 'border-[#c9e5f8] bg-[#edf8ff] text-[#24749d]',
      dotClassName: 'bg-[#42b9df]',
    },
    closed: {
      label: 'Ca thi đã kết thúc',
      helper: 'Bài làm đang chờ xử lý và hậu kiểm',
      className: 'border-[#dce2ea] bg-[#f4f6f9] text-[#627186]',
      dotClassName: 'bg-[#8c99aa]',
    },
  }[phase]

  const details: DetailItem[] = [
    {
      label: 'Ngày & giờ thi',
      value: formatTime(selectedExam.startAt),
      helper: formatDate(selectedExam.startAt),
      icon: CalendarDays,
      accentClassName: 'bg-[#eef3ff] text-[#3568df]',
    },
    {
      label: 'Phòng thi (Lab)',
      value: selectedExam.room,
      helper: selectedExam.building,
      icon: MapPinned,
      accentClassName: 'bg-[#f0f7ff] text-[#2671cf]',
    },
    {
      label: 'Vị trí Kiosk / Bàn',
      value: `Kiosk số ${selectedExam.kioskNumber}`,
      helper: `${selectedExam.workstationId} · ${selectedExam.seatZone}`,
      icon: Monitor,
      accentClassName: 'bg-[#eef0ff] text-[#5d62dd]',
    },
    {
      label: 'Thời lượng vấn đáp',
      value: `${selectedExam.durationMinutes} phút`,
      helper: `${selectedExam.questionCount} câu hỏi chuyên sâu`,
      icon: Clock3,
      accentClassName: 'bg-[#ecfaf7] text-[#168d76]',
    },
  ]

  const handleMicCheck = async () => {
    if (!confirmedAtKiosk || micState === 'checking') return

    const AudioContextConstructor =
      window.AudioContext ??
      (window as typeof window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext

    if (!navigator.mediaDevices?.getUserMedia || !AudioContextConstructor) {
      setMicState('unsupported')
      return
    }

    setMicState('checking')
    setMicDeviceLabel('')
    let stream: MediaStream | null = null
    let audioContext: AudioContext | null = null
    let sourceNode: MediaStreamAudioSourceNode | null = null

    try {
      stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true,
        },
      })
      const audioTrack = stream.getAudioTracks()[0]
      if (!audioTrack || audioTrack.readyState !== 'live') {
        throw new Error('No active audio track')
      }

      audioContext = new AudioContextConstructor()
      if (audioContext.state === 'suspended') await audioContext.resume()
      const analyser = audioContext.createAnalyser()
      analyser.fftSize = 2048
      analyser.smoothingTimeConstant = 0.25
      sourceNode = audioContext.createMediaStreamSource(stream)
      sourceNode.connect(analyser)

      const samples = new Uint8Array(analyser.fftSize)
      const signalDeadline = performance.now() + 2200
      let peakRms = 0

      while (performance.now() < signalDeadline && peakRms < 0.012) {
        analyser.getByteTimeDomainData(samples)
        let squareSum = 0
        for (const sample of samples) {
          const normalized = (sample - 128) / 128
          squareSum += normalized * normalized
        }
        peakRms = Math.max(peakRms, Math.sqrt(squareSum / samples.length))
        if (peakRms < 0.012) {
          await new Promise((resolve) => window.setTimeout(resolve, 90))
        }
      }

      if (audioTrack.muted || peakRms < 0.008) {
        throw new Error('No microphone signal detected')
      }

      setMicDeviceLabel(audioTrack.label || 'Microphone mặc định của Kiosk')
      setMicState('ready')
    } catch {
      setMicState('error')
    } finally {
      sourceNode?.disconnect()
      if (audioContext) {
        await audioContext.close().catch(() => undefined)
      }
      stream?.getTracks().forEach((track) => track.stop())
    }
  }

  const handleSelectExam = (assignmentId: string) => {
    setSelectedId(assignmentId)
    setMicState('idle')
    setMicDeviceLabel('')
    setConfirmedAtKiosk(false)
    setCopyState('idle')
    setQrDataUrl('')
    setQrError(false)
  }

  const handleCopyCode = async () => {
    if (!canShowCheckIn || !selectedExam.checkInCode) return
    try {
      await navigator.clipboard.writeText(selectedExam.checkInCode.replace(/\s/g, ''))
      setCopyState('copied')
      if (copyResetTimerRef.current !== null) window.clearTimeout(copyResetTimerRef.current)
      copyResetTimerRef.current = window.setTimeout(() => {
        setCopyState('idle')
        copyResetTimerRef.current = null
      }, 2200)
    } catch {
      setCopyState('error')
    }
  }

  const handleAddToCalendar = () => {
    const endAt = addMinutes(selectedExam.startAt, selectedExam.durationMinutes)
    const calendar = [
      'BEGIN:VCALENDAR',
      'VERSION:2.0',
      'PRODID:-//OralExam AI//Official Exam//VI',
      'BEGIN:VEVENT',
      `UID:${selectedExam.id}@oralexam.local`,
      `DTSTAMP:${toIcsDate(new Date())}`,
      `DTSTART:${toIcsDate(selectedExam.startAt)}`,
      `DTEND:${toIcsDate(endAt)}`,
      `SUMMARY:${escapeCalendarText(`Thi vấn đáp ${selectedExam.subjectCode}`)}`,
      `DESCRIPTION:${escapeCalendarText(`Có mặt trước 15 phút. Kiosk ${selectedExam.kioskNumber}, ${selectedExam.room}.`)}`,
      `LOCATION:${escapeCalendarText(`${selectedExam.room}, ${selectedExam.building}`)}`,
      'BEGIN:VALARM',
      'TRIGGER:-PT30M',
      'ACTION:DISPLAY',
      'DESCRIPTION:Nhắc lịch thi vấn đáp',
      'END:VALARM',
      'END:VEVENT',
      'END:VCALENDAR',
    ].join('\r\n')
    const url = URL.createObjectURL(new Blob([calendar], { type: 'text/calendar;charset=utf-8' }))
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = `lich-thi-${selectedExam.subjectCode.toLowerCase()}.ics`
    document.body.appendChild(anchor)
    anchor.click()
    anchor.remove()
    window.setTimeout(() => URL.revokeObjectURL(url), 0)
  }

  const micStatus = {
    idle: {
      icon: Mic2,
      title: 'Chưa kiểm tra micro',
      helper: 'Xác nhận đúng Kiosk trước khi cấp quyền truy cập micro.',
      className: 'border-[#dce5f0] bg-white text-[#5d6c80]',
    },
    checking: {
      icon: LoaderCircle,
      title: 'Đang kiểm tra tín hiệu...',
      helper: 'Giữ yên kết nối và nói thử một câu ngắn.',
      className: 'border-[#cfdaf8] bg-[#f4f7ff] text-[#4663b6]',
    },
    ready: {
      icon: CheckCircle2,
      title: 'Mic Health · Đã nhận tín hiệu',
      helper: micDeviceLabel || 'Microphone đã thu được tín hiệu giọng nói.',
      className: 'border-[#bcebdc] bg-[#effbf6] text-[#16785f]',
    },
    error: {
      icon: XCircle,
      title: 'Mic Bad · Không nhận được tín hiệu',
      helper: 'Không bỏ qua lỗi. Hãy báo giám thị để được đổi sang máy dự phòng.',
      className: 'border-[#f3c8cf] bg-[#fff5f6] text-[#b33d52]',
    },
    unsupported: {
      icon: AlertTriangle,
      title: 'Trình duyệt chưa hỗ trợ kiểm tra',
      helper: 'Hãy dùng trình duyệt Kiosk hoặc liên hệ giám thị phòng thi.',
      className: 'border-[#f3dfb8] bg-[#fff9eb] text-[#9a6c16]',
    },
  }[micState]
  const MicStatusIcon = micStatus.icon

  return (
    <div className="space-y-4 pb-5 sm:space-y-5">
      <style>{`
        @media print {
          @page { size: A4; margin: 14mm; }
          body * { visibility: hidden !important; }
          #official-exam-print-ticket,
          #official-exam-print-ticket * { visibility: visible !important; }
          #official-exam-print-ticket {
            display: block !important;
            position: fixed;
            inset: 0;
            width: 100%;
            padding: 24px;
            background: white;
            color: #10203d;
          }
        }
      `}</style>

      <section id="official-exam-print-ticket" className="hidden" aria-hidden="true">
        <div className="rounded-2xl border-2 border-[#173f9c] p-8">
          <div className="flex items-start justify-between gap-8 border-b border-[#dce4ef] pb-5">
            <div>
              <p className="text-sm font-bold tracking-[0.12em] text-[#315eca] uppercase">
                OralExam AI · University Tech
              </p>
              <h1 className="mt-2 text-3xl font-bold">Phiếu dự thi vấn đáp chính thức</h1>
              <p className="mt-2 text-sm text-[#64748b]">
                {selectedExam.academicTerm} · Mã ca {selectedExam.sessionCode}
              </p>
            </div>
            {qrDataUrl && canShowCheckIn && (
              <img src={qrDataUrl} alt="QR minh họa trên phiếu dự thi" className="size-32" />
            )}
          </div>
          <div className="mt-6 grid grid-cols-2 gap-x-10 gap-y-5 text-sm">
            <div>
              <strong>Thí sinh:</strong> {studentName}
            </div>
            <div>
              <strong>MSSV:</strong> {studentCode}
            </div>
            <div>
              <strong>Số báo danh:</strong> {selectedExam.candidateNumber}
            </div>
            <div>
              <strong>Học phần:</strong> {selectedExam.subjectCode}
            </div>
            <div className="col-span-2">
              <strong>Tên học phần:</strong> {selectedExam.subjectName}
            </div>
            <div>
              <strong>Thời gian:</strong> {formatTime(selectedExam.startAt)} ·{' '}
              {formatDate(selectedExam.startAt)}
            </div>
            <div>
              <strong>Thời lượng:</strong> {selectedExam.durationMinutes} phút
            </div>
            <div>
              <strong>Phòng thi:</strong> {selectedExam.room} · {selectedExam.building}
            </div>
            <div>
              <strong>Vị trí:</strong> Kiosk {selectedExam.kioskNumber} · {selectedExam.seatZone}
            </div>
          </div>
          <div className="mt-7 rounded-xl bg-[#f1f5ff] p-4 text-sm leading-relaxed">
            Có mặt trước 15 phút, mang thẻ sinh viên và đến đúng Kiosk được phân công. QR/mã trên
            giao diện hiện là dữ liệu xem trước; thông tin xác thực thật phải do hệ thống backend
            cấp.
          </div>
        </div>
      </section>

      <section className="relative isolate min-h-[210px] overflow-hidden rounded-[24px] border border-[#dce7f3] bg-[linear-gradient(118deg,#ffffff_0%,#f6fbff_56%,#e9f8ff_100%)] shadow-[0_14px_45px_rgba(31,61,105,0.08)] sm:min-h-[226px]">
        <div
          className="pointer-events-none absolute inset-0 opacity-60"
          style={{
            backgroundImage:
              'linear-gradient(rgba(43,112,175,0.055) 1px, transparent 1px), linear-gradient(90deg, rgba(43,112,175,0.055) 1px, transparent 1px)',
            backgroundSize: '28px 28px',
            maskImage: 'linear-gradient(90deg, black 0%, transparent 74%)',
          }}
        />
        <div className="absolute inset-y-0 right-0 hidden w-[48%] lg:block">
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_55%_50%,rgba(76,212,233,0.2),transparent_58%)]" />
          <Suspense
            fallback={
              <div className="flex size-full items-center justify-center text-[#4ba3c6]">
                <LoaderCircle className="size-6 animate-spin" />
              </div>
            }
          >
            <OfficialExamThreeScene
              state={sceneState}
              roomLabel={selectedExam.room}
              kioskNumber={selectedExam.kioskNumber}
            />
          </Suspense>
        </div>

        <div className="relative z-10 flex min-h-[210px] max-w-[760px] flex-col justify-center p-5 sm:min-h-[226px] sm:p-7 lg:w-[58%] xl:p-8">
          <div className="flex flex-wrap items-center gap-2">
            <span className="inline-flex items-center gap-1.5 rounded-full border border-[#cddafd] bg-white/90 px-3 py-1 text-[9px] font-bold tracking-[0.04em] text-[#315bb7] uppercase shadow-sm">
              <ShieldCheck className="size-3.5" />
              Kỳ thi chính thức
            </span>
            <span className="text-[9px] font-semibold text-[#718299]">
              {selectedExam.academicTerm} · {selectedExam.subjectCode}
            </span>
            <span className="rounded-full bg-[#e9f0fa] px-2 py-1 text-[8px] font-bold text-[#60738e]">
              Dữ liệu xem trước FE
            </span>
          </div>
          <h1 className="mt-3 text-[26px] leading-[1.08] font-bold tracking-[-0.035em] text-[#10213e] sm:text-[34px]">
            Lịch Thi Vấn Đáp Chính Thức
          </h1>
          <p className="mt-2 max-w-[620px] text-[11px] leading-relaxed text-[#5f7088] sm:text-xs">
            Theo dõi ca thi, vị trí Kiosk được phân công và các bước check-in bắt buộc tại phòng Lab
            của nhà trường.
          </p>

          <div className="mt-4 flex flex-wrap items-center gap-2.5">
            <div
              className={cn(
                'inline-flex items-center gap-2 rounded-xl border px-3 py-2 text-[9px] font-bold shadow-sm',
                phaseConfig.className,
              )}
            >
              <span className={cn('size-2 rounded-full', phaseConfig.dotClassName)} />
              {phaseConfig.label}
            </div>
            <span className="text-[9px] font-medium text-[#64768e]" role="timer" aria-live="off">
              {phaseConfig.helper}
            </span>
          </div>
        </div>

        <div className="pointer-events-none absolute right-4 bottom-3 hidden items-center gap-2 rounded-full border border-white/70 bg-white/78 px-3 py-1.5 text-[8px] font-bold text-[#41647e] shadow-lg backdrop-blur-md lg:flex">
          <span className="size-1.5 rounded-full bg-[#38c997] shadow-[0_0_8px_rgba(56,201,151,0.75)]" />
          {selectedExam.room} · Kiosk {selectedExam.kioskNumber}
        </div>
      </section>

      <section className="flex items-start gap-3 rounded-2xl border border-[#d9e4f0] bg-white p-4 shadow-[0_7px_24px_rgba(31,61,105,0.045)] sm:px-5">
        <div className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-[#edf3ff] text-[#315ed0]">
          <AlertTriangle className="size-[18px]" />
        </div>
        <div className="min-w-0 flex-1">
          <h2 className="text-[11px] font-bold text-[#26364d]">Lưu ý bắt buộc về địa điểm thi</h2>
          <p className="mt-1 text-[10px] leading-relaxed text-[#68788e] sm:text-[11px]">
            Không thể thực hiện bài thi chính thức trên thiết bị cá nhân. Hãy có mặt trước 15 phút,
            đến đúng Kiosk được Admin phân công và chờ Giảng viên mở ca thi.
          </p>
        </div>
        <span className="hidden rounded-full bg-[#f4f7fb] px-2.5 py-1 text-[8px] font-bold whitespace-nowrap text-[#718095] sm:inline-flex">
          MF-04 · Lab Viva
        </span>
      </section>

      <section aria-label="Chọn lịch thi" className="overflow-x-auto pb-1">
        <div className="flex min-w-max gap-2" aria-label="Các ca thi đã công bố">
          {assignments.map((assignment) => {
            const isSelected = assignment.id === selectedExam.id
            const assignmentPhase = getExamPhase(assignment, now)
            return (
              <button
                key={assignment.id}
                type="button"
                aria-pressed={isSelected}
                onClick={() => handleSelectExam(assignment.id)}
                className={cn(
                  'group flex min-h-12 items-center gap-3 rounded-xl border px-3.5 py-2 text-left transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea]',
                  isSelected
                    ? 'border-[#7896ed] bg-[#eef3ff] shadow-[0_6px_18px_rgba(36,94,234,0.1)]'
                    : 'border-[#dce4ee] bg-white hover:border-[#b9c9de] hover:bg-[#fafcff]',
                )}
              >
                <span
                  className={cn(
                    'flex size-8 items-center justify-center rounded-lg text-[9px] font-bold',
                    isSelected ? 'bg-[#245eea] text-white' : 'bg-[#f0f4f9] text-[#59708e]',
                  )}
                >
                  {formatShortDate(assignment.startAt)}
                </span>
                <span>
                  <span className="block text-[9px] font-bold text-[#25364e]">
                    {assignment.subjectCode} · {formatTime(assignment.startAt)}
                  </span>
                  <span className="mt-0.5 block text-[8px] text-[#7c899b]">
                    {assignment.room} · Kiosk {assignment.kioskNumber}
                  </span>
                </span>
                <span
                  className={cn(
                    'ml-1 size-2 rounded-full',
                    assignmentPhase === 'check-in' ? 'bg-[#35c897]' : 'bg-[#8ea2bf]',
                  )}
                />
              </button>
            )
          })}
        </div>
      </section>

      <div className="grid items-start gap-4 lg:grid-cols-[minmax(0,1fr)_310px] xl:grid-cols-[minmax(0,1fr)_340px]">
        <section className="overflow-hidden rounded-[22px] border border-[#dfe6f0] bg-white shadow-[0_10px_34px_rgba(31,61,105,0.06)]">
          <div className="flex flex-col gap-3 border-b border-[#edf1f6] px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6">
            <div className="flex min-w-0 items-start gap-3">
              <div className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-[#07172e] text-white shadow-[0_7px_18px_rgba(7,23,46,0.16)]">
                <BookOpenCheck className="size-5" />
              </div>
              <div className="min-w-0">
                <p className="text-[8px] font-bold tracking-[0.08em] text-[#4770cd] uppercase">
                  {selectedExam.track}
                </p>
                <h2 className="mt-1 text-[14px] leading-tight font-bold text-[#172842] sm:text-[16px]">
                  {selectedExam.subjectCode} · {selectedExam.subjectName}
                </h2>
                <p className="mt-1 text-[8px] font-medium text-[#8a97a9]">
                  Mã ca: {selectedExam.sessionCode}
                </p>
              </div>
            </div>
            <span
              className={cn(
                'self-start rounded-full border px-2.5 py-1 text-[8px] font-bold',
                phaseConfig.className,
              )}
            >
              {phaseConfig.label}
            </span>
          </div>

          <div className="p-4 sm:p-5 lg:p-6">
            <div className="grid grid-cols-2 gap-2.5 xl:grid-cols-4">
              {details.map(({ label, value, helper, icon: Icon, accentClassName }) => (
                <article
                  key={label}
                  className="min-w-0 rounded-2xl border border-[#e6ebf2] bg-[#fafbfd] p-3.5"
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-[7px] font-bold tracking-[0.05em] text-[#8592a5] uppercase">
                      {label}
                    </span>
                    <span
                      className={cn(
                        'flex size-7 shrink-0 items-center justify-center rounded-lg',
                        accentClassName,
                      )}
                    >
                      <Icon className="size-3.5" />
                    </span>
                  </div>
                  <p className="mt-3 truncate text-[13px] font-bold text-[#203149]">{value}</p>
                  <p className="mt-1 line-clamp-2 min-h-[24px] text-[8px] leading-relaxed text-[#77859a]">
                    {helper}
                  </p>
                </article>
              ))}
            </div>

            <div className="mt-4 flex flex-col gap-3 rounded-2xl border border-[#e1e8f1] bg-[#f6f8fb] p-3.5 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex min-w-0 items-center gap-3">
                <div className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-[linear-gradient(145deg,#245eea,#0d307d)] text-[11px] font-bold text-white shadow-[0_7px_18px_rgba(36,94,234,0.2)]">
                  {getInitials(studentName) || 'SV'}
                </div>
                <div className="min-w-0">
                  <p className="text-[7px] font-bold tracking-[0.06em] text-[#8794a6] uppercase">
                    Thông tin thí sinh dự thi
                  </p>
                  <p className="mt-1 truncate text-[11px] font-bold text-[#23344d]">
                    {studentName}
                  </p>
                  <p className="mt-1 text-[8px] text-[#758399]">
                    {selectedExam.candidateNumber} <span className="mx-1.5 text-[#bcc5d1]">•</span>{' '}
                    MSSV: {studentCode}
                  </p>
                </div>
              </div>
              <div className="flex shrink-0 items-center gap-2 rounded-xl border border-[#cfebe1] bg-white px-3 py-2 text-[8px] font-semibold text-[#277c67]">
                <BadgeCheck className="size-4 text-[#35bd91]" />
                Danh tính đã liên kết
              </div>
            </div>

            <div className="mt-4 overflow-hidden rounded-2xl border border-[#e1e7ef]">
              <button
                type="button"
                onClick={() => setRulesOpen((isOpen) => !isOpen)}
                aria-expanded={rulesOpen}
                className="flex min-h-12 w-full items-center justify-between gap-3 bg-white px-4 text-left transition hover:bg-[#fafcff] focus-visible:outline-2 focus-visible:outline-[#245eea]"
              >
                <span className="flex items-center gap-2 text-[10px] font-bold text-[#27384f]">
                  <Clipboard className="size-4 text-[#4568c7]" />
                  Quy chế phòng Kiosk & tiến trình thi
                </span>
                <span className="flex items-center gap-2 text-[8px] font-semibold text-[#697a91]">
                  3 bước bắt buộc
                  <ChevronDown
                    className={cn('size-4 transition-transform', rulesOpen && 'rotate-180')}
                  />
                </span>
              </button>

              {rulesOpen && (
                <ol className="space-y-2 border-t border-[#edf1f5] bg-[#fbfcfe] p-3 sm:p-4">
                  {[
                    {
                      title: 'Đến sớm & đối chiếu hồ sơ',
                      detail: `Có mặt tại ${selectedExam.room} trước giờ thi 15 phút, mang thẻ sinh viên và kiểm tra đúng ${selectedExam.candidateNumber}.`,
                    },
                    {
                      title: `Check-in tại Kiosk số ${selectedExam.kioskNumber}`,
                      detail:
                        'Quét QR hoặc nhập mã trên đúng Kiosk được phân công. Kiosk xác nhận mã và danh tính theo cấu hình của ca thi.',
                    },
                    {
                      title: 'Kiểm tra Mic rồi chờ Giảng viên mở timer',
                      detail:
                        'Nếu Mic Bad, báo giám thị để đổi máy. Chỉ khi Mic Health và Giảng viên mở ca, Kiosk mới khóa màn hình và bắt đầu tính giờ.',
                    },
                  ].map((rule, index) => (
                    <li
                      key={rule.title}
                      className="flex gap-3 rounded-xl border border-[#e8edf3] bg-white p-3"
                    >
                      <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-[#eaf0ff] text-[9px] font-bold text-[#315eca]">
                        {index + 1}
                      </span>
                      <div>
                        <h3 className="text-[10px] font-bold text-[#2c3c53]">{rule.title}</h3>
                        <p className="mt-1 text-[10px] leading-relaxed text-[#76859a]">
                          {rule.detail}
                        </p>
                      </div>
                    </li>
                  ))}
                </ol>
              )}
            </div>

            <div className="mt-4 flex flex-col gap-2.5 sm:flex-row sm:flex-wrap">
              <button
                type="button"
                onClick={() => setDevicePanelOpen((isOpen) => !isOpen)}
                aria-expanded={devicePanelOpen}
                className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#245eea] px-4 text-[9px] font-bold text-white shadow-[0_10px_24px_rgba(36,94,234,0.22)] transition hover:-translate-y-0.5 hover:bg-[#1b50cc] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea] motion-reduce:transform-none"
              >
                <Mic2 className="size-4" />
                Kiểm tra micro tại Kiosk
              </button>
              <button
                type="button"
                onClick={() => setRoomMapOpen((isOpen) => !isOpen)}
                aria-expanded={roomMapOpen}
                className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[#d7e0eb] bg-white px-4 text-[9px] font-bold text-[#3c4f68] transition hover:border-[#adc0d7] hover:bg-[#f7f9fc] focus-visible:outline-2 focus-visible:outline-[#245eea]"
              >
                <MapPinned className="size-4 text-[#3568d5]" />
                {roomMapOpen ? 'Ẩn sơ đồ phòng' : `Xem sơ đồ ${selectedExam.room}`}
              </button>
              <button
                type="button"
                onClick={handleAddToCalendar}
                className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[#d7e0eb] bg-white px-4 text-[9px] font-bold text-[#3c4f68] transition hover:border-[#adc0d7] hover:bg-[#f7f9fc] focus-visible:outline-2 focus-visible:outline-[#245eea]"
              >
                <CalendarPlus className="size-4 text-[#3568d5]" />
                Thêm vào lịch
              </button>
              <button
                type="button"
                onClick={() => window.print()}
                className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[#d7e0eb] bg-white px-4 text-[9px] font-bold text-[#3c4f68] transition hover:border-[#adc0d7] hover:bg-[#f7f9fc] focus-visible:outline-2 focus-visible:outline-[#245eea]"
              >
                <Printer className="size-4 text-[#3568d5]" />
                In / lưu phiếu dự thi
              </button>
            </div>

            {devicePanelOpen && (
              <div
                ref={devicePanelRef}
                id="exam-readiness"
                className="mt-4 scroll-mt-24 rounded-2xl border border-[#cfdcf1] bg-[linear-gradient(135deg,#f7faff,#eef5ff)] p-4"
              >
                <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                  <div className="flex items-start gap-3">
                    <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-white text-[#315eca] shadow-sm">
                      <Headphones className="size-[18px]" />
                    </span>
                    <div>
                      <h3 className="text-[10px] font-bold text-[#263a57]">
                        Kiểm tra phần cứng tại Kiosk
                      </h3>
                      <p className="mt-1 max-w-[560px] text-[10px] leading-relaxed text-[#687b95]">
                        Bài kiểm tra này chỉ xác nhận micro của trình duyệt hiện tại. Kết quả chính
                        thức được Kiosk kiểm tra lại ngay trước khi Giảng viên bắt đầu timer.
                      </p>
                    </div>
                  </div>
                  <span className="rounded-full bg-white px-2.5 py-1 text-[8px] font-bold text-[#657993] shadow-sm">
                    {selectedExam.workstationId}
                  </span>
                </div>

                <label className="mt-3 flex cursor-pointer items-start gap-2.5 rounded-xl border border-[#dbe4f0] bg-white p-3 text-[10px] leading-relaxed text-[#52647d]">
                  <input
                    type="checkbox"
                    checked={confirmedAtKiosk}
                    onChange={(event) => setConfirmedAtKiosk(event.target.checked)}
                    className="mt-0.5 size-4 accent-[#245eea]"
                  />
                  Tôi xác nhận đang ngồi tại {selectedExam.room}, Kiosk {selectedExam.kioskNumber}{' '}
                  được phân công.
                </label>

                <div className="mt-3 grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center">
                  <div
                    className={cn(
                      'flex min-h-14 items-start gap-3 rounded-xl border p-3',
                      micStatus.className,
                    )}
                  >
                    <MicStatusIcon
                      className={cn(
                        'mt-0.5 size-4 shrink-0',
                        micState === 'checking' && 'animate-spin',
                      )}
                    />
                    <div className="min-w-0">
                      <p className="text-[9px] font-bold">{micStatus.title}</p>
                      <p className="mt-1 text-[9px] leading-relaxed break-words opacity-80">
                        {micStatus.helper}
                      </p>
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={handleMicCheck}
                    disabled={!confirmedAtKiosk || micState === 'checking'}
                    className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#173f9c] px-4 text-[9px] font-bold text-white transition hover:bg-[#12347f] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea] disabled:cursor-not-allowed disabled:opacity-45"
                  >
                    {micState === 'checking' ? (
                      <LoaderCircle className="size-4 animate-spin" />
                    ) : (
                      <RefreshCw className="size-4" />
                    )}
                    {micState === 'ready' || micState === 'error'
                      ? 'Kiểm tra lại'
                      : 'Bắt đầu kiểm tra'}
                  </button>
                </div>
              </div>
            )}

            {roomMapOpen && (
              <div className="mt-4 overflow-hidden rounded-2xl border border-[#dfe6ef] bg-[#f8fafc] p-4">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <h3 className="flex items-center gap-2 text-[10px] font-bold text-[#26384f]">
                      <Building2 className="size-4 text-[#3568d5]" /> Sơ đồ {selectedExam.room}
                    </h3>
                    <p className="mt-1 text-[8px] text-[#75849a]">
                      Đi theo lối vào bên phải và tìm Kiosk được tô xanh.
                    </p>
                  </div>
                  <div className="flex items-center gap-3 text-[8px] text-[#718095]">
                    <span className="flex items-center gap-1.5">
                      <span className="size-2 rounded-sm bg-[#245eea]" /> Vị trí của bạn
                    </span>
                    <span className="flex items-center gap-1.5">
                      <span className="size-2 rounded-sm border border-[#cbd6e4] bg-white" /> Kiosk
                      khác
                    </span>
                  </div>
                </div>
                <div className="mt-4 grid gap-3 sm:grid-cols-[120px_minmax(0,1fr)]">
                  <div className="flex min-h-20 items-center justify-center rounded-xl border border-dashed border-[#b9c8da] bg-white text-center text-[8px] font-bold text-[#60738d]">
                    <div>
                      <UserRound className="mx-auto mb-1 size-4 text-[#496ac0]" />
                      Bàn giám thị
                    </div>
                  </div>
                  <div className="grid grid-cols-4 gap-2">
                    {Array.from({ length: 16 }, (_, index) => index + 1).map((kiosk) => {
                      const isAssigned = kiosk === selectedExam.kioskNumber
                      return (
                        <div
                          key={kiosk}
                          className={cn(
                            'flex min-h-11 items-center justify-center rounded-lg border text-[8px] font-bold',
                            isAssigned
                              ? 'border-[#245eea] bg-[#245eea] text-white shadow-[0_6px_16px_rgba(36,94,234,0.2)]'
                              : 'border-[#dce4ee] bg-white text-[#75849a]',
                          )}
                        >
                          K{kiosk.toString().padStart(2, '0')}
                        </div>
                      )
                    })}
                  </div>
                </div>
                <div className="mt-3 flex items-center justify-end gap-2 text-[8px] font-semibold text-[#5f728c]">
                  <DoorOpen className="size-4 text-[#2aa380]" /> Lối vào phòng
                </div>
              </div>
            )}
          </div>
        </section>

        <aside className="space-y-4">
          <section className="overflow-hidden rounded-[22px] border border-[#dfe6f0] bg-white shadow-[0_10px_34px_rgba(31,61,105,0.06)]">
            <div className="flex items-center justify-between border-b border-[#edf1f6] px-4 py-3.5">
              <div className="flex items-center gap-2">
                <QrCode className="size-4 text-[#315eca]" />
                <h2 className="text-[10px] font-bold text-[#26374e]">Bản xem trước QR Check-in</h2>
              </div>
              <span className="flex items-center gap-1 text-[8px] font-semibold text-[#4e67b4]">
                <ScanLine className="size-3.5" /> Đúng tại Kiosk
              </span>
            </div>

            <div className="p-4">
              {canShowCheckIn ? (
                <>
                  <div className="relative mx-auto aspect-square w-full max-w-[220px] overflow-hidden rounded-2xl border border-[#dce4ee] bg-white p-3 shadow-[0_8px_24px_rgba(26,49,83,0.08)]">
                    {qrDataUrl ? (
                      <img
                        src={qrDataUrl}
                        alt={`QR check-in cho ${selectedExam.room}, Kiosk ${selectedExam.kioskNumber}`}
                        className="size-full object-contain"
                        draggable={false}
                      />
                    ) : qrError ? (
                      <div className="flex size-full flex-col items-center justify-center gap-2 text-center text-[9px] text-[#a14354]">
                        <XCircle className="size-7" /> Không thể tạo mã QR
                      </div>
                    ) : (
                      <div className="flex size-full items-center justify-center text-[#4d68b8]">
                        <LoaderCircle className="size-6 animate-spin" />
                      </div>
                    )}
                    <span className="pointer-events-none absolute top-2 left-2 size-5 rounded-tl-lg border-t-2 border-l-2 border-[#46cfe5]" />
                    <span className="pointer-events-none absolute right-2 bottom-2 size-5 rounded-br-lg border-r-2 border-b-2 border-[#46cfe5]" />
                  </div>
                  <p className="mt-3 text-center font-mono text-[9px] font-bold text-[#26384f]">
                    {selectedExam.candidateNumber} · {selectedExam.subjectCode}
                  </p>
                  <p className="mt-1 text-center text-[9px] leading-relaxed text-[#8290a3]">
                    QR minh họa UI · token thật sẽ do backend cấp cho {selectedExam.workstationId}
                  </p>

                  <div className="mt-3 rounded-xl border border-[#dfe6ef] bg-[#f7f9fc] p-3">
                    <div className="flex items-center justify-between gap-3">
                      <div>
                        <p className="text-[8px] font-bold tracking-[0.05em] text-[#8794a6] uppercase">
                          Mã demo dự phòng
                        </p>
                        <p className="mt-1 font-mono text-[17px] font-bold tracking-[0.18em] text-[#244fba]">
                          {selectedExam.checkInCode}
                        </p>
                      </div>
                      <button
                        type="button"
                        onClick={handleCopyCode}
                        className="flex size-10 items-center justify-center rounded-xl border border-[#d7e0eb] bg-white text-[#4461b4] transition hover:border-[#9cb0d9] hover:bg-[#f2f6ff] focus-visible:outline-2 focus-visible:outline-[#245eea]"
                        aria-label="Sao chép mã check-in dự phòng"
                      >
                        {copyState === 'copied' ? (
                          <Check className="size-4" />
                        ) : (
                          <Copy className="size-4" />
                        )}
                      </button>
                    </div>
                    <div className="mt-2 flex items-center justify-between border-t border-[#e4eaf1] pt-2 text-[8px]">
                      <span className="text-[#7a889b]">Token giao diện mẫu hết hạn sau</span>
                      <span
                        className="font-mono font-bold text-[#365ab2]"
                        role="timer"
                        aria-live="off"
                      >
                        {formatCountdown(tokenTimeLeft)}
                      </span>
                    </div>
                  </div>
                </>
              ) : (
                <div className="flex min-h-[285px] flex-col items-center justify-center rounded-2xl border border-dashed border-[#cad5e3] bg-[#f8fafc] px-5 text-center">
                  <div className="flex size-14 items-center justify-center rounded-2xl bg-white text-[#7184a0] shadow-sm">
                    <QrCode className="size-7" />
                  </div>
                  <h3 className="mt-4 text-[10px] font-bold text-[#36485f]">
                    {phase === 'check-in' ? 'Mã check-in đã hết hạn' : 'QR chưa được phát hành'}
                  </h3>
                  <p className="mt-2 text-[10px] leading-relaxed text-[#7d8a9d]">
                    {phase === 'check-in'
                      ? 'Hãy chờ Kiosk đồng bộ token mới hoặc liên hệ giám thị.'
                      : 'Mã chỉ xuất hiện khi Giảng viên mở ca và cửa sổ check-in bắt đầu.'}
                  </p>
                  <span className="mt-4 inline-flex items-center gap-1.5 rounded-full bg-[#edf2fa] px-3 py-1.5 text-[8px] font-bold text-[#5e718d]">
                    <Clock3 className="size-3.5" /> {phaseConfig.label}
                  </span>
                </div>
              )}
            </div>
          </section>

          <section className="rounded-[22px] border border-[#dfe6f0] bg-white p-4 shadow-[0_10px_34px_rgba(31,61,105,0.05)]">
            <div className="flex items-center justify-between gap-3">
              <div>
                <p className="text-[8px] font-bold tracking-[0.05em] text-[#7890aa] uppercase">
                  Không gian Kiosk thi
                </p>
                <h2 className="mt-1 text-[11px] font-bold text-[#26374e]">
                  {selectedExam.room} · Kiosk {selectedExam.kioskNumber}
                </h2>
              </div>
              <span className="flex size-9 items-center justify-center rounded-xl bg-[#eef4ff] text-[#315eca]">
                <Monitor className="size-4" />
              </span>
            </div>
            <div className="mt-3 overflow-hidden rounded-2xl border border-[#dce6f1] bg-[linear-gradient(145deg,#071a38,#0a315d)] p-3">
              <div className="grid grid-cols-2 gap-2">
                <div className="rounded-lg border border-white/10 bg-white/6 p-2 text-[8px] text-[#bdd2eb]">
                  <Wifi className="mb-1 size-3.5 text-[#68e4cd]" /> Mạng Lab
                  <strong className="mt-0.5 block text-white">Ổn định</strong>
                </div>
                <div className="rounded-lg border border-white/10 bg-white/6 p-2 text-[8px] text-[#bdd2eb]">
                  <Mic2 className="mb-1 size-3.5 text-[#75eaf3]" /> Micro Kiosk
                  <strong className="mt-0.5 block text-white">Chờ kiểm tra</strong>
                </div>
              </div>
              <div className="mt-2 flex items-center gap-2 rounded-lg bg-[#071326]/70 px-2.5 py-2 text-[8px] text-[#b9cbe1]">
                <Sparkles className="size-3.5 text-[#6ce8f1]" /> AI Voice Core kết nối khi Giảng
                viên mở timer.
              </div>
            </div>
          </section>

          <section className="rounded-[22px] border border-[#dfe6f0] bg-white p-4 shadow-[0_10px_34px_rgba(31,61,105,0.05)]">
            <div className="flex items-start gap-3">
              <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-[#eef8f5] text-[#218c72]">
                <Info className="size-4" />
              </span>
              <div>
                <h2 className="text-[10px] font-bold text-[#2e4057]">Cần hỗ trợ kỹ thuật?</h2>
                <p className="mt-1 text-[10px] leading-relaxed text-[#7a899c]">
                  Báo trực tiếp giám thị tại {selectedExam.room}. Không tự ý đổi Kiosk hoặc bỏ qua
                  bước kiểm tra micro.
                </p>
              </div>
            </div>
          </section>
        </aside>
      </div>

      <div className="sr-only" aria-live="polite">
        {copyState === 'copied' && 'Đã sao chép mã check-in.'}
        {copyState === 'error' && 'Không thể sao chép mã check-in.'}
        {micState === 'ready' && 'Micro hoạt động tốt. Hãy chờ Giảng viên mở ca thi.'}
        {micState === 'error' && 'Không nhận được micro. Hãy báo giám thị để đổi máy.'}
      </div>
    </div>
  )
}
