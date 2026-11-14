/* eslint-disable react-hooks/set-state-in-effect */
import { useEffect, useState, type FormEvent } from 'react'
import { ClipboardList, RefreshCw, TicketCheck } from 'lucide-react'
import { officialExamService } from '@/services/officialExam.service'
import type { ExamShift, ExamTicket } from '@/types/officialExam.types'
import { PageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { toast } from '@/components/common/toastStore'
import { userMessage } from '@/lib/userMessage'

interface TicketFormState {
  shiftId: string
  studentId: string
  seatNumber: number
  ipAddress: string
}

const initialTicket: TicketFormState = {
  shiftId: '',
  studentId: '',
  seatNumber: 1,
  ipAddress: '',
}

export function ExaminationOfficerTicketAssignmentPage() {
  const [shifts, setShifts] = useState<ExamShift[]>([])
  const [issuedTickets, setIssuedTickets] = useState<ExamTicket[]>([])
  const [ticketForm, setTicketForm] = useState(initialTicket)
  const [isLoading, setIsLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const loadShifts = async () => {
    setIsLoading(true)
    setError('')
    try {
      setShifts(await officialExamService.getShifts())
    } catch (loadError) {
      const message = userMessage(loadError, 'Không tải được danh sách ca thi.')
      setError(message)
      toast.error(message)
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    void loadShifts()
  }, [])

  const submitTicket = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      const ticket = await officialExamService.assignTicket({
        ...ticketForm,
        ipAddress: ticketForm.ipAddress || null,
      })
      setIssuedTickets((currentTickets) => [ticket, ...currentTickets])
      setTicketForm({ ...initialTicket, shiftId: ticketForm.shiftId, seatNumber: ticketForm.seatNumber + 1 })
      toast.success('Đã cấp vé thi cho sinh viên.')
    } catch (submitError) {
      const message = userMessage(submitError, 'Không cấp được vé thi.')
      setError(message)
      toast.error(message)
    } finally {
      setBusy(false)
    }
  }

  const selectedShift = shifts.find((shift) => shift.id === ticketForm.shiftId)

  return (
    <div className={ds.page}>
      <PageBanner
        icon={TicketCheck}
        title="Cấp vé thi"
        description="Gán sinh viên vào ca thi, số ghế và máy trạm theo API thi thật."
      />
      {error ? (
        <div
          role="alert"
          className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
        >
          {error}
        </div>
      ) : null}
      <section className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_minmax(0,1.2fr)]">
        <form onSubmit={submitTicket} className={ds.section}>
          <SectionHeader icon={TicketCheck} title="Cấp vé cho sinh viên" hint="Cấp từng vé theo API hiện có" />
          <div className="grid gap-3 p-5">
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
              Ca thi
              <select
                required
                className={ds.field}
                value={ticketForm.shiftId}
                onChange={(event) => setTicketForm({ ...ticketForm, shiftId: event.target.value })}
              >
                <option value="">Chọn ca thi</option>
                {shifts.map((shift) => (
                  <option key={shift.id} value={shift.id}>
                    {shift.shiftName || shift.id} · {shift.roomLab}
                  </option>
                ))}
              </select>
            </label>
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
              Student ID
              <input
                required
                className={ds.field}
                value={ticketForm.studentId}
                onChange={(event) => setTicketForm({ ...ticketForm, studentId: event.target.value })}
              />
            </label>
            <div className="grid gap-3 sm:grid-cols-2">
              <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
                Số ghế
                <input
                  required
                  type="number"
                  min="1"
                  max="40"
                  className={ds.field}
                  value={ticketForm.seatNumber}
                  onChange={(event) => setTicketForm({ ...ticketForm, seatNumber: Number(event.target.value) })}
                />
              </label>
              <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
                IP máy
                <input
                  className={ds.field}
                  value={ticketForm.ipAddress}
                  onChange={(event) => setTicketForm({ ...ticketForm, ipAddress: event.target.value })}
                />
              </label>
            </div>
            <div className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] px-3 py-2 text-xs font-semibold text-[#5b6b84]">
              Backend hiện hỗ trợ cấp từng sinh viên. Import roster và cấp vé hàng loạt chưa có API.
            </div>
            <button type="submit" disabled={busy || !selectedShift} className={ds.primaryButton}>
              <TicketCheck className="size-4" /> {busy ? 'Đang cấp vé...' : 'Cấp vé thi'}
            </button>
          </div>
        </form>
        <section className={ds.section}>
          <SectionHeader
            icon={ClipboardList}
            title="Vé vừa cấp trong phiên"
            hint="Theo dõi các phản hồi cấp vé từ API"
            action={
              <button type="button" onClick={() => void loadShifts()} disabled={isLoading} className={ds.outlineButton}>
                <RefreshCw className={cn('size-4', isLoading && 'animate-spin')} /> Làm mới ca thi
              </button>
            }
          />
          <div className="p-5">
            {issuedTickets.length === 0 ? (
              <EmptyState
                compact
                icon={TicketCheck}
                title="Chưa có vé nào trong phiên này"
                description="Các vé được cấp thành công sẽ xuất hiện tại đây."
              />
            ) : (
              <div className="grid gap-2">
                {issuedTickets.map((ticket) => {
                  const shift = shifts.find((item) => item.id === ticket.shiftId)
                  return (
                    <article key={ticket.id} className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] px-3 py-3">
                      <div className="flex items-start justify-between gap-3">
                        <div>
                          <strong className="block text-sm text-[#0F2854]">Sinh viên: {ticket.studentId}</strong>
                          <span className="mt-1 block text-xs text-[#5b6b84]">
                            {shift?.shiftName || ticket.shiftId} · Ghế {ticket.seatNumber} · IP {ticket.ipAddress || 'Chưa gán'}
                          </span>
                        </div>
                        <span className={ds.infoChip}>{ticket.status}</span>
                      </div>
                    </article>
                  )
                })}
              </div>
            )}
          </div>
        </section>
      </section>
    </div>
  )
}
