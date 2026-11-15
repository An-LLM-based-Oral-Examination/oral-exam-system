import { Users } from 'lucide-react'
import { cn } from '@/lib/utils'

export function ClassCapacity({ enrolledCount, maxStudents }: {
  enrolledCount: number
  maxStudents?: number | null
}) {
  const seatsLeft = Math.max(0, (maxStudents ?? 0) - enrolledCount)
  const fillRatio = maxStudents && maxStudents > 0 ? Math.min(1, Math.max(0, enrolledCount / maxStudents)) : 0
  const nearlyFull = fillRatio >= 0.85

  return (
    <div className="mt-4 space-y-1.5">
      <div className="flex items-center justify-between gap-2 text-xs font-bold">
        <span className="inline-flex items-center gap-1 text-[#5b6b84]">
          <Users className="size-3.5 text-[#1C4D8D]" aria-hidden="true" />
          Sĩ số
        </span>
        <span className={cn(nearlyFull ? 'text-[#c26a12]' : 'text-[#0F2854]')}>
          {enrolledCount}
          {maxStudents != null && <span className="font-semibold text-[#5b6b84]">/{maxStudents}</span>}
          {maxStudents != null && <span className="ml-1.5 font-semibold text-[#5b6b84]">· còn {seatsLeft}</span>}
        </span>
      </div>
      {maxStudents != null && maxStudents > 0 && (
        <div className="h-2 overflow-hidden rounded-full bg-[#E7F3FB] ring-1 ring-[#82aedb]/30">
          <div
            className={cn(
              'h-full rounded-full transition-[width] duration-300',
              nearlyFull
                ? 'bg-[linear-gradient(90deg,#e3a23a,#c26a12)]'
                : 'bg-[linear-gradient(90deg,#4988C4,#1C4D8D)]',
            )}
            style={{ width: `${Math.max(fillRatio * 100, enrolledCount > 0 ? 8 : 0)}%` }}
          />
        </div>
      )}
    </div>
  )
}
