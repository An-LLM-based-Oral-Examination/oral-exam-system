import apiClient from './api.client'
import type {
  BatchSubmitForReviewRequest,
  BatchSubmitForReviewResponse,
  CreateDraftQuestionRequest,
  CreateDraftQuestionResponse,
  GenerateQuestionsFromFlmRequest,
  GenerateQuestionsFromFlmResponse,
  PagedResult,
  QuestionDetail,
  QuestionFilters,
  QuestionSummary,
} from '@/types/rubric.types'

type JsonRecord = Record<string, unknown>

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function readNumber(record: JsonRecord, camelCaseKey: string, pascalCaseKey: string) {
  const value = record[camelCaseKey] ?? record[pascalCaseKey]
  return typeof value === 'number' && Number.isFinite(value) ? value : undefined
}

function unwrapRecord(payload: unknown): JsonRecord | null {
  if (!isRecord(payload)) return null
  const nestedPayload = payload.value ?? payload.Value ?? payload.data ?? payload.Data
  return isRecord(nestedPayload) ? nestedPayload : payload
}

function invalidApiResponse(detail: string): never {
  throw {
    status: 502,
    title: 'Phản hồi API không hợp lệ',
    detail,
  }
}

function normalizePagedQuestions(payload: unknown): PagedResult<QuestionSummary> {
  const candidate = unwrapRecord(payload)

  if (Array.isArray(payload)) {
    return {
      items: payload as QuestionSummary[],
      totalCount: payload.length,
      pageNumber: 1,
      pageSize: payload.length,
      totalPages: payload.length > 0 ? 1 : 0,
    }
  }

  if (!candidate) {
    throw {
      status: 502,
      title: 'Phản hồi API không hợp lệ',
      detail:
        'Không nhận được danh sách câu hỏi dạng JSON. Hãy kiểm tra Backend API và cấu hình VITE_API_BASE_URL.',
    }
  }

  const rawItems = candidate.items ?? candidate.Items
  if (!Array.isArray(rawItems)) {
    throw {
      status: 502,
      title: 'Phản hồi API không hợp lệ',
      detail: 'API câu hỏi không trả về trường items. Hãy khởi động Backend API rồi tải lại trang.',
    }
  }

  const items = rawItems as QuestionSummary[]
  const pageSize = readNumber(candidate, 'pageSize', 'PageSize') ?? items.length
  const totalCount = readNumber(candidate, 'totalCount', 'TotalCount') ?? items.length
  const pageNumber = readNumber(candidate, 'pageNumber', 'PageNumber') ?? 1
  const totalPages =
    readNumber(candidate, 'totalPages', 'TotalPages') ??
    (pageSize > 0 ? Math.ceil(totalCount / pageSize) : 0)

  return { items, totalCount, pageNumber, pageSize, totalPages }
}

function normalizeQuestionDetail(payload: unknown): QuestionDetail {
  const candidate = unwrapRecord(payload)
  if (
    !candidate ||
    typeof candidate.id !== 'string' ||
    typeof candidate.title !== 'string' ||
    !isRecord(candidate.rubric) ||
    !Array.isArray(candidate.rubric.criteria)
  ) {
    return invalidApiResponse('API không trả về chi tiết câu hỏi và Barem Rubric hợp lệ.')
  }
  return candidate as unknown as QuestionDetail
}

function normalizeCreateResponse(payload: unknown): CreateDraftQuestionResponse {
  const candidate = unwrapRecord(payload)
  if (!candidate || typeof candidate.questionId !== 'string') {
    return invalidApiResponse('Backend không trả về mã câu hỏi vừa tạo.')
  }
  return candidate as unknown as CreateDraftQuestionResponse
}

function normalizeGenerationResponse(payload: unknown): GenerateQuestionsFromFlmResponse {
  const candidate = unwrapRecord(payload)
  if (
    !candidate ||
    typeof candidate.courseId !== 'string' ||
    typeof candidate.courseCode !== 'string' ||
    typeof candidate.generatedCount !== 'number' ||
    !Array.isArray(candidate.questions)
  ) {
    return invalidApiResponse('API FLM không trả về danh sách câu hỏi đề xuất hợp lệ.')
  }
  return candidate as unknown as GenerateQuestionsFromFlmResponse
}

function normalizeBatchResponse(payload: unknown): BatchSubmitForReviewResponse {
  const candidate = unwrapRecord(payload)
  if (
    !candidate ||
    typeof candidate.courseId !== 'string' ||
    typeof candidate.submittedCount !== 'number' ||
    typeof candidate.message !== 'string'
  ) {
    return invalidApiResponse('Backend không trả về xác nhận gửi thẩm định hợp lệ.')
  }
  return candidate as unknown as BatchSubmitForReviewResponse
}

export const questionService = {
  async getQuestions(
    filters: Readonly<QuestionFilters> = {},
  ): Promise<PagedResult<QuestionSummary>> {
    const response = await apiClient.get<unknown>('/questions', {
      params: filters,
    })
    return normalizePagedQuestions(response.data)
  },

  async getQuestionById(id: string): Promise<QuestionDetail> {
    const response = await apiClient.get<unknown>(`/questions/${id}`)
    return normalizeQuestionDetail(response.data)
  },

  async createDraft(payload: CreateDraftQuestionRequest): Promise<CreateDraftQuestionResponse> {
    const response = await apiClient.post<unknown>('/questions', payload)
    return normalizeCreateResponse(response.data)
  },

  async generateFromFlm(
    payload: GenerateQuestionsFromFlmRequest,
  ): Promise<GenerateQuestionsFromFlmResponse> {
    const response = await apiClient.post<unknown>('/questions/generate-from-flm', payload)
    return normalizeGenerationResponse(response.data)
  },

  async batchSubmitForReview(
    payload: BatchSubmitForReviewRequest,
  ): Promise<BatchSubmitForReviewResponse> {
    const response = await apiClient.post<unknown>('/questions/batch-submit-review', payload)
    return normalizeBatchResponse(response.data)
  },

  async reviewDecision(
    id: string,
    payload: {
      decision: 'APPROVED' | 'NEEDS_REVISION' | 'REJECTED'
      reviewNotes?: string
      targetUsageScope?: 'exam' | 'practice' | 'both'
    },
  ): Promise<{
    questionId: string
    approvalStatus: string
    reviewNotes?: string | null
    message: string
  }> {
    const response = await apiClient.post<unknown>(`/questions/${id}/review-decision`, payload)
    const candidate = unwrapRecord(response.data)
    return {
      questionId:
        (candidate && typeof candidate.questionId === 'string' ? candidate.questionId : null) ?? id,
      approvalStatus:
        (candidate && typeof candidate.approvalStatus === 'string'
          ? candidate.approvalStatus
          : null) ?? payload.decision,
      reviewNotes:
        candidate && typeof candidate.reviewNotes === 'string'
          ? candidate.reviewNotes
          : (payload.reviewNotes ?? null),
      message:
        (candidate && typeof candidate.message === 'string' ? candidate.message : null) ??
        `Đã cập nhật trạng thái thẩm định câu hỏi thành ${payload.decision}.`,
    }
  },
}
