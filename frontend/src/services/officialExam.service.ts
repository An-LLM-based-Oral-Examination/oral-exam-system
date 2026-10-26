import axios, { type AxiosResponse } from 'axios'
import apiClient from './api.client'
import {
  asArray,
  field,
  invalidResponse,
  isRecord,
  normalizeFileName,
  readBoolean,
  readNullableNumber,
  readNullableString,
  readNumber,
  readString,
  requiredBoolean,
  requiredDateString,
  requiredNumber,
  requiredString,
  requireRecord,
  unwrapPayload,
} from './mf04.response'
import type {
  AcknowledgeOfficialGradeResponse,
  AssignExamTicketRequest,
  AudioSealResponse,
  AudioUploadAuthorization,
  AuditEvidenceItem,
  CreateExamShiftRequest,
  CreateExamStructureRequest,
  CreateOfficialExamSessionRequest,
  ExamStructure,
  ExamShift,
  ExamTicket,
  ExamTicketStatus,
  GetExamStructuresParams,
  GetExamShiftsParams,
  GetOfficialExamSessionsParams,
  LockExamTicketResponse,
  OfficialExamAnswerSubmissionRequest,
  OfficialExamExportFile,
  OfficialExamSession,
  OfficialExamSubmissionStatusResponse,
  OverrideExamScoreRequest,
  OverrideExamScoreResponse,
  PublishGradesRequest,
  PublishGradesResponse,
  SealOfficialExamAudioRequest,
  StudentOfficialExamCriterionScore,
  StudentOfficialExamGrade,
  StudentOfficialExamQuestionScorecard,
  StudentWorkstationTicket,
  SubmitOfficialExamRequest,
  SubmitOfficialExamResponse,
  UpdateExamStructureRequest,
  WorkstationExamQuestion,
} from '@/types/officialExam.types'

function normalizeStatus(value: unknown): ExamTicketStatus | string {
  return typeof value === 'string' && value.trim() ? value : 'SCHEDULED'
}

function normalizeAuditEvidence(value: unknown): AuditEvidenceItem {
  const record = requireRecord(value, 'Bằng chứng hậu kiểm không hợp lệ.')
  const ticketId = requiredString(record, 'ticketId', 'Bằng chứng hậu kiểm thiếu mã vé thi.')
  return {
    ticketId,
    studentCode: requiredString(record, 'studentCode', 'Bằng chứng hậu kiểm thiếu mã sinh viên.'),
    fullName: requiredString(record, 'fullName', 'Bằng chứng hậu kiểm thiếu họ tên sinh viên.'),
    seatNumber: requiredNumber(record, 'seatNumber', 'Bằng chứng hậu kiểm thiếu số ghế.'),
    status: normalizeStatus(field(record, 'status')),
    audioR2Url: readNullableString(record, 'audioR2Url', 'audioUrl'),
    audioHashSha256: readNullableString(record, 'audioHashSha256', 'audioHash'),
    transcriptWhisper: readNullableString(record, 'transcriptWhisper', 'transcript'),
    aiScore: readNullableNumber(record, 'aiScore'),
    confidenceScore: readNullableNumber(record, 'confidenceScore'),
    isSuspicious: readBoolean(record, 'isSuspicious') ?? false,
    suspiciousReason: readNullableString(record, 'suspiciousReason'),
    cotTrace: readNullableString(record, 'cotTrace', 'aiChainOfThought'),
    finalScore: readNullableNumber(record, 'finalScore'),
    isLocked: readBoolean(record, 'isLocked') ?? false,
    questionId: readNullableString(record, 'questionId'),
    questionTitle: readNullableString(record, 'questionTitle'),
    questionContent: readNullableString(record, 'questionContent'),
    questionImageUrl: readNullableString(record, 'questionImageUrl'),
    questionImageDescription: readNullableString(record, 'questionImageDescription'),
  }
}

function normalizePublishResponse(payload: unknown): PublishGradesResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi công bố điểm không hợp lệ.')
  return {
    shiftId: requiredString(record, 'shiftId', 'Phản hồi công bố điểm thiếu mã ca thi.'),
    totalPublished: requiredNumber(record, 'totalPublished', 'Phản hồi công bố điểm thiếu tổng số sinh viên.'),
    publishedAt: requiredDateString(record, 'publishedAt', 'Phản hồi công bố điểm thiếu thời điểm công bố.'),
    message: requiredString(record, 'message', 'Phản hồi công bố điểm thiếu thông báo.'),
  }
}

