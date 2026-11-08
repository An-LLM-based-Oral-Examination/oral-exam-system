import { cn } from '@/lib/utils'

/** Viền/nền khi input không hợp lệ — dùng với `ds.field`. */
export const fieldInvalidClass = 'border-[#c2415b] bg-[#fff7f8] focus:ring-[#c2415b]/25'

export function fieldLabelClass(invalid?: boolean) {
  return cn(
    'text-xs font-bold tracking-[0.04em] uppercase',
    invalid ? 'text-[#c2415b]' : 'text-[#5b6b84]',
  )
}
