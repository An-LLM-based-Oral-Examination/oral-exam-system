import { Check } from 'lucide-react'
import { cn } from '@/lib/utils'

export function TwoStepProgress({
  label,
  step,
  steps,
  onStepChange,
  disabled = false,
  canAdvance = true,
}: {
  label: string
  step: 1 | 2
  steps: ReadonlyArray<{ id: 1 | 2; title: string }>
  onStepChange: (step: 1 | 2) => void
  disabled?: boolean
  canAdvance?: boolean
}) {
  return (
    <nav className="shrink-0 border-b border-[#82aedb]/55 bg-white py-3" aria-label={label}>
      <ol className="relative flex w-full items-start justify-center">
        <span aria-hidden="true" className="absolute top-3.5 inset-x-[27.5%] h-0.5 rounded-full bg-[#c5d6ea]" />
        <span
          aria-hidden="true"
          className={cn(
            'absolute top-3.5 left-[27.5%] h-0.5 rounded-full bg-[#1C4D8D] transition-all duration-300',
            step >= 2 ? 'w-[45%]' : 'w-0',
          )}
        />
        {steps.map((item) => {
          const visual = step > item.id ? 'done' : step === item.id ? 'current' : 'upcoming'
          return (
            <li key={item.id} className="relative z-[1] flex w-[45%] flex-col items-center">
              <button
                type="button"
                disabled={disabled || (item.id === 2 && !canAdvance)}
                onClick={() => onStepChange(item.id)}
                aria-label={item.title}
                aria-current={visual === 'current' ? 'step' : undefined}
                className={cn(
                  'flex size-7 items-center justify-center rounded-full text-xs font-black transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed',
                  visual === 'done' && 'bg-[#1C4D8D] text-white',
                  visual === 'current' && 'bg-[#1C4D8D] text-white ring-[3px] ring-[#1C4D8D]/22',
                  visual === 'upcoming' && 'border-2 border-[#82aedb]/55 bg-white text-[#1C4D8D]',
                )}
              >
                {visual === 'done' ? <Check className="size-3.5 stroke-[3]" aria-hidden="true" /> : item.id}
              </button>
              <p className={cn('mt-2 text-center text-xs font-bold', visual === 'current' ? 'text-[#1C4D8D]' : 'text-[#5b6b84]')}>
                {item.title}
              </p>
            </li>
          )
        })}
      </ol>
    </nav>
  )
}
