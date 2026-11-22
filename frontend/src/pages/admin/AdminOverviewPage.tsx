import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  AlertTriangle,
  ArrowRight,
  Bell,
  BookOpen,
  CalendarDays,
  FileText,
  RefreshCw,
  ShieldAlert,
  SlidersHorizontal,
  UsersRound,
} from 'lucide-react'
import { AbuseMetricsPanel } from '@/components/admin/AbuseMetricsPanel'
import { RuntimeDiagnosticsPanel } from '@/components/admin/RuntimeDiagnosticsPanel'
import { BannerFact, PageBanner } from '@/design/ui'
import { ds } from '@/design/system'
import { extractProblemDetails, type NormalizedProblem } from '@/lib/problemDetails'
import { ROUTE_PATHS } from '@/routes/routePaths'
import { adminService, type RuntimeDiagnosticsDto } from '@/services/admin.service'
import { AdminStatCard } from '@/pages/admin/components/AdminStatCard'
import { AdminStateBanner } from '@/pages/admin/components/AdminStateBanner'

interface OverviewCounts {
  usersTotal: number | null
  semestersTotal: number | null
  activeSemesters: number | null
  coursesTotal: number | null
  activeCourses: number | null
}

const QUICK_NAV_ITEMS = [
  {
    title: 'Quản trị người dùng',
    description: 'Danh sách tài khoản, tạo user buộc đổi mật khẩu, khóa/mở và gán 6 vai trò RBAC.',
    to: ROUTE_PATHS.admin.users,
    icon: UsersRound,
  },
  {
    title: 'Học kỳ & Môn mở',
    description: 'Tạo/sửa học kỳ (không xóa) và đồng bộ danh mục môn mở trong kỳ.',
    to: ROUTE_PATHS.admin.semesters,
    icon: CalendarDays,
  },
  {
    title: 'Môn học, CTĐT & CLO',
    description: 'Quản lý môn học, hạn ngạch MF-02, Trưởng bộ môn và chuẩn đầu ra 3NF.',
    to: ROUTE_PATHS.admin.courses,
    icon: BookOpen,
  },
  {
    title: 'Cấu hình MF-01',
    description: 'Điều chỉnh tham số luyện tập tương tác trong bảng system_configs.',
    to: ROUTE_PATHS.admin.configs,
    icon: SlidersHorizontal,
  },
  {
    title: 'Thông báo 3NF',
    description: 'Phát thông báo theo phạm vi toàn hệ thống, vai trò hoặc tài khoản cụ thể.',
    to: ROUTE_PATHS.admin.notifications,
    icon: Bell,
  },
  {
    title: 'Nhật ký kiểm toán',
    description: 'Tra cứu Audit Logs theo thực thể, hành động, người thực hiện và diff dữ liệu.',
    to: ROUTE_PATHS.admin.auditLogs,
    icon: FileText,
  },
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
      <PageBanner
        variant="calm"
        icon={ShieldAlert}
        eyebrow="Cổng quản trị hệ thống · 6 nhóm nghiệp vụ"
        title="Tổng quan & Vận hành hệ thống"
        description="Theo dõi trạng thái tài khoản, học kỳ, môn học, hàng đợi chấm điểm, chỉ số bảo vệ API và xử lý sự cố MF-01/MF-02/MF-05."
        extras={
          <>
            <BannerFact icon={UsersRound} surface="calm">
              Phạm vi Admin chuẩn RBAC
            </BannerFact>
            <BannerFact icon={AlertTriangle} tone="amber" surface="calm">
              DLQ giới hạn MF-01 / MF-02 / MF-05
            </BannerFact>
          </>
        }
      />

      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-xs text-[#5b6b84]">
          Dữ liệu được đồng bộ trực tiếp từ API quản trị và tiến trình runtime.
        </p>
        <button
          type="button"
          onClick={() => void loadAllOverview()}
          disabled={overviewLoading}
          className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-[#0F2854]/15 bg-white px-4 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50 motion-reduce:transition-none"
        >
          <RefreshCw
            className={`size-4 motion-reduce:animate-none ${overviewLoading ? 'animate-spin' : ''}`}
            strokeWidth={1.5}
            aria-hidden="true"
          />
          Làm mới tổng quan
        </button>
      </div>

      {overviewError ? (
        <AdminStateBanner problem={overviewError} onRetry={() => void loadAllOverview()} />
      ) : null}

      {/* KPI Strip */}
      <section aria-label="Chỉ số tổng quan quản trị" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <AdminStatCard
          label="Tài khoản hệ thống"
          value={counts.usersTotal !== null ? counts.usersTotal.toLocaleString('vi-VN') : '—'}
          meta="6 vai trò RBAC"
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
          meta="Không cho phép xóa học kỳ"
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
          meta="Gán Trưởng BM & hạn ngạch MF-02"
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
              : 'Chưa có dữ liệu runtime'
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

      {/* Quick Navigation strictly within /admin/* */}
      <section aria-label="Điều hướng nhanh phân hệ quản trị" className="space-y-3">
        <h2 className="text-base font-semibold text-[#0F2854]">Phân hệ quản trị</h2>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {QUICK_NAV_ITEMS.map(({ title, description, to, icon: Icon }) => (
            <Link
              key={to}
              to={to}
              className="group flex flex-col justify-between rounded-2xl border border-[#0F2854]/10 bg-white p-5 transition-colors duration-150 hover:border-[#1C4D8D]/40 hover:bg-[#F8FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
            >
              <div>
                <div className="flex items-center gap-3">
                  <span className="flex size-9 shrink-0 items-center justify-center rounded-xl border border-[#0F2854]/10 bg-[#F4FBFD] text-[#1C4D8D]">
                    <Icon className="size-[18px]" strokeWidth={1.5} aria-hidden="true" />
                  </span>
                  <h3 className="text-sm font-semibold text-[#0F2854]">{title}</h3>
                </div>
                <p className="mt-2.5 text-xs leading-relaxed text-[#5b6b84]">{description}</p>
              </div>
              <span className="mt-4 inline-flex min-h-11 items-center gap-1.5 text-xs font-medium text-[#1C4D8D] sm:min-h-6">
                Mở phân hệ
                <ArrowRight className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
              </span>
            </Link>
          ))}
        </div>
      </section>
    </div>
  )
}
