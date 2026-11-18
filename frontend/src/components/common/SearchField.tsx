import { Search } from 'lucide-react'
import type { ComponentPropsWithRef } from 'react'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'

export function SearchField({ className, ...props }: ComponentPropsWithRef<'input'>) {
  return (
    <>
      <Search
        className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[#5b6b84]"
        aria-hidden="true"
      />
      <input
        type="search"
        {...props}
        className={cn(ds.field, 'h-10 rounded-full pr-3 pl-9', className)}
      />
    </>
  )
}
