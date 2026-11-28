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
  AutoAllocatedRoom,
  AutoAllocatedShiftGroup,
  AutoAllocateStudentsRequest,
  AutoAllocateStudentsResponse,
  BatchCreateSessionsRequest,
  BatchCreateSessionsResponse,
  BatchOpenShiftsRequest,
  BatchOpenShiftsResponse,
  BulkExamTicketImportCommitResult,
  BulkExamTicketImportIssue,
  BulkExamTicketImportPreview,
  BulkExamTicketImportPreviewRow,
  AudioSealResponse,
  AudioUploadAuthorization,
  AuditEvidenceItem,
  AuditFollowUpEvidenceItem,
  CandidateImportCommitResponse,
  CandidateImportIssue,
  CandidateImportMode,
  CandidateImportPreviewResponse,
  CandidateImportPreviewRow,
  ConfigureOfficialExamFollowUpRequest,
  DistributeShiftTicketsItem,
  DistributeShiftTicketsRequest,
  DistributeShiftTicketsResponse,
  EligibleCourseSessionSummary,
  ExamAntiCheatLog,
  ExamAttemptType,
  ExamSet,
  ExamSetQuestion,
  CreateExamShiftRequest,
  CreateExamStructureRequest,
  CreateOfficialExamSessionRequest,
  ExamStructure,
  ExamShift,
  ExamTicket,
  ExamTicketStatus,
  GetExamStructuresParams,
  GetExamSetsParams,
  GetExamShiftsParams,
  GetOfficialExamSessionsParams,
  GetSessionCandidatesParams,
  LockExamTicketResponse,
  OfficialExamAnswerSubmissionRequest,
  OfficialExamEnrollmentEstimate,
  OfficialExamAudioPlaybackUrlResponse,
  OfficialExamExportFile,
  OfficialExamSession,
  OfficialExamShiftStaffMember,
  OfficialExamShiftStaffResponse,
  OfficialExamSubmissionStatusResponse,
  OfficialShiftStatusValue,
  OverrideExamScoreRequest,
  OverrideExamScoreResponse,
  PublishGradesRequest,
  PublishGradesResponse,
  PublishExamShiftResponse,
  SealOfficialExamAudioRequest,
  SemesterEligibleExamCourse,
  SessionCandidateItem,
  SessionCandidatesResponse,
  StudentOfficialExamCriterionScore,
  StudentOfficialExamGrade,
  StudentOfficialExamQuestionScorecard,
  StudentWorkstationTicket,
  SubmitOfficialExamRequest,
  SubmitOfficialExamResponse,
  UnlockTicketDeviceResponse,
  UpdateExamShiftRequest,
  UpdateExamStructureRequest,
  WorkstationExamQuestion,
} from '@/types/officialExam.types'

function normalizeStatus(value: unknown): ExamTicketStatus | string {
  return typeof value === 'string' && value.trim() ? value : 'SCHEDULED'
}

function normalizeAuditFollowUpEvidence(value: unknown): AuditFollowUpEvidenceItem {
  const record = requireRecord(value, 'Bằng chứng câu hỏi phụ không hợp lệ.')
  return {
    submissionId: requiredString(record, 'submissionId', 'Bằng chứng câu hỏi phụ thiếu mã bài nộp.', 'id'),
    followUpIndex: readNumber(record, 'followUpIndex') ?? 1,
    followUpPrompt: readNullableString(record, 'followUpPrompt', 'generatedFollowUpPrompt'),
    transcriptWhisper: readNullableString(record, 'transcriptWhisper', 'transcript'),
    audioR2Url: readNullableString(record, 'audioR2Url', 'audioUrl'),
    audioHashSha256: readNullableString(record, 'audioHashSha256', 'audioHash'),
    aiScore: readNullableNumber(record, 'aiScore'),
    finalScore: readNullableNumber(record, 'finalScore'),
    cotTrace: readNullableString(record, 'cotTrace', 'aiChainOfThought'),
  }
}

export function normalizeAuditEvidence(value: unknown): AuditEvidenceItem {
  const record = requireRecord(value, 'Bằng chứng hậu kiểm không hợp lệ.')
  const ticketId = requiredString(record, 'ticketId', 'Bằng chứng hậu kiểm thiếu mã vé thi.')
  const rawFollowUps = field(record, 'followUpItems', 'followUps')
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
    submissionId: readNullableString(record, 'submissionId'),
    overrideReason: readNullableString(record, 'overrideReason'),
    auditedByName: readNullableString(record, 'auditedByName'),
    questionId: readNullableString(record, 'questionId'),
    questionTitle: readNullableString(record, 'questionTitle'),
    questionContent: readNullableString(record, 'questionContent'),
    questionImageUrl: readNullableString(record, 'questionImageUrl'),
    questionImageDescription: readNullableString(record, 'questionImageDescription'),
    followUpItems: Array.isArray(rawFollowUps)
      ? rawFollowUps.map(normalizeAuditFollowUpEvidence)
      : undefined,
  }
}

export function normalizeAntiCheatLog(value: unknown): ExamAntiCheatLog {
  const record = requireRecord(value, 'Log chống gian lận không hợp lệ.')
  const id = requiredString(record, 'id', 'Log chống gian lận thiếu mã.')
  const rawClientEventId = readNullableString(record, 'clientEventId')
  const clientEventId = rawClientEventId && rawClientEventId.trim() ? rawClientEventId.trim() : id
  return {
    id,
    ticketId: requiredString(record, 'ticketId', 'Log chống gian lận thiếu mã vé.'),
    shiftId: requiredString(record, 'shiftId', 'Log chống gian lận thiếu mã ca.'),
    studentId: requiredString(record, 'studentId', 'Log chống gian lận thiếu mã sinh viên.'),
    clientEventId,
    studentCode: readString(record, 'studentCode') ?? '',
    studentName: readString(record, 'studentName') ?? '',
    eventType: requiredString(record, 'eventType', 'Log chống gian lận thiếu loại sự kiện.'),
    severity: requiredString(record, 'severity', 'Log chống gian lận thiếu mức độ.'),
    details: readNullableString(record, 'details'),
    violationCount: readNumber(record, 'violationCount') ?? 0,
    occurredAt: requiredDateString(record, 'occurredAt', 'Log chống gian lận thiếu thời điểm.'),
  }
}

function normalizeExamSetQuestion(value: unknown): ExamSetQuestion {
  const record = requireRecord(value, 'Câu hỏi trong bộ đề không hợp lệ.')
  const duplicateInSets = field(record, 'duplicateInSets')
  return {
    questionId: requiredString(record, 'questionId', 'Câu hỏi trong bộ đề thiếu mã.'),
    orderIndex: requiredNumber(record, 'orderIndex', 'Câu hỏi trong bộ đề thiếu thứ tự.'),
    title: requiredString(record, 'title', 'Câu hỏi trong bộ đề thiếu tiêu đề.'),
    content: requiredString(record, 'content', 'Câu hỏi trong bộ đề thiếu nội dung.'),
    bloomLevel: requiredString(record, 'bloomLevel', 'Câu hỏi trong bộ đề thiếu Bloom level.'),
    difficulty: requiredString(record, 'difficulty', 'Câu hỏi trong bộ đề thiếu độ khó.'),
    isDuplicate: readBoolean(record, 'isDuplicate') ?? false,
    duplicateInSets: Array.isArray(duplicateInSets)
      ? duplicateInSets.filter((item): item is string => typeof item === 'string')
      : [],
  }
}

