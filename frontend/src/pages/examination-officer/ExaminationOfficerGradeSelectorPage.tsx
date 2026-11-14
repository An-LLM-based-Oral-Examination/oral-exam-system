/* eslint-disable react-hooks/set-state-in-effect */
import { useEffect, useState } from 'react'
import { ArrowRight, CalendarDays, FileBarChart2, RefreshCw } from 'lucide-react'
import { Link } from 'react-router-dom'
import { officialExamService } from '@/services/officialExam.service'
import type { ExamShift } from '@/types/officialExam.types'
import { PageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { EmptyState } from '@/components/common/EmptyState'
import { userMessage } from '@/lib/userMessage'

export function ExaminationOfficerGradeSelectorPage() {
  const [shifts, setShifts] = useState<ExamShift[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const load = async () => {
    setLoading(true)
    setError('')
    try {
      setShifts(await officialExamService.getShifts())
    } catch (loadError) {
      setError(userMessage(loadError, 'Không tải được danh sách ca thi.'))
    } finally {
      setLoading(false)
    }
  }
  useEffect(() => {
    void load()
  }, [])
  return (
    <div className={ds.page}>
      <PageBanner
        icon={FileBarChart2}
        eyebrow="CÔNG BỐ ĐIỂM MF-04"
        title="Chọn ca thi để xử lý điểm"
        description="Kiểm tra bằng chứng, công bố điểm một lần và tải báo cáo từ API thi thật."
      />
      <section className={ds.section}>
        <SectionHeader
          icon={CalendarDays}
          title="Ca thi"
          hint="Chọn ca cần kiểm tra điểm"
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
        <div className="grid gap-3 p-5 md:grid-cols-2 xl:grid-cols-3">
          {shifts.map((shift) => (
            <article key={shift.id} className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-4">
              <div className="flex items-start justify-between gap-2">
                <div>
                  <h3 className="font-bold text-[#0F2854]">{shift.shiftName || shift.id}</h3>
                  <p className="mt-1 text-xs text-[#5b6b84]">
                    {shift.roomLab} · {shift.startTime}
                  </p>
                </div>
                <span className={ds.infoChip}>{shift.status}</span>
              </div>
              <Link to={`/examination-officer/grades/${shift.id}`} className={cnButton}>
                Mở công bố điểm <ArrowRight className="size-4" />
              </Link>
            </article>
          ))}
          {!loading && shifts.length === 0 ? (
            <EmptyState
              compact
              icon={FileBarChart2}
              title="Chưa có ca thi"
              description="Các ca thi sẽ xuất hiện sau khi được tạo."
            />
          ) : null}
          {error ? (
            <div
              role="alert"
              className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] p-4 text-sm font-semibold text-[#a43d4b]"
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
