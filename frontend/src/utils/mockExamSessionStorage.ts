import type { StoredMockExamAnswer, StoredMockExamSession } from '@/types/mockExam.types'

const STORAGE_KEY = 'mf02_mock_exam_session'

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function isStoredSession(value: unknown): value is StoredMockExamSession {
  if (!isRecord(value)) return false
  return (
    typeof value.sessionId === 'string' &&
    typeof value.courseId === 'string' &&
    typeof value.courseCode === 'string' &&
    typeof value.courseName === 'string' &&
    typeof value.hasFollowUp === 'boolean' &&
    typeof value.durationMinutes === 'number' &&
    typeof value.startedAt === 'string' &&
    typeof value.expiresAt === 'string' &&
    typeof value.usedQuota === 'number' &&
    typeof value.remainingQuota === 'number' &&
    typeof value.maxDailyQuota === 'number' &&
    Array.isArray(value.questions) &&
    Array.isArray(value.answers)
  )
}

export function secondsUntilExpiry(expiresAt: string) {
  const expiresAtMs = Date.parse(expiresAt)
  if (Number.isNaN(expiresAtMs)) return 0
  return Math.max(0, Math.floor((expiresAtMs - Date.now()) / 1000))
}

export function readMockExamSession(): StoredMockExamSession | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const parsed: unknown = JSON.parse(raw)
    return isStoredSession(parsed) ? parsed : null
  } catch {
    return null
  }
}

export function saveMockExamSession(session: StoredMockExamSession) {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session))
}

export function saveMockExamAnswer(answer: StoredMockExamAnswer) {
  const session = readMockExamSession()
  if (!session) return
  const answers = session.answers.filter((item) => item.questionId !== answer.questionId)
  answers.push(answer)
  saveMockExamSession({ ...session, answers })
}

export function clearMockExamSession() {
  sessionStorage.removeItem(STORAGE_KEY)
}
