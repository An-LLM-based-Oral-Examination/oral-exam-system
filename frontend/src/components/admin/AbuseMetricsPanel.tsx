import { useEffect, useState } from 'react'
import { apiClient } from '@/services/api.client'

interface AbuseMetrics {
  requests: number
  rejected: number
  loginLockouts: number
  scope: 'api-process'
}

export function AbuseMetricsPanel() {
  const [data, setData] = useState<AbuseMetrics | null>(null)
  const [error, setError] = useState(false)
  const [loading, setLoading] = useState(true)
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    let active = true
    async function load() {
      try {
        const response = await apiClient.get<AbuseMetrics>('/diagnostics/abuse')
        if (active) { setData(response.data); setError(false) }
      } catch {
        if (active) { setData(null); setError(true) }
      } finally {
        if (active) setLoading(false)
      }
    }
    void load()
    return () => { active = false }
  }, [revision])

  return (
    <section aria-label="Chỉ số chống lạm dụng" className="rounded-2xl border border-slate-200 bg-white p-5 text-[#0F2854]">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="font-semibold">Bảo vệ API và đăng nhập</h2>
        <button type="button" disabled={loading} className="rounded-lg border px-3 py-2 text-sm disabled:opacity-50"
          onClick={() => { setLoading(true); setRevision((value) => value + 1) }}>
          Làm mới chỉ số bảo vệ
        </button>
      </div>
      {loading && <p role="status">Đang tải chỉ số…</p>}
      {!loading && error && <p role="alert">Không đọc được chỉ số bảo vệ. Vui lòng thử lại.</p>}
      {!loading && data && <div className="mt-3 flex flex-wrap gap-5 text-sm">
        <span>Yêu cầu: {data.requests}</span>
        <span>Bị giới hạn: {data.rejected}</span>
        <span>Khóa đăng nhập: {data.loginLockouts}</span>
      </div>}
      <p className="mt-2 text-xs text-slate-600">Phạm vi một tiến trình API. Đặt lại khi API khởi động lại; không phải tổng toàn hệ thống.</p>
    </section>
  )
}
