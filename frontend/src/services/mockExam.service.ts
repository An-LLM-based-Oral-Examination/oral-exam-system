import apiClient from './api.client'
import type {
  MockExamCriterionScore,
  MockExamHistoryItem,
  MockExamHistoryPage,
  MockExamQuestion,
  MockExamQuestionScore,
  MockExamQuota,
  MockExamRubricCriterion,
  MockExamScorecard,
  StartMockExamResponse,
  SubmitMockExamAnswerRequest,
  SubmitMockExamAnswerResponse,
} from '@/types/mockExam.types'
import {
  TEMPORARY_QUESTION_IMAGE_ALT,
  TEMPORARY_QUESTION_IMAGE_URL,
} from '@/components/question/temporaryQuestionImage'

type JsonRecord = Record<string, unknown>

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function field(record: JsonRecord, key: string): unknown {
  if (key in record) return record[key]
  const pascalCaseKey = `${key.charAt(0).toUpperCase()}${key.slice(1)}`
  return record[pascalCaseKey]
}

function unwrapPayload(payload: unknown): unknown {
  if (!isRecord(payload)) return payload
  for (const key of ['value', 'Value', 'data', 'Data']) {
    if (key in payload && payload[key] !== undefined) return payload[key]
  }
  return payload
}

function invalidApiResponse(detail: string): never {
  throw { status: 502, title: 'Phản hồi API không hợp lệ', detail }
}

function requireRecord(value: unknown, detail: string): JsonRecord {
  if (!isRecord(value)) return invalidApiResponse(detail)
  return value
}

function requireString(record: JsonRecord, key: string, detail: string): string {
  const value = field(record, key)
  if (typeof value !== 'string' || !value.trim()) return invalidApiResponse(detail)
  return value
}

function requireGuid(record: JsonRecord, key: string, detail: string): string {
  const value = requireString(record, key, detail)
  if (!UUID_PATTERN.test(value)) return invalidApiResponse(detail)
  return value
}

function requireBoolean(record: JsonRecord, key: string, detail: string): boolean {
  const value = field(record, key)
  if (typeof value !== 'boolean') return invalidApiResponse(detail)
  return value
}

function requireInteger(record: JsonRecord, key: string, detail: string): number {
  const value = readDecimal(record, key)
  if (value === null || !Number.isInteger(value)) return invalidApiResponse(detail)
  return value
}

function requireDecimal(record: JsonRecord, key: string, detail: string): number {
  const value = readDecimal(record, key)
  if (value === null) return invalidApiResponse(detail)
  return value
}

function readDecimal(record: JsonRecord, key: string): number | null {
  const value = field(record, key)
  if (typeof value === 'number' && Number.isFinite(value)) return value
  if (typeof value === 'string' && value.trim() && Number.isFinite(Number(value)))
    return Number(value)
  return null
}

function readOptionalDecimal(record: JsonRecord, key: string): number | null {
  const value = field(record, key)
  if (value === null || value === undefined) return null
  return readDecimal(record, key)
}

function readOptionalString(record: JsonRecord, key: string): string | null {
  const value = field(record, key)
  if (value === null || value === undefined) return null
  return typeof value === 'string' ? value : null
}

function requireDateString(record: JsonRecord, key: string, detail: string): string {
  const value = requireString(record, key, detail)
  if (Number.isNaN(Date.parse(value))) return invalidApiResponse(detail)
  return value
}

function readArray(record: JsonRecord, key: string, detail: string): unknown[] {
  const value = field(record, key)
  if (value === null || value === undefined) return []
  if (!Array.isArray(value)) return invalidApiResponse(detail)
  return value
}

function audioExtension(mimeType: string) {
  if (mimeType.includes('ogg')) return 'ogg'
  if (mimeType.includes('mp4')) return 'm4a'
  if (mimeType.includes('wav')) return 'wav'
  return 'webm'
}

function normalizeCriterion(value: unknown): MockExamRubricCriterion {
  const record = requireRecord(value, 'Tiêu chí chấm của đề thi thử không hợp lệ.')
  return {
    name: requireString(record, 'name', 'Tiêu chí chấm thiếu tên.'),
    maxScore: requireDecimal(record, 'maxScore', 'Tiêu chí chấm thiếu điểm tối đa.'),
  }
}

