import type { InClassShift, StudentExamQuestion } from '@/types/inClassShift.types'
import { safeJsonParse } from '@/utils/json.utils'

const STORAGE_KEY = 'inClassExam.session.v1'
const ACTIVE_SHIFT_ID_KEY = 'inClassExam.activeShiftId.v1'
const COMPLETED_KEY = 'inClassExam.completedShiftIds.v1'
const DRAFT_KEY = 'inClassExam.drafts.v1'

export type InClassExamSessionPayload = {
  shift: InClassShift
  questions: StudentExamQuestion[]
  /** Epoch ms — nguồn dự phòng nếu thiếu examEndsAt. */
  startedAt: number
  /** Epoch ms hết giờ làm bài cá nhân (server JoinedAt + duration). */
  examEndsAt: number
  joinedAt?: string
}

export type InClassExamDraftPayload = {
  drafts: Record<string, string>
  submittedKeys?: Record<string, boolean>
  questionIndex: number
  updatedAt: number
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}

export function saveInClassExamSession(payload: InClassExamSessionPayload) {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(payload))
  if (payload.shift?.id?.trim()) {
    sessionStorage.setItem(ACTIVE_SHIFT_ID_KEY, payload.shift.id.trim())
  }
}

export function loadActiveInClassShiftId(): string | null {
  try {
    const fromSession = loadInClassExamSession()?.shift?.id?.trim()
    if (fromSession) return fromSession
    const raw = sessionStorage.getItem(ACTIVE_SHIFT_ID_KEY)?.trim()
    return raw || null
  } catch {
    return null
  }
}

export function loadInClassExamSession(): InClassExamSessionPayload | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const parsed = safeJsonParse<unknown>(raw, null)
    if (!isRecord(parsed) || !isRecord(parsed.shift) || !Array.isArray(parsed.questions)) return null
    const startedAt = typeof parsed.startedAt === 'number' ? parsed.startedAt : Date.now()
    const examEndsAt =
      typeof parsed.examEndsAt === 'number' && Number.isFinite(parsed.examEndsAt)
        ? parsed.examEndsAt
        : startedAt + 20 * 60 * 1000
    return {
      shift: parsed.shift as unknown as InClassShift,
      questions: parsed.questions as unknown as StudentExamQuestion[],
      startedAt,
      examEndsAt,
      joinedAt: typeof parsed.joinedAt === 'string' ? parsed.joinedAt : undefined,
    }
  } catch {
    return null
  }
}

export function clearInClassExamSession() {
  sessionStorage.removeItem(STORAGE_KEY)
  sessionStorage.removeItem(ACTIVE_SHIFT_ID_KEY)
}

function readDraftMap(): Record<string, InClassExamDraftPayload> {
  try {
    const raw = localStorage.getItem(DRAFT_KEY)
    if (!raw) return {}
    const parsed = safeJsonParse<unknown>(raw, null)
    if (!isRecord(parsed)) return {}
    const result: Record<string, InClassExamDraftPayload> = {}
    for (const [shiftId, value] of Object.entries(parsed)) {
      if (!isRecord(value) || !isRecord(value.drafts)) continue
      const drafts: Record<string, string> = {}
      for (const [qid, text] of Object.entries(value.drafts)) {
        if (typeof text === 'string') drafts[qid] = text
      }
      const submittedKeys: Record<string, boolean> = {}
      if (isRecord(value.submittedKeys)) {
        for (const [k, flag] of Object.entries(value.submittedKeys)) {
          if (flag === true) submittedKeys[k] = true
        }
      }
      result[shiftId] = {
        drafts,
        submittedKeys,
        questionIndex: typeof value.questionIndex === 'number' ? value.questionIndex : 0,
        updatedAt: typeof value.updatedAt === 'number' ? value.updatedAt : Date.now(),
      }
    }
    return result
  } catch {
    return {}
  }
}

export function saveInClassExamDrafts(
  shiftId: string,
  payload: {
    drafts: Record<string, string>
    submittedKeys?: Record<string, boolean>
    questionIndex: number
  },
) {
  if (!shiftId.trim()) return
  const all = readDraftMap()
  const prevSubmitted = all[shiftId]?.submittedKeys ?? {}
  all[shiftId] = {
    drafts: payload.drafts,
    submittedKeys: payload.submittedKeys
      ? { ...prevSubmitted, ...payload.submittedKeys }
      : prevSubmitted,
    questionIndex: payload.questionIndex,
    updatedAt: Date.now(),
  }
  localStorage.setItem(DRAFT_KEY, JSON.stringify(all))
}

export function loadInClassExamDrafts(shiftId: string): InClassExamDraftPayload | null {
  if (!shiftId.trim()) return null
  return readDraftMap()[shiftId] ?? null
}

export function clearInClassExamDrafts(shiftId: string) {
  if (!shiftId.trim()) return
  const all = readDraftMap()
  if (!(shiftId in all)) return
  delete all[shiftId]
  localStorage.setItem(DRAFT_KEY, JSON.stringify(all))
}

function readCompletedShiftIds(): Set<string> {
  try {
    const raw = localStorage.getItem(COMPLETED_KEY)
    if (!raw) return new Set()
    const parsed = safeJsonParse<unknown>(raw, null)
    if (!Array.isArray(parsed)) return new Set()
    return new Set(parsed.filter((item): item is string => typeof item === 'string' && item.trim().length > 0))
  } catch {
    return new Set()
  }
}

export function markInClassShiftCompleted(shiftId: string) {
  if (!shiftId.trim()) return
  const next = readCompletedShiftIds()
  next.add(shiftId)
  localStorage.setItem(COMPLETED_KEY, JSON.stringify([...next]))
  clearInClassExamDrafts(shiftId)
}

export function clearCompletedInClassShift(shiftId: string) {
  if (!shiftId.trim()) return
  const next = readCompletedShiftIds()
  if (!next.delete(shiftId)) return
  localStorage.setItem(COMPLETED_KEY, JSON.stringify([...next]))
}

function isServerSubmitted(status?: string | null) {
  const current = (status ?? '').toUpperCase()
  return (
    current === 'SUBMITTED' ||
    current === 'GRADING' ||
    current === 'AI_GRADED' ||
    current === 'GRADED' ||
    current === 'AUDITED' ||
    current === 'FINALIZED' ||
    current === 'COMPLETED'
  )
}

/** Chỉ hiện “đã nộp” local khi máy chủ cũng đã chốt, hoặc đang chờ đồng bộ sau lần nộp vừa xong. */
export function applyCompletedInClassShifts(shifts: InClassShift[]): InClassShift[] {
  const completed = readCompletedShiftIds()
  if (completed.size === 0) return shifts
  return shifts.map((shift) => {
    if (!completed.has(shift.id)) return shift
    if (isServerSubmitted(shift.myParticipationStatus)) return shift
    return { ...shift, myParticipationStatus: 'SUBMITTED' }
  })
}

/** Gọi finish lại các ca local đã đánh dấu nộp nhưng máy chủ vẫn JOINED/IN_PROGRESS. */
export async function reconcileCompletedInClassShifts(
  shifts: InClassShift[],
  finish: (shiftId: string) => Promise<void>,
): Promise<void> {
  const completed = readCompletedShiftIds()
  if (completed.size === 0) return

  await Promise.all(
    shifts.map(async (shift) => {
      if (!completed.has(shift.id) || isServerSubmitted(shift.myParticipationStatus)) return
      try {
        await finish(shift.id)
      } catch {
        clearCompletedInClassShift(shift.id)
      }
    }),
  )
}
