import apiClient from './api.client'
import {
  asArray,
  field,
  readBoolean,
  readNullableNumber,
  readNullableString,
  readNumber,
  readString,
  requiredString,
  requireRecord,
  unwrapPayload,
} from './mf04.response'
import {
  normalizeAntiCheatLog,
  normalizeAudioSeal,
  normalizeAuditEvidence,
  normalizeAuthorization,
  normalizeLock,
  normalizeOverride,
  normalizeSubmissionStatus,
  normalizeSubmit,
  normalizeTicket,
  normalizeWorkstationTicket,
  officialExamService,
} from './officialExam.service'
import type {
  AudioSealResponse,
  AudioUploadAuthorization,
  OfficialExamAnswerSubmissionRequest,
  OfficialExamSubmissionStatusResponse,
  SealOfficialExamAudioRequest,
  SubmitOfficialExamResponse,
} from '@/types/officialExam.types'
import type {
  AuditEvidenceItem,
  AuthorizeQuestionAudioUploadRequest,
  KioskCheckInRequest,
  KioskCheckInResponse,
  KioskRequestHeaders,
  KioskTicketState,
  KioskWorkstationTicket,
  LockExamTicketResponse,
  LogAntiCheatEventRequest,
  LogAntiCheatEventResponse,
  NextOfficialFollowUpResponse,
  OverrideExamScoreRequest,
  OverrideExamScoreResponse,
  SubmitOfficialQuestionAnswerRequest,
  SubmitOfficialQuestionAnswerResponse,
  WorkstationFollowUpItem,
} from '@/types/kiosk.types'

const DEFAULT_MAX_VIOLATIONS = 3
const DEFAULT_LOCK_THRESHOLD = 4

function buildKioskHeaders(headers?: KioskRequestHeaders): Record<string, string> | undefined {
  if (!headers) return undefined
  const result: Record<string, string> = {}
  if (headers.deviceId?.trim()) result['X-Device-Id'] = headers.deviceId.trim()
  if (headers.kioskSessionToken?.trim()) {
    result['X-Kiosk-Session-Token'] = headers.kioskSessionToken.trim()
  }
  if (headers.kioskVersion?.trim()) result['X-Kiosk-Version'] = headers.kioskVersion.trim()
  return Object.keys(result).length > 0 ? result : undefined
}

function generateClientEventId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  return `evt-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`
}

function normalizeFollowUpItem(value: unknown): WorkstationFollowUpItem {
  const record = requireRecord(value, 'Dữ liệu câu hỏi phụ Kiosk không hợp lệ.')
  return {
    submissionId: requiredString(record, 'submissionId', 'Câu hỏi phụ thiếu mã bài nộp.', 'id'),
    parentSubmissionId: readNullableString(record, 'parentSubmissionId'),
    examQuestionId: requiredString(
      record,
      'examQuestionId',
      'Câu hỏi phụ thiếu mã câu hỏi.',
      'questionId',
    ),
    followUpIndex: readNumber(record, 'followUpIndex') ?? 0,
    followUpPrompt: readNullableString(record, 'followUpPrompt'),
    generatedFollowUpPrompt: readNullableString(record, 'generatedFollowUpPrompt'),
    transcriptWhisper: readNullableString(record, 'transcriptWhisper', 'transcript'),
    audioObjectKey: readNullableString(record, 'audioObjectKey'),
    audioR2Url: readNullableString(record, 'audioR2Url', 'audioUrl'),
    audioHashSha256: readNullableString(record, 'audioHashSha256', 'audioHash'),
    aiScore: readNullableNumber(record, 'aiScore'),
    finalScore: readNullableNumber(record, 'finalScore'),
  }
}

function normalizeFollowUpList(value: unknown): WorkstationFollowUpItem[] {
  return Array.isArray(value) ? value.map(normalizeFollowUpItem) : []
}

function computeRemainingStrikes(
  record: Record<string, unknown>,
  violationCount: number,
  lockThreshold: number,
): number {
  const explicit = readNumber(record, 'remainingStrikes')
  if (explicit !== null) return Math.max(0, explicit)
  return Math.max(0, lockThreshold - violationCount)
}

