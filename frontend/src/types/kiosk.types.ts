import type {
  AuditEvidenceItem,
  AuditFollowUpEvidenceItem,
  ExamAntiCheatLog,
  ExamAttemptType,
  ExamTicket,
  ExamTicketStatus,
  LockExamTicketResponse,
  OverrideCriterionScore,
  OverrideExamScoreRequest,
  OverrideExamScoreResponse,
  StudentWorkstationTicket,
  WorkstationExamQuestion,
} from '@/types/officialExam.types'
import type { ProctorSeatItem } from '@/types/proctor.types'

export type {
  AuditEvidenceItem,
  AuditFollowUpEvidenceItem,
  ExamAntiCheatLog,
  ExamAttemptType,
  ExamTicket,
  ExamTicketStatus,
  LockExamTicketResponse,
  OverrideCriterionScore,
  OverrideExamScoreRequest,
  OverrideExamScoreResponse,
  ProctorSeatItem,
  StudentWorkstationTicket,
  WorkstationExamQuestion,
}

export interface KioskRequestHeaders {
  deviceId?: string | null
  kioskSessionToken?: string | null
  kioskVersion?: string | null
}

export interface WorkstationFollowUpItem {
  submissionId: string
  parentSubmissionId: string | null
  examQuestionId: string
  followUpIndex: number
  followUpPrompt: string | null
  generatedFollowUpPrompt?: string | null
  transcriptWhisper: string | null
  audioObjectKey?: string | null
  audioR2Url: string | null
  audioHashSha256: string | null
  aiScore: number | null
  finalScore: number | null
}

export interface KioskWorkstationTicket extends StudentWorkstationTicket {
  examAttemptType: ExamAttemptType
  isExamPublished: boolean
  isKioskLocked: boolean
  deviceId: string | null
  deviceLockedAt: string | null
  deviceUnlockCount: number
  violationCount: number
  maxViolations: number
  lockThreshold: number
  remainingStrikes: number
  serverNowUtc: string
  followUps: WorkstationFollowUpItem[]
}

export interface KioskTicketState {
  ticketId: string
  shiftId: string
  status: ExamTicketStatus | string
  isExamPublished: boolean
  isKioskLocked: boolean
  violationCount: number
  maxViolations: number
  lockThreshold: number
  remainingStrikes: number
  serverNowUtc: string
  deviceId: string | null
  deviceLockedAt: string | null
  deviceUnlockCount: number
  followUps: WorkstationFollowUpItem[]
}

export interface KioskCheckInRequest {
  deviceId?: string | null
  kioskVersion?: string | null
}

export interface KioskCheckInResponse extends ExamTicket {
  deviceId: string | null
  deviceLockedAt: string | null
  deviceUnlockCount: number
  activeSessionToken: string | null
}

export interface LogAntiCheatEventRequest {
  ticketId: string
  shiftId: string
  studentId?: string | null
  eventType: string
  severity: string
  details?: string | null
  clientEventId?: string
}

export interface LogAntiCheatEventResponse extends ExamAntiCheatLog {
  isLocked: boolean
  maxViolations: number
  lockThreshold: number
  remainingStrikes: number
}

export interface AuthorizeQuestionAudioUploadRequest {
  followUpIndex: number
  expectedSha256Base64?: string | null
}

export interface SubmitOfficialQuestionAnswerRequest {
  followUpIndex: number
  parentSubmissionId?: string | null
  audioObjectKey: string
  audioHashSha256: string
  initialVoiceTranscript?: string | null
  transcriptWhisper?: string | null
  timestampsWhisper?: string | null
  timeSpentSeconds: number
  isManuallyEdited?: boolean
}

export interface SubmitOfficialQuestionAnswerResponse {
  submissionId: string
  ticketId: string
  questionId: string
  followUpIndex: number
  audioHashSha256: string
  status: string
  canRequestFollowUp: boolean
}

export interface NextOfficialFollowUpResponse {
  hasMoreFollowUp: boolean
  followUpIndex: number
  maxFollowUpQuestions: number
  parentSubmissionId: string
  questionId: string
  followUpPrompt: string | null
}
