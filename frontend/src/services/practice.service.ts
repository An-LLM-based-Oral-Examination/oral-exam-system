import apiClient from './api.client'
import type {
  ApiPracticeMode,
  CompletePracticeSessionResponse,
  NextPracticeQuestion,
  NextPracticeQuestionResponse,
  PracticeAnswerDetail,
  PracticeAnswerStatus,
  PracticeDifficulty,
  PracticeEvaluationDetail,
  PracticeHistoryItem,
  PracticeQuestion,
  PracticeSessionDetail,
  PracticeSessionStatus,
  StartPracticeSessionRequest,
  StartPracticeSessionResponse,
  SubmitPracticeAnswerRequest,
  SubmitPracticeAnswerResponse,
  SubmitPracticeBatchRequest,
} from '@/types/practice.types'

type JsonRecord = Record<string, unknown>

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
const API_PRACTICE_MODES = new Set<ApiPracticeMode>(['per_question', 'full_session'])
const SESSION_STATUSES = new Set<PracticeSessionStatus>(['in_progress', 'completed', 'abandoned'])
const ANSWER_STATUSES = new Set<PracticeAnswerStatus>(['pending', 'grading', 'graded', 'failed'])
const DIFFICULTIES = new Set<PracticeDifficulty>(['easy', 'medium', 'hard'])

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
    if (key in payload) return payload[key]
  }

  return payload
}

function invalidApiResponse(detail: string): never {
  throw {
    status: 502,
    title: 'Phản hồi API không hợp lệ',
    detail,
  }
}

function rethrowApiError(error: unknown, fallbackDetail: string): never {
  if (isRecord(error)) {
    const rawStatus = error.status ?? error.Status
    const status = typeof rawStatus === 'number' && Number.isFinite(rawStatus) ? rawStatus : 500
    const rawTitle = error.title ?? error.Title
    const rawDetail = error.error ?? error.detail ?? error.Detail ?? error.message ?? error.Message

    throw {
      status,
      title: typeof rawTitle === 'string' && rawTitle.trim() ? rawTitle : 'Không thể xử lý yêu cầu',
      detail: typeof rawDetail === 'string' && rawDetail.trim() ? rawDetail : fallbackDetail,
    }
  }

  throw {
    status: 500,
    title: 'Không thể xử lý yêu cầu',
    detail: fallbackDetail,
  }
}

function requireRecord(value: unknown, detail: string): JsonRecord {
  if (!isRecord(value)) return invalidApiResponse(detail)
  return value
}

function requireString(record: JsonRecord, key: string, detail: string): string {
  const value = field(record, key)
  if (typeof value !== 'string') return invalidApiResponse(detail)
  return value
}

function requireNonEmptyString(record: JsonRecord, key: string, detail: string): string {
  const value = requireString(record, key, detail)
  if (!value.trim()) return invalidApiResponse(detail)
  return value
}

function requireGuid(record: JsonRecord, key: string, detail: string): string {
  const value = requireNonEmptyString(record, key, detail)
  if (!UUID_PATTERN.test(value)) return invalidApiResponse(detail)
  return value
}

function requireNumber(record: JsonRecord, key: string, detail: string): number {
  const value = field(record, key)
  if (typeof value !== 'number' || !Number.isFinite(value)) return invalidApiResponse(detail)
  return value
}

function requireInteger(record: JsonRecord, key: string, detail: string): number {
  const value = requireNumber(record, key, detail)
  if (!Number.isInteger(value)) return invalidApiResponse(detail)
  return value
}

function requireBoolean(record: JsonRecord, key: string, detail: string): boolean {
  const value = field(record, key)
  if (typeof value !== 'boolean') return invalidApiResponse(detail)
  return value
}

function requireDateString(record: JsonRecord, key: string, detail: string): string {
  const value = requireNonEmptyString(record, key, detail)
  if (Number.isNaN(Date.parse(value))) return invalidApiResponse(detail)
  return value
}

function nullableString(record: JsonRecord, key: string, detail: string): string | null {
  const value = field(record, key)
  if (value === null || value === undefined) return null
  if (typeof value !== 'string') return invalidApiResponse(detail)
  return value
}

function nullableStringFromKeys(
  record: JsonRecord,
  keys: readonly string[],
  detail: string,
): string | null {
  for (const key of keys) {
    const value = field(record, key)
    if (value === null || value === undefined) continue
    if (typeof value !== 'string') return invalidApiResponse(detail)
    return value
  }

  return null
}