function normalizeKioskWorkstationTicket(payload: unknown): KioskWorkstationTicket {
  const base = normalizeWorkstationTicket(payload)
  const record = requireRecord(unwrapPayload(payload), 'Vé thi Kiosk không hợp lệ.')
  const violationCount = readNumber(record, 'violationCount') ?? 0
  const maxViolations = readNumber(record, 'maxViolations') ?? DEFAULT_MAX_VIOLATIONS
  const lockThreshold =
    readNumber(record, 'lockThreshold') ?? Math.max(maxViolations + 1, DEFAULT_LOCK_THRESHOLD)
  const isKioskLocked =
    readBoolean(record, 'isKioskLocked', 'isLocked') ??
    (base.status.toUpperCase() === 'LOCKED' || violationCount >= lockThreshold)
  const rawAttemptType = readString(record, 'examAttemptType')
  return {
    ...base,
    examAttemptType: rawAttemptType?.toUpperCase() === 'RE' ? 'RE' : 'FE',
    isExamPublished:
      readBoolean(record, 'isExamPublished') ??
      (base.questions.length > 0 || base.shiftStatus.toLowerCase() === 'in_progress'),
    isKioskLocked,
    deviceId: readNullableString(record, 'deviceId'),
    deviceLockedAt: readNullableString(record, 'deviceLockedAt'),
    deviceUnlockCount: readNumber(record, 'deviceUnlockCount') ?? 0,
    violationCount,
    maxViolations,
    lockThreshold,
    remainingStrikes: computeRemainingStrikes(record, violationCount, lockThreshold),
    serverNowUtc: readString(record, 'serverNowUtc') ?? new Date().toISOString(),
    followUps: normalizeFollowUpList(field(record, 'followUps')),
  }
}

function normalizeKioskTicketState(payload: unknown): KioskTicketState {
  const record = requireRecord(unwrapPayload(payload), 'Trạng thái vé thi Kiosk không hợp lệ.')
  const status = readString(record, 'status') ?? 'SCHEDULED'
  const violationCount = readNumber(record, 'violationCount') ?? 0
  const maxViolations = readNumber(record, 'maxViolations') ?? DEFAULT_MAX_VIOLATIONS
  const lockThreshold =
    readNumber(record, 'lockThreshold') ?? Math.max(maxViolations + 1, DEFAULT_LOCK_THRESHOLD)
  return {
    ticketId: requiredString(record, 'ticketId', 'Trạng thái vé thiếu mã vé.', 'id'),
    shiftId: requiredString(record, 'shiftId', 'Trạng thái vé thiếu mã ca thi.'),
    status,
    isExamPublished: readBoolean(record, 'isExamPublished') ?? false,
    isKioskLocked:
      readBoolean(record, 'isKioskLocked', 'isLocked') ??
      (status.toUpperCase() === 'LOCKED' || violationCount >= lockThreshold),
    violationCount,
    maxViolations,
    lockThreshold,
    remainingStrikes: computeRemainingStrikes(record, violationCount, lockThreshold),
    serverNowUtc: readString(record, 'serverNowUtc') ?? new Date().toISOString(),
    deviceId: readNullableString(record, 'deviceId'),
    deviceLockedAt: readNullableString(record, 'deviceLockedAt'),
    deviceUnlockCount: readNumber(record, 'deviceUnlockCount') ?? 0,
    followUps: normalizeFollowUpList(field(record, 'followUps')),
  }
}

function normalizeKioskCheckIn(payload: unknown): KioskCheckInResponse {
  const unwrapped = unwrapPayload(payload)
  const record = requireRecord(unwrapped, 'Phản hồi check-in Kiosk không hợp lệ.')
  const base = normalizeTicket(record)
  return {
    ...base,
    deviceId: readNullableString(record, 'deviceId'),
    deviceLockedAt: readNullableString(record, 'deviceLockedAt'),
    deviceUnlockCount: readNumber(record, 'deviceUnlockCount') ?? 0,
    activeSessionToken: readNullableString(
      record,
      'activeSessionToken',
      'sessionToken',
      'kioskSessionToken',
    ),
  }
}

