import { cn } from '@/lib/utils'
import { DIFFICULTY_LABELS } from '@/lib/examPreparation'

export function QuestionDifficultyBadge({ difficulty }: { difficulty: string }) {
  return (
    <span
      className={cn(
        'inline-flex shrink-0 items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs leading-none font-bold whitespace-nowrap',
        difficulty === 'easy'
          ? 'border-[#a9dfc7] bg-[#eaf8f1] text-[#16836a]'
          : difficulty === 'medium'
            ? 'border-[#eed39a] bg-[#fff4df] text-[#a5660e]'
            : 'border-[#efbdc6] bg-[#fdf0f2] text-[#c2415b]',
      )}
    >
      <span aria-hidden="true" className="size-1.5 rounded-full bg-current" />
      {DIFFICULTY_LABELS[difficulty] ?? difficulty}
    </span>
  )
}
