import apiClient from './api'
import type { MockExamSession, ExamSession, CandidateSubmission } from '@/types/exam.types'

export const examService = {
  /**
   * MF-01: Submit Practice Answer (Returns HTTP 202 Accepted)
   */
  async submitPracticeAnswer(payload: {
    questionId: string
    transcript: string
    audioDurationSeconds: number
  }): Promise<{ submissionId: string; status: string }> {
    const response = await apiClient.post<{ submissionId: string; status: string }>('/practice/submit', payload)
    return response.data
  },

  /**
   * MF-02: Start Mock Exam with Quota Check (Max 3/day/subject)
   */
  async startMockExam(subjectId: string): Promise<MockExamSession> {
    const response = await apiClient.post<MockExamSession>('/mock-exam/start', { subjectId })
    return response.data
  },

  /**
   * MF-02: Submit Mock Exam Answer
   */
  async submitMockExamAnswer(payload: {
    sessionId: string
    questionId: string
    transcript: string
  }): Promise<{ success: boolean }> {
    const response = await apiClient.post<{ success: boolean }>('/mock-exam/submit', payload)
    return response.data
  },

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
    const response = await apiClient.get<CandidateSubmission[]>(`/audit/sessions/${sessionId}/candidates`)
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
