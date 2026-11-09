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
}
