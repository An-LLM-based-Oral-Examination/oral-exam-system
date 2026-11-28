import { useState, type FormEvent } from 'react'
import { Ban, LoaderCircle, ShieldAlert } from 'lucide-react'
import { proctorService } from '@/services/proctor.service'
import type { TicketProctorActionResponse } from '@/types/proctor.types'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { toast } from '@/components/common/toastStore'
import { userMessage } from '@/lib/userMessage'

const MIN_SUSPEND_REASON_LENGTH = 10
const MAX_SUSPEND_REASON_LENGTH = 500

export interface SuspendTicketTarget {
  ticketId: string
  studentCode: string
  fullName?: string | null
  seatNumber: number
  violationCount?: number
}

interface SuspendTicketDialogProps {
  open: boolean
  target: SuspendTicketTarget | null
  onClose: () => void
  onSuspended: (result: TicketProctorActionResponse) => Promise<void> | void
}

export function SuspendTicketDialog({
  open,
  target,
  onClose,
  onSuspended,
}: SuspendTicketDialogProps) {
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  if (!open || !target) return null

  const trimmedLength = reason.trim().length
  const isLengthInvalid =
    trimmedLength < MIN_SUSPEND_REASON_LENGTH || trimmedLength > MAX_SUSPEND_REASON_LENGTH

  const handleClose = () => {
    if (busy) return
    setReason('')
    setError('')
    onClose()
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const trimmed = reason.trim()
    if (trimmed.length < MIN_SUSPEND_REASON_LENGTH) {
      setError(`Lý do đình chỉ thi cần ít nhất ${MIN_SUSPEND_REASON_LENGTH} ký tự.`)
      return
    }
    if (trimmed.length > MAX_SUSPEND_REASON_LENGTH) {
      setError(`Lý do đình chỉ thi không được vượt quá ${MAX_SUSPEND_REASON_LENGTH} ký tự.`)
      return
    }

    setBusy(true)
    setError('')
    try {
      const result = await proctorService.suspendTicket(target.ticketId, {
        reason: trimmed,
        blurViolationCount: target.violationCount ?? 0,
      })
      toast.success(`Đã đình chỉ thi đối với ${result.studentCode || target.studentCode}.`)
      setReason('')
      await Promise.resolve(onSuspended(result))
      onClose()
    } catch (submitError) {
      const message = userMessage(submitError, 'Không thể đình chỉ vé thi này.')
      setError(message)
      toast.error(message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <FormDialog
      open={open}
      align="center"
      size="md"
      variant="calm"
      icon={Ban}
      eyebrow="ĐÌNH CHỈ THI · KHẢO THÍ"
      title="Xác nhận đình chỉ thi thí sinh"
      busy={busy}
      onClose={handleClose}
      footer={
        <DialogFooter variant="calm">
          <button
            type="button"
            className={ds.outlineButton}
            onClick={handleClose}
            disabled={busy}
          >
            Hủy
          </button>
          <button
            type="submit"
            form="eo-suspend-ticket-form"
            disabled={busy}
            className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-[#c2415b] px-4 text-xs font-bold text-white transition hover:bg-[#a43249] disabled:opacity-50"
          >
            {busy ? <LoaderCircle className="size-4 animate-spin" /> : <Ban className="size-4" />}
            {busy ? 'Đang đình chỉ…' : 'Xác nhận đình chỉ thi'}
          </button>
        </DialogFooter>
      }
    >
      <form id="eo-suspend-ticket-form" onSubmit={handleSubmit} className="space-y-4">
        <div className="rounded-xl border border-[#f3c2c8] bg-[#fff7f8] p-3.5 text-xs text-[#6f3340]">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <strong className="text-sm text-[#0F2854]">
              Ghế #{String(target.seatNumber).padStart(2, '0')} · {target.studentCode}
              {target.fullName ? ` — ${target.fullName}` : ''}
            </strong>
            <span className={ds.dangerChip}>Vi phạm: {target.violationCount ?? 0}</span>
          </div>
          <p className="mt-2 flex items-start gap-1.5 text-[11px]">
            <ShieldAlert className="mt-0.5 size-3.5 shrink-0 text-[#c2415b]" aria-hidden="true" />
            Vé thi sẽ chuyển sang trạng thái khóa đình chỉ (LOCKED), thí sinh không thể tiếp tục làm
            bài và hệ thống sẽ ghi biên bản kiểm toán.
          </p>
        </div>

        {error ? (
          <div
            role="alert"
            className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-xs font-semibold text-[#a43d4b]"
          >
            {error}
          </div>
        ) : null}

        <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="suspend-ticket-reason">
          <span className="flex items-center justify-between gap-2">
            <span>Lý do đình chỉ thi (bắt buộc 10–500 ký tự) *</span>
            <span
              className={cn(
                'font-mono text-[11px]',
                trimmedLength > 0 && isLengthInvalid ? 'text-[#a43d4b]' : 'text-[#687a91]',
              )}
            >
              {trimmedLength}/{MAX_SUSPEND_REASON_LENGTH}
            </span>
          </span>
          <textarea
            id="suspend-ticket-reason"
            rows={4}
            maxLength={550}
            className={cn(
              ds.field,
              'min-h-24 py-2',
              error && 'border-[#d66474] focus:border-[#a43d4b]',
            )}
            placeholder="Ví dụ: Thí sinh cố tình rời màn hình thi nhiều lần và sử dụng tài liệu trái phép theo biên bản của Giám thị."
            value={reason}
            onChange={(event) => {
              setReason(event.target.value)
              if (error) setError('')
            }}
            aria-invalid={Boolean(error)}
          />
        </label>
      </form>
    </FormDialog>
  )
}