function normalizeQuestion(value: unknown): MockExamQuestion {
  const record = requireRecord(value, 'Một câu hỏi trong đề thi thử không hợp lệ.')
  return {
    id: requireGuid(record, 'id', 'Câu hỏi thi thử thiếu mã định danh.'),
    orderIndex: requireInteger(record, 'orderIndex', 'Câu hỏi thi thử thiếu thứ tự.'),
    title: requireString(record, 'title', 'Câu hỏi thi thử thiếu tiêu đề.'),
    content: requireString(record, 'content', 'Câu hỏi thi thử thiếu nội dung.'),
    imageUrl: readOptionalString(record, 'imageUrl'),
    imageAlt: readOptionalString(record, 'imageAlt'),
    bloomLevel: requireString(record, 'bloomLevel', 'Câu hỏi thi thử thiếu mức Bloom.'),
    difficulty: requireString(record, 'difficulty', 'Câu hỏi thi thử thiếu độ khó.'),
    rubricCriteria: readArray(
      record,
      'rubricCriteria',
      'Danh sách tiêu chí chấm không hợp lệ.',
    ).map(normalizeCriterion),
  }
}

function normalizeQuota(value: unknown): MockExamQuota {
  const record = requireRecord(value, 'Hạn ngạch thi thử không hợp lệ.')
  return {
    courseId: requireGuid(record, 'courseId', 'Hạn ngạch thiếu mã môn học.'),
    studentId: requireGuid(record, 'studentId', 'Hạn ngạch thiếu mã sinh viên.'),
    quotaDate: requireString(record, 'quotaDate', 'Hạn ngạch thiếu ngày áp dụng.'),
    usedCount: requireInteger(record, 'usedCount', 'Hạn ngạch thiếu số lượt đã dùng.'),
    remainingCount: requireInteger(record, 'remainingCount', 'Hạn ngạch thiếu số lượt còn lại.'),
    maxDailyQuota: requireInteger(record, 'maxDailyQuota', 'Hạn ngạch thiếu giới hạn trong ngày.'),
    canStartExam: requireBoolean(record, 'canStartExam', 'Hạn ngạch thiếu quyền bắt đầu ca thi.'),
  }
}

function normalizeStart(value: unknown): StartMockExamResponse {
  const record = requireRecord(value, 'Phản hồi tạo ca thi thử không hợp lệ.')
  const questions = readArray(record, 'questions', 'Đề thi thử không có danh sách câu hỏi.')
    .map(normalizeQuestion)
    .sort((left, right) => left.orderIndex - right.orderIndex)

  return {
    sessionId: requireGuid(record, 'sessionId', 'Ca thi thử thiếu mã phiên.'),
    courseId: requireGuid(record, 'courseId', 'Ca thi thử thiếu mã môn học.'),
    courseCode: requireString(record, 'courseCode', 'Ca thi thử thiếu mã môn.'),
    courseName: requireString(record, 'courseName', 'Ca thi thử thiếu tên môn.'),
    hasFollowUp: requireBoolean(record, 'hasFollowUp', 'Ca thi thử thiếu tùy chọn câu hỏi phụ.'),
    durationMinutes: requireInteger(record, 'durationMinutes', 'Ca thi thử thiếu thời lượng.'),
    startedAt: requireDateString(record, 'startedAt', 'Ca thi thử thiếu thời điểm bắt đầu.'),
    expiresAt: requireDateString(record, 'expiresAt', 'Ca thi thử thiếu thời điểm hết giờ.'),
    usedQuota: requireInteger(record, 'usedQuota', 'Ca thi thử thiếu số lượt đã dùng.'),
    remainingQuota: requireInteger(record, 'remainingQuota', 'Ca thi thử thiếu số lượt còn lại.'),
    maxDailyQuota: requireInteger(record, 'maxDailyQuota', 'Ca thi thử thiếu hạn ngạch tối đa.'),
    questions: questions.map((question, index) =>
      index === 0
        ? {
            ...question,
            imageUrl: question.imageUrl?.trim() || TEMPORARY_QUESTION_IMAGE_URL,
            imageAlt: question.imageAlt?.trim() || TEMPORARY_QUESTION_IMAGE_ALT,
          }
        : question,
    ),
  }
}

function normalizeAnswer(value: unknown): SubmitMockExamAnswerResponse {
  const record = requireRecord(value, 'Phản hồi nộp câu trả lời không hợp lệ.')
  return {
    answerId: requireGuid(record, 'answerId', 'Câu trả lời thiếu mã bản ghi.'),
    questionId: requireGuid(record, 'questionId', 'Câu trả lời thiếu mã câu hỏi.'),
    transcript: readOptionalString(record, 'transcript') ?? '',
    status: requireString(record, 'status', 'Câu trả lời thiếu trạng thái.'),
    answeredAt: requireDateString(record, 'answeredAt', 'Câu trả lời thiếu thời điểm nộp.'),
  }
}

