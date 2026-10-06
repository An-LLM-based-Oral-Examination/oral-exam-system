import apiClient from './api.client'

export const practiceService = {
  /**
   * MF-01: Submit Practice Answer (Returns HTTP 202 Accepted)
   */
  async submitPracticeAnswer(payload: {
    questionId: string
    transcript: string
    audioDurationSeconds: number
  }): Promise<{ submissionId: string; status: string }> {
    const response = await apiClient.post<{ submissionId: string; status: string }>(
      '/practice/submit',
      payload,
    )
    return response.data
  },
}