function nullableGuid(record: JsonRecord, key: string, detail: string): string | null {
  const value = nullableString(record, key, detail)
  if (value !== null && !UUID_PATTERN.test(value)) return invalidApiResponse(detail)
  return value
}

function nullableNumber(record: JsonRecord, key: string, detail: string): number | null {
  const value = field(record, key)
  if (value === null || value === undefined) return null
  if (typeof value !== 'number' || !Number.isFinite(value)) return invalidApiResponse(detail)
  return value
}

function nullableBoolean(record: JsonRecord, key: string, detail: string): boolean | null {
  const value = field(record, key)
  if (value === null || value === undefined) return null
  if (typeof value !== 'boolean') return invalidApiResponse(detail)
  return value
}

function nullableDateString(record: JsonRecord, key: string, detail: string): string | null {
  const value = nullableString(record, key, detail)
  if (value !== null && Number.isNaN(Date.parse(value))) return invalidApiResponse(detail)
  return value
}

function stringArray(record: JsonRecord, key: string, detail: string): string[] {
  const value = field(record, key)
  if (!Array.isArray(value) || !value.every((item) => typeof item === 'string')) {
    return invalidApiResponse(detail)
  }
  return value
}

function assertGuid(value: string, fieldName: string): void {
  if (!UUID_PATTERN.test(value)) {
    throw {
      status: 400,
      title: 'Dữ liệu đầu vào không hợp lệ',
      detail: `${fieldName} không phải mã định danh UUID hợp lệ.`,
    }
  }
}

function assertAnswerText(value: string): void {
  if (value.trim().length < 10) {
    throw {
      status: 400,
      title: 'Dữ liệu đầu vào không hợp lệ',
      detail: 'Bản transcript câu trả lời phải có ít nhất 10 ký tự.',
    }
  }
}

function normalizeQuestion(payload: unknown): PracticeQuestion {
  const candidate = requireRecord(payload, 'API không trả về câu hỏi luyện tập dạng JSON hợp lệ.')
  return {
    questionId: requireGuid(candidate, 'questionId', 'Câu hỏi luyện tập thiếu questionId hợp lệ.'),
    content: requireNonEmptyString(candidate, 'content', 'Câu hỏi luyện tập thiếu nội dung.'),
    imageUrl: nullableString(candidate, 'imageUrl', 'URL hình ảnh câu hỏi không hợp lệ.'),
    imageAlt: nullableStringFromKeys(
      candidate,
      ['imageAlt', 'imageDescription'],
      'Mô tả hình ảnh câu hỏi không hợp lệ.',
    ),
    rubricCriteria: stringArray(
      candidate,
      'rubricCriteria',
      'Câu hỏi luyện tập thiếu danh sách tiêu chí rubric.',
    ),
  }
}

function normalizeNextQuestion(payload: unknown): NextPracticeQuestion {
  const candidate = requireRecord(payload, 'API không trả về câu hỏi tiếp theo dạng JSON hợp lệ.')
  const difficulty = requireNonEmptyString(
    candidate,
    'difficulty',
    'Câu hỏi tiếp theo thiếu mức độ.',
  ).toLowerCase()

  if (!DIFFICULTIES.has(difficulty as PracticeDifficulty)) {
    return invalidApiResponse('Câu hỏi tiếp theo có mức độ không hợp lệ.')
  }

  const questionOrder = requireInteger(
    candidate,
    'questionOrder',
    'Câu hỏi tiếp theo thiếu số thứ tự hợp lệ.',
  )
  if (questionOrder < 1) return invalidApiResponse('Số thứ tự câu hỏi phải lớn hơn 0.')

  return {
    id: requireGuid(candidate, 'id', 'Câu hỏi tiếp theo thiếu id hợp lệ.'),
    content: requireNonEmptyString(candidate, 'content', 'Câu hỏi tiếp theo thiếu nội dung.'),
    imageUrl: nullableString(candidate, 'imageUrl', 'URL hình ảnh câu hỏi tiếp theo không hợp lệ.'),
    imageAlt: nullableStringFromKeys(
      candidate,
      ['imageAlt', 'imageDescription'],
      'Mô tả hình ảnh câu hỏi tiếp theo không hợp lệ.',
    ),
    difficulty: difficulty as PracticeDifficulty,
    questionOrder,
    rubricCriteria: stringArray(
      candidate,
      'rubricCriteria',
      'Câu hỏi tiếp theo thiếu danh sách tiêu chí rubric.',
    ),
  }
}