function normalizeExamSet(value: unknown): ExamSet {
  const record = requireRecord(value, 'Bộ đề thi thật không hợp lệ.')
  const questions = field(record, 'questions')
  return {
    id: requiredString(record, 'id', 'Bộ đề thiếu mã.'),
    structureId: requiredString(record, 'structureId', 'Bộ đề thiếu cấu trúc.'),
    courseId: requiredString(record, 'courseId', 'Bộ đề thiếu mã môn.'),
    courseCode: requiredString(record, 'courseCode', 'Bộ đề thiếu mã môn hiển thị.'),
    sessionId: readNullableString(record, 'sessionId'),
    createdBy: readNullableString(record, 'createdBy'),
    setCode: requiredString(record, 'setCode', 'Bộ đề thiếu mã bộ đề.'),
    status: requiredString(record, 'status', 'Bộ đề thiếu trạng thái.'),
    approvedBy: readNullableString(record, 'approvedBy'),
    reviewNotes: readNullableString(record, 'reviewNotes'),
    submittedAt: readNullableString(record, 'submittedAt'),
    approvedAt: readNullableString(record, 'approvedAt'),
    handedOverAt: readNullableString(record, 'handedOverAt'),
    generatedAt: requiredDateString(record, 'generatedAt', 'Bộ đề thiếu thời điểm tạo.'),
    isActive: readBoolean(record, 'isActive') ?? false,
    questions: Array.isArray(questions) ? questions.map(normalizeExamSetQuestion) : [],
  }
}

function normalizePublishResponse(payload: unknown): PublishGradesResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi công bố điểm không hợp lệ.')
  return {
    shiftId: requiredString(record, 'shiftId', 'Phản hồi công bố điểm thiếu mã ca thi.'),
    totalPublished: requiredNumber(
      record,
      'totalPublished',
      'Phản hồi công bố điểm thiếu tổng số sinh viên.',
    ),
    publishedAt: requiredDateString(
      record,
      'publishedAt',
      'Phản hồi công bố điểm thiếu thời điểm công bố.',
    ),
    message: requiredString(record, 'message', 'Phản hồi công bố điểm thiếu thông báo.'),
  }
}

function normalizePublishShiftResponse(payload: unknown): PublishExamShiftResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi công bố đề không hợp lệ.')
  return {
    success: readBoolean(record, 'success') ?? false,
    shiftId: requiredString(record, 'shiftId', 'Phản hồi công bố đề thiếu mã ca thi.'),
    message: requiredString(record, 'message', 'Phản hồi công bố đề thiếu thông báo.'),
  }
}

function normalizeAttemptType(value: unknown): ExamAttemptType {
  return typeof value === 'string' && value.trim().toUpperCase() === 'RE' ? 'RE' : 'FE'
}

function normalizeSession(value: unknown): OfficialExamSession {
  const record = requireRecord(value, 'Kỳ thi thật không hợp lệ.')
  return {
    id: requiredString(record, 'id', 'Kỳ thi thật thiếu mã kỳ thi.'),
    courseId: requiredString(record, 'courseId', 'Kỳ thi thật thiếu mã môn học.'),
    courseCode: readNullableString(record, 'courseCode'),
    courseName: readNullableString(record, 'courseName'),
    semesterId: readNullableString(record, 'semesterId'),
    examAttemptType: normalizeAttemptType(field(record, 'examAttemptType')),
    examStructureId: readNullableString(record, 'examStructureId') ?? '',
    title: requiredString(record, 'title', 'Kỳ thi thật thiếu tiêu đề.'),
    examDate: requiredString(record, 'examDate', 'Kỳ thi thật thiếu ngày thi.'),
    hasFollowUp: requiredBoolean(record, 'hasFollowUp', 'Kỳ thi thật thiếu cấu hình follow-up.'),
    maxFollowUpQuestions: requiredNumber(
      record,
      'maxFollowUpQuestions',
      'Kỳ thi thật thiếu số câu follow-up.',
    ),
    examInputMode: requiredString(record, 'examInputMode', 'Kỳ thi thật thiếu chế độ nhập bài.'),
    transcriptBufferSeconds: requiredNumber(
      record,
      'transcriptBufferSeconds',
      'Kỳ thi thật thiếu thời gian đệm transcript.',
    ),
    status: requiredString(record, 'status', 'Kỳ thi thật thiếu trạng thái.'),
    candidateCount: readNumber(record, 'candidateCount') ?? undefined,
    allocatedCandidateCount: readNumber(record, 'allocatedCandidateCount') ?? undefined,
  }
}

function normalizeShift(value: unknown): ExamShift {
  const record = requireRecord(value, 'Ca thi thật không hợp lệ.')
  const maxSeats = readNumber(record, 'maxSeats', 'totalSeats') ?? 24
  const assignedTicketsCount = readNumber(record, 'assignedTicketsCount', 'ticketCount') ?? 0
  const remainingSeats =
    readNumber(record, 'remainingSeats') ?? Math.max(0, maxSeats - assignedTicketsCount)
  const gradesPublishedAt = readNullableString(record, 'gradesPublishedAt', 'certificateSignedAt')
  const isGradesPublished =
    readBoolean(record, 'isGradesPublished') ?? Boolean(gradesPublishedAt)
  return {
    id: requiredString(record, 'id', 'Ca thi thật thiếu mã ca thi.'),
    sessionId: requiredString(record, 'sessionId', 'Ca thi thật thiếu mã kỳ thi.'),
    shiftName: requiredString(record, 'shiftName', 'Ca thi thật thiếu tên ca.'),
    roomLab: requiredString(record, 'roomLab', 'Ca thi thật thiếu phòng thi.'),
    startTime: requiredDateString(record, 'startTime', 'Ca thi thật thiếu thời gian bắt đầu.'),
    endTime: requiredDateString(record, 'endTime', 'Ca thi thật thiếu thời gian kết thúc.'),
    proctorUserId: readNullableString(record, 'proctorUserId'),
    examinerUserId: readNullableString(record, 'examinerUserId'),
    examinerName: readNullableString(record, 'examinerName'),
    proctorName: readNullableString(record, 'proctorName'),
    status: requiredString(record, 'status', 'Ca thi thật thiếu trạng thái.'),
    isExamPublished: readBoolean(record, 'isExamPublished') ?? undefined,
    isGradesPublished,
    gradesPublishedAt,
    certificateHash: readNullableString(record, 'certificateHash'),
    examAttemptType:
      field(record, 'examAttemptType') !== undefined
        ? normalizeAttemptType(field(record, 'examAttemptType'))
        : undefined,
    maxSeats,
    assignedTicketsCount,
    remainingSeats,
  }
}

function normalizeExamStructure(value: unknown): ExamStructure {
  const record = requireRecord(value, 'Cấu trúc đề thi không hợp lệ.')
  const requiredSetCount = readNumber(record, 'requiredSetCount')
  const slotDefinitions = readNullableString(record, 'slotDefinitions')
  return {
    id: requiredString(record, 'id', 'Cấu trúc đề thi thiếu mã.'),
    courseId: requiredString(record, 'courseId', 'Cấu trúc đề thi thiếu mã môn.'),
    courseCode: requiredString(record, 'courseCode', 'Cấu trúc đề thi thiếu mã môn hiển thị.'),
    createdBy: requiredString(record, 'createdBy', 'Cấu trúc đề thi thiếu người tạo.'),
    name: requiredString(record, 'name', 'Cấu trúc đề thi thiếu tên.'),
    totalQuestions: requiredNumber(record, 'totalQuestions', 'Cấu trúc đề thi thiếu số câu.'),
    durationMinutes: requiredNumber(record, 'durationMinutes', 'Cấu trúc đề thi thiếu thời lượng.'),
    bloomDistribution: requiredString(
      record,
      'bloomDistribution',
      'Cấu trúc đề thi thiếu phân bổ Bloom.',
    ),
    isActive: requiredBoolean(record, 'isActive', 'Cấu trúc đề thi thiếu trạng thái.'),
    createdAt: requiredDateString(record, 'createdAt', 'Cấu trúc đề thi thiếu thời điểm tạo.'),
    ...(requiredSetCount !== null ? { requiredSetCount } : {}),
    ...(slotDefinitions !== null ? { slotDefinitions } : {}),
  }
}

