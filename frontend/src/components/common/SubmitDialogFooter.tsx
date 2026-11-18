import { LoaderCircle, type LucideIcon } from 'lucide-react'
import { DialogFooter } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'

interface SubmitDialogFooterProps {
  form: string
  onCancel: () => void
  label: string
  busyLabel: string
  busy?: boolean
  disabled?: boolean
  danger?: boolean
  icon?: LucideIcon
}

export function SubmitDialogFooter({
  form,
  onCancel,
  label,
  busyLabel,
  busy,
  disabled,
  danger,
  icon: Icon,
}: SubmitDialogFooterProps) {
  return (
    <DialogFooter className="bg-white">
      <button type="button" className={ds.outlineButton} disabled={busy} onClick={onCancel}>
        Hủy
      </button>
      <button
        type="submit"
        form={form}
        disabled={busy || disabled}
        className={cn(
          ds.primaryButton,
          danger && 'bg-[#a83249] hover:bg-[#8b263a] focus-visible:outline-[#a83249]',
        )}
      >
        {busy ? (
          <LoaderCircle
            className="size-4 animate-spin motion-reduce:animate-none"
            aria-hidden="true"
          />
        ) : (
          Icon && <Icon className="size-4" aria-hidden="true" />
        )}
        {busy ? busyLabel : label}
      </button>
    </DialogFooter>
  )
}
