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

export const DEFAULT_ADMIN_PAGE_SIZE = 20

export interface CreateUserPayload {
  email: string
  fullName: string
  password: string
  role: string
  studentCode?: string
  username?: string
}

export interface UpdateUserPayload {
  email: string
  fullName: string
  username?: string | null
  studentCode?: string | null
}

export type AdminImportPasswordSource = 'ROW' | 'DEFAULT' | 'AUTO_GENERATED' | 'NONE'
export type AdminImportRowStatus = 'VALID' | 'INVALID' | 'DUPLICATE'

export interface AdminUserImportPreviewRow {
  rowNumber: number
  email: string
  fullName: string
  role: string
  studentCode?: string | null
  username?: string | null
  passwordSource: AdminImportPasswordSource
  isValid: boolean
  status: AdminImportRowStatus
  errorMessage?: string | null
}

export interface AdminUserImportPreviewResponse {
  fileName: string
  totalRows: number
  validCount: number
  invalidCount: number
  duplicateCount: number
  skippedCount: number
  hasDefaultPassword: boolean
  rows: AdminUserImportPreviewRow[]
}

export interface AdminUserImportCreatedRow {
  rowNumber: number
  userId: string
  email: string
  fullName: string
  role: string
  studentCode?: string | null
  username?: string | null
  temporaryPassword: string
  passwordSource: AdminImportPasswordSource
  mustChangePassword: boolean
}

export interface AdminUserImportSkippedRow {
  rowNumber: number
  email: string
  fullName: string
  role: string
  studentCode?: string | null
  reason: string
}

export interface AdminUserImportCommitResponse {
  totalRows: number
  createdCount: number
  skippedCount: number
  resultFileName: string
  resultFileContentType: string
  resultFileBase64: string
  createdRows: AdminUserImportCreatedRow[]
  skippedRows: AdminUserImportSkippedRow[]
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
  academicProgramId?: string | null
  semesterId?: string
  semesterCode?: string | null
  departmentHeadUserId?: string | null
  departmentHeadName?: string | null
  maxMockExamsPerDay: number
  mockExamDurationMinutes: number
  mockExamTotalQuestions: number
  mockExamBloomDistribution?: string
  hasFollowUp: boolean
  examInputMode: string
  isActive: boolean
}

export interface CreateCoursePayload {
  code: string
  name: string
  credits: number
  description?: string
  academicProgramId?: string | null
  departmentHeadUserId?: string
  semesterId?: string
  semesterCode?: string | null
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
  academicProgramId?: string | null
  departmentHeadUserId?: string
  semesterId?: string
  semesterCode?: string | null
  maxMockExamsPerDay?: number
  mockExamDurationMinutes?: number
  mockExamTotalQuestions?: number
  isActive?: boolean
}

export interface AcademicProgramItem {
  id: string
  code: string
  name: string
  departmentHeadUserId?: string | null
  departmentHeadName?: string | null
  isActive: boolean
  coursesCount: number
}

export interface SaveAcademicProgramPayload {
  code: string
  name: string
  departmentHeadUserId?: string | null
  isActive?: boolean
}

export interface BoundedQueueStats {
  count: number
  capacity: number
}

export interface BoundedChannelsStats {
  gradingQueue: BoundedQueueStats
}

export interface DlqByFlowTypeStats {
  practice: number
  mockExam: number
  inClassExam: number
}

export interface DeadLetterQueueStats {
  totalPending: number
  byFlowType: DlqByFlowTypeStats
}

export interface RuntimeAbuseMetrics {
  requests: number
  rejected: number
  loginLockouts: number
}

export interface PerformanceStats {
  totalRequests: number
  serverErrors5xx: number
  serverErrorRatePercent: number
  averageLatencyMs: number
  p95LatencyMs: number
}

export interface SystemStats {
  workingSetMb: number
  gcTotalMemoryMb: number
  threadCount: number
}

export interface RuntimeDiagnosticsDto {
  timestamp: string
  uptime: string
  boundedChannels: BoundedChannelsStats
  deadLetterQueue: DeadLetterQueueStats
  abuseMetrics: RuntimeAbuseMetrics
  performance: PerformanceStats
  system: SystemStats
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

