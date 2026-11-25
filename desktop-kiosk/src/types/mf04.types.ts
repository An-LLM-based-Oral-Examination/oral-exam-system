export type ScreenId =
  | 'sign-in'
  | 'waiting-ticket'
  | 'pre-check'
  | 'lobby'
  | 'exam'
  | 'submitted'
  | 'blocked'

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  errors?: Record<string, string[]>
}

export type ByodConflictCode =
  | 'DEVICE_MISMATCH'
  | 'ACTIVE_SESSION_CONFLICT'
  | 'DEVICE_IN_USE_OTHER_TICKET'

export interface AuthUser {
  id: string
  email: string
  fullName: string
  role: string
  studentCode?: string | null
  mustChangePassword?: boolean
}

export interface LoginResponse {
  accessToken: string
  refreshToken: string
  tokenType: string
  expiresIn: number
  mustChangePassword: boolean
  user: AuthUser
}

export interface RefreshTokenResponse {
  accessToken: string
  refreshToken: string
  tokenType: string
  expiresIn: number
}

export interface ChangePasswordResponse {
  accessToken: string
  refreshToken: string
  tokenType: string
  expiresIn: number
  mustChangePassword: boolean
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

export type ExamInputMode = 'VoiceOnly' | 'VoiceWithTranscriptEdit' | string

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
  status: string
  examSetId: string | null
  examSetCode: string | null
  examInputMode: ExamInputMode | null
  transcriptBufferSeconds: number
  hasFollowUp: boolean
  maxFollowUpQuestions: number
  serverTimeUtc: string
  questions: WorkstationExamQuestion[]
}

export interface ExamTicketResponse {
  id: string
  shiftId: string
  studentId: string
  seatNumber: number
  ipAddress: string | null
  status: string
  checkedInAt: string
  examSetId: string | null
  activeSessionToken?: string | null
}

export interface WorkstationFollowUpItem {
  submissionId: string
  parentSubmissionId: string | null
  questionId: string
  followUpIndex: number
  followUpPrompt: string | null
  generatedFollowUpPrompt: string | null
  transcriptWhisper: string | null
  audioObjectKey: string | null
  audioHashSha256: string | null
}

export interface OfficialExamTicketStateResponse {
  ticketId: string
  shiftId: string
  status: string
  isExamPublished: boolean
  isKioskLocked: boolean
  violationCount: number
  maxAllowedWindowBlurs: number
  windowBlurLockThreshold: number
  remainingStrikes: number
  serverUtcNow: string
  deviceId: string | null
  deviceLockedAt: string | null
  deviceUnlockCount: number
  followUps: WorkstationFollowUpItem[]
}

export interface KioskHeartbeatRequest {
  audioLevelDb?: number | null
  isFullscreen?: boolean | null
  displayCount?: number | null
  deviceId?: string | null
  kioskVersion?: string | null
}

export interface KioskHeartbeatResponse {
  ticketId: string
  shiftId: string
  ticketStatus: string
  shiftStatus: string
  serverUtcNow: string
  remainingShiftSeconds: number
  isLocked: boolean
  deviceUnlockRequired: boolean
  shouldTerminateExam: boolean
  notice: string | null
}

export interface AudioUploadAuthorization {
  objectKey: string
  fileName: string
  uploadUrl: string
  expiresAt: string
  requiredHeaders: Record<string, string>
}

export interface AudioSealResponse {
  ticketId: string
  objectKey: string
  sha256: string
  contentLength: number
  verifiedAt: string
}

export interface ExamAnswer {
  examQuestionId: string
  transcriptWhisper: string
  initialVoiceTranscript?: string | null
  isManuallyEdited?: boolean
  timestampsWhisper: string | null
  timeSpentSeconds: number
}

export interface SubmitQuestionAnswerRequest {
  followUpIndex: number
  parentSubmissionId: string | null
  audioObjectKey: string
  audioHashSha256: string
  initialVoiceTranscript: string | null
  transcriptWhisper: string
  timestampsWhisper: string | null
  timeSpentSeconds: number
  isManuallyEdited: boolean
}

export interface SubmitQuestionAnswerResponse {
  submissionId: string
  ticketId: string
  questionId: string
  followUpIndex: number
  audioObjectKey: string
  audioHashSha256: string
  status: string
  canRequestFollowUp: boolean
}

export interface OfficialExamNextFollowUpResponse {
  hasMoreFollowUp: boolean
  followUpIndex: number
  maxFollowUpQuestions: number
  parentSubmissionId: string
  questionId: string
  followUpPrompt: string | null
}

export interface SubmitResponse {
  ticketId: string
  status: string
  submittedAt: string
  submissionCount: number
  isKioskLocked?: boolean
}

export interface SubmissionStatus {
  ticketId: string
  status: string
  submittedAt: string | null
  totalSubmissions: number
  pendingSubmissions: number
  gradedSubmissions: number
  failedSubmissions: number
}

export type TurnDraftStage =
  | 'RECORDED'
  | 'UPLOAD_AUTHORIZED'
  | 'AUDIO_UPLOADED'
  | 'ANSWER_SUBMITTED'

export interface TurnDraftMetadata {
  draftKey: string
  ticketId: string
  shiftId: string
  studentId: string
  questionId: string
  followUpIndex: number
  parentSubmissionId: string | null
  followUpPrompt: string | null
  initialVoiceTranscript: string | null
  transcriptWhisper: string
  timestampsWhisper: string | null
  timeSpentSeconds: number
  isManuallyEdited: boolean
  objectKey: string | null
  sha256: string | null
  stage: TurnDraftStage
  createdAt: string
  updatedAt: string
}

export interface TurnDraft {
  metadata: TurnDraftMetadata
  audio: Blob
}

export interface ActiveFollowUpState {
  questionId: string
  parentSubmissionId: string
  followUpIndex: number
  maxFollowUpQuestions: number
  prompt: string
}

export interface SpeechTranscriptionResult {
  text: string
  rawText?: string | null
  provider?: string | null
}

export type AntiCheatSeverity = 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL'

export interface AntiCheatLogRequest {
  ticketId: string
  shiftId: string
  studentId: string
  clientEventId: string
  eventType: string
  severity: AntiCheatSeverity
  details?: string | null
}

export type BlockReason = 'ANTI_CHEAT' | 'BYOD_CONFLICT' | 'SERVER_LOCKED'
