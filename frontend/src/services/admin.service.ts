import apiClient from './api.client'

export interface AdminUserItem {
  id: string
  email: string
  fullName: string
  studentCode?: string | null
  username?: string | null
  role: string
  isActive: boolean
  mustChangePassword: boolean
  createdAt: string
  updatedAt: string
}

export interface PagedAdminUsersResponse {
  items: AdminUserItem[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface CreateUserPayload {
  email: string
  fullName: string
  password: string
  role: string
  studentCode?: string
  department?: string
}

export interface SystemConfigItem {
  id: string
  key: string
  value: string
  description?: string | null
  updatedAt: string
}

export interface SemesterItem {
  id: string
  code: string
  name: string
  startDate: string
  endDate: string
  isActive: boolean
  coursesCount?: number
  classesCount?: number
  createdAt: string
}

export interface SaveSemesterPayload {
  code: string
  name: string
  startDate: string
  endDate: string
  isActive?: boolean
}

export interface SemesterCourseOfferingItem {
  courseId: string
  code: string
  name: string
  description?: string | null
  credits: number
  academicProgramId?: string | null
  academicProgramCode?: string | null
  isCourseActive: boolean
  courseOfferingId?: string | null
  isSelected: boolean
}

export interface SyncSemesterCourseOfferingsResult {
  semesterId: string
  createdCount: number
  enabledCount: number
  disabledCount: number
  courses: SemesterCourseOfferingItem[]
}

export interface AdminCourseItem {
  id: string
  code: string
  name: string
  description?: string | null
  credits: number
  semesterId?: string
  departmentHeadUserId?: string | null
  departmentHeadName?: string | null
  maxMockExamsPerDay: number
  mockExamDurationMinutes: number
  mockExamTotalQuestions: number
  hasFollowUp: boolean
  examInputMode: string
  isActive: boolean
}

export interface CreateCoursePayload {
  code: string
  name: string
  credits: number
  description?: string
  departmentHeadUserId?: string
  semesterId?: string
  maxMockExamsPerDay?: number
  mockExamDurationMinutes?: number
  mockExamTotalQuestions?: number
  isActive?: boolean
}

export interface UpdateCoursePayload {
  code: string
  name: string
  credits: number
  description?: string
  departmentHeadUserId?: string
  semesterId?: string
  maxMockExamsPerDay?: number
  mockExamDurationMinutes?: number
  mockExamTotalQuestions?: number
  isActive?: boolean
}

export interface AuditLogItem {
  id: string
  userId?: string | null
  userEmail?: string | null
  userFullName?: string | null
  action: string
  entityName: string
  entityId?: string | null
  oldValues?: string | null
  newValues?: string | null
  ipAddress?: string | null
  createdAt: string
}

export interface PagedAuditLogsResponse {
  items: AuditLogItem[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface AdminNotificationItem {
  id: string
  userId: string
  userEmail: string
  userFullName: string
  userRole: string
  title: string
  message: string
  type: string
  isRead: boolean
  readAt?: string | null
  createdAt: string
}

export interface PagedAdminNotificationsResponse {
  items: AdminNotificationItem[]
  totalCount: number
  unreadTotalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface CreateAdminNotificationPayload {
  targetScope: 'ALL_ACTIVE' | 'ROLE' | 'USER'
  targetRole?: string
  targetUserId?: string
  type: string
  title: string
  message: string
}

export interface DispatchAdminNotificationResult {
  createdCount: number
  targetScope: string
  targetRole?: string | null
  targetUserId?: string | null
  type: string
  title: string
  createdAt: string
}

export const adminService = {
  async getUsers(params?: {
    search?: string
    role?: string
    isActive?: boolean
    page?: number
    pageSize?: number
  }): Promise<PagedAdminUsersResponse> {
    const response = await apiClient.get<PagedAdminUsersResponse>('/admin/users', { params })
    return response.data
  },

  async createUser(payload: CreateUserPayload): Promise<AdminUserItem> {
    const response = await apiClient.post<AdminUserItem>('/admin/users', payload)
    return response.data
  },

  async updateUserStatus(
    id: string,
    payload: { isActive: boolean; reason?: string },
  ): Promise<AdminUserItem> {
    const response = await apiClient.put<AdminUserItem>(`/admin/users/${id}/status`, payload)
    return response.data
  },

  async updateUserRole(id: string, payload: { role: string }): Promise<AdminUserItem> {
    const response = await apiClient.put<AdminUserItem>(`/admin/users/${id}/role`, payload)
    return response.data
  },

  async resetUserPassword(
    id: string,
    payload: { temporaryPassword: string; reason?: string },
  ): Promise<AdminUserItem> {
    const response = await apiClient.post<AdminUserItem>(
      `/admin/users/${id}/reset-password`,
      payload,
    )
    return response.data
  },

  async getSystemConfigs(): Promise<SystemConfigItem[]> {
    const response = await apiClient.get<SystemConfigItem[]>('/admin/system-configs')
    return response.data
  },

  async updateSystemConfig(
    key: string,
    payload: { value: string; description?: string },
  ): Promise<SystemConfigItem> {
    const response = await apiClient.put<SystemConfigItem>(`/admin/system-configs/${key}`, payload)
    return response.data
  },

  async getAuditLogs(params?: {
    entityName?: string
    action?: string
    userId?: string
    page?: number
    pageSize?: number
  }): Promise<PagedAuditLogsResponse> {
    const response = await apiClient.get<PagedAuditLogsResponse>('/admin/audit-logs', { params })
    return response.data
  },

  async getNotifications(params?: {
    search?: string
    type?: string
    role?: string
    isRead?: boolean
    page?: number
    pageSize?: number
  }): Promise<PagedAdminNotificationsResponse> {
    const response = await apiClient.get<PagedAdminNotificationsResponse>('/admin/notifications', {
      params,
    })
    return response.data
  },

  async createNotification(
    payload: CreateAdminNotificationPayload,
  ): Promise<DispatchAdminNotificationResult> {
    const response = await apiClient.post<DispatchAdminNotificationResult>(
      '/admin/notifications',
      payload,
    )
    return response.data
  },

  async deleteNotification(id: string): Promise<void> {
    await apiClient.delete(`/admin/notifications/${id}`)
  },

  async getSemesters(isActive?: boolean): Promise<SemesterItem[]> {
    const response = await apiClient.get<SemesterItem[]>('/semesters', {
      params: isActive !== undefined ? { isActive } : undefined,
    })
    return Array.isArray(response.data) ? response.data : []
  },

  async createSemester(payload: SaveSemesterPayload): Promise<SemesterItem> {
    const response = await apiClient.post<SemesterItem>('/admin/semesters', payload)
    return response.data
  },

  async updateSemester(id: string, payload: SaveSemesterPayload): Promise<SemesterItem> {
    const response = await apiClient.put<SemesterItem>(`/admin/semesters/${id}`, payload)
    return response.data
  },

  async getSemesterCourseOfferings(
    semesterId: string,
    includeInactive = true,
  ): Promise<SemesterCourseOfferingItem[]> {
    const response = await apiClient.get<SemesterCourseOfferingItem[]>(
      `/semesters/${semesterId}/course-offerings`,
      { params: { includeInactive } },
    )
    return Array.isArray(response.data) ? response.data : []
  },

  async syncSemesterCourseOfferings(
    semesterId: string,
    selectedCourseIds: string[],
  ): Promise<SyncSemesterCourseOfferingsResult> {
    const response = await apiClient.put<SyncSemesterCourseOfferingsResult>(
      `/semesters/${semesterId}/course-offerings`,
      { selectedCourseIds },
    )
    return response.data
  },

  async getCourses(includeInactive: boolean = true): Promise<AdminCourseItem[]> {
    const response = await apiClient.get<AdminCourseItem[]>('/admin/courses', {
      params: { includeInactive },
    })
    return Array.isArray(response.data) ? response.data : []
  },

  async createCourse(payload: CreateCoursePayload): Promise<AdminCourseItem> {
    const response = await apiClient.post<AdminCourseItem>('/admin/courses', payload)
    return response.data
  },

  async updateCourse(id: string, payload: UpdateCoursePayload): Promise<AdminCourseItem> {
    const response = await apiClient.put<AdminCourseItem>(`/admin/courses/${id}`, payload)
    return response.data
  },

  async assignDepartmentHead(
    courseId: string,
    departmentHeadUserId: string,
  ): Promise<{ courseId: string; departmentHeadUserId: string }> {
    const response = await apiClient.post<{ courseId: string; departmentHeadUserId: string }>(
      `/admin/courses/${courseId}/assign-department-head`,
      { departmentHeadUserId },
    )
    return response.data
  },

  async getDepartmentHeads(): Promise<AdminUserItem[]> {
    const res = await this.getUsers({ role: 'department_head', isActive: true, pageSize: 100 })
    return res.items || []
  },
}
