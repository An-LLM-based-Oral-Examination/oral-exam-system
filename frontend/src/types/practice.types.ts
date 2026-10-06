/**
 * Submission Status Life-cycle
 */
export type SubmissionStatus = 'PENDING' | 'QUEUED' | 'GRADED' | 'PENDING_RETRY' | 'FAILED'

/**
 * AI Criterion Score Detail
 */
export interface CriterionScore {
  criterionId: string
  score: number
  comment: string
}

/**
 * Scorecard Payload pushed via SignalR or returned in API
 */
export interface ScorecardPayload {
  submissionId: string
  totalScore: number
  criteriaScores: CriterionScore[]
  feedback: string
  followUpQuestion?: string
}

/**
 * MF-01 Practice Session Contract
 */
export interface PracticeSession {
  id: string
  userId: string
  subjectId: string
  mode: 'PER_QUESTION' | 'FULL_SESSION'
  status: string
  createdAt: string
}