export function normalizeTicket(value: unknown): ExamTicket {
  const record = requireRecord(value, 'Vé thi thật không hợp lệ.')
  return {
    id: requiredString(record, 'id', 'Vé thi thật thiếu mã vé.'),
    shiftId: requiredString(record, 'shiftId', 'Vé thi thật thiếu mã ca thi.'),
    studentId: requiredString(record, 'studentId', 'Vé thi thật thiếu mã sinh viên.'),
    studentCode: readNullableString(record, 'studentCode'),
    fullName: readNullableString(record, 'fullName'),
    seatNumber: requiredNumber(record, 'seatNumber', 'Vé thi thật thiếu số ghế.'),
    ipAddress: readNullableString(record, 'ipAddress'),
    deviceId: readNullableString(record, 'deviceId'),
    deviceLockedAt: readNullableString(record, 'deviceLockedAt'),
    deviceUnlockCount: readNumber(record, 'deviceUnlockCount') ?? 0,
    status: normalizeStatus(field(record, 'status')),
    checkedInAt: requiredDateString(
      record,
      'checkedInAt',
      'Vé thi thật thiếu thời điểm điểm danh.',
    ),
    examSetId: readNullableString(record, 'examSetId'),
    setCode: readNullableString(record, 'setCode', 'examSetCode'),
  }
}

function normalizeBulkImportIssue(value: unknown): BulkExamTicketImportIssue {
  const record = requireRecord(value, 'Lỗi import roster không hợp lệ.')
  return {
    sourceRow: readNullableNumber(record, 'sourceRow'),
    code: readString(record, 'code') ?? 'IMPORT_ROW_INVALID',
    field: readNullableString(record, 'field'),
    message: requiredString(record, 'message', 'Lỗi import roster thiếu nội dung.'),
  }
}

function normalizeBulkImportRow(value: unknown): BulkExamTicketImportPreviewRow {
  const record = requireRecord(value, 'Dòng preview roster không hợp lệ.')
  const errors = field(record, 'errors')
  const rawIssues = field(record, 'issues')
  const normalizedErrors = Array.isArray(errors) ? errors.map(normalizeBulkImportIssue) : []
  const normalizedIssues = Array.isArray(rawIssues)
    ? rawIssues.filter((item): item is string => typeof item === 'string' && item.trim().length > 0)
    : normalizedErrors.map((issue) => issue.message)
  return {
    sourceRow: requiredNumber(record, 'sourceRow', 'Dòng preview roster thiếu số dòng.'),
    studentCode: readString(record, 'studentCode') ?? '',
    fullName: readNullableString(record, 'fullName'),
    seatNumber: readNullableNumber(record, 'seatNumber'),
    ipAddress: readNullableString(record, 'ipAddress'),
    setCode: readNullableString(record, 'setCode'),
    isValid: readBoolean(record, 'isValid') ?? false,
    errors: normalizedErrors,
    issues: normalizedIssues,
  }
}

function normalizeBulkImportPreview(payload: unknown): BulkExamTicketImportPreview {
  const record = requireRecord(unwrapPayload(payload), 'Preview import roster không hợp lệ.')
  const rows = field(record, 'rows')
  const globalErrors = field(record, 'globalErrors', 'issues')
  const reservedExamSetCodes = field(record, 'reservedExamSetCodes')
  const normalizedRows = Array.isArray(rows) ? rows.map(normalizeBulkImportRow) : []
  const rowErrorMessages = new Set(
    normalizedRows.flatMap((row) =>
      row.errors.map((issue) => `${issue.sourceRow ?? ''}:${issue.code}:${issue.message}`),
    ),
  )
  const parsedGlobalErrors = Array.isArray(globalErrors)
    ? globalErrors
        .map(normalizeBulkImportIssue)
        .filter(
          (issue) =>
            issue.sourceRow === null ||
            !rowErrorMessages.has(`${issue.sourceRow ?? ''}:${issue.code}:${issue.message}`),
        )
    : []
  const maxSeats = readNullableNumber(record, 'maxSeats') ?? undefined
  const existingTicketCount = readNullableNumber(record, 'existingTicketCount') ?? undefined
  const remainingSeats =
    readNullableNumber(record, 'remainingSeats') ??
    (maxSeats !== undefined && existingTicketCount !== undefined
      ? Math.max(0, maxSeats - existingTicketCount)
      : undefined)
  return {
    shiftId: requiredString(record, 'shiftId', 'Preview import roster thiếu mã ca thi.'),
    totalRowCount: readNumber(record, 'totalRowCount', 'rowCount') ?? 0,
    validRowCount: readNumber(record, 'validRowCount') ?? 0,
    errorRowCount: readNumber(record, 'errorRowCount') ?? 0,
    availableHandedOverSetCount:
      readNumber(record, 'availableHandedOverSetCount', 'availableSetCount') ?? 0,
    rows: normalizedRows,
    globalErrors: parsedGlobalErrors,
    reservedExamSetCodes: Array.isArray(reservedExamSetCodes)
      ? reservedExamSetCodes.filter((code): code is string => typeof code === 'string')
      : normalizedRows
          .map((row) => row.setCode)
          .filter((code): code is string => typeof code === 'string' && code.trim().length > 0),
    maxSeats,
    existingTicketCount,
    remainingSeats,
    canCommit: readBoolean(record, 'canCommit') ?? false,
  }
}

