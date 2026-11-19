export function readStringParam(
  params: URLSearchParams,
  key: string,
  fallback = '',
): string {
  const raw = params.get(key)
  if (raw === null) return fallback
  const trimmed = raw.trim()
  return trimmed || fallback
}

export function readEnumParam<T extends string>(
  params: URLSearchParams,
  key: string,
  allowed: readonly T[],
  fallback: T,
): T {
  const raw = params.get(key)?.trim()
  if (!raw) return fallback
  return (allowed as readonly string[]).includes(raw) ? (raw as T) : fallback
}

export function readPositiveIntParam(
  params: URLSearchParams,
  key: string,
  fallback = 1,
): number {
  const raw = params.get(key)
  if (!raw) return fallback
  const parsed = Number.parseInt(raw, 10)
  if (!Number.isFinite(parsed) || parsed < 1 || String(parsed) !== raw.trim()) {
    return fallback
  }
  return parsed
}

export function patchSearchParams(
  prev: URLSearchParams,
  patch: Record<string, string | number | null | undefined>,
  defaults: Record<string, string | number> = {},
): URLSearchParams {
  const next = new URLSearchParams(prev)
  const hasNonPageUpdate = Object.keys(patch).some((key) => key !== 'page')

  if (hasNonPageUpdate && !Object.prototype.hasOwnProperty.call(patch, 'page')) {
    const defaultPage = defaults.page ?? 1
    if (String(defaultPage) === '1') {
      next.delete('page')
    } else {
      next.set('page', String(defaultPage))
    }
  }

  for (const [key, value] of Object.entries(patch)) {
    if (value === undefined || value === null) {
      next.delete(key)
      continue
    }
    const serialized = String(value).trim()
    const defaultValue = defaults[key] !== undefined ? String(defaults[key]) : ''
    if (!serialized || serialized === defaultValue) {
      next.delete(key)
    } else {
      next.set(key, serialized)
    }
  }

  return next
}