function normalizeSession(value: unknown): OfficialExamSession {
  const record = requireRecord(value, 'Kỳ thi thật không hợp lệ.')
  return {
    id: requiredString(record, 'id', 'Kỳ thi thật thiếu mã kỳ thi.'),
    courseId: requiredString(record, 'courseId', 'Kỳ thi thật thiếu mã môn học.'),
    examStructureId: requiredString(record, 'examStructureId', 'Kỳ thi thật thiếu cấu trúc đề.'),
    title: requiredString(record, 'title', 'Kỳ thi thật thiếu tiêu đề.'),
    examDate: requiredString(record, 'examDate', 'Kỳ thi thật thiếu ngày thi.'),
    hasFollowUp: requiredBoolean(record, 'hasFollowUp', 'Kỳ thi thật thiếu cấu hình follow-up.'),
    maxFollowUpQuestions: requiredNumber(record, 'maxFollowUpQuestions', 'Kỳ thi thật thiếu số câu follow-up.'),
    examInputMode: requiredString(record, 'examInputMode', 'Kỳ thi thật thiếu chế độ nhập bài.'),
    transcriptBufferSeconds: requiredNumber(record, 'transcriptBufferSeconds', 'Kỳ thi thật thiếu thời gian đệm transcript.'),
    status: requiredString(record, 'status', 'Kỳ thi thật thiếu trạng thái.'),
  }
}

function normalizeShift(value: unknown): ExamShift {
  const record = requireRecord(value, 'Ca thi thật không hợp lệ.')
  return {
    id: requiredString(record, 'id', 'Ca thi thật thiếu mã ca thi.'),
    sessionId: requiredString(record, 'sessionId', 'Ca thi thật thiếu mã kỳ thi.'),
    shiftName: requiredString(record, 'shiftName', 'Ca thi thật thiếu tên ca.'),
    roomLab: requiredString(record, 'roomLab', 'Ca thi thật thiếu phòng thi.'),
    startTime: requiredDateString(record, 'startTime', 'Ca thi thật thiếu thời gian bắt đầu.'),
    endTime: requiredDateString(record, 'endTime', 'Ca thi thật thiếu thời gian kết thúc.'),
    proctorUserId: readNullableString(record, 'proctorUserId'),
    status: requiredString(record, 'status', 'Ca thi thật thiếu trạng thái.'),
  }
}

function normalizeExamStructure(value: unknown): ExamStructure {
  const record = requireRecord(value, 'Cấu trúc đề thi không hợp lệ.')
  return {
    id: requiredString(record, 'id', 'Cấu trúc đề thi thiếu mã.'),
    courseId: requiredString(record, 'courseId', 'Cấu trúc đề thi thiếu mã môn.'),
    courseCode: requiredString(record, 'courseCode', 'Cấu trúc đề thi thiếu mã môn hiển thị.'),
    createdBy: requiredString(record, 'createdBy', 'Cấu trúc đề thi thiếu người tạo.'),
    name: requiredString(record, 'name', 'Cấu trúc đề thi thiếu tên.'),
    totalQuestions: requiredNumber(record, 'totalQuestions', 'Cấu trúc đề thi thiếu số câu.'),
    durationMinutes: requiredNumber(record, 'durationMinutes', 'Cấu trúc đề thi thiếu thời lượng.'),
    bloomDistribution: requiredString(record, 'bloomDistribution', 'Cấu trúc đề thi thiếu phân bổ Bloom.'),
    isActive: requiredBoolean(record, 'isActive', 'Cấu trúc đề thi thiếu trạng thái.'),
    createdAt: requiredDateString(record, 'createdAt', 'Cấu trúc đề thi thiếu thời điểm tạo.'),
  }
}

function normalizeTicket(value: unknown): ExamTicket {
  const record = requireRecord(value, 'Vé thi thật không hợp lệ.')
  return {
    id: requiredString(record, 'id', 'Vé thi thật thiếu mã vé.'),
    shiftId: requiredString(record, 'shiftId', 'Vé thi thật thiếu mã ca thi.'),
    studentId: requiredString(record, 'studentId', 'Vé thi thật thiếu mã sinh viên.'),
    seatNumber: requiredNumber(record, 'seatNumber', 'Vé thi thật thiếu số ghế.'),
    ipAddress: readNullableString(record, 'ipAddress'),
    status: normalizeStatus(field(record, 'status')),
    checkedInAt: requiredDateString(record, 'checkedInAt', 'Vé thi thật thiếu thời điểm điểm danh.'),
    examSetId: readNullableString(record, 'examSetId'),
  }
}

