import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  AlertTriangle,
  ArrowRight,
  Bell,
  BookOpen,
  CalendarDays,
  FileText,
  LayoutDashboard,
  RefreshCw,
  SlidersHorizontal,
  UsersRound,
} from 'lucide-react'
import { AbuseMetricsPanel } from '@/components/admin/AbuseMetricsPanel'
import { RuntimeDiagnosticsPanel } from '@/components/admin/RuntimeDiagnosticsPanel'
import { ds } from '@/design/system'
import { extractProblemDetails, type NormalizedProblem } from '@/lib/problemDetails'
import { cn } from '@/lib/utils'
import { ROUTE_PATHS } from '@/routes/routePaths'
import { adminService, type RuntimeDiagnosticsDto } from '@/services/admin.service'
import { AdminPageHeader } from '@/pages/admin/components/AdminPageHeader'
import { AdminStatCard } from '@/pages/admin/components/AdminStatCard'
import { AdminStateBanner } from '@/pages/admin/components/AdminStateBanner'
import { adminUi } from '@/pages/admin/components/adminUi'

interface OverviewCounts {
  usersTotal: number | null
  semestersTotal: number | null
  activeSemesters: number | null
  coursesTotal: number | null
  activeCourses: number | null
}

const QUICK_NAV_ITEMS = [
  { title: 'Người dùng', to: ROUTE_PATHS.admin.users, icon: UsersRound },
  { title: 'Học kỳ & Môn mở', to: ROUTE_PATHS.admin.semesters, icon: CalendarDays },
  { title: 'Môn học & CLO', to: ROUTE_PATHS.admin.courses, icon: BookOpen },
  { title: 'Cấu hình luyện tập', to: ROUTE_PATHS.admin.configs, icon: SlidersHorizontal },
  { title: 'Thông báo hệ thống', to: ROUTE_PATHS.admin.notifications, icon: Bell },
  { title: 'Nhật ký kiểm toán', to: ROUTE_PATHS.admin.auditLogs, icon: FileText },
] as const

