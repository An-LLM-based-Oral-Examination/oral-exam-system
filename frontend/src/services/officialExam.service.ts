import apiClient from './api.client'
import type { ExamSession, CandidateSubmission } from '@/types/officialExam.types'

export const officialExamService = {
  /**
   * MF-04: Get Exam Sessions for Lecturer Audit Portal
   */
  async getAuditExamSessions(): Promise<ExamSession[]> {
    const response = await apiClient.get<ExamSession[]>('/audit/sessions')
    return response.data
  },

  /**
   * MF-04: Get Candidates for a specific exam session
   */
  async getSessionCandidates(sessionId: string): Promise<CandidateSubmission[]> {
    const response = await apiClient.get<CandidateSubmission[]>(
      `/audit/sessions/${sessionId}/candidates`,
    )
    return response.data
  },

  /**
   * MF-04: Lecturer Override Score
   */
  async overrideScore(payload: {
    submissionId: string
    criteriaScores: { criterionId: string; score: number }[]
    overrideReason: string
  }): Promise<void> {
    await apiClient.put('/audit/override', payload)
  },

  /**
   * MF-04: One-Way Grade Lock (is_locked = true)
   */
  async lockScore(submissionId: string): Promise<void> {
    await apiClient.post('/audit/lock', { submissionId })
  },
}