function normalizeWorkstationQuestion(value: unknown): WorkstationExamQuestion {
  const record = requireRecord(value, 'Câu hỏi thi thật không hợp lệ.')
  return {
    questionId: requiredString(record, 'questionId', 'Câu hỏi thi thật thiếu mã câu hỏi.'),
    orderIndex: requiredNumber(record, 'orderIndex', 'Câu hỏi thi thật thiếu thứ tự.'),
    title: requiredString(record, 'title', 'Câu hỏi thi thật thiếu tiêu đề.'),
    content: requiredString(record, 'content', 'Câu hỏi thi thật thiếu nội dung.'),
    bloomLevel: requiredString(record, 'bloomLevel', 'Câu hỏi thi thật thiếu Bloom level.'),
    difficulty: requiredString(record, 'difficulty', 'Câu hỏi thi thật thiếu độ khó.'),
    imageUrl: readNullableString(record, 'imageUrl'),
    imageDescription: readNullableString(record, 'imageDescription', 'imageAlt'),
  }
}

function normalizeWorkstationTicket(payload: unknown): StudentWorkstationTicket {
  const record = requireRecord(unwrapPayload(payload), 'Vé thi tại máy trạm không hợp lệ.')
  const questionsValue = field(record, 'questions')
  const questions = questionsValue === undefined || questionsValue === null
    ? []
    : Array.isArray(questionsValue)
      ? questionsValue.map(normalizeWorkstationQuestion)
      : invalidResponse('Danh sách câu hỏi của vé thi không hợp lệ.')
  return {
    id: requiredString(record, 'id', 'Vé thi tại máy trạm thiếu mã vé.'),
    shiftId: requiredString(record, 'shiftId', 'Vé thi tại máy trạm thiếu mã ca thi.'),
    shiftName: requiredString(record, 'shiftName', 'Vé thi tại máy trạm thiếu tên ca.'),
    roomLab: requiredString(record, 'roomLab', 'Vé thi tại máy trạm thiếu phòng thi.'),
    startTime: requiredDateString(record, 'startTime', 'Vé thi tại máy trạm thiếu thời gian bắt đầu.'),
    endTime: requiredDateString(record, 'endTime', 'Vé thi tại máy trạm thiếu thời gian kết thúc.'),
    shiftStatus: requiredString(record, 'shiftStatus', 'Vé thi tại máy trạm thiếu trạng thái ca.'),
    sessionTitle: requiredString(record, 'sessionTitle', 'Vé thi tại máy trạm thiếu tên kỳ thi.'),
    studentId: requiredString(record, 'studentId', 'Vé thi tại máy trạm thiếu mã sinh viên.'),
    seatNumber: requiredNumber(record, 'seatNumber', 'Vé thi tại máy trạm thiếu số ghế.'),
    status: normalizeStatus(field(record, 'status')),
    examSetId: readNullableString(record, 'examSetId'),
    examSetCode: readNullableString(record, 'examSetCode'),
    examInputMode: readNullableString(record, 'examInputMode'),
    transcriptBufferSeconds: readNumber(record, 'transcriptBufferSeconds') ?? 60,
    hasFollowUp: readBoolean(record, 'hasFollowUp') ?? false,
    maxFollowUpQuestions: readNumber(record, 'maxFollowUpQuestions') ?? 2,
    questions: questions.sort((left, right) => left.orderIndex - right.orderIndex),
  }
}

function normalizeAuthorization(payload: unknown): AudioUploadAuthorization {
  const record = requireRecord(unwrapPayload(payload), 'Quyền tải audio không hợp lệ.')
  const headers = field(record, 'requiredHeaders')
  const requiredHeaders: Record<string, string> = {}
  if (isRecord(headers)) {
    for (const [key, value] of Object.entries(headers)) {
      if (typeof value === 'string') requiredHeaders[key] = value
    }
  }
  return {
    objectKey: requiredString(record, 'objectKey', 'Quyền tải audio thiếu object key.'),
    fileName: requiredString(record, 'fileName', 'Quyền tải audio thiếu tên file.'),
    uploadUrl: requiredString(record, 'uploadUrl', 'Quyền tải audio thiếu URL tải lên.'),
    expiresAt: requiredDateString(record, 'expiresAt', 'Quyền tải audio thiếu thời gian hết hạn.'),
    requiredHeaders,
  }
}

