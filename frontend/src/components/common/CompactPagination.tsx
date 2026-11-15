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
      <button type="button" className="inline-flex min-h-7 items-center gap-1 rounded-md px-1.5 enabled:hover:bg-slate-100 disabled:opacity-40"
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
              className={cn('grid size-7 place-items-center rounded-full tabular-nums', number === page ? 'bg-[#1C4D8D] text-white' : 'text-[#0F2854] hover:bg-[#E7F3FB]')}
            >
              {number}
            </button>
          </span>
        ))}
      <button type="button" className="inline-flex min-h-7 items-center gap-1 rounded-md px-1.5 enabled:hover:bg-slate-100 disabled:opacity-40"
        disabled={page >= pageCount} onClick={() => onPageChange(page + 1)}>
        Sau <ChevronRight className="size-3.5" aria-hidden="true" />
      </button>
    </nav>
  )
}
