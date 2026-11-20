import { useCallback } from 'react'
import { useSearchParams } from 'react-router-dom'
import {
  patchSearchParams,
  readEnumParam,
  readPositiveIntParam,
  readStringParam,
} from '@/lib/urlSearchParams'

export function useUrlQueryState(defaults: Record<string, string | number> = {}) {
  const [searchParams, setSearchParams] = useSearchParams()

  const getString = useCallback(
    (key: string, fallback = '') => readStringParam(searchParams, key, fallback),
    [searchParams],
  )

  const getEnum = useCallback(
    <T extends string>(key: string, allowed: readonly T[], fallback: T) =>
      readEnumParam(searchParams, key, allowed, fallback),
    [searchParams],
  )

  const getPage = useCallback(
    (key = 'page', fallback = 1) => readPositiveIntParam(searchParams, key, fallback),
    [searchParams],
  )

  const setParams = useCallback(
    (patch: Record<string, string | number | null | undefined>) => {
      setSearchParams((prev) => patchSearchParams(prev, patch, defaults), { replace: true })
    },
    [defaults, setSearchParams],
  )

  const resetParams = useCallback(
    (keys: readonly string[]) => {
      const patch: Record<string, null> = {}
      for (const key of keys) {
        patch[key] = null
      }
      setSearchParams((prev) => patchSearchParams(prev, patch, defaults), { replace: true })
    },
    [defaults, setSearchParams],
  )

  return {
    searchParams,
    getString,
    getEnum,
    getPage,
    setParams,
    resetParams,
  }
}