function normalizeBulkImportCommit(payload: unknown): BulkExamTicketImportCommitResult {
  const record = requireRecord(unwrapPayload(payload), 'Kết quả import roster không hợp lệ.')
  const tickets = field(record, 'tickets')
  return {
    shiftId: requiredString(record, 'shiftId', 'Kết quả import roster thiếu mã ca thi.'),
    createdTicketCount: readNumber(record, 'createdTicketCount') ?? 0,
    tickets: Array.isArray(tickets) ? tickets.map(normalizeTicket) : [],
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

export function normalizeWorkstationTicket(payload: unknown): StudentWorkstationTicket {
  const record = requireRecord(unwrapPayload(payload), 'Vé thi tại máy trạm không hợp lệ.')
  const questionsValue = field(record, 'questions')
  const questions =
    questionsValue === undefined || questionsValue === null
      ? []
      : Array.isArray(questionsValue)
        ? questionsValue.map(normalizeWorkstationQuestion)
        : invalidResponse('Danh sách câu hỏi của vé thi không hợp lệ.')
  return {
    id: requiredString(record, 'id', 'Vé thi tại máy trạm thiếu mã vé.'),
    shiftId: requiredString(record, 'shiftId', 'Vé thi tại máy trạm thiếu mã ca thi.'),
    shiftName: requiredString(record, 'shiftName', 'Vé thi tại máy trạm thiếu tên ca.'),
    roomLab: requiredString(record, 'roomLab', 'Vé thi tại máy trạm thiếu phòng thi.'),
    startTime: requiredDateString(
      record,
      'startTime',
      'Vé thi tại máy trạm thiếu thời gian bắt đầu.',
    ),
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

export function normalizeAuthorization(payload: unknown): AudioUploadAuthorization {
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

export function normalizeAudioSeal(payload: unknown): AudioSealResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi niêm phong audio không hợp lệ.')
  return {
    ticketId: requiredString(record, 'ticketId', 'Phản hồi niêm phong audio thiếu mã vé.'),
    objectKey: requiredString(record, 'objectKey', 'Phản hồi niêm phong audio thiếu object key.'),
    sha256: requiredString(record, 'sha256', 'Phản hồi niêm phong audio thiếu mã SHA-256.'),
    contentLength: requiredNumber(
      record,
      'contentLength',
      'Phản hồi niêm phong audio thiếu dung lượng.',
    ),
    verifiedAt: requiredDateString(
      record,
      'verifiedAt',
      'Phản hồi niêm phong audio thiếu thời điểm xác minh.',
    ),
  }
}

export function normalizeSubmit(payload: unknown): SubmitOfficialExamResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi nộp bài thi thật không hợp lệ.')
  return {
    ticketId: requiredString(record, 'ticketId', 'Phản hồi nộp bài thi thiếu mã vé.'),
    status: requiredString(record, 'status', 'Phản hồi nộp bài thi thiếu trạng thái.'),
    submittedAt: requiredDateString(
      record,
      'submittedAt',
      'Phản hồi nộp bài thi thiếu thời điểm nộp.',
    ),
    submissionCount: requiredNumber(
      record,
      'submissionCount',
      'Phản hồi nộp bài thi thiếu số lượng câu.',
    ),
  }
}

export function normalizeSubmissionStatus(payload: unknown): OfficialExamSubmissionStatusResponse {
  const record = requireRecord(unwrapPayload(payload), 'Trạng thái nộp bài thi thật không hợp lệ.')
  return {
    ticketId: requiredString(record, 'ticketId', 'Trạng thái nộp bài thiếu mã vé.'),
    status: requiredString(record, 'status', 'Trạng thái nộp bài thiếu trạng thái.'),
    submittedAt: readNullableString(record, 'submittedAt'),
    totalSubmissions: requiredNumber(
      record,
      'totalSubmissions',
      'Trạng thái nộp bài thiếu tổng số submission.',
    ),
    pendingSubmissions: requiredNumber(
      record,
      'pendingSubmissions',
      'Trạng thái nộp bài thiếu số submission đang chờ.',
    ),
    gradedSubmissions: requiredNumber(
      record,
      'gradedSubmissions',
      'Trạng thái nộp bài thiếu số submission đã chấm.',
    ),
    failedSubmissions: requiredNumber(
      record,
      'failedSubmissions',
      'Trạng thái nộp bài thiếu số submission lỗi.',
    ),
  }
}

export function normalizeOverride(payload: unknown): OverrideExamScoreResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi ghi đè điểm không hợp lệ.')
  return {
    auditId: requiredString(record, 'auditId', 'Phản hồi ghi đè điểm thiếu mã audit.'),
    ticketId: requiredString(record, 'ticketId', 'Phản hồi ghi đè điểm thiếu mã vé.'),
    lecturerId: requiredString(record, 'lecturerId', 'Phản hồi ghi đè điểm thiếu mã giảng viên.'),
    originalAiScore: requiredNumber(
      record,
      'originalAiScore',
      'Phản hồi ghi đè điểm thiếu điểm AI gốc.',
    ),
    auditedScore: requiredNumber(
      record,
      'auditedScore',
      'Phản hồi ghi đè điểm thiếu điểm hậu kiểm.',
    ),
    overrideReason: requiredString(record, 'overrideReason', 'Phản hồi ghi đè điểm thiếu lý do.'),
    ticketStatus: requiredString(
      record,
      'ticketStatus',
      'Phản hồi ghi đè điểm thiếu trạng thái vé.',
    ),
    isLocked: requiredBoolean(record, 'isLocked', 'Phản hồi ghi đè điểm thiếu trạng thái khóa.'),
    auditedAt: requiredDateString(
      record,
      'auditedAt',
      'Phản hồi ghi đè điểm thiếu thời điểm hậu kiểm.',
    ),
  }
}

export function normalizeLock(payload: unknown): LockExamTicketResponse {
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
  const appeal = isRecord(field(record, 'internalAppeal'))
    ? (field(record, 'internalAppeal') as Record<string, unknown>)
    : {}
  return {
    ticketId: requiredString(record, 'ticketId', 'Điểm thi thật thiếu mã vé.'),
    studentCode: requiredString(record, 'studentCode', 'Điểm thi thật thiếu mã sinh viên.'),
    fullName: requiredString(record, 'fullName', 'Điểm thi thật thiếu họ tên.'),
    courseCode: requiredString(record, 'courseCode', 'Điểm thi thật thiếu mã môn.'),
    status: requiredString(record, 'status', 'Điểm thi thật thiếu trạng thái.'),
    isLocked: requiredBoolean(record, 'isLocked', 'Điểm thi thật thiếu trạng thái khóa.'),
    totalScore: readNullableNumber(record, 'totalScore'),
    publishedAt: readNullableString(record, 'publishedAt'),
    publishedBy: requiredString(record, 'publishedBy', 'Điểm thi thật thiếu người công bố.'),
    studentAcknowledgementStatus: requiredString(
      record,
      'studentAcknowledgementStatus',
      'Điểm thi thật thiếu trạng thái xác nhận.',
    ),
    detailedScorecard: Array.isArray(scorecards) ? scorecards.map(normalizeScorecard) : [],
    internalAppeal: {
      canAppeal: readBoolean(appeal, 'canAppeal') ?? false,
      instruction: readString(appeal, 'instruction') ?? '',
      appealDeadlineDays: readNumber(appeal, 'appealDeadlineDays') ?? 0,
      appealDeadlineAtUtc: readNullableString(appeal, 'appealDeadlineAtUtc'),
      canAcknowledge: readBoolean(appeal, 'canAcknowledge') ?? false,
      appealEndpoint: readString(appeal, 'appealEndpoint') ?? '/api/v1/appeals',
    },
  }
}

function normalizeAcknowledge(payload: unknown): AcknowledgeOfficialGradeResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi xác nhận điểm không hợp lệ.')
  return {
    ticketId: requiredString(record, 'ticketId', 'Phản hồi xác nhận điểm thiếu mã vé.'),
    studentId: requiredString(record, 'studentId', 'Phản hồi xác nhận điểm thiếu mã sinh viên.'),
    studentAcknowledgementStatus: requiredString(
      record,
      'studentAcknowledgementStatus',
      'Phản hồi xác nhận điểm thiếu trạng thái.',
    ),
    acknowledgedAtUtc: requiredDateString(
      record,
      'acknowledgedAtUtc',
      'Phản hồi xác nhận điểm thiếu thời điểm.',
    ),
    message: requiredString(record, 'message', 'Phản hồi xác nhận điểm thiếu thông báo.'),
  }
}

function exportFile(response: AxiosResponse<Blob>, fallbackName: string): OfficialExamExportFile {
  const readHeader = (name: string): string | undefined => {
    const value = response.headers[name]
    return typeof value === 'string'
      ? value
      : value === undefined || value === null
        ? undefined
        : String(value)
  }
  return {
    blob: response.data,
    fileName: normalizeFileName(readHeader('content-disposition'), fallbackName),
    contentType: readHeader('content-type') ?? 'application/octet-stream',
    certificateHash: readHeader('x-certificate-hash') ?? null,
  }
}

function normalizeCandidateImportMode(value: unknown): CandidateImportMode {
  return typeof value === 'string' && value.trim().toUpperCase() === 'APPEND' ? 'APPEND' : 'REPLACE'
}

function normalizeEligibleSessionSummary(value: unknown): EligibleCourseSessionSummary | null {
  if (!isRecord(value)) return null
  const sessionId = readString(value, 'sessionId', 'id')
  if (!sessionId) return null
  return {
    sessionId,
    title: readString(value, 'title') ?? '',
    status: readString(value, 'status') ?? 'scheduled',
    candidateCount: readNumber(value, 'candidateCount') ?? 0,
    allocatedCount: readNumber(value, 'allocatedCount', 'allocatedCandidateCount') ?? 0,
  }
}

function normalizeEligibleCourse(value: unknown): SemesterEligibleExamCourse {
  const record = requireRecord(value, 'Thông tin môn đủ điều kiện mở kỳ thi không hợp lệ.')
  const existingSessionId = readNullableString(record, 'existingSessionId')
  const existingSessionTitle = readNullableString(record, 'existingSessionTitle')
  const existingSessionStatus = readNullableString(record, 'existingSessionStatus')
  const feSession =
    normalizeEligibleSessionSummary(field(record, 'feSession')) ??
    (existingSessionId
      ? {
          sessionId: existingSessionId,
          title: existingSessionTitle ?? '',
          status: existingSessionStatus ?? 'scheduled',
          candidateCount: readNumber(record, 'feCandidateCount') ?? 0,
          allocatedCount: 0,
        }
      : null)
  return {
    courseId: requiredString(record, 'courseId', 'Môn đủ điều kiện thiếu mã môn.'),
    courseCode: requiredString(record, 'courseCode', 'Môn đủ điều kiện thiếu mã hiển thị.'),
    courseName: requiredString(record, 'courseName', 'Môn đủ điều kiện thiếu tên môn.'),
    semesterId: requiredString(record, 'semesterId', 'Môn đủ điều kiện thiếu học kỳ.'),
    semesterCode: readString(record, 'semesterCode') ?? '',
    activeClassCount: readNumber(record, 'activeClassCount') ?? 0,
    eligibleStudentCount: readNumber(record, 'eligibleStudentCount') ?? 0,
    maxStudentsPerRoom: readNumber(record, 'maxStudentsPerRoom') ?? 24,
    recommendedRoomCount: readNumber(record, 'recommendedRoomCount') ?? 0,
    activeExamStructureId: readNullableString(record, 'activeExamStructureId'),
    activeExamStructureName: readNullableString(record, 'activeExamStructureName'),
    handedOverExamSetCount: readNumber(record, 'handedOverExamSetCount') ?? 0,
    hasFollowUp: readBoolean(record, 'hasFollowUp') ?? false,
    maxFollowUpQuestions: readNumber(record, 'maxFollowUpQuestions') ?? 2,
    existingSessionId,
    existingSessionTitle,
    existingSessionStatus,
    feSession,
    reSession: normalizeEligibleSessionSummary(field(record, 'reSession')),
  }
}

function normalizeBatchCreateSessionsResponse(payload: unknown): BatchCreateSessionsResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi tạo hàng loạt kỳ thi không hợp lệ.')
  const sessions = field(record, 'sessions')
  return {
    semesterId: requiredString(record, 'semesterId', 'Phản hồi tạo kỳ thi thiếu mã học kỳ.'),
    createdCount: readNumber(record, 'createdCount') ?? (Array.isArray(sessions) ? sessions.length : 0),
    sessions: Array.isArray(sessions) ? sessions.map(normalizeSession) : [],
  }
}