function normalizeEvaluationDetail(payload: unknown): PracticeEvaluationDetail {
  const candidate = requireRecord(payload, 'Chi tiết điểm rubric không phải JSON hợp lệ.')
  return {
    criterionId: requireGuid(candidate, 'criterionId', 'Chi tiết điểm thiếu criterionId hợp lệ.'),
    criterionName: requireString(candidate, 'criterionName', 'Chi tiết điểm thiếu tên tiêu chí.'),
    score: requireNumber(candidate, 'score', 'Chi tiết điểm thiếu số điểm hợp lệ.'),
    maxScore:
      nullableNumber(candidate, 'maxScore', 'Điểm tối đa của tiêu chí không hợp lệ.') ?? undefined,
    comment: nullableString(candidate, 'comment', 'Nhận xét tiêu chí không hợp lệ.'),
  }
}

export function normalizePracticeAnswerDetail(payload: unknown): PracticeAnswerDetail {
  const candidate = requireRecord(payload, 'Kết quả chấm luyện tập không phải JSON hợp lệ.')
  const status = requireNonEmptyString(
    candidate,
    'status',
    'Kết quả chấm thiếu trạng thái.',
  ).toLowerCase()
  if (!ANSWER_STATUSES.has(status as PracticeAnswerStatus)) {
    return invalidApiResponse('Kết quả chấm có trạng thái không hợp lệ.')
  }

  const rawCriteriaScores = field(candidate, 'criteriaScores')
  if (!Array.isArray(rawCriteriaScores)) {
    return invalidApiResponse('Kết quả chấm thiếu danh sách điểm rubric.')
  }

  const rawNeedsFollowUp = field(candidate, 'needsFollowUp')
  const needsFollowUp =
    rawNeedsFollowUp === undefined
      ? false
      : requireBoolean(candidate, 'needsFollowUp', 'Cờ yêu cầu câu hỏi phụ không hợp lệ.')

  return {
    answerId: requireGuid(candidate, 'answerId', 'Kết quả chấm thiếu answerId hợp lệ.'),
    questionId: requireGuid(candidate, 'questionId', 'Kết quả chấm thiếu questionId hợp lệ.'),
    answerText: requireString(candidate, 'answerText', 'Kết quả chấm thiếu nội dung trả lời.'),
    isFollowUp: requireBoolean(candidate, 'isFollowUp', 'Cờ câu hỏi phụ không hợp lệ.'),
    parentAnswerId: nullableGuid(candidate, 'parentAnswerId', 'Mã câu trả lời cha không hợp lệ.'),
    promptText: nullableString(candidate, 'promptText', 'Nội dung câu hỏi phụ không hợp lệ.'),
    status: status as PracticeAnswerStatus,
    submittedAt: requireDateString(candidate, 'submittedAt', 'Thời gian nộp bài không hợp lệ.'),
    totalScore: nullableNumber(candidate, 'totalScore', 'Tổng điểm không hợp lệ.'),
    feedback: nullableString(candidate, 'feedback', 'Nhận xét tổng thể không hợp lệ.'),
    confidenceScore: nullableNumber(candidate, 'confidenceScore', 'Điểm tin cậy không hợp lệ.'),
    isSuspicious: nullableBoolean(candidate, 'isSuspicious', 'Cờ nghi ngờ không hợp lệ.'),
    needsFollowUp,
    followUpPrompt: nullableString(
      candidate,
      'followUpPrompt',
      'Nội dung câu hỏi phụ không hợp lệ.',
    ),
    criteriaScores: rawCriteriaScores.map(normalizeEvaluationDetail),
  }
}

