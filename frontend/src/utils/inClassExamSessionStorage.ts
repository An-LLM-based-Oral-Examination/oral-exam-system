import type { InClassShift, StudentExamQuestion } from '@/types/inClassShift.types'

const STORAGE_KEY = 'inClassExam.session.v1'
const COMPLETED_KEY = 'inClassExam.completedShiftIds.v1'

export type InClassExamSessionPayload = {
  shift: InClassShift
  questions: StudentExamQuestion[]
  startedAt: number
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}

export function saveInClassExamSession(payload: InClassExamSessionPayload) {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(payload))
}

export function loadInClassExamSession(): InClassExamSessionPayload | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const parsed: unknown = JSON.parse(raw)
    if (!isRecord(parsed) || !isRecord(parsed.shift) || !Array.isArray(parsed.questions)) return null
    const startedAt = typeof parsed.startedAt === 'number' ? parsed.startedAt : Date.now()
    return {
      shift: parsed.shift as unknown as InClassShift,
      questions: parsed.questions as unknown as StudentExamQuestion[],
      startedAt,
    }
  } catch {
    return null
  }
}

export function clearInClassExamSession() {
  sessionStorage.removeItem(STORAGE_KEY)
}

function readCompletedShiftIds(): Set<string> {
  try {
    const raw = localStorage.getItem(COMPLETED_KEY)
    if (!raw) return new Set()
    const parsed: unknown = JSON.parse(raw)
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
}

export function applyCompletedInClassShifts(shifts: InClassShift[]): InClassShift[] {
  const completed = readCompletedShiftIds()
  if (completed.size === 0) return shifts
  return shifts.map((shift) => {
    if (!completed.has(shift.id)) return shift
    const current = (shift.myParticipationStatus ?? '').toUpperCase()
    if (current === 'SUBMITTED' || current === 'GRADED' || current === 'FINALIZED' || current === 'COMPLETED') {
      return shift
    }
    return { ...shift, myParticipationStatus: 'SUBMITTED' }
  })
}
