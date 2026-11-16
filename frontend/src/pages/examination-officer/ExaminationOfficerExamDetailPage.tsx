/* eslint-disable react-hooks/set-state-in-effect */
import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  ArrowLeft,
  CalendarDays,
  CheckCircle2,
  LoaderCircle,
  RefreshCw,
  ScanSearch,
  TicketCheck,
} from 'lucide-react'
import { officialExamService } from '@/services/officialExam.service'
import type { ExamSet, ExamShift, OfficialExamSession } from '@/types/officialExam.types'
import { PageBanner, SectionHeader } from '@/design/ui'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { EmptyState } from '@/components/common/EmptyState'
import { userMessage } from '@/lib/userMessage'
import { toast } from '@/components/common/toastStore'
import {
  DEFAULT_SHIFT_CAPACITY,
  MAX_SHIFT_CAPACITY,
  MIN_SHIFT_CAPACITY,
  getShiftCapacityMetrics,
  validateShiftCapacityInput,
} from '@/lib/shiftCapacity'

function toDateTimeLocalValue(value: string) {
  if (!value) return ''
  if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value)) return value
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return value.slice(0, 16)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${parsed.getFullYear()}-${pad(parsed.getMonth() + 1)}-${pad(parsed.getDate())}T${pad(parsed.getHours())}:${pad(parsed.getMinutes())}`
}

export function ExaminationOfficerExamDetailPage() {
  const { sessionId = '' } = useParams<{ sessionId: string }>()
  const [session, setSession] = useState<OfficialExamSession | null>(null)
  const [shifts, setShifts] = useState<ExamShift[]>([])
  const [examSets, setExamSets] = useState<ExamSet[]>([])
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [editingShift, setEditingShift] = useState<ExamShift | null>(null)
  const [editMaxSeats, setEditMaxSeats] = useState(String(DEFAULT_SHIFT_CAPACITY))
  const [editShiftName, setEditShiftName] = useState('')
  const [editRoomLab, setEditRoomLab] = useState('')
  const [editStartTime, setEditStartTime] = useState('')
  const [editEndTime, setEditEndTime] = useState('')
  const [editBusy, setEditBusy] = useState(false)
  const [editError, setEditError] = useState('')
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
  const openEditShift = (shift: ExamShift) => {
    setEditingShift(shift)
    setEditShiftName(shift.shiftName || '')
    setEditRoomLab(shift.roomLab || '')
    setEditStartTime(toDateTimeLocalValue(shift.startTime))
    setEditEndTime(toDateTimeLocalValue(shift.endTime))
    setEditMaxSeats(String(shift.maxSeats ?? DEFAULT_SHIFT_CAPACITY))
    setEditError('')
  }
  const submitEditShift = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!editingShift) return
    const capacityCheck = validateShiftCapacityInput(editMaxSeats, {
      defaultWhenEmpty: editingShift.maxSeats ?? DEFAULT_SHIFT_CAPACITY,
    })
    if (capacityCheck.error || capacityCheck.value === null) {
      setEditError(capacityCheck.error)
      toast.error(capacityCheck.error)
      return
    }
    setEditBusy(true)
    setEditError('')
    try {
      await officialExamService.updateShift(editingShift.id, {
        shiftName: editShiftName.trim() || editingShift.shiftName,
        roomLab: editRoomLab.trim() || editingShift.roomLab,
        startTime: new Date(editStartTime || editingShift.startTime).toISOString(),
        endTime: new Date(editEndTime || editingShift.endTime).toISOString(),
        proctorUserId: editingShift.proctorUserId ?? '',
        examinerUserId: editingShift.examinerUserId ?? '',
        maxSeats: capacityCheck.value,
      })
      toast.success('Đã cập nhật ca thi thành công.')
      setEditingShift(null)
      await load()
    } catch (updateError) {
      const message = userMessage(updateError, 'Không cập nhật được ca thi.')
      setEditError(message)
      toast.error(message)
    } finally {
      setEditBusy(false)
    }
  }
  const editCapacityValidation = validateShiftCapacityInput(editMaxSeats, {
    defaultWhenEmpty: editingShift?.maxSeats ?? DEFAULT_SHIFT_CAPACITY,
  })
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
          {shifts.map((shift) => {
            const capacity = getShiftCapacityMetrics(shift)
            return (
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
                    <p className="mt-1.5 text-xs font-semibold text-[#245997]">
                      Sức chứa tối đa: {capacity.maxSeats ?? '—'} · Số vé đã phát:{' '}
                      {capacity.assignedTicketsCount} · Số chỗ còn lại:{' '}
                      {capacity.remainingSeats ?? '—'}
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
                  <Link to={`/examination-officer/shifts/${shift.id}/tickets/import`} className={ds.outlineButton}>
                    <TicketCheck className="size-4" /> Import DSSV
                  </Link>
                  <button
                    type="button"
                    onClick={() => openEditShift(shift)}
                    className={ds.outlineButton}
                  >
                    Cập nhật ca
                  </button>
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
            )
          })}
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
      {editingShift ? (
        <FormDialog
          open
          align="center"
          size="md"
          eyebrow="CẤU HÌNH CA THI MF-04"
          title="Cập nhật ca thi"
          icon={CalendarDays}
          busy={editBusy}
          onClose={() => !editBusy && setEditingShift(null)}
          footer={
            <DialogFooter className="justify-end bg-white">
              <button
                type="button"
                className={ds.outlineButton}
                onClick={() => setEditingShift(null)}
                disabled={editBusy}
              >
                Hủy
              </button>
              <button
                type="submit"
                form="detail-update-shift-form"
                disabled={editBusy}
                className={ds.primaryButton}
              >
                {editBusy ? <LoaderCircle className="size-4 animate-spin" /> : null}
                {editBusy ? 'Đang lưu…' : 'Lưu cập nhật ca thi'}
              </button>
            </DialogFooter>
          }
        >
          <form id="detail-update-shift-form" onSubmit={submitEditShift} className="grid gap-4">
            {editError ? (
              <div
                role="alert"
                className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
              >
                {editError}
              </div>
            ) : null}
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="detail-edit-shift-name">
              Tên ca *
              <input
                id="detail-edit-shift-name"
                required
                className={ds.field}
                value={editShiftName}
                onChange={(event) => setEditShiftName(event.target.value)}
              />
            </label>
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="detail-edit-shift-room">
              Phòng Lab *
              <input
                id="detail-edit-shift-room"
                required
                className={ds.field}
                value={editRoomLab}
                onChange={(event) => setEditRoomLab(event.target.value)}
              />
            </label>
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="detail-edit-max-seats">
              Sức chứa tối đa (maxSeats) *
              <input
                id="detail-edit-max-seats"
                type="number"
                min={MIN_SHIFT_CAPACITY}
                max={MAX_SHIFT_CAPACITY}
                step={1}
                className={ds.field}
                value={editMaxSeats}
                onChange={(event) => setEditMaxSeats(event.target.value)}
              />
              {editMaxSeats.trim() !== '' && editCapacityValidation.error ? (
                <span role="alert" className="text-[11px] font-semibold text-[#a43d4b]">
                  {editCapacityValidation.error}
                </span>
              ) : null}
            </label>
          </form>
        </FormDialog>
      ) : null}
    </div>
  )
}
