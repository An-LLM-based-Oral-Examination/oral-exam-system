import { cn } from '@/lib/utils'

export function QuestionRowMetrics({
  bloom,
  score,
  className,
}: {
  bloom?: string
  score?: number
  className?: string
}) {
  const metricClass = cn('flex items-center gap-2 lg:block', className)
  return (
    <>
      <div className={metricClass}>
        <span className="text-xs font-semibold text-[#8995a6] lg:block">Bloom</span>
        <span className="mt-0.5 inline-flex rounded-lg bg-[#4988C4]/16 px-2 py-1 text-xs font-bold text-[#1C4D8D]">
          {bloom ?? '—'}
        </span>
      </div>
      <div className={metricClass}>
        <span className="text-xs font-semibold text-[#8995a6] lg:block">Barem</span>
        <span
          className={cn(
            'mt-0.5 text-sm font-black',
            score === undefined
              ? 'text-[#5b6b84]'
              : score === 10
                ? 'text-[#168367]'
                : 'text-[#b8404b]',
          )}
        >
          {score === undefined ? '—' : `${score.toFixed(1)} / 10`}
        </span>
      </div>
    </>
  )
}
