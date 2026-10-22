/**
 * Hợp đồng MF-02 — thi thử vấn đáp bấm giờ có hạn ngạch.
 */

export interface MockExamQuota {
  courseId: string
  studentId: string
  quotaDate: string
  usedCount: number
  remainingCount: number
  maxDailyQuota: number
  canStartExam: boolean
}

export interface MockExamRubricCriterion {
  name: string
  maxScore: number
}

export interface MockExamQuestion {
  id: string
  orderIndex: number
  title: string
  content: string
  imageUrl?: string | null
  imageAlt?: string | null
  bloomLevel: string
  difficulty: string
  rubricCriteria: MockExamRubricCriterion[]
}

export interface StartMockExamRequest {
  courseId: string
  hasFollowUp: boolean
}

export interface StartMockExamResponse {
  sessionId: string
  courseId: string
  courseCode: string
  courseName: string
  hasFollowUp: boolean
  durationMinutes: number
  startedAt: string
  expiresAt: string
  usedQuota: number
  remainingQuota: number
  maxDailyQuota: number
  questions: MockExamQuestion[]
}

export interface SubmitMockExamAnswerRequest {
  questionId: string
  answerId?: string | null
  file?: Blob | null
  fileName?: string
  timeTakenSeconds: number
  transcript?: string
}

export interface SubmitMockExamAnswerResponse {
  answerId: string
  questionId: string
  transcript: string
  status: string
  answeredAt: string
  isFollowUp: boolean
  parentAnswerId: string | null
  needsFollowUp: boolean
  followUpPrompt: string | null
  followUpAnswerId: string | null
  followUpIndex: number
  maxFollowUps: number
}

export interface MockExamActiveFollowUp {
  answerId: string
  questionId: string
  parentAnswerId: string
  prompt: string
  followUpIndex: number
  maxFollowUps: number
}

export interface MockExamCriterionScore {
  criterionId: string
  criterionName: string
  bloomLevel: string | null
  maxScore: number
  score: number
  comment: string | null
}

export interface MockExamQuestionScore {
  questionId: string
  orderIndex: number
  title: string
  content: string
  imageUrl?: string | null
  imageAlt?: string | null
  bloomLevel: string
  difficulty: string
  transcript: string | null
  score: number
  feedback: string | null
  confidenceScore: number | null
  criteriaScores: MockExamCriterionScore[]
  isFollowUp?: boolean
  answerId?: string | null
  parentAnswerId?: string | null
}

export interface MockExamScorecard {
  sessionId: string
  courseId: string
  courseCode: string
  courseName: string
  totalScore: number
  classification: string
  totalQuestions: number
  answeredQuestions: number
  durationMinutes: number
  isOverdue: boolean
  startedAt: string
  submittedAt: string
  questions: MockExamQuestionScore[]
}

export interface MockExamHistoryItem {
  sessionId: string
  courseId: string
  courseCode: string
  courseName: string
  startedAt: string
  durationMinutes: number
  totalScore: number | null
  status: string
  answeredQuestions: number
  totalQuestions: number
}

export interface MockExamHistoryPage {
  items: MockExamHistoryItem[]
  pageIndex: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface StoredMockExamAnswer {
  questionId: string
  transcript: string
  timeTakenSeconds: number
  answerId: string
  answeredAt: string
  isFollowUp?: boolean
  parentAnswerId?: string | null
  /** Nội dung câu hỏi phụ (để xem lại trong phiên). */
  promptText?: string | null
}

export interface StoredMockExamSession extends StartMockExamResponse {
  answers: StoredMockExamAnswer[]
  /** Chỉ số câu xa nhất đã mở (làm hoặc bỏ qua). Không nhảy tới câu chưa xem. */
  furthestQuestionIndex?: number
  activeFollowUp?: MockExamActiveFollowUp | null
}

export interface ResumeMockExamResponse extends StartMockExamResponse {
  answers: StoredMockExamAnswer[]
  activeFollowUp?: MockExamActiveFollowUp | null
}
