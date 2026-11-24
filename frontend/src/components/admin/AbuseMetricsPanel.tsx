import { useCallback, useEffect, useState } from 'react'
import { RefreshCw, ShieldCheck } from 'lucide-react'
import { apiClient } from '@/services/api.client'
import { extractProblemDetails, type NormalizedProblem } from '@/lib/problemDetails'
import { LoadingState } from '@/components/common/LoadingState'
import { AdminStateBanner } from '@/pages/admin/components/AdminStateBanner'
import { adminUi } from '@/pages/admin/components/adminUi'
import { cn } from '@/lib/utils'

interface AbuseMetrics {
  requests: number
  rejected: number
  loginLockouts: number
  scope: 'api-process'
}

export function AbuseMetricsPanel() {
  const [data, setData] = useState<AbuseMetrics | null>(null)
  const [error, setError] = useState<NormalizedProblem | null>(null)
  const [loading, setLoading] = useState(true)
  const [revision, setRevision] = useState(0)

  const handleRefresh = useCallback(() => {
    setLoading(true)
    setError(null)
    setRevision((value) => value + 1)
  }, [])

  useEffect(() => {
    let active = true
    async function load() {
      try {
        const response = await apiClient.get<AbuseMetrics>('/diagnostics/abuse')
        if (active) {
          setData(response.data)
          setError(null)
        }
      } catch (err: unknown) {
        if (active) {
          setData(null)
          setError(extractProblemDetails(err, 'Không đọc được chỉ số bảo vệ. Vui lòng thử lại.'))
        }
      } finally {
        if (active) setLoading(false)
      }
    }
    void load()
    return () => {
      active = false
    }
  }, [revision])

  return (
    <section
      aria-label="Chỉ số chống lạm dụng"
      className={cn(adminUi.cardPadded, 'flex flex-col justify-between text-[#0F2854]')}
    >
      <div>
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 pb-4">
          <div className="flex items-center gap-2.5">
            <span className="flex size-8 items-center justify-center rounded-lg border border-[#94b8da] bg-[#edf5fd] text-[#1C4D8D]">
              <ShieldCheck className="size-4" strokeWidth={1.75} aria-hidden="true" />
            </span>
            <h2 className="text-base font-bold text-[#0F2854]">Bảo vệ API và đăng nhập</h2>
          </div>

          <button
            type="button"
            disabled={loading}
            className={adminUi.btnSecondary}
            onClick={handleRefresh}
          >
            <RefreshCw
              className={cn('size-4 motion-reduce:animate-none', loading && 'animate-spin')}
              strokeWidth={1.75}
              aria-hidden="true"
            />
            Làm mới chỉ số bảo vệ
          </button>
        </div>

        {loading && <LoadingState label="Đang tải chỉ số…" className="min-h-28" />}

        {!loading && error && (
          <div className="mt-4">
            <AdminStateBanner problem={error} onRetry={handleRefresh} />
          </div>
        )}

        {!loading && !error && data && (
          <dl className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
            <div className="rounded-xl border border-slate-300 bg-[#f7fafd] p-4">
              <dt className="text-xs font-semibold text-slate-600">Yêu cầu</dt>
              <dd className="mt-1.5 text-2xl font-bold text-[#0F2854] tabular-nums">
                {data.requests}
              </dd>
            </div>
            <div className="rounded-xl border border-slate-300 bg-[#f7fafd] p-4">
              <dt className="text-xs font-semibold text-slate-600">Bị giới hạn</dt>
              <dd
                className={cn(
                  'mt-1.5 text-2xl font-bold tabular-nums',
                  data.rejected > 0 ? 'text-amber-800' : 'text-[#0F2854]',
                )}
              >
                {data.rejected}
              </dd>
            </div>
            <div className="rounded-xl border border-slate-300 bg-[#f7fafd] p-4">
              <dt className="text-xs font-semibold text-slate-600">Khóa đăng nhập</dt>
              <dd
                className={cn(
                  'mt-1.5 text-2xl font-bold tabular-nums',
                  data.loginLockouts > 0 ? 'text-rose-700' : 'text-[#0F2854]',
                )}
              >
                {data.loginLockouts}
              </dd>
            </div>
          </dl>
        )}
      </div>

      <p className="mt-4 text-xs text-slate-600">
        Phạm vi một tiến trình API. Đặt lại khi API khởi động lại; không phải tổng toàn hệ thống.
      </p>
    </section>
  )
}
