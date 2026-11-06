/** MF-05 / R6 — Ca thi vấn đáp trên lớp (điểm thành phần). */

export type InClassShiftStatus = 'ACTIVE' | 'CLOSED' | 'FINALIZED'

export type InClassSubmissionStatus =
  | 'JOINED'
  | 'IN_PROGRESS'
  | 'SUBMITTED'
  | 'GRADED'
  | 'FINALIZED'

export interface LecturerClassOption {
  id: string
  name: string
  courseCode: string
  courseName: string
  studentCount?: number
}

export interface InClassShift {
  id: string
  classId: string
  className: string
  courseCode: string
  title: string
  status: InClassShiftStatus
  durationMinutes: number
  questionCount: number
  hasFollowUp: boolean
  passwordRotationSeconds: number
  gracePeriodSeconds: number
  joinedCount: number
  submittedCount: number
  createdAt: string
  finalizedAt?: string | null
}

export interface CreateInClassShiftRequest {
  classId: string
  title: string
  durationMinutes: number
  questionCount: number
  hasFollowUp: boolean
  passwordRotationSeconds: number
  gracePeriodSeconds: number
}

export interface ActiveTotpPassword {
  shiftId: string
  currentOtpCode: string
  generatedAt: string
  expiresAt: string
  gracePeriodSeconds: number
  remainingSeconds: number
  previousOtpCode?: string | null
}

export type InClassAnswerStatus = 'PENDING' | 'ANSWERED' | 'GRADED'

/** Một câu trong bài ca trên lớp — mỗi câu có transcript riêng. */
export interface InClassQuestionAnswer {
  answerId: string
  questionId: string
  orderIndex: number
  title: string
  content?: string | null
  isFollowUp?: boolean
  status: InClassAnswerStatus
  transcript?: string | null
  /** Nhận xét / rationale của AI khi chấm câu này */
  aiFeedback?: string | null
  aiScore?: number | null
  /** Điểm GV chỉnh cho câu (null = chưa chỉnh, dùng aiScore) */
  lecturerScore?: number | null
  lecturerComment?: string | null
}

export interface InClassSubmission {
  id: string
  shiftId: string
  studentId: string
  studentCode: string
  studentName: string
  status: InClassSubmissionStatus
  aiScore: number | null
  finalScore: number | null
  overridden: boolean
  overrideReason?: string | null
  /** @deprecated Dùng answers[].transcript — giữ để tương thích mock cũ */
  transcript?: string | null
  answers: InClassQuestionAnswer[]
  joinedAt: string
  submittedAt?: string | null
}

export interface GradeInClassSubmissionRequest {
  adjustedScore: number
  overrideReason: string
}

export interface GradeInClassAnswerRequest {
  lecturerScore: number
  lecturerComment: string
}

export interface FinalizeInClassGradesResponse {
  shiftId: string
  status: InClassShiftStatus
  isLocked: boolean
  finalizedAt: string
  message: string
}