export function AdminOverviewPage() {
  const [counts, setCounts] = useState<OverviewCounts>({
    usersTotal: null,
    semestersTotal: null,
    activeSemesters: null,
    coursesTotal: null,
    activeCourses: null,
  })
  const [runtimeData, setRuntimeData] = useState<RuntimeDiagnosticsDto | null>(null)
  const [runtimeLoading, setRuntimeLoading] = useState(true)
  const [runtimeError, setRuntimeError] = useState<NormalizedProblem | null>(null)
  const [overviewLoading, setOverviewLoading] = useState(true)
  const [overviewError, setOverviewError] = useState<NormalizedProblem | null>(null)
  const overviewRequestRef = useRef(0)
  const runtimeRequestRef = useRef(0)

  const loadRuntimeOnly = useCallback(async () => {
    const runtimeId = ++runtimeRequestRef.current
    setRuntimeLoading(true)
    setRuntimeError(null)
    try {
      const diagnostics = await adminService.getRuntimeDiagnostics()
      if (runtimeId !== runtimeRequestRef.current) return
      setRuntimeData(diagnostics)
    } catch (err: unknown) {
      if (runtimeId !== runtimeRequestRef.current) return
      setRuntimeData(null)
      setRuntimeError(
        extractProblemDetails(err, 'Không tải được thông số runtime hệ thống. Vui lòng thử lại.'),
      )
    } finally {
      if (runtimeId === runtimeRequestRef.current) {
        setRuntimeLoading(false)
      }
    }
  }, [])

  const loadAllOverview = useCallback(async () => {
    const overviewId = ++overviewRequestRef.current
    const runtimeId = ++runtimeRequestRef.current
    setOverviewLoading(true)
    setRuntimeLoading(true)
    setOverviewError(null)
    setRuntimeError(null)

    const [usersRes, semestersRes, coursesRes, runtimeRes] = await Promise.allSettled([
      adminService.getUsers({ page: 1, pageSize: 1 }),
      adminService.getSemesters(),
      adminService.getCourses(true),
      adminService.getRuntimeDiagnostics(),
    ])

    if (overviewId === overviewRequestRef.current) {
      let firstOverviewError: NormalizedProblem | null = null

      const nextCounts: OverviewCounts = {
        usersTotal: usersRes.status === 'fulfilled' ? usersRes.value.totalCount : null,
        semestersTotal: semestersRes.status === 'fulfilled' ? semestersRes.value.length : null,
        activeSemesters:
          semestersRes.status === 'fulfilled'
            ? semestersRes.value.filter((item) => item.isActive).length
            : null,
        coursesTotal: coursesRes.status === 'fulfilled' ? coursesRes.value.length : null,
        activeCourses:
          coursesRes.status === 'fulfilled'
            ? coursesRes.value.filter((item) => item.isActive).length
            : null,
      }
      setCounts(nextCounts)

      if (usersRes.status === 'rejected') {
        firstOverviewError = extractProblemDetails(
          usersRes.reason,
          'Không thể tải đầy đủ số liệu tổng quan người dùng.',
        )
      } else if (semestersRes.status === 'rejected') {
        firstOverviewError = extractProblemDetails(
          semestersRes.reason,
          'Không thể tải danh sách học kỳ.',
        )
      } else if (coursesRes.status === 'rejected') {
        firstOverviewError = extractProblemDetails(
          coursesRes.reason,
          'Không thể tải danh sách môn học.',
        )
      }
      setOverviewError(firstOverviewError)
      setOverviewLoading(false)
    }

    if (runtimeId === runtimeRequestRef.current) {
      if (runtimeRes.status === 'fulfilled') {
        setRuntimeData(runtimeRes.value)
        setRuntimeError(null)
      } else {
        setRuntimeData(null)
        setRuntimeError(
          extractProblemDetails(
            runtimeRes.reason,
            'Không tải được thông số runtime hệ thống. Vui lòng thử lại.',
          ),
        )
      }
      setRuntimeLoading(false)
    }
  }, [])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadAllOverview()
    }, 0)
    return () => {
      window.clearTimeout(timer)
      overviewRequestRef.current += 1
      runtimeRequestRef.current += 1
    }
  }, [loadAllOverview])

  const adminDlqPending =
    runtimeData !== null
      ? runtimeData.deadLetterQueue.byFlowType.practice +
        runtimeData.deadLetterQueue.byFlowType.mockExam +
        runtimeData.deadLetterQueue.byFlowType.inClassExam
      : null

  return (
    <div className={ds.page}>
      <AdminPageHeader
        icon={LayoutDashboard}
        title="Tổng quan & Vận hành hệ thống"
        description="Theo dõi số liệu tài khoản, học thuật, hàng đợi chấm điểm và tình trạng vận hành API."
        actions={
          <button
            type="button"
            onClick={() => void loadAllOverview()}
            disabled={overviewLoading}
            className={adminUi.btnSecondary}
          >
            <RefreshCw
              className={cn('size-4 motion-reduce:animate-none', overviewLoading && 'animate-spin')}
              strokeWidth={1.75}
              aria-hidden="true"
            />
            Làm mới tổng quan
          </button>
        }
      />

      {overviewError ? (
        <AdminStateBanner problem={overviewError} onRetry={() => void loadAllOverview()} />
      ) : null}

      {/* KPI Strip */}
      <section aria-label="Chỉ số tổng quan quản trị" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <AdminStatCard
          label="Tài khoản hệ thống"
          value={counts.usersTotal !== null ? counts.usersTotal.toLocaleString('vi-VN') : '—'}
          icon={UsersRound}
          to={ROUTE_PATHS.admin.users}
          linkLabel="Quản lý người dùng"
        />
        <AdminStatCard
          label="Học kỳ đang hoạt động"
          value={
            counts.activeSemesters !== null && counts.semestersTotal !== null
              ? `${counts.activeSemesters}/${counts.semestersTotal}`
              : '—'
          }
          icon={CalendarDays}
          to={ROUTE_PATHS.admin.semesters}
          linkLabel="Quản lý học kỳ"
        />
        <AdminStatCard
          label="Môn học đang mở"
          value={
            counts.activeCourses !== null && counts.coursesTotal !== null
              ? `${counts.activeCourses}/${counts.coursesTotal}`
              : '—'
          }
          icon={BookOpen}
          to={ROUTE_PATHS.admin.courses}
          linkLabel="Quản lý môn học"
        />
        <AdminStatCard
          label="DLQ chờ xử lý (MF-01/02/05)"
          value={adminDlqPending !== null ? adminDlqPending.toLocaleString('vi-VN') : '—'}
          meta={
            runtimeData
              ? `MF-01: ${runtimeData.deadLetterQueue.byFlowType.practice} · MF-02: ${runtimeData.deadLetterQueue.byFlowType.mockExam} · MF-05: ${runtimeData.deadLetterQueue.byFlowType.inClassExam}`
              : undefined
          }
          icon={AlertTriangle}
          tone={adminDlqPending !== null && adminDlqPending > 0 ? 'warning' : 'default'}
          to={ROUTE_PATHS.admin.dlq}
          linkLabel="Mở hàng đợi lỗi"
        />
      </section>

      {/* Diagnostics Grid */}
      <section
        aria-label="Giám sát hệ thống và bảo mật"
        className="grid gap-4 lg:grid-cols-[minmax(0,1.25fr)_minmax(0,1fr)]"
      >
        <RuntimeDiagnosticsPanel
          data={runtimeData}
          loading={runtimeLoading}
          error={runtimeError}
          onRefresh={() => void loadRuntimeOnly()}
          refreshing={runtimeLoading}
        />
        <AbuseMetricsPanel />
      </section>

      {/* Compact Quick Shortcuts strictly within /admin/* */}
      <section aria-label="Điều hướng nhanh phân hệ quản trị" className={adminUi.cardPadded}>
        <h2 className="text-sm font-bold tracking-tight text-[#0F2854]">Truy cập nhanh</h2>
        <div className="mt-3 grid gap-2.5 sm:grid-cols-2 lg:grid-cols-3">
          {QUICK_NAV_ITEMS.map(({ title, to, icon: Icon }) => (
            <Link
              key={to}
              to={to}
              className="group flex min-h-12 items-center justify-between gap-3 rounded-xl border border-slate-300 bg-[#f8fbfe] px-3.5 py-2.5 text-sm font-semibold text-[#0F2854] transition-colors duration-150 hover:border-[#1C4D8D] hover:bg-[#edf5fd] hover:text-[#1C4D8D] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
            >
              <span className="flex min-w-0 items-center gap-2.5">
                <span className="flex size-8 shrink-0 items-center justify-center rounded-lg border border-[#94b8da] bg-white text-[#1C4D8D]">
                  <Icon className="size-4" strokeWidth={1.75} aria-hidden="true" />
                </span>
                <span className="truncate">{title}</span>
              </span>
              <ArrowRight
                className="size-4 shrink-0 text-slate-400 transition-colors duration-150 group-hover:text-[#1C4D8D] motion-reduce:transition-none"
                strokeWidth={1.75}
                aria-hidden="true"
              />
            </Link>
          ))}
        </div>
      </section>
    </div>
  )
}
