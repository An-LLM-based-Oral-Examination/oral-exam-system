export type AppealStatus = 'PENDING' | 'IN_REVIEW' | 'APPROVED' | 'REJECTED' | 'CANCELLED' | string

export interface Appeal {
  id: string
  ticketId: string
  studentId: string
  studentCode: string
  studentName: string
  sessionId: string
  sessionTitle: string
  submissionId: string | null
  reason: string
  status: AppealStatus
  assignedTo?: string
  assignedToName: string
  reviewedBy: string | null
  reviewedByName: string | null
  decision: string | null
  originalScore: number | null
  proposedScore: number | null
  reviewNotes: string | null
  createdAt: string
  updatedAt: string
  resolvedAt: string | null
}

export interface CreateAppealRequest {
  ticketId: string
  studentId?: string | null
  submissionId?: string | null
  reason: string
}

export interface GetAppealsParams {
  status?: string
  sessionId?: string
  studentId?: string
  assignedTo?: string
}

export interface ReviewAppealRequest {
  decision: string
  proposedScore?: number | null
  reviewNotes: string
  reviewerId?: string | null
}

export interface AssignAppealLecturerRequest {
  lecturerId: string
}

export interface RegradeAppealRequest {
  newScore: number
  reviewNotes: string
}

export interface LecturerAllocationSummary {
  lecturerId: string
  lecturerName: string
  email: string
  assignedCount: number
}

export interface AppealAssignmentProposalItem {
  appealId: string
  ticketId: string
  studentCode: string
  studentName: string
  sessionId: string
  sessionTitle: string
  initialExaminerUserId: string | null
  initialExaminerName: string | null
  proposedLecturerId: string | null
  proposedLecturerName: string | null
  originalScore: number | null
  reason: string
}

export interface AppealAllocationProposal {
  totalAppeals: number
  totalLecturers: number
  exceedsStandardQuota: boolean
  lecturerSummaries: LecturerAllocationSummary[]
  items: AppealAssignmentProposalItem[]
}

export interface AppealAssignmentItem {
  appealId: string
  lecturerId: string
}

export interface BatchAssignAppealsRequest {
  assignments: AppealAssignmentItem[]
}

export interface BatchAssignAppealsResponse {
  totalAssigned: number
  assignedCount: number
  failedCount: number
  assignedAppeals: Appeal[]
}
