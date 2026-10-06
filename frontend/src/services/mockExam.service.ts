import apiClient from './api.client'
import type { MockExamSession } from '@/types/mockExam.types'

export const mockExamService = {
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
}