function normalizeAudioSeal(payload: unknown): AudioSealResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi niêm phong audio không hợp lệ.')
  return {
    ticketId: requiredString(record, 'ticketId', 'Phản hồi niêm phong audio thiếu mã vé.'),
    objectKey: requiredString(record, 'objectKey', 'Phản hồi niêm phong audio thiếu object key.'),
    sha256: requiredString(record, 'sha256', 'Phản hồi niêm phong audio thiếu mã SHA-256.'),
    contentLength: requiredNumber(record, 'contentLength', 'Phản hồi niêm phong audio thiếu dung lượng.'),
    verifiedAt: requiredDateString(record, 'verifiedAt', 'Phản hồi niêm phong audio thiếu thời điểm xác minh.'),
  }
}

function normalizeSubmit(payload: unknown): SubmitOfficialExamResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi nộp bài thi thật không hợp lệ.')
  return {
    ticketId: requiredString(record, 'ticketId', 'Phản hồi nộp bài thi thiếu mã vé.'),
    status: requiredString(record, 'status', 'Phản hồi nộp bài thi thiếu trạng thái.'),
    submittedAt: requiredDateString(record, 'submittedAt', 'Phản hồi nộp bài thi thiếu thời điểm nộp.'),
    submissionCount: requiredNumber(record, 'submissionCount', 'Phản hồi nộp bài thi thiếu số lượng câu.'),
  }
}

function normalizeSubmissionStatus(payload: unknown): OfficialExamSubmissionStatusResponse {
  const record = requireRecord(unwrapPayload(payload), 'Trạng thái nộp bài thi thật không hợp lệ.')
  return {
    ticketId: requiredString(record, 'ticketId', 'Trạng thái nộp bài thiếu mã vé.'),
    status: requiredString(record, 'status', 'Trạng thái nộp bài thiếu trạng thái.'),
    submittedAt: readNullableString(record, 'submittedAt'),
    totalSubmissions: requiredNumber(record, 'totalSubmissions', 'Trạng thái nộp bài thiếu tổng số submission.'),
    pendingSubmissions: requiredNumber(record, 'pendingSubmissions', 'Trạng thái nộp bài thiếu số submission đang chờ.'),
    gradedSubmissions: requiredNumber(record, 'gradedSubmissions', 'Trạng thái nộp bài thiếu số submission đã chấm.'),
    failedSubmissions: requiredNumber(record, 'failedSubmissions', 'Trạng thái nộp bài thiếu số submission lỗi.'),
  }
}

function normalizeOverride(payload: unknown): OverrideExamScoreResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi ghi đè điểm không hợp lệ.')
  return {
    auditId: requiredString(record, 'auditId', 'Phản hồi ghi đè điểm thiếu mã audit.'),
    ticketId: requiredString(record, 'ticketId', 'Phản hồi ghi đè điểm thiếu mã vé.'),
    lecturerId: requiredString(record, 'lecturerId', 'Phản hồi ghi đè điểm thiếu mã giảng viên.'),
    originalAiScore: requiredNumber(record, 'originalAiScore', 'Phản hồi ghi đè điểm thiếu điểm AI gốc.'),
    auditedScore: requiredNumber(record, 'auditedScore', 'Phản hồi ghi đè điểm thiếu điểm hậu kiểm.'),
    overrideReason: requiredString(record, 'overrideReason', 'Phản hồi ghi đè điểm thiếu lý do.'),
    ticketStatus: requiredString(record, 'ticketStatus', 'Phản hồi ghi đè điểm thiếu trạng thái vé.'),
    isLocked: requiredBoolean(record, 'isLocked', 'Phản hồi ghi đè điểm thiếu trạng thái khóa.'),
    auditedAt: requiredDateString(record, 'auditedAt', 'Phản hồi ghi đè điểm thiếu thời điểm hậu kiểm.'),
  }
}

