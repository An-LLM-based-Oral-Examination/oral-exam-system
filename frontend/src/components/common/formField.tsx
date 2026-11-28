import { cn } from '@/lib/utils'

export function FieldError({ message, className }: { message?: string | null; className?: string }) {
  if (!message?.trim()) return null
  return <p className={cn('text-xs font-semibold text-[#c2415b]', className)}>{message}</p>
}
