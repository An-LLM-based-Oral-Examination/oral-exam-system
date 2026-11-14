/* eslint-disable react-hooks/set-state-in-effect */
import { useCallback, useEffect, useMemo, useState } from 'react'
import { ArrowRight, ClipboardCheck, RefreshCw, Search } from 'lucide-react'
import { Link } from 'react-router-dom'
import { appealsService } from '@/services/appeals.service'
import type { Appeal } from '@/types/appeal.types'
import { PageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { EmptyState } from '@/components/common/EmptyState'
import { userMessage } from '@/lib/userMessage'

export function ExaminationOfficerAppealListPage() {
  const [appeals, setAppeals] = useState<Appeal[]>([])
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      setAppeals(await appealsService.list(status ? { status } : {}))
    } catch (loadError) {
      setError(userMessage(loadError, 'Không tải được danh sách phúc khảo.'))
    } finally {
      setLoading(false)
    }
  }, [status])
  useEffect(() => {
    void load()
  }, [load])
  const filtered = useMemo(() => {
    const query = search.trim().toLowerCase()
    return appeals.filter(
      (appeal) =>
        !query ||
        [appeal.studentCode, appeal.studentName, appeal.sessionTitle, appeal.reason]
          .join(' ')
          .toLowerCase()
          .includes(query),
    )
  }, [appeals, search])
  return (
    <div className={ds.page}>
      <PageBanner
        icon={ClipboardCheck}
        eyebrow="PHÚC KHẢO MF-04"
        title="Tiếp nhận và phân công phúc khảo"
        description="Thẩm tra đơn, phân công Giảng viên chấm lại và theo dõi trạng thái xử lý."
      />
      <section className={ds.section}>
        <SectionHeader
          icon={ClipboardCheck}
          title="Danh sách đơn"
          badge={filtered.length}
          hint="Dữ liệu lấy từ API appeals của backend"
          action={
            <button
              type="button"
              onClick={() => void load()}
              disabled={loading}
              className={ds.outlineButton}
            >
              <RefreshCw className={loading ? 'size-4 animate-spin' : 'size-4'} /> Làm mới
            </button>
          }
        />
        <div className="flex flex-col gap-3 border-b border-[#d9e8f2] bg-[#F8FBFD] p-5 sm:flex-row">
          <label className="relative flex-1">
            <span className="sr-only">Tìm kiếm phúc khảo</span>
            <Search
              className="pointer-events-none absolute top-3 left-3 size-4 text-[#5b6b84]"
              aria-hidden="true"
            />
            <input
              className={`${ds.field} pl-9`}
              placeholder="MSSV, họ tên, kỳ thi hoặc lý do"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </label>
          <label className="sm:w-52">
            <span className="sr-only">Lọc trạng thái</span>
            <select
              className={ds.field}
              value={status}
              onChange={(event) => setStatus(event.target.value)}
            >
              <option value="">Tất cả trạng thái</option>
              <option value="PENDING">PENDING</option>
              <option value="IN_REVIEW">IN_REVIEW</option>
              <option value="APPROVED">APPROVED</option>
              <option value="REJECTED">REJECTED</option>
            </select>
          </label>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[860px] text-left text-sm">
            <thead className="bg-white text-xs font-bold text-[#5b6b84] uppercase">
              <tr>
                <th className="px-5 py-3">Sinh viên</th>
                <th className="px-5 py-3">Kỳ thi</th>
                <th className="px-5 py-3">Điểm gốc</th>
                <th className="px-5 py-3">Trạng thái</th>
                <th className="px-5 py-3">Phân công</th>
                <th className="px-5 py-3 text-right"> </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#d9e8f2]">
              {filtered.map((appeal) => (
                <tr key={appeal.id} className="hover:bg-[#F8FBFD]">
                  <td className="px-5 py-3">
                    <strong className="block text-[#0F2854]">
                      {appeal.studentCode || appeal.studentId}
                    </strong>
                    <span className="text-xs text-[#5b6b84]">{appeal.studentName}</span>
                  </td>
                  <td className="px-5 py-3 text-[#5b6b84]">
                    {appeal.sessionTitle || appeal.sessionId}
                  </td>
                  <td className="px-5 py-3 text-[#5b6b84]">{appeal.originalScore ?? '—'}</td>
                  <td className="px-5 py-3">
                    <span
                      className={
                        appeal.status.toUpperCase() === 'REJECTED' ? ds.dangerChip : ds.infoChip
                      }
                    >
                      {appeal.status}
                    </span>
                  </td>
                  <td className="px-5 py-3 text-[#5b6b84]">
                    {appeal.assignedToName ||
                      (appeal.assignedTo ? appeal.assignedTo : 'Chưa phân công')}
                  </td>
                  <td className="px-5 py-3 text-right">
                    <Link
                      to={`/examination-officer/appeals/${appeal.id}`}
                      className={ds.outlineButton}
                    >
                      Mở <ArrowRight className="size-3.5" />
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!loading && filtered.length === 0 ? (
          <div className="p-5">
            <EmptyState
              compact
              icon={ClipboardCheck}
              title="Không có đơn phù hợp"
              description="Thử thay đổi bộ lọc hoặc chờ sinh viên gửi đơn mới."
            />
          </div>
        ) : null}
        {error ? (
          <div
            role="alert"
            className="m-5 rounded-xl border border-[#f3c2c8] bg-[#fdecec] p-4 text-sm font-semibold text-[#a43d4b]"
          >
            {error}
          </div>
        ) : null}
      </section>
    </div>
  )
}
