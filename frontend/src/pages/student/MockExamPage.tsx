import { lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  AlertTriangle,
  ArrowRight,
  Bot,
  Check,
  ChevronDown,
  ChevronLeft,
  Eye,
  GraduationCap,
  History,
  Keyboard,
  ListChecks,
  LoaderCircle,
  Mic2,
  PenLine,
  Play,
  ShieldCheck,
  TimerReset,
  X,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { ds } from '@/design/system'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { MockExamScorecard } from '@/components/mock-exam/MockExamScorecard'
import { courseService } from '@/services/course.service'
import {
  getMockExamErrorDetail,
  getMockExamErrorStatus,
  mockExamService,
} from '@/services/mockExam.service'
import type { CourseDto } from '@/types/practice.types'
import type {
  MockExamHistoryItem,
  MockExamQuota,
  MockExamScorecard as MockExamScorecardData,
} from '@/types/mockExam.types'
import {
  readMockExamSession,
  saveMockExamSession,
  secondsUntilExpiry,
} from '@/utils/mockExamSessionStorage'

const MockExamHeroThreeScene = lazy(() =>
  import('./MockExamHeroThreeScene').then((module) => ({
    default: module.MockExamHeroThreeScene,
  })),
)

type DeviceState = 'idle' | 'checking' | 'testing' | 'ready' | 'error' | 'unsupported'
type DeviceIssue = '' | 'permission' | 'silent'

const MIC_VOICE_THRESHOLD = 0.012
const MIC_TEST_LIMIT_MS = 10000
const MIC_VOICE_HOLD_MS = 1500

function readMicrophoneLevel(samples: Uint8Array) {
  let squareSum = 0
  for (const sample of samples) {
    const normalized = (sample - 128) / 128
    squareSum += normalized * normalized
  }
  return Math.sqrt(squareSum / samples.length)
}

const RECENT_HISTORY_SIZE = 3
const ARCHIVE_PAGE_SIZE = 8

const HISTORY_STATUS_OPTIONS = [
  { value: '', label: 'Mọi trạng thái' },
  { value: 'in_progress', label: 'Đang làm' },
  { value: 'submitted', label: 'Đã nộp' },
  { value: 'graded', label: 'Đã chấm' },
  { value: 'abandoned', label: 'Đã bỏ' },
]

function FilterDropMenu({
  label,
  value,
  options,
  onChange,
  className,
}: {
  label: string
  value: string
  options: { value: string; label: string }[]
  onChange: (value: string) => void
  className?: string
}) {
  const [open, setOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)
  const selected = options.find((option) => option.value === value)?.label ?? label

  useEffect(() => {
    if (!open) return
    const onPointerDown = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false)
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false)
    }
    window.addEventListener('pointerdown', onPointerDown)
    window.addEventListener('keydown', onKeyDown)
    return () => {
      window.removeEventListener('pointerdown', onPointerDown)
      window.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  return (
    <div ref={rootRef} data-filter-open={open ? 'true' : undefined} className={cn('relative', className)}>
      <button
        type="button"
        aria-label={label}
        aria-expanded={open}
        aria-haspopup="listbox"
        onClick={() => setOpen((current) => !current)}
        className={cn(
          'relative inline-flex h-10 w-full items-center rounded-full border bg-white py-0 pr-11 pl-4 text-left text-sm font-semibold text-[#0F2854] shadow-[0_2px_8px_rgba(23,48,96,0.04)]',
          open ? 'border-[#4988C4]/45 ring-2 ring-[#1C4D8D]/15' : 'border-[#4988C4]/45',
        )}
      >
        <span className="truncate">{selected}</span>
        <ChevronDown
          className={cn(
            'pointer-events-none absolute right-5 size-4 text-[#1C4D8D] transition',
            open && 'rotate-180',
          )}
        />
      </button>
      {open && (
        <ul
          role="listbox"
          aria-label={label}
          className="absolute top-[calc(100%+8px)] left-0 z-30 max-h-64 min-w-full overflow-y-auto rounded-2xl border border-[#4988C4]/45 bg-white p-1.5 shadow-[0_16px_36px_rgba(23,48,96,0.14)]"
        >
          {options.map((option) => {
            const isSelected = option.value === value
            return (
              <li key={option.value || 'all'}>
                <button
                  type="button"
                  role="option"
                  aria-selected={isSelected}
                  onClick={() => {
                    onChange(option.value)
                    setOpen(false)
                  }}
                  className={cn(
                    'flex w-full items-center gap-3 rounded-xl px-3 py-2 text-left text-sm font-semibold',
                    isSelected
                      ? 'bg-[#4988C4]/16 text-[#1C4D8D]'
                      : 'text-[#0F2854] hover:bg-[#4988C4]/18',
                  )}
                >
                  <span className="min-w-0 flex-1 truncate">{option.label}</span>
                  {isSelected && <Check className="size-4 shrink-0" />}
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}

const COURSE_CODE_TONES = [
  'bg-[#4988C4]/16 text-[#1C4D8D]',
  'bg-[#e8f8f3] text-[#16836a]',
  'bg-[#fff4e8] text-[#c26a12]',
  'bg-[#f3eefe] text-[#6d4cc4]',
  'bg-[#e7f6fb] text-[#0e7490]',
  'bg-[#fdecef] text-[#c2415b]',
  'bg-[#eef8e8] text-[#3d7a2a]',
  'bg-[#fff0f6] text-[#be185d]',
]

function courseCodeTone(code: string) {
  let hash = 0
  for (let index = 0; index < code.length; index += 1) {
    hash = (hash * 31 + code.charCodeAt(index)) >>> 0
  }
  return COURSE_CODE_TONES[hash % COURSE_CODE_TONES.length]
}

const EXAM_STEPS = [
  {
    title: 'Chọn môn còn lượt',
    hint: 'Bấm Bắt đầu và chọn hình thức thi.',
    icon: Play,
    card: 'border-[#9ec4e8] bg-[#f3f9fd]',
    badge: 'bg-[#1C4D8D] text-white',
    iconClass: 'text-[#1C4D8D]',
  },
  {
    title: 'Kiểm tra micro',
    hint: 'Nói thử một câu trước khi vào phòng.',
    icon: Mic2,
    card: 'border-[#b7e4cf] bg-[#f3fbf7]',
    badge: 'bg-[#16836a] text-white',
    iconClass: 'text-[#16836a]',
  },
  {
    title: 'Nhấn giữ để nói',
    hint: 'Giữ micro hoặc phím Space khi trả lời.',
    icon: Keyboard,
    card: 'border-[#f5b6d6] bg-[#fff5fa]',
    badge: 'bg-[#e11d74] text-white',
    iconClass: 'text-[#9d174d]',
  },
  {
    title: 'Sửa lời rồi nộp',
    hint: 'Thả ra để xem và sửa trước khi nộp.',
    icon: PenLine,
    card: 'border-[#d4c4f5] bg-[#f7f3fd]',
    badge: 'bg-[#6d4cc4] text-white',
    iconClass: 'text-[#6d4cc4]',
  },
]

const EXAM_NOTES = [
  'Điểm thi thử không cộng vào học phần.',
  'Mỗi môn có số lượt riêng, làm mới lúc 00:00.',
  'Hết giờ bài tự nộp. Câu chưa trả lời tính 0 điểm.',
  'Môn hết lượt không bắt đầu được đến ngày hôm sau.',
  'Có thể bật tự động đọc câu trong phòng thi.',
]

const DEVICE_STATUS: Record<
  DeviceState,
  { title: string; helper: string; color: string; dot: string }
> = {
  idle: {
    title: 'Chưa kiểm tra micro',
    helper: 'Nói thử một câu trước khi vào phòng.',
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
    helper: 'Bạn có thể thử lại bằng Chrome hoặc Edge phiên bản mới.',
    color: 'text-[#a26812]',
    dot: 'bg-[#e1a13f]',
  },
}

function formatExamDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone: 'Asia/Ho_Chi_Minh',
  }).format(date)
}

function formatExamDayTime(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return { day: value, time: '' }
  return {
    day: new Intl.DateTimeFormat('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      timeZone: 'Asia/Ho_Chi_Minh',
    }).format(date),
    time: new Intl.DateTimeFormat('vi-VN', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
      timeZone: 'Asia/Ho_Chi_Minh',
    }).format(date),
  }
}

function formatClock(totalSeconds: number) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
}

function statusLabel(status: string) {
  return HISTORY_STATUS_OPTIONS.find((option) => option.value === status)?.label ?? status
}

function statusChipClass(status: string) {
  if (status === 'graded' || status === 'submitted') return 'bg-[#e8f8f1] text-[#16836a]'
  if (status === 'abandoned') return 'bg-[#fdecec] text-[#c2415b]'
  return 'bg-[#F4FBFD] text-[#1C4D8D]'
}

export function MockExamPage() {
  const navigate = useNavigate()
  const [courses, setCourses] = useState<CourseDto[]>([])
  const [openCourseId, setOpenCourseId] = useState<string | null>(null)
  const [quotas, setQuotas] = useState<Record<string, MockExamQuota>>({})
  const [quotaFailedIds, setQuotaFailedIds] = useState<string[]>([])
  const [hasFollowUp, setHasFollowUp] = useState(false)
  const [isLoadingCourses, setIsLoadingCourses] = useState(true)
  const [isLoadingQuotas, setIsLoadingQuotas] = useState(false)
  const [isStarting, setIsStarting] = useState(false)
  const [deviceState, setDeviceState] = useState<DeviceState>('idle')
  const [deviceIssue, setDeviceIssue] = useState<DeviceIssue>('')
  const [deviceNames, setDeviceNames] = useState('')
  const [micLevel, setMicLevel] = useState(0)
  const micTestRef = useRef<{ cancelled: boolean; stop: () => void } | null>(null)
  const [pageError, setPageError] = useState('')
  const [quotaModal, setQuotaModal] = useState('')
  const [confirmReplace, setConfirmReplace] = useState(false)
  const [guideOpen, setGuideOpen] = useState(false)
  const [historyItems, setHistoryItems] = useState<MockExamHistoryItem[]>([])
  const [historyTotal, setHistoryTotal] = useState(0)
  const [archiveOpen, setArchiveOpen] = useState(false)
  const [archiveCourseId, setArchiveCourseId] = useState('')
  const [archiveStatus, setArchiveStatus] = useState('')
  const [archivePage, setArchivePage] = useState(1)
  const [archiveItems, setArchiveItems] = useState<MockExamHistoryItem[]>([])
  const [archiveTotal, setArchiveTotal] = useState(0)
  const [archiveTotalPages, setArchiveTotalPages] = useState(0)
  const [isLoadingArchive, setIsLoadingArchive] = useState(false)
  const [archiveError, setArchiveError] = useState('')
  const [isLoadingHistory, setIsLoadingHistory] = useState(true)
  const [historyError, setHistoryError] = useState('')
  const [expandedHistoryId, setExpandedHistoryId] = useState<string | null>(null)
  const [historyDetails, setHistoryDetails] = useState<Record<string, MockExamScorecardData>>({})
  const [detailError, setDetailError] = useState('')
  const [loadingDetailId, setLoadingDetailId] = useState<string | null>(null)
  const [activeSessionVersion, setActiveSessionVersion] = useState(0)

  const activeSession = useMemo(() => {
    void activeSessionVersion
    return readMockExamSession()
  }, [activeSessionVersion])
  const activeSecondsLeft = activeSession ? secondsUntilExpiry(activeSession.expiresAt) : 0
  const modalCourse = courses.find((course) => course.id === openCourseId) ?? null
  const modalQuota = modalCourse ? (quotas[modalCourse.id] ?? null) : null
  const deviceStatus = DEVICE_STATUS[deviceState]
  const canStart = Boolean(
    modalCourse && modalQuota?.canStartExam && deviceState === 'ready' && !isStarting,
  )

  const loadQuotas = useCallback(async (courseIds: string[]) => {
    if (courseIds.length === 0) {
      setQuotas({})
      setQuotaFailedIds([])
      return
    }

    setIsLoadingQuotas(true)
    const results = await Promise.all(
      courseIds.map(async (courseId) => {
        try {
          return { courseId, quota: await mockExamService.getQuota(courseId), failed: false }
        } catch {
          return { courseId, quota: null, failed: true }
        }
      }),
    )
    const nextQuotas: Record<string, MockExamQuota> = {}
    const failed: string[] = []
    for (const result of results) {
      if (result.quota) nextQuotas[result.courseId] = result.quota
      if (result.failed) failed.push(result.courseId)
    }
    setQuotas(nextQuotas)
    setQuotaFailedIds(failed)
    setIsLoadingQuotas(false)
  }, [])

  const loadCourses = useCallback(async () => {
    setIsLoadingCourses(true)
    setPageError('')
    try {
      const nextCourses = (await courseService.getCourses()).filter((course) => course.isActive)
      setCourses(nextCourses)
      void loadQuotas(nextCourses.map((course) => course.id))
    } catch (error) {
      setPageError(getMockExamErrorDetail(error, 'Không thể tải danh sách môn học.'))
    } finally {
      setIsLoadingCourses(false)
    }
  }, [loadQuotas])

  const loadHistory = useCallback(async () => {
    setIsLoadingHistory(true)
    setHistoryError('')
    try {
      const page = await mockExamService.getHistory({
        pageIndex: 1,
        pageSize: RECENT_HISTORY_SIZE,
      })
      setHistoryItems(page.items)
      setHistoryTotal(page.totalCount)
    } catch (error) {
      setHistoryItems([])
      setHistoryError(getMockExamErrorDetail(error, 'Không thể tải lịch sử thi thử.'))
    } finally {
      setIsLoadingHistory(false)
    }
  }, [])

  const loadArchive = useCallback(async () => {
    if (!archiveOpen) return
    setIsLoadingArchive(true)
    setArchiveError('')
    try {
      const page = await mockExamService.getHistory({
        courseId: archiveCourseId || undefined,
        status: archiveStatus || undefined,
        pageIndex: archivePage,
        pageSize: ARCHIVE_PAGE_SIZE,
      })
      setArchiveItems(page.items)
      setArchiveTotal(page.totalCount)
      setArchiveTotalPages(page.totalPages)
    } catch (error) {
      setArchiveItems([])
      setArchiveError(getMockExamErrorDetail(error, 'Không thể tải toàn bộ lịch sử thi thử.'))
    } finally {
      setIsLoadingArchive(false)
    }
  }, [archiveCourseId, archiveOpen, archivePage, archiveStatus])

  useEffect(() => {
    const timer = window.setTimeout(() => void loadCourses(), 0)
    return () => window.clearTimeout(timer)
  }, [loadCourses])

  useEffect(() => {
    const timer = window.setTimeout(() => void loadHistory(), 0)
    return () => window.clearTimeout(timer)
  }, [loadHistory])

  const pageScrollLocked = Boolean(archiveOpen || expandedHistoryId || openCourseId || quotaModal)

  useEffect(() => {
    if (!pageScrollLocked) return
    const previousBody = document.body.style.overflow
    const previousRoot = document.documentElement.style.overflow
    document.body.style.overflow = 'hidden'
    document.documentElement.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = previousBody
      document.documentElement.style.overflow = previousRoot
    }
  }, [pageScrollLocked])

  useEffect(() => {
    if (!archiveOpen) return
    const timer = window.setTimeout(() => void loadArchive(), 0)
    return () => window.clearTimeout(timer)
  }, [archiveOpen, loadArchive])

  const stopMicTest = useCallback(() => {
    micTestRef.current?.stop()
    micTestRef.current = null
  }, [])

  const closeCourseModal = useCallback(() => {
    stopMicTest()
    setMicLevel(0)
    setDeviceState((current) =>
      current === 'checking' || current === 'testing' ? 'idle' : current,
    )
    setOpenCourseId(null)
  }, [stopMicTest])

  useEffect(() => {
    if (!quotaModal && !openCourseId && !archiveOpen && !expandedHistoryId) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      if (document.querySelector('[data-filter-open="true"]')) return
      if (expandedHistoryId) setExpandedHistoryId(null)
      else if (quotaModal) setQuotaModal('')
      else if (archiveOpen) setArchiveOpen(false)
      else closeCourseModal()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [archiveOpen, closeCourseModal, expandedHistoryId, openCourseId, quotaModal])

  useEffect(
    () => () => {
      micTestRef.current?.stop()
    },
    [],
  )

  const handleDeviceCheck = async () => {
    const AudioContextConstructor =
      window.AudioContext ??
      (window as typeof window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext

    if (!navigator.mediaDevices?.getUserMedia || !AudioContextConstructor) {
      setDeviceIssue('')
      setDeviceState('unsupported')
      return
    }

    stopMicTest()
    setMicLevel(0)
    setDeviceIssue('')
    setDeviceNames('')
    setDeviceState('checking')

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

      setDeviceNames(audioTrack.label || 'Microphone mặc định')
      setDeviceState('testing')

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
        setDeviceIssue('silent')
        setDeviceState('error')
        return
      }

      setMicLevel(0)
      setDeviceIssue('')
      setDeviceState('ready')
    } catch {
      if (session.cancelled) return
      setMicLevel(0)
      setDeviceIssue('permission')
      setDeviceState('error')
    } finally {
      session.stop()
      if (micTestRef.current === session) micTestRef.current = null
    }
  }

  const openCourse = (courseId: string) => {
    setOpenCourseId(courseId)
    setHasFollowUp(false)
    setPageError('')
    setExpandedHistoryId(null)
  }

  const startExam = async () => {
    if (!modalCourse || !modalQuota?.canStartExam || deviceState !== 'ready') return
    setIsStarting(true)
    setPageError('')
    try {
      const session = await mockExamService.startSession(modalCourse.id, hasFollowUp)
      setConfirmReplace(false)
      saveMockExamSession({ ...session, answers: [] })
      setActiveSessionVersion((current) => current + 1)
      navigate(`/mock-exam/session?session=${encodeURIComponent(session.sessionId)}`)
    } catch (error) {
      if (getMockExamErrorStatus(error) === 429) {
        setQuotas((current) => {
          const existing = current[modalCourse.id]
          return existing
            ? {
                ...current,
                [modalCourse.id]: { ...existing, remainingCount: 0, canStartExam: false },
              }
            : current
        })
        setConfirmReplace(false)
        setQuotaModal(
          getMockExamErrorDetail(
            error,
            'Bạn đã dùng hết hạn ngạch thi thử hôm nay. Vui lòng quay lại vào ngày mai.',
          ),
        )
        const refreshed = await mockExamService.getQuota(modalCourse.id).catch(() => null)
        if (refreshed) {
          setQuotas((current) => ({ ...current, [modalCourse.id]: refreshed }))
          setQuotaFailedIds((current) => current.filter((id) => id !== modalCourse.id))
        }
        return
      }
      setConfirmReplace(false)
      setPageError(getMockExamErrorDetail(error, 'Không thể bắt đầu ca thi thử.'))
    } finally {
      setIsStarting(false)
    }
  }

  const handleStartExam = () => {
    if (!modalCourse || !modalQuota?.canStartExam || deviceState !== 'ready') return
    if (activeSession && activeSecondsLeft > 0) {
      setConfirmReplace(true)
      return
    }
    void startExam()
  }

  const handleToggleHistory = async (item: MockExamHistoryItem) => {
    if (item.status === 'in_progress' && activeSession?.sessionId === item.sessionId) {
      navigate(`/mock-exam/session?session=${encodeURIComponent(item.sessionId)}`)
      return
    }
    if (expandedHistoryId === item.sessionId) {
      setExpandedHistoryId(null)
      return
    }

    setExpandedHistoryId(item.sessionId)
    setDetailError('')
    if (historyDetails[item.sessionId]) return

    setLoadingDetailId(item.sessionId)
    try {
      const detail = await mockExamService.getHistoryDetail(item.sessionId)
      setHistoryDetails((current) => ({ ...current, [item.sessionId]: detail }))
    } catch (error) {
      setDetailError(getMockExamErrorDetail(error, 'Không thể mở bảng điểm của ca thi này.'))
    } finally {
      setLoadingDetailId(null)
    }
  }

  const renderHistoryAction = (item: MockExamHistoryItem) => {
    const isOpen = expandedHistoryId === item.sessionId
    const canResume = item.status === 'in_progress' && activeSession?.sessionId === item.sessionId
    return (
      <button
        type="button"
        onClick={() => void handleToggleHistory(item)}
        className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#4988C4]/45 bg-white px-3 text-xs font-semibold text-[#0F2854] shadow-[0_3px_8px_rgba(23,48,96,0.05)] transition hover:border-[#4988C4]/45 hover:text-[#1C4D8D] focus-visible:outline-2 focus-visible:outline-[#1C4D8D]"
        aria-expanded={canResume ? undefined : isOpen}
      >
        <Eye className="size-3" />
        {canResume ? 'Tiếp tục ca thi' : isOpen ? 'Đóng bảng điểm' : 'Xem bảng điểm'}
      </button>
    )
  }

  const selectedDetail = expandedHistoryId ? historyDetails[expandedHistoryId] : undefined

  return (
    <div className={ds.page}>
      <section className={cn(ds.banner, 'min-h-[218px] sm:min-h-[205px]')}>
        <div className="pointer-events-none absolute inset-0 [background-image:radial-gradient(rgba(255,255,255,0.35)_0.8px,transparent_0.8px)] [background-size:22px_22px] opacity-40" />
        <div className="pointer-events-none absolute inset-y-0 right-0 w-[75%] bg-[radial-gradient(circle_at_72%_48%,rgba(189,232,245,0.22),transparent_46%)] sm:w-[60%]" />
        <div className="relative z-10 flex min-h-[218px] max-w-[760px] flex-col items-start justify-center px-5 py-6 sm:min-h-[205px] sm:px-7 lg:max-w-[64%] lg:px-8">
          <div className={cn(ds.bannerChip, 'backdrop-blur-sm')}>
            <ShieldCheck className="size-3" />
            Phòng khảo thí mô phỏng · AI Proctor
          </div>
          <h1 className={ds.bannerTitle}>
            Thi thử Vấn đáp Giả lập
          </h1>
          <p className={cn(ds.bannerText, 'max-w-[620px]')}>
            Làm bài có đồng hồ máy chủ, trả lời bằng giọng nói và nhận bảng điểm theo rubric ngay
            khi nộp.
          </p>
          <div className="mt-4 flex flex-wrap items-center gap-2">
            <span className="inline-flex items-center gap-1.5 rounded-full border border-white/25 bg-white/12 px-3 py-1.5 text-xs font-semibold text-white backdrop-blur-sm">
              <TimerReset className="size-3 text-[#BDE8F5]" /> Đồng hồ đồng bộ thời gian máy chủ
            </span>
            <span className="inline-flex items-center gap-1.5 rounded-full border border-[#d6ece7] bg-[#f5fffc]/80 px-3 py-1.5 text-xs font-semibold text-[#26816c] backdrop-blur-sm">
              <span className="size-1.5 animate-pulse rounded-full bg-[#31c494] motion-reduce:animate-none" />
              Nhấn giữ micro hoặc phím Space
            </span>
          </div>
        </div>
        <div className="pointer-events-none absolute inset-y-0 right-[-30%] z-0 w-[72%] opacity-35 sm:right-[-7%] sm:w-[62%] sm:opacity-85 lg:right-0 lg:w-[46%] lg:opacity-100">
          <Suspense
            fallback={
              <div className="flex size-full items-center justify-center" aria-hidden="true">
                <div className="size-20 animate-pulse rounded-full border border-[#4988C4]/30 bg-[#4988C4]/10 motion-reduce:animate-none" />
              </div>
            }
          >
            <MockExamHeroThreeScene
              scanning={deviceState === 'checking' || deviceState === 'testing'}
              ready={deviceState === 'ready'}
            />
          </Suspense>
        </div>
        <div className="pointer-events-none absolute top-5 right-5 z-10 hidden items-center gap-2 rounded-xl border border-white/25 bg-white/12 px-3 py-2 text-xs font-semibold text-white shadow-sm backdrop-blur-md md:flex">
          <Bot className="size-4 text-[#BDE8F5]" />
          <span>
            Chấm điểm tức thì
            <span className="mt-0.5 block text-xs font-medium text-white/75">
              Rubric 10.0 theo CLO
            </span>
          </span>
        </div>
      </section>

      {activeSession && (
        <div className="flex flex-col gap-3 rounded-2xl border border-[#4988C4]/45 bg-white px-5 py-4 shadow-[0_1px_0_#fff_inset,0_14px_32px_rgba(36,78,170,0.12)] sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="text-sm font-bold text-[#0F2854]">
              Ca thi {activeSession.courseCode} vẫn đang mở
            </p>
            <p className="mt-1 text-sm text-[#5b6b84]">
              {activeSecondsLeft > 0
                ? `Còn ${formatClock(activeSecondsLeft)}`
                : 'Đã hết giờ. Vào phòng để nộp bài.'}
            </p>
          </div>
          <button
            type="button"
            onClick={() =>
              navigate(`/mock-exam/session?session=${encodeURIComponent(activeSession.sessionId)}`)
            }
            className={ds.primaryButton}
          >
            Tiếp tục ca thi
            <ArrowRight className="size-4" />
          </button>
        </div>
      )}

      <section className={ds.section}>
        <button
          type="button"
          onClick={() => setGuideOpen((open) => !open)}
          aria-expanded={guideOpen}
          className={cn(
            ds.sectionHeader,
            'flex w-full cursor-pointer items-center justify-between gap-3 text-left',
            !guideOpen && 'border-b-0',
          )}
        >
          <span>
            <span className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}>
                <ListChecks className="size-[18px] stroke-[2.75]" aria-hidden="true" />
              </span>
              <h2 className={ds.sectionTitle}>Hướng dẫn thi thử</h2>
            </span>
            <span className={cn(ds.sectionHint, 'block')}>
              Bốn bước từ lúc chọn môn đến khi nộp bài.
            </span>
          </span>
          <ChevronDown
            className={cn(
              'size-5 shrink-0 text-[#1C4D8D] transition',
              guideOpen && 'rotate-180',
            )}
            aria-hidden="true"
          />
        </button>
        {guideOpen && (
          <div className={ds.sectionBody}>
            <ol className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              {EXAM_STEPS.map((step, index) => {
                const StepIcon = step.icon
                return (
                  <li
                    key={step.title}
                    className={cn(
                      'flex gap-3 rounded-2xl border p-3.5 shadow-[0_8px_22px_rgba(23,48,96,0.06)]',
                      step.card,
                    )}
                  >
                    <span
                      className={cn(
                        'flex size-8 shrink-0 items-center justify-center rounded-full text-sm font-bold',
                        step.badge,
                      )}
                    >
                      {index + 1}
                    </span>
                    <span className="min-w-0">
                      <span className="flex items-center gap-1.5 text-sm font-bold text-[#0F2854]">
                        <StepIcon className={cn('size-4 shrink-0 stroke-[2.5]', step.iconClass)} />
                        {step.title}
                      </span>
                      <span className="mt-1 block text-xs leading-relaxed text-[#5b6b84]">
                        {step.hint}
                      </span>
                    </span>
                  </li>
                )
              })}
            </ol>
            <div className="mt-4 rounded-2xl border border-[#f0b56a] bg-[#fff6e8] p-4">
              <h3 className="flex items-center gap-2 text-sm font-bold text-[#b45309]">
                <AlertTriangle className="size-4 shrink-0 text-[#e07a12]" aria-hidden="true" />
                Lưu ý
              </h3>
              <ul className="mt-3 flex flex-wrap gap-2">
                {EXAM_NOTES.map((note) => (
                  <li
                    key={note}
                    className="inline-flex items-center gap-1.5 rounded-full border border-[#f0b56a] bg-white px-3 py-1.5 text-xs font-semibold text-[#b45309]"
                  >
                    <span className="size-1.5 shrink-0 rounded-full bg-[#e07a12]" />
                    {note}
                  </li>
                ))}
              </ul>
            </div>
          </div>
        )}
      </section>

      <section className={ds.section}>
        <div className={ds.sectionHeader}>
          <div className={ds.sectionTitleRow}>
            <span className={ds.sectionIconWrap}>
              <GraduationCap className="size-[18px] stroke-[2.75]" aria-hidden="true" />
            </span>
            <h2 className={ds.sectionTitle}>Môn học</h2>
          </div>
          <p className={ds.sectionHint}>
            Môn hết lượt được đánh dấu riêng và không bắt đầu được cho đến ngày hôm sau.
          </p>
        </div>

        <div className={cn(ds.sectionBody, 'space-y-4')}>
          {isLoadingCourses ? (
            <div className="flex items-center gap-2 text-sm font-semibold text-[#5b6b84]">
              <LoaderCircle className="size-4 animate-spin text-[#1C4D8D]" />
              Đang tải môn học…
            </div>
          ) : courses.length === 0 ? (
            <p className="text-sm text-[#5b6b84]">Chưa có môn học đang mở để thi thử.</p>
          ) : (
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {courses.map((course) => {
                const courseQuota = quotas[course.id]
                const exhausted = Boolean(courseQuota && !courseQuota.canStartExam)
                const quotaUnknown = !isLoadingQuotas && quotaFailedIds.includes(course.id)
                return (
                  <article
                    key={course.id}
                    className={cn(
                      'flex h-full flex-col rounded-2xl border p-4 shadow-[0_1px_0_#fff_inset,0_12px_28px_rgba(23,48,96,0.06)] transition duration-200',
                      exhausted
                        ? 'border-[#efc6ce] bg-[linear-gradient(180deg,#fff7f8_0%,#ffffff_48%)]'
                        : 'border-[#4988C4]/45 bg-white hover:-translate-y-0.5 hover:border-[#4988C4]/45 hover:shadow-[0_16px_32px_rgba(36,78,170,0.1)]',
                    )}
                  >
                    <div className="flex items-center justify-between gap-2">
                      <span
                        className={cn(
                          'inline-flex items-center rounded-lg px-2.5 py-1 text-sm font-bold tracking-[0.04em]',
                          courseCodeTone(course.code),
                        )}
                      >
                        {course.code}
                      </span>
                      <span
                        className={cn(
                          'inline-flex shrink-0 items-center gap-1 rounded-full border px-2 py-1 text-sm font-semibold',
                          exhausted
                            ? 'border-[#f3c2c8] bg-[#fdecec] text-[#c2415b]'
                            : courseQuota
                              ? 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]'
                              : 'border-[#4988C4]/30 bg-[#F4FBFD] text-[#1C4D8D]',
                        )}
                      >
                        <TimerReset className="size-3 shrink-0" />
                        {courseQuota
                          ? exhausted
                            ? 'Hết lượt'
                            : `Còn ${courseQuota.remainingCount}/${courseQuota.maxDailyQuota} lượt`
                          : quotaUnknown
                            ? 'Chưa rõ lượt'
                            : 'Đang kiểm tra…'}
                      </span>
                    </div>
                    <h3
                      className={cn(
                        'mt-3.5 text-[20px] leading-snug font-bold',
                        exhausted ? 'text-[#7a4450]' : 'text-[#0F2854]',
                      )}
                    >
                      {course.name}
                      {' - '}
                      <span className="relative -top-0.5 inline-flex items-center gap-1.5 rounded-full border border-[#4988C4]/45 bg-white px-2 py-1 align-middle text-xs font-semibold text-[#5b6b84]">
                        <GraduationCap className="size-3.5 shrink-0 text-[#1C4D8D]" />
                        {course.credits} tín chỉ
                      </span>
                    </h3>
                    <div className="mt-auto flex pt-4">
                      <button
                        type="button"
                        onClick={() => openCourse(course.id)}
                        disabled={exhausted}
                        className={cn(
                          'inline-flex h-10 items-center justify-center gap-1.5 rounded-xl px-4 text-sm font-bold',
                          exhausted
                            ? 'cursor-not-allowed bg-[#f3d5db] text-[#9a4d5c]'
                            : 'bg-[#1C4D8D] text-white shadow-[0_8px_16px_rgba(36,94,234,0.22)] hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]',
                        )}
                      >
                        <Play className="size-3.5 fill-current" />
                        {exhausted ? 'Hết lượt' : 'Bắt đầu'}
                      </button>
                    </div>
                  </article>
                )
              })}
            </div>
          )}

          {pageError && !openCourseId && (
            <div className="flex items-start gap-2 rounded-xl border border-[#e7b7c2] bg-white p-3 text-sm leading-relaxed text-[#9a3d50] shadow-[0_6px_16px_rgba(160,40,60,0.06)]">
              <AlertTriangle className="mt-0.5 size-4 shrink-0" />
              {pageError}
            </div>
          )}
        </div>
      </section>

      <section className={ds.section}>
        <div className={cn(ds.sectionHeader, 'flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between')}>
          <div>
            <div className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}>
                <History className="size-[18px] stroke-[2.75]" aria-hidden="true" />
              </span>
              <h2 className={ds.sectionTitle}>Lịch sử</h2>
            </div>
            <p className={ds.sectionHint}>
              {historyTotal > RECENT_HISTORY_SIZE
                ? `${RECENT_HISTORY_SIZE} phiên gần nhất trong ${historyTotal} lượt.`
                : 'Các phiên thi thử gần nhất.'}
            </p>
          </div>
          <button
            type="button"
            onClick={() => {
              setArchiveOpen(true)
              setExpandedHistoryId(null)
            }}
            className={ds.outlineButton}
          >
            Xem toàn bộ lịch sử
          </button>
        </div>

        {historyError && (
          <div className="px-5 py-4 text-xs text-[#ae3d50] sm:px-6">{historyError}</div>
        )}
        {isLoadingHistory ? (
          <div className="flex items-center gap-2 px-5 py-8 text-sm font-semibold text-[#5b6b84] sm:px-6">
            <LoaderCircle className="size-4 animate-spin text-[#1C4D8D]" />
            Đang tải lịch sử…
          </div>
        ) : historyItems.length === 0 ? (
          <p className="px-5 py-8 text-sm text-[#5b6b84] sm:px-6">Chưa có ca thi thử nào.</p>
        ) : (
          <>
            <div className="hidden md:block">
              <table className="w-full table-fixed border-collapse text-left">
                <thead className="border-b border-[#4988C4]/45 bg-white text-xs font-bold text-[#5b6b84]">
                  <tr>
                    <th className="w-[10%] px-5 py-3">Mã ca</th>
                    <th className="w-[34%] px-3 py-3">Môn thi</th>
                    <th className="w-[16%] px-3 py-3">Thời điểm</th>
                    <th className="w-[12%] px-3 py-3">Trạng thái</th>
                    <th className="w-[12%] px-3 py-3">Điểm</th>
                    <th className="w-[16%] px-3 py-3">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#4988C4]/30">
                  {historyItems.map((item) => (
                    <tr key={item.sessionId} className="text-sm text-[#5b6b84]">
                      <td className="px-5 py-3 font-mono font-bold text-[#1C4D8D]">
                        {item.sessionId.slice(0, 8).toUpperCase()}
                      </td>
                      <td className="px-3 py-3">
                        <p className="font-bold text-[#0F2854]">{item.courseName}</p>
                        <p className="mt-0.5 truncate text-xs text-[#5b6b84]">
                          {item.courseCode} · {item.answeredQuestions}/{item.totalQuestions} câu
                        </p>
                      </td>
                      <td className="px-3 py-3">{formatExamDate(item.startedAt)}</td>
                      <td className="px-3 py-3">
                        <span
                          className={cn(
                            'inline-flex rounded-full px-2.5 py-1 text-xs font-bold',
                            statusChipClass(item.status),
                          )}
                        >
                          {statusLabel(item.status)}
                        </span>
                      </td>
                      <td className="px-3 py-3">
                        {item.totalScore === null ? (
                          '—'
                        ) : (
                          <span className="inline-flex items-center gap-1 rounded-full bg-[#e8f8f1] px-2 py-1 font-bold text-[#16836a]">
                            {item.totalScore.toFixed(2)} / 10
                          </span>
                        )}
                      </td>
                      <td className="px-3 py-3">{renderHistoryAction(item)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="divide-y divide-[#4988C4]/30 md:hidden">
              {historyItems.map((item) => (
                <article key={item.sessionId} className="space-y-3 p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <h3 className="text-sm font-bold text-[#0F2854]">{item.courseName}</h3>
                      <p className="mt-0.5 text-xs text-[#5b6b84]">
                        {item.courseCode} · {statusLabel(item.status)}
                      </p>
                    </div>
                    <span
                      className={cn(
                        'shrink-0 rounded-full px-2 py-1 text-xs font-bold',
                        item.totalScore === null
                          ? 'bg-[#F4FBFD] text-[#1C4D8D]'
                          : 'bg-[#e8f8f1] text-[#16836a]',
                      )}
                    >
                      {item.totalScore === null ? '—' : `${item.totalScore.toFixed(2)}/10`}
                    </span>
                  </div>
                  <div className="flex items-center justify-between gap-3 text-xs text-[#5b6b84]">
                    <span>
                      {formatExamDate(item.startedAt)} · {item.durationMinutes} phút
                    </span>
                    {renderHistoryAction(item)}
                  </div>
                </article>
              ))}
            </div>
          </>
        )}

      </section>

      {archiveOpen && (
        <div
          className="fixed inset-0 z-[80] flex items-end justify-center bg-[#0F2854]/45 px-4 py-6 sm:items-center"
          role="presentation"
          onClick={(event) => {
            if (event.target === event.currentTarget) setArchiveOpen(false)
          }}
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="mock-history-title"
            className="flex max-h-[90dvh] w-full max-w-4xl flex-col overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-white shadow-[0_24px_60px_rgba(16,32,61,0.22)]"
          >
            <div className="relative z-20 border-b border-[#4988C4]/45 bg-[linear-gradient(180deg,#f7f9ff_0%,#ffffff_100%)] px-5 py-4">
              <div className="flex items-center justify-between gap-3">
                <div className="flex min-w-0 items-center gap-3">
                  <span className="flex size-11 shrink-0 items-center justify-center rounded-2xl bg-[#1C4D8D] text-white shadow-[0_8px_16px_rgba(36,94,234,0.28)]">
                    <History className="size-5" />
                  </span>
                  <div className="min-w-0">
                    <h2 id="mock-history-title" className="text-xl font-bold tracking-[-0.02em] text-[#0F2854]">
                      Lịch sử thi thử
                    </h2>
                    <p className="mt-0.5 text-sm text-[#5b6b84]">{archiveTotal} phiên</p>
                  </div>
                </div>
                <button
                  type="button"
                  aria-label="Đóng"
                  onClick={() => setArchiveOpen(false)}
                  className="flex size-9 shrink-0 items-center justify-center rounded-full border border-[#4988C4]/45 bg-white text-[#5b6b84] hover:bg-[#4988C4]/18"
                >
                  <X className="size-4" />
                </button>
              </div>
              <div className="mt-4 flex flex-wrap items-center gap-2">
                <FilterDropMenu
                  label="Lọc theo môn học"
                  value={archiveCourseId}
                  onChange={(nextValue) => {
                    setArchiveCourseId(nextValue)
                    setArchivePage(1)
                  }}
                  options={[
                    { value: '', label: 'Tất cả môn' },
                    ...courses.map((course) => ({
                      value: course.id,
                      label: `${course.code} · ${course.name}`,
                    })),
                  ]}
                  className="min-w-52 flex-1 sm:max-w-xs sm:flex-none"
                />
                <FilterDropMenu
                  label="Lọc theo trạng thái"
                  value={archiveStatus}
                  onChange={(nextValue) => {
                    setArchiveStatus(nextValue)
                    setArchivePage(1)
                  }}
                  options={HISTORY_STATUS_OPTIONS}
                  className="min-w-44"
                />
              </div>
            </div>
            <div className="min-h-0 flex-1 overflow-y-auto px-3 py-3 sm:px-4">
              {archiveError ? (
                <p className="rounded-xl bg-white px-4 py-8 text-sm text-[#ae3d50]">{archiveError}</p>
              ) : isLoadingArchive ? (
                <div className="flex items-center gap-2 rounded-xl bg-white px-4 py-8 text-sm font-semibold text-[#5b6b84]">
                  <LoaderCircle className="size-4 animate-spin text-[#1C4D8D]" />
                  Đang tải lịch sử…
                </div>
              ) : archiveItems.length === 0 ? (
                <p className="rounded-xl bg-white px-4 py-8 text-sm text-[#5b6b84]">
                  Không có phiên thi thử phù hợp.
                </p>
              ) : (
                <div className="overflow-x-auto rounded-2xl border border-[#4988C4]/45 bg-white">
                  <div className="min-w-[720px]">
                    <div className="grid grid-cols-[minmax(0,1fr)_90px_70px_85px_130px] items-center gap-3 border-b border-[#4988C4]/45 bg-white px-4 py-3 text-xs font-bold text-[#5b6b84]">
                      <span>Môn thi</span>
                      <span>Thời điểm</span>
                      <span>Trạng thái</span>
                      <span>Điểm</span>
                      <span>Thao tác</span>
                    </div>
                    <ul className="divide-y divide-[#eef2f7]">
                      {archiveItems.map((item) => {
                        const when = formatExamDayTime(item.startedAt)
                        return (
                          <li
                            key={item.sessionId}
                            className="grid grid-cols-[minmax(0,1fr)_90px_70px_85px_130px] items-center gap-3 px-4 py-3 text-sm text-[#5b6b84]"
                          >
                            <div className="min-w-0">
                              <p className="truncate font-bold text-[#0F2854]">{item.courseName}</p>
                              <p className="mt-0.5 truncate text-xs text-[#5b6b84]">
                                {item.courseCode} · {item.answeredQuestions}/{item.totalQuestions} câu
                              </p>
                            </div>
                            <div>
                              <p className="font-bold text-[#0F2854]">{when.day}</p>
                              <p className="mt-0.5 text-xs text-[#5b6b84]">{when.time}</p>
                            </div>
                            <span
                              className={cn(
                                'inline-flex w-fit rounded-full px-2.5 py-1 text-xs font-bold',
                                statusChipClass(item.status),
                              )}
                            >
                              {statusLabel(item.status)}
                            </span>
                            <span>
                              {item.totalScore === null ? (
                                '—'
                              ) : (
                                <span className="inline-flex rounded-full bg-[#e8f8f1] px-2.5 py-1 font-bold text-[#16836a]">
                                  {item.totalScore.toFixed(2)} / 10
                                </span>
                              )}
                            </span>
                            <span>{renderHistoryAction(item)}</span>
                          </li>
                        )
                      })}
                    </ul>
                  </div>
                </div>
              )}
            </div>
            {archiveTotalPages > 1 && (
              <div className="flex items-center justify-between border-t border-[#4988C4]/45 bg-white px-5 py-3">
                <button
                  type="button"
                  disabled={archivePage <= 1}
                  onClick={() => setArchivePage((current) => Math.max(1, current - 1))}
                  className="inline-flex min-h-9 items-center gap-1 rounded-lg px-2 text-sm font-bold text-[#1C4D8D] disabled:opacity-40"
                >
                  <ChevronLeft className="size-4" /> Trước
                </button>
                <span className="text-sm font-semibold text-[#6d7c90]">
                  Trang {archivePage}/{archiveTotalPages}
                </span>
                <button
                  type="button"
                  disabled={archivePage >= archiveTotalPages}
                  onClick={() => setArchivePage((current) => current + 1)}
                  className="inline-flex min-h-9 items-center gap-1 rounded-lg px-2 text-sm font-bold text-[#1C4D8D] disabled:opacity-40"
                >
                  Sau <ChevronDown className="size-4 -rotate-90" />
                </button>
              </div>
            )}
          </div>
        </div>
      )}

      {expandedHistoryId && (
        <div
          className="fixed inset-0 z-[90] flex items-end justify-center bg-[#0F2854]/50 px-4 py-6 sm:items-center"
          role="presentation"
          onClick={(event) => {
            if (event.target === event.currentTarget) setExpandedHistoryId(null)
          }}
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="mock-scorecard-title"
            className="flex max-h-[90dvh] w-full max-w-3xl flex-col overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-white shadow-[0_24px_60px_rgba(16,32,61,0.28)]"
          >
            <div className="flex items-center justify-between gap-3 border-b border-[#4988C4]/45 bg-white px-5 py-4">
              <h2 id="mock-scorecard-title" className="text-lg font-bold text-[#0F2854]">
                Bảng điểm
              </h2>
              <button
                type="button"
                aria-label="Đóng bảng điểm"
                onClick={() => setExpandedHistoryId(null)}
                className="flex size-9 shrink-0 items-center justify-center rounded-full border border-[#4988C4]/45 text-[#5b6b84] hover:bg-[#4988C4]/18"
              >
                <X className="size-4" />
              </button>
            </div>
            <div className="min-h-0 flex-1 overflow-y-auto p-4 sm:p-5">
              {loadingDetailId === expandedHistoryId && (
                <div className="flex items-center gap-2 text-sm font-semibold text-[#5b6b84]">
                  <LoaderCircle className="size-4 animate-spin text-[#1C4D8D]" />
                  Đang mở bảng điểm…
                </div>
              )}
              {detailError && <p className="text-sm text-[#ae3d50]">{detailError}</p>}
              {selectedDetail && <MockExamScorecard scorecard={selectedDetail} />}
            </div>
          </div>
        </div>
      )}

      {modalCourse && (
        <div
          className="fixed inset-0 z-[80] flex items-end justify-center bg-[#0F2854]/45 px-4 py-6 sm:items-center"
          role="presentation"
          onClick={(event) => {
            if (event.target === event.currentTarget) closeCourseModal()
          }}
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="mock-course-title"
            className="flex max-h-[90dvh] w-full max-w-lg flex-col overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-white shadow-[0_1px_0_#fff_inset,0_24px_60px_rgba(16,32,61,0.22)]"
          >
            <div className="border-b border-[#4988C4]/45 px-5 py-4">
              <div className="flex items-center justify-between gap-3">
                <span className="inline-flex rounded-full bg-[#4988C4]/16 px-3 py-1.5 text-xs font-bold text-[#1C4D8D]">
                  {modalCourse.code}
                </span>
                <button
                  type="button"
                  aria-label="Đóng"
                  onClick={closeCourseModal}
                  className="flex size-9 shrink-0 items-center justify-center rounded-full border border-[#4988C4]/45 text-[#5b6b84] hover:bg-[#4988C4]/18"
                >
                  <X className="size-4" />
                </button>
              </div>
              <div className="mt-2 flex items-start justify-between gap-4">
                <h2
                  id="mock-course-title"
                  className="min-w-0 text-[20px] leading-snug font-bold text-[#0F2854] sm:text-lg"
                >
                  {modalCourse.name}{' - '}
                  <span className="relative -top-0.5 inline-flex items-center gap-1.5 rounded-full border border-[#4988C4]/45 bg-white px-2 py-1 align-middle text-xs font-semibold text-[#5b6b84]">
                    <GraduationCap className="size-3.5 shrink-0 text-[#1C4D8D]" />
                    {modalCourse.credits} tín chỉ
                  </span>
                </h2>
                <span
                  className={cn(
                    'relative top-0.5 inline-flex shrink-0 items-center gap-1.5 rounded-full border px-2 py-1 text-xs font-semibold',
                    modalQuota && !modalQuota.canStartExam
                      ? 'border-[#f3c2c8] bg-[#fdecec] text-[#c2415b]'
                      : modalQuota
                        ? 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]'
                        : 'border-[#4988C4]/30 bg-[#F4FBFD] text-[#1C4D8D]',
                  )}
                >
                  <TimerReset className="size-3.5 shrink-0" />
                  {modalQuota
                    ? modalQuota.canStartExam
                      ? `Còn ${modalQuota.remainingCount}/${modalQuota.maxDailyQuota} lượt`
                      : 'Hết lượt'
                    : quotaFailedIds.includes(modalCourse.id)
                      ? 'Chưa rõ lượt'
                      : 'Đang kiểm tra lượt…'}
                </span>
              </div>
            </div>

            <div className="space-y-4 overflow-y-auto bg-white p-4 sm:p-5">
              {modalQuota && !modalQuota.canStartExam && (
                <p className="rounded-xl border border-[#f3c9d0] bg-[#fff7f8] px-3 py-2 text-sm leading-relaxed text-[#b4233c]">
                  Môn này đã hết lượt thi thử hôm nay. Quay lại vào ngày mai.
                </p>
              )}

              <fieldset>
                <legend className="text-sm font-bold text-[#0F2854]">Hình thức thi</legend>
                <div className="mt-2 grid gap-3 sm:grid-cols-2">
                  <button
                    type="button"
                    onClick={() => setHasFollowUp(false)}
                    className={cn(
                      'rounded-2xl border bg-white px-3.5 py-3 text-left shadow-[0_8px_22px_rgba(23,48,96,0.06)]',
                      !hasFollowUp ? 'border-[#4988C4]' : 'border-[#4988C4]/45 hover:border-[#b7c6ef]',
                    )}
                    aria-pressed={!hasFollowUp}
                  >
                    <span className="flex items-center gap-2">
                      <span
                        className={cn(
                          'size-3.5 rounded-full border-2',
                          !hasFollowUp ? 'border-[#4988C4] bg-[#1C4D8D]' : 'border-[#4988C4]/45',
                        )}
                      />
                      <span className="text-sm font-bold text-[#0F2854]">Không câu hỏi phụ</span>
                    </span>
                  </button>
                  <button
                    type="button"
                    onClick={() => setHasFollowUp(true)}
                    className={cn(
                      'rounded-2xl border bg-white px-3.5 py-3 text-left shadow-[0_8px_22px_rgba(23,48,96,0.06)]',
                      hasFollowUp ? 'border-[#4988C4]' : 'border-[#4988C4]/45 hover:border-[#b7c6ef]',
                    )}
                    aria-pressed={hasFollowUp}
                  >
                    <span className="flex items-center gap-2">
                      <span
                        className={cn(
                          'size-3.5 rounded-full border-2',
                          hasFollowUp ? 'border-[#4988C4] bg-[#1C4D8D]' : 'border-[#4988C4]/45',
                        )}
                      />
                      <span className="text-sm font-bold text-[#0F2854]">Có câu hỏi phụ</span>
                    </span>
                  </button>
                </div>
                <p className="mt-3 rounded-2xl border border-[#4988C4]/45 bg-white px-4 py-3 text-sm leading-relaxed text-[#5b6b84] shadow-[0_8px_22px_rgba(23,48,96,0.06)]">
                  {hasFollowUp
                    ? 'Sau câu trả lời có thể có thêm câu hỏi phụ để làm rõ ý. Thời gian làm bài là 45 phút.'
                    : 'Chỉ làm các câu chính trong đề. Thời gian theo cấu hình môn, thường 30 phút.'}
                </p>
              </fieldset>

              <div
                className={cn(
                  'rounded-2xl border p-3.5',
                  deviceState === 'ready'
                    ? 'border-[#b7e4cf] bg-[#e8f8f1]'
                    : deviceState === 'error'
                      ? 'border-[#f3c2c8] bg-[#fdecec]'
                      : 'border-[#4988C4]/30 bg-[#F4FBFD]',
                )}
              >
                <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <div className="min-w-0">
                    <p
                      className={cn('flex items-center gap-2 text-sm font-bold', deviceStatus.color)}
                    >
                      <span className={cn('size-2 shrink-0 rounded-full', deviceStatus.dot)} />
                      {deviceState === 'error' && deviceIssue === 'silent'
                        ? 'Chưa nghe thấy giọng nói'
                        : deviceStatus.title}
                    </p>
                    <p className="mt-1 text-xs leading-relaxed text-[#5b6b84]">
                      {deviceState === 'error' && deviceIssue === 'silent'
                        ? 'Hãy nói gần micro một câu ngắn rồi thử lại.'
                        : deviceState === 'ready' && deviceNames
                          ? `${deviceNames}. ${deviceStatus.helper}`
                          : deviceStatus.helper}
                    </p>
                  </div>
                  <button
                    type="button"
                    onClick={() => void handleDeviceCheck()}
                    disabled={deviceState === 'checking' || deviceState === 'testing'}
                    className="inline-flex min-h-10 shrink-0 items-center justify-center gap-1.5 rounded-xl border border-[#4988C4]/45 bg-white px-3 text-xs font-bold text-[#0F2854] shadow-[0_3px_8px_rgba(23,48,96,0.05)] hover:border-[#4988C4]/45 disabled:cursor-wait disabled:opacity-60"
                  >
                    {deviceState === 'checking' || deviceState === 'testing' ? (
                      <LoaderCircle className="size-3.5 animate-spin" />
                    ) : (
                      <Mic2 className="size-3.5" />
                    )}
                    {deviceState === 'testing'
                      ? 'Đang nghe…'
                      : deviceState === 'ready'
                        ? 'Kiểm tra lại'
                        : 'Kiểm tra micro'}
                  </button>
                </div>
                {deviceState === 'testing' && (
                  <div
                    className="mt-3 flex h-8 items-end gap-1"
                    aria-hidden="true"
                  >
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
              </div>

              {pageError && (
                <p className="rounded-xl border border-[#f3c9d0] bg-[#fff7f8] px-3 py-2 text-sm leading-relaxed text-[#b4233c]">
                  {pageError}
                </p>
              )}
            </div>

            <div className="border-t border-[#4988C4]/45 bg-white px-4 py-4 sm:px-5">
              <button
                type="button"
                onClick={() => void handleStartExam()}
                disabled={!canStart}
                className="inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-6 text-sm font-bold text-white shadow-[0_10px_22px_rgba(36,94,234,0.28)] disabled:opacity-45"
              >
                {isStarting ? (
                  <LoaderCircle className="size-4 animate-spin" />
                ) : (
                  <Play className="size-4 fill-current" />
                )}
                {isStarting
                  ? 'Đang tạo đề thi…'
                  : deviceState === 'ready'
                    ? 'Bắt đầu thi thử'
                    : 'Kiểm tra micro để tiếp tục'}
              </button>
            </div>
          </div>
        </div>
      )}

      {quotaModal && (
        <ConfirmDialog
          tone="danger"
          eyebrow="Hạn ngạch"
          title="Hết lượt thi thử hôm nay"
          message={quotaModal}
          confirmLabel="Đã hiểu"
          onConfirm={() => setQuotaModal('')}
        />
      )}
      {confirmReplace && (
        <ConfirmDialog
          tone="warning"
          title="Ca thi trước vẫn đang mở"
          message="Bắt đầu ca mới sẽ dùng thêm một lượt hạn ngạch. Ca chưa nộp không được khôi phục sau khi bạn vào đề mới."
          confirmLabel="Bắt đầu ca mới"
          cancelLabel="Ở lại"
          pending={isStarting}
          onCancel={() => setConfirmReplace(false)}
          onConfirm={() => void startExam()}
        />
      )}

      <div className="sr-only" aria-live="polite">
        {deviceStatus.title}. {deviceNames || deviceStatus.helper}
      </div>
    </div>
  )
}
