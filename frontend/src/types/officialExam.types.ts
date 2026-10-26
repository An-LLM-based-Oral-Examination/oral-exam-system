/** MF-04 contracts returned by the official-exam, proctor and student APIs. */
type OfficialExamInputMode = string
export type ExamTicketStatus =
  | 'SCHEDULED'
  | 'IN_PROGRESS'
  | 'SUBMITTED'
  | 'AI_GRADED'
  | 'AUDITED'
  | 'PUBLISHED'
  | 'LOCKED'

export interface AuditEvidenceItem {
  ticketId: string
  studentCode: string
  fullName: string
  seatNumber: number
  status: ExamTicketStatus | string
  audioR2Url: string | null
  audioHashSha256: string | null
  transcriptWhisper: string | null
  aiScore: number | null
  confidenceScore: number | null
  isSuspicious: boolean
  suspiciousReason: string | null
  cotTrace: string | null
  finalScore: number | null
  isLocked: boolean
  questionId?: string | null
  questionTitle?: string | null
  questionContent?: string | null
  questionImageUrl?: string | null
  questionImageDescription?: string | null
}

export interface PublishGradesResponse {
  shiftId: string
  totalPublished: number
  publishedAt: string
  message: string
}

/** Kept for backwards compatibility; the backend derives the actor from the access token. */
export interface PublishGradesRequest {
  lecturerId?: string
}

export interface OfficialExamSession {
  id: string
  courseId: string
  examStructureId: string
  title: string
  examDate: string
  hasFollowUp: boolean
  maxFollowUpQuestions: number
  examInputMode: OfficialExamInputMode
  transcriptBufferSeconds: number
  status: string
}

export interface GetOfficialExamSessionsParams {
  courseId?: string
  status?: string
}

export interface ExamStructure {
  id: string
  courseId: string
  courseCode: string
  createdBy: string
  name: string
  totalQuestions: number
  durationMinutes: number
  bloomDistribution: string
  isActive: boolean
  createdAt: string
}

export interface GetExamStructuresParams {
  courseId?: string
  isActive?: boolean
}

export interface CreateExamStructureRequest {
  courseId: string
  name: string
  totalQuestions?: number
  durationMinutes?: number
  bloomDistribution?: string | null
  isActive?: boolean
}

export interface UpdateExamStructureRequest {
  name: string
  totalQuestions?: number
  durationMinutes?: number
  bloomDistribution?: string | null
  isActive?: boolean
}

export interface CreateOfficialExamSessionRequest {
  courseId: string
  examStructureId: string
  title?: string | null
  examDate: string
  hasFollowUp: boolean
  maxFollowUpQuestions: number
  examInputMode?: OfficialExamInputMode | null
  transcriptBufferSeconds: number
}

export interface ExamShift {
  id: string
  sessionId: string
  shiftName: string
  roomLab: string
  startTime: string
  endTime: string
  proctorUserId: string | null
  status: string
}

export interface GetExamShiftsParams {
  sessionId?: string
  proctorUserId?: string
  status?: string
}

export interface CreateExamShiftRequest {
  sessionId: string
  shiftName?: string | null
  roomLab?: string | null
  startTime: string
  endTime: string
  proctorUserId: string
}

export interface AssignExamTicketRequest {
  shiftId: string
  studentId: string
  seatNumber: number
  ipAddress?: string | null
}

export interface ExamTicket {
  id: string
  shiftId: string
  studentId: string
  seatNumber: number
  ipAddress: string | null
  status: ExamTicketStatus | string
  checkedInAt: string
  examSetId: string | null
}

export interface WorkstationExamQuestion {
  questionId: string
  orderIndex: number
  title: string
  content: string
  bloomLevel: string
  difficulty: string
  imageUrl: string | null
  imageDescription: string | null
}

export interface StudentWorkstationTicket {
  id: string
  shiftId: string
  shiftName: string
  roomLab: string
  startTime: string
  endTime: string
  shiftStatus: string
  sessionTitle: string
  studentId: string
  seatNumber: number
  status: ExamTicketStatus | string
  examSetId: string | null
  examSetCode: string | null
  examInputMode: OfficialExamInputMode | null
  transcriptBufferSeconds: number
  hasFollowUp: boolean
  maxFollowUpQuestions: number
  questions: WorkstationExamQuestion[]
}

export interface AudioUploadAuthorization {
  objectKey: string
  fileName: string
  uploadUrl: string
  expiresAt: string
  requiredHeaders: Record<string, string>
}

export interface SealOfficialExamAudioRequest {
  objectKey?: string | null
  sha256?: string | null
}

export interface AudioSealResponse {
  ticketId: string
  objectKey: string
  sha256: string
  contentLength: number
  verifiedAt: string
}

export interface OfficialExamAnswerSubmissionRequest {
  examQuestionId: string
  transcriptWhisper?: string | null
  timestampsWhisper?: string | null
  timeSpentSeconds: number
}

export interface SubmitOfficialExamRequest {
  answers: OfficialExamAnswerSubmissionRequest[]
}

export interface SubmitOfficialExamResponse {
  ticketId: string
  status: string
  submittedAt: string
  submissionCount: number
}

export interface OfficialExamSubmissionStatusResponse {
  ticketId: string
  status: string
  submittedAt: string | null
  totalSubmissions: number
  pendingSubmissions: number
  gradedSubmissions: number
  failedSubmissions: number
}

export interface OverrideCriterionScore {
  criterionId: string
  auditedScore: number
  lecturerComment?: string | null
  aiScore?: number | null
  comment?: string | null
}

export interface OverrideExamScoreRequest {
  ticketId?: string
  auditedScore: number
  overrideReason: string
  criterionDetails?: OverrideCriterionScore[] | null
  criteriaScores?: OverrideCriterionScore[] | null
}

export interface OverrideExamScoreResponse {
  auditId: string
  ticketId: string
  lecturerId: string
  originalAiScore: number
  auditedScore: number
  overrideReason: string
  ticketStatus: string
  isLocked: boolean
  auditedAt: string
}

export interface LockExamTicketResponse {
  ticketId: string
  isLocked: boolean
  lockedAt: string
  status: string
  message: string
}

export interface OfficialExamExportFile {
  blob: Blob
  fileName: string
  contentType: string
  certificateHash: string | null
}

export interface StudentOfficialExamCriterionScore {
  criterionName: string
  score: number
  maxScore: number
  comment: string | null
}

export interface StudentOfficialExamQuestionScorecard {
  questionId: string
  score: number
  criteriaScores: StudentOfficialExamCriterionScore[]
  strengths: string | null
  weaknesses: string | null
  improvementSuggestions: string | null
  questionTitle: string | null
  questionContent: string | null
  questionImageUrl: string | null
  questionImageDescription: string | null
  studentTranscript: string | null
  lecturerOverrideReason: string | null
}

export interface StudentInternalAppealInfo {
  canAppeal: boolean
  instruction: string
  appealDeadlineDays: number
  appealEndpoint: string
}

export interface StudentOfficialExamGrade {
  ticketId: string
  studentCode: string
  fullName: string
  courseCode: string
  status: string
  isLocked: boolean
  totalScore: number | null
  publishedAt: string
  publishedBy: string
  studentAcknowledgementStatus: string
  detailedScorecard: StudentOfficialExamQuestionScorecard[]
  internalAppeal: StudentInternalAppealInfo
}

export interface AcknowledgeOfficialGradeResponse {
  ticketId: string
  studentId: string
  studentAcknowledgementStatus: string
  acknowledgedAtUtc: string
  message: string
}
