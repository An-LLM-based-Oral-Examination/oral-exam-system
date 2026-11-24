import { useEffect, useState } from 'react'
import { AlertTriangle, Armchair, CheckCircle2, LoaderCircle, MonitorCheck, RefreshCw, ShieldAlert, UserCheck, UserX } from 'lucide-react'
import { proctorService } from '@/services/proctor.service'
import { useAuth } from '@/hooks/useAuth'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError, fieldInvalidClass, fieldLabelClass } from '@/components/common/formField'
import { BannerFact, LecturerPageBanner, SectionHeader } from '@/design/ui'
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
  const { user } = useAuth()
  const [shiftId, setShiftId] = useState('')
  const [monitor, setMonitor] = useState<Monitor | null>(null)
  const [selectedSeat, setSelectedSeat] = useState<Seat | null>(null)
  const [violationEventType, setViolationEventType] = useState('PROCTOR_REPORTED_VIOLATION')
  const [violationSeverity, setViolationSeverity] = useState('HIGH')
  const [violationDetails, setViolationDetails] = useState('')
  const [violationDetailsError, setViolationDetailsError] = useState('')
  const [suspendReason, setSuspendReason] = useState('')
  const [blurViolationCount, setBlurViolationCount] = useState(0)
  const [shiftIdError, setShiftIdError] = useState('')
  const [suspendReasonError, setSuspendReasonError] = useState('')
  const [blurCountError, setBlurCountError] = useState('')
  const [loading, setLoading] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const canSuspendTicket = user?.role === 'ExaminationOfficer' || user?.role === 'Admin'

  const loadAttendance = async () => {
    if (!shiftId.trim()) {
      setShiftIdError('Vui lòng nhập Shift ID của ca thi.')
      return
    }
    setShiftIdError('')
    setLoading(true); setError('')
    try {
      setMonitor(await proctorService.getShiftAttendance(shiftId.trim()))
    } catch (loadError) {
      setMonitor(null)
      setError(getErrorMessage(loadError))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      const params = new URLSearchParams(window.location.search)
      const initial = params.get('shiftId') ?? ''
      if (!initial) return
      setShiftId(initial)
      void proctorService.getShiftAttendance(initial).then(setMonitor).catch((loadErr) => setError(getErrorMessage(loadErr)))
    }, 0)
    return () => window.clearTimeout(timer)
  }, [])

  const updateSeat = async (seat: Seat, present: boolean) => {
    if (!seat.ticketId) return
    setBusy(true); setError(''); setNotice('')
    try {
      await proctorService.updateTicketAttendance(seat.ticketId, {
        isPresent: present,
        ipAddress: seat.ipAddress ?? undefined,
      })
      setNotice(present ? `Đã điểm danh ghế ${seat.seatNumber}.` : `Đã ghi nhận ghế ${seat.seatNumber} vắng.`)
      await loadAttendance()
    } catch (actionError) {
      setError(getErrorMessage(actionError))
    } finally {
      setBusy(false)
    }
  }

  const reportViolation = async () => {
    if (!monitor?.shiftId || !selectedSeat?.ticketId) return
    if (violationDetails.trim().length < 5) {
      setViolationDetailsError('Chi tiết biên bản vi phạm cần ít nhất 5 ký tự.')
      return
    }
    setViolationDetailsError('')
    setBusy(true); setError(''); setNotice('')
    try {
      await proctorService.recordViolation(monitor.shiftId, {
        ticketId: selectedSeat.ticketId,
        eventType: violationEventType,
        severity: violationSeverity,
        details: violationDetails.trim(),
      })
      setNotice(`Đã ghi nhận biên bản vi phạm tại ghế #${selectedSeat.seatNumber} vào exam_anti_cheat_logs.`)
      setViolationDetails('')
      await loadAttendance()
    } catch (actionError) {
      setError(getErrorMessage(actionError))
    } finally {
      setBusy(false)
    }
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
    setBusy(true); setError(''); setNotice('')
    try {
      await proctorService.suspendTicket(selectedSeat.ticketId, {
        reason: suspendReason.trim(),
        blurViolationCount,
      })
      setNotice(`Đã đình chỉ vé thi tại ghế ${selectedSeat.seatNumber}.`)
      setSuspendReason('')
      setSuspendReasonError('')
      setBlurCountError('')
      setSelectedSeat(null)
      await loadAttendance()
    } catch (actionError) {
      setError(getErrorMessage(actionError))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className={ds.page}>
      <LecturerPageBanner
        icon={MonitorCheck}
        eyebrow="Giám thị phòng Lab (40 ghế)"
        title="Điểm danh & Báo cáo vi phạm phòng thi"
        description="Giám thị đối chiếu thẻ SV/CCCD điểm danh STT (SeatNumber 1–40) và lập biên bản vi phạm an ninh phòng thi trực tiếp vào exam_anti_cheat_logs."
        extras={
          <>
            <BannerFact icon={Armchair}>Sơ đồ 40 ghế Lab</BannerFact>
            <BannerFact icon={UserCheck} tone="green">
              Điểm danh thẻ SV/CCCD
            </BannerFact>
            <BannerFact icon={ShieldAlert} tone="violet">
              Ghi log Anti-Cheat
            </BannerFact>
          </>
        }
      />
      <form
        className={cn(ds.section, 'flex flex-col gap-3 p-4 sm:flex-row sm:items-start')}
        onSubmit={(event) => {
          event.preventDefault()
          void loadAttendance()
        }}
      >
        <div className="min-w-0 flex-1 sm:max-w-md">
          <input
            className={cn(ds.field, 'font-mono text-xs', shiftIdError && fieldInvalidClass)}
            value={shiftId}
            onChange={(event) => {
              setShiftId(event.target.value)
              if (shiftIdError) setShiftIdError('')
            }}
            placeholder="Nhập Shift ID"
            aria-label="Shift ID"
            aria-invalid={Boolean(shiftIdError)}
          />
          <FieldError message={shiftIdError} />
        </div>
        <button className={ds.primaryButton}>Tải sơ đồ ca thi</button>
      </form>
      {(error || notice) && (
        <div
          role={error ? 'alert' : 'status'}
          className={cn(
            'rounded-xl border px-4 py-3 text-sm font-semibold',
            error ? 'border-[#f0c5c9] bg-[#fff5f6] text-[#a43c47]' : 'border-[#b7e4cf] bg-[#effaf6] text-[#176f58]'
          )}
        >
          {error || notice}
        </div>
      )}
      {loading ? (
        <div className="flex min-h-[300px] items-center justify-center rounded-2xl bg-white">
          <LoaderCircle className="size-8 animate-spin text-[#1C4D8D]" />
        </div>
      ) : monitor ? (
        <>
          <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
            {[
              ['Đã cấp vé', monitor.assignedTicketsCount],
              ['Đã điểm danh', monitor.checkedInCount],
              ['Đang thi', monitor.inProgressCount],
              ['Đã nộp', monitor.submittedCount],
              ['Đình chỉ', monitor.suspendedCount],
            ].map(([label, value]) => (
              <div key={String(label)} className="rounded-2xl border border-[#82aedb]/55 bg-white p-4">
                <p className="text-xs font-bold text-[#708097]">{label}</p>
                <p className="mt-2 font-mono text-2xl font-black text-[#0F2854]">{value}</p>
              </div>
            ))}
          </section>
          <section className={ds.section}>
            <SectionHeader
              icon={UserCheck}
              title={`${monitor.shiftName} · ${monitor.roomLab}`}
              hint={`Điểm danh ghế Lab · trạng thái: ${monitor.shiftStatus}`}
              action={
                <button type="button" className={ds.outlineButton} onClick={() => void loadAttendance()}>
                  <RefreshCw className="size-4" /> Làm mới
                </button>
              }
            />
            <div className="grid grid-cols-2 gap-3 p-4 sm:grid-cols-4 lg:grid-cols-5">
              {monitor.seats.map((seat) => (
                <button
                  key={seat.seatNumber}
                  type="button"
                  onClick={() => {
                    setSelectedSeat(seat)
                    setViolationDetailsError('')
                    setSuspendReasonError('')
                    setBlurCountError('')
                  }}
                  className={cn(
                    'min-h-28 rounded-2xl border p-3 text-left transition hover:-translate-y-0.5',
                    seat.isSuspended
                      ? 'border-[#f0c5c9] bg-[#fff5f6]'
                      : seat.isCheckedIn
                        ? 'border-[#b7e4cf] bg-[#effaf6]'
                        : seat.isOccupied
                          ? 'border-[#ead9ae] bg-[#fff9ec]'
                          : 'border-[#82aedb]/55 bg-white'
                  )}
                >
                  <div className="flex items-center justify-between">
                    <span className="font-mono text-sm font-black text-[#1C4D8D]">
                      #{String(seat.seatNumber).padStart(2, '0')}
                    </span>
                    {seat.isSuspended ? (
                      <AlertTriangle className="size-4 text-[#b8404b]" />
                    ) : seat.isCheckedIn ? (
                      <CheckCircle2 className="size-4 text-[#16836a]" />
                    ) : (
                      <UserX className="size-4 text-[#9aa8ba]" />
                    )}
                  </div>
                  <p className="mt-3 truncate text-xs font-bold text-[#0F2854]">{seat.fullName || 'Ghế trống'}</p>
                  <p className="mt-1 truncate font-mono text-[10px] text-[#718298]">
                    {seat.studentCode || seat.ipAddress || 'Chưa cấp vé'}
                  </p>
                </button>
              ))}
            </div>
          </section>
          {selectedSeat?.ticketId && (
            <section className="space-y-4 rounded-2xl border border-[#82aedb]/55 bg-white p-5">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <h2 className="font-bold text-[#0F2854]">
                    Ghế #{selectedSeat.seatNumber} · {selectedSeat.fullName || selectedSeat.studentCode}
                  </h2>
                  <p className="mt-1 text-xs text-[#687a91]">
                    Trạng thái: {selectedSeat.status || 'SCHEDULED'} · IP: {selectedSeat.ipAddress || '—'}
                  </p>
                </div>
                <div className="flex gap-2">
                  <button
                    type="button"
                    disabled={busy || selectedSeat.isCheckedIn}
                    onClick={() => void updateSeat(selectedSeat, true)}
                    className={ds.primaryButton}
                  >
                    <UserCheck className="size-4" /> Điểm danh có mặt
                  </button>
                  <button
                    type="button"
                    disabled={busy || !selectedSeat.isCheckedIn}
                    onClick={() => void updateSeat(selectedSeat, false)}
                    className={ds.outlineButton}
                  >
                    <UserX className="size-4" /> Ghi vắng
                  </button>
                </div>
              </div>

              <div className="rounded-xl border border-[#d6e4f2] bg-[#f8fbfd] p-4">
                <h3 className="text-xs font-bold uppercase tracking-wider text-[#1C4D8D]">
                  Báo cáo vi phạm quy chế phòng thi (Lưu vào exam_anti_cheat_logs)
                </h3>
                <div className="mt-3 grid gap-3 sm:grid-cols-[180px_140px_1fr_auto] sm:items-start">
                  <label className="text-xs font-bold text-[#63758e]">
                    Loại vi phạm
                    <select
                      className={cn(ds.field, 'mt-1')}
                      value={violationEventType}
                      onChange={(event) => setViolationEventType(event.target.value)}
                    >
                      <option value="PROCTOR_REPORTED_VIOLATION">Vi phạm quy chế phòng thi</option>
                      <option value="TAB_SWITCH_OR_BLUR">Chuyển tab / thoát Kiosk</option>
                      <option value="UNAUTHORIZED_DEVICE">Sử dụng tài liệu / thiết bị cấm</option>
                      <option value="IDENTITY_MISMATCH">Sai lệch thẻ SV / CCCD</option>
                    </select>
                  </label>
                  <label className="text-xs font-bold text-[#63758e]">
                    Mức độ
                    <select
                      className={cn(ds.field, 'mt-1')}
                      value={violationSeverity}
                      onChange={(event) => setViolationSeverity(event.target.value)}
                    >
                      <option value="MEDIUM">MEDIUM</option>
                      <option value="HIGH">HIGH</option>
                      <option value="CRITICAL">CRITICAL</option>
                    </select>
                  </label>
                  <label className={fieldLabelClass(Boolean(violationDetailsError))}>
                    Nội dung biên bản vi phạm
                    <input
                      className={cn(ds.field, 'mt-1', violationDetailsError && fieldInvalidClass)}
                      value={violationDetails}
                      onChange={(event) => {
                        setViolationDetails(event.target.value)
                        if (violationDetailsError) setViolationDetailsError('')
                      }}
                      placeholder="Mô tả hành vi vi phạm tại máy trạm..."
                      aria-invalid={Boolean(violationDetailsError)}
                    />
                    <FieldError message={violationDetailsError} />
                  </label>
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => void reportViolation()}
                    className={cn(ds.primaryButton, 'sm:mt-5 bg-[#b8404b] hover:bg-[#953943]')}
                  >
                    <ShieldAlert className="size-4" /> Báo cáo vi phạm
                  </button>
                </div>
              </div>

              {canSuspendTicket && (
                <div className="grid gap-3 border-t border-[#e8edf3] pt-4 sm:grid-cols-[1fr_auto_auto] sm:items-start">
                  <label className={fieldLabelClass(Boolean(suspendReasonError))}>
                    Lý do đình chỉ vé thi (Thẩm quyền Cán bộ Khảo thí / Admin)
                    <input
                      className={cn(ds.field, 'mt-1', suspendReasonError && fieldInvalidClass)}
                      value={suspendReason}
                      onChange={(event) => {
                        setSuspendReason(event.target.value)
                        if (suspendReasonError) setSuspendReasonError('')
                      }}
                      placeholder="Ví dụ: blur quá 3 lần hoặc gian lận nghiêm trọng"
                      aria-invalid={Boolean(suspendReasonError)}
                    />
                    <FieldError message={suspendReasonError} />
                  </label>
                  <label className={fieldLabelClass(Boolean(blurCountError))}>
                    Số lần blur
                    <input
                      type="number"
                      min="0"
                      className={cn(ds.field, 'mt-1 w-32', blurCountError && fieldInvalidClass)}
                      value={blurViolationCount}
                      onChange={(event) => {
                        setBlurViolationCount(Number(event.target.value))
                        if (blurCountError) setBlurCountError('')
                      }}
                      aria-invalid={Boolean(blurCountError)}
                    />
                    <FieldError message={blurCountError} className="w-32" />
                  </label>
                  <button
                    type="button"
                    disabled={busy || selectedSeat.isSuspended}
                    onClick={() => void suspendSeat()}
                    className={cn(ds.primaryButton, 'sm:mt-5 bg-[#b8404b] hover:bg-[#953943]')}
                  >
                    <AlertTriangle className="size-4" /> Đình chỉ vé
                  </button>
                </div>
              )}
            </section>
          )}
        </>
      ) : (
        <EmptyState
          icon={Armchair}
          title="Chưa mở ca điểm danh"
          description="Nhập Shift ID ở trên để xem sơ đồ ghế và danh sách điểm danh."
        />
      )}
    </div>
  )
}
