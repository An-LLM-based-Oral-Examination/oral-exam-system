import { useEffect, useState } from 'react'
import {
  ChevronLeft,
  ChevronRight,
  FileText,
  KeyRound,
  LoaderCircle,
  RefreshCw,
  Search,
  ShieldAlert,
  Sliders,
  UserCheck,
  UserPlus,
  Users,
  UserX,
  X,
} from 'lucide-react'
import {
  adminService,
  type AdminUserItem,
  type AuditLogItem,
  type CreateUserPayload,
  type SystemConfigItem,
} from '@/services/admin.service'
import { toast } from '@/components/common/toastStore'
import { cn } from '@/lib/utils'

const SYSTEM_ROLES = [
  { value: 'student', label: 'Sinh viên (student)' },
  { value: 'lecturer', label: 'Giảng viên (lecturer)' },
  { value: 'department_head', label: 'Trưởng bộ môn (department_head)' },
  { value: 'examination_officer', label: 'Cán bộ khảo thí (examination_officer)' },
  { value: 'proctor', label: 'Giám thị phòng lab (proctor)' },
  { value: 'admin', label: 'Quản trị viên (admin)' },
]

export function AdminDashboardPage() {
  const [activeTab, setActiveTab] = useState<'users' | 'configs' | 'audit'>('users')

  // Users tab state
  const [users, setUsers] = useState<AdminUserItem[]>([])
  const [userTotal, setUserTotal] = useState(0)
  const [userPage, setUserPage] = useState(1)
  const [userTotalPages, setUserTotalPages] = useState(1)
  const [userSearch, setUserSearch] = useState('')
  const [userRoleFilter, setUserRoleFilter] = useState('')
  const [userStatusFilter, setUserStatusFilter] = useState<'all' | 'active' | 'locked'>('all')
  const [isUsersLoading, setIsUsersLoading] = useState(false)

  // System configs tab state
  const [configs, setConfigs] = useState<SystemConfigItem[]>([])
  const [isConfigsLoading, setIsConfigsLoading] = useState(false)

  // Audit logs tab state
  const [logs, setLogs] = useState<AuditLogItem[]>([])
  const [logTotal, setLogTotal] = useState(0)
  const [logPage, setLogPage] = useState(1)
  const [logTotalPages, setLogTotalPages] = useState(1)
  const [logEntityFilter, setLogEntityFilter] = useState('')
  const [isLogsLoading, setIsLogsLoading] = useState(false)

  // Modals state
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [createForm, setCreateForm] = useState<CreateUserPayload>({
    email: '',
    fullName: '',
    password: '',
    role: 'student',
    studentCode: '',
    department: '',
  })
  const [isCreatingUser, setIsCreatingUser] = useState(false)

  const [editRoleUser, setEditRoleUser] = useState<AdminUserItem | null>(null)
  const [newRole, setNewRole] = useState('')
  const [isUpdatingRole, setIsUpdatingRole] = useState(false)

  const [statusUser, setStatusUser] = useState<AdminUserItem | null>(null)
  const [statusReason, setStatusReason] = useState('')
  const [isUpdatingStatus, setIsUpdatingStatus] = useState(false)

  const [editConfig, setEditConfig] = useState<SystemConfigItem | null>(null)
  const [configValue, setConfigValue] = useState('')
  const [configDesc, setConfigDesc] = useState('')
  const [isUpdatingConfig, setIsUpdatingConfig] = useState(false)

  // Load Users
  const loadUsers = async (page = 1) => {
    setIsUsersLoading(true)
    try {
      const res = await adminService.getUsers({
        search: userSearch.trim() || undefined,
        role: userRoleFilter || undefined,
        isActive: userStatusFilter === 'all' ? undefined : userStatusFilter === 'active',
        page,
        pageSize: 15,
      })
      setUsers(res.items)
      setUserTotal(res.totalCount)
      setUserPage(res.page)
      setUserTotalPages(res.totalPages)
    } catch {
      toast.error('Không thể tải danh sách người dùng.')
    } finally {
      setIsUsersLoading(false)
    }
  }

  // Load Configs
  const loadConfigs = async () => {
    setIsConfigsLoading(true)
    try {
      const data = await adminService.getSystemConfigs()
      setConfigs(data)
    } catch {
      toast.error('Không thể tải danh sách cấu hình hệ thống.')
    } finally {
      setIsConfigsLoading(false)
    }
  }

  // Load Audit Logs
  const loadLogs = async (page = 1) => {
    setIsLogsLoading(true)
    try {
      const res = await adminService.getAuditLogs({
        entityName: logEntityFilter.trim() || undefined,
        page,
        pageSize: 20,
      })
      setLogs(res.items)
      setLogTotal(res.totalCount)
      setLogPage(res.page)
      setLogTotalPages(res.totalPages)
    } catch {
      toast.error('Không thể tải nhật ký kiểm toán.')
    } finally {
      setIsLogsLoading(false)
    }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      if (activeTab === 'users') {
        void loadUsers(userPage)
      } else if (activeTab === 'configs') {
        void loadConfigs()
      } else if (activeTab === 'audit') {
        void loadLogs(logPage)
      }
    }, 0)
    return () => window.clearTimeout(timer)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeTab, userPage, logPage])

  // Handle Create User
  const handleCreateUser = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!createForm.email || !createForm.fullName || !createForm.password) {
      toast.error('Vui lòng điền đầy đủ Email, Họ tên và Mật khẩu tạm thời.')
      return
    }

    setIsCreatingUser(true)
    try {
      await adminService.createUser({
        ...createForm,
        studentCode: createForm.studentCode?.trim() || undefined,
        department: createForm.department?.trim() || undefined,
      })
      toast.success(`Đã tạo tài khoản ${createForm.email} thành công (buộc đổi mật khẩu lần đầu).`)
      setShowCreateModal(false)
      setCreateForm({
        email: '',
        fullName: '',
        password: '',
        role: 'student',
        studentCode: '',
        department: '',
      })
      void loadUsers(1)
    } catch (err: unknown) {
      const msg = typeof err === 'object' && err !== null && 'detail' in err ? String((err as { detail: string }).detail) : 'Tạo người dùng thất bại.'
      toast.error(msg)
    } finally {
      setIsCreatingUser(false)
    }
  }

  // Handle Update Role
  const handleUpdateRole = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!editRoleUser || !newRole) return

    setIsUpdatingRole(true)
    try {
      await adminService.updateUserRole(editRoleUser.id, { role: newRole })
      toast.success(`Đã đổi vai trò cho ${editRoleUser.fullName} thành ${newRole}.`)
      setEditRoleUser(null)
      void loadUsers(userPage)
    } catch {
      toast.error('Không thể cập nhật vai trò người dùng.')
    } finally {
      setIsUpdatingRole(false)
    }
  }

  // Handle Update Status
  const handleToggleStatus = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!statusUser) return

    const targetStatus = !statusUser.isActive
    setIsUpdatingStatus(true)
    try {
      await adminService.updateUserStatus(statusUser.id, {
        isActive: targetStatus,
        reason: statusReason.trim() || undefined,
      })
      toast.success(
        targetStatus
          ? `Đã mở khóa tài khoản ${statusUser.email}.`
          : `Đã khóa tài khoản ${statusUser.email}.`,
      )
      setStatusUser(null)
      setStatusReason('')
      void loadUsers(userPage)
    } catch {
      toast.error('Không thể thay đổi trạng thái tài khoản.')
    } finally {
      setIsUpdatingStatus(false)
    }
  }

  // Handle Update Config
  const handleUpdateConfig = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!editConfig) return

    setIsUpdatingConfig(true)
    try {
      await adminService.updateSystemConfig(editConfig.key, {
        value: configValue.trim(),
        description: configDesc.trim() || undefined,
      })
      toast.success(`Đã cập nhật cấu hình [${editConfig.key}].`)
      setEditConfig(null)
      void loadConfigs()
    } catch {
      toast.error('Không thể cập nhật cấu hình.')
    } finally {
      setIsUpdatingConfig(false)
    }
  }

  return (
    <div className="space-y-6">
      {/* Top Banner */}
      <div className="rounded-3xl border border-[#82aedb]/50 bg-gradient-to-r from-[#0F2854] via-[#1C4D8D] to-[#4988C4] p-6 text-white shadow-xl sm:p-8">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-4">
            <div className="flex size-14 items-center justify-center rounded-2xl bg-white/10 backdrop-blur-md">
              <ShieldAlert className="size-8 text-[#99ccf3]" />
            </div>
            <div>
              <h1 className="text-2xl font-bold tracking-tight text-white sm:text-3xl">
                Cổng Quản Trị Hệ Thống (Admin Portal)
              </h1>
              <p className="mt-1 text-sm text-[#d4e4f7]">
                Quản lý người dùng, phân quyền RBAC, cấu hình thông số MF-01..MF-05 và nhật ký kiểm toán.
              </p>
            </div>
          </div>
          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={() => {
                if (activeTab === 'users') void loadUsers(userPage)
                else if (activeTab === 'configs') void loadConfigs()
                else void loadLogs(logPage)
              }}
              className="inline-flex items-center gap-2 rounded-xl border border-white/20 bg-white/10 px-4 py-2.5 text-xs font-bold text-white transition hover:bg-white/20 backdrop-blur-sm"
            >
              <RefreshCw className="size-3.5" />
              Làm mới dữ liệu
            </button>
          </div>
        </div>

        {/* Tab Navigation */}
        <div className="mt-6 flex flex-wrap gap-2 border-t border-white/10 pt-4">
          <button
            type="button"
            onClick={() => setActiveTab('users')}
            className={cn(
              'inline-flex items-center gap-2 rounded-xl px-4 py-2 text-xs font-bold transition',
              activeTab === 'users'
                ? 'bg-white text-[#0F2854] shadow-md'
                : 'text-white/80 hover:bg-white/10 hover:text-white',
            )}
          >
            <Users className="size-4" />
            Người dùng ({userTotal})
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('configs')}
            className={cn(
              'inline-flex items-center gap-2 rounded-xl px-4 py-2 text-xs font-bold transition',
              activeTab === 'configs'
                ? 'bg-white text-[#0F2854] shadow-md'
                : 'text-white/80 hover:bg-white/10 hover:text-white',
            )}
          >
            <Sliders className="size-4" />
            Cấu hình hệ thống ({configs.length})
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('audit')}
            className={cn(
              'inline-flex items-center gap-2 rounded-xl px-4 py-2 text-xs font-bold transition',
              activeTab === 'audit'
                ? 'bg-white text-[#0F2854] shadow-md'
                : 'text-white/80 hover:bg-white/10 hover:text-white',
            )}
          >
            <FileText className="size-4" />
            Nhật ký kiểm toán ({logTotal})
          </button>
        </div>
      </div>

      {/* TAB 1: USERS */}
      {activeTab === 'users' && (
        <section className="space-y-4">
          {/* Controls Bar */}
          <div className="flex flex-col gap-3 rounded-2xl border border-[#82aedb]/50 bg-white p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between">
            <div className="flex flex-1 flex-wrap items-center gap-3">
              {/* Search */}
              <div className="relative min-w-[240px] flex-1 max-w-md">
                <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[#82aedb]" />
                <input
                  value={userSearch}
                  onChange={(e) => setUserSearch(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') void loadUsers(1)
                  }}
                  placeholder="Tìm theo email, họ tên, MSSV..."
                  className="w-full rounded-xl border border-[#82aedb]/50 bg-[#F4FBFD]/50 py-2 pr-3 pl-9 text-xs text-[#0F2854] outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/20"
                />
              </div>

              {/* Role filter */}
              <select
                value={userRoleFilter}
                onChange={(e) => {
                  setUserRoleFilter(e.target.value)
                  void loadUsers(1)
                }}
                className="rounded-xl border border-[#82aedb]/50 bg-white px-3 py-2 text-xs font-semibold text-[#0F2854] outline-none"
              >
                <option value="">Tất cả vai trò</option>
                {SYSTEM_ROLES.map((r) => (
                  <option key={r.value} value={r.value}>
                    {r.label}
                  </option>
                ))}
              </select>

              {/* Status filter */}
              <select
                value={userStatusFilter}
                onChange={(e) => {
                  setUserStatusFilter(e.target.value as 'all' | 'active' | 'locked')
                  void loadUsers(1)
                }}
                className="rounded-xl border border-[#82aedb]/50 bg-white px-3 py-2 text-xs font-semibold text-[#0F2854] outline-none"
              >
                <option value="all">Tất cả trạng thái</option>
                <option value="active">Đang hoạt động</option>
                <option value="locked">Đã bị khóa</option>
              </select>

              <button
                type="button"
                onClick={() => void loadUsers(1)}
                className="rounded-xl bg-[#0F2854] px-3.5 py-2 text-xs font-bold text-white transition hover:bg-[#1C4D8D]"
              >
                Lọc
              </button>
            </div>

            {/* Create button */}
            <button
              type="button"
              onClick={() => setShowCreateModal(true)}
              className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-[#1C4D8D] to-[#4988C4] px-4 py-2 text-xs font-bold text-white shadow-sm transition hover:brightness-110"
            >
              <UserPlus className="size-4" />
              Tạo tài khoản trường cấp
            </button>
          </div>

          {/* Users Table */}
          <div className="overflow-hidden rounded-2xl border border-[#82aedb]/50 bg-white shadow-sm">
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="border-b border-[#82aedb]/40 bg-[#F4FBFD] font-bold text-[#0F2854] uppercase tracking-wider">
                  <tr>
                    <th className="px-4 py-3">Người dùng / Email</th>
                    <th className="px-4 py-3">Vai trò</th>
                    <th className="px-4 py-3">Mã SV / Đơn vị</th>
                    <th className="px-4 py-3">Trạng thái</th>
                    <th className="px-4 py-3">Đổi mật khẩu</th>
                    <th className="px-4 py-3">Ngày tạo</th>
                    <th className="px-4 py-3 text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#82aedb]/30">
                  {isUsersLoading ? (
                    <tr>
                      <td colSpan={7} className="py-12 text-center text-[#63738a]">
                        <LoaderCircle className="mx-auto size-6 animate-spin text-[#1C4D8D]" />
                        <span className="mt-2 block">Đang tải danh sách tài khoản...</span>
                      </td>
                    </tr>
                  ) : users.length === 0 ? (
                    <tr>
                      <td colSpan={7} className="py-12 text-center text-[#63738a]">
                        Không tìm thấy người dùng nào phù hợp với bộ lọc.
                      </td>
                    </tr>
                  ) : (
                    users.map((u) => (
                      <tr key={u.id} className="transition hover:bg-[#F4FBFD]/50">
                        <td className="px-4 py-3">
                          <p className="font-bold text-[#0F2854]">{u.fullName}</p>
                          <p className="text-[11px] text-[#63738a]">{u.email}</p>
                        </td>
                        <td className="px-4 py-3">
                          <span className="inline-flex items-center rounded-lg bg-[#0F2854]/10 px-2 py-1 font-mono text-[11px] font-bold text-[#0F2854]">
                            {u.role}
                          </span>
                        </td>
                        <td className="px-4 py-3">
                          {u.studentCode ? (
                            <span className="font-mono font-bold text-[#1C4D8D]">{u.studentCode}</span>
                          ) : (
                            <span className="text-[#94a3b8]">—</span>
                          )}
                        </td>
                        <td className="px-4 py-3">
                          {u.isActive ? (
                            <span className="inline-flex items-center gap-1 rounded-full bg-[#ecfdf5] px-2 py-0.5 font-bold text-[#059669]">
                              <UserCheck className="size-3" /> Hoạt động
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1 rounded-full bg-[#fef2f2] px-2 py-0.5 font-bold text-[#dc2626]">
                              <UserX className="size-3" /> Đã khóa
                            </span>
                          )}
                        </td>
                        <td className="px-4 py-3">
                          {u.mustChangePassword ? (
                            <span className="inline-flex items-center gap-1 rounded-full bg-[#fffbeb] px-2 py-0.5 font-bold text-[#d97706]">
                              <KeyRound className="size-3" /> Cần đổi
                            </span>
                          ) : (
                            <span className="text-[#94a3b8]">Đã đổi</span>
                          )}
                        </td>
                        <td className="px-4 py-3 text-[#63738a]">
                          {new Date(u.createdAt).toLocaleDateString('vi-VN')}
                        </td>
                        <td className="px-4 py-3 text-right">
                          <div className="flex items-center justify-end gap-2">
                            <button
                              type="button"
                              onClick={() => {
                                setEditRoleUser(u)
                                setNewRole(u.role)
                              }}
                              className="rounded-lg border border-[#82aedb]/50 px-2.5 py-1 text-[11px] font-bold text-[#1C4D8D] transition hover:bg-[#edf5fc]"
                            >
                              Đổi vai trò
                            </button>
                            <button
                              type="button"
                              onClick={() => {
                                setStatusUser(u)
                                setStatusReason('')
                              }}
                              className={cn(
                                'rounded-lg px-2.5 py-1 text-[11px] font-bold transition',
                                u.isActive
                                  ? 'border border-[#ef4444]/40 text-[#dc2626] hover:bg-[#fef2f2]'
                                  : 'border border-[#10b981]/40 text-[#059669] hover:bg-[#ecfdf5]',
                              )}
                            >
                              {u.isActive ? 'Khóa' : 'Mở khóa'}
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>

            {/* Pagination */}
            {userTotalPages > 1 && (
              <div className="flex items-center justify-between border-t border-[#82aedb]/30 px-4 py-3 text-xs text-[#63738a]">
                <span>
                  Trang {userPage} / {userTotalPages} (Tổng cộng {userTotal} tài khoản)
                </span>
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    disabled={userPage <= 1 || isUsersLoading}
                    onClick={() => void loadUsers(userPage - 1)}
                    className="flex size-8 items-center justify-center rounded-lg border border-[#82aedb]/50 transition hover:bg-[#F4FBFD] disabled:opacity-40"
                  >
                    <ChevronLeft className="size-4" />
                  </button>
                  <button
                    type="button"
                    disabled={userPage >= userTotalPages || isUsersLoading}
                    onClick={() => void loadUsers(userPage + 1)}
                    className="flex size-8 items-center justify-center rounded-lg border border-[#82aedb]/50 transition hover:bg-[#F4FBFD] disabled:opacity-40"
                  >
                    <ChevronRight className="size-4" />
                  </button>
                </div>
              </div>
            )}
          </div>
        </section>
      )}

      {/* TAB 2: SYSTEM CONFIGS */}
      {activeTab === 'configs' && (
        <section className="space-y-4">
          <div className="rounded-2xl border border-[#82aedb]/50 bg-white p-4 shadow-sm">
            <h2 className="text-base font-bold text-[#0F2854]">
              Cấu Hình Tham Số Hệ Thống (System Configs)
            </h2>
            <p className="mt-1 text-xs text-[#63738a]">
              Các giá trị cấu hình có hiệu lực toàn hệ thống cho các luồng Luyện tập (MF-01) và Thi thử (MF-02).
            </p>
          </div>

          <div className="overflow-hidden rounded-2xl border border-[#82aedb]/50 bg-white shadow-sm">
            <table className="w-full text-left text-xs">
              <thead className="border-b border-[#82aedb]/40 bg-[#F4FBFD] font-bold text-[#0F2854] uppercase tracking-wider">
                <tr>
                  <th className="px-4 py-3">Khóa tham số (Key)</th>
                  <th className="px-4 py-3">Giá trị cấu hình (Value)</th>
                  <th className="px-4 py-3">Diễn giải nghiệp vụ</th>
                  <th className="px-4 py-3">Cập nhật lúc</th>
                  <th className="px-4 py-3 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#82aedb]/30">
                {isConfigsLoading ? (
                  <tr>
                    <td colSpan={5} className="py-12 text-center text-[#63738a]">
                      <LoaderCircle className="mx-auto size-6 animate-spin text-[#1C4D8D]" />
                      <span className="mt-2 block">Đang tải danh sách tham số...</span>
                    </td>
                  </tr>
                ) : configs.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="py-12 text-center text-[#63738a]">
                      Chưa có tham số hệ thống nào trong bảng system_configs.
                    </td>
                  </tr>
                ) : (
                  configs.map((c) => (
                    <tr key={c.id} className="transition hover:bg-[#F4FBFD]/50">
                      <td className="px-4 py-3 font-mono font-bold text-[#0F2854]">{c.key}</td>
                      <td className="px-4 py-3 font-mono font-bold text-[#1C4D8D]">{c.value}</td>
                      <td className="px-4 py-3 text-[#63738a]">{c.description || '—'}</td>
                      <td className="px-4 py-3 text-[#63738a]">
                        {new Date(c.updatedAt).toLocaleString('vi-VN')}
                      </td>
                      <td className="px-4 py-3 text-right">
                        <button
                          type="button"
                          onClick={() => {
                            setEditConfig(c)
                            setConfigValue(c.value)
                            setConfigDesc(c.description || '')
                          }}
                          className="rounded-lg border border-[#82aedb]/50 px-3 py-1 font-bold text-[#1C4D8D] transition hover:bg-[#edf5fc]"
                        >
                          Chỉnh sửa
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {/* TAB 3: AUDIT LOGS */}
      {activeTab === 'audit' && (
        <section className="space-y-4">
          <div className="flex flex-col gap-3 rounded-2xl border border-[#82aedb]/50 bg-white p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h2 className="text-base font-bold text-[#0F2854]">Nhật Ký Kiểm Toán (Audit Logs)</h2>
              <p className="mt-1 text-xs text-[#63738a]">
                Truy vết toàn bộ các thao tác nghiệp vụ quan trọng trong hệ thống để bảo đảm tính minh bạch.
              </p>
            </div>
            <div className="flex items-center gap-2">
              <input
                value={logEntityFilter}
                onChange={(e) => setLogEntityFilter(e.target.value)}
                placeholder="Lọc theo tên bảng (users, config...)"
                className="rounded-xl border border-[#82aedb]/50 bg-[#F4FBFD]/50 px-3 py-1.5 text-xs text-[#0F2854] outline-none"
              />
              <button
                type="button"
                onClick={() => void loadLogs(1)}
                className="rounded-xl bg-[#0F2854] px-3.5 py-1.5 text-xs font-bold text-white transition hover:bg-[#1C4D8D]"
              >
                Lọc
              </button>
            </div>
          </div>

          <div className="overflow-hidden rounded-2xl border border-[#82aedb]/50 bg-white shadow-sm">
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="border-b border-[#82aedb]/40 bg-[#F4FBFD] font-bold text-[#0F2854] uppercase tracking-wider">
                  <tr>
                    <th className="px-4 py-3">Thời gian</th>
                    <th className="px-4 py-3">Người thực hiện</th>
                    <th className="px-4 py-3">Hành động</th>
                    <th className="px-4 py-3">Thực thể</th>
                    <th className="px-4 py-3">Dữ liệu chi tiết</th>
                    <th className="px-4 py-3">IP Address</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#82aedb]/30">
                  {isLogsLoading ? (
                    <tr>
                      <td colSpan={6} className="py-12 text-center text-[#63738a]">
                        <LoaderCircle className="mx-auto size-6 animate-spin text-[#1C4D8D]" />
                        <span className="mt-2 block">Đang tải nhật ký kiểm toán...</span>
                      </td>
                    </tr>
                  ) : logs.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="py-12 text-center text-[#63738a]">
                        Chưa có bản ghi kiểm toán nào phù hợp.
                      </td>
                    </tr>
                  ) : (
                    logs.map((log) => (
                      <tr key={log.id} className="transition hover:bg-[#F4FBFD]/50">
                        <td className="px-4 py-3 font-mono text-[#63738a]">
                          {new Date(log.createdAt).toLocaleString('vi-VN')}
                        </td>
                        <td className="px-4 py-3">
                          <p className="font-bold text-[#0F2854]">{log.userFullName || 'Hệ thống'}</p>
                          <p className="text-[11px] text-[#63738a]">{log.userEmail || 'system'}</p>
                        </td>
                        <td className="px-4 py-3">
                          <span
                            className={cn(
                              'inline-block rounded-md px-2 py-0.5 font-mono text-[11px] font-bold',
                              log.action === 'CREATE' && 'bg-[#ecfdf5] text-[#059669]',
                              log.action === 'UPDATE' && 'bg-[#eff6ff] text-[#2563eb]',
                              log.action === 'DELETE' && 'bg-[#fef2f2] text-[#dc2626]',
                            )}
                          >
                            {log.action}
                          </span>
                        </td>
                        <td className="px-4 py-3 font-mono text-[#0F2854]">
                          {log.entityName} {log.entityId ? `(#${log.entityId.slice(0, 8)})` : ''}
                        </td>
                        <td className="px-4 py-3">
                          <div className="max-w-md truncate font-mono text-[11px] text-[#475569]">
                            {log.newValues || log.oldValues || '—'}
                          </div>
                        </td>
                        <td className="px-4 py-3 font-mono text-[11px] text-[#64748b]">
                          {log.ipAddress || '—'}
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>

            {logTotalPages > 1 && (
              <div className="flex items-center justify-between border-t border-[#82aedb]/30 px-4 py-3 text-xs text-[#63738a]">
                <span>
                  Trang {logPage} / {logTotalPages} (Tổng cộng {logTotal} bản ghi)
                </span>
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    disabled={logPage <= 1 || isLogsLoading}
                    onClick={() => void loadLogs(logPage - 1)}
                    className="flex size-8 items-center justify-center rounded-lg border border-[#82aedb]/50 transition hover:bg-[#F4FBFD] disabled:opacity-40"
                  >
                    <ChevronLeft className="size-4" />
                  </button>
                  <button
                    type="button"
                    disabled={logPage >= logTotalPages || isLogsLoading}
                    onClick={() => void loadLogs(logPage + 1)}
                    className="flex size-8 items-center justify-center rounded-lg border border-[#82aedb]/50 transition hover:bg-[#F4FBFD] disabled:opacity-40"
                  >
                    <ChevronRight className="size-4" />
                  </button>
                </div>
              </div>
            )}
          </div>
        </section>
      )}

      {/* MODAL: CREATE USER */}
      {showCreateModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm">
          <div className="w-full max-w-lg rounded-3xl border border-[#82aedb]/50 bg-white p-6 shadow-2xl">
            <div className="flex items-center justify-between border-b border-[#82aedb]/30 pb-3">
              <div className="flex items-center gap-2">
                <UserPlus className="size-5 text-[#1C4D8D]" />
                <h3 className="text-base font-bold text-[#0F2854]">
                  Tạo Tài Khoản Nhà Trường Cấp
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setShowCreateModal(false)}
                className="text-[#94a3b8] transition hover:text-[#0F2854]"
              >
                <X className="size-5" />
              </button>
            </div>

            <form onSubmit={handleCreateUser} className="mt-4 space-y-3 text-xs">
              <div>
                <label className="block font-bold text-[#0F2854]">Email tài khoản (*)</label>
                <input
                  type="email"
                  required
                  value={createForm.email}
                  onChange={(e) => setCreateForm({ ...createForm, email: e.target.value })}
                  placeholder="ví dụ: student01@fe.edu.vn"
                  className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 text-xs outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/20"
                />
              </div>

              <div>
                <label className="block font-bold text-[#0F2854]">Họ và tên (*)</label>
                <input
                  type="text"
                  required
                  value={createForm.fullName}
                  onChange={(e) => setCreateForm({ ...createForm, fullName: e.target.value })}
                  placeholder="ví dụ: Nguyễn Văn A"
                  className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 text-xs outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/20"
                />
              </div>

              <div>
                <label className="block font-bold text-[#0F2854]">Mật khẩu tạm thời (*)</label>
                <input
                  type="password"
                  required
                  value={createForm.password}
                  onChange={(e) => setCreateForm({ ...createForm, password: e.target.value })}
                  placeholder="Nhập mật khẩu tạm thời"
                  className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 text-xs outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/20"
                />
                <p className="mt-1 text-[11px] text-[#63738a]">
                  Hệ thống tự động gắn cờ <code className="font-bold text-[#d97706]">mustChangePassword = true</code>.
                </p>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-bold text-[#0F2854]">Vai trò hệ thống</label>
                  <select
                    value={createForm.role}
                    onChange={(e) => setCreateForm({ ...createForm, role: e.target.value })}
                    className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 text-xs font-semibold outline-none"
                  >
                    {SYSTEM_ROLES.map((r) => (
                      <option key={r.value} value={r.value}>
                        {r.label}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block font-bold text-[#0F2854]">Mã sinh viên (Nếu là SV)</label>
                  <input
                    type="text"
                    value={createForm.studentCode || ''}
                    onChange={(e) => setCreateForm({ ...createForm, studentCode: e.target.value })}
                    placeholder="ví dụ: SE180001"
                    className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 text-xs outline-none focus:border-[#4988C4]"
                  />
                </div>
              </div>

              <div className="flex items-center justify-end gap-2 pt-4">
                <button
                  type="button"
                  onClick={() => setShowCreateModal(false)}
                  className="rounded-xl border border-[#82aedb]/50 px-4 py-2 font-bold text-[#64748b] transition hover:bg-[#f1f5f9]"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isCreatingUser}
                  className="flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-[#1C4D8D] to-[#4988C4] px-4 py-2 font-bold text-white shadow-md transition hover:brightness-110 disabled:opacity-50"
                >
                  {isCreatingUser && <LoaderCircle className="size-3.5 animate-spin" />}
                  Tạo tài khoản
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: EDIT ROLE */}
      {editRoleUser && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm">
          <div className="w-full max-w-sm rounded-3xl border border-[#82aedb]/50 bg-white p-6 shadow-2xl">
            <h3 className="text-base font-bold text-[#0F2854]">Đổi Vai Trò Người Dùng</h3>
            <p className="mt-1 text-xs text-[#63738a]">
              Đang chỉnh sửa: <strong>{editRoleUser.fullName}</strong> ({editRoleUser.email})
            </p>

            <form onSubmit={handleUpdateRole} className="mt-4 space-y-3 text-xs">
              <div>
                <label className="block font-bold text-[#0F2854]">Chọn vai trò mới</label>
                <select
                  value={newRole}
                  onChange={(e) => setNewRole(e.target.value)}
                  className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 text-xs font-semibold outline-none"
                >
                  {SYSTEM_ROLES.map((r) => (
                    <option key={r.value} value={r.value}>
                      {r.label}
                    </option>
                  ))}
                </select>
              </div>

              <div className="flex items-center justify-end gap-2 pt-4">
                <button
                  type="button"
                  onClick={() => setEditRoleUser(null)}
                  className="rounded-xl border border-[#82aedb]/50 px-4 py-2 font-bold text-[#64748b] transition hover:bg-[#f1f5f9]"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isUpdatingRole}
                  className="flex items-center gap-1.5 rounded-xl bg-[#0F2854] px-4 py-2 font-bold text-white transition hover:bg-[#1C4D8D] disabled:opacity-50"
                >
                  {isUpdatingRole && <LoaderCircle className="size-3.5 animate-spin" />}
                  Cập nhật vai trò
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: TOGGLE STATUS */}
      {statusUser && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm">
          <div className="w-full max-w-sm rounded-3xl border border-[#82aedb]/50 bg-white p-6 shadow-2xl">
            <h3 className="text-base font-bold text-[#0F2854]">
              {statusUser.isActive ? 'Khóa Tài Khoản' : 'Mở Khóa Tài Khoản'}
            </h3>
            <p className="mt-1 text-xs text-[#63738a]">
              Tài khoản: <strong>{statusUser.fullName}</strong> ({statusUser.email})
            </p>

            <form onSubmit={handleToggleStatus} className="mt-4 space-y-3 text-xs">
              <div>
                <label className="block font-bold text-[#0F2854]">Lý do thay đổi trạng thái</label>
                <textarea
                  value={statusReason}
                  onChange={(e) => setStatusReason(e.target.value)}
                  placeholder="Nhập lý do lưu vào Audit Log (tùy chọn)..."
                  className="mt-1 min-h-20 w-full rounded-xl border border-[#82aedb]/50 p-2.5 text-xs outline-none"
                />
              </div>

              <div className="flex items-center justify-end gap-2 pt-4">
                <button
                  type="button"
                  onClick={() => setStatusUser(null)}
                  className="rounded-xl border border-[#82aedb]/50 px-4 py-2 font-bold text-[#64748b] transition hover:bg-[#f1f5f9]"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isUpdatingStatus}
                  className={cn(
                    'flex items-center gap-1.5 rounded-xl px-4 py-2 font-bold text-white transition disabled:opacity-50',
                    statusUser.isActive ? 'bg-[#dc2626] hover:bg-[#b91c1c]' : 'bg-[#059669] hover:bg-[#047857]',
                  )}
                >
                  {isUpdatingStatus && <LoaderCircle className="size-3.5 animate-spin" />}
                  {statusUser.isActive ? 'Xác nhận khóa' : 'Xác nhận mở khóa'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: EDIT CONFIG */}
      {editConfig && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm">
          <div className="w-full max-w-md rounded-3xl border border-[#82aedb]/50 bg-white p-6 shadow-2xl">
            <h3 className="text-base font-bold text-[#0F2854]">Chỉnh Sửa Tham Số Hệ Thống</h3>
            <p className="mt-1 font-mono text-xs font-bold text-[#1C4D8D]">{editConfig.key}</p>

            <form onSubmit={handleUpdateConfig} className="mt-4 space-y-3 text-xs">
              <div>
                <label className="block font-bold text-[#0F2854]">Giá trị cấu hình (*)</label>
                <input
                  required
                  value={configValue}
                  onChange={(e) => setConfigValue(e.target.value)}
                  className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 font-mono text-xs outline-none focus:border-[#4988C4]"
                />
              </div>

              <div>
                <label className="block font-bold text-[#0F2854]">Diễn giải nghiệp vụ</label>
                <textarea
                  value={configDesc}
                  onChange={(e) => setConfigDesc(e.target.value)}
                  className="mt-1 min-h-20 w-full rounded-xl border border-[#82aedb]/50 p-2.5 text-xs outline-none"
                />
              </div>

              <div className="flex items-center justify-end gap-2 pt-4">
                <button
                  type="button"
                  onClick={() => setEditConfig(null)}
                  className="rounded-xl border border-[#82aedb]/50 px-4 py-2 font-bold text-[#64748b] transition hover:bg-[#f1f5f9]"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isUpdatingConfig}
                  className="flex items-center gap-1.5 rounded-xl bg-[#0F2854] px-4 py-2 font-bold text-white transition hover:bg-[#1C4D8D] disabled:opacity-50"
                >
                  {isUpdatingConfig && <LoaderCircle className="size-3.5 animate-spin" />}
                  Lưu cấu hình
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