function normalizeCandidateImportIssue(value: unknown): CandidateImportIssue {
  const record = requireRecord(value, 'Lỗi import danh sách thí sinh không hợp lệ.')
  return {
    sourceRow: readNullableNumber(record, 'sourceRow'),
    message: requiredString(record, 'message', 'Lỗi import thí sinh thiếu nội dung.'),
  }
}

function normalizeCandidateImportPreviewRow(value: unknown): CandidateImportPreviewRow {
  const record = requireRecord(value, 'Dòng preview thí sinh không hợp lệ.')
  const rawIssues = field(record, 'issues')
  const issues = Array.isArray(rawIssues)
    ? rawIssues.filter((item): item is string => typeof item === 'string' && item.trim().length > 0)
    : []
  return {
    sourceRow: readNumber(record, 'sourceRow') ?? 0,
    studentCode: readString(record, 'studentCode') ?? '',
    studentId: readNullableString(record, 'studentId'),
    fullName: readNullableString(record, 'fullName'),
    email: readNullableString(record, 'email'),
    isEnrolledInSemester: readBoolean(record, 'isEnrolledInSemester') ?? true,
    alreadyInSession: readBoolean(record, 'alreadyInSession') ?? false,
    isValid: readBoolean(record, 'isValid') ?? issues.length === 0,
    issues,
  }
}

function normalizeCandidateImportPreview(payload: unknown): CandidateImportPreviewResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi preview thí sinh không hợp lệ.')
  const rawRows = field(record, 'rows')
  const rawIssues = field(record, 'issues')
  const rows = Array.isArray(rawRows) ? rawRows.map(normalizeCandidateImportPreviewRow) : []
  const maxStudentsPerRoom = readNumber(record, 'maxStudentsPerRoom') ?? 24
  const validCount = readNumber(record, 'validCount') ?? rows.filter((row) => row.isValid).length
  const invalidCount =
    readNumber(record, 'invalidCount', 'errorCount') ?? rows.filter((row) => !row.isValid).length
  const projectedTotalCandidates = readNumber(record, 'projectedTotalCandidates') ?? validCount
  return {
    sessionId: requiredString(record, 'sessionId', 'Preview thí sinh thiếu mã kỳ thi.'),
    examAttemptType: normalizeAttemptType(field(record, 'examAttemptType')),
    mode: normalizeCandidateImportMode(field(record, 'mode')),
    totalRows: readNumber(record, 'totalRows') ?? rows.length,
    validCount,
    invalidCount,
    duplicateCount: readNumber(record, 'duplicateCount') ?? 0,
    canCommit: readBoolean(record, 'canCommit') ?? (invalidCount === 0 && validCount > 0),
    existingCandidateCount: readNumber(record, 'existingCandidateCount') ?? 0,
    projectedTotalCandidates,
    maxStudentsPerRoom,
    recommendedTotalRooms:
      readNumber(record, 'recommendedTotalRooms') ??
      Math.ceil(projectedTotalCandidates / Math.max(1, maxStudentsPerRoom)),
    issues: Array.isArray(rawIssues) ? rawIssues.map(normalizeCandidateImportIssue) : [],
    rows,
  }
}

function normalizeCandidateImportCommit(payload: unknown): CandidateImportCommitResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi lưu danh sách thí sinh không hợp lệ.')
  const totalCandidateCount = readNumber(record, 'totalCandidateCount') ?? 0
  return {
    sessionId: requiredString(record, 'sessionId', 'Kết quả import thí sinh thiếu mã kỳ thi.'),
    examAttemptType: normalizeAttemptType(field(record, 'examAttemptType')),
    mode: normalizeCandidateImportMode(field(record, 'mode')),
    importedCount: readNumber(record, 'importedCount') ?? 0,
    totalCandidateCount,
    recommendedTotalRooms:
      readNumber(record, 'recommendedTotalRooms') ?? Math.ceil(totalCandidateCount / 24),
    importedAt: readString(record, 'importedAt') ?? new Date().toISOString(),
  }
}

function normalizeSessionCandidateItem(value: unknown): SessionCandidateItem {
  const record = requireRecord(value, 'Thông tin thí sinh trong kỳ thi không hợp lệ.')
  return {
    studentId: requiredString(record, 'studentId', 'Thí sinh thiếu mã người dùng.'),
    studentCode: requiredString(record, 'studentCode', 'Thí sinh thiếu MSSV.'),
    fullName: readString(record, 'fullName') ?? '',
    email: readNullableString(record, 'email'),
    importedAt: readString(record, 'importedAt') ?? '',
    isAllocated: readBoolean(record, 'isAllocated') ?? false,
    ticketId: readNullableString(record, 'ticketId'),
    shiftId: readNullableString(record, 'shiftId'),
    roomLab: readNullableString(record, 'roomLab'),
    seatNumber: readNullableNumber(record, 'seatNumber'),
    ticketStatus: readNullableString(record, 'ticketStatus'),
  }
}

