/**
 * Submission Status Life-cycle
 */
export type SubmissionStatus = 'PENDING' | 'QUEUED' | 'GRADED' | 'PENDING_RETRY' | 'FAILED'

export type PracticeMode = 'PER_QUESTION' | 'FULL_SESSION'

export type PracticeDifficulty = 'easy' | 'medium' | 'hard'

export interface PracticeSessionConfig {
  courseId: string
  courseName: string
  courseCode: string
  module: string
  mode: PracticeMode
  selectedDifficulties: PracticeDifficulty[]
  questionCount: number | null
  microphoneName: string
}

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
  mode: PracticeMode
  status: string
  createdAt: string
}
