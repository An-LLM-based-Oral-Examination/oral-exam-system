import type {
  BatchPracticeAnswer,
  PracticeAnswerDetail,
  PracticeDifficulty,
  PracticeQuestion,
  PracticeSessionConfig,
} from '@/types/practice.types'

const STORAGE_PREFIX = 'mf01_practice_session:'

export interface PendingPracticeSubmission {
  questionId: string
  answerText: string
  isFollowUp: boolean
  parentAnswerId: string | null
  attemptedAt: string
  knownAnswerIds: string[]
}

export interface StoredPracticeRuntime {
  version: 1
  sessionId: string
  studentId: string
  transcriptBufferSeconds: number
  config: PracticeSessionConfig
  questions: PracticeQuestion[]
  drafts: BatchPracticeAnswer[]
  currentQuestionIndex: number
  mainQuestionsCompleted: number
  questionDifficulties: Record<string, PracticeDifficulty>
  activeFollowUp: {
    questionId: string
    prompt: string
    parentAnswerId: string
  } | null
  pendingSubmission?: PendingPracticeSubmission | null
  pendingAnswerId: string | null
  resolvedAnswer?: PracticeAnswerDetail | null
  submittedAnswerIdsByQuestion?: Record<string, string>
  fullSessionSubmitted: boolean
  /** Bản nháp hiện tại để khôi phục an toàn sau khi reload trang. */
  draftAnswerText?: string
  draftAnswerMode?: 'voice' | 'text'
  showBuffer?: boolean
  bufferDeadlineTimestamp?: number | null
}

function storageKey(sessionId: string) {
  return `${STORAGE_PREFIX}${sessionId}`
}

function isStringRecord(value: unknown): value is Record<string, string> {
  return (
    typeof value === 'object' &&
    value !== null &&
    !Array.isArray(value) &&
    Object.values(value).every((item) => typeof item === 'string')
  )
}

function isPendingSubmission(value: unknown): value is PendingPracticeSubmission {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return false
  const candidate = value as Partial<PendingPracticeSubmission>
  return (
    typeof candidate.questionId === 'string' &&
    typeof candidate.answerText === 'string' &&
    typeof candidate.isFollowUp === 'boolean' &&
    (candidate.parentAnswerId === null || typeof candidate.parentAnswerId === 'string') &&
    typeof candidate.attemptedAt === 'string' &&
    !Number.isNaN(Date.parse(candidate.attemptedAt)) &&
    Array.isArray(candidate.knownAnswerIds) &&
    candidate.knownAnswerIds.every((answerId) => typeof answerId === 'string')
  )
}

function isStoredRuntime(value: unknown): value is StoredPracticeRuntime {
  if (typeof value !== 'object' || value === null) return false
  const candidate = value as Partial<StoredPracticeRuntime>
  return (
    candidate.version === 1 &&
    typeof candidate.sessionId === 'string' &&
    typeof candidate.studentId === 'string' &&
    typeof candidate.transcriptBufferSeconds === 'number' &&
    typeof candidate.config === 'object' &&
    candidate.config !== null &&
    Array.isArray(candidate.questions) &&
    Array.isArray(candidate.drafts) &&
    typeof candidate.currentQuestionIndex === 'number' &&
    typeof candidate.mainQuestionsCompleted === 'number' &&
    typeof candidate.questionDifficulties === 'object' &&
    candidate.questionDifficulties !== null &&
    (candidate.activeFollowUp === null || typeof candidate.activeFollowUp === 'object') &&
    (candidate.pendingSubmission === undefined ||
      candidate.pendingSubmission === null ||
      isPendingSubmission(candidate.pendingSubmission)) &&
    (candidate.pendingAnswerId === null || typeof candidate.pendingAnswerId === 'string') &&
    (candidate.resolvedAnswer === undefined ||
      candidate.resolvedAnswer === null ||
      typeof candidate.resolvedAnswer === 'object') &&
    (candidate.submittedAnswerIdsByQuestion === undefined ||
      isStringRecord(candidate.submittedAnswerIdsByQuestion)) &&
    typeof candidate.fullSessionSubmitted === 'boolean'
  )
}

export function savePracticeRuntime(runtime: StoredPracticeRuntime) {
  window.sessionStorage.setItem(storageKey(runtime.sessionId), JSON.stringify(runtime))
}

export function loadPracticeRuntime(sessionId: string): StoredPracticeRuntime | null {
  try {
    const rawValue = window.sessionStorage.getItem(storageKey(sessionId))
    if (!rawValue) return null
    const value: unknown = JSON.parse(rawValue)
    if (!isStoredRuntime(value)) return null
    return {
      ...value,
      pendingSubmission: value.pendingSubmission ?? null,
      submittedAnswerIdsByQuestion: value.submittedAnswerIdsByQuestion ?? {},
      draftAnswerText: value.draftAnswerText ?? '',
      draftAnswerMode: value.draftAnswerMode ?? 'voice',
      showBuffer: value.showBuffer ?? false,
      bufferDeadlineTimestamp: value.bufferDeadlineTimestamp ?? null,
    }
  } catch {
    return null
  }
}

export function removePracticeRuntime(sessionId: string) {
  window.sessionStorage.removeItem(storageKey(sessionId))
}

export function clearPracticeRuntimes() {
  const keys = Object.keys(window.sessionStorage).filter((key) => key.startsWith(STORAGE_PREFIX))
  for (const key of keys) window.sessionStorage.removeItem(key)
}
