export type JsonRecord = Record<string, unknown>

export function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

export function field(record: JsonRecord, key: string, ...aliases: string[]): unknown {
  for (const candidate of [key, ...aliases]) {
    if (candidate in record) return record[candidate]
    const pascal = `${candidate.charAt(0).toUpperCase()}${candidate.slice(1)}`
    if (pascal in record) return record[pascal]
  }
  return undefined
}

export function unwrapPayload(payload: unknown): unknown {
  if (!isRecord(payload)) return payload
  for (const key of ['value', 'Value', 'data', 'Data', 'result', 'Result']) {
    if (key in payload && payload[key] !== undefined && payload[key] !== null) return payload[key]
  }
  return payload
}

export function asArray(payload: unknown, detail: string): unknown[] {
  const candidate = unwrapPayload(payload)
  if (Array.isArray(candidate)) return candidate
  if (isRecord(candidate)) {
    const nested = field(candidate, 'items', 'Items')
    if (Array.isArray(nested)) return nested
  }
  return invalidResponse(detail)
}

export function invalidResponse(detail: string): never {
  throw { status: 502, title: 'Phản hồi API không hợp lệ', detail }
}

export function requireRecord(value: unknown, detail: string): JsonRecord {
  if (!isRecord(value)) return invalidResponse(detail)
  return value
}

export function readString(record: JsonRecord, key: string, ...aliases: string[]): string | null {
  const value = field(record, key, ...aliases)
  return typeof value === 'string' ? value : null
}

export function requiredString(record: JsonRecord, key: string, detail: string, ...aliases: string[]) {
  const value = readString(record, key, ...aliases)
  if (!value?.trim()) return invalidResponse(detail)
  return value
}

export function readNumber(record: JsonRecord, key: string, ...aliases: string[]): number | null {
  const value = field(record, key, ...aliases)
  if (typeof value === 'number' && Number.isFinite(value)) return value
  if (typeof value === 'string' && value.trim() && Number.isFinite(Number(value))) return Number(value)
  return null
}

export function requiredNumber(record: JsonRecord, key: string, detail: string, ...aliases: string[]) {
  const value = readNumber(record, key, ...aliases)
  if (value === null) return invalidResponse(detail)
  return value
}

export function readBoolean(record: JsonRecord, key: string, ...aliases: string[]): boolean | null {
  const value = field(record, key, ...aliases)
  return typeof value === 'boolean' ? value : null
}

export function requiredBoolean(record: JsonRecord, key: string, detail: string, ...aliases: string[]) {
  const value = readBoolean(record, key, ...aliases)
  if (value === null) return invalidResponse(detail)
  return value
}

export function readNullableString(record: JsonRecord, key: string, ...aliases: string[]): string | null {
  const value = field(record, key, ...aliases)
  return value === null || value === undefined ? null : typeof value === 'string' ? value : null
}

export function readNullableNumber(record: JsonRecord, key: string, ...aliases: string[]): number | null {
  const value = field(record, key, ...aliases)
  return value === null || value === undefined ? null : readNumber(record, key, ...aliases)
}

export function requiredDateString(record: JsonRecord, key: string, detail: string, ...aliases: string[]) {
  const value = requiredString(record, key, detail, ...aliases)
  if (Number.isNaN(Date.parse(value))) return invalidResponse(detail)
  return value
}

export function normalizeFileName(contentDisposition: string | undefined, fallback: string) {
  const match = contentDisposition?.match(/filename\*?=(?:UTF-8'')?([^;"]+)/i)
  if (!match?.[1]) return fallback
  try {
    return decodeURIComponent(match[1].trim())
  } catch {
    return match[1].trim()
  }
}
