import { isLikelySilentOrInvalidTranscript } from '@/lib/transcriptGuards'

export interface SttSegmentOutcome {
  transcript: string | null
  confidenceScore?: number | null
  provider?: string | null
  usedFallback?: boolean
  isSilence: boolean
  failed: boolean
  errorDetail?: string
}

export function extractSegmentErrorStatus(error: unknown): number {
  if (
    typeof error === 'object' &&
    error !== null &&
    'status' in error &&
    typeof (error as { status?: unknown }).status === 'number'
  ) {
    return (error as { status: number }).status
  }
  return 500
}

export function extractSegmentErrorDetail(error: unknown): string | undefined {
  if (
    typeof error === 'object' &&
    error !== null &&
    'detail' in error &&
    typeof (error as { detail?: unknown }).detail === 'string'
  ) {
    const trimmed = (error as { detail: string }).detail.trim()
    return trimmed || undefined
  }
  if (error instanceof Error && error.message.trim()) {
    return error.message.trim()
  }
  return undefined
}

export function classifySegmentError(error: unknown): SttSegmentOutcome {
  const status = extractSegmentErrorStatus(error)
  // 422 từ /speech/stt hoặc /storage/upload-audio nghĩa là im lặng / không có giọng nói trong đoạn.
  if (status === 422) {
    return { transcript: null, isSilence: true, failed: false }
  }
  return {
    transcript: null,
    isSilence: false,
    failed: true,
    errorDetail: extractSegmentErrorDetail(error),
  }
}

export function summarizeSegmentOutcomes(results: readonly SttSegmentOutcome[]) {
  const failedOutcomes = results.filter((item) => item.failed)
  const validOutcomes = results.filter(
    (item) =>
      !item.failed &&
      typeof item.transcript === 'string' &&
      item.transcript.trim().length > 0 &&
      !isLikelySilentOrInvalidTranscript(item.transcript, 20),
  )
  return {
    failedCount: failedOutcomes.length,
    firstErrorDetail: failedOutcomes.find((item) => Boolean(item.errorDetail))?.errorDetail,
    validOutcomes,
  }
}