function normalizeCriterionScore(value: unknown): MockExamCriterionScore {
  const record = requireRecord(value, 'Điểm tiêu chí không hợp lệ.')
  return {
    criterionId: requireGuid(record, 'criterionId', 'Tiêu chí thiếu mã định danh.'),
    criterionName: requireString(record, 'criterionName', 'Tiêu chí thiếu tên.'),
    bloomLevel: readOptionalString(record, 'bloomLevel'),
    maxScore: requireDecimal(record, 'maxScore', 'Tiêu chí thiếu điểm tối đa.'),
    score: requireDecimal(record, 'score', 'Tiêu chí thiếu điểm đạt được.'),
    comment: readOptionalString(record, 'comment'),
  }
}

function normalizeQuestionScore(value: unknown): MockExamQuestionScore {
  const record = requireRecord(value, 'Điểm một câu hỏi không hợp lệ.')
  return {
    questionId: requireGuid(record, 'questionId', 'Điểm câu hỏi thiếu mã câu hỏi.'),
    orderIndex: requireInteger(record, 'orderIndex', 'Điểm câu hỏi thiếu thứ tự.'),
    title: requireString(record, 'title', 'Điểm câu hỏi thiếu tiêu đề.'),
    content: requireString(record, 'content', 'Điểm câu hỏi thiếu nội dung đề.'),
    imageUrl: readOptionalString(record, 'imageUrl'),
    imageAlt: readOptionalString(record, 'imageAlt'),
    bloomLevel: requireString(record, 'bloomLevel', 'Điểm câu hỏi thiếu mức Bloom.'),
    difficulty: requireString(record, 'difficulty', 'Điểm câu hỏi thiếu độ khó.'),
    transcript: readOptionalString(record, 'transcript'),
    score: requireDecimal(record, 'score', 'Điểm câu hỏi thiếu điểm.'),
    feedback: readOptionalString(record, 'feedback'),
    confidenceScore: readOptionalDecimal(record, 'confidenceScore'),
    criteriaScores: readArray(
      record,
      'criteriaScores',
      'Danh sách điểm tiêu chí không hợp lệ.',
    ).map(normalizeCriterionScore),
  }
}

function normalizeScorecard(value: unknown): MockExamScorecard {
  const record = requireRecord(value, 'Bảng điểm thi thử không hợp lệ.')
  const questions = readArray(record, 'questions', 'Bảng điểm thiếu danh sách câu hỏi.')
    .map(normalizeQuestionScore)
    .sort((left, right) => left.orderIndex - right.orderIndex)

  return {
    sessionId: requireGuid(record, 'sessionId', 'Bảng điểm thiếu mã ca thi.'),
    courseId: requireGuid(record, 'courseId', 'Bảng điểm thiếu mã môn học.'),
    courseCode: requireString(record, 'courseCode', 'Bảng điểm thiếu mã môn.'),
    courseName: requireString(record, 'courseName', 'Bảng điểm thiếu tên môn.'),
    totalScore: requireDecimal(record, 'totalScore', 'Bảng điểm thiếu điểm tổng.'),
    classification: requireString(record, 'classification', 'Bảng điểm thiếu xếp loại.'),
    totalQuestions: requireInteger(record, 'totalQuestions', 'Bảng điểm thiếu tổng số câu.'),
    answeredQuestions: requireInteger(
      record,
      'answeredQuestions',
      'Bảng điểm thiếu số câu đã trả lời.',
    ),
    durationMinutes: requireInteger(record, 'durationMinutes', 'Bảng điểm thiếu thời lượng.'),
    isOverdue: requireBoolean(record, 'isOverdue', 'Bảng điểm thiếu cờ nộp quá hạn.'),
    startedAt: requireDateString(record, 'startedAt', 'Bảng điểm thiếu thời điểm bắt đầu.'),
    submittedAt: requireDateString(record, 'submittedAt', 'Bảng điểm thiếu thời điểm nộp.'),
    questions: questions.map((question, index) =>
      index === 0
        ? {
            ...question,
            imageUrl: question.imageUrl?.trim() || TEMPORARY_QUESTION_IMAGE_URL,
            imageAlt: question.imageAlt?.trim() || TEMPORARY_QUESTION_IMAGE_ALT,
          }
        : question,
    ),
  }
}

function normalizeHistoryItem(value: unknown): MockExamHistoryItem {
  const record = requireRecord(value, 'Một mục lịch sử thi thử không hợp lệ.')
  return {
    sessionId: requireGuid(record, 'sessionId', 'Lịch sử thiếu mã ca thi.'),
    courseId: requireGuid(record, 'courseId', 'Lịch sử thiếu mã môn học.'),
    courseCode: requireString(record, 'courseCode', 'Lịch sử thiếu mã môn.'),
    courseName: requireString(record, 'courseName', 'Lịch sử thiếu tên môn.'),
    startedAt: requireDateString(record, 'startedAt', 'Lịch sử thiếu thời điểm bắt đầu.'),
    durationMinutes: requireInteger(record, 'durationMinutes', 'Lịch sử thiếu thời lượng.'),
    totalScore: readOptionalDecimal(record, 'totalScore'),
    status: requireString(record, 'status', 'Lịch sử thiếu trạng thái.'),
    answeredQuestions: requireInteger(
      record,
      'answeredQuestions',
      'Lịch sử thiếu số câu đã trả lời.',
    ),
    totalQuestions: requireInteger(record, 'totalQuestions', 'Lịch sử thiếu tổng số câu.'),
  }
}