function normalizeLogAntiCheatResponse(payload: unknown): LogAntiCheatEventResponse {
  const unwrapped = unwrapPayload(payload)
  const record = requireRecord(unwrapped, 'Phản hồi ghi nhận chống gian lận không hợp lệ.')
  const base = normalizeAntiCheatLog(record)
  const maxViolations = readNumber(record, 'maxViolations') ?? DEFAULT_MAX_VIOLATIONS
  const lockThreshold =
    readNumber(record, 'lockThreshold') ?? Math.max(maxViolations + 1, DEFAULT_LOCK_THRESHOLD)
  const isLocked =
    readBoolean(record, 'isLocked', 'isKioskLocked') ?? base.violationCount >= lockThreshold
  return {
    ...base,
    isLocked,
    maxViolations,
    lockThreshold,
    remainingStrikes: computeRemainingStrikes(record, base.violationCount, lockThreshold),
  }
}

function normalizeQuestionAnswerResponse(payload: unknown): SubmitOfficialQuestionAnswerResponse {
  const record = requireRecord(
    unwrapPayload(payload),
    'Phản hồi lưu câu trả lời từng lượt không hợp lệ.',
  )
  return {
    submissionId: requiredString(record, 'submissionId', 'Thiếu mã bài nộp từng câu.', 'id'),
    ticketId: requiredString(record, 'ticketId', 'Thiếu mã vé thi.'),
    questionId: requiredString(record, 'questionId', 'Thiếu mã câu hỏi.', 'examQuestionId'),
    followUpIndex: readNumber(record, 'followUpIndex') ?? 0,
    audioHashSha256: readString(record, 'audioHashSha256') ?? '',
    status: readString(record, 'status') ?? 'RECORDED',
    canRequestFollowUp: readBoolean(record, 'canRequestFollowUp') ?? false,
  }
}

function normalizeNextFollowUpResponse(payload: unknown): NextOfficialFollowUpResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi sinh câu hỏi phụ không hợp lệ.')
  return {
    hasMoreFollowUp: readBoolean(record, 'hasMoreFollowUp') ?? false,
    followUpIndex: readNumber(record, 'followUpIndex') ?? 0,
    maxFollowUpQuestions: readNumber(record, 'maxFollowUpQuestions') ?? 2,
    parentSubmissionId: readString(record, 'parentSubmissionId') ?? '',
    questionId: readString(record, 'questionId', 'examQuestionId') ?? '',
    followUpPrompt: readNullableString(record, 'followUpPrompt'),
  }
}

