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
  variant?: 'default' | 'calm'
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
  variant = 'default',
}: SubmitDialogFooterProps) {
  const isCalm = variant === 'calm'
  return (
    <DialogFooter variant={variant} className="bg-white">
      <button
        type="button"
        className={
          isCalm
            ? 'inline-flex min-h-11 items-center justify-center gap-1.5 rounded-xl border border-[#0F2854]/15 bg-white px-4 text-xs font-medium text-[#5b6b84] transition-colors duration-150 hover:bg-[#F4FBFD] hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:pointer-events-none disabled:opacity-45 motion-reduce:transition-none'
            : ds.outlineButton
        }
        disabled={busy}
        onClick={onCancel}
      >
        Hủy
      </button>
      <button
        type="submit"
        form={form}
        disabled={busy || disabled}
        className={cn(
          isCalm
            ? 'inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-xs font-medium text-white transition-colors duration-150 hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:pointer-events-none disabled:opacity-45 motion-reduce:transition-none'
            : ds.primaryButton,
          danger && 'bg-[#a83249] hover:bg-[#8b263a] focus-visible:outline-[#a83249]',
        )}
      >
        {busy ? (
          <LoaderCircle
            className="size-4 animate-spin motion-reduce:animate-none"
            aria-hidden="true"
          />
        ) : (
          Icon && <Icon className="size-4" strokeWidth={1.5} aria-hidden="true" />
        )}
        {busy ? busyLabel : label}
      </button>
    </DialogFooter>
  )
}