function normalizeSessionCandidates(payload: unknown): SessionCandidatesResponse {
  const record = requireRecord(unwrapPayload(payload), 'Danh sách thí sinh kỳ thi không hợp lệ.')
  const rawItems = field(record, 'items')
  const items = Array.isArray(rawItems) ? rawItems.map(normalizeSessionCandidateItem) : []
  const totalCount = readNumber(record, 'totalCount') ?? items.length
  const allocatedCount =
    readNumber(record, 'allocatedCount') ?? items.filter((item) => item.isAllocated).length
  return {
    sessionId: requiredString(record, 'sessionId', 'Danh sách thí sinh thiếu mã kỳ thi.'),
    examAttemptType: normalizeAttemptType(field(record, 'examAttemptType')),
    totalCount,
    allocatedCount,
    unallocatedCount: readNumber(record, 'unallocatedCount') ?? Math.max(0, totalCount - allocatedCount),
    pageNumber: readNumber(record, 'pageNumber') ?? 1,
    pageSize: readNumber(record, 'pageSize') ?? 50,
    items,
  }
}

function normalizeEnrollmentEstimate(payload: unknown): OfficialExamEnrollmentEstimate {
  const record = requireRecord(unwrapPayload(payload), 'Ước tính phòng thi không hợp lệ.')
  const maxStudentsPerRoom = readNumber(record, 'maxStudentsPerRoom') ?? 24
  const importedCandidateCount =
    readNumber(record, 'importedCandidateCount', 'eligibleStudentCount') ?? 0
  const eligibleStudentCount = readNumber(record, 'eligibleStudentCount') ?? importedCandidateCount
  const allocatedStudentCount = readNumber(record, 'allocatedStudentCount') ?? 0
  const unallocatedStudentCount =
    readNumber(record, 'unallocatedStudentCount') ??
    Math.max(0, eligibleStudentCount - allocatedStudentCount)
  return {
    sessionId: requiredString(record, 'sessionId', 'Ước tính phòng thiếu mã kỳ thi.'),
    courseId: requiredString(record, 'courseId', 'Ước tính phòng thiếu mã môn học.'),
    courseCode: readString(record, 'courseCode') ?? '',
    courseName: readString(record, 'courseName') ?? '',
    semesterId: readString(record, 'semesterId') ?? '',
    semesterCode: readString(record, 'semesterCode') ?? '',
    examAttemptType: normalizeAttemptType(field(record, 'examAttemptType')),
    candidateSource: readString(record, 'candidateSource') ?? 'IMPORTED_CANDIDATES',
    hasImportedCandidates: readBoolean(record, 'hasImportedCandidates') ?? importedCandidateCount > 0,
    importedCandidateCount,
    semesterEnrolledCount: readNumber(record, 'semesterEnrolledCount') ?? 0,
    eligibleStudentCount,
    allocatedStudentCount,
    unallocatedStudentCount,
    maxStudentsPerRoom,
    recommendedTotalRooms:
      readNumber(record, 'recommendedTotalRooms') ??
      Math.ceil(eligibleStudentCount / Math.max(1, maxStudentsPerRoom)),
    recommendedAdditionalRooms: readNumber(record, 'recommendedAdditionalRooms') ?? 0,
    currentShiftRoomCount: readNumber(record, 'currentShiftRoomCount') ?? 0,
    currentTotalCapacity: readNumber(record, 'currentTotalCapacity') ?? 0,
    currentAvailableSeats: readNumber(record, 'currentAvailableSeats') ?? 0,
    handedOverExamSetCount: readNumber(record, 'handedOverExamSetCount') ?? 0,
  }
}

function normalizeBatchOpenShiftsResponse(payload: unknown): BatchOpenShiftsResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi mở hàng loạt phòng thi không hợp lệ.')
  const shifts = field(record, 'shifts')
  return {
    sessionId: requiredString(record, 'sessionId', 'Phản hồi mở phòng thiếu mã kỳ thi.'),
    shiftGroupName: readString(record, 'shiftGroupName') ?? '',
    createdRoomCount: readNumber(record, 'createdRoomCount') ?? (Array.isArray(shifts) ? shifts.length : 0),
    totalCapacityCreated: readNumber(record, 'totalCapacityCreated') ?? 0,
    shifts: Array.isArray(shifts) ? shifts.map(normalizeShift) : [],
  }
}

function normalizeAutoAllocatedRoom(value: unknown): AutoAllocatedRoom {
  const record = requireRecord(value, 'Phòng phân bổ tự động không hợp lệ.')
  return {
    shiftId: requiredString(record, 'shiftId', 'Phòng phân bổ thiếu mã ca thi.'),
    roomLab: readString(record, 'roomLab') ?? '',
    maxSeats: readNumber(record, 'maxSeats') ?? 24,
    assignedBefore: readNumber(record, 'assignedBefore') ?? 0,
    assignedNow: readNumber(record, 'assignedNow') ?? 0,
    totalAfter: readNumber(record, 'totalAfter') ?? 0,
  }
}

function normalizeAutoAllocatedShiftGroup(value: unknown): AutoAllocatedShiftGroup {
  const record = requireRecord(value, 'Nhóm ca phân bổ tự động không hợp lệ.')
  const rooms = field(record, 'rooms')
  return {
    startTime: readString(record, 'startTime') ?? '',
    endTime: readString(record, 'endTime') ?? '',
    isFinalBalancedShift: readBoolean(record, 'isFinalBalancedShift') ?? false,
    totalAssignedInGroup: readNumber(record, 'totalAssignedInGroup') ?? 0,
    rooms: Array.isArray(rooms) ? rooms.map(normalizeAutoAllocatedRoom) : [],
  }
}

function normalizeAutoAllocateResponse(payload: unknown): AutoAllocateStudentsResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi chia phòng ngẫu nhiên không hợp lệ.')
  const shiftGroups = field(record, 'shiftGroups')
  return {
    sessionId: requiredString(record, 'sessionId', 'Phản hồi chia phòng thiếu mã kỳ thi.'),
    dryRun: readBoolean(record, 'dryRun') ?? false,
    totalEligibleStudents: readNumber(record, 'totalEligibleStudents') ?? 0,
    previouslyAllocatedCount: readNumber(record, 'previouslyAllocatedCount') ?? 0,
    newlyAllocatedCount: readNumber(record, 'newlyAllocatedCount') ?? 0,
    shiftGroups: Array.isArray(shiftGroups)
      ? shiftGroups.map(normalizeAutoAllocatedShiftGroup)
      : [],
  }
}

function normalizeDistributeTicketsItem(value: unknown): DistributeShiftTicketsItem {
  const record = requireRecord(value, 'Thông tin vé được phát đề không hợp lệ.')
  return {
    ticketId: requiredString(record, 'ticketId', 'Vé phát đề thiếu mã vé.'),
    studentCode: readString(record, 'studentCode') ?? '',
    seatNumber: readNumber(record, 'seatNumber') ?? 0,
    examSetId: requiredString(record, 'examSetId', 'Vé phát đề thiếu mã bộ đề.'),
  }
}