function normalizeStartSession(payload: unknown): StartPracticeSessionResponse {
  const candidate = requireRecord(
    unwrapPayload(payload),
    'API không trả về thông tin phiên luyện tập hợp lệ.',
  )
  const rawQuestions = field(candidate, 'questions')
  if (!Array.isArray(rawQuestions)) {
    return invalidApiResponse('Phiên luyện tập thiếu danh sách câu hỏi.')
  }

  const transcriptBufferSeconds = requireInteger(
    candidate,
    'transcriptBufferSeconds',
    'Phiên luyện tập thiếu thời gian hiệu đính transcript.',
  )
  if (transcriptBufferSeconds < 1) {
    return invalidApiResponse('Thời gian hiệu đính transcript phải lớn hơn 0.')
  }

  return {
    sessionId: requireGuid(candidate, 'sessionId', 'Phiên luyện tập thiếu sessionId hợp lệ.'),
    transcriptBufferSeconds,
    questions: rawQuestions.map(normalizeQuestion),
  }
}

function normalizeNextQuestionResponse(payload: unknown): NextPracticeQuestionResponse {
  const candidate = requireRecord(
    unwrapPayload(payload),
    'API không trả về kết quả bốc câu hỏi tiếp theo hợp lệ.',
  )
  const hasMoreQuestions = requireBoolean(
    candidate,
    'hasMoreQuestions',
    'Phản hồi thiếu cờ hasMoreQuestions.',
  )
  const rawQuestion = field(candidate, 'question')
  const question =
    rawQuestion === null || rawQuestion === undefined ? null : normalizeNextQuestion(rawQuestion)

  if (hasMoreQuestions && question === null) {
    return invalidApiResponse('API báo còn câu hỏi nhưng không trả về nội dung câu hỏi tiếp theo.')
  }

  return {
    hasMoreQuestions,
    message: nullableString(candidate, 'message', 'Thông báo câu hỏi tiếp theo không hợp lệ.'),
    question,
  }
}

function normalizeSubmitAnswer(payload: unknown): SubmitPracticeAnswerResponse {
  const candidate = requireRecord(
    unwrapPayload(payload),
    'API không trả về xác nhận tiếp nhận câu trả lời hợp lệ.',
  )
  return {
    answerId: requireGuid(candidate, 'answerId', 'Phản hồi thiếu answerId hợp lệ.'),
  }
}

function normalizeSessionDetail(payload: unknown): PracticeSessionDetail {
  const candidate = requireRecord(
    unwrapPayload(payload),
    'API không trả về chi tiết phiên luyện tập hợp lệ.',
  )
  const practiceMode = requireNonEmptyString(
    candidate,
    'practiceMode',
    'Chi tiết phiên thiếu chế độ luyện tập.',
  )
  const status = requireNonEmptyString(candidate, 'status', 'Chi tiết phiên thiếu trạng thái.')
  if (!API_PRACTICE_MODES.has(practiceMode as ApiPracticeMode)) {
    return invalidApiResponse('Chế độ luyện tập trong phản hồi không hợp lệ.')
  }
  if (!SESSION_STATUSES.has(status as PracticeSessionStatus)) {
    return invalidApiResponse('Trạng thái phiên luyện tập trong phản hồi không hợp lệ.')
  }

  const rawQuestions = field(candidate, 'questions')
  const rawAnswers = field(candidate, 'answers')
  if (!Array.isArray(rawQuestions) || !Array.isArray(rawAnswers)) {
    return invalidApiResponse('Chi tiết phiên thiếu danh sách câu hỏi hoặc câu trả lời.')
  }

  const transcriptBufferSeconds = requireInteger(
    candidate,
    'transcriptBufferSeconds',
    'Chi tiết phiên thiếu thời gian hiệu đính transcript.',
  )

  return {
    sessionId: requireGuid(candidate, 'sessionId', 'Chi tiết phiên thiếu sessionId hợp lệ.'),
    courseId: requireGuid(candidate, 'courseId', 'Chi tiết phiên thiếu courseId hợp lệ.'),
    courseCode: requireString(candidate, 'courseCode', 'Chi tiết phiên thiếu mã môn học.'),
    courseName: requireString(candidate, 'courseName', 'Chi tiết phiên thiếu tên môn học.'),
    studentId: requireGuid(candidate, 'studentId', 'Chi tiết phiên thiếu studentId hợp lệ.'),
    studentName: requireString(candidate, 'studentName', 'Chi tiết phiên thiếu tên sinh viên.'),
    practiceMode: practiceMode as ApiPracticeMode,
    status: status as PracticeSessionStatus,
    transcriptBufferSeconds,
    startedAt: requireDateString(candidate, 'startedAt', 'Thời gian bắt đầu phiên không hợp lệ.'),
    endedAt: nullableDateString(candidate, 'endedAt', 'Thời gian kết thúc phiên không hợp lệ.'),
    questions: rawQuestions.map(normalizeQuestion),
    answers: rawAnswers.map(normalizePracticeAnswerDetail),
  }
}

