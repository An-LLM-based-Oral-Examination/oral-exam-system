import { lazy, Suspense, useCallback, useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  AlertTriangle,
  ArrowRight,
  Bot,
  ChevronDown,
  ChevronLeft,
  Eye,
  GraduationCap,
  History,
  Keyboard,
  LoaderCircle,
  Mic2,
  PenLine,
  Play,
  RefreshCw,
  ShieldCheck,
  TimerReset,
  X,
} from 'lucide-react'
import { cn } from '@/lib/utils'
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

type DeviceState = 'idle' | 'checking' | 'ready' | 'error' | 'unsupported'
type HistoryScope = 'course' | 'all'

const HISTORY_PAGE_SIZE = 5

const EXAM_STEPS = [
  {
    title: 'Chọn môn còn lượt',
    hint: 'Bấm Bắt đầu và chọn hình thức thi.',
    icon: Play,
  },
  {
    title: 'Kiểm tra micro',
    hint: 'Micro sẵn sàng thì mới vào phòng.',
    icon: Mic2,
  },
  {
    title: 'Nhấn giữ để nói',
    hint: 'Giữ micro hoặc phím Space khi trả lời.',
    icon: Keyboard,
  },
  {
    title: 'Sửa lời rồi nộp',
    hint: 'Thả ra để xem và sửa trước khi nộp.',
    icon: PenLine,
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
    helper: 'Kiểm tra trước để tránh gián đoạn khi đồng hồ bắt đầu.',
    color: 'text-[#5d6d83]',
    dot: 'bg-[#9aa9bc]',
  },
  checking: {
    title: 'Đang kiểm tra micro…',
    helper: 'Vui lòng cho phép trình duyệt sử dụng micro.',
    color: 'text-[#385ed0]',
    dot: 'bg-[#5c73e7] animate-pulse motion-reduce:animate-none',
  },
  ready: {
    title: 'Micro đã sẵn sàng',
    helper: 'Micro đạt yêu cầu cho phiên thi thử.',
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

function formatClock(totalSeconds: number) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
}

function statusLabel(status: string) {
  if (status === 'graded') return 'Đã chấm'
  if (status === 'submitted') return 'Đã nộp'
  if (status === 'in_progress') return 'Đang làm'
  return status
}

export function MockExamPage() {
  const navigate = useNavigate()
  const [courses, setCourses] = useState<CourseDto[]>([])
  const [selectedCourseId, setSelectedCourseId] = useState('')
  const [openCourseId, setOpenCourseId] = useState<string | null>(null)
  const [quotas, setQuotas] = useState<Record<string, MockExamQuota>>({})
  const [quotaFailedIds, setQuotaFailedIds] = useState<string[]>([])
  const [hasFollowUp, setHasFollowUp] = useState(false)
  const [isLoadingCourses, setIsLoadingCourses] = useState(true)
  const [isLoadingQuotas, setIsLoadingQuotas] = useState(false)
  const [isStarting, setIsStarting] = useState(false)
  const [deviceState, setDeviceState] = useState<DeviceState>('idle')
  const [deviceNames, setDeviceNames] = useState('')
  const [pageError, setPageError] = useState('')
  const [quotaModal, setQuotaModal] = useState('')
  const [historyScope, setHistoryScope] = useState<HistoryScope>('all')
  const [historyPage, setHistoryPage] = useState(1)
  const [historyItems, setHistoryItems] = useState<MockExamHistoryItem[]>([])
  const [historyTotal, setHistoryTotal] = useState(0)
  const [historyTotalPages, setHistoryTotalPages] = useState(0)
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
      setSelectedCourseId((current) =>
        nextCourses.some((course) => course.id === current) ? current : '',
      )
      void loadQuotas(nextCourses.map((course) => course.id))
    } catch (error) {
      setPageError(getMockExamErrorDetail(error, 'Không thể tải danh sách môn học.'))
    } finally {
      setIsLoadingCourses(false)
    }
  }, [loadQuotas])

  const loadHistory = useCallback(async () => {
    if (historyScope === 'course' && !selectedCourseId && isLoadingCourses) return

    setIsLoadingHistory(true)
    setHistoryError('')
    try {
      const courseId = historyScope === 'course' ? selectedCourseId : undefined
      if (historyScope === 'course' && !courseId) {
        setHistoryItems([])
        setHistoryTotal(0)
        setHistoryTotalPages(0)
        return
      }
      const page = await mockExamService.getHistory({
        courseId,
        pageIndex: historyPage,
        pageSize: HISTORY_PAGE_SIZE,
      })
      setHistoryItems(page.items)
      setHistoryTotal(page.totalCount)
      setHistoryTotalPages(page.totalPages)
    } catch (error) {
      setHistoryItems([])
      setHistoryError(getMockExamErrorDetail(error, 'Không thể tải lịch sử thi thử.'))
    } finally {
      setIsLoadingHistory(false)
    }
  }, [historyPage, historyScope, isLoadingCourses, selectedCourseId])

  useEffect(() => {
    const timer = window.setTimeout(() => void loadCourses(), 0)
    return () => window.clearTimeout(timer)
  }, [loadCourses])

  useEffect(() => {
    const timer = window.setTimeout(() => void loadHistory(), 0)
    return () => window.clearTimeout(timer)
  }, [loadHistory])

  useEffect(() => {
    if (!quotaModal && !openCourseId) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      if (quotaModal) setQuotaModal('')
      else setOpenCourseId(null)
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [openCourseId, quotaModal])

  const handleDeviceCheck = async () => {
    if (!navigator.mediaDevices?.getUserMedia) {
      setDeviceState('unsupported')
      return
    }

    setDeviceState('checking')
    setDeviceNames('')
    let stream: MediaStream | undefined
    try {
      stream = await navigator.mediaDevices.getUserMedia({
        audio: { echoCancellation: true, noiseSuppression: true },
      })
      if (stream.getAudioTracks().length === 0) {
        throw new Error('Microphone is unavailable.')
      }
      setDeviceNames(stream.getAudioTracks()[0]?.label || 'Microphone mặc định')
      setDeviceState('ready')
    } catch {
      setDeviceState('error')
    } finally {
      stream?.getTracks().forEach((track) => track.stop())
    }
  }

  const openCourse = (courseId: string) => {
    if (selectedCourseId !== courseId) setHistoryPage(1)
    setSelectedCourseId(courseId)
    setOpenCourseId(courseId)
    setHasFollowUp(false)
    setPageError('')
    setExpandedHistoryId(null)
  }

  const handleStartExam = async () => {
    if (!modalCourse || !modalQuota?.canStartExam || deviceState !== 'ready') return
    if (activeSession && activeSecondsLeft > 0) {
      const confirmed = window.confirm(
        'Bạn đang có một ca thi chưa kết thúc. Bắt đầu ca mới sẽ dùng thêm một lượt hạn ngạch. Tiếp tục?',
      )
      if (!confirmed) return
    }

    setIsStarting(true)
    setPageError('')
    try {
      const session = await mockExamService.startSession(modalCourse.id, hasFollowUp)
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
      setPageError(getMockExamErrorDetail(error, 'Không thể bắt đầu ca thi thử.'))
    } finally {
      setIsStarting(false)
    }
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
    const isExpanded = expandedHistoryId === item.sessionId
    const canResume = item.status === 'in_progress' && activeSession?.sessionId === item.sessionId
    return (
      <button
        type="button"
        onClick={() => void handleToggleHistory(item)}
        className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#c9d6e8] bg-white px-3 text-xs font-semibold text-[#31445f] shadow-[0_3px_8px_rgba(23,48,96,0.05)] transition hover:border-[#9eb4df] hover:text-[#2456c8] focus-visible:outline-2 focus-visible:outline-[#245eea]"
        aria-expanded={canResume ? undefined : isExpanded}
      >
        <Eye className="size-3" />
        {canResume ? 'Tiếp tục ca thi' : isExpanded ? 'Đóng bảng điểm' : 'Xem bảng điểm'}
      </button>
    )
  }

  const selectedDetail = expandedHistoryId ? historyDetails[expandedHistoryId] : undefined

  return (
    <div className="space-y-6">
      <section className="relative isolate min-h-[218px] overflow-hidden rounded-[22px] border border-[#d9e3f2] bg-[linear-gradient(118deg,#ffffff_0%,#f5f8ff_48%,#e9efff_100%)] shadow-[0_14px_38px_rgba(31,61,105,0.08)] sm:min-h-[205px]">
        <div className="pointer-events-none absolute inset-0 [background-image:radial-gradient(#a9bbe0_0.8px,transparent_0.8px)] [background-size:22px_22px] opacity-55" />
        <div className="pointer-events-none absolute inset-y-0 right-0 w-[75%] bg-[radial-gradient(circle_at_72%_48%,rgba(88,111,230,0.15),transparent_46%)] sm:w-[60%]" />
        <div className="relative z-10 flex min-h-[218px] max-w-[760px] flex-col items-start justify-center px-5 py-6 sm:min-h-[205px] sm:px-7 lg:max-w-[64%] lg:px-8">
          <div className="inline-flex items-center gap-2 rounded-full border border-[#cbd8f3] bg-white/75 px-3 py-1 text-[9px] font-bold tracking-[0.08em] text-[#4b5fc6] uppercase shadow-sm backdrop-blur-sm">
            <ShieldCheck className="size-3" />
            Phòng khảo thí mô phỏng · AI Proctor
          </div>
          <h1 className="mt-3 text-[clamp(1.6rem,3vw,2.35rem)] leading-[1.08] font-bold tracking-[-0.045em] text-[#101d38]">
            Thi thử Vấn đáp Giả lập
          </h1>
          <p className="mt-2 max-w-[620px] text-[11px] leading-relaxed text-[#5f6f87] sm:text-[12px]">
            Làm bài có đồng hồ máy chủ, trả lời bằng giọng nói và nhận bảng điểm theo rubric ngay
            khi nộp.
          </p>
          <div className="mt-4 flex flex-wrap items-center gap-2">
            <span className="inline-flex items-center gap-1.5 rounded-full border border-[#dde4f3] bg-white/75 px-3 py-1.5 text-[9px] font-semibold text-[#576980] backdrop-blur-sm">
              <TimerReset className="size-3 text-[#5b63db]" /> Đồng hồ đồng bộ thời gian máy chủ
            </span>
            <span className="inline-flex items-center gap-1.5 rounded-full border border-[#d6ece7] bg-[#f5fffc]/80 px-3 py-1.5 text-[9px] font-semibold text-[#26816c] backdrop-blur-sm">
              <span className="size-1.5 animate-pulse rounded-full bg-[#31c494] motion-reduce:animate-none" />
              Nhấn giữ micro hoặc phím Space
            </span>
          </div>
        </div>
        <div className="pointer-events-none absolute inset-y-0 right-[-30%] z-0 w-[72%] opacity-35 sm:right-[-7%] sm:w-[62%] sm:opacity-85 lg:right-0 lg:w-[46%] lg:opacity-100">
          <Suspense
            fallback={
              <div className="flex size-full items-center justify-center" aria-hidden="true">
                <div className="size-20 animate-pulse rounded-full border border-[#8194ea]/30 bg-[#6573e6]/10 motion-reduce:animate-none" />
              </div>
            }
          >
            <MockExamHeroThreeScene
              scanning={deviceState === 'checking'}
              ready={deviceState === 'ready'}
            />
          </Suspense>
        </div>
        <div className="pointer-events-none absolute top-5 right-5 z-10 hidden items-center gap-2 rounded-xl border border-[#d8e1f2] bg-white/72 px-3 py-2 text-[9px] font-semibold text-[#58677c] shadow-sm backdrop-blur-md md:flex">
          <Bot className="size-4 text-[#5765dc]" />
          <span>
            Chấm điểm tức thì
            <span className="mt-0.5 block text-[8px] font-medium text-[#8793a6]">
              Rubric 10.0 theo CLO
            </span>
          </span>
        </div>
      </section>

      {activeSession && (
        <div className="flex flex-col gap-3 rounded-2xl border border-[#b7c8f2] bg-white px-5 py-4 shadow-[0_1px_0_#fff_inset,0_14px_32px_rgba(36,78,170,0.12)] sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="text-sm font-bold text-[#1b2f52]">
              Ca thi {activeSession.courseCode} vẫn đang mở
            </p>
            <p className="mt-1 text-sm text-[#52627a]">
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
            className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#245eea] px-4 text-sm font-bold text-white shadow-[0_8px_18px_rgba(36,94,234,0.28)] transition hover:bg-[#194fce] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea]"
          >
            Tiếp tục ca thi
            <ArrowRight className="size-4" />
          </button>
        </div>
      )}

      <section className="overflow-hidden rounded-2xl border border-[#c5d3e8] bg-white shadow-[0_1px_0_#fff_inset,0_16px_40px_rgba(23,48,96,0.1),0_2px_8px_rgba(23,48,96,0.05)]">
        <div className="border-b border-[#d5e0ef] px-5 py-4 sm:px-6">
          <h2 className="text-base font-bold tracking-[-0.02em] text-[#1b2a42] sm:text-lg">
            Hướng dẫn thi thử
          </h2>
          <p className="mt-1 text-sm text-[#5b6b84]">Bốn bước từ lúc chọn môn đến khi nộp bài.</p>
        </div>
        <div className="bg-[#f3f6fb] p-4 sm:p-5">
          <ol className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            {EXAM_STEPS.map((step, index) => {
              const StepIcon = step.icon
              return (
                <li
                  key={step.title}
                  className="flex gap-3 rounded-2xl border border-[#d0dced] bg-white p-3.5 shadow-[0_8px_22px_rgba(23,48,96,0.06)]"
                >
                  <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-[#e8edff] text-sm font-bold text-[#315fc4]">
                    {index + 1}
                  </span>
                  <span className="min-w-0">
                    <span className="flex items-center gap-1.5 text-sm font-bold text-[#1b2a42]">
                      <StepIcon className="size-3.5 shrink-0 text-[#245eea]" />
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
          <ul className="mt-3 flex flex-wrap gap-2">
            {EXAM_NOTES.map((note) => (
              <li
                key={note}
                className="inline-flex items-center gap-1.5 rounded-full border border-[#dde4f3] bg-white px-3 py-1.5 text-xs font-semibold text-[#576980]"
              >
                <span className="size-1.5 shrink-0 rounded-full bg-[#8ea0bd]" />
                {note}
              </li>
            ))}
          </ul>
        </div>
      </section>

      <section className="overflow-hidden rounded-2xl border border-[#c5d3e8] bg-white shadow-[0_1px_0_#fff_inset,0_16px_40px_rgba(23,48,96,0.1),0_2px_8px_rgba(23,48,96,0.05)]">
        <div className="border-b border-[#d5e0ef] px-5 py-4 sm:px-6">
          <h2 className="text-base font-bold tracking-[-0.02em] text-[#1b2a42] sm:text-lg">
            Môn học
          </h2>
          <p className="mt-1 text-sm text-[#5b6b84]">
            Môn hết lượt được đánh dấu riêng và không bắt đầu được cho đến ngày hôm sau.
          </p>
        </div>

        <div className="space-y-4 bg-[#f3f6fb] p-4 sm:p-5">
          {isLoadingCourses ? (
            <div className="flex items-center gap-2 text-sm font-semibold text-[#52627a]">
              <LoaderCircle className="size-4 animate-spin text-[#5865d8]" />
              Đang tải môn học…
            </div>
          ) : courses.length === 0 ? (
            <p className="text-sm text-[#5b6b84]">Chưa có môn học đang mở để thi thử.</p>
          ) : (
            <div className="grid gap-4 sm:grid-cols-2">
              {courses.map((course) => {
                const courseQuota = quotas[course.id]
                const exhausted = Boolean(courseQuota && !courseQuota.canStartExam)
                const quotaUnknown = !isLoadingQuotas && quotaFailedIds.includes(course.id)
                return (
                  <article
                    key={course.id}
                    className={cn(
                      'flex flex-col rounded-2xl border bg-white p-4 shadow-[0_8px_22px_rgba(23,48,96,0.08)]',
                      exhausted ? 'border-[#e4b4bf] bg-[#fff8f9]' : 'border-[#d0dced] bg-white',
                    )}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <span
                        className={cn(
                          'rounded-full px-3 py-1.5 text-xs font-bold',
                          exhausted ? 'bg-[#ffe4e8] text-[#b4233c]' : 'bg-[#e8edff] text-[#315fc4]',
                        )}
                      >
                        {course.code}
                      </span>
                      <span
                        className={cn(
                          'inline-flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-xs font-semibold',
                          exhausted
                            ? 'border-[#f3c9d0] bg-[#fff7f8] text-[#b4233c]'
                            : courseQuota
                              ? 'border-[#d6ece7] bg-[#f5fffc] text-[#26816c]'
                              : 'border-[#dde4f3] bg-white text-[#576980]',
                        )}
                      >
                        <TimerReset className="size-3.5 shrink-0" />
                        {courseQuota
                          ? exhausted
                            ? 'Hết lượt'
                            : `Còn ${courseQuota.remainingCount}/${courseQuota.maxDailyQuota} lượt`
                          : quotaUnknown
                            ? 'Chưa rõ lượt'
                            : 'Đang kiểm tra lượt…'}
                      </span>
                    </div>
                    <p
                      className={cn(
                        'mt-3 text-[20px] font-bold',
                        exhausted ? 'text-[#7a4450]' : 'text-[#1e2f47]',
                      )}
                    >
                      {course.name}
                    </p>
                    <span className="mt-2 inline-flex w-fit items-center gap-1.5 rounded-full border border-[#dde4f3] bg-white px-3 py-1.5 text-xs font-semibold text-[#576980]">
                      <GraduationCap className="size-3.5 shrink-0 text-[#5b63db]" />
                      {course.credits} tín chỉ
                    </span>
                    <button
                      type="button"
                      onClick={() => openCourse(course.id)}
                      disabled={exhausted}
                      className={cn(
                        'mt-4 inline-flex min-h-10 items-center justify-center gap-2 self-start rounded-xl px-4 text-sm font-bold shadow-[0_8px_16px_rgba(23,48,96,0.08)]',
                        exhausted
                          ? 'cursor-not-allowed bg-[#f3d5db] text-[#9a4d5c]'
                          : 'bg-[#245eea] text-white hover:bg-[#194fce] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea]',
                      )}
                    >
                      <Play className="size-3.5 fill-current" />
                      {exhausted ? 'Hết lượt' : 'Bắt đầu'}
                    </button>
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

      <section className="overflow-hidden rounded-2xl border border-[#c5d3e8] bg-white shadow-[0_1px_0_#fff_inset,0_16px_40px_rgba(23,48,96,0.1),0_2px_8px_rgba(23,48,96,0.05)]">
        <div className="flex flex-col gap-3 border-b border-[#d5e0ef] bg-[#f7f9fd] px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6">
          <div>
            <div className="flex items-center gap-2">
              <History className="size-4 text-[#5865d8]" />
              <h2 className="text-base font-bold text-[#1b2a42] sm:text-lg">Lịch sử</h2>
            </div>
            <p className="mt-1 text-sm text-[#5b6b84]">Các ca đã làm, kể cả bài đang dở.</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <div className="inline-flex rounded-xl border border-[#c9d6e8] bg-white p-1 shadow-[0_4px_12px_rgba(23,48,96,0.06)]">
              <button
                type="button"
                onClick={() => {
                  setHistoryScope('course')
                  setHistoryPage(1)
                  setExpandedHistoryId(null)
                }}
                className={cn(
                  'rounded-lg px-3 py-1.5 text-sm font-bold',
                  historyScope === 'course' ? 'bg-[#e8eeff] text-[#2456c8]' : 'text-[#5c6d86]',
                )}
              >
                Môn vừa chọn
              </button>
              <button
                type="button"
                onClick={() => {
                  setHistoryScope('all')
                  setHistoryPage(1)
                  setExpandedHistoryId(null)
                }}
                className={cn(
                  'rounded-lg px-3 py-1.5 text-sm font-bold',
                  historyScope === 'all' ? 'bg-[#e8eeff] text-[#2456c8]' : 'text-[#5c6d86]',
                )}
              >
                Tất cả môn
              </button>
            </div>
            <span className="rounded-full border border-[#d5e0ef] bg-white px-3 py-1 text-xs font-semibold text-[#52627a]">
              {historyTotal} lượt
            </span>
          </div>
        </div>

        {historyError && (
          <div className="px-5 py-4 text-[10px] text-[#ae3d50] sm:px-6">{historyError}</div>
        )}
        {isLoadingHistory ? (
          <div className="flex items-center gap-2 px-5 py-8 text-[11px] font-semibold text-[#52627a] sm:px-6">
            <LoaderCircle className="size-4 animate-spin text-[#5865d8]" />
            Đang tải lịch sử…
          </div>
        ) : historyItems.length === 0 ? (
          <p className="px-5 py-8 text-sm text-[#5b6b84] sm:px-6">
            {historyScope === 'course' && !selectedCourseId
              ? 'Bấm Bắt đầu ở một môn để lọc lịch sử môn đó.'
              : 'Chưa có ca thi thử nào.'}
          </p>
        ) : (
          <>
            <div className="hidden md:block">
              <table className="w-full table-fixed border-collapse text-left">
                <thead className="border-b border-[#d5e0ef] bg-[#f7f9fd] text-xs font-bold text-[#5b6b84]">
                  <tr>
                    <th className="w-[10%] px-5 py-3">Mã ca</th>
                    <th className="w-[34%] px-3 py-3">Môn thi</th>
                    <th className="w-[16%] px-3 py-3">Thời điểm</th>
                    <th className="w-[12%] px-3 py-3">Trạng thái</th>
                    <th className="w-[12%] px-3 py-3">Điểm</th>
                    <th className="w-[16%] px-3 py-3">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#d5e0ef]">
                  {historyItems.map((item) => (
                    <tr key={item.sessionId} className="text-sm text-[#4d5e78]">
                      <td className="px-5 py-3 font-mono font-bold text-[#3f5fc7]">
                        {item.sessionId.slice(0, 8).toUpperCase()}
                      </td>
                      <td className="px-3 py-3">
                        <p className="font-bold text-[#26364d]">{item.courseName}</p>
                        <p className="mt-0.5 truncate text-xs text-[#6d7c96]">
                          {item.courseCode} · {item.answeredQuestions}/{item.totalQuestions} câu
                        </p>
                      </td>
                      <td className="px-3 py-3">{formatExamDate(item.startedAt)}</td>
                      <td className="px-3 py-3">{statusLabel(item.status)}</td>
                      <td className="px-3 py-3">
                        {item.totalScore === null ? (
                          '—'
                        ) : (
                          <span className="inline-flex items-center gap-1 rounded-full bg-[#edfbf6] px-2 py-1 font-bold text-[#16866c]">
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
            <div className="divide-y divide-[#d5e0ef] md:hidden">
              {historyItems.map((item) => (
                <article key={item.sessionId} className="space-y-3 p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="font-mono text-[9px] font-bold text-[#4961c8]">
                        {item.sessionId.slice(0, 8).toUpperCase()}
                      </p>
                      <h3 className="mt-1 text-[11px] font-bold text-[#26364d]">
                        {item.courseName}
                      </h3>
                      <p className="mt-0.5 text-[8px] text-[#8794a6]">
                        {item.courseCode} · {statusLabel(item.status)}
                      </p>
                    </div>
                    <span className="shrink-0 rounded-full bg-[#edfbf6] px-2 py-1 text-[9px] font-bold text-[#16866c]">
                      {item.totalScore === null ? '—' : `${item.totalScore.toFixed(2)}/10`}
                    </span>
                  </div>
                  <div className="flex items-center justify-between gap-3 text-[9px] text-[#758398]">
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

        {expandedHistoryId && (
          <div className="border-t border-[#dfe6f1] bg-[#f7f9fd] px-5 py-4 sm:px-6">
            {loadingDetailId === expandedHistoryId && (
              <div className="flex items-center gap-2 text-[11px] font-semibold text-[#52627a]">
                <LoaderCircle className="size-4 animate-spin text-[#5865d8]" />
                Đang mở bảng điểm…
              </div>
            )}
            {detailError && <p className="text-[10px] text-[#ae3d50]">{detailError}</p>}
            {selectedDetail && <MockExamScorecard scorecard={selectedDetail} />}
          </div>
        )}

        {historyTotalPages > 1 && (
          <div className="flex items-center justify-between border-t border-[#edf1f6] px-5 py-3 sm:px-6">
            <button
              type="button"
              disabled={historyPage <= 1}
              onClick={() => {
                setHistoryPage((current) => Math.max(1, current - 1))
                setExpandedHistoryId(null)
              }}
              className="inline-flex min-h-8 items-center gap-1 rounded-lg px-2 text-[10px] font-bold text-[#425fc3] disabled:opacity-40"
            >
              <ChevronLeft className="size-4" /> Trước
            </button>
            <span className="text-[10px] font-semibold text-[#6d7c90]">
              Trang {historyPage}/{historyTotalPages}
            </span>
            <button
              type="button"
              disabled={historyPage >= historyTotalPages}
              onClick={() => {
                setHistoryPage((current) => current + 1)
                setExpandedHistoryId(null)
              }}
              className="inline-flex min-h-8 items-center gap-1 rounded-lg px-2 text-[10px] font-bold text-[#425fc3] disabled:opacity-40"
            >
              Sau <ChevronDown className="size-4 -rotate-90" />
            </button>
          </div>
        )}
      </section>

      {modalCourse && (
        <div
          className="fixed inset-0 z-[80] flex items-end justify-center bg-[#10203d]/45 px-4 py-6 sm:items-center"
          role="presentation"
          onClick={(event) => {
            if (event.target === event.currentTarget) setOpenCourseId(null)
          }}
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="mock-course-title"
            className="flex max-h-[90dvh] w-full max-w-lg flex-col overflow-hidden rounded-2xl border border-[#c5d3e8] bg-white shadow-[0_1px_0_#fff_inset,0_24px_60px_rgba(16,32,61,0.22)]"
          >
            <div className="border-b border-[#d5e0ef] px-5 py-4">
              <div className="flex items-center justify-between gap-3">
                <span className="inline-flex rounded-full bg-[#e8edff] px-3 py-1.5 text-xs font-bold text-[#315fc4]">
                  {modalCourse.code}
                </span>
                <button
                  type="button"
                  aria-label="Đóng"
                  onClick={() => setOpenCourseId(null)}
                  className="flex size-9 shrink-0 items-center justify-center rounded-full border border-[#d5e0ef] text-[#52627a] hover:bg-[#f3f6fb]"
                >
                  <X className="size-4" />
                </button>
              </div>
              <div className="mt-2 flex items-start justify-between gap-4">
                <h2
                  id="mock-course-title"
                  className="min-w-0 text-[20px] leading-snug font-bold text-[#1b2a42]"
                >
                  {modalCourse.name}{' - '}
                  <span className="relative -top-0.5 inline-flex items-center gap-1.5 rounded-full border border-[#dde4f3] bg-white px-2 py-1 align-middle text-xs font-semibold text-[#576980]">
                    <GraduationCap className="size-3.5 shrink-0 text-[#5b63db]" />
                    {modalCourse.credits} tín chỉ
                  </span>
                </h2>
                <span
                  className={cn(
                    'relative top-0.5 inline-flex shrink-0 items-center gap-1.5 rounded-full border px-2 py-1 text-xs font-semibold',
                    modalQuota && !modalQuota.canStartExam
                      ? 'border-[#f3c9d0] bg-[#fff7f8] text-[#b4233c]'
                      : modalQuota
                        ? 'border-[#d6ece7] bg-[#f5fffc] text-[#26816c]'
                        : 'border-[#dde4f3] bg-white text-[#576980]',
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

            <div className="space-y-4 overflow-y-auto bg-[#f3f6fb] p-4 sm:p-5">
              {modalQuota && !modalQuota.canStartExam && (
                <p className="rounded-xl border border-[#f3c9d0] bg-[#fff7f8] px-3 py-2 text-sm leading-relaxed text-[#b4233c]">
                  Môn này đã hết lượt thi thử hôm nay. Quay lại vào ngày mai.
                </p>
              )}

              <fieldset>
                <legend className="text-sm font-bold text-[#1b2a42]">Hình thức thi</legend>
                <div className="mt-2 grid gap-3 sm:grid-cols-2">
                  <button
                    type="button"
                    onClick={() => setHasFollowUp(false)}
                    className={cn(
                      'rounded-2xl border bg-white px-3.5 py-3 text-left shadow-[0_8px_22px_rgba(23,48,96,0.06)]',
                      !hasFollowUp ? 'border-[#245eea]' : 'border-[#d0dced] hover:border-[#b7c6ef]',
                    )}
                    aria-pressed={!hasFollowUp}
                  >
                    <span className="flex items-center gap-2">
                      <span
                        className={cn(
                          'size-3.5 rounded-full border-2',
                          !hasFollowUp ? 'border-[#245eea] bg-[#245eea]' : 'border-[#c5d3e8]',
                        )}
                      />
                      <span className="text-sm font-bold text-[#1b2a42]">Không câu hỏi phụ</span>
                    </span>
                  </button>
                  <button
                    type="button"
                    onClick={() => setHasFollowUp(true)}
                    className={cn(
                      'rounded-2xl border bg-white px-3.5 py-3 text-left shadow-[0_8px_22px_rgba(23,48,96,0.06)]',
                      hasFollowUp ? 'border-[#245eea]' : 'border-[#d0dced] hover:border-[#b7c6ef]',
                    )}
                    aria-pressed={hasFollowUp}
                  >
                    <span className="flex items-center gap-2">
                      <span
                        className={cn(
                          'size-3.5 rounded-full border-2',
                          hasFollowUp ? 'border-[#245eea] bg-[#245eea]' : 'border-[#c5d3e8]',
                        )}
                      />
                      <span className="text-sm font-bold text-[#1b2a42]">Có câu hỏi phụ</span>
                    </span>
                  </button>
                </div>
                <p className="mt-3 rounded-2xl border border-[#d0dced] bg-white px-4 py-3 text-sm leading-relaxed text-[#52627a] shadow-[0_8px_22px_rgba(23,48,96,0.06)]">
                  {hasFollowUp
                    ? 'Sau câu trả lời có thể có thêm câu hỏi phụ để làm rõ ý. Thời gian làm bài là 45 phút.'
                    : 'Chỉ làm các câu chính trong đề. Thời gian theo cấu hình môn, thường 30 phút.'}
                </p>
              </fieldset>

              <div className="flex flex-col gap-3 rounded-2xl border border-[#d0dced] bg-white p-3.5 shadow-[0_8px_22px_rgba(23,48,96,0.06)] sm:flex-row sm:items-center sm:justify-between">
                <div className="min-w-0">
                  <p
                    className={cn('flex items-center gap-2 text-sm font-bold', deviceStatus.color)}
                  >
                    <span className={cn('size-2 shrink-0 rounded-full', deviceStatus.dot)} />
                    {deviceStatus.title}
                  </p>
                  <p className="mt-1 truncate text-xs text-[#5b6b84]">
                    {deviceState === 'ready' && deviceNames ? deviceNames : deviceStatus.helper}
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => void handleDeviceCheck()}
                  disabled={deviceState === 'checking'}
                  className="inline-flex min-h-10 shrink-0 items-center justify-center gap-1.5 rounded-xl border border-[#c9d6e8] bg-white px-3 text-xs font-bold text-[#31445f] shadow-[0_3px_8px_rgba(23,48,96,0.05)] hover:border-[#9eb4df] disabled:cursor-wait disabled:opacity-60"
                >
                  {deviceState === 'checking' ? (
                    <LoaderCircle className="size-3.5 animate-spin" />
                  ) : (
                    <Mic2 className="size-3.5" />
                  )}
                  {deviceState === 'ready' ? 'Kiểm tra lại' : 'Kiểm tra micro'}
                </button>
              </div>

              {pageError && (
                <p className="rounded-xl border border-[#f3c9d0] bg-[#fff7f8] px-3 py-2 text-sm leading-relaxed text-[#b4233c]">
                  {pageError}
                </p>
              )}
            </div>

            <div className="border-t border-[#d5e0ef] bg-white px-4 py-4 sm:px-5">
              <button
                type="button"
                onClick={() => void handleStartExam()}
                disabled={!canStart}
                className="inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-xl bg-[#245eea] px-6 text-sm font-bold text-white shadow-[0_10px_22px_rgba(36,94,234,0.28)] disabled:opacity-45"
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
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-[#06142b]/60 px-4"
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="quota-modal-title"
            className="w-full max-w-md rounded-2xl bg-white p-5 shadow-[0_24px_70px_rgba(4,20,48,0.28)]"
          >
            <div className="flex size-11 items-center justify-center rounded-xl bg-[#fff1f3] text-[#c4475c]">
              <AlertTriangle className="size-5" />
            </div>
            <h2 id="quota-modal-title" className="mt-4 text-base font-bold text-[#1b2a42]">
              Hết lượt thi thử hôm nay
            </h2>
            <p className="mt-2 text-[12px] leading-relaxed text-[#5d6e88]">{quotaModal}</p>
            <button
              type="button"
              onClick={() => setQuotaModal('')}
              className="mt-5 inline-flex min-h-10 items-center justify-center rounded-xl bg-[#245eea] px-4 text-[11px] font-bold text-white"
            >
              Đã hiểu
            </button>
          </div>
        </div>
      )}

      <div className="sr-only" aria-live="polite">
        {deviceStatus.title}. {deviceNames || deviceStatus.helper}
      </div>
    </div>
  )
}
