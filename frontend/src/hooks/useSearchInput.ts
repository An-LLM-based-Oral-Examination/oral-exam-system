import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { scheduleUi } from '@/lib/scheduleUi'

export const SEARCH_DELAY_MS = 500

export function useSearchInput(onApply?: (query: string) => void, initialValue = '') {
  const [draft, setDraft] = useState(initialValue)
  const [query, setQuery] = useState(initialValue.trim())
  const timer = useRef<number | undefined>(undefined)
  const lastInitialRef = useRef(initialValue)

  useEffect(() => () => window.clearTimeout(timer.current), [])

  useEffect(() => {
    if (initialValue === lastInitialRef.current) return
    lastInitialRef.current = initialValue
    window.clearTimeout(timer.current)
    return scheduleUi(() => {
      setDraft(initialValue)
      setQuery(initialValue.trim())
    })
  }, [initialValue])

  const apply = (value: string) => {
    window.clearTimeout(timer.current)
    const nextQuery = value.trim()
    if (nextQuery === query) return
    setQuery(nextQuery)
    onApply?.(nextQuery)
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

  const reset = (nextValue = '') => {
    window.clearTimeout(timer.current)
    lastInitialRef.current = nextValue
    setDraft(nextValue)
    setQuery(nextValue.trim())
  }

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key !== 'Enter' || event.nativeEvent.isComposing) return
    event.preventDefault()
    apply(event.currentTarget.value)
  }

  return { draft, query, change, onKeyDown, reset }
}
