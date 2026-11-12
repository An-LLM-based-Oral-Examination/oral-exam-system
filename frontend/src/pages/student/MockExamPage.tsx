import { lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  AlertTriangle,
  ArrowRight,
  Award,
  Bot,
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
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { EmptyState } from '@/components/common/EmptyState'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { toast } from '@/components/common/toastStore'
import { studentCardShellClass } from '@/design/studentCardTones'
import { BannerFact, PageBanner, StudentColorCardAccent } from '@/design/ui'
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
  listMockExamSessions,
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


function courseToneIndex(code: string) {
  let hash = 0
  for (let index = 0; index < code.length; index += 1) {
    hash = (hash * 31 + code.charCodeAt(index)) >>> 0
  }
  return hash
}

function courseCodeTone(code: string) {
  return COURSE_CODE_TONES[courseToneIndex(code) % COURSE_CODE_TONES.length]
}

const EXAM_STEPS = [
  {
    title: 'Chọn môn còn lượt',
    hint: 'Bấm Bắt đầu, chọn hình thức thi.',
    icon: Play,
    card: 'border-[#8eb7e0]/55 bg-[#dfeff9]',
    badge: 'bg-[#1C4D8D] text-white',
    iconClass: 'text-[#1C4D8D]',
  },
  {
    title: 'Kiểm tra micro',
    hint: 'Nói thử một câu trước khi vào.',
    icon: Mic2,
    card: 'border-[#95d6b9]/55 bg-[#dff5eb]',
    badge: 'bg-[#16836a] text-white',
    iconClass: 'text-[#16836a]',
  },
  {
    title: 'Nhấn để ghi âm',
    hint: 'Nhấn micro hoặc Space để bật tắt.',
    icon: Keyboard,
    card: 'border-[#efb5d3]/55 bg-[#fee3ee]',
    badge: 'bg-[#e11d74] text-white',
    iconClass: 'text-[#9d174d]',
  },
  {
    title: 'Sửa lời rồi sang câu',
    hint: 'Sửa lời, rồi sang câu hoặc nộp.',
    icon: PenLine,
    card: 'border-[#c7b4ef]/55 bg-[#ebe4fc]',
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

function normalizeHistoryStatus(status: string) {
  return status.trim().toLowerCase()
}

function statusLabel(status: string) {
  const normalized = normalizeHistoryStatus(status)
  return HISTORY_STATUS_OPTIONS.find((option) => option.value === normalized)?.label ?? status
}

function statusChipClass(status: string) {
  const normalized = normalizeHistoryStatus(status)
  if (normalized === 'graded') return 'border border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]'
  if (normalized === 'submitted') return 'border border-[#f3ddb0] bg-[#fff8eb] text-[#9a6414]'
  if (normalized === 'abandoned') return 'border border-[#f3c2c8] bg-[#fdecec] text-[#c2415b]'
  if (normalized === 'in_progress') return 'border border-[#82aedb]/55 bg-[#F4FBFD] text-[#1C4D8D]'
  return 'border border-[#82aedb]/55 bg-[#F4FBFD] text-[#1C4D8D]'
}

/** Ca chưa nộp — có thể vào phòng thi tiếp. */
function canResumeHistoryItem(status: string) {
  return normalizeHistoryStatus(status) === 'in_progress'
}

/** Chỉ ca đã chấm mới xem bảng điểm. */
function canViewScorecard(status: string) {
  return normalizeHistoryStatus(status) === 'graded'
}

export function MockExamPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
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
  const [quotaModal, setQuotaModal] = useState('')
  const [guideOpen, setGuideOpen] = useState(true)
  const [coursesExpanded, setCoursesExpanded] = useState(false)
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
  const [loadingDetailId, setLoadingDetailId] = useState<string | null>(null)
  const [activeSessionVersion, setActiveSessionVersion] = useState(0)

  const localSessions = useMemo(() => {
    void activeSessionVersion
    return listMockExamSessions()
  }, [activeSessionVersion])
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
    try {
      const nextCourses = await courseService.getEnrolledCourses()
      setCourses(nextCourses)
      const requestedCourse = searchParams.get('course')?.trim().toLowerCase()
      const requestedCourseMatch = requestedCourse
        ? nextCourses.find(
            (course) =>
              course.id.toLowerCase() === requestedCourse ||
              course.code.trim().toLowerCase() === requestedCourse,
          )
        : undefined
      setOpenCourseId(requestedCourseMatch?.id ?? null)
      void loadQuotas(nextCourses.map((course) => course.id))
    } catch (error) {
      toast.error(getMockExamErrorDetail(error, 'Không thể tải danh sách môn học.'))
    } finally {
      setIsLoadingCourses(false)
    }
  }, [loadQuotas, searchParams])

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
    setExpandedHistoryId(null)
  }

  const startExam = async () => {
    if (!modalCourse || !modalQuota?.canStartExam || deviceState !== 'ready') return
    setIsStarting(true)
    try {
      const session = await mockExamService.startSession(modalCourse.id, hasFollowUp)
      saveMockExamSession({ ...session, questions: session.questions, answers: [] })
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
      toast.error(getMockExamErrorDetail(error, 'Không thể bắt đầu ca thi thử.'))
    } finally {
      setIsStarting(false)
    }
  }

  const handleStartExam = () => {
    if (!modalCourse || !modalQuota?.canStartExam || deviceState !== 'ready') return
    void startExam()
  }

  const resumeSession = (sessionId: string) => {
    setActiveSessionVersion((current) => current + 1)
    navigate(`/mock-exam/session?session=${encodeURIComponent(sessionId)}`)
  }

  const handleToggleHistory = async (item: MockExamHistoryItem) => {
    if (canResumeHistoryItem(item.status)) {
      resumeSession(item.sessionId)
      return
    }

    if (!canViewScorecard(item.status)) {
      setExpandedHistoryId(null)
      return
    }

    if (expandedHistoryId === item.sessionId) {
      setExpandedHistoryId(null)
      return
    }

    setExpandedHistoryId(item.sessionId)
    if (historyDetails[item.sessionId]) return

    setLoadingDetailId(item.sessionId)
    try {
      const detail = await mockExamService.getHistoryDetail(item.sessionId)
      setHistoryDetails((current) => ({ ...current, [item.sessionId]: detail }))
    } catch (error) {
      toast.error(getMockExamErrorDetail(error, 'Không thể mở bảng điểm của ca thi này.'))
      setExpandedHistoryId(null)
    } finally {
      setLoadingDetailId(null)
    }
  }

  const renderHistoryAction = (item: MockExamHistoryItem) => {
    const isOpen = expandedHistoryId === item.sessionId
    const canResume = canResumeHistoryItem(item.status)
    const canView = canViewScorecard(item.status)
    const status = normalizeHistoryStatus(item.status)

    if (canResume) {
      return (
        <button
          type="button"
          onClick={() => void handleToggleHistory(item)}
          className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#4988C4] bg-white px-3 text-xs font-semibold text-[#0F2854] shadow-[0_3px_8px_rgba(23,48,96,0.05)] transition hover:border-[#4988C4] hover:text-[#1C4D8D] focus-visible:outline-2 focus-visible:outline-[#1C4D8D]"
        >
          <ArrowRight className="size-3" />
          Tiếp tục ca thi
        </button>
      )
    }

    if (canView) {
      return (
        <button
          type="button"
          onClick={() => void handleToggleHistory(item)}
          className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#4988C4] bg-white px-3 text-xs font-semibold text-[#0F2854] shadow-[0_3px_8px_rgba(23,48,96,0.05)] transition hover:border-[#4988C4] hover:text-[#1C4D8D] focus-visible:outline-2 focus-visible:outline-[#1C4D8D]"
          aria-expanded={isOpen}
        >
          <Eye className="size-3" />
          {isOpen ? 'Đóng bảng điểm' : 'Xem bảng điểm'}
        </button>
      )
    }

    return (
      <span className="inline-flex min-h-9 items-center rounded-lg border border-[#82aedb]/55 bg-[#f7f9fc] px-3 text-xs font-semibold text-[#74839a]">
        {status === 'submitted' ? 'Đang chấm điểm' : status === 'abandoned' ? 'Không có bảng điểm' : 'Chưa có bảng điểm'}
      </span>
    )
  }

  const selectedDetail = expandedHistoryId ? historyDetails[expandedHistoryId] : undefined

  return (
    <div className={ds.page}>
      <PageBanner
        icon={ShieldCheck}
        eyebrow="Thi thử · Không tính điểm"
        title="Thi thử Vấn đáp Giả lập"
        description="Làm bài có đồng hồ máy chủ, trả lời bằng giọng nói và nhận bảng điểm theo rubric ngay khi nộp."
        extras={
          <>
            <BannerFact icon={TimerReset}>Hết giờ thì bài tự nộp</BannerFact>
            <BannerFact icon={Mic2} tone="green">
              Nhấn micro hoặc Space để nói
            </BannerFact>
            <BannerFact icon={Bot} tone="violet">
              Bảng điểm ngay khi nộp
            </BannerFact>
          </>
        }
        scene={
          <Suspense
            fallback={
              <div className="flex size-full items-center justify-center" aria-hidden="true">
                <div className="size-20 animate-pulse rounded-full border border-[#82aedb]/55 bg-[#4988C4]/10 motion-reduce:animate-none" />
              </div>
            }
          >
            <MockExamHeroThreeScene
              scanning={deviceState === 'checking' || deviceState === 'testing'}
              ready={deviceState === 'ready'}
            />
          </Suspense>
        }
      />

      {localSessions.length > 0 && (
        <section className="overflow-hidden rounded-2xl border border-[#f3e2c4] bg-white shadow-[0_1px_0_#fff_inset,0_8px_14px_rgba(176,106,18,0.18)]">
          <div className="border-l-4 border-[#e07a12] px-5 py-4">
            <p className="text-sm font-bold text-[#0F2854]">
              Ca thi chưa hoàn thành ({localSessions.length})
            </p>
            <p className="mt-0.5 text-xs text-[#5b6b84]">
              Tiếp tục sẽ vào đúng câu đã làm, không tạo đề mới.
            </p>
            <ul className="mt-3 space-y-2">
              {localSessions.map((item) => {
                const secondsLeft = secondsUntilExpiry(item.expiresAt)
                const answered = item.answers.filter((answer) => !answer.isFollowUp).length
                return (
                  <li
                    key={item.sessionId}
                    className="flex flex-col gap-3 rounded-xl border border-[#f0d7a8] bg-[#fffaf3] px-3 py-3 sm:flex-row sm:items-center sm:justify-between"
                  >
                    <div className="min-w-0">
                      <p className="text-xs font-bold tracking-[0.06em] text-[#b45309]">
                        {item.courseCode}
                      </p>
                      <p className="mt-0.5 text-sm font-semibold text-[#0F2854]">{item.courseName}</p>
                      <p className="mt-0.5 text-xs text-[#5b6b84]">
                        {secondsLeft > 0
                          ? `Còn ${formatClock(secondsLeft)} · Đã trả lời ${answered}/${item.questions.length}`
                          : `Đã hết giờ · Đã trả lời ${answered}/${item.questions.length}`}
                      </p>
                    </div>
                    <button
                      type="button"
                      onClick={() => resumeSession(item.sessionId)}
                      className={ds.outlineButton}
                    >
                      Tiếp tục
                      <ArrowRight className="size-4" />
                    </button>
                  </li>
                )
              })}
            </ul>
          </div>
        </section>
      )}

      <section className={ds.section}>
        <div className={ds.sectionHeader}>
          <div className={ds.sectionHeading}>
            <div className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}>
                <GraduationCap className={ds.sectionIcon} aria-hidden="true" />
              </span>
              <h2 className={ds.sectionTitle}>Môn học của tôi</h2>
            </div>
            <p className={ds.sectionHint}>Chọn môn để bắt đầu phiên thi thử</p>
          </div>
        </div>

        <div className={cn(ds.sectionBody, 'space-y-4')}>
          {isLoadingCourses ? (
            <div className="flex items-center gap-2 text-sm font-semibold text-[#5b6b84]">
              <LoaderCircle className="size-4 animate-spin text-[#1C4D8D]" />
              Đang tải môn học…
            </div>
          ) : courses.length === 0 ? (
            <EmptyState
              icon={GraduationCap}
              title="Chưa có môn học đang mở"
              description="Tham gia lớp học phần trước để thấy môn đủ điều kiện thi thử."
            />
          ) : (
            <>
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                {courses.map((course, index) => {
                  const courseQuota = quotas[course.id]
                  const exhausted = Boolean(courseQuota && !courseQuota.canStartExam)
                  const quotaUnknown = !isLoadingQuotas && quotaFailedIds.includes(course.id)
                  return (
                    <article
                      key={course.id}
                      className={cn(
                        !coursesExpanded && index >= 6 && 'hidden',
                        !coursesExpanded && index >= 4 && 'max-lg:hidden',
                        !coursesExpanded && index >= 2 && 'max-sm:hidden',
                        exhausted
                          ? 'relative flex h-full flex-col overflow-hidden rounded-[22px] border border-[#82aedb]/55 bg-[#f7f8fa] p-3.5'
                          : studentCardShellClass(course.code),
                      )}
                    >
                      {!exhausted && <StudentColorCardAccent seed={course.code} />}
                      <div className={exhausted ? 'flex h-full flex-1 flex-col' : 'relative flex h-full flex-1 flex-col p-3.5'}>
                        <div className="flex items-center justify-between gap-2">
                          <div className="flex min-w-0 flex-wrap items-center gap-1.5">
                            <span
                              className={cn(
                                'inline-flex h-6 items-center rounded-md px-2 text-xs font-bold tracking-[0.04em]',
                                exhausted ? 'border border-[#8b97a8]/30 bg-[#e8edf3] text-[#8b97a8]' : courseCodeTone(course.code),
                              )}
                            >
                              {course.code}
                            </span>
                            <span className="inline-flex h-6 items-center gap-1 rounded-full border border-[#4988C4]/55 bg-white/85 px-2 text-xs font-semibold text-[#5b6b84] backdrop-blur-[2px]">
                              <GraduationCap className="size-3 shrink-0 text-[#1C4D8D]" />
                              {course.credits} tín chỉ
                            </span>
                          </div>
                          <span
                            className={cn(
                              'inline-flex h-6 shrink-0 items-center gap-1 rounded-full border px-2 text-xs font-semibold',
                              exhausted
                                ? 'border-[#f3c2c8] bg-[#fdecec] text-[#c2415b]'
                                : courseQuota
                                  ? 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]'
                                  : 'border-[#82aedb]/55 bg-[#F4FBFD] text-[#1C4D8D]',
                            )}
                          >
                            <TimerReset className="size-3 shrink-0" />
                            {courseQuota
                              ? exhausted
                                ? 'Hết lượt'
                                : `${courseQuota.remainingCount}/${courseQuota.maxDailyQuota}`
                              : quotaUnknown
                                ? 'Chưa rõ'
                                : '…'}
                          </span>
                        </div>
                        <h3
                          className={cn(
                            'mt-3 truncate text-xl leading-snug font-bold',
                            exhausted ? 'text-[#6d7888]' : 'text-[#0F2854]',
                          )}
                        >
                          {course.name}
                        </h3>
                        {exhausted ? (
                          <p className="mt-auto pt-3 text-xs font-semibold text-[#8b97a8]">
                            Hết lượt · quay lại ngày mai
                          </p>
                        ) : (
                          <div className="mt-auto pt-3">
                            <button
                              type="button"
                              onClick={() => openCourse(course.id)}
                              className={ds.outlineButton}
                            >
                              <Play className="size-3.5 fill-current" />
                              Bắt đầu
                            </button>
                          </div>
                        )}
                      </div>
                    </article>
                  )
                })}
              </div>
              {courses.length > 2 && (
                <div
                  className={cn(
                    'flex justify-center',
                    courses.length <= 4 && 'sm:hidden',
                    courses.length <= 6 && 'lg:hidden',
                  )}
                >
                  <button
                    type="button"
                    onClick={() => setCoursesExpanded((open) => !open)}
                    aria-expanded={coursesExpanded}
                    className="inline-flex items-center gap-1 text-sm font-bold text-[#1C4D8D] underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
                  >
                    {coursesExpanded ? 'Thu gọn' : 'Xem thêm'}
                    <ChevronDown
                      className={cn('size-4 transition', coursesExpanded && 'rotate-180')}
                      aria-hidden="true"
                    />
                  </button>
                </div>
              )}
            </>
          )}
        </div>
      </section>

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
          <span className={ds.sectionHeading}>
            <span className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}>
                <ListChecks className={ds.sectionIcon} aria-hidden="true" />
              </span>
              <h2 className={ds.sectionTitle}>Hướng dẫn thi thử</h2>
            </span>
            <span className={cn(ds.sectionHint, 'block')}>
              Bốn bước từ chọn môn đến khi nộp bài
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
                      'flex gap-3 rounded-2xl border p-3.5 shadow-[0_1px_0_#fff_inset,0_8px_18px_rgba(23,48,96,0.14)]',
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
            <div className="mt-4 rounded-2xl border border-[#f0b56a] bg-[#fff6e8] p-4 shadow-[0_1px_0_#fff_inset,0_8px_18px_rgba(180,83,9,0.12)]">
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
        <div className={cn(ds.sectionHeader, 'flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between')}>
          <div className={ds.sectionHeading}>
            <div className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}>
                <History className={ds.sectionIcon} aria-hidden="true" />
              </span>
              <h2 className={ds.sectionTitle}>Lịch sử</h2>
              <span className={ds.sectionBadge}>
                {historyTotal > RECENT_HISTORY_SIZE
                  ? `${RECENT_HISTORY_SIZE}/${historyTotal}`
                  : `${historyTotal} phiên`}
              </span>
            </div>
            <p className={ds.sectionHint}>
              {historyTotal > RECENT_HISTORY_SIZE
                ? `${RECENT_HISTORY_SIZE} phiên gần nhất trong ${historyTotal} lượt`
                : 'Các phiên thi thử gần nhất'}
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
          <div className="p-4 sm:p-5">
            <EmptyState
              icon={History}
              title="Chưa có ca thi thử nào"
              description="Bắt đầu phiên thi thử đầu tiên để xem lịch sử và bảng điểm tại đây."
            />
          </div>
        ) : (
          <>
            <div className="hidden md:block">
              <table className="w-full table-fixed border-collapse text-left">
                <thead className="border-b border-[#4988C4] bg-white text-xs font-bold text-[#5b6b84]">
                  <tr>
                    <th className="w-[40%] px-5 py-3">Môn thi</th>
                    <th className="w-[16%] px-3 py-3">Thời điểm</th>
                    <th className="w-[13%] px-3 py-3">Trạng thái</th>
                    <th className="w-[13%] px-3 py-3">Điểm</th>
                    <th className="w-[18%] px-3 py-3">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#4988C4]/30">
                  {historyItems.map((item) => (
                    <tr key={item.sessionId} className="text-sm text-[#5b6b84]">
                      <td className="px-5 py-3">
                        <p className="truncate font-bold text-[#0F2854]">{item.courseName}</p>
                        <p className="mt-0.5 truncate text-xs text-[#5b6b84]">
                          {item.courseCode} · {item.answeredQuestions}/{item.totalQuestions} câu
                        </p>
                      </td>
                      <td className="px-3 py-3 whitespace-nowrap">{formatExamDate(item.startedAt)}</td>
                      <td className="px-3 py-3">
                        <span
                          className={cn(
                            'inline-flex whitespace-nowrap rounded-full px-2.5 py-1 text-xs font-bold',
                            statusChipClass(item.status),
                          )}
                        >
                          {statusLabel(item.status)}
                        </span>
                      </td>
                      <td className="px-3 py-3 whitespace-nowrap">
                        {item.totalScore === null ? (
                          '—'
                        ) : (
                          <span className="inline-flex items-center gap-1 rounded-full border border-[#b7e4cf] bg-[#e8f8f1] px-2 py-1 font-bold text-[#16836a]">
                            {item.totalScore.toFixed(2)} / 10
                          </span>
                        )}
                      </td>
                      <td className="px-3 py-3 whitespace-nowrap">{renderHistoryAction(item)}</td>
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
        <FormDialog
          titleId="mock-history-title"
          eyebrow="Thi thử"
          title="Lịch sử thi thử"
          subtitle={`${archiveTotal} phiên`}
          icon={History}
          onClose={() => setArchiveOpen(false)}
          size="2xl"
          maxHeight="max-h-[min(90dvh,820px)]"
          beforeBody={(
            <div className="flex flex-wrap items-center gap-2 border-b border-[#82aedb]/55 bg-white px-5 py-3">
              <FilterDropMenu
                label="Lọc theo môn học"
                value={archiveCourseId}
                onChange={(nextValue) => {
                  setArchiveCourseId(nextValue)
                  setArchivePage(1)
                }}
                options={
                  courses.length === 0
                    ? []
                    : [
                        { value: '', label: 'Tất cả môn' },
                        ...courses.map((course) => ({
                          value: course.id,
                          label: `${course.code} · ${course.name}`,
                        })),
                      ]
                }
                emptyText="Chưa có môn học"
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
          )}
          footer={
            archiveTotalPages > 1 ? (
              <DialogFooter className="bg-white sm:items-center sm:justify-between">
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
              </DialogFooter>
            ) : undefined
          }
        >
            <div className="min-h-0">
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
                <div className="overflow-x-auto rounded-2xl border border-[#82aedb]/55 bg-white">
                  <div className="min-w-[760px]">
                    <div className="grid grid-cols-[minmax(0,2.4fr)_124px_96px_100px_144px] items-center gap-3 border-b border-[#4988C4] bg-white px-4 py-3 text-xs font-bold text-[#5b6b84]">
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
                            className="grid grid-cols-[minmax(0,2.4fr)_124px_96px_100px_144px] items-center gap-3 px-4 py-3 text-sm text-[#5b6b84]"
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
                                'inline-flex w-fit whitespace-nowrap rounded-full px-2.5 py-1 text-xs font-bold',
                                statusChipClass(item.status),
                              )}
                            >
                              {statusLabel(item.status)}
                            </span>
                            <span className="whitespace-nowrap">
                              {item.totalScore === null ? (
                                '—'
                              ) : (
                                <span className="inline-flex rounded-full border border-[#b7e4cf] bg-[#e8f8f1] px-2.5 py-1 font-bold text-[#16836a]">
                                  {item.totalScore.toFixed(2)} / 10
                                </span>
                              )}
                            </span>
                            <span className="whitespace-nowrap">{renderHistoryAction(item)}</span>
                          </li>
                        )
                      })}
                    </ul>
                  </div>
                </div>
              )}
            </div>
        </FormDialog>
      )}

      {expandedHistoryId && (
        <FormDialog
          titleId="mock-scorecard-title"
          eyebrow="Thi thử"
          title="Bảng điểm"
          icon={Award}
          onClose={() => setExpandedHistoryId(null)}
          size="xl"
          nested
          maxHeight="max-h-[min(90dvh,820px)]"
        >
          {loadingDetailId === expandedHistoryId && (
            <div className="flex items-center gap-2 text-sm font-semibold text-[#5b6b84]">
              <LoaderCircle className="size-4 animate-spin text-[#1C4D8D]" />
              Đang mở bảng điểm…
            </div>
          )}
          {selectedDetail && <MockExamScorecard scorecard={selectedDetail} />}
        </FormDialog>
      )}

      {modalCourse && (
        <FormDialog
          titleId="mock-course-title"
          eyebrow={modalCourse.code}
          title={modalCourse.name}
          subtitle={`${modalCourse.credits} tín chỉ`}
          icon={GraduationCap}
          onClose={closeCourseModal}
          busy={isStarting}
          size="md"
          maxHeight="max-h-[min(90dvh,720px)]"
          headerExtra={(
            <span
              className={cn(
                'inline-flex max-w-[9.5rem] items-center gap-1 truncate rounded-full border px-2.5 py-1 text-[11px] font-bold sm:max-w-none',
                modalQuota && !modalQuota.canStartExam
                  ? 'border-[#f3c2c8] bg-[#fdecec] text-[#c2415b]'
                  : modalQuota
                    ? 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]'
                    : 'border-[#82aedb]/55 bg-white text-[#1C4D8D]',
              )}
            >
              <TimerReset className="size-3.5 shrink-0" />
              <span className="truncate">
                {modalQuota
                  ? modalQuota.canStartExam
                    ? `Còn ${modalQuota.remainingCount}/${modalQuota.maxDailyQuota} lượt`
                    : 'Hết lượt'
                  : quotaFailedIds.includes(modalCourse.id)
                    ? 'Chưa rõ lượt'
                    : 'Đang kiểm tra…'}
              </span>
            </span>
          )}
          footer={(
            <DialogFooter className="bg-white">
              <button
                type="button"
                onClick={() => void handleStartExam()}
                disabled={!canStart}
                className={cn(ds.primaryButton, 'w-full sm:w-auto')}
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
            </DialogFooter>
          )}
        >
            <div className="space-y-4">
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
                      !hasFollowUp ? 'border-[#82aedb]/55' : 'border-[#82aedb]/55 hover:border-[#82aedb]/55',
                    )}
                    aria-pressed={!hasFollowUp}
                  >
                    <span className="flex items-center gap-2">
                      <span
                        className={cn(
                          'size-3.5 rounded-full border-2',
                          !hasFollowUp ? 'border-[#82aedb]/55 bg-[#1C4D8D]' : 'border-[#82aedb]/55',
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
                      hasFollowUp ? 'border-[#82aedb]/55' : 'border-[#82aedb]/55 hover:border-[#82aedb]/55',
                    )}
                    aria-pressed={hasFollowUp}
                  >
                    <span className="flex items-center gap-2">
                      <span
                        className={cn(
                          'size-3.5 rounded-full border-2',
                          hasFollowUp ? 'border-[#82aedb]/55 bg-[#1C4D8D]' : 'border-[#82aedb]/55',
                        )}
                      />
                      <span className="text-sm font-bold text-[#0F2854]">Có câu hỏi phụ</span>
                    </span>
                  </button>
                </div>
                <p className="mt-3 rounded-2xl border border-[#82aedb]/55 bg-white px-4 py-3 text-sm leading-relaxed text-[#5b6b84] shadow-[0_8px_22px_rgba(23,48,96,0.06)]">
                  {hasFollowUp
                    ? 'Hệ thống AI sẽ tự động sinh câu hỏi phụ đào sâu theo ngữ cảnh câu trả lời của bạn và cộng thêm thời gian làm bài.'
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
                      : 'border-[#82aedb]/55 bg-[#F4FBFD]',
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
                    className="inline-flex min-h-10 shrink-0 items-center justify-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#0F2854] shadow-[0_3px_8px_rgba(23,48,96,0.05)] hover:-translate-y-0.5 hover:border-[#4988C4] disabled:cursor-wait disabled:opacity-60 motion-reduce:transform-none"
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
            </div>
        </FormDialog>
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
      <div className="sr-only" aria-live="polite">
        {deviceStatus.title}. {deviceNames || deviceStatus.helper}
      </div>
    </div>
  )
}
