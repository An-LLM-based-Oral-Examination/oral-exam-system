import { useEffect, useState } from 'react'
import { AlertTriangle, Armchair, CheckCircle2, LoaderCircle, MonitorCheck, RefreshCw, UserCheck, UserX } from 'lucide-react'
import { proctorService } from '@/services/proctor.service'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass, fieldLabelClass } from '@/components/common/formFieldClasses'
import { toast } from '@/components/common/toastStore'
import { BannerFact, LecturerPageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { userMessage } from '@/lib/userMessage'

interface Seat {
  seatNumber: number
  isOccupied: boolean
  ticketId?: string | null
  studentCode?: string | null
  fullName?: string | null
  ipAddress?: string | null
  status?: string | null
  isCheckedIn: boolean
  isSuspended: boolean
  suspensionReason?: string | null
}

interface Monitor {
  shiftId: string
  shiftName: string
  roomLab: string
  shiftStatus: string
  startTime: string
  endTime: string
  totalSeats: number
  assignedTicketsCount: number
  checkedInCount: number
  inProgressCount: number
  submittedCount: number
  suspendedCount: number
  seats: Seat[]
}

function getErrorMessage(error: unknown) {
  return userMessage(error, 'Không hoàn tất được thao tác điểm danh. Vui lòng thử lại.')
}

export function ProctorAttendancePage() {
  const [shiftId, setShiftId] = useState('')
  const [monitor, setMonitor] = useState<Monitor | null>(null)
  const [selectedSeat, setSelectedSeat] = useState<Seat | null>(null)
  const [suspendReason, setSuspendReason] = useState('')
  const [blurViolationCount, setBlurViolationCount] = useState(0)
  const [shiftIdError, setShiftIdError] = useState('')
  const [suspendReasonError, setSuspendReasonError] = useState('')
  const [blurCountError, setBlurCountError] = useState('')
  const [loading, setLoading] = useState(false)
  const [busy, setBusy] = useState(false)

  const loadAttendance = async () => {
    if (!shiftId.trim()) {
      setShiftIdError('Vui lòng nhập Shift ID của ca thi.')
      return
    }
    setShiftIdError('')
    setLoading(true)
    try { setMonitor(await proctorService.getShiftAttendance(shiftId.trim())) } catch (loadError) { setMonitor(null); toast.error(getErrorMessage(loadError)) } finally { setLoading(false) }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      const params = new URLSearchParams(window.location.search)
      const initial = params.get('shiftId') ?? ''
      if (!initial) return
      setShiftId(initial)
      void proctorService.getShiftAttendance(initial).then(setMonitor).catch((error) => toast.error(getErrorMessage(error)))
    }, 0)
    return () => window.clearTimeout(timer)
  }, [])

  const updateSeat = async (seat: Seat, present: boolean) => {
    if (!seat.ticketId) return
    setBusy(true)
    try { await proctorService.updateTicketAttendance(seat.ticketId, { isPresent: present, ipAddress: seat.ipAddress ?? undefined }); toast.success(present ? `Đã điểm danh ghế ${seat.seatNumber}.` : `Đã ghi nhận ghế ${seat.seatNumber} vắng.`); await loadAttendance() } catch (actionError) { toast.error(getErrorMessage(actionError)) } finally { setBusy(false) }
  }

  const suspendSeat = async () => {
    if (!selectedSeat?.ticketId) return
    const reasonError = suspendReason.trim().length < 5 ? 'Lý do đình chỉ cần ít nhất 5 ký tự.' : ''
    const countError =
      !Number.isInteger(blurViolationCount) || blurViolationCount < 0
        ? 'Số lần blur phải là số nguyên không âm.'
        : ''
    setSuspendReasonError(reasonError)
    setBlurCountError(countError)
    if (reasonError || countError) return
    setBusy(true)
    try { await proctorService.suspendTicket(selectedSeat.ticketId, { reason: suspendReason.trim(), blurViolationCount }); toast.success(`Đã đình chỉ vé thi tại ghế ${selectedSeat.seatNumber}.`); setSuspendReason(''); setSuspendReasonError(''); setBlurCountError(''); setSelectedSeat(null); await loadAttendance() } catch (actionError) { toast.error(getErrorMessage(actionError)) } finally { setBusy(false) }
  }

  return (
    <div className={ds.page}>
      <LecturerPageBanner
        icon={MonitorCheck}
        eyebrow="Điểm danh"
        title="Điểm danh phòng thi"
        description="Theo dõi 40 ghế, xác nhận điểm danh và đình chỉ vé khi phát hiện vi phạm trong phòng thi."
        extras={
          <>
            <BannerFact icon={Armchair}>Sơ đồ ghế Lab</BannerFact>
            <BannerFact icon={UserCheck} tone="green">
              Xác nhận điểm danh
            </BannerFact>
            <BannerFact icon={AlertTriangle} tone="violet">
              Đình chỉ vé
            </BannerFact>
          </>
        }
      />
      <form className={cn(ds.section, 'flex flex-col gap-3 p-4 sm:flex-row sm:items-start')} onSubmit={(event) => { event.preventDefault(); void loadAttendance() }}>
        <div className="min-w-0 flex-1 sm:max-w-md"><input className={cn(ds.field, 'font-mono text-xs', shiftIdError && fieldInvalidClass)} value={shiftId} onChange={(event) => { setShiftId(event.target.value); if (shiftIdError) setShiftIdError('') }} placeholder="Nhập Shift ID" aria-label="Shift ID" aria-invalid={Boolean(shiftIdError)} /><FieldError message={shiftIdError} /></div>
        <button className={ds.primaryButton}>Mở ca thi</button>
      </form>
      {loading ? <div className="flex min-h-[300px] items-center justify-center rounded-2xl bg-white"><LoaderCircle className="size-8 animate-spin text-[#1C4D8D]" /></div> : monitor ? <>
        <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">{[['Đã cấp vé', monitor.assignedTicketsCount], ['Đã điểm danh', monitor.checkedInCount], ['Đang thi', monitor.inProgressCount], ['Đã nộp', monitor.submittedCount], ['Đình chỉ', monitor.suspendedCount]].map(([label, value]) => <div key={String(label)} className="rounded-2xl border border-[#82aedb]/55 bg-white p-4"><p className="text-xs font-bold text-[#708097]">{label}</p><p className="mt-2 font-mono text-2xl font-black text-[#0F2854]">{value}</p></div>)}</section>
        <section className={ds.section}><SectionHeader icon={UserCheck} title={`${monitor.shiftName} · ${monitor.roomLab}`} hint={`Điểm danh ghế Lab · trạng thái: ${monitor.shiftStatus}`} action={<button type="button" className={ds.outlineButton} onClick={() => void loadAttendance()}><RefreshCw className="size-4" /> Làm mới</button>} /><div className="grid grid-cols-2 gap-3 p-4 sm:grid-cols-4 lg:grid-cols-5">{monitor.seats.map((seat) => <button key={seat.seatNumber} type="button" onClick={() => { setSelectedSeat(seat); setSuspendReasonError(''); setBlurCountError('') }} className={cn('min-h-28 rounded-2xl border p-3 text-left transition hover:-translate-y-0.5', seat.isSuspended ? 'border-[#f0c5c9] bg-[#fff5f6]' : seat.isCheckedIn ? 'border-[#b7e4cf] bg-[#effaf6]' : seat.isOccupied ? 'border-[#ead9ae] bg-[#fff9ec]' : 'border-[#82aedb]/55 bg-white')}><div className="flex items-center justify-between"><span className="font-mono text-sm font-black text-[#1C4D8D]">#{String(seat.seatNumber).padStart(2, '0')}</span>{seat.isSuspended ? <AlertTriangle className="size-4 text-[#b8404b]" /> : seat.isCheckedIn ? <CheckCircle2 className="size-4 text-[#16836a]" /> : <UserX className="size-4 text-[#9aa8ba]" />}</div><p className="mt-3 truncate text-xs font-bold text-[#0F2854]">{seat.fullName || 'Ghế trống'}</p><p className="mt-1 truncate font-mono text-[10px] text-[#718298]">{seat.studentCode || seat.ipAddress || 'Chưa cấp vé'}</p></button>)}</div></section>
        {selectedSeat?.ticketId && <section className="rounded-2xl border border-[#82aedb]/55 bg-white p-5"><div className="flex flex-wrap items-center justify-between gap-3"><div><h2 className="font-bold text-[#0F2854]">Ghế #{selectedSeat.seatNumber} · {selectedSeat.fullName || selectedSeat.studentCode}</h2><p className="mt-1 text-xs text-[#687a91]">Trạng thái: {selectedSeat.status || 'SCHEDULED'} · IP: {selectedSeat.ipAddress || '—'}</p></div><div className="flex gap-2"><button type="button" disabled={busy || selectedSeat.isCheckedIn} onClick={() => void updateSeat(selectedSeat, true)} className={ds.primaryButton}><UserCheck className="size-4" /> Điểm danh</button><button type="button" disabled={busy || !selectedSeat.isCheckedIn} onClick={() => void updateSeat(selectedSeat, false)} className={ds.outlineButton}><UserX className="size-4" /> Ghi vắng</button></div></div><div className="mt-4 grid gap-3 sm:grid-cols-[1fr_auto_auto] sm:items-start"><label className={fieldLabelClass(Boolean(suspendReasonError))}>Lý do đình chỉ<input className={cn(ds.field, 'mt-1', suspendReasonError && fieldInvalidClass)} value={suspendReason} onChange={(event) => { setSuspendReason(event.target.value); if (suspendReasonError) setSuspendReasonError('') }} placeholder="Ví dụ: blur quá 3 lần" aria-invalid={Boolean(suspendReasonError)} /><FieldError message={suspendReasonError} /></label><label className={fieldLabelClass(Boolean(blurCountError))}>Số lần blur<input type="number" min="0" className={cn(ds.field, 'mt-1 w-32', blurCountError && fieldInvalidClass)} value={blurViolationCount} onChange={(event) => { setBlurViolationCount(Number(event.target.value)); if (blurCountError) setBlurCountError('') }} aria-invalid={Boolean(blurCountError)} /><FieldError message={blurCountError} className="w-32" /></label><button type="button" disabled={busy || selectedSeat.isSuspended} onClick={() => void suspendSeat()} className={cn(ds.primaryButton, 'bg-[#b8404b] hover:bg-[#953943]')}><AlertTriangle className="size-4" /> Đình chỉ vé</button></div></section>}
      </> : <EmptyState icon={Armchair} title="Chưa mở ca điểm danh" description="Nhập Shift ID ở trên để xem sơ đồ ghế và danh sách điểm danh." />}
    </div>
  )
}

