import type { ComponentProps } from 'react'
import { CompactPagination } from './CompactPagination'

export function ListPagination(props: ComponentProps<typeof CompactPagination>) {
  return (
    <div className="border-t border-[#82aedb]/55 bg-white px-4 py-3 sm:px-5">
      <CompactPagination {...props} />
    </div>
  )
}
