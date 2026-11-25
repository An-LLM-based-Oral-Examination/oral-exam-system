export const MAX_TRANSCRIPT_EDIT_RATIO = 0.35
const MAX_LENGTH = 50_000

/**
 * Mirrors Backend TranscriptEditRatioCalculator.Normalize:
 * trim -> truncate 50_000 -> Unicode NFC -> collapse whitespace -> lowercase.
 */
export function normalizeTranscript(value: string | null | undefined): string {
  if (!value || !value.trim()) return ''
  const trimmed = value.trim().slice(0, MAX_LENGTH)
  return trimmed.normalize('NFC').replace(/\s+/g, ' ').toLowerCase()
}

function computeLevenshteinDistance(source: string, target: string): number {
  let a = source
  let b = target
  if (a.length < b.length) {
    a = target
    b = source
  }

  const n = b.length
  let prev = new Int32Array(n + 1)
  let curr = new Int32Array(n + 1)

  for (let j = 0; j <= n; j += 1) {
    prev[j] = j
  }

  for (let i = 1; i <= a.length; i += 1) {
    curr[0] = i
    const sc = a.charCodeAt(i - 1)
    for (let j = 1; j <= n; j += 1) {
      const cost = sc === b.charCodeAt(j - 1) ? 0 : 1
      const del = prev[j] + 1
      const ins = curr[j - 1] + 1
      const sub = prev[j - 1] + cost
      curr[j] = Math.min(del, ins, sub)
    }
    const swap = prev
    prev = curr
    curr = swap
  }

  return prev[n]
}

function round4(value: number): number {
  return Math.round((value + Number.EPSILON) * 10_000) / 10_000
}

/**
 * Computes normalized Levenshtein edit ratio in [0.0000, 1.0000].
 * Empty raw + non-empty edited returns 1.0000 (blocks typing without voice).
 */
export function calculateTranscriptEditRatio(
  initialVoiceTranscript: string | null | undefined,
  editedTranscript: string | null | undefined,
): number {
  const raw = normalizeTranscript(initialVoiceTranscript)
  const edited = normalizeTranscript(editedTranscript)

  if (raw.length === 0 && edited.length === 0) return 0
  if (raw.length === 0 || edited.length === 0) return 1
  if (raw === edited) return 0

  const distance = computeLevenshteinDistance(raw, edited)
  const denominator = Math.max(raw.length, edited.length)
  return Math.max(0, Math.min(1, round4(distance / denominator)))
}

export interface TranscriptEditEvaluation {
  ratio: number
  percent: number
  isManuallyEdited: boolean
  isExceeded: boolean
}

export function evaluateTranscriptEdit(
  initialVoiceTranscript: string | null | undefined,
  editedTranscript: string | null | undefined,
): TranscriptEditEvaluation {
  const raw = normalizeTranscript(initialVoiceTranscript)
  const edited = normalizeTranscript(editedTranscript)
  const ratio = calculateTranscriptEditRatio(initialVoiceTranscript, editedTranscript)
  const percent = Math.round(ratio * 1000) / 10
  return {
    ratio,
    percent,
    isManuallyEdited: raw !== edited,
    isExceeded: ratio > MAX_TRANSCRIPT_EDIT_RATIO,
  }
}
