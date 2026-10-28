/**
 * Hợp đồng dữ liệu MF-01 dùng chung giữa giao diện, REST API và PracticeHub.
 */

export type SubmissionStatus = 'PENDING' | 'QUEUED' | 'GRADED' | 'PENDING_RETRY' | 'FAILED'

export type PracticeMode = 'PER_QUESTION' | 'FULL_SESSION'

export type ApiPracticeMode = 'per_question' | 'full_session'

export type PracticeDifficulty = 'easy' | 'medium' | 'hard'

export type PracticeDifficultyOption = PracticeDifficulty | 'progressive'

export type PracticeSessionStatus = 'in_progress' | 'completed' | 'abandoned'

export type PracticeAnswerStatus = 'pending' | 'grading' | 'graded' | 'failed'

export type ExamInputMode = 'VoiceOnly' | 'VoiceWithTranscriptEdit'

export interface PracticeSessionConfig {
  courseId: string
  courseName: string
  courseCode: string
  mode: PracticeMode
  selectedDifficulties: PracticeDifficulty[]
  questionCount: number | null
  microphoneName: string
}

export interface CourseDto {
  id: string
  code: string
  name: string
  credits: number
  transcriptBufferSeconds: number
  maxFollowUpQuestions: number
  hasFollowUp: boolean
  examInputMode: ExamInputMode
  isActive: boolean
}

export interface CourseConfigurationDto {
  courseId: string
  courseCode: string
  courseName: string
  transcriptBufferSeconds: number
  maxFollowUpQuestions: number
  hasFollowUp: boolean
  examInputMode: ExamInputMode
  isActive: boolean
}

export interface PracticeQuestion {
  questionId: string
  content: string
  imageUrl?: string | null
  imageAlt?: string | null
  rubricCriteria: string[]
}

export interface NextPracticeQuestion {
  id: string
  content: string
  imageUrl?: string | null
  imageAlt?: string | null
  difficulty: PracticeDifficulty
  questionOrder: number
  rubricCriteria: string[]
}

export interface NextPracticeQuestionResponse {
  hasMoreQuestions: boolean
  message: string | null
  question: NextPracticeQuestion | null
}

export interface StartPracticeSessionRequest {
  courseId: string
  difficulty?: PracticeDifficultyOption | null
  questionCount?: number | null
  isFullSession: boolean
  topic?: string | null
  studentId?: string | null
  difficulties?: PracticeDifficulty[] | null
}

export interface StartPracticeSessionResponse {
  sessionId: string
  transcriptBufferSeconds: number
  questions: PracticeQuestion[]
}

export interface SubmitPracticeAnswerRequest {
  questionId: string
  studentId: string
  answerText: string
  isFollowUp: boolean
  parentAnswerId?: string | null
}

export interface SubmitPracticeAnswerResponse {
  answerId: string
}

export interface BatchPracticeAnswer {
  questionId: string
  answerText: string
  isFollowUp: boolean
  parentAnswerId?: string | null
}

export interface SubmitPracticeBatchRequest {
  studentId: string
  answers: BatchPracticeAnswer[]
}

export interface CompletePracticeSessionResponse {
  message: string
}

export interface PracticeEvaluationDetail {
  criterionId: string
  criterionName: string
  score: number
  maxScore?: number
  comment: string | null
}

export interface PracticeAnswerDetail {
  answerId: string
  questionId: string
  answerText: string
  isFollowUp: boolean
  parentAnswerId: string | null
  status: PracticeAnswerStatus
  submittedAt: string
  totalScore: number | null
  feedback: string | null
  confidenceScore: number | null
  isSuspicious: boolean | null
  needsFollowUp: boolean
  followUpPrompt: string | null
  criteriaScores: PracticeEvaluationDetail[]
}

export interface PracticeSessionDetail {
  sessionId: string
  courseId: string
  courseCode: string
  courseName: string
  studentId: string
  studentName: string
  practiceMode: ApiPracticeMode
  status: PracticeSessionStatus
  transcriptBufferSeconds: number
  startedAt: string
  endedAt: string | null
  questions: PracticeQuestion[]
  answers: PracticeAnswerDetail[]
}

export interface PracticeHistoryItem {
  sessionId: string
  courseId: string
  courseCode: string
  courseName: string
  practiceMode: ApiPracticeMode
  status: PracticeSessionStatus
  startedAt: string
  endedAt: string | null
  totalQuestions: number
  answeredQuestions: number
  averageScore: number | null
}

export interface UploadAudioResponse {
  transcript: string
}

/** Dữ liệu hiển thị Scorecard hiện có của frontend. */
export interface CriterionScore {
  criterionId: string
  criterionName?: string
  score: number
  maxScore?: number
  comment: string
  questionNumber?: number
}

export interface ScorecardPayload {
  submissionId: string
  totalScore: number
  criteriaScores: CriterionScore[]
  feedback: string
  needsFollowUp?: boolean
  followUpPrompt?: string | null
  followUpQuestion?: string
}

/** Tên tương thích cho những component cũ còn dùng PracticeSession. */
export type PracticeSession = PracticeSessionDetail
