import { Activity, AlertTriangle, ArrowRight, Cpu, Gauge, RefreshCw, ServerCog } from 'lucide-react'
import { Link } from 'react-router-dom'
import { LoadingState } from '@/components/common/LoadingState'
import type { NormalizedProblem } from '@/lib/problemDetails'
import { cn } from '@/lib/utils'
import type { RuntimeDiagnosticsDto } from '@/services/admin.service'
import { ROUTE_PATHS } from '@/routes/routePaths'
import { AdminStateBanner } from '@/pages/admin/components/AdminStateBanner'

interface RuntimeDiagnosticsPanelProps {
  data: RuntimeDiagnosticsDto | null
  loading: boolean
  error: NormalizedProblem | null
  onRefresh: () => void
  refreshing?: boolean
}

function formatTimestamp(raw: string): string {
  const parsed = new Date(raw)
  return Number.isNaN(parsed.getTime()) ? raw : parsed.toLocaleString('vi-VN')
}

export function RuntimeDiagnosticsPanel({
  data,
  loading,
  error,
  onRefresh,
  refreshing = false,
}: RuntimeDiagnosticsPanelProps) {
  const queueCount = data?.boundedChannels.gradingQueue.count ?? 0
  const queueCapacity = data?.boundedChannels.gradingQueue.capacity ?? 0
  const utilizationPercent =
    queueCapacity > 0 ? Math.min(100, Math.round((queueCount / queueCapacity) * 100)) : 0
  const barTone =
    utilizationPercent >= 90
      ? 'bg-[#c2415b]'
      : utilizationPercent >= 75
        ? 'bg-[#9a6b12]'
        : 'bg-[#1C4D8D]'

  const adminDlqTotal = data
    ? data.deadLetterQueue.byFlowType.practice + data.deadLetterQueue.byFlowType.mockExam
    : 0

  return (
    <section
      aria-label="Chẩn đoán vận hành hệ thống"
      className="rounded-2xl border border-[#0F2854]/10 bg-white p-5 text-[#0F2854]"
    >
      <div className="flex flex-wrap items-start justify-between gap-3 border-b border-[#0F2854]/8 pb-4">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <span className="flex size-8 items-center justify-center rounded-lg border border-[#0F2854]/10 bg-[#F4FBFD] text-[#1C4D8D]">
              <Activity className="size-4" strokeWidth={1.5} aria-hidden="true" />
            </span>
            <h2 className="text-base font-semibold text-[#0F2854]">
              Giám sát Runtime &amp; Hàng đợi chấm
            </h2>
          </div>
          {data ? (
            <p className="mt-1.5 text-xs text-[#5b6b84] tabular-nums">
              Cập nhật: {formatTimestamp(data.timestamp)} · Uptime: {data.uptime}
            </p>
          ) : (
            <p className="mt-1.5 text-xs text-[#5b6b84]">
              Thông số tiến trình API hiện tại (BoundedChannel, độ trễ, bộ nhớ và DLQ).
            </p>
          )}
        </div>

        <button
          type="button"
          onClick={onRefresh}
          disabled={loading || refreshing}
          className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-[#0F2854]/15 bg-white px-3.5 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50 motion-reduce:transition-none"
        >
          <RefreshCw
            className={cn('size-4 motion-reduce:animate-none', (loading || refreshing) && 'animate-spin')}
            strokeWidth={1.5}
            aria-hidden="true"
          />
          Làm mới runtime
        </button>
      </div>

      {loading && !data ? (
        <LoadingState label="Đang tải chỉ số runtime hệ thống…" className="min-h-40" />
      ) : null}

      {!loading && error ? (
        <div className="mt-4">
          <AdminStateBanner problem={error} onRetry={onRefresh} />
        </div>
      ) : null}

      {data && !error ? (
        <div className="mt-4 space-y-4">
          {/* BoundedChannel Grading Queue */}
          <div className="rounded-xl border border-[#0F2854]/10 bg-[#F8FBFD] p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="inline-flex items-center gap-1.5 text-xs font-medium text-[#0F2854]">
                <ServerCog className="size-4 text-[#1C4D8D]" strokeWidth={1.5} aria-hidden="true" />
                Hàng đợi chấm điểm (BoundedChannel)
              </span>
              <span className="font-mono text-sm font-semibold text-[#0F2854] tabular-nums">
                {queueCount}/{queueCapacity} ({utilizationPercent}%)
              </span>
            </div>
            <div
              role="progressbar"
              aria-label="Mức sử dụng hàng đợi chấm điểm"
              aria-valuemin={0}
              aria-valuemax={Math.max(1, queueCapacity)}
              aria-valuenow={queueCount}
              className="mt-2.5 h-2 w-full overflow-hidden rounded-full bg-[#0F2854]/10"
            >
              <div
                className={cn('h-full rounded-full transition-[width] duration-150 motion-reduce:transition-none', barTone)}
                style={{ width: `${utilizationPercent}%` }}
              />
            </div>
          </div>

          {/* Performance 4-metric grid */}
          <div>
            <p className="mb-2 inline-flex items-center gap-1.5 text-xs font-medium text-[#5b6b84]">
              <Gauge className="size-3.5 text-[#1C4D8D]" strokeWidth={1.5} aria-hidden="true" />
              Hiệu năng HTTP API
            </p>
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              <div className="rounded-xl border border-[#0F2854]/10 bg-white p-3">
                <p className="text-xs text-[#5b6b84]">Tổng yêu cầu</p>
                <p className="mt-1 text-base font-semibold text-[#0F2854] tabular-nums">
                  {data.performance.totalRequests.toLocaleString('vi-VN')}
                </p>
              </div>
              <div className="rounded-xl border border-[#0F2854]/10 bg-white p-3">
                <p className="text-xs text-[#5b6b84]">Lỗi 5xx</p>
                <p
                  className={cn(
                    'mt-1 text-base font-semibold tabular-nums',
                    data.performance.serverErrors5xx > 0 ? 'text-[#c2415b]' : 'text-[#0F2854]',
                  )}
                >
                  {data.performance.serverErrors5xx} ({data.performance.serverErrorRatePercent.toFixed(2)}%)
                </p>
              </div>
              <div className="rounded-xl border border-[#0F2854]/10 bg-white p-3">
                <p className="text-xs text-[#5b6b84]">Độ trễ TB</p>
                <p className="mt-1 text-base font-semibold text-[#0F2854] tabular-nums">
                  {data.performance.averageLatencyMs.toFixed(2)} ms
                </p>
              </div>
              <div className="rounded-xl border border-[#0F2854]/10 bg-white p-3">
                <p className="text-xs text-[#5b6b84]">Độ trễ P95</p>
                <p className="mt-1 text-base font-semibold text-[#0F2854] tabular-nums">
                  {data.performance.p95LatencyMs.toFixed(2)} ms
                </p>
              </div>
            </div>
          </div>

          {/* System memory & threads */}
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <div className="flex items-center justify-between rounded-xl border border-[#0F2854]/10 bg-white px-3.5 py-2.5 text-xs">
              <span className="inline-flex items-center gap-1.5 text-[#5b6b84]">
                <Cpu className="size-3.5 text-[#1C4D8D]" strokeWidth={1.5} aria-hidden="true" />
                Working Set
              </span>
              <span className="font-semibold text-[#0F2854] tabular-nums">
                {data.system.workingSetMb.toFixed(1)} MB
              </span>
            </div>
            <div className="flex items-center justify-between rounded-xl border border-[#0F2854]/10 bg-white px-3.5 py-2.5 text-xs">
              <span className="text-[#5b6b84]">GC Heap</span>
              <span className="font-semibold text-[#0F2854] tabular-nums">
                {data.system.gcTotalMemoryMb.toFixed(1)} MB
              </span>
            </div>
            <div className="flex items-center justify-between rounded-xl border border-[#0F2854]/10 bg-white px-3.5 py-2.5 text-xs">
              <span className="text-[#5b6b84]">Threads</span>
              <span className="font-semibold text-[#0F2854] tabular-nums">
                {data.system.threadCount}
              </span>
            </div>
          </div>

          {/* DLQ MF-01 / MF-02 Breakdown */}
          <div className="flex flex-col gap-3 rounded-xl border border-[#0F2854]/10 bg-[#F8FBFD] p-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="space-y-1">
              <p className="inline-flex items-center gap-1.5 text-xs font-medium text-[#0F2854]">
                <AlertTriangle className="size-3.5 text-[#9a6b12]" strokeWidth={1.5} aria-hidden="true" />
                Tác vụ DLQ chờ xử lý thuộc phạm vi Admin (MF-01/02):{' '}
                <strong className="font-semibold tabular-nums">{adminDlqTotal}</strong>
              </p>
              <div className="flex flex-wrap gap-3 text-xs text-[#5b6b84] tabular-nums">
                <span>MF-01 (Luyện tập): {data.deadLetterQueue.byFlowType.practice}</span>
                <span>MF-02 (Thi thử): {data.deadLetterQueue.byFlowType.mockExam}</span>
                <span>Tổng pending (MF-01/02/05): {data.deadLetterQueue.totalPending}</span>
              </div>
            </div>
            <Link
              to={ROUTE_PATHS.admin.dlq}
              className="inline-flex min-h-11 shrink-0 items-center justify-center gap-1.5 rounded-xl border border-[#0F2854]/15 bg-white px-3.5 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
            >
              Mở hàng đợi lỗi MF-01/02
              <ArrowRight className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
            </Link>
          </div>
        </div>
      ) : null}
    </section>
  )
}
