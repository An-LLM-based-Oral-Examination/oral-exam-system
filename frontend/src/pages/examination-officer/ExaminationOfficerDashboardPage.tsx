import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  AlertTriangle,
  ArrowRight,
  CalendarDays,
  ClipboardCheck,
  FileBarChart2,
  RefreshCw,
  ScanSearch,
  ServerCog,
  TicketCheck,
  type LucideIcon,
} from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'
import { officialExamService } from '@/services/officialExam.service'
import { appealsService } from '@/services/appeals.service'
import { deadLetterQueueService } from '@/services/deadLetterQueue.service'
import type { OfficialExamSession, ExamShift } from '@/types/officialExam.types'
import type { Appeal } from '@/types/appeal.types'
import type { DeadLetterQueueTask } from '@/types/deadLetterQueue.types'
import { PageBanner, BannerFact } from '@/design/ui'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { toast } from '@/components/common/toastStore'
import {
  formatEoTimeRange,
  getShiftStatusMeta,
} from '@/components/examination-officer/eoFormatters'

interface MetricCardProps {
  label: string
  value: number
  helper: string
  icon: LucideIcon
  tone: string
}

function MetricCard({ label, value, helper, icon: Icon, tone }: MetricCardProps) {
  return (
    <article
      className={cn(
        'flex items-center gap-4 rounded-2xl border px-4 py-4 shadow-[0_10px_24px_rgba(15,40,84,0.08)]',
        tone,
      )}
    >
      <span className="flex size-12 shrink-0 items-center justify-center rounded-2xl bg-[#1C4D8D] text-white">
        <Icon className="size-5" aria-hidden="true" />
      </span>
      <div className="min-w-0">
        <p className="truncate text-[11px] font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
          {label}
        </p>
        <strong className="mt-0.5 block text-[1.65rem] leading-none font-black tracking-[-0.04em] text-[#0F2854]">
          {value}
        </strong>
        <p className="mt-1 truncate text-xs font-semibold text-[#5b6b84]">{helper}</p>
      </div>
    </article>
  )
}

function isToday(value: string) {
  const date = new Date(value)
  const now = new Date()
  return !Number.isNaN(date.getTime()) && date.toDateString() === now.toDateString()
}

