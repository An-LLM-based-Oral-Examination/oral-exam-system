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

/**
 * MF-02 Mock Exam Session Contract
 */
export interface MockExamSession {
  id: string
  userId: string
  subjectId: string
  startTime: string
  maxDurationSeconds: number
  status: string
  dailyQuotaRemaining?: number
}

/**
 * MF-04 Lab Oral Exam Session Contract
 */
export interface ExamSession {
  id: string
  sessionCode: string
  roomCode: string
  subjectId: string
  subjectCode?: string
  startTime: string
  endTime: string
  isCompleted: boolean
  totalCandidates?: number
}

/**
 * Candidate Submission for Lecturer Audit Portal
 */
export interface CandidateSubmission {
  id: string
  sessionId: string
  studentId: string
  studentCode: string
  studentName: string
  seatNumber: number
  audioUrl: string
  audioSha256Hash: string
  aiProposedScore: number
  officialScore?: number
  isLocked: boolean
  overrideReason?: string
  lockedAt?: string
}
