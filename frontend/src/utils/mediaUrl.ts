/** Resolve `/uploads/...` for Vite/nginx proxy or absolute API origin. */
export function resolvePublicAssetUrl(path?: string | null): string | null {
  const trimmed = path?.trim()
  if (!trimmed) return null
  if (/^(https?:|data:|blob:)/i.test(trimmed)) return trimmed
  if (!trimmed.startsWith('/')) return trimmed

  const apiBase = import.meta.env.VITE_API_BASE_URL
  if (typeof apiBase === 'string' && /^https?:\/\//i.test(apiBase)) {
    try {
      return `${new URL(apiBase).origin}${trimmed}`
    } catch {
      return trimmed
    }
  }

  return trimmed
}