function normalizeHistoryItem(payload: unknown): PracticeHistoryItem {
  const candidate = requireRecord(payload, 'Một mục lịch sử luyện tập không phải JSON hợp lệ.')
  const practiceMode = requireNonEmptyString(
    candidate,
    'practiceMode',
    'Lịch sử luyện tập thiếu chế độ.',
  )
  const status = requireNonEmptyString(candidate, 'status', 'Lịch sử luyện tập thiếu trạng thái.')
  if (!API_PRACTICE_MODES.has(practiceMode as ApiPracticeMode)) {
    return invalidApiResponse('Lịch sử luyện tập có chế độ không hợp lệ.')
  }
  if (!SESSION_STATUSES.has(status as PracticeSessionStatus)) {
    return invalidApiResponse('Lịch sử luyện tập có trạng thái không hợp lệ.')
  }

  const totalQuestions = requireInteger(
    candidate,
    'totalQuestions',
    'Lịch sử luyện tập thiếu tổng số câu hỏi.',
  )
  const answeredQuestions = requireInteger(
    candidate,
    'answeredQuestions',
    'Lịch sử luyện tập thiếu số câu đã trả lời.',
  )
  if (totalQuestions < 0 || answeredQuestions < 0) {
    return invalidApiResponse('Số lượng câu hỏi trong lịch sử không được âm.')
  }

  return {
    sessionId: requireGuid(candidate, 'sessionId', 'Lịch sử luyện tập thiếu sessionId hợp lệ.'),
    courseId: requireGuid(candidate, 'courseId', 'Lịch sử luyện tập thiếu courseId hợp lệ.'),
    courseCode: requireString(candidate, 'courseCode', 'Lịch sử luyện tập thiếu mã môn học.'),
    courseName: requireString(candidate, 'courseName', 'Lịch sử luyện tập thiếu tên môn học.'),
    practiceMode: practiceMode as ApiPracticeMode,
    status: status as PracticeSessionStatus,
    startedAt: requireDateString(
      candidate,
      'startedAt',
      'Thời gian bắt đầu trong lịch sử không hợp lệ.',
    ),
    endedAt: nullableDateString(
      candidate,
      'endedAt',
      'Thời gian kết thúc trong lịch sử không hợp lệ.',
    ),
    totalQuestions,
    answeredQuestions,
    averageScore: nullableNumber(candidate, 'averageScore', 'Điểm trung bình không hợp lệ.'),
  }
}

function normalizeHistory(payload: unknown): PracticeHistoryItem[] {
  const candidate = unwrapPayload(payload)
  if (!Array.isArray(candidate)) {
    return invalidApiResponse('API không trả về danh sách lịch sử luyện tập hợp lệ.')
  }
  return candidate.map(normalizeHistoryItem)
}

function normalizeCompleteResponse(payload: unknown): CompletePracticeSessionResponse {
  const candidate = requireRecord(
    unwrapPayload(payload),
    'API không trả về xác nhận kết thúc phiên hợp lệ.',
  )
  return {
    message: requireNonEmptyString(
      candidate,
      'message',
      'Phản hồi kết thúc phiên thiếu thông báo.',
    ),
  }
}

function validateStartRequest(payload: StartPracticeSessionRequest): void {
  assertGuid(payload.courseId, 'courseId')
  if (payload.studentId) assertGuid(payload.studentId, 'studentId')
  if (payload.questionCount !== null && payload.questionCount !== undefined) {
    if (!Number.isInteger(payload.questionCount) || payload.questionCount < 1) {
      throw {
        status: 400,
        title: 'Dữ liệu đầu vào không hợp lệ',
        detail: 'Số lượng câu hỏi phải là số nguyên lớn hơn 0.',
      }
    }
  }
}

async function startSession(
  payload: StartPracticeSessionRequest,
): Promise<StartPracticeSessionResponse> {
  validateStartRequest(payload)
  try {
    const response = await apiClient.post<unknown>('/practice/sessions', payload)
    return normalizeStartSession(response.data)
  } catch (error) {
    return rethrowApiError(error, 'Không thể bắt đầu phiên luyện tập.')
  }
}