function normalizeDistributeTicketsResponse(payload: unknown): DistributeShiftTicketsResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi phát bộ đề ca thi không hợp lệ.')
  const tickets = field(record, 'distributedTickets')
  return {
    shiftId: requiredString(record, 'shiftId', 'Phản hồi phát đề thiếu mã ca thi.'),
    totalDistributed: readNumber(record, 'totalDistributed') ?? 0,
    message: readString(record, 'message') ?? 'Đã phân bổ bộ đề thi cho ca thi.',
    distributedTickets: Array.isArray(tickets) ? tickets.map(normalizeDistributeTicketsItem) : [],
  }
}

export const officialExamService = {
  async getExamStructures(params: GetExamStructuresParams = {}): Promise<ExamStructure[]> {
    const response = await apiClient.get<unknown>('/exam-structures', { params })
    return asArray(response.data, 'API cấu trúc đề thi không trả về danh sách.').map(
      normalizeExamStructure,
    )
  },

  async getExamStructure(id: string): Promise<ExamStructure> {
    const response = await apiClient.get<unknown>(`/exam-structures/${id}`)
    return normalizeExamStructure(unwrapPayload(response.data))
  },

  async getExamSets(params: GetExamSetsParams): Promise<ExamSet[]> {
    const response = await apiClient.get<unknown>('/official-exams/exam-sets', { params })
    return asArray(response.data, 'API bộ đề thi thật không trả về danh sách.').map(
      normalizeExamSet,
    )
  },

  async createExamStructure(payload: CreateExamStructureRequest): Promise<ExamStructure> {
    const response = await apiClient.post<unknown>('/exam-structures', payload)
    return normalizeExamStructure(unwrapPayload(response.data))
  },

  async updateExamStructure(
    id: string,
    payload: UpdateExamStructureRequest,
  ): Promise<ExamStructure> {
    const response = await apiClient.put<unknown>(`/exam-structures/${id}`, payload)
    return normalizeExamStructure(unwrapPayload(response.data))
  },

  async getSessions(params: GetOfficialExamSessionsParams = {}): Promise<OfficialExamSession[]> {
    const response = await apiClient.get<unknown>('/official-exams/sessions', { params })
    return asArray(response.data, 'API kỳ thi thật không trả về danh sách kỳ thi.').map(
      normalizeSession,
    )
  },

  async createSession(payload: CreateOfficialExamSessionRequest): Promise<OfficialExamSession> {
    const response = await apiClient.post<unknown>('/official-exams/sessions', payload)
    return normalizeSession(unwrapPayload(response.data))
  },

  async getShifts(params: GetExamShiftsParams = {}): Promise<ExamShift[]> {
    const response = await apiClient.get<unknown>('/official-exams/shifts', { params })
    return asArray(response.data, 'API ca thi thật không trả về danh sách ca thi.').map(
      normalizeShift,
    )
  },

  async createShift(payload: CreateExamShiftRequest): Promise<ExamShift> {
    const response = await apiClient.post<unknown>('/official-exams/shifts', payload)
    return normalizeShift(unwrapPayload(response.data))
  },

  async updateShift(shiftId: string, payload: UpdateExamShiftRequest): Promise<void> {
    await apiClient.put<unknown>(`/official-exams/shifts/${shiftId}`, payload)
  },

  async deleteShift(shiftId: string): Promise<void> {
    await apiClient.delete<unknown>(`/official-exams/shifts/${shiftId}`)
  },

  async publishShift(shiftId: string): Promise<PublishExamShiftResponse> {
    const response = await apiClient.post<unknown>(`/official-exams/shifts/${shiftId}/publish`)
    return normalizePublishShiftResponse(response.data)
  },

  async assignTicket(payload: AssignExamTicketRequest): Promise<ExamTicket> {
    const response = await apiClient.post<unknown>('/official-exams/tickets', payload)
    return normalizeTicket(unwrapPayload(response.data))
  },

  async previewTicketRosterImport(
    shiftId: string,
    file: File,
  ): Promise<BulkExamTicketImportPreview> {
    const formData = new FormData()
    formData.append('file', file)
    const response = await apiClient.post<unknown>(
      `/official-exams/shifts/${shiftId}/tickets/import/preview`,
      formData,
    )
    return normalizeBulkImportPreview(response.data)
  },

  async commitTicketRosterImport(
    shiftId: string,
    file: File,
  ): Promise<BulkExamTicketImportCommitResult> {
    const formData = new FormData()
    formData.append('file', file)
    const response = await apiClient.post<unknown>(
      `/official-exams/shifts/${shiftId}/tickets/import/commit`,
      formData,
    )
    return normalizeBulkImportCommit(response.data)
  },

  async downloadCandidateImportTemplate(format: 'csv' | 'xlsx' = 'xlsx'): Promise<OfficialExamExportFile> {
    const response = await apiClient.get<Blob>('/official-exams/candidates/import/template', {
      params: { format },
      responseType: 'blob',
    })
    return exportFile(response, `exam-candidates-template.${format}`)
  },

  async downloadCandidateTemplate(format: 'csv' | 'xlsx' = 'xlsx'): Promise<OfficialExamExportFile> {
    return this.downloadCandidateImportTemplate(format)
  },

  async downloadTicketRosterTemplate(format: 'csv' | 'xlsx' = 'xlsx'): Promise<OfficialExamExportFile> {
    const response = await apiClient.get<Blob>('/official-exams/tickets/import/template', {
      params: { format },
      responseType: 'blob',
    })
    return exportFile(response, `exam-tickets-template.${format}`)
  },

  async getEligibleCourses(semesterId: string): Promise<SemesterEligibleExamCourse[]> {
    const response = await apiClient.get<unknown>(
      `/official-exams/semesters/${semesterId}/eligible-courses`,
    )
    return asArray(response.data, 'API môn đủ điều kiện mở kỳ thi không trả về danh sách.').map(
      normalizeEligibleCourse,
    )
  },

  async batchCreateSessions(
    semesterId: string,
    payload: BatchCreateSessionsRequest,
  ): Promise<BatchCreateSessionsResponse> {
    const response = await apiClient.post<unknown>(
      `/official-exams/semesters/${semesterId}/sessions/batch`,
      payload,
    )
    return normalizeBatchCreateSessionsResponse(response.data)
  },

  async previewCandidateImport(
    sessionId: string,
    file: File,
    mode: CandidateImportMode = 'REPLACE',
  ): Promise<CandidateImportPreviewResponse> {
    const formData = new FormData()
    formData.append('file', file)
    formData.append('mode', mode)
    const response = await apiClient.post<unknown>(
      `/official-exams/sessions/${sessionId}/candidates/import/preview`,
      formData,
    )
    return normalizeCandidateImportPreview(response.data)
  },

  async commitCandidateImport(
    sessionId: string,
    file: File,
    mode: CandidateImportMode = 'REPLACE',
  ): Promise<CandidateImportCommitResponse> {
    const formData = new FormData()
    formData.append('file', file)
    formData.append('mode', mode)
    const response = await apiClient.post<unknown>(
      `/official-exams/sessions/${sessionId}/candidates/import/commit`,
      formData,
    )
    return normalizeCandidateImportCommit(response.data)
  },

  async getSessionCandidates(
    sessionId: string,
    params: GetSessionCandidatesParams = {},
  ): Promise<SessionCandidatesResponse> {
    const response = await apiClient.get<unknown>(
      `/official-exams/sessions/${sessionId}/candidates`,
      { params },
    )
    return normalizeSessionCandidates(response.data)
  },

  async removeSessionCandidate(sessionId: string, studentId: string): Promise<void> {
    await apiClient.delete<unknown>(`/official-exams/sessions/${sessionId}/candidates/${studentId}`)
  },

  async getEnrollmentEstimate(sessionId: string): Promise<OfficialExamEnrollmentEstimate> {
    const response = await apiClient.get<unknown>(
      `/official-exams/sessions/${sessionId}/enrollment-estimate`,
    )
    return normalizeEnrollmentEstimate(response.data)
  },

  async batchOpenShifts(
    sessionId: string,
    payload: BatchOpenShiftsRequest,
  ): Promise<BatchOpenShiftsResponse> {
    const response = await apiClient.post<unknown>(
      `/official-exams/sessions/${sessionId}/shifts/batch`,
      payload,
    )
    return normalizeBatchOpenShiftsResponse(response.data)
  },

  async autoAllocateStudents(
    sessionId: string,
    payload: AutoAllocateStudentsRequest,
  ): Promise<AutoAllocateStudentsResponse> {
    const response = await apiClient.post<unknown>(
      `/official-exams/sessions/${sessionId}/auto-allocate-students`,
      payload,
    )
    return normalizeAutoAllocateResponse(response.data)
  },

  async distributeShiftTickets(
    shiftId: string,
    payload: DistributeShiftTicketsRequest = { allowRandomShuffle: true },
  ): Promise<DistributeShiftTicketsResponse> {
    const response = await apiClient.post<unknown>(
      `/official-exams/shifts/${shiftId}/distribute-tickets`,
      payload,
    )
    return normalizeDistributeTicketsResponse(response.data)
  },

  async updateShiftStatus(
    shiftId: string,
    status: OfficialShiftStatusValue,
  ): Promise<ExamShift> {
    const response = await apiClient.put<unknown>(`/official-exams/shifts/${shiftId}/status`, {
      status,
    })
    return normalizeShift(unwrapPayload(response.data))
  },

  async configureFollowUp(
    sessionId: string,
    payload: ConfigureOfficialExamFollowUpRequest,
  ): Promise<OfficialExamSession> {
    const response = await apiClient.put<unknown>(
      `/official-exams/sessions/${sessionId}/follow-up-config`,
      payload,
    )
    return normalizeSession(unwrapPayload(response.data))
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

  async sealAudio(
    ticketId: string,
    payload: SealOfficialExamAudioRequest,
  ): Promise<AudioSealResponse> {
    const response = await apiClient.put<unknown>(
      `/official-exams/tickets/${ticketId}/audio`,
      payload,
    )
    return normalizeAudioSeal(response.data)
  },

  async submitExam(
    ticketId: string,
    answers: OfficialExamAnswerSubmissionRequest[],
  ): Promise<SubmitOfficialExamResponse> {
    const payload: SubmitOfficialExamRequest = { answers }
    const response = await apiClient.put<unknown>(
      `/official-exams/tickets/${ticketId}/submission`,
      payload,
    )
    return normalizeSubmit(response.data)
  },

  async getSubmissionStatus(ticketId: string): Promise<OfficialExamSubmissionStatusResponse> {
    const response = await apiClient.get<unknown>(`/official-exams/tickets/${ticketId}/submission`)
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

  async getAntiCheatLogs(shiftId: string, ticketId?: string): Promise<ExamAntiCheatLog[]> {
    const response = await apiClient.get<unknown>(
      `/official-exams/shifts/${shiftId}/anti-cheat/logs`,
      {
        params: ticketId ? { ticketId } : undefined,
      },
    )
    return asArray(response.data, 'API log chống gian lận không trả về danh sách.').map(
      normalizeAntiCheatLog,
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

  async publishGrades(
    shiftId: string,
    payload: PublishGradesRequest = {},
  ): Promise<PublishGradesResponse> {
    const response = await apiClient.post<unknown>(
      `/official-exams/shifts/${shiftId}/publish-grades`,
      payload,
    )
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

  async acknowledgeGrade(
    ticketId: string,
    studentId?: string,
  ): Promise<AcknowledgeOfficialGradeResponse> {
    const response = await apiClient.post<unknown>(
      `/student/official-exams/${ticketId}/acknowledge-grade`,
      undefined,
      {
        params: studentId ? { studentId } : undefined,
      },
    )
    return normalizeAcknowledge(response.data)
  },

  async unlockTicketDevice(ticketId: string, reason: string): Promise<UnlockTicketDeviceResponse> {
    const response = await apiClient.post<unknown>(
      `/official-exams/tickets/${ticketId}/unlock-device`,
      { reason: reason.trim() },
    )
    const record = requireRecord(
      unwrapPayload(response.data),
      'Phản hồi mở khóa thiết bị BYOD không hợp lệ.',
    )
    return {
      ticketId: requiredString(record, 'ticketId', 'Phản hồi mở khóa thiết bị thiếu mã vé.'),
      shiftId: readString(record, 'shiftId') ?? '',
      studentCode: readString(record, 'studentCode') ?? '',
      seatNumber: readNumber(record, 'seatNumber') ?? 0,
      previousDeviceId: readNullableString(record, 'previousDeviceId'),
      deviceUnlockCount: readNumber(record, 'deviceUnlockCount') ?? 1,
      unlockedBy: readString(record, 'unlockedBy') ?? '',
      unlockedAt: readString(record, 'unlockedAt') ?? new Date().toISOString(),
      reason: readString(record, 'reason') ?? reason.trim(),
    }
  },

  async getAudioPlaybackUrl(
    ticketId: string,
    submissionId?: string | null,
  ): Promise<OfficialExamAudioPlaybackUrlResponse> {
    const response = await apiClient.get<unknown>(
      `/official-exams/tickets/${encodeURIComponent(ticketId)}/audio-playback-url`,
      {
        params: submissionId ? { submissionId } : undefined,
      },
    )
    const record = requireRecord(
      unwrapPayload(response.data),
      'Phản hồi liên kết âm thanh R2 không hợp lệ.',
    )
    return {
      ticketId: requiredString(record, 'ticketId', 'Phản hồi liên kết âm thanh thiếu mã vé.'),
      submissionId: readNullableString(record, 'submissionId'),
      downloadUrl: requiredString(
        record,
        'downloadUrl',
        'Phản hồi liên kết âm thanh thiếu đường dẫn tải.',
      ),
      audioHashSha256: readNullableString(record, 'audioHashSha256'),
      expiresAt: requiredDateString(
        record,
        'expiresAt',
        'Phản hồi liên kết âm thanh thiếu thời điểm hết hạn.',
      ),
    }
  },

  async getShiftStaff(params?: {
    courseId?: string | null
    semesterId?: string | null
  }): Promise<OfficialExamShiftStaffResponse> {
    const response = await apiClient.get<unknown>('/official-exams/shift-staff', {
      params: {
        courseId: params?.courseId || undefined,
        semesterId: params?.semesterId || undefined,
      },
    })
    const record = requireRecord(
      unwrapPayload(response.data),
      'Phản hồi danh sách nhân sự ca thi không hợp lệ.',
    )
    const parseStaffList = (raw: unknown): OfficialExamShiftStaffMember[] => {
      if (!Array.isArray(raw)) return []
      const result: OfficialExamShiftStaffMember[] = []
      for (const item of raw) {
        if (!isRecord(item)) continue
        const userId = readString(item, 'userId')
        const fullName = readString(item, 'fullName')
        if (!userId || !fullName) continue
        result.push({
          userId,
          fullName,
          email: readNullableString(item, 'email'),
          username: readNullableString(item, 'username'),
          role: readString(item, 'role') ?? '',
          detail: readNullableString(item, 'detail'),
          isCourseAssigned: readBoolean(item, 'isCourseAssigned') ?? false,
        })
      }
      return result
    }
    return {
      proctors: parseStaffList(field(record, 'proctors')),
      examiners: parseStaffList(field(record, 'examiners')),
    }
  },
}

export type OfficialExamService = typeof officialExamService