function normalizeLock(payload: unknown): LockExamTicketResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi khóa vé thi không hợp lệ.')
  return {
    ticketId: requiredString(record, 'ticketId', 'Phản hồi khóa vé thiếu mã vé.'),
    isLocked: requiredBoolean(record, 'isLocked', 'Phản hồi khóa vé thiếu trạng thái khóa.'),
    lockedAt: requiredDateString(record, 'lockedAt', 'Phản hồi khóa vé thiếu thời điểm khóa.'),
    status: requiredString(record, 'status', 'Phản hồi khóa vé thiếu trạng thái.'),
    message: requiredString(record, 'message', 'Phản hồi khóa vé thiếu thông báo.'),
  }
}

function normalizeCriterionScore(value: unknown): StudentOfficialExamCriterionScore {
  const record = requireRecord(value, 'Điểm tiêu chí thi thật không hợp lệ.')
  return {
    criterionName: requiredString(record, 'criterionName', 'Điểm tiêu chí thiếu tên.'),
    score: requiredNumber(record, 'score', 'Điểm tiêu chí thiếu điểm.'),
    maxScore: requiredNumber(record, 'maxScore', 'Điểm tiêu chí thiếu điểm tối đa.'),
    comment: readNullableString(record, 'comment'),
  }
}

function normalizeScorecard(value: unknown): StudentOfficialExamQuestionScorecard {
  const record = requireRecord(value, 'Scorecard câu hỏi thi thật không hợp lệ.')
  const criteria = field(record, 'criteriaScores')
  return {
    questionId: requiredString(record, 'questionId', 'Scorecard thiếu mã câu hỏi.'),
    score: requiredNumber(record, 'score', 'Scorecard thiếu điểm.'),
    criteriaScores: Array.isArray(criteria) ? criteria.map(normalizeCriterionScore) : [],
    strengths: readNullableString(record, 'strengths'),
    weaknesses: readNullableString(record, 'weaknesses'),
    improvementSuggestions: readNullableString(record, 'improvementSuggestions'),
    studentTranscript: readNullableString(record, 'studentTranscript'),
    lecturerOverrideReason: readNullableString(record, 'lecturerOverrideReason'),
    questionTitle: readNullableString(record, 'questionTitle'),
    questionContent: readNullableString(record, 'questionContent'),
    questionImageUrl: readNullableString(record, 'questionImageUrl'),
    questionImageDescription: readNullableString(record, 'questionImageDescription'),
  }
}

function normalizeGrade(payload: unknown): StudentOfficialExamGrade {
  const record = requireRecord(unwrapPayload(payload), 'Điểm thi thật không hợp lệ.')
  const scorecards = field(record, 'detailedScorecard')
  const appeal = isRecord(field(record, 'internalAppeal')) ? field(record, 'internalAppeal') as Record<string, unknown> : {}
  return {
    ticketId: requiredString(record, 'ticketId', 'Điểm thi thật thiếu mã vé.'),
    studentCode: requiredString(record, 'studentCode', 'Điểm thi thật thiếu mã sinh viên.'),
    fullName: requiredString(record, 'fullName', 'Điểm thi thật thiếu họ tên.'),
    courseCode: requiredString(record, 'courseCode', 'Điểm thi thật thiếu mã môn.'),
    status: requiredString(record, 'status', 'Điểm thi thật thiếu trạng thái.'),
    isLocked: requiredBoolean(record, 'isLocked', 'Điểm thi thật thiếu trạng thái khóa.'),
    totalScore: readNullableNumber(record, 'totalScore'),
    publishedAt: requiredDateString(record, 'publishedAt', 'Điểm thi thật thiếu thời điểm công bố.'),
    publishedBy: requiredString(record, 'publishedBy', 'Điểm thi thật thiếu người công bố.'),
    studentAcknowledgementStatus: requiredString(record, 'studentAcknowledgementStatus', 'Điểm thi thật thiếu trạng thái xác nhận.'),
    detailedScorecard: Array.isArray(scorecards) ? scorecards.map(normalizeScorecard) : [],
    internalAppeal: {
      canAppeal: readBoolean(appeal, 'canAppeal') ?? false,
      instruction: readString(appeal, 'instruction') ?? '',
      appealDeadlineDays: readNumber(appeal, 'appealDeadlineDays') ?? 0,
      appealEndpoint: readString(appeal, 'appealEndpoint') ?? '/api/v1/appeals',
    },
  }
}

