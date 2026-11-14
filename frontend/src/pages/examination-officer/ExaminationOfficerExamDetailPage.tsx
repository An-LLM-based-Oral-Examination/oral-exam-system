/* eslint-disable react-hooks/set-state-in-effect */
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  ArrowLeft,
  CalendarDays,
  CheckCircle2,
  RefreshCw,
  ScanSearch,
  TicketCheck,
} from 'lucide-react'
import { officialExamService } from '@/services/officialExam.service'
import type { ExamSet, ExamShift, OfficialExamSession } from '@/types/officialExam.types'
import { PageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { EmptyState } from '@/components/common/EmptyState'
import { userMessage } from '@/lib/userMessage'
import { toast } from '@/components/common/toastStore'

export function ExaminationOfficerExamDetailPage() {
  const { sessionId = '' } = useParams<{ sessionId: string }>()
  const [session, setSession] = useState<OfficialExamSession | null>(null)
  const [shifts, setShifts] = useState<ExamShift[]>([])
  const [examSets, setExamSets] = useState<ExamSet[]>([])
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const [sessions, shiftResult] = await Promise.all([
        officialExamService.getSessions(),
        officialExamService.getShifts({ sessionId }),
      ])
      const currentSession = sessions.find((item) => item.id === sessionId) ?? null
      setSession(currentSession)
      setShifts(shiftResult)
      setExamSets(
        currentSession
          ? await officialExamService.getExamSets({
              courseId: currentSession.courseId,
              sessionId,
              status: 'HANDED_OVER',
            })
          : [],
      )
    } catch (loadError) {
      setError(userMessage(loadError, 'Không tải được chi tiết kỳ thi.'))
    } finally {
      setLoading(false)
    }
  }, [sessionId])
  const publishShift = async (shiftId: string) => {
    if (!window.confirm('Xuất bản đề cho ca thi này? Sau khi xuất bản, Kiosk có thể tải đề.'))
      return
    try {
      const result = await officialExamService.publishShift(shiftId)
      setShifts((current) =>
        current.map((shift) => (shift.id === shiftId ? { ...shift, status: 'PUBLISHED' } : shift)),
      )
      toast.success(result.message || 'Đã xuất bản đề cho ca thi.')
    } catch (publishError) {
      const message = userMessage(publishError, 'Không thể xuất bản đề cho ca thi.')
      setError(message)
      toast.error(message)
    }
  }
  useEffect(() => {
    void load()
  }, [load])
  const sessionTitle = useMemo(
    () => session?.title || sessionId || 'Kỳ thi chính thức',
    [session, sessionId],
  )
  return (
    <div className={ds.page}>
      <PageBanner
        icon={CalendarDays}
        eyebrow="CHI TIẾT KỲ THI"
        title={sessionTitle}
        description="Theo dõi các ca thi thuộc kỳ thi chính thức và điều hướng đến màn hình vận hành."
      />
      <div className="flex flex-wrap gap-2">
        <Link to="/examination-officer/exams" className={ds.outlineButton}>
          <ArrowLeft className="size-4" /> Danh sách kỳ thi
        </Link>
        <button
          type="button"
          onClick={() => void load()}
          disabled={loading}
          className={ds.outlineButton}
        >
          <RefreshCw className={loading ? 'size-4 animate-spin' : 'size-4'} /> Làm mới
        </button>
      </div>
      {error ? (
        <div
          role="alert"
          className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
        >
          {error}
        </div>
      ) : null}
      <section className={ds.section}>
        <SectionHeader
          icon={CalendarDays}
          title="Các ca thi"
          badge={shifts.length}
          hint={session ? `${session.examDate} · ${session.status}` : 'Đang tải thông tin kỳ thi'}
        />
        <div className="grid gap-3 p-5 md:grid-cols-2">
          {shifts.map((shift) => (
            <article key={shift.id} className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-4">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <h3 className="font-bold text-[#0F2854]">{shift.shiftName || shift.id}</h3>
                  <p className="mt-1 text-xs text-[#5b6b84]">
                    {shift.roomLab} · {shift.startTime}
                  </p>
                  <p className="mt-1 text-xs text-[#5b6b84]">
                    Giám thị: {shift.proctorName || shift.proctorUserId || 'Chưa phân công'} · Giám
                    khảo: {shift.examinerName || shift.examinerUserId || 'Chưa phân công'}
                  </p>
                </div>
                <span className={ds.infoChip}>{shift.status}</span>
              </div>
              <div className="mt-4 flex flex-wrap gap-2">
                <Link to={`/examination-officer/monitor/${shift.id}`} className={ds.outlineButton}>
                  <ScanSearch className="size-4" /> Giám sát
                </Link>
                <Link to={`/examination-officer/grades/${shift.id}`} className={ds.outlineButton}>
                  <TicketCheck className="size-4" /> Điểm
                </Link>
                <button
                  type="button"
                  onClick={() => void publishShift(shift.id)}
                  disabled={shift.status.toUpperCase() === 'PUBLISHED'}
                  className={ds.primaryButton}
                >
                  <CheckCircle2 className="size-4" />{' '}
                  {shift.status.toUpperCase() === 'PUBLISHED' ? 'Đã xuất bản' : 'Xuất bản đề'}
                </button>
              </div>
            </article>
          ))}
          {!loading && shifts.length === 0 ? (
            <EmptyState
              compact
              icon={CalendarDays}
              title="Chưa có ca thi"
              description="Kỳ thi này chưa có ca thi được cấu hình."
            />
          ) : null}
        </div>
      </section>
      <section className={ds.section}>
        <SectionHeader
          icon={TicketCheck}
          title="Bộ đề đã bàn giao"
          badge={examSets.length}
          hint="Chỉ hiển thị bộ đề HANDED_OVER trả về từ backend."
        />
        <div className="grid gap-3 p-5 md:grid-cols-2 xl:grid-cols-3">
          {examSets.map((examSet) => (
            <article
              key={examSet.id}
              className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-4"
            >
              <div className="flex items-center justify-between gap-3">
                <strong className="text-[#0F2854]">{examSet.setCode}</strong>
                <span className={ds.successChip}>{examSet.status}</span>
              </div>
              <p className="mt-2 text-xs text-[#5b6b84]">
                {examSet.questions.length} câu ·{' '}
                {examSet.handedOverAt ? `Bàn giao ${examSet.handedOverAt}` : 'Đã bàn giao'}
              </p>
            </article>
          ))}
          {!loading && examSets.length === 0 ? (
            <EmptyState
              compact
              icon={TicketCheck}
              title="Chưa có bộ đề bàn giao"
              description="Backend chưa trả về bộ đề HANDED_OVER cho kỳ thi này."
            />
          ) : null}
        </div>
      </section>
    </div>
  )
}
