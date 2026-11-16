import { useEffect, useRef, useState, type KeyboardEvent } from 'react'

export const SEARCH_DELAY_MS = 500

export function useSearchInput(onApply?: () => void) {
  const [draft, setDraft] = useState('')
  const [query, setQuery] = useState('')
  const timer = useRef<number | undefined>(undefined)

  useEffect(() => () => window.clearTimeout(timer.current), [])

  const apply = (value: string) => {
    window.clearTimeout(timer.current)
    const nextQuery = value.trim()
    if (nextQuery === query) return
    setQuery(nextQuery)
    onApply?.()
  }

  const change = (value: string) => {
    window.clearTimeout(timer.current)
    setDraft(value)
    if (!value.trim()) {
      apply(value)
      return
    }
    timer.current = window.setTimeout(() => apply(value), SEARCH_DELAY_MS)
  }

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key !== 'Enter' || event.nativeEvent.isComposing) return
    event.preventDefault()
    apply(event.currentTarget.value)
  }

  return { draft, query, change, onKeyDown }
}