function normalizeAcknowledge(payload: unknown): AcknowledgeOfficialGradeResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi xác nhận điểm không hợp lệ.')
  return {
    ticketId: requiredString(record, 'ticketId', 'Phản hồi xác nhận điểm thiếu mã vé.'),
    studentId: requiredString(record, 'studentId', 'Phản hồi xác nhận điểm thiếu mã sinh viên.'),
    studentAcknowledgementStatus: requiredString(record, 'studentAcknowledgementStatus', 'Phản hồi xác nhận điểm thiếu trạng thái.'),
    acknowledgedAtUtc: requiredDateString(record, 'acknowledgedAtUtc', 'Phản hồi xác nhận điểm thiếu thời điểm.'),
    message: requiredString(record, 'message', 'Phản hồi xác nhận điểm thiếu thông báo.'),
  }
}

function exportFile(response: AxiosResponse<Blob>, fallbackName: string): OfficialExamExportFile {
  const readHeader = (name: string): string | undefined => {
    const value = response.headers[name]
    return typeof value === 'string' ? value : value === undefined || value === null ? undefined : String(value)
  }
  return {
    blob: response.data,
    fileName: normalizeFileName(readHeader('content-disposition'), fallbackName),
    contentType: readHeader('content-type') ?? 'application/octet-stream',
    certificateHash: readHeader('x-certificate-hash') ?? null,
  }
}

