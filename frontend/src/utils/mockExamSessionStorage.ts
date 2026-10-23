import type { StoredMockExamAnswer, StoredMockExamSession } from '@/types/mockExam.types'

const STORAGE_KEY = 'mf02_mock_exam_sessions'
const LEGACY_STORAGE_KEY = 'mf02_mock_exam_session'
const LAST_ACTIVE_KEY = 'mf02_mock_exam_last_active'

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

function readSessionMap(): Record<string, StoredMockExamSession> {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (raw) {
      const parsed: unknown = JSON.parse(raw)
      if (!isRecord(parsed)) return {}
      const next: Record<string, StoredMockExamSession> = {}
      for (const [key, value] of Object.entries(parsed)) {
        if (isStoredSession(value)) next[key] = value
      }
      return next
    }

    // Migrate legacy single-session key once.
    const legacyRaw = sessionStorage.getItem(LEGACY_STORAGE_KEY)
    if (!legacyRaw) return {}
    const legacyParsed: unknown = JSON.parse(legacyRaw)
    if (!isStoredSession(legacyParsed)) {
      sessionStorage.removeItem(LEGACY_STORAGE_KEY)
      return {}
    }
    const migrated = { [legacyParsed.sessionId]: legacyParsed }
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(migrated))
    sessionStorage.setItem(LAST_ACTIVE_KEY, legacyParsed.sessionId)
    sessionStorage.removeItem(LEGACY_STORAGE_KEY)
    return migrated
  } catch {
    return {}
  }
}

function writeSessionMap(map: Record<string, StoredMockExamSession>) {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(map))
}

export function secondsUntilExpiry(expiresAt: string) {
  const expiresAtMs = Date.parse(expiresAt)
  if (Number.isNaN(expiresAtMs)) return 0
  return Math.max(0, Math.floor((expiresAtMs - Date.now()) / 1000))
}

/** Last focused in-progress session (for lobby banner). */
export function readMockExamSession(): StoredMockExamSession | null {
  const map = readSessionMap()
  const lastId = sessionStorage.getItem(LAST_ACTIVE_KEY)
  if (lastId && map[lastId]) return map[lastId]
  const sessions = Object.values(map)
  if (sessions.length === 0) return null
  return sessions.sort(
    (left, right) => Date.parse(right.startedAt) - Date.parse(left.startedAt),
  )[0]
}

export function readMockExamSessionById(sessionId: string): StoredMockExamSession | null {
  if (!sessionId) return null
  return readSessionMap()[sessionId] ?? null
}

export function listMockExamSessions(): StoredMockExamSession[] {
  return Object.values(readSessionMap()).sort(
    (left, right) => Date.parse(right.startedAt) - Date.parse(left.startedAt),
  )
}

export function saveMockExamSession(session: StoredMockExamSession) {
  const map = readSessionMap()
  map[session.sessionId] = session
  writeSessionMap(map)
  sessionStorage.setItem(LAST_ACTIVE_KEY, session.sessionId)
}

export function saveMockExamAnswer(answer: StoredMockExamAnswer, sessionId?: string) {
  const map = readSessionMap()
  const targetId = sessionId ?? sessionStorage.getItem(LAST_ACTIVE_KEY)
  if (!targetId || !map[targetId]) return
  const session = map[targetId]
  // Upsert theo answerId — câu chính và câu phụ cùng questionId không ghi đè nhau.
  const answers = session.answers.filter((item) => item.answerId !== answer.answerId)
  answers.push(answer)
  map[targetId] = { ...session, answers }
  writeSessionMap(map)
  sessionStorage.setItem(LAST_ACTIVE_KEY, targetId)
}

/** Xóa câu chính + mọi câu phụ cùng questionId (ghi âm lại câu chính từ đầu). */
export function removeMockExamAnswer(questionId: string, sessionId?: string) {
  const map = readSessionMap()
  const targetId = sessionId ?? sessionStorage.getItem(LAST_ACTIVE_KEY)
  if (!targetId || !map[targetId]) return
  const session = map[targetId]
  map[targetId] = {
    ...session,
    answers: session.answers.filter((item) => item.questionId !== questionId),
  }
  writeSessionMap(map)
  sessionStorage.setItem(LAST_ACTIVE_KEY, targetId)
}

/** Xóa một answer theo answerId (ghi âm lại câu phụ, không đụng câu chính). */
export function removeMockExamAnswerById(answerId: string, sessionId?: string) {
  if (!answerId) return
  const map = readSessionMap()
  const targetId = sessionId ?? sessionStorage.getItem(LAST_ACTIVE_KEY)
  if (!targetId || !map[targetId]) return
  const session = map[targetId]
  map[targetId] = {
    ...session,
    answers: session.answers.filter((item) => item.answerId !== answerId),
  }
  writeSessionMap(map)
  sessionStorage.setItem(LAST_ACTIVE_KEY, targetId)
}

export function removeMockExamSession(sessionId: string) {
  const map = readSessionMap()
  if (!map[sessionId]) return
  delete map[sessionId]
  writeSessionMap(map)
  if (sessionStorage.getItem(LAST_ACTIVE_KEY) === sessionId) {
    const next = Object.values(map).sort(
      (left, right) => Date.parse(right.startedAt) - Date.parse(left.startedAt),
    )[0]
    if (next) sessionStorage.setItem(LAST_ACTIVE_KEY, next.sessionId)
    else sessionStorage.removeItem(LAST_ACTIVE_KEY)
  }
}

export function clearMockExamSession(sessionId?: string) {
  if (sessionId) {
    removeMockExamSession(sessionId)
    return
  }
  sessionStorage.removeItem(STORAGE_KEY)
  sessionStorage.removeItem(LEGACY_STORAGE_KEY)
  sessionStorage.removeItem(LAST_ACTIVE_KEY)
}
