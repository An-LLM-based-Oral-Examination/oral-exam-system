/** MF-04 contracts returned by the official-exam, proctor and student APIs. */
type OfficialExamInputMode = string
export type ExamAttemptType = 'FE' | 'RE'
export type ExamTicketStatus =
  'SCHEDULED' | 'IN_PROGRESS' | 'SUBMITTED' | 'AI_GRADED' | 'AUDITED' | 'PUBLISHED' | 'LOCKED'

export interface AuditFollowUpEvidenceItem {
  submissionId: string
  followUpIndex: number
  followUpPrompt: string | null
  transcriptWhisper: string | null
  audioR2Url: string | null
  audioHashSha256: string | null
  aiScore: number | null
  finalScore: number | null
  cotTrace: string | null
}

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
  submissionId?: string | null
  overrideReason?: string | null
  auditedByName?: string | null
  questionId?: string | null
  questionTitle?: string | null
  questionContent?: string | null
  questionImageUrl?: string | null
  questionImageDescription?: string | null
  followUpItems?: AuditFollowUpEvidenceItem[]
}

export interface ExamAntiCheatLog {
  id: string
  ticketId: string
  shiftId: string
  studentId: string
  clientEventId: string
  studentCode: string
  studentName: string
  eventType: string
  severity: string
  details: string | null
  violationCount: number
  occurredAt: string
}

export interface ExamSetQuestion {
  questionId: string
  orderIndex: number
  title: string
  content: string
  bloomLevel: string
  difficulty: string
  isDuplicate: boolean
  duplicateInSets: string[]
}