export function ExaminationOfficerDashboardPage() {
  const { user } = useAuth()
  const [sessions, setSessions] = useState<OfficialExamSession[]>([])
  const [shifts, setShifts] = useState<ExamShift[]>([])
  const [appeals, setAppeals] = useState<Appeal[]>([])
  const [dlqTasks, setDlqTasks] = useState<DeadLetterQueueTask[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [error, setError] = useState('')

  const loadDashboard = useCallback(async () => {
    setIsRefreshing(true)
    setError('')
    const [sessionResult, shiftResult, appealResult, dlqResult] = await Promise.allSettled([
      officialExamService.getSessions(),
      officialExamService.getShifts(),
      appealsService.list(),
      deadLetterQueueService.listForExaminationOfficer('pending'),
    ])
    const failed = [sessionResult, shiftResult, appealResult, dlqResult].some(
      (result) => result.status === 'rejected',
    )
    if (sessionResult.status === 'fulfilled') setSessions(sessionResult.value)
    if (shiftResult.status === 'fulfilled') setShifts(shiftResult.value)
    if (appealResult.status === 'fulfilled') setAppeals(appealResult.value)
    if (dlqResult.status === 'fulfilled') setDlqTasks(dlqResult.value)
    if (failed) {
      setError('Một số dữ liệu tổng quan chưa tải được. Bạn có thể thử làm mới.')
      toast.error('Không tải đầy đủ dữ liệu tổng quan Khảo thí.')
    }
    setIsLoading(false)
    setIsRefreshing(false)
  }, [])

  useEffect(() => {
    let active = true
    void Promise.resolve().then(() => {
      if (active) void loadDashboard()
    })
    return () => {
      active = false
    }
  }, [loadDashboard])

  const sessionsById = useMemo(
    () => new Map(sessions.map((item) => [item.id, item])),
    [sessions],
  )

  const todayShifts = useMemo(() => shifts.filter((shift) => isToday(shift.startTime)), [shifts])

  const prioritizedShifts = useMemo(() => {
    const rank = (shift: ExamShift) => {
      const st = shift.status.toLowerCase()
      if (st === 'in_progress') return 0
      if (isToday(shift.startTime) && st === 'scheduled') return 1
      if (st === 'scheduled') return 2
      if (st === 'completed' && !shift.isGradesPublished) return 3
      return 4
    }
    return [...shifts]
      .sort((a, b) => {
        const diff = rank(a) - rank(b)
        if (diff !== 0) return diff
        return Date.parse(a.startTime) - Date.parse(b.startTime)
      })
      .slice(0, 8)
  }, [shifts])

  const pendingAppeals = useMemo(
    () =>
      appeals.filter(
        (appeal) => !['APPROVED', 'REJECTED', 'CANCELLED'].includes(appeal.status.toUpperCase()),
      ),
    [appeals],
  )
  const pendingAssignments = useMemo(
    () => pendingAppeals.filter((appeal) => !appeal.assignedTo),
    [pendingAppeals],
  )

  return (
    <div className={ds.page}>
      <PageBanner
        icon={TicketCheck}
        eyebrow="CỔNG KHẢO THÍ · MF-04"
        title={
          <>
            Điều hành kỳ thi thật,{' '}
            <span className="text-[#BDE8F5]">{user?.fullName ?? 'Cán bộ Khảo thí'}</span>
          </>
        }
        description="Quản lý đợt thi FE/RE, chia phòng thi, giám sát ca thi trực tiếp, công bố điểm và điều phối phúc khảo."
        extras={
          <>
            <BannerFact icon={CalendarDays}>Điều phối ca thi</BannerFact>
            <BannerFact icon={ScanSearch} tone="green">
              Giám sát MF-04
            </BannerFact>
            <BannerFact icon={ServerCog} tone="violet">
              DLQ thi thật
            </BannerFact>
          </>
        }
      />

      {error ? (
        <div
          role="alert"
          className="flex items-center justify-between gap-3 rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
        >
          <span>{error}</span>
          <button type="button" onClick={() => void loadDashboard()} className={ds.outlineButton}>
            <RefreshCw className="size-4" /> Thử lại
          </button>
        </div>
      ) : null}

      <section aria-label="Tổng quan Khảo thí" className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <MetricCard
          label="Đợt thi"
          value={sessions.length}
          helper="Kỳ thi chính thức FE/RE"
          icon={CalendarDays}
          tone="border-[#88b2de]/55 bg-[linear-gradient(145deg,#e9f5fc_0%,#cae3f5_100%)]"
        />
        <MetricCard
          label="Ca thi hôm nay"
          value={todayShifts.length}
          helper={`Tổng ${shifts.length} ca thi`}
          icon={ScanSearch}
          tone="border-[#edca8c]/55 bg-[linear-gradient(145deg,#fff4e2_0%,#f9dcac_100%)]"
        />
        <MetricCard
          label="Phúc khảo chờ xử lý"
          value={pendingAppeals.length}
          helper={`${pendingAssignments.length} đơn chưa phân công`}
          icon={ClipboardCheck}
          tone="border-[#c4afe8]/55 bg-[linear-gradient(145deg,#f2eafc_0%,#ddcef7_100%)]"
        />
        <MetricCard
          label="Lỗi chấm AI (DLQ)"
          value={dlqTasks.length}
          helper="Task MF-04 chờ xử lý"
          icon={AlertTriangle}
          tone="border-[#f3c2c8]/55 bg-[linear-gradient(145deg,#fff1f2_0%,#fde1e5_100%)]"
        />
      </section>

      <section aria-label="Lối tắt nghiệp vụ" className="grid gap-2.5 sm:grid-cols-2 xl:grid-cols-4">
        {[
          {
            to: '/examination-officer/exams',
            title: 'Kỳ thi & Mở ca',
            desc: 'Tạo đợt thi, nhập DSSV, chia phòng',
            icon: CalendarDays,
          },
          {
            to: '/examination-officer/tickets/assign',
            title: 'Cấp vé thi',
            desc: 'Cấp vé lẻ hoặc import danh sách ca',
            icon: TicketCheck,
          },
          {
            to: '/examination-officer/monitor',
            title: 'Giám sát phòng thi',
            desc: 'Theo dõi ghế, mở khóa máy, đình chỉ',
            icon: ScanSearch,
          },
          {
            to: '/examination-officer/grades',
            title: 'Công bố điểm',
            desc: 'Khóa điểm 1 chiều & xuất Excel/PDF',
            icon: FileBarChart2,
          },
        ].map(({ to, title, desc, icon: Icon }) => (
          <Link
            key={to}
            to={to}
            className="group flex items-center justify-between gap-3 rounded-2xl border border-[#d9e8f2] bg-white px-4 py-3.5 transition hover:border-[#4988C4] hover:bg-[#F8FBFD]"
          >
            <div className="flex min-w-0 items-center gap-3">
              <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-[#E7F3FB] text-[#1C4D8D] transition group-hover:bg-[#1C4D8D] group-hover:text-white">
                <Icon className="size-4" aria-hidden="true" />
              </span>
              <div className="min-w-0">
                <strong className="block truncate text-sm text-[#0F2854]">{title}</strong>
                <span className="block truncate text-xs text-[#5b6b84]">{desc}</span>
              </div>
            </div>
            <ArrowRight className="size-4 shrink-0 text-[#4988C4] transition group-hover:translate-x-0.5" />
          </Link>
        ))}
      </section>

      <section className={ds.section}>
        <div
          className={cn(
            ds.sectionHeader,
            'flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between',
          )}
        >
          <div>
            <div className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}>
                <CalendarDays className={ds.sectionIcon} aria-hidden="true" />
              </span>
              <h2 className={ds.sectionTitle}>Ca thi cần theo dõi</h2>
              <span className={ds.sectionBadge}>
                {isLoading ? '…' : `${todayShifts.length} ca hôm nay`}
              </span>
            </div>
            <p className={ds.sectionHint}>
              Ưu tiên ca đang diễn ra, ca sắp tới và ca đã kết thúc chờ công bố điểm.
            </p>
          </div>
          <button
            type="button"
            onClick={() => void loadDashboard()}
            disabled={isRefreshing}
            className={ds.outlineButton}
          >
            <RefreshCw className={cn('size-4', isRefreshing && 'animate-spin')} /> Làm mới
          </button>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[760px] text-left text-sm">
            <thead className="bg-[#F4FBFD] text-xs font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
              <tr>
                <th className="px-5 py-3">Ca thi</th>
                <th className="px-5 py-3">Phòng & Sĩ số</th>
                <th className="px-5 py-3">Thời gian</th>
                <th className="px-5 py-3">Trạng thái</th>
                <th className="px-5 py-3 text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#d9e8f2]">
              {prioritizedShifts.map((shift) => {
                const statusMeta = getShiftStatusMeta(shift.status)
                const sessionTitle = sessionsById.get(shift.sessionId)?.title
                return (
                  <tr key={shift.id} className="hover:bg-[#F8FBFD]">
                    <td className="px-5 py-3">
                      <strong className="block font-bold text-[#0F2854]">
                        {shift.shiftName || shift.id}
                      </strong>
                      {sessionTitle ? (
                        <span className="text-xs text-[#5b6b84]">{sessionTitle}</span>
                      ) : null}
                    </td>
                    <td className="px-5 py-3 text-[#5b6b84]">
                      <span className="font-semibold text-[#0F2854]">
                        {shift.roomLab || 'Chưa cập nhật'}
                      </span>
                      <span className="ml-2 text-xs">
                        ({shift.assignedTicketsCount ?? 0}/{shift.maxSeats ?? 24} SV)
                      </span>
                    </td>
                    <td className="px-5 py-3 whitespace-nowrap text-[#5b6b84]">
                      {formatEoTimeRange(shift.startTime, shift.endTime)}
                    </td>
                    <td className="px-5 py-3">
                      <span className={statusMeta.chipClass}>{statusMeta.label}</span>
                    </td>
                    <td className="px-5 py-3">
                      <div className="flex justify-end gap-2">
                        <Link
                          to={`/examination-officer/monitor/${shift.id}`}
                          className={ds.outlineButton}
                        >
                          Giám sát <ArrowRight className="size-3.5" />
                        </Link>
                        {shift.status.toLowerCase() === 'completed' ? (
                          <Link
                            to={`/examination-officer/grades/${shift.id}`}
                            className={ds.primaryButton}
                          >
                            Điểm
                          </Link>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
        {!isLoading && prioritizedShifts.length === 0 ? (
          <div className="p-5">
            <EmptyState
              compact
              icon={CalendarDays}
              title="Chưa có ca thi nào"
              description="Vào mục Kỳ thi chính thức để khởi tạo đợt thi và mở phòng thi."
            />
          </div>
        ) : null}
      </section>

      <section className="grid gap-4 lg:grid-cols-2">
        <section className={ds.section}>
          <div className={ds.sectionHeader}>
            <div className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}>
                <ClipboardCheck className={ds.sectionIcon} aria-hidden="true" />
              </span>
              <h2 className={ds.sectionTitle}>Phúc khảo cần xử lý</h2>
            </div>
            <p className={ds.sectionHint}>Đơn phúc khảo đang chờ phân công hoặc phê duyệt.</p>
          </div>
          <div className="space-y-3 p-5">
            {pendingAppeals.slice(0, 4).map((appeal) => (
              <Link
                key={appeal.id}
                to={`/examination-officer/appeals/${appeal.id}`}
                className="flex items-center justify-between gap-3 rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] px-3 py-3 transition hover:-translate-y-0.5 hover:border-[#4988C4]"
              >
                <span className="min-w-0">
                  <strong className="block truncate text-sm text-[#0F2854]">
                    {appeal.studentCode || appeal.studentName}
                  </strong>
                  <span className="mt-1 block truncate text-xs text-[#5b6b84]">
                    {appeal.reason || 'Đơn phúc khảo'} ·{' '}
                    {appeal.assignedTo ? 'Đã phân công' : 'Chưa phân công'}
                  </span>
                </span>
                <ArrowRight className="size-4 shrink-0 text-[#1C4D8D]" />
              </Link>
            ))}
            {pendingAppeals.length === 0 ? (
              <EmptyState
                compact
                icon={ClipboardCheck}
                title="Không có đơn tồn đọng"
                description="Các đơn phúc khảo đang ở trạng thái ổn định."
              />
            ) : null}
          </div>
        </section>
        <section className={ds.section}>
          <div className={ds.sectionHeader}>
            <div className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}>
                <ServerCog className={ds.sectionIcon} aria-hidden="true" />
              </span>
              <h2 className={ds.sectionTitle}>Lỗi chấm AI (DLQ MF-04)</h2>
            </div>
            <p className={ds.sectionHint}>Chỉ hiển thị task OFFICIAL_EXAM do Khảo thí quản lý.</p>
          </div>
          <div className="space-y-3 p-5">
            {dlqTasks.slice(0, 4).map((task) => (
              <Link
                key={task.id}
                to="/examination-officer/dlq"
                className="flex items-center justify-between gap-3 rounded-xl border border-[#f3c2c8] bg-[#fff7f7] px-3 py-3 transition hover:-translate-y-0.5"
              >
                <span className="min-w-0">
                  <strong className="block truncate text-sm text-[#0F2854]">
                    {task.taskType || 'Task chấm thi'}
                  </strong>
                  <span className="mt-1 block truncate text-xs text-[#a43d4b]">
                    Retry {task.retryCount} · {task.status}
                  </span>
                </span>
                <ArrowRight className="size-4 shrink-0 text-[#c2415b]" />
              </Link>
            ))}
            {dlqTasks.length === 0 ? (
              <EmptyState
                compact
                icon={ServerCog}
                title="DLQ đang trống"
                description="Không có task thi thật đang chờ xử lý."
              />
            ) : null}
          </div>
        </section>
      </section>
    </div>
  )
}
