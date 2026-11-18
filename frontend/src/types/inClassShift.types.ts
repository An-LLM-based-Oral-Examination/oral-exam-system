/** MF-05 / R6 — Ca thi vấn đáp trên lớp (điểm thành phần). */

export type InClassShiftStatus = 'SCHEDULED' | 'ACTIVE' | 'CLOSED' | 'FINALIZED'

export type InClassSubmissionStatus =
  | 'JOINED'
  | 'IN_PROGRESS'
  | 'SUBMITTED'
  | 'GRADING'
  | 'GRADED'
  | 'AUDITED'
  | 'FINALIZED'

export type InClassDifficulty = 'easy' | 'medium' | 'hard'

/** Chế độ nguồn câu hỏi khi đọc ca (hỗ trợ đọc tương thích ngược ca cũ SELECTED_POOL). */
export type InClassQuestionPoolMode = 'ALL_COURSE_BANK' | 'SELECTED_CLOS' | 'SELECTED_POOL'

/** Chế độ nguồn câu hỏi khi tạo ca mới theo thiết kế RC-3. */
export type InClassCreatePoolMode = 'ALL_COURSE_BANK' | 'SELECTED_CLOS'

export interface InClassDifficultyCounts {
  easy: number
  medium: number
  hard: number
  total: number
}

export interface InClassCloPoolStat {
  cloId: string
  cloCode: string
  description: string
  counts: InClassDifficultyCounts
}

export interface InClassShiftPoolStats {
  classId: string
  courseId: string
  courseCode: string
  courseBankCounts: InClassDifficultyCounts
  clos: InClassCloPoolStat[]
}

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
  isLocked?: boolean
  startTime: string
  durationMinutes: number
  examDurationMinutes: number | null
  questionCount: number
  hasFollowUp: boolean
  maxFollowUpQuestions: number
  isAutoPublishGrades: boolean
  questionPoolMode: InClassQuestionPoolMode
  selectedDifficulties: InClassDifficulty[]
  selectedCloIds: string[]
  selectedCloCodes?: string[]
  passwordRotationSeconds: number
  gracePeriodSeconds: number
  joinedCount: number
  submittedCount: number
  createdAt: string
  finalizedAt?: string | null
  endTime?: string | null
  /** Trạng thái tham gia của SV hiện tại: JOINED | IN_PROGRESS | SUBMITTED | GRADED | AUDITED | FINALIZED */
  myParticipationStatus?: string | null
  /** Thời điểm SV vào phòng (ISO UTC). */
  myJoinedAt?: string | null
  /** Hết giờ làm bài cá nhân (ISO UTC). */
  myExamEndsAt?: string | null
  /** Số giây còn lại theo đồng hồ server. */
  myRemainingSeconds?: number | null
}

export interface CreateInClassShiftRequest {
  classId: string
  shiftName: string
  startTime: string
  endTime: string
  description?: string
  questionPoolMode: InClassCreatePoolMode
  totalQuestionsPerStudent: number
  selectedDifficulties: InClassDifficulty[]
  selectedCloIds?: string[]
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

export type InClassAnswerStatus = 'PENDING' | 'ANSWERED' | 'GRADING' | 'GRADED' | 'AUDITED'

/** Một câu trong bài ca trên lớp — mỗi câu có transcript riêng. */
export interface InClassQuestionAnswer {
  answerId: string
  questionId: string
  orderIndex: number
  /** Số thứ tự câu hỏi chính (1..N) để định danh nhóm câu chính + câu phụ */
  mainOrderIndex?: number
  title: string
  content?: string | null
  isFollowUp?: boolean
  followUpIndex?: number
  parentSubmissionId?: string | null
  /** Nội dung câu hỏi phụ AI đã hỏi cho lượt trả lời này */
  followUpPrompt?: string | null
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
  imageUrl?: string | null
  imageDescription?: string | null
}

/** Kết quả enter / resume phòng làm bài. */
export interface EnterInClassExamResult {
  questions: StudentExamQuestion[]
  joinedAt: string
  examEndsAt: string
  remainingSeconds: number
  timedOut: boolean
  status: string
}

export interface StudentExamResult {
  submissionId: string
  questionId: string
  mainOrderIndex?: number
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
  parentSubmissionId?: string | null
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