export interface ExamSet {
  id: string
  structureId: string
  courseId: string
  courseCode: string
  sessionId: string | null
  createdBy: string | null
  setCode: string
  status: string
  approvedBy: string | null
  reviewNotes: string | null
  submittedAt: string | null
  approvedAt: string | null
  handedOverAt: string | null
  generatedAt: string
  isActive: boolean
  questions: ExamSetQuestion[]
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

export interface PublishExamShiftResponse {
  success: boolean
  shiftId: string
  message: string
}

export interface OfficialExamSession {
  id: string
  courseId: string
  courseCode?: string | null
  courseName?: string | null
  semesterId?: string | null
  examAttemptType?: ExamAttemptType
  examStructureId?: string
  title: string
  examDate: string
  hasFollowUp: boolean
  maxFollowUpQuestions: number
  examInputMode: OfficialExamInputMode
  transcriptBufferSeconds: number
  status: string
  candidateCount?: number
  allocatedCandidateCount?: number
}

export interface GetOfficialExamSessionsParams {
  courseId?: string
  semesterId?: string
  examAttemptType?: ExamAttemptType
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
  requiredSetCount?: number
  slotDefinitions?: string | null
}

export interface GetExamStructuresParams {
  courseId?: string
  isActive?: boolean
}

export interface GetExamSetsParams {
  courseId: string
  sessionId?: string
  status?: string
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
  requiredSetCount?: number
  slotDefinitions?: string | null
}

export interface CreateOfficialExamSessionRequest {
  courseId: string
  semesterId?: string | null
  examStructureId: string | null
  title?: string | null
  examDate: string
  examAttemptType?: ExamAttemptType
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
  examinerUserId: string | null
  examinerName: string | null
  proctorName: string | null
  status: string
  isExamPublished?: boolean
  isGradesPublished?: boolean
  gradesPublishedAt?: string | null
  certificateHash?: string | null
  examAttemptType?: ExamAttemptType
  maxSeats?: number
  assignedTicketsCount?: number
  remainingSeats?: number
}

export interface GetExamShiftsParams {
  sessionId?: string
  proctorUserId?: string
  examinerUserId?: string
  status?: string
}

export interface CreateExamShiftRequest {
  sessionId: string
  shiftName?: string | null
  roomLab?: string | null
  startTime: string
  endTime: string
  proctorUserId: string
  examinerUserId: string
  maxSeats?: number
}

export interface UpdateExamShiftRequest {
  shiftName: string
  roomLab: string
  startTime: string
  endTime: string
  proctorUserId: string
  examinerUserId: string
  maxSeats?: number | null
}

export interface AssignExamTicketRequest {
  shiftId: string
  studentCode: string
  studentId?: string
  seatNumber: number
  ipAddress?: string | null
}

export interface ExamTicket {
  id: string
  shiftId: string
  studentId: string
  studentCode: string | null
  fullName?: string | null
  seatNumber: number
  ipAddress: string | null
  deviceId?: string | null
  deviceLockedAt?: string | null
  deviceUnlockCount?: number
  status: ExamTicketStatus | string
  checkedInAt: string
  examSetId: string | null
  setCode?: string | null
}

export interface BulkExamTicketImportIssue {
  sourceRow: number | null
  code: string
  field: string | null
  message: string
}

export interface BulkExamTicketImportPreviewRow {
  sourceRow: number
  studentCode: string
  fullName: string | null
  seatNumber: number | null
  ipAddress: string | null
  setCode: string | null
  isValid: boolean
  errors: BulkExamTicketImportIssue[]
  issues?: string[]
}

export interface BulkExamTicketImportPreview {
  shiftId: string
  totalRowCount: number
  validRowCount: number
  errorRowCount: number
  availableHandedOverSetCount: number
  rows: BulkExamTicketImportPreviewRow[]
  globalErrors: BulkExamTicketImportIssue[]
  reservedExamSetCodes?: string[]
  maxSeats?: number
  existingTicketCount?: number
  remainingSeats?: number
  canCommit: boolean
}

export interface BulkExamTicketImportCommitResult {
  shiftId: string
  createdTicketCount: number
  tickets: ExamTicket[]
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
  initialVoiceTranscript?: string | null
  transcriptWhisper?: string | null
  timestampsWhisper?: string | null
  timeSpentSeconds: number
  isManuallyEdited?: boolean
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
  submissionId?: string | null
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
  appealDeadlineAtUtc: string | null
  canAcknowledge: boolean
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
  publishedAt: string | null
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

export type CandidateImportMode = 'REPLACE' | 'APPEND'

export interface EligibleCourseSessionSummary {
  sessionId: string
  title: string
  status: string
  candidateCount: number
  allocatedCount: number
}

export interface SemesterEligibleExamCourse {
  courseId: string
  courseCode: string
  courseName: string
  semesterId: string
  semesterCode: string
  activeClassCount: number
  eligibleStudentCount: number
  maxStudentsPerRoom: number
  recommendedRoomCount: number
  activeExamStructureId: string | null
  activeExamStructureName: string | null
  handedOverExamSetCount: number
  hasFollowUp: boolean
  maxFollowUpQuestions: number
  existingSessionId: string | null
  existingSessionTitle: string | null
  existingSessionStatus: string | null
  feSession: EligibleCourseSessionSummary | null
  reSession: EligibleCourseSessionSummary | null
}

export interface BatchCreateOfficialExamSessionCourseItem {
  courseId: string
  title?: string | null
  examAttemptType?: ExamAttemptType
}

export interface BatchCreateSessionsRequest {
  examDate: string
  examAttemptType?: ExamAttemptType
  examInputMode?: OfficialExamInputMode | null
  transcriptBufferSeconds: number
  skipExisting?: boolean
  courses: BatchCreateOfficialExamSessionCourseItem[]
}

export interface BatchCreateSessionsResponse {
  semesterId: string
  createdCount: number
  sessions: OfficialExamSession[]
}

export interface CandidateImportIssue {
  sourceRow: number | null
  message: string
}

export interface CandidateImportPreviewRow {
  sourceRow: number
  studentCode: string
  studentId: string | null
  fullName: string | null
  email: string | null
  isEnrolledInSemester: boolean
  alreadyInSession: boolean
  isValid: boolean
  issues: string[]
}

export interface CandidateImportPreviewResponse {
  sessionId: string
  examAttemptType: ExamAttemptType
  mode: CandidateImportMode
  totalRows: number
  validCount: number
  invalidCount: number
  duplicateCount: number
  canCommit: boolean
  existingCandidateCount: number
  projectedTotalCandidates: number
  maxStudentsPerRoom: number
  recommendedTotalRooms: number
  issues: CandidateImportIssue[]
  rows: CandidateImportPreviewRow[]
}

export interface CandidateImportCommitResponse {
  sessionId: string
  examAttemptType: ExamAttemptType
  mode: CandidateImportMode
  importedCount: number
  totalCandidateCount: number
  recommendedTotalRooms: number
  importedAt: string
}

export interface SessionCandidateItem {
  studentId: string
  studentCode: string
  fullName: string
  email: string | null
  importedAt: string
  isAllocated: boolean
  ticketId: string | null
  shiftId: string | null
  roomLab: string | null
  seatNumber: number | null
  ticketStatus: string | null
}

export interface GetSessionCandidatesParams {
  search?: string
  allocatedOnly?: boolean
  pageNumber?: number
  pageSize?: number
}

export interface SessionCandidatesResponse {
  sessionId: string
  examAttemptType: ExamAttemptType
  totalCount: number
  allocatedCount: number
  unallocatedCount: number
  pageNumber: number
  pageSize: number
  items: SessionCandidateItem[]
}

export interface OfficialExamEnrollmentEstimate {
  sessionId: string
  courseId: string
  courseCode: string
  courseName: string
  semesterId: string
  semesterCode: string
  examAttemptType: ExamAttemptType
  candidateSource: string
  hasImportedCandidates: boolean
  importedCandidateCount: number
  semesterEnrolledCount: number
  eligibleStudentCount: number
  allocatedStudentCount: number
  unallocatedStudentCount: number
  maxStudentsPerRoom: number
  recommendedTotalRooms: number
  recommendedAdditionalRooms: number
  currentShiftRoomCount: number
  currentTotalCapacity: number
  currentAvailableSeats: number
  handedOverExamSetCount: number
}

export interface BatchOpenExamShiftRoomRequest {
  roomLab: string
  maxSeats: number
  proctorUserId: string
  examinerUserId: string
}

export interface BatchOpenShiftsRequest {
  shiftGroupName: string
  startTime: string
  endTime: string
  rooms: BatchOpenExamShiftRoomRequest[]
}

export interface BatchOpenShiftsResponse {
  sessionId: string
  shiftGroupName: string
  createdRoomCount: number
  totalCapacityCreated: number
  shifts: ExamShift[]
}

export interface AutoAllocateStudentsRequest {
  shiftIds?: string[] | null
  assignHandedOverExamSets: boolean
  dryRun: boolean
}

export interface AutoAllocatedRoom {
  shiftId: string
  roomLab: string
  maxSeats: number
  assignedBefore: number
  assignedNow: number
  totalAfter: number
}

export interface AutoAllocatedShiftGroup {
  startTime: string
  endTime: string
  isFinalBalancedShift: boolean
  totalAssignedInGroup: number
  rooms: AutoAllocatedRoom[]
}

export interface AutoAllocateStudentsResponse {
  sessionId: string
  dryRun: boolean
  totalEligibleStudents: number
  previouslyAllocatedCount: number
  newlyAllocatedCount: number
  shiftGroups: AutoAllocatedShiftGroup[]
}

export interface DistributeShiftTicketsRequest {
  allowRandomShuffle?: boolean
}

export interface DistributeShiftTicketsItem {
  ticketId: string
  studentCode: string
  seatNumber: number
  examSetId: string
}

export interface DistributeShiftTicketsResponse {
  shiftId: string
  totalDistributed: number
  message: string
  distributedTickets: DistributeShiftTicketsItem[]
}

export type OfficialShiftStatusValue = 'scheduled' | 'in_progress' | 'completed' | 'cancelled'

export interface UpdateShiftStatusRequest {
  status: OfficialShiftStatusValue
}

export interface ConfigureOfficialExamFollowUpRequest {
  hasFollowUp: boolean
  maxFollowUpQuestions: number
}

export interface UnlockTicketDeviceRequest {
  reason: string
}

export interface UnlockTicketDeviceResponse {
  ticketId: string
  shiftId: string
  studentCode: string
  seatNumber: number
  previousDeviceId: string | null
  deviceUnlockCount: number
  unlockedBy: string
  unlockedAt: string
  reason: string
}

export interface OfficialExamAudioPlaybackUrlResponse {
  ticketId: string
  submissionId: string | null
  downloadUrl: string
  audioHashSha256: string | null
  expiresAt: string
}

export interface OfficialExamShiftStaffMember {
  userId: string
  fullName: string
  email: string | null
  username: string | null
  role: string
  detail: string | null
  isCourseAssigned: boolean
}

export interface OfficialExamShiftStaffResponse {
  proctors: OfficialExamShiftStaffMember[]
  examiners: OfficialExamShiftStaffMember[]
}
