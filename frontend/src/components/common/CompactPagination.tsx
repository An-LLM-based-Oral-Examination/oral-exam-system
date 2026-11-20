import { ChevronLeft, ChevronRight } from 'lucide-react'
import { cn } from '@/lib/utils'

export function CompactPagination({ label, page, pageCount, onPageChange }: {
  label: string
  page: number
  pageCount: number
  onPageChange: (page: number) => void
}) {
  return (
    <nav aria-label={label} className="flex shrink-0 flex-wrap items-center justify-center gap-1 text-xs font-semibold">
      <button type="button" className="inline-flex min-h-11 min-w-11 items-center justify-center gap-1 rounded-md px-2 transition-colors duration-150 sm:min-h-8 sm:min-w-8 enabled:hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-40 motion-reduce:transition-none"
        disabled={page === 1} onClick={() => onPageChange(page - 1)}>
        <ChevronLeft className="size-3.5" aria-hidden="true" /> Trước
      </button>
      {[...new Set([1, page - 1, page, page + 1, pageCount])]
        .filter((page) => page >= 1 && page <= pageCount)
        .map((number, index, pages) => (
          <span key={number} className="inline-flex items-center gap-1">
            {index > 0 && number - pages[index - 1] > 1 && <span aria-hidden="true">…</span>}
            <button
              type="button"
              aria-label={`Trang ${number}`}
              aria-current={number === page ? 'page' : undefined}
              onClick={() => onPageChange(number)}
              className={cn('grid min-h-11 min-w-11 place-items-center rounded-full tabular-nums transition-colors duration-150 sm:min-h-8 sm:min-w-8 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none', number === page ? 'bg-[#1C4D8D] text-white' : 'text-[#0F2854] hover:bg-[#E7F3FB]')}
            >
              {number}
            </button>
          </span>
        ))}
      <button type="button" className="inline-flex min-h-11 min-w-11 items-center justify-center gap-1 rounded-md px-2 transition-colors duration-150 sm:min-h-8 sm:min-w-8 enabled:hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-40 motion-reduce:transition-none"
        disabled={page >= pageCount} onClick={() => onPageChange(page + 1)}>
        Sau <ChevronRight className="size-3.5" aria-hidden="true" />
      </button>
    </nav>
  )
}
