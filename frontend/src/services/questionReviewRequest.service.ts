import apiClient from './api.client'

export type ReviewDecision = 'APPROVED' | 'NEEDS_REVISION' | 'REJECTED'
export interface ReviewSnapshot {
  rubricName?: string | null
  rubricTotalMaxScore?: number | null
  source?: string | null
  createdAt?: string | null
  title: string
  content: string
  sampleAnswer: string | null
  difficulty: string
  bloomLevel: string
  cloCode: string | null
  imageUrl: string | null
  imageDescription: string | null
  criteria: Array<{
    id?: string | null
    name: string
    description: string | null
    maxScore: number
    bloomLevel: string | null
  }>
}
export interface ReviewRequest {
  id: string
  courseId: string
  courseCode: string
  courseName: string
  lecturerName: string
  lecturerEmail: string
  submittedAt: string | null
  notes: string | null
  status: string
  total: number
  pending: number
  approved: number
  needsRevision: number
  rejected: number
  version: number
}
export interface ReviewRequestItem {
  id: string
  questionId: string
  usageScope: string
  decision: 'PENDING' | ReviewDecision
  reviewNotes: string | null
  reviewedAt: string | null
  reviewerName: string | null
  question: ReviewSnapshot | null
}
export interface ReviewRequestDetail {
  summary: ReviewRequest
  items: ReviewRequestItem[]
}
export interface ReviewRequestPage {
  items: ReviewRequest[]
  totalCount: number
  page: number
  pageSize: number
}

const endpoint = '/department-head/question-review-requests'
export const questionReviewRequestService = {
  async list(
    params: { courseId?: string; status?: string; keyword?: string; page: number },
    signal?: AbortSignal,
  ) {
    return (await apiClient.get<ReviewRequestPage>(endpoint, { params, signal })).data
  },
  async detail(id: string, signal?: AbortSignal) {
    return (await apiClient.get<ReviewRequestDetail>(`${endpoint}/${id}`, { signal })).data
  },
  async decide(
    id: string,
    input: { itemIds: string[]; decision: ReviewDecision; notes?: string; version: number },
  ) {
    await apiClient.post(`${endpoint}/${id}/decisions`, input)
  },
}
