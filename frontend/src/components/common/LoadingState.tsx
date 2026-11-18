import { LoaderCircle } from 'lucide-react'
import { cn } from '@/lib/utils'

export function LoadingState({
  label = 'Đang tải dữ liệu…',
  className,
}: {
  label?: string
  className?: string
}) {
  return (
    <div
      role="status"
      className={cn(
        'flex min-h-32 items-center justify-center gap-2 text-sm text-[#5b6b84]',
        className,
      )}
    >
      <LoaderCircle
        className="size-5 animate-spin text-[#1C4D8D] motion-reduce:animate-none"
        aria-hidden="true"
      />
      {label}
    </div>
  )
}
