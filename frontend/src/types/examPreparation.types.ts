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
export type BlueprintInput = Omit<ExamBlueprint, 'id'>
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
  isActive: boolean
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
