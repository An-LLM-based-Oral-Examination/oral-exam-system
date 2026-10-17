import apiClient from './api.client'
import type {
  AuditEvidenceItem,
  ExamTicketStatus,
  PublishGradesRequest,
  PublishGradesResponse,
} from '@/types/officialExam.types'

type JsonRecord = Record<string, unknown>

const TICKET_STATUSES = new Set<ExamTicketStatus>([
  'SCHEDULED',
  'IN_PROGRESS',
  'SUBMITTED',
  'AI_GRADED',
  'AUDITED',
  'PUBLISHED',
  'LOCKED',
])

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function isNullableString(value: unknown) {
  return value === null || typeof value === 'string'
}

function isNullableNumber(value: unknown) {
  return value === null || (typeof value === 'number' && Number.isFinite(value))
}

function unwrapPayload(payload: unknown) {
  if (!isRecord(payload)) return payload
  return payload.value ?? payload.Value ?? payload.data ?? payload.Data ?? payload
}

function isAuditEvidenceItem(value: unknown): value is AuditEvidenceItem {
  if (!isRecord(value)) return false
  return (
    typeof value.ticketId === 'string' &&
    typeof value.studentCode === 'string' &&
    typeof value.fullName === 'string' &&
    typeof value.seatNumber === 'number' &&
    typeof value.status === 'string' &&
    TICKET_STATUSES.has(value.status as ExamTicketStatus) &&
    isNullableString(value.audioR2Url) &&
    isNullableString(value.audioHashSha256) &&
    isNullableString(value.transcriptWhisper) &&
    isNullableNumber(value.aiScore) &&
    isNullableNumber(value.confidenceScore) &&
    typeof value.isSuspicious === 'boolean' &&
    isNullableString(value.suspiciousReason) &&
    isNullableString(value.cotTrace) &&
    isNullableNumber(value.finalScore) &&
    typeof value.isLocked === 'boolean'
  )
}

function normalizeAuditEvidence(payload: unknown): AuditEvidenceItem[] {
  const candidate = unwrapPayload(payload)
  if (!Array.isArray(candidate) || !candidate.every(isAuditEvidenceItem)) {
    throw {
      status: 502,
      title: 'Phản hồi API không hợp lệ',
      detail:
        'API hậu kiểm không trả về danh sách bằng chứng hợp lệ. Hãy kiểm tra Backend API và cấu hình kết nối.',
    }
  }
  return candidate
}

function normalizePublishResponse(payload: unknown): PublishGradesResponse {
  const candidate = unwrapPayload(payload)
  if (
    !isRecord(candidate) ||
    typeof candidate.shiftId !== 'string' ||
    typeof candidate.totalPublished !== 'number' ||
    typeof candidate.publishedAt !== 'string' ||
    typeof candidate.message !== 'string'
  ) {
    throw {
      status: 502,
      title: 'Phản hồi API không hợp lệ',
      detail: 'Backend không trả về xác nhận công bố điểm hợp lệ.',
    }
  }
  return candidate as unknown as PublishGradesResponse
}

export interface WorkstationTicketDto {
  ticketId: string
  shiftId: string
  sessionId?: string
  studentId: string
  studentCode: string
  studentName: string
  courseCode?: string
  courseName?: string
  shiftName?: string
  roomLab?: string
  seatNumber: number
  ipAddress: string
  status: ExamTicketStatus
  startTime?: string
  endTime?: string
  durationMinutes?: number
  questionCount?: number
}

function readStr(record: JsonRecord, ...keys: string[]): string | undefined {
  for (const key of keys) {
    const val = record[key] ?? record[`${key.charAt(0).toUpperCase()}${key.slice(1)}`]
    if (typeof val === 'string' && val.trim()) return val
  }
  return undefined
}

function readNum(record: JsonRecord, ...keys: string[]): number | undefined {
  for (const key of keys) {
    const val = record[key] ?? record[`${key.charAt(0).toUpperCase()}${key.slice(1)}`]
    if (typeof val === 'number' && Number.isFinite(val)) return val
  }
  return undefined
}

