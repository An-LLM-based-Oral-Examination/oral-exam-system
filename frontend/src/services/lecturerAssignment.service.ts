import apiClient from './api.client'
import type {
  AssignedLecturer,
  AssignmentOffering,
  LecturerDirectoryItem,
  SyncLecturerAssignmentsResponse,
} from '@/types/lecturerAssignment.types'

export const lecturerAssignmentService = {
  async offerings(signal?: AbortSignal): Promise<AssignmentOffering[]> {
    const { data } = await apiClient.get<AssignmentOffering[]>('/lecturer/course-offerings', {
      signal,
    })
    return data
  },
  async assignments(offeringId: string, signal?: AbortSignal): Promise<AssignedLecturer[]> {
    const { data } = await apiClient.get<AssignedLecturer[]>(
      `/course-offerings/${offeringId}/lecturers`,
      { signal },
    )
    return data
  },
  async search(
    offeringId: string,
    search: string,
    signal?: AbortSignal,
  ): Promise<LecturerDirectoryItem[]> {
    const { data } = await apiClient.get<LecturerDirectoryItem[]>(
      `/course-offerings/${offeringId}/lecturer-directory`,
      {
        params: { search },
        signal,
      },
    )
    return data
  },
  async sync(
    offeringId: string,
    lecturerUserIds: string[],
  ): Promise<SyncLecturerAssignmentsResponse> {
    const { data } = await apiClient.put<SyncLecturerAssignmentsResponse>(
      `/course-offerings/${offeringId}/lecturers`,
      {
        lecturerUserIds: [...new Set(lecturerUserIds)],
      },
    )
    return data
  },
}
