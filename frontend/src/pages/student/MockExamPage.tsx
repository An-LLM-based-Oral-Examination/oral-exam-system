import { coloredCardTones } from '@/design/coloredCardTones'
import { MicrophoneCheckPanel } from '@/components/common/MicrophoneCheckPanel'
import { MICROPHONE_STATUS, useMicrophoneCheck } from '@/hooks/useMicrophoneCheck'
import { CompactPagination } from '@/components/common/CompactPagination'
import { lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  ArrowRight,
  Award,
  Bot,
  ChevronDown,
  Eye,
  GraduationCap,
  History,
  LoaderCircle,
  Mic2,
  Play,
  ShieldCheck,
  TimerReset,
  AlertTriangle,
  Keyboard,
  ListChecks,
  PenLine,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { EmptyState } from '@/components/common/EmptyState'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { toast } from '@/components/common/toastStore'
import { BannerFact, PageBanner } from '@/design/ui'
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

const COURSE_PAGE_SIZE = 4
const RECENT_HISTORY_SIZE = 4
const ARCHIVE_PAGE_SIZE = 5

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

const EXAM_STEPS = [
  {
    title: 'Chọn môn còn lượt',
    hint: 'Bấm Bắt đầu, chọn hình thức thi.',
    icon: Play,
    card: coloredCardTones.blue,
    badge: 'bg-[#1C4D8D] text-white',
    iconClass: 'text-[#1C4D8D]',
  },
  {
    title: 'Kiểm tra micro',
    hint: 'Nói thử một câu trước khi vào.',
    icon: Mic2,
    card: coloredCardTones.green,
    badge: 'bg-[#16836a] text-white',
    iconClass: 'text-[#16836a]',
  },
  {
    title: 'Nhấn để ghi âm',
    hint: 'Nhấn micro hoặc Space để bật tắt.',
    icon: Keyboard,
    card: coloredCardTones.rose,
    badge: 'bg-[#e11d74] text-white',
    iconClass: 'text-[#9d174d]',
  },
  {
    title: 'Sửa lời rồi sang câu',
    hint: 'Sửa lời, rồi sang câu hoặc nộp.',
    icon: PenLine,
    card: coloredCardTones.violet,
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

function courseCodeTone(code: string) {
  return COURSE_CODE_TONES[courseToneIndex(code) % COURSE_CODE_TONES.length]
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

/** Ca đã chấm hoặc đã nộp chờ chấm đều xem được chi tiết bài nộp / bảng điểm. */
function canViewScorecard(status: string) {
  const normalized = normalizeHistoryStatus(status)
  return normalized === 'graded' || normalized === 'submitted'
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
  const microphoneCheck = useMicrophoneCheck()
  const { deviceState, setDeviceState, deviceNames, setMicLevel, stopMicTest } = microphoneCheck
  const [quotaModal, setQuotaModal] = useState('')
  const [coursePage, setCoursePage] = useState(1)
  const [historyItems, setHistoryItems] = useState<MockExamHistoryItem[]>([])
  const [historyTotal, setHistoryTotal] = useState(0)
  const [guideOpen, setGuideOpen] = useState(true)
  const [archiveOpen, setArchiveOpen] = useState(false)
  const [archiveCourseId, setArchiveCourseId] = useState('')
  const [archiveStatus, setArchiveStatus] = useState('')
  const [archivePage, setArchivePage] = useState(1)
  const [archiveItems, setArchiveItems] = useState<MockExamHistoryItem[]>([])
  const [archiveTotalPages, setArchiveTotalPages] = useState(0)
  const [isLoadingArchive, setIsLoadingArchive] = useState(false)
  const [archiveError, setArchiveError] = useState('')
  const [isLoadingHistory, setIsLoadingHistory] = useState(true)
  const [historyError, setHistoryError] = useState('')
  const [expandedHistoryId, setExpandedHistoryId] = useState<string | null>(null)
  const [historyDetails, setHistoryDetails] = useState<Record<string, MockExamScorecardData>>({})
  const [loadingDetailId, setLoadingDetailId] = useState<string | null>(null)
  const [activeSessionVersion, setActiveSessionVersion] = useState(0)
  const courseSectionRef = useRef<HTMLElement | null>(null)
  const [compactSectionHeight, setCompactSectionHeight] = useState<number | null>(null)

  const localSessions = useMemo(() => {
    void activeSessionVersion
    return listMockExamSessions()
  }, [activeSessionVersion])
  const modalCourse = courses.find((course) => course.id === openCourseId) ?? null
  const coursePageCount = Math.max(1, Math.ceil(courses.length / COURSE_PAGE_SIZE))
  const currentCoursePage = Math.min(coursePage, coursePageCount)
  const visibleCourses = courses.slice(
    (currentCoursePage - 1) * COURSE_PAGE_SIZE,
    currentCoursePage * COURSE_PAGE_SIZE,
  )
  const recentHistoryLimit = courses.length < 3 ? 2 : RECENT_HISTORY_SIZE
  const visibleHistoryItems = historyItems.slice(0, recentHistoryLimit)
  const modalQuota = modalCourse ? (quotas[modalCourse.id] ?? null) : null
  const deviceStatus = MICROPHONE_STATUS[deviceState]
  const canStart = Boolean(
    modalCourse && modalQuota?.canStartExam && deviceState === 'ready' && !isStarting,
  )

  useEffect(() => {
    const courseSection = courseSectionRef.current
    if (!courseSection || courses.length >= 3) {
      setCompactSectionHeight(null)
      return
    }

    const updateHeight = () => {
      if (!window.matchMedia('(min-width: 1280px)').matches) {
        setCompactSectionHeight(null)
        return
      }
      setCompactSectionHeight(Math.round(courseSection.getBoundingClientRect().height))
    }

    updateHeight()
    const observer = new ResizeObserver(updateHeight)
    observer.observe(courseSection)
    window.addEventListener('resize', updateHeight)
    return () => {
      observer.disconnect()
      window.removeEventListener('resize', updateHeight)
    }
  }, [courses.length])

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



  const closeCourseModal = useCallback(() => {
    stopMicTest()
    setMicLevel(0)
    setDeviceState((current) =>
      current === 'checking' || current === 'testing' ? 'idle' : current,
    )
    setOpenCourseId(null)
  }, [setDeviceState, setMicLevel, stopMicTest])

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
    const cachedDetail = historyDetails[item.sessionId]
    if (cachedDetail && !cachedDetail.isGradingPending) return

    setLoadingDetailId(item.sessionId)
    try {
      const detail = await mockExamService.getHistoryDetail(item.sessionId)
      setHistoryDetails((current) => ({ ...current, [item.sessionId]: detail }))
      if (!detail.isGradingPending) {
        setHistoryItems((current) =>
          current.map((entry) =>
            entry.sessionId === item.sessionId
              ? { ...entry, status: 'graded', totalScore: detail.totalScore }
              : entry,
          ),
        )
        setArchiveItems((current) =>
          current.map((entry) =>
            entry.sessionId === item.sessionId
              ? { ...entry, status: 'graded', totalScore: detail.totalScore }
              : entry,
          ),
        )
      }
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
          className={cn(ds.outlineButton, 'h-9 min-h-9 rounded-lg px-3 text-xs')}
        >
          <ArrowRight className="size-3" />
          Tiếp tục
        </button>
      )
    }

    if (canView) {
      const isSubmittedPending = status === 'submitted'
      return (
        <button
          type="button"
          onClick={() => void handleToggleHistory(item)}
          className={cn(ds.outlineButton, 'h-9 min-h-9 rounded-lg px-3 text-xs')}
          aria-expanded={isOpen}
        >
          <Eye className="size-3" />
          {isSubmittedPending
            ? isOpen
              ? 'Đóng bài nộp'
              : 'Xem bài nộp'
            : isOpen
              ? 'Đóng bảng điểm'
              : 'Xem điểm'}
        </button>
      )
    }

    return (
      <span className="inline-flex min-h-9 items-center rounded-lg border border-[#82aedb]/55 bg-[#f7f9fc] px-3 text-xs font-semibold text-[#74839a]">
        {status === 'abandoned' ? 'Không có bảng điểm' : 'Chưa có bảng điểm'}
      </span>
    )
  }

  const selectedDetail = expandedHistoryId ? historyDetails[expandedHistoryId] : undefined

  const renderUnfinishedSessions = (sessions: typeof localSessions) => (
    <ul className="space-y-2">
      {sessions.map((item) => {
        const secondsLeft = item.pausedRemainingSeconds ?? secondsUntilExpiry(item.expiresAt)
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
  )

  return (
    <div className={ds.page}>
      <PageBanner
        sceneClassName="lg:h-0"
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
            <div className="mt-3">{renderUnfinishedSessions(localSessions.slice(0, 2))}</div>
            {localSessions.length > 2 && (
              <div className="mt-2 flex justify-end">
                <button
                  type="button"
                  onClick={() => {
                    setArchiveCourseId('')
                    setArchiveStatus('in_progress')
                    setArchivePage(1)
                    setExpandedHistoryId(null)
                    setArchiveOpen(true)
                  }}
                  className="inline-flex items-center gap-1 rounded-md px-1 py-0.5 text-xs font-bold text-[#1C4D8D] hover:bg-[#E7F3FB] hover:underline focus-visible:outline-2 focus-visible:outline-[#1C4D8D]"
                >
                  Xem lịch sử <ArrowRight className="size-3.5" aria-hidden="true" />
                </button>
              </div>
            )}
          </div>
        </section>
      )}

      <div
        className={cn(
          'grid gap-4 xl:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]',
          courses.length < 3 && 'xl:items-start',
        )}
      >
        <section ref={courseSectionRef} className={cn(ds.section, 'flex min-w-0 flex-col')}>
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

          <div className={cn(ds.sectionBody, 'flex flex-1 flex-col gap-4')}>
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
                <div
                  className={cn(
                    'grid flex-1 grid-cols-1 gap-4 sm:grid-cols-2',
                    courses.length >= 3 && 'sm:grid-rows-2',
                  )}
                >
                  {visibleCourses.map((course) => {
                    const courseQuota = quotas[course.id]
                    const exhausted = Boolean(courseQuota && !courseQuota.canStartExam)
                    const quotaUnknown = !isLoadingQuotas && quotaFailedIds.includes(course.id)
                    return (
                      <article
                        key={course.id}
                        className={cn(
                          exhausted
                            ? 'relative flex h-full flex-col overflow-hidden rounded-[22px] border border-[#82aedb]/55 bg-[#f7f8fa] p-3.5'
                            : cn(ds.softCard, ds.softCardHover, ds.panelShadow),
                        )}
                      >
                        {!exhausted && (
                          <div className={ds.softCardAccentWrap} aria-hidden="true">
                            <div className={ds.softCardAccentGlow} />
                            <div className={ds.softCardAccentLine} />
                          </div>
                        )}
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
                <CompactPagination
                  label="Phân trang môn học"
                  page={currentCoursePage}
                  pageCount={coursePageCount}
                  onPageChange={setCoursePage}
                />
              </>
            )}
          </div>
        </section>

        <section
          className={cn(ds.section, 'flex min-w-0 flex-col', compactSectionHeight && 'overflow-hidden')}
          style={compactSectionHeight ? { height: compactSectionHeight } : undefined}
        >
          <div className={ds.sectionHeader}>
            <div className={ds.sectionHeading}>
              <div className={ds.sectionTitleRow}>
                <span className={ds.sectionIconWrap}>
                  <History className={ds.sectionIcon} aria-hidden="true" />
                </span>
                <h2 className={ds.sectionTitle}>Lịch sử</h2>
                <span className={ds.sectionBadge}>
                  {historyTotal > recentHistoryLimit
                    ? `${recentHistoryLimit}/${historyTotal}`
                    : `${historyTotal} phiên`}
                </span>
              </div>
              <p className={ds.sectionHint}>
                {historyTotal > recentHistoryLimit
                  ? `${recentHistoryLimit} phiên gần nhất trong ${historyTotal} lượt`
                  : 'Các phiên thi thử gần nhất'}
              </p>
            </div>
          </div>

          {historyError && (
            <div className="px-5 py-4 text-xs text-[#ae3d50] sm:px-6">{historyError}</div>
          )}
          <div className={cn(ds.sectionBody, 'flex min-h-0 flex-1 flex-col gap-4', !isLoadingHistory && historyItems.length > 0 && 'gap-2.5 pt-2 pb-3 sm:pt-2 sm:pb-3')}>
            {isLoadingHistory ? (
              <div className="flex flex-1 items-center gap-2 text-sm font-semibold text-[#5b6b84]">
                <LoaderCircle className="size-4 animate-spin text-[#1C4D8D]" />
                Đang tải lịch sử…
              </div>
            ) : historyItems.length === 0 ? (
              <EmptyState
                className="flex-1 justify-center"
                icon={History}
                title="Chưa có ca thi thử nào"
                description="Lịch sử và điểm sẽ hiện sau khi thi thử."
              />
            ) : (
              <ul
                className={cn(
                  'grid min-h-0 flex-1',
                  recentHistoryLimit === 2 ? 'grid-rows-2' : 'grid-rows-4',
                )}
              >
                {visibleHistoryItems.map((item) => (
                  <li key={item.sessionId} className="relative grid grid-cols-[auto_minmax(0,1fr)] items-center gap-x-3 gap-y-2 rounded-b-lg border-b border-[#b7cce1] px-3 py-2 transition-shadow hover:z-10 hover:shadow-[0_5px_5px_-5px_rgba(15,40,84,0.35)] sm:grid-cols-[auto_minmax(0,1fr)_auto]">
                    <span className="grid size-10 place-items-center rounded-lg bg-[#E7F3FB] text-[#1C4D8D]">
                      <TimerReset className="size-5" aria-hidden="true" />
                    </span>
                    <div className="min-w-0">
                      <h3 className="break-words text-sm font-bold leading-snug text-[#0F2854]">{item.courseName}</h3>
                      <div className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-[#5b6b84]">
                        <span className={cn('rounded-full px-2 py-0.5 font-bold', statusChipClass(item.status))}>
                          {normalizeHistoryStatus(item.status) === 'graded' && item.totalScore !== null
                            ? `${item.totalScore.toFixed(2)}/10`
                            : statusLabel(item.status)}
                        </span>
                        <time dateTime={item.startedAt}>{formatExamDate(item.startedAt)}</time>
                        <span>{item.answeredQuestions}/{item.totalQuestions} câu</span>
                      </div>
                    </div>
                    <div className="col-start-2 min-w-0 sm:col-start-3">
                      {renderHistoryAction(item)}
                    </div>
                  </li>
                ))}
              </ul>
            )}

            {!isLoadingHistory && historyItems.length > 0 && (
            <div className="flex h-7 shrink-0 items-center justify-end text-xs">
              <button
                type="button"
                onClick={() => {
                  setArchiveCourseId('')
                  setArchiveStatus('')
                  setArchivePage(1)
                  setArchiveOpen(true)
                  setExpandedHistoryId(null)
                }}
                className="ml-auto inline-flex items-center gap-1 rounded-md px-1 py-0.5 font-bold text-[#1C4D8D] transition hover:bg-[#E7F3FB] hover:text-[#0F2854] hover:underline focus-visible:outline-2 focus-visible:outline-[#1C4D8D]"
              >
                Xem tất cả
                <ArrowRight className="size-3.5" aria-hidden="true" />
              </button>
            </div>
            )}
          </div>

        </section>
      </div>

      <section className={cn(ds.section, 'mb-0!')}>
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

      {archiveOpen && (
        <FormDialog
          titleId="mock-history-title"
          eyebrow="Thi thử"
          title="Lịch sử thi thử"
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
                className="min-w-64 flex-1 sm:w-96 sm:max-w-96 sm:flex-none"
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
          footer={(
            <DialogFooter className="justify-center bg-white sm:justify-center">
              <CompactPagination
                label="Phân trang lịch sử thi thử"
                page={Math.min(archivePage, Math.max(1, archiveTotalPages))}
                pageCount={Math.max(1, archiveTotalPages)}
                onPageChange={setArchivePage}
              />
            </DialogFooter>
            )
          }
        >
            <div className="flex min-h-[362px] flex-col">
              {archiveError ? (
                <p className="rounded-xl bg-white px-4 py-8 text-sm text-[#ae3d50]">{archiveError}</p>
              ) : isLoadingArchive ? (
                <div className="flex items-center gap-2 rounded-xl bg-white px-4 py-8 text-sm font-semibold text-[#5b6b84]">
                  <LoaderCircle className="size-4 animate-spin text-[#1C4D8D]" />
                  Đang tải lịch sử…
                </div>
              ) : archiveItems.length === 0 ? (
                <EmptyState
                  compact
                  className="flex-1 justify-center"
                  icon={History}
                  title="Không có phiên thi thử phù hợp"
                  description="Thử thay đổi bộ lọc để xem các phiên khác."
                />
              ) : (
                <div className="flex-1 overflow-x-auto rounded-2xl border border-[#82aedb]/55 bg-white">
                  <div className="min-w-[760px]">
                    <div className="grid grid-cols-[minmax(0,2.4fr)_124px_96px_100px_144px] min-h-10 items-center gap-3 border-b border-[#4988C4] bg-white px-4 py-3 text-xs font-bold text-[#5b6b84]">
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
                            className="grid grid-cols-[minmax(0,2.4fr)_124px_96px_100px_144px] min-h-16 items-center gap-3 px-4 py-3 text-sm text-[#5b6b84]"
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
          title={selectedDetail?.isGradingPending ? 'Bài nộp chờ chấm điểm' : 'Bảng điểm'}
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
          eyebrow="Thi thử"
          title={modalCourse.name}
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
            <div className="space-y-3">
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

              <MicrophoneCheckPanel check={microphoneCheck} />
            </div>
        </FormDialog>
      )}

      {quotaModal && (
        <ConfirmDialog
          tone="danger"
          eyebrow="Thi thử"
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
