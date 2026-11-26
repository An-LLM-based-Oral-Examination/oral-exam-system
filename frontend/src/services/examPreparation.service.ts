import apiClient from './api.client'
import type {
  BlueprintInput,
  DuplicatePaperQuestion,
  ExamBlueprint,
  ExamPaper,
  ExamPreparation,
  PaperDecision,
  PaperRevision,
} from '@/types/examPreparation.types'
import type { CreateDraftQuestionRequest } from '@/types/rubric.types'
const root = '/official-exams'
export const examPreparationService = {
  async list(sessionId?: string, signal?: AbortSignal) {
    return (
      await apiClient.get<ExamPreparation[]>(`${root}/preparations`, {
        params: { sessionId },
        signal,
      })
    ).data
  },
  async papers(courseId: string, sessionId: string, signal?: AbortSignal) {
    return (
      await apiClient.get<ExamPaper[]>(`${root}/exam-sets`, {
        params: { courseId, sessionId },
        signal,
      })
    ).data
  },
  async blueprint(id: string, signal?: AbortSignal) {
    return (
      await apiClient.get<ExamBlueprint>(`/exam-structures/${encodeURIComponent(id)}`, { signal })
    ).data
  },
  async configure(sessionId: string, input: BlueprintInput) {
    return (
      await apiClient.post<{ structureId: string }>(
        `${root}/sessions/${sessionId}/preparation/blueprints`,
        input,
      )
    ).data
  },
  async assignBatch(
    sessionId: string,
    input: {
      structureId: string
      lecturers: { lecturerId: string; setCount: number }[]
      deadline: string
      notes: string
    },
  ) {
    return (
      await apiClient.post<{ count: number; ids: string[] }>(
        `${root}/sessions/${sessionId}/preparation/assignments/batch`,
        input,
      )
    ).data
  },
  async assign(
    sessionId: string,
    input: {
      structureId: string
      lecturerId: string
      setCount: number
      deadline: string
      notes: string
    },
  ) {
    return (
      await apiClient.post<{ id: string }>(
        `${root}/sessions/${sessionId}/preparation/assignments`,
        input,
      )
    ).data
  },
  async decide(id: string, decision: PaperDecision, notes: string) {
    return (
      await apiClient.post<{ status?: string; duplicateWarnings?: string[] }>(
        `${root}/exam-sets/${id}/${decision}`,
        decision === 'approve' ? { reviewNotes: notes || null } : { notes },
      )
    ).data
  },
  async duplicates(id: string, signal?: AbortSignal) {
    return (
      await apiClient.get<DuplicatePaperQuestion[]>(`${root}/exam-sets/${id}/duplicates`, {
        signal,
      })
    ).data
  },
  async history(id: string, signal?: AbortSignal) {
    return (await apiClient.get<PaperRevision[]>(`${root}/exam-sets/${id}/history`, { signal }))
      .data
  },
  async createPaper(input: {
    assignmentId: string
    structureId: string
    courseId: string
    sessionId: string
    setCode: string
    questionIds: string[]
  }) {
    return (await apiClient.post<ExamPaper>(`${root}/exam-sets`, input)).data
  },
  async savePaper(id: string, questionIds: string[]) {
    return (await apiClient.put(`${root}/exam-sets/${id}/draft`, { questionIds })).data as unknown
  },
  async createQuestion(input: CreateDraftQuestionRequest) {
    return (
      await apiClient.post<{ questionId: string }>('/questions', { ...input, usageScope: 'exam' })
    ).data
  },
  async submitAssignment(id: string, examSetIds: string[]) {
    return (
      await apiClient.post<{ submittedCount: number }>(
        `${root}/preparation/assignments/${id}/submit-review`,
        { examSetIds },
      )
    ).data
  },
  async handOver(sessionId: string, examSetIds: string[]) {
    return (
      await apiClient.post(`${root}/sessions/${sessionId}/preparation/hand-over`, { examSetIds })
    ).data as unknown
  },
}
