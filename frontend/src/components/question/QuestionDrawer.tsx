import { ArrowLeft, ArrowRight, X } from 'lucide-react'
import type { ReactNode, Ref } from 'react'
import { SideDrawer } from '@/design/overlays'

interface QuestionDrawerProps {
  title: string
  badges?: ReactNode
  meta?: ReactNode
  onPrevious?: () => void
  onNext?: () => void
  onClose: () => void
  closeRef?: Ref<HTMLButtonElement>
  footer?: ReactNode
  children: ReactNode
}

const navigationButton =
  'flex size-9 items-center justify-center rounded-xl border border-[#4988C4] bg-white text-[#1C4D8D] transition hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] disabled:opacity-35'

export function QuestionDrawer({
  title,
  badges,
  meta,
  onPrevious,
  onNext,
  onClose,
  closeRef,
  footer,
  children,
}: QuestionDrawerProps) {
  return (
    <SideDrawer
      label="Chi tiết câu hỏi"
      onClose={onClose}
      bodyClassName="bg-white p-0 sm:p-0"
      footer={
        footer && (
          <div className="shrink-0 border-t border-[#82aedb]/55 bg-white px-4 py-3 sm:px-5">
            {footer}
          </div>
        )
      }
      header={
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0 flex-1">
            {badges && <div className="flex flex-wrap items-center gap-2">{badges}</div>}
            <h2 className="mt-1.5 line-clamp-2 text-base font-bold wrap-anywhere text-[#0F2854] sm:text-lg">
              {title}
            </h2>
            {meta && (
              <p className="mt-1.5 text-xs font-medium wrap-anywhere text-[#6f8198]">{meta}</p>
            )}
          </div>
          <div className="flex shrink-0 items-center gap-1.5">
            <button
              type="button"
              className={navigationButton}
              aria-label="Câu hỏi trước"
              disabled={!onPrevious}
              onClick={onPrevious}
            >
              <ArrowLeft className="size-4" aria-hidden="true" />
            </button>
            <button
              type="button"
              className={navigationButton}
              aria-label="Câu hỏi tiếp"
              disabled={!onNext}
              onClick={onNext}
            >
              <ArrowRight className="size-4" aria-hidden="true" />
            </button>
            <button
              ref={closeRef}
              type="button"
              className={navigationButton}
              aria-label="Đóng chi tiết câu hỏi"
              onClick={onClose}
            >
              <X className="size-4" aria-hidden="true" />
            </button>
          </div>
        </div>
      }
    >
      {children}
    </SideDrawer>
  )
}
