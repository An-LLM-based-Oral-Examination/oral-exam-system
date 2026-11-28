import { RefreshCw } from 'lucide-react'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { PAPER_LABELS, PREPARATION_LABELS } from '@/lib/examPreparation'
import type { ExamBlueprint } from '@/types/examPreparation.types'
export function ExamStatusBadge({
  status,
  preparation = false,
}: {
  status: string
  preparation?: boolean
}) {
  const tone = ['OFFICIAL', 'READY_FOR_HANDOVER', 'HANDED_OVER'].includes(status)
    ? ds.successChip
    : ['REJECTED', 'NEEDS_REVISION'].includes(status)
      ? ds.dangerChip
      : status === 'SUBMITTED_FOR_REVIEW' || status === 'REQUESTED'
        ? 'inline-flex rounded-full border border-[#f3d19a] bg-[#fff8eb] px-2.5 py-1 text-xs font-bold text-[#9a6b12]'
        : ds.infoChip
  return (
    <span className={cn(tone, 'shrink-0 whitespace-nowrap')}>
      {(preparation ? PREPARATION_LABELS : PAPER_LABELS)[status] ?? status}
    </span>
  )
}
export function ExamLoadError({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div
      role="alert"
      className="rounded-xl border border-[#f3c2c8] bg-[#fff7f8] p-4 text-sm text-[#a83249]"
    >
      <p className="wrap-anywhere">{message}</p>
      {onRetry && (
        <button type="button" onClick={onRetry} className={cn(ds.outlineButton, 'mt-3')}>
          Thử lại
        </button>
      )}
    </div>
  )
}
export function ExamRefreshButton({ onClick, busy }: { onClick: () => void; busy?: boolean }) {
  return (
    <button
      type="button"
      aria-label="Làm mới"
      title="Làm mới"
      onClick={onClick}
      disabled={busy}
      className={cn(ds.outlineButton, 'size-10 shrink-0 rounded-full p-0 disabled:opacity-45')}
    >
      <RefreshCw className={cn('size-4', busy && 'animate-spin')} aria-hidden="true" />
    </button>
  )
}
export function BlueprintFacts({ blueprint }: { blueprint: Omit<ExamBlueprint, 'id'> }) {
  return (
    <div className="flex min-w-0 flex-wrap items-center gap-2">
      {[
        `${blueprint.totalQuestions} câu/đề`,
        `${blueprint.durationMinutes} phút`,
        `Dễ ${blueprint.easyCount}`,
        `Trung bình ${blueprint.mediumCount}`,
        `Khó ${blueprint.hardCount}`,
        `${blueprint.cloIds.length} CLO`,
      ].map((text) => (
        <span key={text} className={ds.infoChip}>
          {text}
        </span>
      ))}
    </div>
  )
}