export const officialExamService = {
  async getExamStructures(params: GetExamStructuresParams = {}): Promise<ExamStructure[]> {
    const response = await apiClient.get<unknown>('/exam-structures', { params })
    return asArray(response.data, 'API cấu trúc đề thi không trả về danh sách.').map(normalizeExamStructure)
  },

  async getExamStructure(id: string): Promise<ExamStructure> {
    const response = await apiClient.get<unknown>(`/exam-structures/${id}`)
    return normalizeExamStructure(unwrapPayload(response.data))
  },

  async createExamStructure(payload: CreateExamStructureRequest): Promise<ExamStructure> {
    const response = await apiClient.post<unknown>('/exam-structures', payload)
    return normalizeExamStructure(unwrapPayload(response.data))
  },

  async updateExamStructure(id: string, payload: UpdateExamStructureRequest): Promise<ExamStructure> {
    const response = await apiClient.put<unknown>(`/exam-structures/${id}`, payload)
    return normalizeExamStructure(unwrapPayload(response.data))
  },

  async getSessions(params: GetOfficialExamSessionsParams = {}): Promise<OfficialExamSession[]> {
    const response = await apiClient.get<unknown>('/official-exams/sessions', { params })
    return asArray(response.data, 'API kỳ thi thật không trả về danh sách kỳ thi.').map(normalizeSession)
  },

  async createSession(payload: CreateOfficialExamSessionRequest): Promise<OfficialExamSession> {
    const response = await apiClient.post<unknown>('/official-exams/sessions', payload)
    return normalizeSession(unwrapPayload(response.data))
  },

  async getShifts(params: GetExamShiftsParams = {}): Promise<ExamShift[]> {
    const response = await apiClient.get<unknown>('/official-exams/shifts', { params })
    return asArray(response.data, 'API ca thi thật không trả về danh sách ca thi.').map(normalizeShift)
  },

  async createShift(payload: CreateExamShiftRequest): Promise<ExamShift> {
    const response = await apiClient.post<unknown>('/official-exams/shifts', payload)
    return normalizeShift(unwrapPayload(response.data))
  },

  async assignTicket(payload: AssignExamTicketRequest): Promise<ExamTicket> {
    const response = await apiClient.post<unknown>('/official-exams/tickets', payload)
    return normalizeTicket(unwrapPayload(response.data))
  },

  async getCurrentTicket(): Promise<StudentWorkstationTicket> {
    const response = await apiClient.get<unknown>('/official-exams/tickets/current')
    return normalizeWorkstationTicket(response.data)
  },

  async checkIn(ticketId: string): Promise<ExamTicket> {
    const response = await apiClient.post<unknown>(`/official-exams/tickets/${ticketId}/check-in`)
    return normalizeTicket(unwrapPayload(response.data))
  },

  /** Compatibility alias for the Kiosk flow; the dedicated proctor service exposes the typed version. */
  async suspendTicket(
    ticketId: string,
    payload: { reason: string; blurViolationCount?: number | null },
  ): Promise<unknown> {
    const response = await apiClient.post<unknown>(`/proctor/tickets/${ticketId}/suspend`, payload)
    return unwrapPayload(response.data)
  },

  async createAudioUploadAuthorization(ticketId: string): Promise<AudioUploadAuthorization> {
    const response = await apiClient.post<unknown>(
      `/official-exams/tickets/${ticketId}/audio-upload-authorizations`,
    )
    return normalizeAuthorization(response.data)
  },

  /** Uploads the recorded blob directly to the pre-signed storage URL returned by the API. */
  async uploadAudio(authorization: AudioUploadAuthorization, audio: Blob): Promise<void> {
    await axios.put(authorization.uploadUrl, audio, {
      headers: { ...authorization.requiredHeaders },
      timeout: 120000,
    })
  },

  async sealAudio(ticketId: string, payload: SealOfficialExamAudioRequest): Promise<AudioSealResponse> {
    const response = await apiClient.put<unknown>(`/official-exams/tickets/${ticketId}/audio`, payload)
    return normalizeAudioSeal(response.data)
  },

  async submitExam(ticketId: string, answers: OfficialExamAnswerSubmissionRequest[]): Promise<SubmitOfficialExamResponse> {
    const payload: SubmitOfficialExamRequest = { answers }
    const response = await apiClient.put<unknown>(`/official-exams/tickets/${ticketId}/submission`, payload)
    return normalizeSubmit(response.data)
  },

  async getSubmissionStatus(ticketId: string): Promise<OfficialExamSubmissionStatusResponse> {
    const response = await apiClient.get<unknown>(`/official-exams/tickets/${ticketId}/submission`)
    return normalizeSubmissionStatus(response.data)
  },

  async getAuditEvidence(shiftId: string): Promise<AuditEvidenceItem[]> {
    const response = await apiClient.get<unknown>(`/official-exams/shifts/${shiftId}/audit-evidence`)
    return asArray(response.data, 'API hậu kiểm không trả về danh sách bằng chứng.').map(normalizeAuditEvidence)
  },

  async overrideScore(ticketId: string, payload: OverrideExamScoreRequest): Promise<OverrideExamScoreResponse> {
    const response = await apiClient.put<unknown>(`/official-exams/tickets/${ticketId}/override-score`, {
      ...payload,
      ticketId,
    })
    return normalizeOverride(response.data)
  },

  async lockTicket(ticketId: string): Promise<LockExamTicketResponse> {
    const response = await apiClient.post<unknown>(`/official-exams/tickets/${ticketId}/lock`, { ticketId })
    return normalizeLock(response.data)
  },

  async publishGrades(
    shiftId: string,
    payload: PublishGradesRequest = {},
  ): Promise<PublishGradesResponse> {
    const response = await apiClient.post<unknown>(`/official-exams/shifts/${shiftId}/publish-grades`, payload)
    return normalizePublishResponse(response.data)
  },

  async exportShiftExcel(shiftId: string): Promise<OfficialExamExportFile> {
    const response = await apiClient.get<Blob>(`/official-exams/shifts/${shiftId}/export-excel`, {
      responseType: 'blob',
    })
    return exportFile(response, `official-exam-${shiftId}.xlsx`)
  },

  async exportShiftPdf(shiftId: string): Promise<OfficialExamExportFile> {
    const response = await apiClient.get<Blob>(`/official-exams/shifts/${shiftId}/export-pdf`, {
      responseType: 'blob',
    })
    return exportFile(response, `official-exam-${shiftId}.pdf`)
  },

  async getStudentGrade(ticketId: string, studentId?: string): Promise<StudentOfficialExamGrade> {
    const response = await apiClient.get<unknown>(`/student/official-exams/${ticketId}/grade`, {
      params: studentId ? { studentId } : undefined,
    })
    return normalizeGrade(response.data)
  },

  async acknowledgeGrade(ticketId: string, studentId?: string): Promise<AcknowledgeOfficialGradeResponse> {
    const response = await apiClient.post<unknown>(`/student/official-exams/${ticketId}/acknowledge-grade`, undefined, {
      params: studentId ? { studentId } : undefined,
    })
    return normalizeAcknowledge(response.data)
  },
}

export type OfficialExamService = typeof officialExamService
