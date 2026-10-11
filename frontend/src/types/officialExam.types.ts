/** MF-04 contracts returned by OfficialExamsController. */
export type ExamTicketStatus =
  'SCHEDULED' | 'IN_PROGRESS' | 'SUBMITTED' | 'AI_GRADED' | 'AUDITED' | 'PUBLISHED' | 'LOCKED'

export interface AuditEvidenceItem {
  ticketId: string
  studentCode: string
  fullName: string
  seatNumber: number
  status: ExamTicketStatus
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
}

export interface PublishGradesResponse {
  shiftId: string
  totalPublished: number
  publishedAt: string
  message: string
}

export interface PublishGradesRequest {
  lecturerId?: string
}
