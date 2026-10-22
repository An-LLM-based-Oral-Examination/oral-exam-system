import { useEffect, useState } from 'react'
import { AlertTriangle, CheckCircle2, LoaderCircle, MonitorCheck, RefreshCw, UserCheck, UserX } from 'lucide-react'
import { proctorService } from '@/services/proctor.service'
import { cn } from '@/lib/utils'
import { ds } from '@/design/system'

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
  if (typeof error === 'object' && error !== null && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail
    if (typeof detail === 'string' && detail.trim()) return detail
  }
  return 'Không thể hoàn tất yêu cầu. Vui lòng thử lại.'
}

export function ProctorAttendancePage() {
  const [shiftId, setShiftId] = useState('')
  const [monitor, setMonitor] = useState<Monitor | null>(null)
  const [selectedSeat, setSelectedSeat] = useState<Seat | null>(null)
  const [suspendReason, setSuspendReason] = useState('')
  const [blurViolationCount, setBlurViolationCount] = useState(0)
  const [loading, setLoading] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const loadAttendance = async () => {
    if (!shiftId.trim()) return
    setLoading(true); setError('')
    try { setMonitor(await proctorService.getShiftAttendance(shiftId.trim())) } catch (loadError) { setMonitor(null); setError(getErrorMessage(loadError)) } finally { setLoading(false) }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      const params = new URLSearchParams(window.location.search)
      const initial = params.get('shiftId') ?? ''
      if (!initial) return
      setShiftId(initial)
      void proctorService.getShiftAttendance(initial).then(setMonitor).catch((error) => setError(getErrorMessage(error)))
    }, 0)
    return () => window.clearTimeout(timer)
  }, [])

  const updateSeat = async (seat: Seat, present: boolean) => {
    if (!seat.ticketId) return
    setBusy(true); setError(''); setNotice('')
    try { await proctorService.updateTicketAttendance(seat.ticketId, { isPresent: present, ipAddress: seat.ipAddress ?? undefined }); setNotice(present ? `Đã điểm danh ghế ${seat.seatNumber}.` : `Đã ghi nhận ghế ${seat.seatNumber} vắng.`); await loadAttendance() } catch (actionError) { setError(getErrorMessage(actionError)) } finally { setBusy(false) }
  }

  const suspendSeat = async () => {
    if (!selectedSeat?.ticketId || suspendReason.trim().length < 5) return
    setBusy(true); setError(''); setNotice('')
    try { await proctorService.suspendTicket(selectedSeat.ticketId, { reason: suspendReason.trim(), blurViolationCount }); setNotice(`Đã đình chỉ vé thi tại ghế ${selectedSeat.seatNumber}.`); setSuspendReason(''); setSelectedSeat(null); await loadAttendance() } catch (actionError) { setError(getErrorMessage(actionError)) } finally { setBusy(false) }
  }

  return (
    <div className={ds.page}>
      <section className={cn(ds.banner, 'p-5 sm:p-6')}><p className={ds.bannerChip}><MonitorCheck className="size-4" /> MF-04 · Cổng giám thị</p><h1 className={ds.bannerTitle}>Điểm danh & giám sát phòng Lab</h1><p className={ds.bannerText}>Theo dõi 40 ghế, xác nhận điểm danh và đình chỉ vé khi phát hiện vi phạm Kiosk Lockdown.</p><form className="mt-5 flex max-w-xl gap-2" onSubmit={(event) => { event.preventDefault(); void loadAttendance() }}><input required className={cn(ds.field, 'font-mono text-xs')} value={shiftId} onChange={(event) => setShiftId(event.target.value)} placeholder="Nhập Shift ID" /><button className={ds.bannerButton}>Mở ca thi</button></form></section>
      {(error || notice) && <div role={error ? 'alert' : 'status'} className={cn('rounded-xl border px-4 py-3 text-sm font-semibold', error ? 'border-[#f0c5c9] bg-[#fff5f6] text-[#a43c47]' : 'border-[#b7e4cf] bg-[#effaf6] text-[#176f58]')}>{error || notice}</div>}
      {loading ? <div className="flex min-h-[300px] items-center justify-center rounded-2xl bg-white"><LoaderCircle className="size-8 animate-spin text-[#1C4D8D]" /></div> : monitor ? <>
        <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">{[['Đã cấp vé', monitor.assignedTicketsCount], ['Đã điểm danh', monitor.checkedInCount], ['Đang thi', monitor.inProgressCount], ['Đã nộp', monitor.submittedCount], ['Đình chỉ', monitor.suspendedCount]].map(([label, value]) => <div key={String(label)} className="rounded-2xl border border-[#dbe5ef] bg-white p-4"><p className="text-xs font-bold text-[#708097]">{label}</p><p className="mt-2 font-mono text-2xl font-black text-[#0F2854]">{value}</p></div>)}</section>
        <section className={ds.section}><div className={cn(ds.sectionHeader, 'flex items-center justify-between')}><div><h2 className={ds.sectionTitleRow}><UserCheck className={ds.sectionIcon} /> <span className="text-sm font-bold text-[#0F2854]">{monitor.shiftName} · {monitor.roomLab}</span></h2><p className={ds.sectionHint}>Trạng thái ca: {monitor.shiftStatus}</p></div><button type="button" className={ds.outlineButton} onClick={() => void loadAttendance()}><RefreshCw className="size-4" /> Làm mới</button></div><div className="grid grid-cols-2 gap-3 p-4 sm:grid-cols-4 lg:grid-cols-5">{monitor.seats.map((seat) => <button key={seat.seatNumber} type="button" onClick={() => setSelectedSeat(seat)} className={cn('min-h-28 rounded-2xl border p-3 text-left transition hover:-translate-y-0.5', seat.isSuspended ? 'border-[#f0c5c9] bg-[#fff5f6]' : seat.isCheckedIn ? 'border-[#b7e4cf] bg-[#effaf6]' : seat.isOccupied ? 'border-[#ead9ae] bg-[#fff9ec]' : 'border-[#dbe5ef] bg-white')}><div className="flex items-center justify-between"><span className="font-mono text-sm font-black text-[#1C4D8D]">#{String(seat.seatNumber).padStart(2, '0')}</span>{seat.isSuspended ? <AlertTriangle className="size-4 text-[#b8404b]" /> : seat.isCheckedIn ? <CheckCircle2 className="size-4 text-[#16836a]" /> : <UserX className="size-4 text-[#9aa8ba]" />}</div><p className="mt-3 truncate text-xs font-bold text-[#0F2854]">{seat.fullName || 'Ghế trống'}</p><p className="mt-1 truncate font-mono text-[10px] text-[#718298]">{seat.studentCode || seat.ipAddress || 'Chưa cấp vé'}</p></button>)}</div></section>
        {selectedSeat?.ticketId && <section className="rounded-2xl border border-[#4988C4]/45 bg-white p-5"><div className="flex flex-wrap items-center justify-between gap-3"><div><h2 className="font-bold text-[#0F2854]">Ghế #{selectedSeat.seatNumber} · {selectedSeat.fullName || selectedSeat.studentCode}</h2><p className="mt-1 text-xs text-[#687a91]">Trạng thái: {selectedSeat.status || 'SCHEDULED'} · IP: {selectedSeat.ipAddress || '—'}</p></div><div className="flex gap-2"><button type="button" disabled={busy || selectedSeat.isCheckedIn} onClick={() => void updateSeat(selectedSeat, true)} className={ds.primaryButton}><UserCheck className="size-4" /> Điểm danh</button><button type="button" disabled={busy || !selectedSeat.isCheckedIn} onClick={() => void updateSeat(selectedSeat, false)} className={ds.outlineButton}><UserX className="size-4" /> Ghi vắng</button></div></div><div className="mt-4 grid gap-3 sm:grid-cols-[1fr_auto_auto] sm:items-end"><label className="text-xs font-bold text-[#63758e]">Lý do đình chỉ<input className={cn(ds.field, 'mt-1')} value={suspendReason} onChange={(event) => setSuspendReason(event.target.value)} placeholder="Ví dụ: blur quá 3 lần" /></label><label className="text-xs font-bold text-[#63758e]">Số lần blur<input type="number" min="0" className={cn(ds.field, 'mt-1 w-32')} value={blurViolationCount} onChange={(event) => setBlurViolationCount(Number(event.target.value))} /></label><button type="button" disabled={busy || suspendReason.trim().length < 5 || selectedSeat.isSuspended} onClick={() => void suspendSeat()} className={cn(ds.primaryButton, 'bg-[#b8404b] hover:bg-[#953943]')}><AlertTriangle className="size-4" /> Đình chỉ vé</button></div></section>}
      </> : <section className="rounded-2xl border border-dashed border-[#4988C4]/45 bg-white p-12 text-center text-sm text-[#687a91]">Nhập Shift ID để xem sơ đồ ghế và danh sách điểm danh.</section>}
    </div>
  )
}

