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