  async downloadUsersImportTemplate(format: 'xlsx' | 'csv' = 'xlsx'): Promise<{
    blob: Blob
    fileName: string
  }> {
    try {
      const response = await apiClient.get<Blob>('/admin/users/import/template', {
        params: { format },
        responseType: 'blob',
      })
      const disposition = String(response.headers?.['content-disposition'] ?? '')
      const match = /filename\*?=(?:UTF-8''|")?([^";]+)/i.exec(disposition)
      const fallbackName = format === 'csv' ? 'mau-import-tai-khoan.csv' : 'mau-import-tai-khoan.xlsx'
      let fileName = fallbackName
      if (match?.[1]) {
        const rawName = match[1].replace(/"/g, '').trim()
        try {
          fileName = decodeURIComponent(rawName)
        } catch {
          fileName = rawName || fallbackName
        }
      }
      return {
        blob: response.data,
        fileName,
      }
    } catch (err: unknown) {
      const maybeAxios = err as { response?: { data?: unknown; status?: number } }
      if (maybeAxios?.response?.data instanceof Blob) {
        try {
          const text = await maybeAxios.response.data.text()
          const parsed = JSON.parse(text) as Record<string, unknown>
          throw {
            ...parsed,
            status: typeof parsed.status === 'number' ? parsed.status : maybeAxios.response.status,
          }
        } catch (parseErr: unknown) {
          if (parseErr && typeof parseErr === 'object' && 'detail' in parseErr) {
            throw parseErr
          }
        }
      }
      throw err
    }
  },

  async previewUsersImport(
    file: File,
    defaultPassword?: string,
  ): Promise<AdminUserImportPreviewResponse> {
    const formData = new FormData()
    formData.append('file', file)
    if (defaultPassword && defaultPassword.length > 0) {
      formData.append('defaultPassword', defaultPassword)
    }
    const response = await apiClient.post<AdminUserImportPreviewResponse>(
      '/admin/users/import/preview',
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } },
    )
    return response.data
  },

  async commitUsersImport(
    file: File,
    defaultPassword?: string,
  ): Promise<AdminUserImportCommitResponse> {
    const formData = new FormData()
    formData.append('file', file)
    if (defaultPassword && defaultPassword.length > 0) {
      formData.append('defaultPassword', defaultPassword)
    }
    const response = await apiClient.post<AdminUserImportCommitResponse>(
      '/admin/users/import/commit',
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } },
    )
    return response.data
  },

  async updateUser(id: string, payload: UpdateUserPayload): Promise<AdminUserItem> {
    const response = await apiClient.put<AdminUserItem>(`/admin/users/${id}`, payload)
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
  ): Promise<{
    courseId: string
    courseCode: string
    courseName: string
    departmentHeadUserId: string
    departmentHeadName: string
    departmentHeadEmail: string
  }> {
    const response = await apiClient.post<{
      courseId: string
      courseCode: string
      courseName: string
      departmentHeadUserId: string
      departmentHeadName: string
      departmentHeadEmail: string
    }>(
      `/admin/courses/${courseId}/assign-department-head`,
      { departmentHeadUserId },
    )
    return response.data
  },

  async getDepartmentHeads(): Promise<AdminUserItem[]> {
    const res = await this.getUsers({ role: 'department_head', isActive: true, pageSize: 100 })
    return res.items || []
  },

  async getRuntimeDiagnostics(): Promise<RuntimeDiagnosticsDto> {
    const response = await apiClient.get<RuntimeDiagnosticsDto>('/admin/diagnostics/runtime')
    return response.data
  },

  async getAcademicPrograms(isActive?: boolean): Promise<AcademicProgramItem[]> {
    const response = await apiClient.get<AcademicProgramItem[]>('/academic-programs', {
      params: isActive !== undefined ? { isActive } : undefined,
    })
    return Array.isArray(response.data) ? response.data : []
  },

  async createAcademicProgram(payload: SaveAcademicProgramPayload): Promise<AcademicProgramItem> {
    const response = await apiClient.post<AcademicProgramItem>('/academic-programs', payload)
    return response.data
  },

  async updateAcademicProgram(
    programId: string,
    payload: SaveAcademicProgramPayload,
  ): Promise<AcademicProgramItem> {
    const response = await apiClient.put<AcademicProgramItem>(
      `/academic-programs/${programId}`,
      payload,
    )
    return response.data
  },

  async assignProgramDepartmentHead(
    programId: string,
    departmentHeadUserId: string,
  ): Promise<AcademicProgramItem> {
    const response = await apiClient.put<AcademicProgramItem>(
      `/academic-programs/${programId}/department-head`,
      { departmentHeadUserId },
    )
    return response.data
  },
}
