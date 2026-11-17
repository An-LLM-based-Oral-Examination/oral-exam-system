import apiClient from './api.client'
import { classService } from './class.service'
import { lecturerAssignmentService } from './lecturerAssignment.service'
import { pickCurrentSemester } from '@/lib/semester'
import type {
  CourseClo,
  CreateCourseCloInput,
  ManagedCloCourse,
  UpdateCourseCloInput,
} from '@/types/courseClo.types'

export const courseCloService = {
  async getManagedCourses(): Promise<ManagedCloCourse[]> {
    return (await apiClient.get<ManagedCloCourse[]>('/courses/managed-for-clos')).data
  },
  async getCurrentSemesterManagedCourses(): Promise<ManagedCloCourse[]> {
    const [courses, offerings, semesters] = await Promise.all([
      courseCloService.getManagedCourses(),
      lecturerAssignmentService.offerings(),
      classService.listSemesters(),
    ])
    const semester = pickCurrentSemester(semesters, false)
    if (!semester) return []
    const courseIds = new Set(
      offerings
        .filter((offering) => offering.semesterId === semester.id && offering.isOfferingEnabled)
        .map((offering) => offering.courseId),
    )
    return courses.filter((course) => courseIds.has(course.id))
  },
  async list(courseId: string, activeOnly?: boolean): Promise<CourseClo[]> {
    return (
      await apiClient.get<CourseClo[]>(`/courses/${encodeURIComponent(courseId)}/clos`, {
        params: { activeOnly },
      })
    ).data
  },
  async create(courseId: string, input: CreateCourseCloInput): Promise<CourseClo> {
    return (await apiClient.post<CourseClo>(`/courses/${encodeURIComponent(courseId)}/clos`, input))
      .data
  },
  async update(courseId: string, cloId: string, input: UpdateCourseCloInput): Promise<CourseClo> {
    return (
      await apiClient.put<CourseClo>(
        `/courses/${encodeURIComponent(courseId)}/clos/${encodeURIComponent(cloId)}`,
        input,
      )
    ).data
  },
  async remove(courseId: string, cloId: string): Promise<void> {
    await apiClient.delete(
      `/courses/${encodeURIComponent(courseId)}/clos/${encodeURIComponent(cloId)}`,
    )
  },
}
