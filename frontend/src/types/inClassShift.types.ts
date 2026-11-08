/** MF-05 / R6 — Ca thi vấn đáp trên lớp (điểm thành phần). */

export type InClassShiftStatus = 'SCHEDULED' | 'ACTIVE' | 'CLOSED' | 'FINALIZED'

export type InClassSubmissionStatus =
  | 'JOINED'
  | 'IN_PROGRESS'
  | 'SUBMITTED'
  | 'GRADED'
  | 'FINALIZED'

export interface LecturerClassOption {
  id: string
  name: string
  courseId?: string
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
  description: string
  status: InClassShiftStatus
  startTime: string
  durationMinutes: number
  examDurationMinutes: number | null
  questionCount: number
  hasFollowUp: boolean
  maxFollowUpQuestions: number
  questionPoolMode: InClassQuestionPoolMode
  passwordRotationSeconds: number
  gracePeriodSeconds: number
  joinedCount: number
  submittedCount: number
  createdAt: string
  finalizedAt?: string | null
  endTime?: string | null
  /** Trạng thái tham gia của SV hiện tại: JOINED | IN_PROGRESS | SUBMITTED | ... */
  myParticipationStatus?: string | null
}

export type InClassQuestionPoolMode = 'ALL_COURSE_BANK' | 'SELECTED_POOL'

export interface CreateInClassShiftRequest {
  classId: string
  shiftName: string
  startTime: string
  endTime: string
  description?: string
  questionPoolMode: InClassQuestionPoolMode
  selectedQuestionIds?: string[]
  totalQuestionsPerStudent: number
  hasFollowUp: boolean
  maxFollowUpQuestions: number
  isAutoPublishGrades: boolean
  examDurationMinutes: number
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
  studentEmail?: string | null
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
  lecturerComment?: string
}

export interface StudentExamQuestion {
  questionId: string
  title: string
  content: string
}

export interface StudentExamResult {
  submissionId: string
  questionId: string
  title: string
  content: string
  transcript: string
  aiScore: number | null
  aiFeedback: string | null
  finalScore: number | null
  lecturerNotes: string | null
  status: string
  isFollowUp?: boolean
  followUpIndex?: number
  followUpPrompt?: string | null
}

export interface InClassNextFollowUp {
  hasMoreFollowUp: boolean
  followUpIndex: number
  maxFollowUpQuestions: number
  parentSubmissionId: string
  questionId: string
  followUpPrompt: string | null
}

export interface SubmitStudentInClassAnswerResponse {
  submissionId: string
  shiftId: string
  questionId: string
  status: string
  isFollowUp: boolean
  parentSubmissionId: string | null
  followUpIndex: number
}

export interface FinalizeInClassGradesResponse {
  shiftId: string
  status: InClassShiftStatus
  isLocked: boolean
  finalizedAt: string
  message: string
}
