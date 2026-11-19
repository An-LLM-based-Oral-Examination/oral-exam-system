export interface ParsedAuditPayload {
  kind: 'empty' | 'json' | 'kv' | 'scalar'
  entries: Record<string, string>
  note?: string
  raw: string
}

export type AuditDiffChangeKind = 'added' | 'changed' | 'removed' | 'unchanged'

export interface AuditDiffRow {
  field: string
  before: string | null
  after: string | null
  change: AuditDiffChangeKind
}

export interface AuditDiffSummary {
  rows: AuditDiffRow[]
  changedCount: number
  oldNote?: string
  newNote?: string
  hasRaw: boolean
}

function formatScalarValue(value: unknown): string {
  if (value === null) return 'null'
  if (value === undefined) return ''
  if (typeof value === 'string') return value
  if (typeof value === 'number' || typeof value === 'boolean') return String(value)
  try {
    return JSON.stringify(value)
  } catch {
    return String(value)
  }
}

export function parseAuditPayload(raw?: string | null): ParsedAuditPayload {
  const trimmed = raw?.trim() ?? ''
  if (!trimmed) {
    return { kind: 'empty', entries: {}, raw: '' }
  }

  if (trimmed.startsWith('{') && trimmed.endsWith('}')) {
    try {
      const parsed = JSON.parse(trimmed) as unknown
      if (typeof parsed === 'object' && parsed !== null && !Array.isArray(parsed)) {
        const entries: Record<string, string> = {}
        for (const [key, val] of Object.entries(parsed as Record<string, unknown>)) {
          entries[key] = formatScalarValue(val)
        }
        return { kind: 'json', entries, raw: trimmed }
      }
    } catch {
      // Fall through to key=value or scalar parser when string is malformed JSON
    }
  }

  const pipeIndex = trimmed.indexOf(' | ')
  const bodyPart = pipeIndex >= 0 ? trimmed.slice(0, pipeIndex).trim() : trimmed
  const notePart = pipeIndex >= 0 ? trimmed.slice(pipeIndex + 3).trim() : undefined

  const segments = bodyPart.includes(';')
    ? bodyPart.split(';')
    : bodyPart.split(',')

  const kvEntries: Record<string, string> = {}
  let matchedPairCount = 0
  let totalNonEmptySegments = 0

  for (const segment of segments) {
    const part = segment.trim()
    if (!part) continue
    totalNonEmptySegments += 1
    const eqIndex = part.indexOf('=')
    if (eqIndex > 0) {
      const key = part.slice(0, eqIndex).trim()
      const val = part.slice(eqIndex + 1).trim()
      if (key && /^[A-Za-z0-9_.\-\s]+$/.test(key)) {
        kvEntries[key] = val
        matchedPairCount += 1
      }
    }
  }

  if (matchedPairCount > 0 && matchedPairCount === totalNonEmptySegments) {
    return {
      kind: 'kv',
      entries: kvEntries,
      note: notePart || undefined,
      raw: trimmed,
    }
  }

  return {
    kind: 'scalar',
    entries: { 'Giá trị': bodyPart },
    note: notePart || undefined,
    raw: trimmed,
  }
}

export function buildAuditDiffSummary(
  oldValues?: string | null,
  newValues?: string | null,
): AuditDiffSummary {
  const before = parseAuditPayload(oldValues)
  const after = parseAuditPayload(newValues)

  const orderedKeys: string[] = []
  const seen = new Set<string>()
  for (const key of [...Object.keys(before.entries), ...Object.keys(after.entries)]) {
    if (!seen.has(key)) {
      seen.add(key)
      orderedKeys.push(key)
    }
  }

  const rows: AuditDiffRow[] = orderedKeys.map((field) => {
    const hasOld = Object.prototype.hasOwnProperty.call(before.entries, field)
    const hasNew = Object.prototype.hasOwnProperty.call(after.entries, field)
    const oldVal = hasOld ? before.entries[field] : null
    const newVal = hasNew ? after.entries[field] : null

    let change: AuditDiffChangeKind = 'unchanged'
    if (!hasOld && hasNew) change = 'added'
    else if (hasOld && !hasNew) change = 'removed'
    else if (oldVal !== newVal) change = 'changed'

    return {
      field,
      before: oldVal,
      after: newVal,
      change,
    }
  })

  const changedCount = rows.filter((row) => row.change !== 'unchanged').length

  return {
    rows,
    changedCount,
    oldNote: before.note,
    newNote: after.note,
    hasRaw: before.kind !== 'empty' || after.kind !== 'empty',
  }
}

export function summarizeAuditDiff(oldValues?: string | null, newValues?: string | null): string {
  const summary = buildAuditDiffSummary(oldValues, newValues)
  if (!summary.hasRaw) return 'Không ghi nhận thay đổi'
  const meaningful = summary.rows.filter((row) => row.change !== 'unchanged')
  const targetRows = meaningful.length > 0 ? meaningful : summary.rows
  const preview = targetRows
    .slice(0, 2)
    .map((row) => {
      if (row.change === 'added') return `+ ${row.field}: ${row.after ?? ''}`
      if (row.change === 'removed') return `- ${row.field}: ${row.before ?? ''}`
      if (row.change === 'changed') return `${row.field}: ${row.before ?? '—'} → ${row.after ?? '—'}`
      return `${row.field}: ${row.after ?? row.before ?? ''}`
    })
    .join(' · ')
  const extraCount = targetRows.length - 2
  return extraCount > 0 ? `${preview} (+${extraCount} trường)` : preview
}