async function getSession(sessionId: string): Promise<PracticeSessionDetail> {
  assertGuid(sessionId, 'sessionId')
  try {
    const response = await apiClient.get<unknown>(`/practice/sessions/${sessionId}`)
    return normalizeSessionDetail(response.data)
  } catch (error) {
    return rethrowApiError(error, 'Không thể tải chi tiết phiên luyện tập.')
  }
}

async function getNextQuestion(
  sessionId: string,
  studentId?: string,
): Promise<NextPracticeQuestionResponse> {
  assertGuid(sessionId, 'sessionId')
  if (studentId) assertGuid(studentId, 'studentId')
  try {
    const response = await apiClient.post<unknown>(
      `/practice/sessions/${sessionId}/next-question`,
      undefined,
      { params: studentId ? { studentId } : undefined },
    )
    return normalizeNextQuestionResponse(response.data)
  } catch (error) {
    return rethrowApiError(error, 'Không thể lấy câu hỏi luyện tập tiếp theo.')
  }
}

async function submitAnswer(
  sessionId: string,
  payload: SubmitPracticeAnswerRequest,
): Promise<SubmitPracticeAnswerResponse> {
  assertGuid(sessionId, 'sessionId')
  assertGuid(payload.questionId, 'questionId')
  assertGuid(payload.studentId, 'studentId')
  if (payload.parentAnswerId) assertGuid(payload.parentAnswerId, 'parentAnswerId')
  assertAnswerText(payload.answerText)

  try {
    const response = await apiClient.post<unknown>(
      `/practice/sessions/${sessionId}/answers`,
      payload,
    )
    return normalizeSubmitAnswer(response.data)
  } catch (error) {
    return rethrowApiError(error, 'Không thể nộp câu trả lời luyện tập.')
  }
}

async function submitBatch(sessionId: string, payload: SubmitPracticeBatchRequest): Promise<void> {
  assertGuid(sessionId, 'sessionId')
  assertGuid(payload.studentId, 'studentId')
  if (payload.answers.length === 0) {
    throw {
      status: 400,
      title: 'Dữ liệu đầu vào không hợp lệ',
      detail: 'Danh sách câu trả lời không được để trống.',
    }
  }
  for (const answer of payload.answers) {
    assertGuid(answer.questionId, 'questionId')
    if (answer.parentAnswerId) assertGuid(answer.parentAnswerId, 'parentAnswerId')
    if (answer.answerText.trim().length > 0) {
      assertAnswerText(answer.answerText)
    }
  }

  try {
    const response = await apiClient.post(`/practice/sessions/${sessionId}/batch-submit`, payload)
    if (response.status !== 202) {
      return invalidApiResponse(
        'Backend không xác nhận tiếp nhận danh sách câu trả lời bằng HTTP 202.',
      )
    }
  } catch (error) {
    return rethrowApiError(error, 'Không thể nộp danh sách câu trả lời luyện tập.')
  }
}

async function completeSession(
  sessionId: string,
  studentId?: string,
): Promise<CompletePracticeSessionResponse> {
  assertGuid(sessionId, 'sessionId')
  if (studentId) assertGuid(studentId, 'studentId')
  try {
    const response = await apiClient.post<unknown>(
      `/practice/sessions/${sessionId}/complete`,
      undefined,
      { params: studentId ? { studentId } : undefined },
    )
    return normalizeCompleteResponse(response.data)
  } catch (error) {
    return rethrowApiError(error, 'Không thể kết thúc phiên luyện tập.')
  }
}

async function getStudentHistory(studentId?: string): Promise<PracticeHistoryItem[]> {
  if (studentId) assertGuid(studentId, 'studentId')
  try {
    const response = await apiClient.get<unknown>('/practice/student/history', {
      params: studentId ? { studentId } : undefined,
    })
    return normalizeHistory(response.data)
  } catch (error) {
    return rethrowApiError(error, 'Không thể tải lịch sử luyện tập.')
  }
}

export const practiceService = {
  startSession,
  getSession,
  getNextQuestion,
  submitAnswer,
  submitBatch,
  submitBatchAnswers: submitBatch,
  completeSession,
  getStudentHistory,
  startPracticeSession: startSession,
  getPracticeSession: getSession,
  submitPracticeAnswer: submitAnswer,
  submitPracticeBatch: submitBatch,
  completePracticeSession: completeSession,
}