export const kioskExamService = {
  async getCurrentTicket(headers?: KioskRequestHeaders): Promise<KioskWorkstationTicket> {
    const response = await apiClient.get<unknown>('/official-exams/tickets/current', {
      headers: buildKioskHeaders(headers),
    })
    return normalizeKioskWorkstationTicket(response.data)
  },

  async getTicketState(
    ticketId: string,
    headers?: KioskRequestHeaders,
  ): Promise<KioskTicketState> {
    const response = await apiClient.get<unknown>(`/official-exams/tickets/${ticketId}`, {
      headers: buildKioskHeaders(headers),
    })
    return normalizeKioskTicketState(response.data)
  },

  async checkInTicket(
    ticketId: string,
    payload: KioskCheckInRequest = {},
    headers?: KioskRequestHeaders,
  ): Promise<KioskCheckInResponse> {
    const mergedHeaders = buildKioskHeaders({
      ...headers,
      deviceId: headers?.deviceId ?? payload.deviceId,
      kioskVersion: headers?.kioskVersion ?? payload.kioskVersion,
    })
    const response = await apiClient.post<unknown>(
      `/official-exams/tickets/${ticketId}/check-in`,
      payload,
      { headers: mergedHeaders },
    )
    return normalizeKioskCheckIn(response.data)
  },

  async logAntiCheat(
    payload: LogAntiCheatEventRequest,
    headers?: KioskRequestHeaders,
  ): Promise<LogAntiCheatEventResponse> {
    const body = {
      ...payload,
      clientEventId: payload.clientEventId?.trim() || generateClientEventId(),
    }
    const response = await apiClient.post<unknown>('/official-exams/anti-cheat/log', body, {
      headers: buildKioskHeaders(headers),
    })
    return normalizeLogAntiCheatResponse(response.data)
  },

  async createAudioUploadAuthorization(
    ticketId: string,
    expectedSha256Base64?: string | null,
    headers?: KioskRequestHeaders,
  ): Promise<AudioUploadAuthorization> {
    const body = expectedSha256Base64 ? { expectedSha256Base64 } : undefined
    const response = await apiClient.post<unknown>(
      `/official-exams/tickets/${ticketId}/audio-upload-authorizations`,
      body,
      { headers: buildKioskHeaders(headers) },
    )
    return normalizeAuthorization(response.data)
  },

  async uploadAudio(authorization: AudioUploadAuthorization, audio: Blob): Promise<void> {
    await officialExamService.uploadAudio(authorization, audio)
  },

  async sealAudio(
    ticketId: string,
    payload: SealOfficialExamAudioRequest,
    headers?: KioskRequestHeaders,
  ): Promise<AudioSealResponse> {
    const response = await apiClient.put<unknown>(
      `/official-exams/tickets/${ticketId}/audio`,
      payload,
      { headers: buildKioskHeaders(headers) },
    )
    return normalizeAudioSeal(response.data)
  },

  async authorizeQuestionAudioUpload(
    ticketId: string,
    questionId: string,
    payload: AuthorizeQuestionAudioUploadRequest,
    headers?: KioskRequestHeaders,
  ): Promise<AudioUploadAuthorization> {
    const response = await apiClient.post<unknown>(
      `/official-exams/tickets/${ticketId}/questions/${questionId}/audio-upload-authorizations`,
      payload,
      { headers: buildKioskHeaders(headers) },
    )
    return normalizeAuthorization(response.data)
  },

  async submitQuestionAnswer(
    ticketId: string,
    questionId: string,
    payload: SubmitOfficialQuestionAnswerRequest,
    headers?: KioskRequestHeaders,
  ): Promise<SubmitOfficialQuestionAnswerResponse> {
    const response = await apiClient.put<unknown>(
      `/official-exams/tickets/${ticketId}/questions/${questionId}/answers`,
      payload,
      { headers: buildKioskHeaders(headers) },
    )
    return normalizeQuestionAnswerResponse(response.data)
  },

  async requestNextFollowUp(
    ticketId: string,
    submissionId: string,
    headers?: KioskRequestHeaders,
  ): Promise<NextOfficialFollowUpResponse> {
    const response = await apiClient.post<unknown>(
      `/official-exams/tickets/${ticketId}/submissions/${submissionId}/next-followup`,
      undefined,
      { headers: buildKioskHeaders(headers), timeout: 65_000 },
    )
    return normalizeNextFollowUpResponse(response.data)
  },

  async submitExam(
    ticketId: string,
    answers: OfficialExamAnswerSubmissionRequest[],
    headers?: KioskRequestHeaders,
    options?: { finalizePerTurn?: boolean },
  ): Promise<SubmitOfficialExamResponse> {
    const payload: {
      answers: OfficialExamAnswerSubmissionRequest[]
      finalizePerTurn?: boolean
    } = { answers }
    if (options?.finalizePerTurn !== undefined) {
      payload.finalizePerTurn = Boolean(options.finalizePerTurn)
    }
    const response = await apiClient.put<unknown>(
      `/official-exams/tickets/${ticketId}/submission`,
      payload,
      { headers: buildKioskHeaders(headers) },
    )
    return normalizeSubmit(response.data)
  },

  async getSubmissionStatus(
    ticketId: string,
    headers?: KioskRequestHeaders,
  ): Promise<OfficialExamSubmissionStatusResponse> {
    const response = await apiClient.get<unknown>(
      `/official-exams/tickets/${ticketId}/submission`,
      { headers: buildKioskHeaders(headers) },
    )
    return normalizeSubmissionStatus(response.data)
  },

  async getAuditEvidence(shiftId: string): Promise<AuditEvidenceItem[]> {
    const response = await apiClient.get<unknown>(
      `/official-exams/shifts/${shiftId}/audit-evidence`,
    )
    return asArray(response.data, 'API hậu kiểm không trả về danh sách bằng chứng.').map(
      normalizeAuditEvidence,
    )
  },

  async overrideScore(
    ticketId: string,
    payload: OverrideExamScoreRequest,
  ): Promise<OverrideExamScoreResponse> {
    const response = await apiClient.put<unknown>(
      `/official-exams/tickets/${ticketId}/override-score`,
      {
        ...payload,
        ticketId,
      },
    )
    return normalizeOverride(response.data)
  },

  async lockTicket(ticketId: string): Promise<LockExamTicketResponse> {
    const response = await apiClient.post<unknown>(`/official-exams/tickets/${ticketId}/lock`, {
      ticketId,
    })
    return normalizeLock(response.data)
  },
}

export type KioskExamService = typeof kioskExamService
