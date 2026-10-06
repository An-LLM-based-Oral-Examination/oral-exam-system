import apiClient from './api.client'
import type { Question, CreateQuestionRequest, Subject, Chapter } from '@/types/rubric.types'

export const questionService = {
  /**
   * Fetch paginated or filtered questions
   */
  async getQuestions(subjectId?: string, bloomLevel?: string): Promise<Question[]> {
    const params = new URLSearchParams()
    if (subjectId) params.append('subjectId', subjectId)
    if (bloomLevel) params.append('bloomLevel', bloomLevel)
    const response = await apiClient.get<Question[]>(`/questions?${params.toString()}`)
    return response.data
  },

  /**
   * Get single question by ID
   */
  async getQuestionById(id: string): Promise<Question> {
    const response = await apiClient.get<Question>(`/questions/${id}`)
    return response.data
  },

  /**
   * Create question with Rubric criteria (sum must be 10.0)
   */
  async createQuestion(payload: CreateQuestionRequest): Promise<Question> {
    const response = await apiClient.post<Question>('/questions', payload)
    return response.data
  },

  /**
   * Fetch subjects for dropdowns
   */
  async getSubjects(): Promise<Subject[]> {
    const response = await apiClient.get<Subject[]>('/subjects')
    return response.data
  },

  /**
   * Fetch chapters by subject ID
   */
  async getChapters(subjectId: string): Promise<Chapter[]> {
    const response = await apiClient.get<Chapter[]>(`/subjects/${subjectId}/chapters`)
    return response.data
  },
}