function normalizeHistory(value: unknown): MockExamHistoryPage {
  const record = requireRecord(value, 'Danh sách lịch sử thi thử không hợp lệ.')
  return {
    items: readArray(record, 'items', 'Lịch sử thi thử thiếu danh sách.').map(normalizeHistoryItem),
    pageIndex: requireInteger(record, 'pageIndex', 'Lịch sử thiếu trang hiện tại.'),
    pageSize: requireInteger(record, 'pageSize', 'Lịch sử thiếu kích thước trang.'),
    totalCount: requireInteger(record, 'totalCount', 'Lịch sử thiếu tổng số bản ghi.'),
    totalPages: requireInteger(record, 'totalPages', 'Lịch sử thiếu tổng số trang.'),
  }
}

export function getMockExamErrorStatus(error: unknown) {
  if (!isRecord(error)) return 500
  const status = error.status ?? error.Status
  return typeof status === 'number' && Number.isFinite(status) ? status : 500
}

export function getMockExamErrorDetail(error: unknown, fallback: string) {
  if (!isRecord(error)) return fallback
  const detail = error.detail ?? error.Detail ?? error.error ?? error.message ?? error.Message
  return typeof detail === 'string' && detail.trim() ? detail : fallback
}

export const mockExamService = {
  async getQuota(courseId: string): Promise<MockExamQuota> {
    const response = await apiClient.get<unknown>('/mock-exams/quota', { params: { courseId } })
    return normalizeQuota(unwrapPayload(response.data))
  },

  async startSession(courseId: string, hasFollowUp: boolean): Promise<StartMockExamResponse> {
    const response = await apiClient.post<unknown>('/mock-exams/sessions', {
      courseId,
      hasFollowUp,
    })
    return normalizeStart(unwrapPayload(response.data))
  },

  async submitAnswer(
    sessionId: string,
    payload: SubmitMockExamAnswerRequest,
  ): Promise<SubmitMockExamAnswerResponse> {
    const transcript = payload.transcript?.trim()
    if (!payload.file && !transcript) {
      throw {
        status: 400,
        title: 'Thiếu câu trả lời',
        detail: 'Hãy ghi âm hoặc nhập transcript trước khi gửi.',
      }
    }

    const formData = new FormData()
    formData.append('QuestionId', payload.questionId)
    formData.append('TimeTakenSeconds', String(Math.max(0, Math.round(payload.timeTakenSeconds))))
    if (payload.file) {
      const extension = audioExtension(payload.file.type)
      formData.append(
        'File',
        payload.file,
        payload.fileName ?? `mock-answer-${payload.questionId}.${extension}`,
      )
    }
    if (transcript && !payload.file) formData.append('Transcript', transcript)

    const response = await apiClient.post<unknown>(
      `/mock-exams/sessions/${sessionId}/answers`,
      formData,
      {
        headers: { 'Content-Type': 'multipart/form-data' },
        timeout: 120_000,
      },
    )
    return normalizeAnswer(unwrapPayload(response.data))
  },

  async submitSession(sessionId: string): Promise<MockExamScorecard> {
    const response = await apiClient.post<unknown>(
      `/mock-exams/sessions/${sessionId}/submit`,
      {},
      { timeout: 120_000 },
    )
    return normalizeScorecard(unwrapPayload(response.data))
  },

  async getHistory(params: {
    courseId?: string
    status?: string
    pageIndex?: number
    pageSize?: number
  }): Promise<MockExamHistoryPage> {
    const response = await apiClient.get<unknown>('/mock-exams/history', {
      params: {
        pageIndex: params.pageIndex ?? 1,
        pageSize: params.pageSize ?? 5,
        ...(params.courseId ? { courseId: params.courseId } : {}),
        ...(params.status ? { status: params.status } : {}),
      },
    })
    return normalizeHistory(unwrapPayload(response.data))
  },

  async getHistoryDetail(sessionId: string): Promise<MockExamScorecard> {
    const response = await apiClient.get<unknown>(`/mock-exams/history/${sessionId}`)
    return normalizeScorecard(unwrapPayload(response.data))
  },
}
