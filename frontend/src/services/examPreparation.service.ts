import apiClient from './api.client'
import type {
  BlueprintInput,
  DuplicatePaperQuestion,
  ExamBlueprint,
  ExamPaper,
  ExamPreparation,
  PaperDecision,
  PaperRevision,
  PaperSimilarityReport,
  BankPromotionCandidate,
  ExamCapacityReport,
  PaperDecisionResult,
  AvailablePaperQuestion,
  BankCurationCandidatesResponse,
  DecideBankCurationRequest,
  DecideBankCurationResult,
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
  async request(
    sessionId: string,
    input: { requiredSetCount: number; deadline: string; notes?: string | null },
  ) {
    return (
      await apiClient.post<{
        id: string
        sessionId: string
        requiredSetCount: number
        deadline: string
      }>(`${root}/sessions/${sessionId}/preparation`, input)
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
      await apiClient.post<PaperDecisionResult>(
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
  async similarity(id: string, signal?: AbortSignal) {
    return (
      await apiClient.get<PaperSimilarityReport>(`${root}/exam-sets/${id}/similarity`, { signal })
    ).data
  },
  async bankCandidates(id: string, signal?: AbortSignal) {
    return (
      await apiClient.get<BankPromotionCandidate[]>(`${root}/exam-sets/${id}/bank-candidates`, {
        signal,
      })
    ).data
  },
  async promoteQuestions(id: string, questionIds: string[]) {
    return (await apiClient.post(`${root}/exam-sets/${id}/bank-questions`, { questionIds }))
      .data as unknown
  },
  async capacity(sessionId: string, input: BlueprintInput, signal?: AbortSignal) {
    return (
      await apiClient.post<ExamCapacityReport>(
        `${root}/sessions/${sessionId}/preparation/capacity-preview`,
        input,
        { signal },
      )
    ).data
  },
  async updateReuse(sessionId: string, reusePercent: number) {
    return (
      await apiClient.put(`${root}/sessions/${sessionId}/preparation/reuse-policy`, {
        reusePercent,
      })
    ).data as unknown
  },
  async availableQuestions(
    structureId: string,
    sessionId: string,
    signal?: AbortSignal,
    excludeExamSetId?: string,
  ) {
    return (
      await apiClient.get<{ items: AvailablePaperQuestion[] }>(
        `${root}/structures/${structureId}/available-questions`,
        { params: { sessionId, excludeExamSetId }, signal },
      )
    ).data.items
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
  async createSamplePaper(input: {
    structureId: string
    courseId: string
    sessionId?: string | null
    setCode: string
    questionIds: string[]
  }) {
    return (
      await apiClient.post<ExamPaper>(`${root}/exam-sets`, {
        ...input,
        isSampleSet: true,
      })
    ).data
  },
  async deleteSamplePaper(id: string) {
    await apiClient.delete(`${root}/exam-sets/${id}`)
  },
  async archiveDraftPaper(id: string) {
    return (await apiClient.delete(`${root}/preparation/exam-sets/${id}`)).data as unknown
  },
  async savePaper(id: string, questionIds: string[]) {
    return (await apiClient.put(`${root}/exam-sets/${id}/draft`, { questionIds })).data as unknown
  },
  async createQuestion(input: CreateDraftQuestionRequest) {
    return (
      await apiClient.post<{ questionId: string }>('/questions', { ...input, usageScope: 'exam' })
    ).data
  },
  async getBankCurationCandidates(courseId: string, band?: string, signal?: AbortSignal) {
    return (
      await apiClient.get<BankCurationCandidatesResponse>(
        `${root}/courses/${courseId}/bank-curation-candidates`,
        {
          params: band && band !== 'ALL' ? { band } : undefined,
          signal,
        },
      )
    ).data
  },
  async decideBankCuration(courseId: string, payload: DecideBankCurationRequest) {
    return (
      await apiClient.post<DecideBankCurationResult>(
        `${root}/courses/${courseId}/bank-curation/decide`,
        payload,
      )
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
