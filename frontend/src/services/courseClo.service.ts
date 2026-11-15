import apiClient from './api.client'
import type { CourseClo, CreateCourseCloInput, ManagedCloCourse, UpdateCourseCloInput } from '@/types/courseClo.types'

export const courseCloService = {
  async getManagedCourses(): Promise<ManagedCloCourse[]> {
    return (await apiClient.get<ManagedCloCourse[]>('/courses/managed-for-clos')).data
  },
  async list(courseId: string): Promise<CourseClo[]> {
    return (await apiClient.get<CourseClo[]>(`/courses/${encodeURIComponent(courseId)}/clos`)).data
  },
  async create(courseId: string, input: CreateCourseCloInput): Promise<CourseClo> {
    return (await apiClient.post<CourseClo>(`/courses/${encodeURIComponent(courseId)}/clos`, input)).data
  },
  async update(courseId: string, cloId: string, input: UpdateCourseCloInput): Promise<CourseClo> {
    return (await apiClient.put<CourseClo>(`/courses/${encodeURIComponent(courseId)}/clos/${encodeURIComponent(cloId)}`, input)).data
  },
  async remove(courseId: string, cloId: string): Promise<void> {
    await apiClient.delete(`/courses/${encodeURIComponent(courseId)}/clos/${encodeURIComponent(cloId)}`)
  },
}