function normalizeWorkstationTicket(payload: unknown): WorkstationTicketDto {
  const candidate = unwrapPayload(payload)
  if (!isRecord(candidate)) {
    throw {
      status: 502,
      title: 'Phản hồi API không hợp lệ',
      detail: 'Backend không trả về thông tin vé thi máy trạm Kiosk hợp lệ.',
    }
  }

  const ticketId = readStr(candidate, 'ticketId', 'id') ?? ''
  const shiftId = readStr(candidate, 'shiftId') ?? ''
  const studentId = readStr(candidate, 'studentId') ?? ''
  const studentCode = readStr(candidate, 'studentCode') ?? 'SE170001'
  const studentName = readStr(candidate, 'studentName', 'fullName') ?? 'Sinh viên dự thi'
  const seatNumber = readNum(candidate, 'seatNumber') ?? 1
  const ipAddress = readStr(candidate, 'ipAddress') ?? '127.0.0.1'
  const rawStatus = readStr(candidate, 'status') ?? 'SCHEDULED'
  const status: ExamTicketStatus = TICKET_STATUSES.has(rawStatus as ExamTicketStatus)
    ? (rawStatus as ExamTicketStatus)
    : 'SCHEDULED'

  if (!ticketId) {
    throw {
      status: 404,
      title: 'Chưa tìm thấy vé thi tại máy trạm này',
      detail: 'Không có vé thi được gán với địa chỉ IP của máy trạm hiện tại.',
    }
  }

  return {
    ticketId,
    shiftId,
    sessionId: readStr(candidate, 'sessionId'),
    studentId,
    studentCode,
    studentName,
    courseCode: readStr(candidate, 'courseCode', 'subjectCode'),
    courseName: readStr(candidate, 'courseName', 'subjectName'),
    shiftName: readStr(candidate, 'shiftName'),
    roomLab: readStr(candidate, 'roomLab', 'room'),
    seatNumber,
    ipAddress,
    status,
    startTime: readStr(candidate, 'startTime', 'startAt'),
    endTime: readStr(candidate, 'endTime', 'endAt'),
    durationMinutes: readNum(candidate, 'durationMinutes'),
    questionCount: readNum(candidate, 'questionCount'),
  }
}

export const officialExamService = {
  async getAuditEvidence(shiftId: string): Promise<AuditEvidenceItem[]> {
    const response = await apiClient.get<unknown>(`/officialexams/shifts/${shiftId}/audit-evidence`)
    return normalizeAuditEvidence(response.data)
  },

  async publishGrades(
    shiftId: string,
    payload: PublishGradesRequest = {},
  ): Promise<PublishGradesResponse> {
    const response = await apiClient.post<unknown>(
      `/officialexams/shifts/${shiftId}/publish-grades`,
      payload,
    )
    return normalizePublishResponse(response.data)
  },

  async getCurrentWorkstationTicket(): Promise<WorkstationTicketDto> {
    const response = await apiClient.get<unknown>('/official-exams/tickets/current')
    return normalizeWorkstationTicket(response.data)
  },

  async checkInWorkstationTicket(ticketId: string): Promise<WorkstationTicketDto> {
    const response = await apiClient.post<unknown>(`/official-exams/tickets/${ticketId}/check-in`)
    return normalizeWorkstationTicket(response.data)
  },

  async createSession(payload: {
    courseId: string
    examStructureId: string
    title: string
    examDate: string
    hasFollowUp: boolean
    maxFollowUpQuestions: number
    examInputMode: string
    transcriptBufferSeconds: number
  }): Promise<Record<string, unknown>> {
    const response = await apiClient.post<unknown>('/officialexams/sessions', payload)
    const candidate = unwrapPayload(response.data)
    return isRecord(candidate) ? candidate : { data: candidate }
  },

  async openShift(payload: {
    sessionId: string
    shiftName: string
    roomLab: string
    startTime: string
    endTime: string
    proctorUserId: string
  }): Promise<Record<string, unknown>> {
    const response = await apiClient.post<unknown>('/officialexams/shifts', payload)
    const candidate = unwrapPayload(response.data)
    return isRecord(candidate) ? candidate : { data: candidate }
  },

  async assignTicket(payload: {
    shiftId: string
    studentId: string
    seatNumber: number
    ipAddress: string
  }): Promise<Record<string, unknown>> {
    const response = await apiClient.post<unknown>('/officialexams/tickets', payload)
    const candidate = unwrapPayload(response.data)
    return isRecord(candidate) ? candidate : { data: candidate }
  },

  getWorkstationEnvMode(): WorkstationEnvironmentMode {
    if (typeof window === 'undefined') return 'auto'
    const stored = window.sessionStorage.getItem(WORKSTATION_ENV_STORAGE_KEY)
    if (stored === 'personal' || stored === 'lab-kiosk' || stored === 'auto') {
      return stored
    }
    return 'auto'
  },

  setWorkstationEnvMode(mode: WorkstationEnvironmentMode): void {
    if (typeof window === 'undefined') return
    window.sessionStorage.setItem(WORKSTATION_ENV_STORAGE_KEY, mode)
    window.dispatchEvent(new CustomEvent(WORKSTATION_MODE_CHANGE_EVENT, { detail: mode }))
  },
}

export type WorkstationEnvironmentMode = 'auto' | 'personal' | 'lab-kiosk'
export const WORKSTATION_ENV_STORAGE_KEY = 'oral_exam_workstation_env_mode'
export const WORKSTATION_MODE_CHANGE_EVENT = 'oral-exam-workstation-mode-changed'

