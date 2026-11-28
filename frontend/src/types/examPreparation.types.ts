export type PaperStatus =
  'DRAFT' | 'SUBMITTED_FOR_REVIEW' | 'NEEDS_REVISION' | 'REJECTED' | 'OFFICIAL' | 'HANDED_OVER'
export type PreparationStatus =
  'REQUESTED' | 'CONFIGURED' | 'IN_PROGRESS' | 'READY_FOR_HANDOVER' | 'HANDED_OVER'
export interface ExamBlueprint {
  id: string
  name: string
  totalQuestions: number
  durationMinutes: number
  easyCount: number
  mediumCount: number
  hardCount: number
  cloIds: string[]
}
export type BlueprintInput = Omit<ExamBlueprint, 'id'> & { reusePercent?: number }
export interface ExamAuthoringAssignment {
  id: string
  lecturerId: string
  lecturerName: string
  structureId: string
  setCount: number
  deadline: string
  notes: string | null
  assignedAt: string
  blueprint: Omit<ExamBlueprint, 'id'>
  draft: number
  pending: number
  needsRevision: number
  rejected: number
  approved: number
  handedOver: number
}
export interface ExamPreparation {
  id: string
  sessionId: string
  title: string
  courseId: string
  courseCode: string
  semesterId: string
  semesterCode: string
  examStructureId: string | null
  status: PreparationStatus
  requiredSetCount: number
  reusePercent?: number
  approvedSetCount: number
  handedOverSetCount: number
  deadline: string
  notes: string | null
  requestedAt: string
  handedOverAt: string | null
  assignments: ExamAuthoringAssignment[]
}
export interface PaperQuestion {
  source?: string
  isBankQuestion?: boolean
  keyPoints?: string
  questionId: string
  sourceQuestionId: string | null
  orderIndex: number
  title: string
  content: string
  bloomLevel: string
  difficulty: string
  cloId: string | null
  sampleAnswer: string | null
  imageUrl: string | null
  imageDescription: string | null
  rubricDescription?: string | null
  rubricName: string | null
  totalMaxScore: number
  isDuplicate?: boolean
  overlapSeverity?: string
  duplicateInSets?: string[]
  bankCurationStatus?: string
  criteria: {
    name: string
    description: string | null
    maxScore: number
    weight: number
    bloomLevel: string | null
    orderIndex: number
  }[]
}
export interface ExamPaper {
  id: string
  assignmentId: string | null
  structureId: string
  courseId: string
  sessionId: string | null
  createdBy: string | null
  setCode: string
  status: PaperStatus
  reviewNotes: string | null
  submittedAt: string | null
  approvedAt: string | null
  handedOverAt: string | null
  generatedAt: string
  isSampleSet?: boolean
  isActive: boolean
  overlapSeverity?: string
  yellowOverlapRatio?: number
  yellowOverlapPercent?: number
  redOverlapQuestionCount?: number
  yellowOverlapQuestionCount?: number
  questions: PaperQuestion[]
}
export interface DuplicatePaperQuestion {
  questionId: string
  questionTitle: string
  setCode: string
  sessionId: string
  sessionTitle: string
  semesterCode: string
}
export interface PaperRevision {
  id: string
  sequence: number
  action: string
  actorId: string
  createdAt: string
  notes: string | null
  snapshot: string
}
export type PaperDecision = 'approve' | 'request-changes' | 'reject'

export interface PaperSimilarityReport {
  examSetId: string
  similarityPercent: number
  comparedPaper: {
    examSetId: string
    setCode: string
    sessionId: string
    sessionTitle: string
    semesterCode: string
    questionCount: number
  } | null
  questions: {
    questionId: string
    orderIndex: number
    percent: number
    band: string
    matches: {
      questionId: string
      examSetId: string
      setCode: string
      sessionTitle: string
      semesterCode: string
      title: string
      content: string
      percent: number
      sameSource: boolean
      orderIndex: number
    }[]
  }[]
}
export interface BankPromotionCandidate {
  questionId: string
  sourceQuestionId: string
  title: string
  similarityPercent: number
  band: string
  recommendation: string
  comparedQuestionId: string | null
  comparedTitle: string | null
  comparedContent: string | null
}
export interface ExamCapacityReport {
  requiredSetCount: number
  reusePercent: number
  maxUses: number
  band: string
  additionalQuestionsRequired: number
  suggestedReusePercent: number | null
  warning: string | null
}
export interface AvailablePaperQuestion {
  questionId: string
  sourceQuestionId: string | null
  usedCount: number
  maxUses: number
  remainingUses: number
  canSelect: boolean
  disabledReason: string | null
  isYellowPendingByOtherLecturer?: boolean
  yellowWarningLabel?: string | null
  yellowThresholdPercent?: number
}
export interface PaperDecisionResult {
  status?: string
  duplicateWarnings?: string[]
  similarity?: PaperSimilarityReport
  bankCandidates?: BankPromotionCandidate[]
}
export type BankCurationBand = 'RED_80_PLUS' | 'YELLOW_70_79' | 'CLEAR'
export type BankCurationDecision = 'CURATED_INTO_BANK' | 'EXCLUDED_FROM_BANK'
export interface BankCurationSimilarityMatch {
  comparedQuestionId: string
  title: string
  content: string
  difficulty: string
  bloomLevel: string
  bankCurationStatus: string
  isFromExistingBank: boolean
  sourceSetCodes: string[]
  similarityScore: number
  similarityPercent: number
  band: BankCurationBand | string
}
export interface BankCurationCandidate {
  questionId: string
  title: string
  content: string
  difficulty: string
  bloomLevel: string
  cloId: string | null
  cloCode: string | null
  approvalStatus: string
  bankCurationStatus: string
  submittedBy: string | null
  submittedByName: string | null
  sourceSetCodes: string[]
  createdAt: string
  maxSimilarityScore: number
  maxSimilarityPercent: number
  highestBand: BankCurationBand | string
  matches: BankCurationSimilarityMatch[]
}
export interface BankCurationCandidatesResponse {
  courseId: string
  courseCode: string
  totalPendingCount: number
  redCount: number
  yellowCount: number
  clearCount: number
  items: BankCurationCandidate[]
}
export interface DecideBankCurationRequest {
  questionIds: string[]
  decision: BankCurationDecision
  notes?: string
}
export interface DecideBankCurationResult {
  courseId: string
  decision: BankCurationDecision
  processedCount: number
  questionIds: string[]
  curatedAt: string
}
