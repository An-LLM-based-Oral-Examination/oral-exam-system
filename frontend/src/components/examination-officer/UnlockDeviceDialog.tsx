import { useState, type FormEvent } from 'react'
import { KeyRound, Laptop, LoaderCircle, ShieldAlert } from 'lucide-react'
import { officialExamService } from '@/services/officialExam.service'
import type { UnlockTicketDeviceResponse } from '@/types/officialExam.types'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { toast } from '@/components/common/toastStore'
import { userMessage } from '@/lib/userMessage'

const MIN_UNLOCK_REASON_LENGTH = 10
const MAX_UNLOCK_REASON_LENGTH = 500

export interface UnlockDeviceTarget {
  ticketId: string
  studentCode: string
  fullName?: string | null
  seatNumber: number
  deviceId?: string | null
  deviceUnlockCount?: number
}

interface UnlockDeviceDialogProps {
  open: boolean
  target: UnlockDeviceTarget | null
  onClose: () => void
  onUnlocked: (result: UnlockTicketDeviceResponse) => Promise<void> | void
}

export function UnlockDeviceDialog({
  open,
  target,
  onClose,
  onUnlocked,
}: UnlockDeviceDialogProps) {
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  if (!open || !target) return null

  const trimmedLength = reason.trim().length
  const isLengthInvalid =
    trimmedLength < MIN_UNLOCK_REASON_LENGTH || trimmedLength > MAX_UNLOCK_REASON_LENGTH

  const handleClose = () => {
    if (busy) return
    setReason('')
    setError('')
    onClose()
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const trimmed = reason.trim()
    if (trimmed.length < MIN_UNLOCK_REASON_LENGTH) {
      setError(`Lý do mở khóa thiết bị cần ít nhất ${MIN_UNLOCK_REASON_LENGTH} ký tự.`)
      return
    }
    if (trimmed.length > MAX_UNLOCK_REASON_LENGTH) {
      setError(`Lý do mở khóa thiết bị không được vượt quá ${MAX_UNLOCK_REASON_LENGTH} ký tự.`)
      return
    }

    setBusy(true)
    setError('')
    try {
      const result = await officialExamService.unlockTicketDevice(target.ticketId, trimmed)
      toast.success(
        `Đã mở khóa thiết bị cho ${result.studentCode || target.studentCode} (lần mở khóa #${result.deviceUnlockCount}).`,
      )
      setReason('')
      await Promise.resolve(onUnlocked(result))
      onClose()
    } catch (submitError) {
      const message = userMessage(
        submitError,
        'Không thể mở khóa thiết bị BYOD cho vé thi này.',
      )
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
      icon={KeyRound}
      eyebrow="MỞ KHÓA THIẾT BỊ BYOD · KHẢO THÍ"
      title="Mở khóa đổi thiết bị cho thí sinh"
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
            form="eo-unlock-device-form"
            disabled={busy}
            className={ds.primaryButton}
          >
            {busy ? <LoaderCircle className="size-4 animate-spin" /> : <KeyRound className="size-4" />}
            {busy ? 'Đang mở khóa…' : 'Xác nhận mở khóa thiết bị'}
          </button>
        </DialogFooter>
      }
    >
      <form id="eo-unlock-device-form" onSubmit={handleSubmit} className="space-y-3">
        <div className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-3.5 text-xs text-[#52657d]">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <strong className="text-sm text-[#0F2854]">
              Ghế #{String(target.seatNumber).padStart(2, '0')} · {target.studentCode}
              {target.fullName ? ` — ${target.fullName}` : ''}
            </strong>
            <span className={ds.infoChip}>
              Đã mở khóa: {target.deviceUnlockCount ?? 0} lần
            </span>
          </div>
          <p className="mt-2 flex items-center gap-1.5 font-mono text-[11px] text-[#245997]">
            <Laptop className="size-3.5 shrink-0" aria-hidden="true" />
            Thiết bị hiện tại: {target.deviceId || 'Chưa gắn thiết bị'}
          </p>
          <p className="mt-2 flex items-start gap-1.5 text-[11px] text-[#687a91]">
            <ShieldAlert className="mt-0.5 size-3.5 shrink-0 text-[#1C4D8D]" aria-hidden="true" />
            Thao tác chỉ áp dụng khi vé chưa nộp/chưa khóa điểm. Phiên cũ trên máy cũ sẽ bị thu hồi
            tức thì và hệ thống ghi nhận AuditLog.
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

        <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="unlock-device-reason">
          <span className="flex items-center justify-between gap-2">
            <span>Lý do mở khóa thiết bị (bắt buộc 10–500 ký tự) *</span>
            <span
              className={cn(
                'font-mono text-[11px]',
                trimmedLength > 0 && isLengthInvalid ? 'text-[#a43d4b]' : 'text-[#687a91]',
              )}
            >
              {trimmedLength}/{MAX_UNLOCK_REASON_LENGTH}
            </span>
          </span>
          <textarea
            id="unlock-device-reason"
            rows={4}
            maxLength={550}
            className={cn(
              ds.field,
              'min-h-24 py-2',
              error && 'border-[#d66474] focus:border-[#a43d4b]',
            )}
            placeholder="Ví dụ: Laptop cá nhân của thí sinh bị treo nguồn, Giám thị xác nhận chuyển sang máy dự phòng Lab 301."
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
