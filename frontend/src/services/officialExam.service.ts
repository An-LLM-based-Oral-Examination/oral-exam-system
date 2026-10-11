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
}
