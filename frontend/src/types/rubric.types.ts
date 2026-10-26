/**
 * MF-03 contracts. Values mirror the .NET question-bank DTOs and domain enums.
 */
export type QuestionApprovalStatus =
  'DRAFT' | 'SUBMITTED_FOR_REVIEW' | 'APPROVED' | 'NEEDS_REVISION' | 'REJECTED'

export type BloomLevel = 'Remember' | 'Understand' | 'Apply' | 'Analyze' | 'Evaluate' | 'Create'

export type QuestionDifficulty = 'easy' | 'medium' | 'hard'

export type QuestionSource = 'manual' | 'flm_api'

export type UsageScope = 'practice' | 'exam' | 'both' | 'shared'

export interface DifficultyDistribution {
  easy: number
  medium: number
  hard: number
}

export interface RubricCriterionDraft {
  criterionName: string
  maxScore: number
  weight: number
  bloomLevel: BloomLevel
  description: string
  orderIndex?: number
}

export interface RubricDraft {
  name: string
  description?: string | null
  totalMaxScore: number
  criteria: RubricCriterionDraft[]
}

export interface RubricCriterionDetail {
  id: string
  criterionName: string
  description: string | null
  maxScore: number
  weight: number
  bloomLevel: BloomLevel | null
  orderIndex: number
}

export interface RubricDetail {
  id: string
  name: string
  description: string | null
  totalMaxScore: number
  criteria: RubricCriterionDetail[]
}

export interface QuestionSummary {
  id: string
  courseId: string
  courseCode: string
  title: string
  difficulty: QuestionDifficulty
  bloomLevel: BloomLevel
  source: QuestionSource
  approvalStatus: QuestionApprovalStatus
  submittedBy: string | null
  approvedBy: string | null
  createdAt: string
  totalRubricScore: number
}

export interface QuestionDetail {
  id: string
  courseId: string
  courseCode: string
  rubricId: string
  title: string
  content: string
  imageUrl?: string | null
  imageAlt?: string | null
  sampleAnswer: string | null
  keyPoints: string[]
  difficulty: QuestionDifficulty
  bloomLevel: BloomLevel
  source: QuestionSource
  approvalStatus: QuestionApprovalStatus
  submittedBy: string | null
  submittedByName: string | null
  approvedBy: string | null
  approvedByName: string | null
  reviewNotes: string | null
  isActive: boolean
  createdAt: string
  rubric: RubricDetail
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
}

export interface QuestionFilters {
  courseId?: string
  approvalStatus?: QuestionApprovalStatus
  difficulty?: QuestionDifficulty
  bloomLevel?: BloomLevel
  source?: QuestionSource
  search?: string
  pageNumber?: number
  pageSize?: number
}

export interface CreateDraftQuestionRequest {
  courseId: string
  title: string
  content: string
  sampleAnswer: string
  keyPoints: string[]
  difficulty: QuestionDifficulty
  bloomLevel: BloomLevel
  source?: QuestionSource
  usageScope?: UsageScope
  hasFollowUp: boolean
  followUpPrompt?: string | null
  rubric: RubricDraft
  lecturerId?: string
}

export interface CreateDraftQuestionResponse {
  questionId: string
}

export interface GeneratedQuestionDraft {
  tempId: string
  cloCode: string
  title: string
  content: string
  imageUrl?: string | null
  imageAlt?: string | null
  difficulty: QuestionDifficulty
  bloomLevel: BloomLevel
  source: QuestionSource
  usageScope: UsageScope
  modelAnswer: string
  keyPoints: string[]
  hasFollowUp: boolean
  followUpPrompt: string | null
  rubric: RubricDraft
}

export interface GenerateQuestionsFromFlmRequest {
  courseId: string
  selectedCloCodes: string[]
  topics: string[]
  numberOfQuestions: number
  usageScope: UsageScope
  targetBloomLevels: BloomLevel[]
  difficultyDistribution: DifficultyDistribution
  requesterUserId?: string
}

export interface GenerateQuestionsFromFlmResponse {
  courseId: string
  courseCode: string
  generatedCount: number
  source: 'flm_api'
  questions: GeneratedQuestionDraft[]
}

export interface BatchSubmitForReviewRequest {
  courseId: string
  questionIds: string[]
  submissionNotes?: string | null
  lecturerId?: string
}

export interface BatchSubmitForReviewResponse {
  courseId: string
  submittedCount: number
  approvalStatus: 'SUBMITTED_FOR_REVIEW'
  submittedBy: string
  submittedAt: string
  message: string
}

export interface ReviewQuestionDecisionRequest {
  decision: 'APPROVED' | 'NEEDS_REVISION' | 'REJECTED'
  reviewNotes?: string
  targetUsageScope?: UsageScope
  reviewerId?: string
}

export interface BatchApproveQuestionsResult {
  approvedCount: number
}

/** Compatibility aliases for components that use shorter rubric names. */
export type RubricCriterion = RubricCriterionDetail
export type Question = QuestionDetail
export type QuestionBankItem = QuestionDetail
export type CreateQuestionRequest = CreateDraftQuestionRequest
