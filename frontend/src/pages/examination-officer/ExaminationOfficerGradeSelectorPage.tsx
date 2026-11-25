/* eslint-disable react-hooks/set-state-in-effect */
import { useEffect, useMemo, useState } from 'react'
import { ArrowRight, CalendarDays, FileBarChart2, RefreshCw, Users } from 'lucide-react'
import { Link } from 'react-router-dom'
import { officialExamService } from '@/services/officialExam.service'
import type { ExamShift, OfficialExamSession } from '@/types/officialExam.types'
import { PageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { SearchField } from '@/components/common/SearchField'
import {
  formatEoTimeRange,
  getShiftStatusMeta,
} from '@/components/examination-officer/eoFormatters'
import { userMessage } from '@/lib/userMessage'

export function ExaminationOfficerGradeSelectorPage() {
  const [shifts, setShifts] = useState<ExamShift[]>([])
  const [sessions, setSessions] = useState<OfficialExamSession[]>([])
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('ALL')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const load = async () => {
    setLoading(true)
    setError('')
    try {
      const [loadedShifts, loadedSessions] = await Promise.all([
        officialExamService.getShifts(),
        officialExamService.getSessions().catch(() => [] as OfficialExamSession[]),
      ])
      setShifts(loadedShifts)
      setSessions(loadedSessions)
    } catch (loadError) {
      setError(userMessage(loadError, 'Không tải được danh sách ca thi.'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  const sessionsById = useMemo(
    () => new Map(sessions.map((item) => [item.id, item])),
    [sessions],
  )

  const filteredShifts = useMemo(() => {
    const term = search.trim().toLowerCase()
    return shifts.filter((shift) => {
      const sessionTitle = sessionsById.get(shift.sessionId)?.title ?? ''
      const matchesStatus =
        statusFilter === 'ALL'
          ? true
          : statusFilter === 'published'
            ? Boolean(shift.isGradesPublished)
            : statusFilter === 'unpublished'
              ? !shift.isGradesPublished && shift.status.toLowerCase() === 'completed'
              : shift.status.toLowerCase() === statusFilter
      const matchesSearch =
        !term ||
        [shift.shiftName, shift.roomLab, sessionTitle]
          .filter(Boolean)
          .join(' ')
          .toLowerCase()
          .includes(term)
      return matchesStatus && matchesSearch
    })
  }, [search, sessionsById, shifts, statusFilter])

  return (
    <div className={ds.page}>
      <PageBanner
        icon={FileBarChart2}
        eyebrow="CÔNG BỐ ĐIỂM MF-04"
        title="Chọn ca thi để công bố điểm"
        description="Kiểm tra kết quả hậu kiểm, kích hoạt khóa điểm một chiều (One-Way Lock) và tải biên bản Excel/PDF."
      />
      <section className={ds.section}>
        <SectionHeader
          icon={CalendarDays}
          title="Danh sách ca thi"
          badge={filteredShifts.length}
          hint="Chọn ca đã kết thúc để kiểm tra và công bố điểm"
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

        <div className="flex flex-wrap items-center gap-2.5 border-b border-[#d9e8f2] px-5 py-3">
          <div className="min-w-[220px] flex-1">
            <SearchField
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Tìm theo tên ca, phòng Lab, kỳ thi…"
            />
          </div>
          <select
            aria-label="Lọc trạng thái công bố điểm"
            className={cn(ds.field, 'w-auto min-w-[190px]')}
            value={statusFilter}
            onChange={(event) => setStatusFilter(event.target.value)}
          >
            <option value="ALL">Tất cả ca thi</option>
            <option value="unpublished">Đã kết thúc · Chờ công bố</option>
            <option value="published">Đã công bố điểm</option>
            <option value="in_progress">Đang diễn ra</option>
            <option value="scheduled">Đã lên lịch</option>
          </select>
        </div>

        <div className="grid gap-3 p-5 md:grid-cols-2 xl:grid-cols-3">
          {filteredShifts.map((shift) => {
            const statusMeta = getShiftStatusMeta(shift.status)
            const sessionTitle = sessionsById.get(shift.sessionId)?.title
            return (
              <article
                key={shift.id}
                className="flex flex-col justify-between rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-4"
              >
                <div>
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <h3 className="truncate font-bold text-[#0F2854]">
                        {shift.shiftName || shift.id}
                      </h3>
                      {sessionTitle ? (
                        <p className="mt-0.5 truncate text-xs font-medium text-[#245997]">
                          {sessionTitle}
                        </p>
                      ) : null}
                    </div>
                    <span className={shift.isGradesPublished ? ds.successChip : statusMeta.chipClass}>
                      {shift.isGradesPublished ? 'Đã công bố' : statusMeta.label}
                    </span>
                  </div>
                  <p className="mt-2 text-xs text-[#5b6b84]">
                    Phòng <strong className="text-[#0F2854]">{shift.roomLab}</strong> ·{' '}
                    {formatEoTimeRange(shift.startTime, shift.endTime)}
                  </p>
                  <p className="mt-1 flex items-center gap-1.5 text-xs text-[#5b6b84]">
                    <Users className="size-3.5 text-[#1C4D8D]" aria-hidden="true" />
                    <span>
                      {shift.assignedTicketsCount ?? 0}/{shift.maxSeats ?? 24} sinh viên
                    </span>
                  </p>
                </div>
                <Link to={`/examination-officer/grades/${shift.id}`} className={cnButton}>
                  <span>{shift.isGradesPublished ? 'Xem bảng điểm & xuất file' : 'Mở công bố điểm'}</span>
                  <ArrowRight className="size-4" />
                </Link>
              </article>
            )
          })}
          {!loading && filteredShifts.length === 0 ? (
            <div className="col-span-full">
              <EmptyState
                compact
                icon={FileBarChart2}
                title={shifts.length === 0 ? 'Chưa có ca thi' : 'Không tìm thấy ca thi phù hợp'}
                description={
                  shifts.length === 0
                    ? 'Các ca thi sẽ xuất hiện sau khi được tạo.'
                    : 'Thử thay đổi bộ lọc hoặc từ khóa tìm kiếm.'
                }
              />
            </div>
          ) : null}
          {error ? (
            <div
              role="alert"
              className="col-span-full rounded-xl border border-[#f3c2c8] bg-[#fdecec] p-4 text-sm font-semibold text-[#a43d4b]"
            >
              {error}
            </div>
          ) : null}
        </div>
      </section>
    </div>
  )
}

const cnButton =
  'mt-4 inline-flex min-h-10 w-full items-center justify-between gap-2 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#E7F3FB] focus-visible:outline-2 focus-visible:outline-[#1C4D8D]'
