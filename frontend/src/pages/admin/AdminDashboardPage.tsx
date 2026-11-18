import { AbuseMetricsPanel } from '@/components/admin/AbuseMetricsPanel'
import { EmptyState } from '@/components/common/EmptyState'
import { SearchField } from '@/components/common/SearchField'
import { ds } from '@/design/system'
import { useSearchInput } from '@/hooks/useSearchInput'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import {
  AlertTriangle,
  Bell,
  BookOpen,
  BookOpenCheck,
  Calendar,
  ChevronLeft,
  ChevronRight,
  FileText,
  GraduationCap,
  KeyRound,
  Layers,
  LoaderCircle,
  Pencil,
  Plus,
  RefreshCw,
  Search,
  Send,
  ServerCog,
  ShieldAlert,
  Sliders,
  Trash2,
  UserCheck,
  UserPlus,
  Users,
  UserX,
  X,
} from 'lucide-react'
import {
  adminService,
  type AdminCourseItem,
  type AdminNotificationItem,
  type AdminUserItem,
  type AuditLogItem,
  type CreateAdminNotificationPayload,
  type CreateUserPayload,
  type SaveSemesterPayload,
  type SemesterItem,
  type SystemConfigItem,
} from '@/services/admin.service'
import { toast } from '@/components/common/toastStore'
import { userMessage } from '@/lib/userMessage'
import { cn } from '@/lib/utils'
import { ROUTE_PATHS } from '@/routes/routePaths'
import { MF01_CONFIG_RULES } from './mf01ConfigRules'
import { AdminSemesterOfferingsModal } from './AdminSemesterOfferingsModal'
import { AdminCourseClosModal } from './AdminCourseClosModal'

type AdminTab = 'users' | 'semesters' | 'courses' | 'configs' | 'notifications' | 'audit'

const VALID_TABS: AdminTab[] = ['users', 'semesters', 'courses', 'configs', 'notifications', 'audit']

const SYSTEM_ROLES = [
  { value: 'student', label: 'Sinh viên (student)' },
  { value: 'lecturer', label: 'Giảng viên (lecturer)' },
  { value: 'department_head', label: 'Trưởng bộ môn (department_head)' },
  { value: 'examination_officer', label: 'Cán bộ khảo thí (examination_officer)' },
  { value: 'proctor', label: 'Giám thị phòng lab (proctor)' },
  { value: 'admin', label: 'Quản trị viên (admin)' },
]

const NOTIFICATION_TYPES = [
  { value: 'SYSTEM_ANNOUNCEMENT', label: 'Thông báo hệ thống' },
  { value: 'ACADEMIC_NOTICE', label: 'Thông báo học vụ' },
  { value: 'MAINTENANCE_ALERT', label: 'Cảnh báo bảo trì' },
  { value: 'SECURITY_ALERT', label: 'Cảnh báo bảo mật' },
]

function parseAdminTab(raw: string | null): AdminTab {
  return VALID_TABS.includes(raw as AdminTab) ? (raw as AdminTab) : 'users'
}

function getAuditBadgeTone(action: string): string {
  const upper = action.toUpperCase()
  if (upper.includes('CREATED') || upper.includes('CREATE') || upper.includes('DISPATCHED')) {
    return 'bg-[#ecfdf5] text-[#059669]'
  }
  if (upper.includes('DELETED') || upper.includes('DELETE') || upper.includes('RESET')) {
    return 'bg-[#fef2f2] text-[#dc2626]'
  }
  return 'bg-[#eff6ff] text-[#2563eb]'
}

export function AdminDashboardPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const activeTab = parseAdminTab(searchParams.get('tab'))

  const setActiveTab = useCallback(
    (tab: AdminTab) => {
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev)
          if (tab === 'users') next.delete('tab')
          else next.set('tab', tab)
          return next
        },
        { replace: true },
      )
    },
    [setSearchParams],
  )

  // Users tab state
  const [users, setUsers] = useState<AdminUserItem[]>([])
  const [userTotal, setUserTotal] = useState(0)
  const [userPage, setUserPage] = useState(1)
  const [userTotalPages, setUserTotalPages] = useState(1)
  const userSearch = useSearchInput(() => setUserPage(1))
  const userQuery = userSearch.query
  const userRequest = useRef(0)
  const [userRoleFilter, setUserRoleFilter] = useState('')
  const [userStatusFilter, setUserStatusFilter] = useState<'all' | 'active' | 'locked'>('all')
  const [isUsersLoading, setIsUsersLoading] = useState(false)
  const [usersError, setUsersError] = useState('')

  // System configs tab state
  const [configs, setConfigs] = useState<SystemConfigItem[]>([])
  const [isConfigsLoading, setIsConfigsLoading] = useState(false)
  const [configsError, setConfigsError] = useState('')
  const [isInitializingConfigs, setIsInitializingConfigs] = useState(false)

  // Semesters tab state
  const [semesters, setSemesters] = useState<SemesterItem[]>([])
  const [isSemestersLoading, setIsSemestersLoading] = useState(false)
  const [semestersError, setSemestersError] = useState('')
  const [showSemesterModal, setShowSemesterModal] = useState(false)
  const [editingSemester, setEditingSemester] = useState<SemesterItem | null>(null)
  const [offeringsSemester, setOfferingsSemester] = useState<SemesterItem | null>(null)
  const [semesterForm, setSemesterForm] = useState<SaveSemesterPayload>({
    code: '',
    name: '',
    startDate: '',
    endDate: '',
    isActive: true,
  })
  const [isSavingSemester, setIsSavingSemester] = useState(false)

  // Courses tab state
  const [courses, setCourses] = useState<AdminCourseItem[]>([])
  const [isCoursesLoading, setIsCoursesLoading] = useState(false)
  const [coursesError, setCoursesError] = useState('')
  const [showCourseModal, setShowCourseModal] = useState(false)
  const [editingCourse, setEditingCourse] = useState<AdminCourseItem | null>(null)
  const [closCourse, setClosCourse] = useState<AdminCourseItem | null>(null)
  const [courseCode, setCourseCode] = useState('')
  const [courseName, setCourseName] = useState('')
  const [courseCreditsInput, setCourseCreditsInput] = useState('3')
  const [courseMaxMockInput, setCourseMaxMockInput] = useState('3')
  const [courseDescription, setCourseDescription] = useState('')
  const [courseDeptHeadId, setCourseDeptHeadId] = useState('')
  const [courseSemesterId, setCourseSemesterId] = useState('')
  const [courseIsActive, setCourseIsActive] = useState(true)
  const [isSavingCourse, setIsSavingCourse] = useState(false)
  const [departmentHeads, setDepartmentHeads] = useState<AdminUserItem[]>([])
  const [showAssignHeadModal, setShowAssignHeadModal] = useState(false)
  const [assignHeadCourse, setAssignHeadCourse] = useState<AdminCourseItem | null>(null)
  const [selectedHeadUserId, setSelectedHeadUserId] = useState('')
  const [isAssigningHead, setIsAssigningHead] = useState(false)

  // Notifications 3NF tab state
  const [notifications, setNotifications] = useState<AdminNotificationItem[]>([])
  const [notifTotal, setNotifTotal] = useState(0)
  const [notifUnreadTotal, setNotifUnreadTotal] = useState(0)
  const [notifPage, setNotifPage] = useState(1)
  const [notifTotalPages, setNotifTotalPages] = useState(1)
  const [notifSearch, setNotifSearch] = useState('')
  const [notifTypeFilter, setNotifTypeFilter] = useState('')
  const [notifRoleFilter, setNotifRoleFilter] = useState('')
  const [notifReadFilter, setNotifReadFilter] = useState<'all' | 'unread' | 'read'>('all')
  const [isNotifsLoading, setIsNotifsLoading] = useState(false)
  const [notifsError, setNotifsError] = useState('')
  const [showSendNotifModal, setShowSendNotifModal] = useState(false)
  const [notifForm, setNotifForm] = useState<CreateAdminNotificationPayload>({
    targetScope: 'ALL_ACTIVE',
    targetRole: 'student',
    targetUserId: '',
    type: 'SYSTEM_ANNOUNCEMENT',
    title: '',
    message: '',
  })
  const [recipientUsers, setRecipientUsers] = useState<AdminUserItem[]>([])
  const [recipientSearch, setRecipientSearch] = useState('')
  const [isLoadingRecipients, setIsLoadingRecipients] = useState(false)
  const [recipientsError, setRecipientsError] = useState('')
  const [isSendingNotif, setIsSendingNotif] = useState(false)
  const [deletingNotif, setDeletingNotif] = useState<AdminNotificationItem | null>(null)
  const [isDeletingNotif, setIsDeletingNotif] = useState(false)

  // Audit logs tab state
  const [logs, setLogs] = useState<AuditLogItem[]>([])
  const [logTotal, setLogTotal] = useState(0)
  const [logPage, setLogPage] = useState(1)
  const [logTotalPages, setLogTotalPages] = useState(1)
  const [logEntityFilter, setLogEntityFilter] = useState('')
  const [logActionFilter, setLogActionFilter] = useState('')
  const [isLogsLoading, setIsLogsLoading] = useState(false)
  const [logsError, setLogsError] = useState('')

  // User Modals state
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

  const [resetPwdUser, setResetPwdUser] = useState<AdminUserItem | null>(null)
  const [tempPassword, setTempPassword] = useState('')
  const [resetPwdReason, setResetPwdReason] = useState('')
  const [isResettingPwd, setIsResettingPwd] = useState(false)

  const [editConfig, setEditConfig] = useState<SystemConfigItem | null>(null)
  const [configValue, setConfigValue] = useState('')
  const [configDesc, setConfigDesc] = useState('')
  const [isUpdatingConfig, setIsUpdatingConfig] = useState(false)

  const loadUsers = useCallback(
    async (
      page = 1,
      overrides?: { search?: string; role?: string; status?: 'all' | 'active' | 'locked' },
    ) => {
      const request = ++userRequest.current
      const effectiveSearch = overrides?.search !== undefined ? overrides.search : userQuery
      const effectiveRole = overrides?.role !== undefined ? overrides.role : userRoleFilter
      const effectiveStatus = overrides?.status !== undefined ? overrides.status : userStatusFilter
      setIsUsersLoading(true)
      setUsersError('')
      try {
        const res = await adminService.getUsers({
          search: effectiveSearch.trim() || undefined,
          role: effectiveRole || undefined,
          isActive: effectiveStatus === 'all' ? undefined : effectiveStatus === 'active',
          page,
          pageSize: 15,
        })
        if (request !== userRequest.current) return
        setUsers(res.items)
        setUserTotal(res.totalCount)
        setUserPage(res.page)
        setUserTotalPages(res.totalPages)
      } catch (err) {
        if (request !== userRequest.current) return
        const msg = userMessage(err, 'Không thể tải danh sách người dùng.')
        setUsersError(msg)
        toast.error(msg)
      } finally {
        if (request === userRequest.current) setIsUsersLoading(false)
      }
    },
    [userQuery, userRoleFilter, userStatusFilter],
  )

  const loadConfigs = useCallback(async () => {
    setIsConfigsLoading(true)
    setConfigsError('')
    try {
      const data = await adminService.getSystemConfigs()
      setConfigs(data)
    } catch (err) {
      setConfigsError(userMessage(err, 'Không thể tải danh sách cấu hình hệ thống.'))
      toast.error('Không thể tải danh sách cấu hình hệ thống.')
    } finally {
      setIsConfigsLoading(false)
    }
  }, [])

  const loadSemesters = useCallback(async () => {
    setIsSemestersLoading(true)
    setSemestersError('')
    try {
      const data = await adminService.getSemesters()
      setSemesters(data)
    } catch (err) {
      const msg = userMessage(err, 'Không thể tải danh sách học kỳ.')
      setSemestersError(msg)
      toast.error(msg)
    } finally {
      setIsSemestersLoading(false)
    }
  }, [])

  const loadCourses = useCallback(async () => {
    setIsCoursesLoading(true)
    setCoursesError('')
    try {
      const [coursesData, deptHeadsData, semestersData] = await Promise.all([
        adminService.getCourses(true),
        adminService.getDepartmentHeads(),
        adminService.getSemesters(),
      ])
      setCourses(coursesData)
      setDepartmentHeads(deptHeadsData)
      setSemesters(semestersData)
    } catch (err) {
      const msg = userMessage(err, 'Không thể tải danh sách môn học.')
      setCoursesError(msg)
      toast.error(msg)
    } finally {
      setIsCoursesLoading(false)
    }
  }, [])

  const loadNotifications = useCallback(
    async (page = 1) => {
      setIsNotifsLoading(true)
      setNotifsError('')
      try {
        const res = await adminService.getNotifications({
          search: notifSearch.trim() || undefined,
          type: notifTypeFilter || undefined,
          role: notifRoleFilter || undefined,
          isRead: notifReadFilter === 'all' ? undefined : notifReadFilter === 'read',
          page,
          pageSize: 20,
        })
        setNotifications(res.items)
        setNotifTotal(res.totalCount)
        setNotifUnreadTotal(res.unreadTotalCount)
        setNotifPage(res.page)
        setNotifTotalPages(res.totalPages)
      } catch (err) {
        const msg = userMessage(err, 'Không thể tải danh sách thông báo hệ thống.')
        setNotifsError(msg)
        toast.error(msg)
      } finally {
        setIsNotifsLoading(false)
      }
    },
    [notifReadFilter, notifRoleFilter, notifSearch, notifTypeFilter],
  )

  const loadLogs = useCallback(
    async (page = 1) => {
      setIsLogsLoading(true)
      setLogsError('')
      try {
        const res = await adminService.getAuditLogs({
          entityName: logEntityFilter.trim() || undefined,
          action: logActionFilter.trim() || undefined,
          page,
          pageSize: 20,
        })
        setLogs(res.items)
        setLogTotal(res.totalCount)
        setLogPage(res.page)
        setLogTotalPages(res.totalPages)
      } catch (err) {
        const msg = userMessage(err, 'Không thể tải nhật ký kiểm toán.')
        setLogsError(msg)
        toast.error(msg)
      } finally {
        setIsLogsLoading(false)
      }
    },
    [logActionFilter, logEntityFilter],
  )

  useEffect(() => {
    const timer = window.setTimeout(() => {
      if (activeTab === 'users') void loadUsers(userPage)
      else if (activeTab === 'semesters') void loadSemesters()
      else if (activeTab === 'courses') void loadCourses()
      else if (activeTab === 'configs') void loadConfigs()
      else if (activeTab === 'notifications') void loadNotifications(1)
      else if (activeTab === 'audit') void loadLogs(1)
    }, 0)
    return () => window.clearTimeout(timer)
  }, [activeTab, loadConfigs, loadCourses, loadLogs, loadNotifications, loadSemesters, loadUsers, userPage])

  const loadRecipientUsers = useCallback(async (searchTerm = '') => {
    setIsLoadingRecipients(true)
    setRecipientsError('')
    try {
      const activeRes = await adminService.getUsers({
        search: searchTerm.trim() || undefined,
        isActive: true,
        page: 1,
        pageSize: 30,
      })
      setRecipientUsers(activeRes.items || [])
    } catch (err) {
      const msg = userMessage(err, 'Không thể tải danh sách tài khoản người nhận.')
      setRecipientsError(msg)
      toast.error(msg)
    } finally {
      setIsLoadingRecipients(false)
    }
  }, [])

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      if (deletingNotif && !isDeletingNotif) setDeletingNotif(null)
      else if (showSendNotifModal && !isSendingNotif) setShowSendNotifModal(false)
      else if (showAssignHeadModal && !isAssigningHead) setShowAssignHeadModal(false)
      else if (showCourseModal && !isSavingCourse) setShowCourseModal(false)
      else if (showSemesterModal && !isSavingSemester) setShowSemesterModal(false)
      else if (editConfig && !isUpdatingConfig) setEditConfig(null)
      else if (resetPwdUser && !isResettingPwd) setResetPwdUser(null)
      else if (statusUser && !isUpdatingStatus) setStatusUser(null)
      else if (editRoleUser && !isUpdatingRole) setEditRoleUser(null)
      else if (showCreateModal && !isCreatingUser) setShowCreateModal(false)
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [
    deletingNotif,
    editConfig,
    editRoleUser,
    isAssigningHead,
    isCreatingUser,
    isDeletingNotif,
    isResettingPwd,
    isSavingCourse,
    isSavingSemester,
    isSendingNotif,
    isUpdatingConfig,
    isUpdatingRole,
    isUpdatingStatus,
    resetPwdUser,
    showAssignHeadModal,
    showCourseModal,
    showCreateModal,
    showSemesterModal,
    showSendNotifModal,
    statusUser,
  ])

  const missingCanonicalConfigKeys = useMemo(() => {
    const existingKeys = new Set(configs.map((c) => c.key))
    return Object.keys(MF01_CONFIG_RULES).filter((key) => !existingKeys.has(key))
  }, [configs])

  const handleInitializeMissingConfigs = async () => {
    if (isInitializingConfigs || missingCanonicalConfigKeys.length === 0) return
    setIsInitializingConfigs(true)
    try {
      for (const key of missingCanonicalConfigKeys) {
        const rule = MF01_CONFIG_RULES[key]
        await adminService.updateSystemConfig(key, {
          value: rule.defaultValue,
          description: rule.description,
        })
      }
      toast.success(
        `Đã khởi tạo ${missingCanonicalConfigKeys.length} tham số mặc định MF-01 vào bảng system_configs.`,
      )
      await loadConfigs()
    } catch (err) {
      toast.error(userMessage(err, 'Không thể khởi tạo tham số mặc định.'))
    } finally {
      setIsInitializingConfigs(false)
    }
  }

  // Handle Create User
  const handleCreateUser = async (e: React.FormEvent) => {
    e.preventDefault()
    if (isCreatingUser) return
    if (!createForm.email.trim() || !createForm.fullName.trim() || !createForm.password) {
      toast.error('Vui lòng điền đầy đủ Email, Họ tên và Mật khẩu tạm thời.')
      return
    }
    if (createForm.role === 'student' && !createForm.studentCode?.trim()) {
      toast.error('Vai trò sinh viên (student) bắt buộc nhập Mã sinh viên (MSSV).')
      return
    }

    setIsCreatingUser(true)
    try {
      await adminService.createUser({
        ...createForm,
        email: createForm.email.trim(),
        fullName: createForm.fullName.trim(),
        studentCode: createForm.studentCode?.trim() || undefined,
        department: createForm.department?.trim() || undefined,
      })
      toast.success(
        `Đã tạo tài khoản ${createForm.email.trim()} thành công (buộc đổi mật khẩu lần đầu).`,
      )
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
      toast.error(userMessage(err, 'Tạo người dùng thất bại.'))
    } finally {
      setIsCreatingUser(false)
    }
  }

  // Handle Update Role
  const handleUpdateRole = async (e: React.FormEvent) => {
    e.preventDefault()
    if (isUpdatingRole || !editRoleUser || !newRole) return

    setIsUpdatingRole(true)
    try {
      await adminService.updateUserRole(editRoleUser.id, { role: newRole })
      toast.success(`Đã đổi vai trò cho ${editRoleUser.fullName} thành ${newRole}.`)
      setEditRoleUser(null)
      void loadUsers(userPage)
    } catch (err: unknown) {
      toast.error(userMessage(err, 'Không thể cập nhật vai trò người dùng.'))
    } finally {
      setIsUpdatingRole(false)
    }
  }

  // Handle Update Status
  const handleToggleStatus = async (e: React.FormEvent) => {
    e.preventDefault()
    if (isUpdatingStatus || !statusUser) return

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
          : `Đã khóa tài khoản ${statusUser.email} và thu hồi phiên đăng nhập hiện tại.`,
      )
      setStatusUser(null)
      setStatusReason('')
      void loadUsers(userPage)
    } catch (err: unknown) {
      toast.error(userMessage(err, 'Không thể thay đổi trạng thái tài khoản.'))
    } finally {
      setIsUpdatingStatus(false)
    }
  }

  // Handle Reset User Password
  const handleResetPassword = async (e: React.FormEvent) => {
    e.preventDefault()
    if (isResettingPwd || !resetPwdUser) return
    if (tempPassword.trim().length < 8) {
      toast.error('Mật khẩu tạm thời bắt buộc có tối thiểu 8 ký tự.')
      return
    }

    setIsResettingPwd(true)
    try {
      await adminService.resetUserPassword(resetPwdUser.id, {
        temporaryPassword: tempPassword,
        reason: resetPwdReason.trim() || undefined,
      })
      toast.success(
        `Đã cấp lại mật khẩu tạm thời cho ${resetPwdUser.email} (mustChangePassword = true, thu hồi phiên cũ).`,
      )
      setResetPwdUser(null)
      setTempPassword('')
      setResetPwdReason('')
      void loadUsers(userPage)
    } catch (err: unknown) {
      toast.error(userMessage(err, 'Không thể cấp lại mật khẩu tạm thời.'))
    } finally {
      setIsResettingPwd(false)
    }
  }

  // Handle Update Config
  const handleUpdateConfig = async (e: React.FormEvent) => {
    e.preventDefault()
    if (isUpdatingConfig || !editConfig) return

    const trimmedVal = configValue.trim()
    const rule = MF01_CONFIG_RULES[editConfig.key]

    if (rule) {
      const num = parseInt(trimmedVal, 10)
      if (isNaN(num) || !/^-?\d+$/.test(trimmedVal)) {
        toast.error(`Giá trị cấu hình [${editConfig.key}] phải là một số nguyên hợp lệ.`)
        return
      }

      if (num < rule.min || num > rule.max) {
        toast.error(
          `Giá trị [${editConfig.key}] phải nằm trong khoảng từ ${rule.min} đến ${rule.max}.`,
        )
        return
      }

      if (rule.validate) {
        const customErr = rule.validate(num, configs)
        if (customErr) {
          toast.error(customErr)
          return
        }
      }
    }

    setIsUpdatingConfig(true)
    try {
      const targetKey = editConfig.key
      const nextDesc = configDesc.trim() || undefined
      const updated = await adminService.updateSystemConfig(targetKey, {
        value: trimmedVal,
        description: nextDesc,
      })
      setConfigs((prev) =>
        prev.map((item) =>
          item.key === targetKey
            ? {
                ...item,
                ...updated,
                value: updated?.value ?? trimmedVal,
                description: updated?.description ?? nextDesc ?? item.description,
              }
            : item,
        ),
      )
      toast.success(`Đã cập nhật cấu hình [${targetKey}].`)
      setEditConfig(null)
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { detail?: string; title?: string } } }
      const detail = axiosErr.response?.data?.detail || axiosErr.response?.data?.title
      toast.error(detail || userMessage(err, 'Không thể cập nhật cấu hình.'))
    } finally {
      setIsUpdatingConfig(false)
    }
  }

  // Handle Semester actions
  const handleOpenCreateSemester = () => {
    setEditingSemester(null)
    setSemesterForm({
      code: '',
      name: '',
      startDate: new Date().toISOString().slice(0, 10),
      endDate: new Date(Date.now() + 90 * 24 * 3600 * 1000).toISOString().slice(0, 10),
      isActive: true,
    })
    setShowSemesterModal(true)
  }

  const handleOpenEditSemester = (s: SemesterItem) => {
    setEditingSemester(s)
    setSemesterForm({
      code: s.code,
      name: s.name,
      startDate: s.startDate ? s.startDate.slice(0, 10) : '',
      endDate: s.endDate ? s.endDate.slice(0, 10) : '',
      isActive: s.isActive,
    })
    setShowSemesterModal(true)
  }

  const handleSaveSemester = async (e: React.FormEvent) => {
    e.preventDefault()
    if (isSavingSemester) return
    if (
      !semesterForm.code.trim() ||
      !semesterForm.name.trim() ||
      !semesterForm.startDate ||
      !semesterForm.endDate
    ) {
      toast.error('Vui lòng điền đầy đủ mã, tên và khoảng thời gian của học kỳ.')
      return
    }
    if (new Date(semesterForm.endDate) <= new Date(semesterForm.startDate)) {
      toast.error('Ngày kết thúc học kỳ bắt buộc phải sau ngày bắt đầu.')
      return
    }

    setIsSavingSemester(true)
    try {
      if (editingSemester) {
        await adminService.updateSemester(editingSemester.id, {
          ...semesterForm,
          code: semesterForm.code.trim().toUpperCase(),
        })
        toast.success(`Đã cập nhật học kỳ [${semesterForm.code.trim().toUpperCase()}] thành công.`)
      } else {
        await adminService.createSemester({
          ...semesterForm,
          code: semesterForm.code.trim().toUpperCase(),
        })
        toast.success(`Đã tạo học kỳ [${semesterForm.code.trim().toUpperCase()}] thành công.`)
      }
      setShowSemesterModal(false)
      void loadSemesters()
    } catch (err: unknown) {
      toast.error(userMessage(err, 'Lưu học kỳ thất bại.'))
    } finally {
      setIsSavingSemester(false)
    }
  }

  // Handle Course actions
  const handleOpenCreateCourse = () => {
    setEditingCourse(null)
    setCourseCode('')
    setCourseName('')
    setCourseCreditsInput('3')
    setCourseMaxMockInput('3')
    setCourseDescription('')
    setCourseDeptHeadId('')
    setCourseSemesterId(semesters.length > 0 ? semesters[0].id : '')
    setCourseIsActive(true)
    setShowCourseModal(true)
  }

  const handleOpenEditCourse = (c: AdminCourseItem) => {
    setEditingCourse(c)
    setCourseCode(c.code)
    setCourseName(c.name)
    setCourseCreditsInput(String(c.credits))
    setCourseMaxMockInput(String(c.maxMockExamsPerDay))
    setCourseDescription(c.description || '')
    setCourseDeptHeadId(c.departmentHeadUserId || '')
    setCourseSemesterId(c.semesterId || (semesters.length > 0 ? semesters[0].id : ''))
    setCourseIsActive(c.isActive)
    setShowCourseModal(true)
  }

  const handleSaveCourse = async (e: React.FormEvent) => {
    e.preventDefault()
    if (isSavingCourse) return
    const credits = Number.parseInt(courseCreditsInput.trim(), 10)
    const maxMock = Number.parseInt(courseMaxMockInput.trim(), 10)

    if (!courseCode.trim() || !courseName.trim() || Number.isNaN(credits) || credits < 1 || credits > 10) {
      toast.error('Vui lòng điền mã môn, tên môn và số tín chỉ hợp lệ (từ 1 đến 10).')
      return
    }
    if (Number.isNaN(maxMock) || maxMock < 1 || maxMock > 20) {
      toast.error('Hạn ngạch thi thử mỗi ngày (max_mock_exams_per_day) phải từ 1 đến 20 lượt.')
      return
    }
    if (!courseSemesterId.trim()) {
      toast.error('Vui lòng chọn học kỳ áp dụng cho môn học.')
      return
    }

    setIsSavingCourse(true)
    try {
      const payload = {
        code: courseCode.trim().toUpperCase(),
        name: courseName.trim(),
        credits,
        description: courseDescription.trim() || undefined,
        departmentHeadUserId:
          courseDeptHeadId.trim() || '00000000-0000-0000-0000-000000000000',
        semesterId: courseSemesterId.trim(),
        maxMockExamsPerDay: maxMock,
        mockExamDurationMinutes: editingCourse?.mockExamDurationMinutes ?? 30,
        mockExamTotalQuestions: editingCourse?.mockExamTotalQuestions ?? 5,
        isActive: courseIsActive,
      }

      if (editingCourse) {
        await adminService.updateCourse(editingCourse.id, payload)
        toast.success(`Đã cập nhật môn học [${payload.code}] thành công.`)
      } else {
        await adminService.createCourse(payload)
        toast.success(`Đã tạo môn học [${payload.code}] thành công.`)
      }
      setShowCourseModal(false)
      void loadCourses()
    } catch (err: unknown) {
      toast.error(userMessage(err, 'Lưu môn học thất bại.'))
    } finally {
      setIsSavingCourse(false)
    }
  }

  const handleOpenAssignHead = (c: AdminCourseItem) => {
    setAssignHeadCourse(c)
    setSelectedHeadUserId(c.departmentHeadUserId || '')
    setShowAssignHeadModal(true)
  }

  const handleAssignHead = async (e: React.FormEvent) => {
    e.preventDefault()
    if (isAssigningHead) return
    if (!assignHeadCourse || !selectedHeadUserId) {
      toast.error('Vui lòng chọn Trưởng bộ môn cần gán.')
      return
    }

    setIsAssigningHead(true)
    try {
      await adminService.assignDepartmentHead(assignHeadCourse.id, selectedHeadUserId)
      toast.success(`Đã gán Trưởng bộ môn cho môn [${assignHeadCourse.code}] thành công.`)
      setShowAssignHeadModal(false)
      void loadCourses()
    } catch (err: unknown) {
      toast.error(userMessage(err, 'Gán Trưởng bộ môn thất bại.'))
    } finally {
      setIsAssigningHead(false)
    }
  }

  // Handle Notification actions
  const handleOpenSendNotification = () => {
    setNotifForm({
      targetScope: 'ALL_ACTIVE',
      targetRole: 'student',
      targetUserId: '',
      type: 'SYSTEM_ANNOUNCEMENT',
      title: '',
      message: '',
    })
    setRecipientSearch('')
    setRecipientsError('')
    setShowSendNotifModal(true)
  }

  const handleSendNotification = async (e: React.FormEvent) => {
    e.preventDefault()
    if (isSendingNotif) return
    if (!notifForm.title.trim() || !notifForm.message.trim()) {
      toast.error('Vui lòng nhập đầy đủ tiêu đề và nội dung thông báo.')
      return
    }
    if (notifForm.targetScope === 'USER' && !notifForm.targetUserId?.trim()) {
      toast.error('Vui lòng chọn người nhận cụ thể.')
      return
    }

    setIsSendingNotif(true)
    try {
      const result = await adminService.createNotification({
        targetScope: notifForm.targetScope,
        targetRole: notifForm.targetScope === 'ROLE' ? notifForm.targetRole : undefined,
        targetUserId: notifForm.targetScope === 'USER' ? notifForm.targetUserId : undefined,
        type: notifForm.type,
        title: notifForm.title.trim(),
        message: notifForm.message.trim(),
      })
      toast.success(`Đã phát thông báo tới ${result.createdCount} tài khoản trong bảng notifications.`)
      setShowSendNotifModal(false)
      void loadNotifications(1)
    } catch (err: unknown) {
      toast.error(userMessage(err, 'Không thể phát thông báo hệ thống.'))
    } finally {
      setIsSendingNotif(false)
    }
  }

  const handleConfirmDeleteNotification = async () => {
    if (isDeletingNotif || !deletingNotif) return
    setIsDeletingNotif(true)
    try {
      await adminService.deleteNotification(deletingNotif.id)
      toast.success('Đã thu hồi bản ghi thông báo.')
      setDeletingNotif(null)
      void loadNotifications(notifPage)
    } catch (err: unknown) {
      toast.error(userMessage(err, 'Không thể thu hồi thông báo.'))
    } finally {
      setIsDeletingNotif(false)
    }
  }

  const refreshActiveTab = () => {
    if (activeTab === 'users') void loadUsers(userPage)
    else if (activeTab === 'semesters') void loadSemesters()
    else if (activeTab === 'courses') void loadCourses()
    else if (activeTab === 'configs') void loadConfigs()
    else if (activeTab === 'notifications') void loadNotifications(notifPage)
    else void loadLogs(logPage)
  }

  return (
    <div className="space-y-6">
      <AbuseMetricsPanel />

      {/* Top Banner */}
      <div className="rounded-3xl border border-[#82aedb]/50 bg-gradient-to-r from-[#0F2854] via-[#1C4D8D] to-[#4988C4] p-6 text-white shadow-xl sm:p-8">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div className="flex items-center gap-4">
            <div className="flex size-14 shrink-0 items-center justify-center rounded-2xl bg-white/10 backdrop-blur-md">
              <ShieldAlert className="size-8 text-[#99ccf3]" />
            </div>
            <div>
              <h1 className="text-2xl font-bold tracking-tight text-white sm:text-3xl">
                Cổng Quản Trị Hệ Thống (Admin Portal)
              </h1>
              <p className="mt-1 text-sm text-[#d4e4f7]">
                Quản trị tài khoản 6 vai trò RBAC, Học kỳ, Môn học & CLO, Cấu hình MF-01/MF-02, Thông báo 3NF, DLQ và Nhật ký kiểm toán.
              </p>
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-2.5">
            <Link
              to={ROUTE_PATHS.admin.dlq}
              className="inline-flex items-center gap-2 rounded-xl border border-white/25 bg-white/10 px-3.5 py-2.5 text-xs font-bold text-white transition hover:bg-white/20"
            >
              <ServerCog className="size-3.5" />
              Hàng đợi lỗi AI (DLQ)
            </Link>
            <button
              type="button"
              onClick={refreshActiveTab}
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
            onClick={() => setActiveTab('semesters')}
            className={cn(
              'inline-flex items-center gap-2 rounded-xl px-4 py-2 text-xs font-bold transition',
              activeTab === 'semesters'
                ? 'bg-white text-[#0F2854] shadow-md'
                : 'text-white/80 hover:bg-white/10 hover:text-white',
            )}
          >
            <Calendar className="size-4" />
            Học kỳ ({semesters.length})
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('courses')}
            className={cn(
              'inline-flex items-center gap-2 rounded-xl px-4 py-2 text-xs font-bold transition',
              activeTab === 'courses'
                ? 'bg-white text-[#0F2854] shadow-md'
                : 'text-white/80 hover:bg-white/10 hover:text-white',
            )}
          >
            <BookOpen className="size-4" />
            Môn học ({courses.length})
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
            onClick={() => setActiveTab('notifications')}
            className={cn(
              'inline-flex items-center gap-2 rounded-xl px-4 py-2 text-xs font-bold transition',
              activeTab === 'notifications'
                ? 'bg-white text-[#0F2854] shadow-md'
                : 'text-white/80 hover:bg-white/10 hover:text-white',
            )}
          >
            <Bell className="size-4" />
            Thông báo ({notifTotal})
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
          <div className="flex flex-col gap-3 rounded-2xl border border-[#82aedb]/50 bg-white p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between">
            <div className="flex flex-1 flex-wrap items-center gap-3">
              <div className={cn('relative', ds.searchControl)}>
                <SearchField
                  value={userSearch.draft}
                  onChange={(event) => userSearch.change(event.target.value)}
                  onKeyDown={userSearch.onKeyDown}
                  placeholder="Tìm theo email, họ tên, MSSV, username..."
                  className="w-full rounded-xl border border-[#82aedb]/50 bg-[#F4FBFD]/50 py-2 pr-3 pl-9 text-xs text-[#0F2854] outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/20"
                />
              </div>

              <select
                aria-label="Lọc vai trò"
                value={userRoleFilter}
                onChange={(e) => {
                  const nextRole = e.target.value
                  setUserRoleFilter(nextRole)
                  setUserPage(1)
                  void loadUsers(1, { role: nextRole })
                }}
                className={cn(ds.filterRole, 'rounded-xl border border-[#82aedb]/50 bg-white px-3 py-2 text-xs font-semibold text-[#0F2854] outline-none')}
              >
                <option value="">Tất cả vai trò</option>
                {SYSTEM_ROLES.map((r) => (
                  <option key={r.value} value={r.value}>
                    {r.label}
                  </option>
                ))}
              </select>

              <select
                aria-label="Lọc trạng thái tài khoản"
                value={userStatusFilter}
                onChange={(e) => {
                  const nextStatus = e.target.value as 'all' | 'active' | 'locked'
                  setUserStatusFilter(nextStatus)
                  setUserPage(1)
                  void loadUsers(1, { status: nextStatus })
                }}
                className={cn(ds.filterStatus, 'rounded-xl border border-[#82aedb]/50 bg-white px-3 py-2 text-xs font-semibold text-[#0F2854] outline-none')}
              >
                <option value="all">Tất cả trạng thái</option>
                <option value="active">Đang hoạt động</option>
                <option value="locked">Đã bị khóa</option>
              </select>

            </div>

            <button
              type="button"
              onClick={() => setShowCreateModal(true)}
              className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-[#1C4D8D] to-[#4988C4] px-4 py-2 text-xs font-bold text-white shadow-sm transition hover:brightness-110"
            >
              <UserPlus className="size-4" />
              Tạo tài khoản trường cấp
            </button>
          </div>

          {usersError && (
            <div
              role="alert"
              className="flex items-center justify-between gap-3 rounded-2xl border border-[#f0c5c9] bg-[#fff5f6] px-4 py-3 text-xs font-semibold text-[#a43d4b]"
            >
              <span>{usersError}</span>
              <button
                type="button"
                onClick={() => void loadUsers(userPage)}
                className="inline-flex items-center gap-1 rounded-lg border border-[#f0c5c9] bg-white px-3 py-1.5 font-bold text-[#a43d4b]"
              >
                <RefreshCw className="size-3.5" /> Thử lại
              </button>
            </div>
          )}

          <div className="overflow-hidden rounded-2xl border border-[#82aedb]/50 bg-white shadow-sm">
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="border-b border-[#82aedb]/40 bg-[#F4FBFD] font-bold text-[#0F2854] uppercase tracking-wider">
                  <tr>
                    <th className="px-4 py-3">Người dùng / Email</th>
                    <th className="px-4 py-3">Vai trò</th>
                    <th className="px-4 py-3">MSSV / Username</th>
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
                      <td colSpan={7} className="p-5">
                        <EmptyState icon={Users} title="Không tìm thấy người dùng phù hợp" description="Thử thay đổi từ khóa hoặc bộ lọc." />
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
                            <div>
                              <span className="font-mono font-bold text-[#1C4D8D]">
                                {u.studentCode}
                              </span>
                              {u.username && (
                                <span className="block font-mono text-[11px] text-[#63738a]">
                                  @{u.username}
                                </span>
                              )}
                            </div>
                          ) : u.username ? (
                            <span className="font-mono text-[11px] text-[#63738a]">
                              @{u.username}
                            </span>
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
                          <div className="flex flex-wrap items-center justify-end gap-1.5">
                            <button
                              type="button"
                              onClick={() => {
                                setResetPwdUser(u)
                                setTempPassword('')
                                setResetPwdReason('')
                              }}
                              className="rounded-lg border border-[#d97706]/40 px-2.5 py-1 text-[11px] font-bold text-[#b45309] transition hover:bg-[#fffbeb]"
                            >
                              Cấp lại MK
                            </button>
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

            {userTotalPages > 1 && (
              <div className="flex items-center justify-between border-t border-[#82aedb]/30 px-4 py-3 text-xs text-[#63738a]">
                <span>
                  Trang {userPage} / {userTotalPages} (Tổng cộng {userTotal} tài khoản)
                </span>
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    disabled={userPage <= 1 || isUsersLoading}
                    onClick={() => setUserPage(userPage - 1)}
                    className="flex size-8 items-center justify-center rounded-lg border border-[#82aedb]/50 transition hover:bg-[#F4FBFD] disabled:opacity-40"
                  >
                    <ChevronLeft className="size-4" />
                  </button>
                  <button
                    type="button"
                    disabled={userPage >= userTotalPages || isUsersLoading}
                    onClick={() => setUserPage(userPage + 1)}
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

      {/* TAB 2: SEMESTERS */}
      {activeTab === 'semesters' && (
        <section className="space-y-4">
          <div className="flex flex-col gap-3 rounded-2xl border border-[#82aedb]/50 bg-white p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between">
            <div>
              <div className="flex items-center gap-2">
                <Calendar className="size-5 text-[#1C4D8D]" />
                <h2 className="text-base font-bold text-[#0F2854]">
                  Quản Lý Học Kỳ (Semesters)
                </h2>
              </div>
              <p className="mt-1 text-xs text-[#63738a]">
                Học kỳ là dữ liệu nền tảng học thuật bất biến —{' '}
                <span className="font-bold text-[#dc2626]">CẤM XÓA (ON DELETE RESTRICT)</span> để bảo
                đảm toàn vẹn khóa ngoại của lớp học, môn học và ca thi.
              </p>
            </div>
            <button
              type="button"
              onClick={handleOpenCreateSemester}
              className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-[#1C4D8D] to-[#4988C4] px-4 py-2 text-xs font-bold text-white shadow-sm transition hover:brightness-110"
            >
              <Plus className="size-4" />
              Thêm học kỳ mới
            </button>
          </div>

          {semestersError && (
            <div
              role="alert"
              className="flex items-center justify-between gap-3 rounded-2xl border border-[#f0c5c9] bg-[#fff5f6] px-4 py-3 text-xs font-semibold text-[#a43d4b]"
            >
              <span>{semestersError}</span>
              <button
                type="button"
                onClick={() => void loadSemesters()}
                className="inline-flex items-center gap-1 rounded-lg border border-[#f0c5c9] bg-white px-3 py-1.5 font-bold text-[#a43d4b]"
              >
                <RefreshCw className="size-3.5" /> Thử lại
              </button>
            </div>
          )}

          <div className="overflow-hidden rounded-2xl border border-[#82aedb]/50 bg-white shadow-sm">
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="border-b border-[#82aedb]/40 bg-[#F4FBFD] font-bold text-[#0F2854] uppercase tracking-wider">
                  <tr>
                    <th className="px-4 py-3">Mã học kỳ</th>
                    <th className="px-4 py-3">Tên học kỳ</th>
                    <th className="px-4 py-3">Ngày bắt đầu</th>
                    <th className="px-4 py-3">Ngày kết thúc</th>
                    <th className="px-4 py-3">Quy mô mở</th>
                    <th className="px-4 py-3">Trạng thái</th>
                    <th className="px-4 py-3 text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#82aedb]/30">
                  {isSemestersLoading ? (
                    <tr>
                      <td colSpan={7} className="py-12 text-center text-[#63738a]">
                        <LoaderCircle className="mx-auto size-6 animate-spin text-[#1C4D8D]" />
                        <span className="mt-2 block">Đang tải danh sách học kỳ...</span>
                      </td>
                    </tr>
                  ) : semesters.length === 0 ? (
                    <tr>
                      <td colSpan={7} className="py-12 text-center text-[#63738a]">
                        Chưa có học kỳ nào trong hệ thống.
                      </td>
                    </tr>
                  ) : (
                    semesters.map((s) => (
                      <tr key={s.id} className="transition hover:bg-[#F4FBFD]/50">
                        <td className="px-4 py-3 font-mono font-bold text-[#0F2854]">{s.code}</td>
                        <td className="px-4 py-3 font-bold text-[#1C4D8D]">{s.name}</td>
                        <td className="px-4 py-3 text-[#63738a]">
                          {s.startDate ? new Date(s.startDate).toLocaleDateString('vi-VN') : '—'}
                        </td>
                        <td className="px-4 py-3 text-[#63738a]">
                          {s.endDate ? new Date(s.endDate).toLocaleDateString('vi-VN') : '—'}
                        </td>
                        <td className="px-4 py-3 font-mono text-[#63738a]">
                          {s.coursesCount ?? 0} môn · {s.classesCount ?? 0} lớp
                        </td>
                        <td className="px-4 py-3">
                          {s.isActive ? (
                            <span className="inline-flex items-center gap-1 rounded-full bg-[#ecfdf5] px-2 py-0.5 font-bold text-[#059669]">
                              Hoạt động
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1 rounded-full bg-[#f1f5f9] px-2 py-0.5 font-bold text-[#64748b]">
                              Đã đóng
                            </span>
                          )}
                        </td>
                        <td className="px-4 py-3 text-right">
                          <div className="flex items-center justify-end gap-1.5">
                            <button
                              type="button"
                              onClick={() => setOfferingsSemester(s)}
                              className="inline-flex items-center gap-1 rounded-lg border border-[#82aedb]/50 px-2.5 py-1 text-[11px] font-bold text-[#1C4D8D] transition hover:bg-[#edf5fc]"
                            >
                              <Layers className="size-3" />
                              Môn mở trong kỳ
                            </button>
                            <button
                              type="button"
                              onClick={() => handleOpenEditSemester(s)}
                              className="inline-flex items-center gap-1 rounded-lg border border-[#82aedb]/50 px-2.5 py-1 text-[11px] font-bold text-[#1C4D8D] transition hover:bg-[#edf5fc]"
                            >
                              <Pencil className="size-3" />
                              Sửa
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </section>
      )}

      {/* TAB 3: COURSES */}
      {activeTab === 'courses' && (
        <section className="space-y-4">
          <div className="flex flex-col gap-3 rounded-2xl border border-[#82aedb]/50 bg-white p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between">
            <div>
              <div className="flex items-center gap-2">
                <BookOpen className="size-5 text-[#1C4D8D]" />
                <h2 className="text-base font-bold text-[#0F2854]">
                  Quản Lý Môn Học, Trưởng Bộ Môn & Chuẩn Đầu Ra CLO (Courses)
                </h2>
              </div>
              <p className="mt-1 text-xs text-[#63738a]">
                Quản trị viên tạo môn học, số tín chỉ (≥ 1), hạn ngạch thi thử mỗi ngày (MF-02 GMT+7), gán Trưởng bộ môn và quản trị danh mục CLO 3NF.
              </p>
            </div>
            <button
              type="button"
              onClick={handleOpenCreateCourse}
              className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-[#1C4D8D] to-[#4988C4] px-4 py-2 text-xs font-bold text-white shadow-sm transition hover:brightness-110"
            >
              <Plus className="size-4" />
              Thêm môn học mới
            </button>
          </div>

          {coursesError && (
            <div
              role="alert"
              className="flex items-center justify-between gap-3 rounded-2xl border border-[#f0c5c9] bg-[#fff5f6] px-4 py-3 text-xs font-semibold text-[#a43d4b]"
            >
              <span>{coursesError}</span>
              <button
                type="button"
                onClick={() => void loadCourses()}
                className="inline-flex items-center gap-1 rounded-lg border border-[#f0c5c9] bg-white px-3 py-1.5 font-bold text-[#a43d4b]"
              >
                <RefreshCw className="size-3.5" /> Thử lại
              </button>
            </div>
          )}

          <div className="overflow-hidden rounded-2xl border border-[#82aedb]/50 bg-white shadow-sm">
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="border-b border-[#82aedb]/40 bg-[#F4FBFD] font-bold text-[#0F2854] uppercase tracking-wider">
                  <tr>
                    <th className="px-4 py-3">Mã môn</th>
                    <th className="px-4 py-3">Tên môn học</th>
                    <th className="px-4 py-3">Tín chỉ</th>
                    <th className="px-4 py-3">Trưởng bộ môn</th>
                    <th className="px-4 py-3">Quota Thi thử (MF-02)</th>
                    <th className="px-4 py-3">Trạng thái</th>
                    <th className="px-4 py-3 text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#82aedb]/30">
                  {isCoursesLoading ? (
                    <tr>
                      <td colSpan={7} className="py-12 text-center text-[#63738a]">
                        <LoaderCircle className="mx-auto size-6 animate-spin text-[#1C4D8D]" />
                        <span className="mt-2 block">Đang tải danh sách môn học...</span>
                      </td>
                    </tr>
                  ) : courses.length === 0 ? (
                    <tr>
                      <td colSpan={7} className="py-12 text-center text-[#63738a]">
                        Chưa có môn học nào trong hệ thống.
                      </td>
                    </tr>
                  ) : (
                    courses.map((c) => (
                      <tr key={c.id} className="transition hover:bg-[#F4FBFD]/50">
                        <td className="px-4 py-3 font-mono font-bold text-[#0F2854]">{c.code}</td>
                        <td className="px-4 py-3 font-bold text-[#1C4D8D]">{c.name}</td>
                        <td className="px-4 py-3 font-bold text-[#0F2854]">{c.credits} TC</td>
                        <td className="px-4 py-3">
                          {c.departmentHeadName ? (
                            <span className="inline-flex items-center gap-1 font-semibold text-[#0F2854]">
                              <GraduationCap className="size-3.5 text-[#1C4D8D]" />
                              {c.departmentHeadName}
                            </span>
                          ) : (
                            <span className="italic text-[#94a3b8]">Chưa phân công</span>
                          )}
                        </td>
                        <td className="px-4 py-3 font-mono font-bold text-[#1C4D8D]">
                          {c.maxMockExamsPerDay} lượt/ngày (GMT+7)
                        </td>
                        <td className="px-4 py-3">
                          {c.isActive ? (
                            <span className="inline-flex items-center gap-1 rounded-full bg-[#ecfdf5] px-2 py-0.5 font-bold text-[#059669]">
                              Hoạt động
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1 rounded-full bg-[#f1f5f9] px-2 py-0.5 font-bold text-[#64748b]">
                              Tạm khóa
                            </span>
                          )}
                        </td>
                        <td className="px-4 py-3 text-right">
                          <div className="flex flex-wrap items-center justify-end gap-1.5">
                            <button
                              type="button"
                              onClick={() => setClosCourse(c)}
                              className="inline-flex items-center gap-1 rounded-lg border border-[#82aedb]/50 px-2 py-1 text-[11px] font-bold text-[#1C4D8D] transition hover:bg-[#edf5fc]"
                            >
                              <BookOpenCheck className="size-3" />
                              CLO
                            </button>
                            <button
                              type="button"
                              onClick={() => handleOpenAssignHead(c)}
                              className="rounded-lg border border-[#82aedb]/50 px-2 py-1 text-[11px] font-bold text-[#1C4D8D] transition hover:bg-[#edf5fc]"
                            >
                              Gán Trưởng BM
                            </button>
                            <button
                              type="button"
                              onClick={() => handleOpenEditCourse(c)}
                              className="inline-flex items-center gap-1 rounded-lg border border-[#82aedb]/50 px-2 py-1 text-[11px] font-bold text-[#1C4D8D] transition hover:bg-[#edf5fc]"
                            >
                              <Pencil className="size-3" />
                              Sửa
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </section>
      )}

      {/* TAB 4: SYSTEM CONFIGS */}
      {activeTab === 'configs' && (
        <section className="space-y-4">
          <div className="flex flex-col gap-3 rounded-2xl border border-[#82aedb]/50 bg-white p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h2 className="text-base font-bold text-[#0F2854]">
                Cấu Hình Tham Số Hệ Thống (System Configs)
              </h2>
              <p className="mt-1 text-xs text-[#63738a]">
                Quản lý thông số toàn cục luồng Luyện tập (MF-01): Follow-up 1–5 (mặc định 2), Progressive 3–10, Buffer 10–300s (mặc định 60s), Timeout không tương tác 5 phút.
              </p>
            </div>
            {!isConfigsLoading && !configsError && missingCanonicalConfigKeys.length > 0 && (
              <button
                type="button"
                disabled={isInitializingConfigs}
                onClick={() => void handleInitializeMissingConfigs()}
                className="inline-flex items-center gap-1.5 rounded-xl bg-[#0F2854] px-3.5 py-2 text-xs font-bold text-white transition hover:bg-[#1C4D8D] disabled:opacity-50"
              >
                {isInitializingConfigs ? (
                  <LoaderCircle className="size-3.5 animate-spin" />
                ) : (
                  <Plus className="size-3.5" />
                )}
                Khởi tạo {missingCanonicalConfigKeys.length} tham số mặc định còn thiếu
              </button>
            )}
          </div>

          {configsError && (
            <div
              role="alert"
              className="flex items-center justify-between gap-3 rounded-2xl border border-[#f0c5c9] bg-[#fff5f6] px-4 py-3 text-xs font-semibold text-[#a43d4b]"
            >
              <span>{configsError}</span>
              <button
                type="button"
                onClick={() => void loadConfigs()}
                className="inline-flex items-center gap-1 rounded-lg border border-[#f0c5c9] bg-white px-3 py-1.5 font-bold text-[#a43d4b]"
              >
                <RefreshCw className="size-3.5" /> Thử lại
              </button>
            </div>
          )}

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
                    <td colSpan={5} className="p-5">
                      <EmptyState
                        icon={Sliders}
                        title="Chưa có tham số hệ thống nào trong bảng system_configs."
                        description="Các tham số cấu hình sẽ xuất hiện tại đây."
                      />
                    </td>
                  </tr>
                ) : (
                  configs.map((c) => (
                    <tr key={c.id} className="transition hover:bg-[#F4FBFD]/50">
                      <td className="px-4 py-3 font-mono font-bold text-[#0F2854]">{c.key}</td>
                      <td className="px-4 py-3 font-mono font-bold text-[#1C4D8D]">{c.value}</td>
                      <td className="px-4 py-3 text-[#63738a]">
                        {c.description || MF01_CONFIG_RULES[c.key]?.description || '—'}
                      </td>
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

      {/* TAB 5: NOTIFICATIONS 3NF */}
      {activeTab === 'notifications' && (
        <section className="space-y-4">
          <div className="flex flex-col gap-3 rounded-2xl border border-[#82aedb]/50 bg-white p-4 shadow-sm lg:flex-row lg:items-center lg:justify-between">
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <Bell className="size-5 text-[#1C4D8D]" />
                <h2 className="text-base font-bold text-[#0F2854]">
                  Quản Lý Thông Báo Nội Bộ 3NF (notifications)
                </h2>
                <span className="rounded-full bg-[#fffbeb] px-2.5 py-0.5 text-[11px] font-bold text-[#d97706]">
                  Chưa đọc: {notifUnreadTotal}
                </span>
              </div>
              <p className="mt-1 text-xs text-[#63738a]">
                Phát thông báo hệ thống / học vụ / bảo trì tới toàn trường, theo vai trò RBAC hoặc cá nhân và giám sát trạng thái đọc thực tế.
              </p>
            </div>
            <button
              type="button"
              onClick={() => void handleOpenSendNotification()}
              className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-[#1C4D8D] to-[#4988C4] px-4 py-2 text-xs font-bold text-white shadow-sm transition hover:brightness-110"
            >
              <Send className="size-3.5" />
              Phát thông báo mới
            </button>
          </div>

          <div className="flex flex-wrap items-center gap-2.5 rounded-2xl border border-[#82aedb]/50 bg-white p-3.5 shadow-sm">
            <div className="relative min-w-[220px] flex-1">
              <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[#82aedb]" />
              <input
                value={notifSearch}
                onChange={(e) => setNotifSearch(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') void loadNotifications(1)
                }}
                placeholder="Tìm theo tiêu đề, nội dung hoặc email người nhận..."
                className="w-full rounded-xl border border-[#82aedb]/50 bg-[#F4FBFD]/50 py-2 pr-3 pl-9 text-xs text-[#0F2854] outline-none"
              />
            </div>
            <select
              aria-label="Lọc loại thông báo"
              value={notifTypeFilter}
              onChange={(e) => setNotifTypeFilter(e.target.value)}
              className="rounded-xl border border-[#82aedb]/50 bg-white px-3 py-2 text-xs font-semibold text-[#0F2854] outline-none"
            >
              <option value="">Mọi loại thông báo</option>
              {NOTIFICATION_TYPES.map((t) => (
                <option key={t.value} value={t.value}>
                  {t.label}
                </option>
              ))}
            </select>
            <select
              aria-label="Lọc vai trò nhận thông báo"
              value={notifRoleFilter}
              onChange={(e) => setNotifRoleFilter(e.target.value)}
              className="rounded-xl border border-[#82aedb]/50 bg-white px-3 py-2 text-xs font-semibold text-[#0F2854] outline-none"
            >
              <option value="">Mọi vai trò nhận</option>
              {SYSTEM_ROLES.map((r) => (
                <option key={r.value} value={r.value}>
                  {r.label}
                </option>
              ))}
            </select>
            <select
              aria-label="Lọc trạng thái đọc"
              value={notifReadFilter}
              onChange={(e) => setNotifReadFilter(e.target.value as 'all' | 'unread' | 'read')}
              className="rounded-xl border border-[#82aedb]/50 bg-white px-3 py-2 text-xs font-semibold text-[#0F2854] outline-none"
            >
              <option value="all">Tất cả trạng thái đọc</option>
              <option value="unread">Chưa đọc</option>
              <option value="read">Đã đọc</option>
            </select>
            <button
              type="button"
              onClick={() => void loadNotifications(1)}
              className="rounded-xl bg-[#0F2854] px-3.5 py-2 text-xs font-bold text-white transition hover:bg-[#1C4D8D]"
            >
              Lọc thông báo
            </button>
          </div>

          {notifsError && (
            <div
              role="alert"
              className="flex items-center justify-between gap-3 rounded-2xl border border-[#f0c5c9] bg-[#fff5f6] px-4 py-3 text-xs font-semibold text-[#a43d4b]"
            >
              <span>{notifsError}</span>
              <button
                type="button"
                onClick={() => void loadNotifications(notifPage)}
                className="inline-flex items-center gap-1 rounded-lg border border-[#f0c5c9] bg-white px-3 py-1.5 font-bold text-[#a43d4b]"
              >
                <RefreshCw className="size-3.5" /> Thử lại
              </button>
            </div>
          )}

          <div className="overflow-hidden rounded-2xl border border-[#82aedb]/50 bg-white shadow-sm">
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="border-b border-[#82aedb]/40 bg-[#F4FBFD] font-bold text-[#0F2854] uppercase tracking-wider">
                  <tr>
                    <th className="px-4 py-3">Thời gian</th>
                    <th className="px-4 py-3">Người nhận</th>
                    <th className="px-4 py-3">Phân loại</th>
                    <th className="px-4 py-3">Tiêu đề & Nội dung</th>
                    <th className="px-4 py-3">Trạng thái</th>
                    <th className="px-4 py-3 text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#82aedb]/30">
                  {isNotifsLoading ? (
                    <tr>
                      <td colSpan={6} className="py-12 text-center text-[#63738a]">
                        <LoaderCircle className="mx-auto size-6 animate-spin text-[#1C4D8D]" />
                        <span className="mt-2 block">Đang tải danh sách thông báo...</span>
                      </td>
                    </tr>
                  ) : notifications.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="py-12 text-center text-[#63738a]">
                        Chưa có bản ghi thông báo nào khớp bộ lọc.
                      </td>
                    </tr>
                  ) : (
                    notifications.map((n) => (
                      <tr key={n.id} className="transition hover:bg-[#F4FBFD]/50">
                        <td className="px-4 py-3 font-mono text-[11px] text-[#63738a]">
                          {new Date(n.createdAt).toLocaleString('vi-VN')}
                        </td>
                        <td className="px-4 py-3">
                          <p className="font-bold text-[#0F2854]">{n.userFullName}</p>
                          <p className="text-[11px] text-[#63738a]">
                            {n.userEmail} · <span className="font-mono">{n.userRole}</span>
                          </p>
                        </td>
                        <td className="px-4 py-3">
                          <span className="rounded-md bg-[#EBF4FA] px-2 py-0.5 font-mono text-[11px] font-bold text-[#1C4D8D]">
                            {n.type}
                          </span>
                        </td>
                        <td className="max-w-md px-4 py-3">
                          <p className="font-bold text-[#0F2854]">{n.title}</p>
                          <p className="mt-0.5 line-clamp-2 text-[11px] text-[#475569]">
                            {n.message}
                          </p>
                        </td>
                        <td className="px-4 py-3">
                          {n.isRead ? (
                            <span className="rounded-full bg-[#ecfdf5] px-2 py-0.5 font-bold text-[#059669]">
                              Đã đọc
                            </span>
                          ) : (
                            <span className="rounded-full bg-[#fffbeb] px-2 py-0.5 font-bold text-[#d97706]">
                              Chưa đọc
                            </span>
                          )}
                        </td>
                        <td className="px-4 py-3 text-right">
                          <button
                            type="button"
                            onClick={() => setDeletingNotif(n)}
                            className="inline-flex items-center gap-1 rounded-lg border border-[#ef4444]/40 px-2.5 py-1 text-[11px] font-bold text-[#dc2626] transition hover:bg-[#fef2f2]"
                          >
                            <Trash2 className="size-3" /> Thu hồi
                          </button>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>

            {notifTotalPages > 1 && (
              <div className="flex items-center justify-between border-t border-[#82aedb]/30 px-4 py-3 text-xs text-[#63738a]">
                <span>
                  Trang {notifPage} / {notifTotalPages} (Tổng cộng {notifTotal} thông báo)
                </span>
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    disabled={notifPage <= 1 || isNotifsLoading}
                    onClick={() => void loadNotifications(notifPage - 1)}
                    className="flex size-8 items-center justify-center rounded-lg border border-[#82aedb]/50 transition hover:bg-[#F4FBFD] disabled:opacity-40"
                  >
                    <ChevronLeft className="size-4" />
                  </button>
                  <button
                    type="button"
                    disabled={notifPage >= notifTotalPages || isNotifsLoading}
                    onClick={() => void loadNotifications(notifPage + 1)}
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

      {/* TAB 6: AUDIT LOGS */}
      {activeTab === 'audit' && (
        <section className="space-y-4">
          <div className="flex flex-col gap-3 rounded-2xl border border-[#82aedb]/50 bg-white p-4 shadow-sm lg:flex-row lg:items-center lg:justify-between">
            <div>
              <h2 className="text-base font-bold text-[#0F2854]">Nhật Ký Kiểm Toán (Audit Logs)</h2>
              <p className="mt-1 text-xs text-[#63738a]">
                Truy vết toàn bộ các thao tác nghiệp vụ quan trọng trong bảng <code className="font-mono">audit_logs</code> để bảo đảm tính minh bạch.
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <input
                value={logEntityFilter}
                onChange={(e) => setLogEntityFilter(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') void loadLogs(1)
                }}
                placeholder="Bảng (users, courses, semesters...)"
                className="rounded-xl border border-[#82aedb]/50 bg-[#F4FBFD]/50 px-3 py-1.5 text-xs text-[#0F2854] outline-none"
              />
              <input
                value={logActionFilter}
                onChange={(e) => setLogActionFilter(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') void loadLogs(1)
                }}
                placeholder="Hành động (USER_CREATED...)"
                className="rounded-xl border border-[#82aedb]/50 bg-[#F4FBFD]/50 px-3 py-1.5 font-mono text-xs text-[#0F2854] outline-none"
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

          {logsError && (
            <div
              role="alert"
              className="flex items-center justify-between gap-3 rounded-2xl border border-[#f0c5c9] bg-[#fff5f6] px-4 py-3 text-xs font-semibold text-[#a43d4b]"
            >
              <span>{logsError}</span>
              <button
                type="button"
                onClick={() => void loadLogs(logPage)}
                className="inline-flex items-center gap-1 rounded-lg border border-[#f0c5c9] bg-white px-3 py-1.5 font-bold text-[#a43d4b]"
              >
                <RefreshCw className="size-3.5" /> Thử lại
              </button>
            </div>
          )}

          <div className="overflow-hidden rounded-2xl border border-[#82aedb]/50 bg-white shadow-sm">
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="border-b border-[#82aedb]/40 bg-[#F4FBFD] font-bold text-[#0F2854] uppercase tracking-wider">
                  <tr>
                    <th className="px-4 py-3">Thời gian</th>
                    <th className="px-4 py-3">Người thực hiện</th>
                    <th className="px-4 py-3">Hành động</th>
                    <th className="px-4 py-3">Thực thể</th>
                    <th className="px-4 py-3">Dữ liệu trước / sau</th>
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
                      <td colSpan={6} className="p-5">
                        <EmptyState icon={FileText} title="Chưa có bản ghi kiểm toán phù hợp" description="Thử thay đổi bộ lọc hoặc kiểm tra lại sau." />
                      </td>
                    </tr>
                  ) : (
                    logs.map((log) => (
                      <tr key={log.id} className="transition hover:bg-[#F4FBFD]/50">
                        <td className="px-4 py-3 font-mono text-[#63738a]">
                          {new Date(log.createdAt).toLocaleString('vi-VN')}
                        </td>
                        <td className="px-4 py-3">
                          <p className="font-bold text-[#0F2854]">
                            {log.userFullName || 'Hệ thống'}
                          </p>
                          <p className="text-[11px] text-[#63738a]">{log.userEmail || 'system'}</p>
                        </td>
                        <td className="px-4 py-3">
                          <span
                            className={cn(
                              'inline-block rounded-md px-2 py-0.5 font-mono text-[11px] font-bold',
                              getAuditBadgeTone(log.action),
                            )}
                          >
                            {log.action}
                          </span>
                        </td>
                        <td className="px-4 py-3 font-mono text-[#0F2854]">
                          {log.entityName} {log.entityId ? `(#${log.entityId.slice(0, 8)})` : ''}
                        </td>
                        <td className="max-w-md px-4 py-3 font-mono text-[11px] text-[#475569]">
                          {log.oldValues && (
                            <p className="truncate text-[#94a3b8]">Trước: {log.oldValues}</p>
                          )}
                          <p className="truncate">{log.newValues || (!log.oldValues ? '—' : '')}</p>
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
                    onClick={() => setLogPage(logPage - 1)}
                    className="flex size-8 items-center justify-center rounded-lg border border-[#82aedb]/50 transition hover:bg-[#F4FBFD] disabled:opacity-40"
                  >
                    <ChevronLeft className="size-4" />
                  </button>
                  <button
                    type="button"
                    disabled={logPage >= logTotalPages || isLogsLoading}
                    onClick={() => setLogPage(logPage + 1)}
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
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Tạo tài khoản nhà trường cấp"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
        >
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
                aria-label="Đóng"
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
                  minLength={8}
                  value={createForm.password}
                  onChange={(e) => setCreateForm({ ...createForm, password: e.target.value })}
                  placeholder="Nhập mật khẩu tạm thời (tối thiểu 8 ký tự)"
                  className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 text-xs outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/20"
                />
                <p className="mt-1 text-[11px] text-[#63738a]">
                  Hệ thống tự động gắn cờ{' '}
                  <code className="font-bold text-[#d97706]">mustChangePassword = true</code>.
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
                  <label className="block font-bold text-[#0F2854]">
                    Mã sinh viên {createForm.role === 'student' ? '(*)' : '(Nếu là SV)'}
                  </label>
                  <input
                    type="text"
                    required={createForm.role === 'student'}
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
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Đổi vai trò người dùng"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
        >
          <div className="w-full max-w-sm rounded-3xl border border-[#82aedb]/50 bg-white p-6 shadow-2xl">
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-[#0F2854]">Đổi Vai Trò Người Dùng</h3>
              <button
                type="button"
                aria-label="Đóng"
                onClick={() => setEditRoleUser(null)}
                className="text-[#94a3b8] transition hover:text-[#0F2854]"
              >
                <X className="size-5" />
              </button>
            </div>
            <p className="mt-1 text-xs text-[#63738a]">
              Đang chỉnh sửa: <strong>{editRoleUser.fullName}</strong> ({editRoleUser.email})
            </p>
            <p className="mt-1 text-[11px] text-[#d97706]">
              Lưu ý: Đổi vai trò sẽ tăng <code className="font-mono">auth_version</code> và thu hồi ngay các phiên cũ.
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
        <div
          role="dialog"
          aria-modal="true"
          aria-label={statusUser.isActive ? 'Khóa tài khoản' : 'Mở khóa tài khoản'}
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
        >
          <div className="w-full max-w-sm rounded-3xl border border-[#82aedb]/50 bg-white p-6 shadow-2xl">
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-[#0F2854]">
                {statusUser.isActive ? 'Khóa Tài Khoản' : 'Mở Khóa Tài Khoản'}
              </h3>
              <button
                type="button"
                aria-label="Đóng"
                onClick={() => setStatusUser(null)}
                className="text-[#94a3b8] transition hover:text-[#0F2854]"
              >
                <X className="size-5" />
              </button>
            </div>
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
                    statusUser.isActive
                      ? 'bg-[#dc2626] hover:bg-[#b91c1c]'
                      : 'bg-[#059669] hover:bg-[#047857]',
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

      {/* MODAL: RESET USER PASSWORD */}
      {resetPwdUser && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Cấp lại mật khẩu tạm thời"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
        >
          <div className="w-full max-w-md rounded-3xl border border-[#82aedb]/50 bg-white p-6 shadow-2xl">
            <div className="flex items-center justify-between border-b border-[#82aedb]/30 pb-3">
              <div className="flex items-center gap-2">
                <KeyRound className="size-5 text-[#d97706]" />
                <h3 className="text-base font-bold text-[#0F2854]">Cấp Lại Mật Khẩu Tạm Thời</h3>
              </div>
              <button
                type="button"
                aria-label="Đóng"
                onClick={() => setResetPwdUser(null)}
                className="text-[#94a3b8] transition hover:text-[#0F2854]"
              >
                <X className="size-5" />
              </button>
            </div>
            <p className="mt-2 text-xs text-[#63738a]">
              Tài khoản: <strong>{resetPwdUser.fullName}</strong> ({resetPwdUser.email})
            </p>
            <p className="mt-1 text-[11px] text-[#b45309]">
              Hệ thống sẽ băm PBKDF2-SHA256, bật <code className="font-mono">mustChangePassword = true</code>, tăng <code className="font-mono">auth_version</code> và thu hồi mọi phiên đăng nhập cũ.
            </p>

            <form onSubmit={handleResetPassword} className="mt-4 space-y-3 text-xs">
              <div>
                <label htmlFor="resetTempPassword" className="block font-bold text-[#0F2854]">
                  Mật khẩu tạm thời mới (* ≥ 8 ký tự)
                </label>
                <input
                  id="resetTempPassword"
                  type="password"
                  required
                  minLength={8}
                  value={tempPassword}
                  onChange={(e) => setTempPassword(e.target.value)}
                  placeholder="Nhập mật khẩu tạm thời mới..."
                  className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 text-xs outline-none focus:border-[#4988C4]"
                />
              </div>
              <div>
                <label htmlFor="resetPwdReason" className="block font-bold text-[#0F2854]">
                  Lý do cấp lại (lưu Audit Log)
                </label>
                <input
                  id="resetPwdReason"
                  type="text"
                  value={resetPwdReason}
                  onChange={(e) => setResetPwdReason(e.target.value)}
                  placeholder="ví dụ: Sinh viên báo quên mật khẩu tại phòng Đào tạo"
                  className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 text-xs outline-none focus:border-[#4988C4]"
                />
              </div>
              <div className="flex items-center justify-end gap-2 pt-3">
                <button
                  type="button"
                  onClick={() => setResetPwdUser(null)}
                  className="rounded-xl border border-[#82aedb]/50 px-4 py-2 font-bold text-[#64748b]"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isResettingPwd}
                  className="inline-flex items-center gap-1.5 rounded-xl bg-[#d97706] px-4 py-2 font-bold text-white transition hover:bg-[#b45309] disabled:opacity-50"
                >
                  {isResettingPwd && <LoaderCircle className="size-3.5 animate-spin" />}
                  Xác nhận cấp lại
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: EDIT CONFIG */}
      {editConfig && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Chỉnh sửa tham số hệ thống"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
        >
          <div className="w-full max-w-md rounded-3xl border border-[#82aedb]/50 bg-white p-6 shadow-2xl">
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-[#0F2854]">Chỉnh Sửa Tham Số Hệ Thống</h3>
              <button
                type="button"
                aria-label="Đóng"
                onClick={() => setEditConfig(null)}
                className="text-[#94a3b8] transition hover:text-[#0F2854]"
              >
                <X className="size-5" />
              </button>
            </div>
            <p className="mt-1 font-mono text-xs font-bold text-[#1C4D8D]">{editConfig.key}</p>

            {(() => {
              const rule = editConfig ? MF01_CONFIG_RULES[editConfig.key] : undefined
              return (
                <form onSubmit={handleUpdateConfig} noValidate className="mt-4 space-y-3 text-xs">
                  <div>
                    <label className="block font-bold text-[#0F2854]">
                      Giá trị cấu hình (*)
                      {rule && (
                        <span className="ml-1 text-[11px] font-normal text-[#1C4D8D]">
                          (Miền hợp lệ: {rule.min} - {rule.max})
                        </span>
                      )}
                    </label>
                    <input
                      required
                      type={rule ? 'number' : 'text'}
                      min={rule?.min}
                      max={rule?.max}
                      step={rule ? '1' : undefined}
                      value={configValue}
                      onChange={(e) => setConfigValue(e.target.value)}
                      placeholder={rule ? `${rule.min} - ${rule.max}` : undefined}
                      className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 font-mono text-xs outline-none focus:border-[#4988C4]"
                    />
                    {rule && (
                      <p className="mt-1 text-[11px] leading-relaxed text-[#64748b]">
                        {rule.description}
                      </p>
                    )}
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
              )
            })()}
          </div>
        </div>
      )}

      {/* MODAL: CREATE / EDIT SEMESTER */}
      {showSemesterModal && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label={editingSemester ? 'Chỉnh sửa học kỳ' : 'Tạo học kỳ mới'}
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
        >
          <div className="w-full max-w-md rounded-3xl border border-[#82aedb]/50 bg-white p-6 shadow-2xl">
            <div className="flex items-center justify-between border-b border-[#82aedb]/30 pb-3">
              <div className="flex items-center gap-2">
                <Calendar className="size-5 text-[#1C4D8D]" />
                <h3 className="text-base font-bold text-[#0F2854]">
                  {editingSemester ? 'Chỉnh Sửa Học Kỳ' : 'Tạo Học Kỳ Mới'}
                </h3>
              </div>
              <button
                type="button"
                aria-label="Đóng"
                onClick={() => setShowSemesterModal(false)}
                className="text-[#94a3b8] transition hover:text-[#0F2854]"
              >
                <X className="size-5" />
              </button>
            </div>

            <form onSubmit={handleSaveSemester} className="mt-4 space-y-3 text-xs">
              <div>
                <label className="block font-bold text-[#0F2854]">Mã học kỳ (*)</label>
                <input
                  type="text"
                  required
                  value={semesterForm.code}
                  onChange={(e) => setSemesterForm({ ...semesterForm, code: e.target.value })}
                  placeholder="ví dụ: FA26, SP26, SU26"
                  className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 font-mono text-xs font-bold uppercase outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/20"
                />
              </div>

              <div>
                <label className="block font-bold text-[#0F2854]">Tên học kỳ (*)</label>
                <input
                  type="text"
                  required
                  value={semesterForm.name}
                  onChange={(e) => setSemesterForm({ ...semesterForm, name: e.target.value })}
                  placeholder="ví dụ: Fall 2026, Spring 2026"
                  className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 text-xs outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/20"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-bold text-[#0F2854]">Ngày bắt đầu (*)</label>
                  <input
                    type="date"
                    required
                    value={semesterForm.startDate}
                    onChange={(e) =>
                      setSemesterForm({ ...semesterForm, startDate: e.target.value })
                    }
                    className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 text-xs outline-none focus:border-[#4988C4]"
                  />
                </div>

                <div>
                  <label className="block font-bold text-[#0F2854]">Ngày kết thúc (*)</label>
                  <input
                    type="date"
                    required
                    value={semesterForm.endDate}
                    onChange={(e) => setSemesterForm({ ...semesterForm, endDate: e.target.value })}
                    className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 text-xs outline-none focus:border-[#4988C4]"
                  />
                </div>
              </div>

              <div className="flex items-center gap-2 pt-1">
                <input
                  type="checkbox"
                  id="semesterIsActive"
                  checked={semesterForm.isActive ?? true}
                  onChange={(e) => setSemesterForm({ ...semesterForm, isActive: e.target.checked })}
                  className="size-4 rounded border-gray-300 text-[#1C4D8D] focus:ring-[#4988C4]"
                />
                <label htmlFor="semesterIsActive" className="text-xs font-semibold text-[#0F2854]">
                  Đang mở hoạt động (isActive)
                </label>
              </div>

              <div className="rounded-xl border border-[#82aedb]/30 bg-[#F4FBFD] p-3 text-[11px] text-[#63738a]">
                <p>
                  <strong>Lưu ý:</strong> Học kỳ là dữ liệu gốc không thể xóa (FK ON DELETE
                  RESTRICT). Ngày kết thúc bắt buộc phải sau ngày bắt đầu.
                </p>
              </div>

              <div className="flex items-center justify-end gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setShowSemesterModal(false)}
                  className="rounded-xl border border-[#82aedb]/50 px-4 py-2 font-bold text-[#64748b] transition hover:bg-[#f1f5f9]"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isSavingSemester}
                  className="flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-[#1C4D8D] to-[#4988C4] px-4 py-2 font-bold text-white shadow-md transition hover:brightness-110 disabled:opacity-50"
                >
                  {isSavingSemester && <LoaderCircle className="size-3.5 animate-spin" />}
                  {editingSemester ? 'Cập nhật học kỳ' : 'Tạo học kỳ'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: SEMESTER COURSE OFFERINGS */}
      {offeringsSemester && (
        <AdminSemesterOfferingsModal
          semester={offeringsSemester}
          onClose={() => setOfferingsSemester(null)}
          onSaved={() => void loadSemesters()}
        />
      )}

      {/* MODAL: CREATE / EDIT COURSE */}
      {showCourseModal && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label={editingCourse ? 'Chỉnh sửa môn học' : 'Tạo môn học mới'}
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
        >
          <div className="w-full max-w-lg rounded-3xl border border-[#82aedb]/50 bg-white p-6 shadow-2xl">
            <div className="flex items-center justify-between border-b border-[#82aedb]/30 pb-3">
              <div className="flex items-center gap-2">
                <BookOpen className="size-5 text-[#1C4D8D]" />
                <h3 className="text-base font-bold text-[#0F2854]">
                  {editingCourse ? 'Chỉnh Sửa Môn Học' : 'Tạo Môn Học Mới'}
                </h3>
              </div>
              <button
                type="button"
                aria-label="Đóng"
                onClick={() => setShowCourseModal(false)}
                className="text-[#94a3b8] transition hover:text-[#0F2854]"
              >
                <X className="size-5" />
              </button>
            </div>

            <form onSubmit={handleSaveCourse} className="mt-4 space-y-3 text-xs">
              <div className="grid grid-cols-3 gap-3">
                <div className="col-span-1">
                  <label className="block font-bold text-[#0F2854]">Mã môn (*)</label>
                  <input
                    type="text"
                    required
                    value={courseCode}
                    onChange={(e) => setCourseCode(e.target.value)}
                    placeholder="ví dụ: PRN231"
                    className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 font-mono text-xs font-bold uppercase outline-none focus:border-[#4988C4]"
                  />
                </div>

                <div className="col-span-2">
                  <label className="block font-bold text-[#0F2854]">Tên môn học (*)</label>
                  <input
                    type="text"
                    required
                    value={courseName}
                    onChange={(e) => setCourseName(e.target.value)}
                    placeholder="ví dụ: Cross-Platform .NET"
                    className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 text-xs outline-none focus:border-[#4988C4]"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-bold text-[#0F2854]">Số tín chỉ (* 1–10)</label>
                  <input
                    type="number"
                    min={1}
                    max={10}
                    required
                    value={courseCreditsInput}
                    onChange={(e) => setCourseCreditsInput(e.target.value)}
                    className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 font-mono text-xs outline-none focus:border-[#4988C4]"
                  />
                </div>

                <div>
                  <label htmlFor="courseDeptHeadSelect" className="block font-bold text-[#0F2854]">
                    Trưởng bộ môn phụ trách
                  </label>
                  <select
                    id="courseDeptHeadSelect"
                    value={courseDeptHeadId}
                    onChange={(e) => setCourseDeptHeadId(e.target.value)}
                    className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 text-xs font-semibold outline-none"
                  >
                    <option value="">-- Chưa gán Trưởng BM --</option>
                    {departmentHeads.map((dh) => (
                      <option key={dh.id} value={dh.id}>
                        {dh.fullName} ({dh.email})
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <div>
                <label htmlFor="courseSemesterSelect" className="block font-bold text-[#0F2854]">
                  Học kỳ áp dụng (*)
                </label>
                <select
                  id="courseSemesterSelect"
                  required
                  value={courseSemesterId}
                  onChange={(e) => setCourseSemesterId(e.target.value)}
                  className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 text-xs font-semibold outline-none"
                >
                  <option value="">-- Chọn học kỳ (*) --</option>
                  {semesters.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.code} - {s.name}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block font-bold text-[#0F2854]">Mô tả tóm tắt môn học</label>
                <textarea
                  value={courseDescription}
                  onChange={(e) => setCourseDescription(e.target.value)}
                  placeholder="Mô tả nội dung môn học..."
                  className="mt-1 min-h-16 w-full rounded-xl border border-[#82aedb]/50 p-2 text-xs outline-none"
                />
              </div>

              <div className="rounded-xl border border-[#82aedb]/30 bg-[#F4FBFD] p-3 text-[11px] text-[#0F2854]">
                <label htmlFor="courseMaxMockQuota" className="block font-bold">
                  Hạn ngạch thi thử mỗi ngày (MF-02 — max_mock_exams_per_day, 1–20 lượt GMT+7):
                </label>
                <input
                  id="courseMaxMockQuota"
                  type="number"
                  min={1}
                  max={20}
                  required
                  value={courseMaxMockInput}
                  onChange={(e) => setCourseMaxMockInput(e.target.value)}
                  className="mt-1.5 w-40 rounded-lg border border-[#82aedb]/50 bg-white px-2.5 py-1.5 font-mono text-xs font-bold"
                />
                <p className="mt-1 text-[#63738a]">
                  Cấu trúc số câu, phân bổ Bloom và thời lượng thi thử thuộc thẩm quyền Trưởng bộ môn tại mục Cấu trúc đề thi (<code className="font-mono">exam_structures</code>).
                </p>
              </div>

              <div className="flex items-center gap-2 pt-1">
                <input
                  type="checkbox"
                  id="courseIsActive"
                  checked={courseIsActive}
                  onChange={(e) => setCourseIsActive(e.target.checked)}
                  className="size-4 rounded border-gray-300 text-[#1C4D8D] focus:ring-[#4988C4]"
                />
                <label htmlFor="courseIsActive" className="text-xs font-semibold text-[#0F2854]">
                  Đang mở giảng dạy (isActive)
                </label>
              </div>

              <div className="flex items-center justify-end gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setShowCourseModal(false)}
                  className="rounded-xl border border-[#82aedb]/50 px-4 py-2 font-bold text-[#64748b] transition hover:bg-[#f1f5f9]"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isSavingCourse}
                  className="flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-[#1C4D8D] to-[#4988C4] px-4 py-2 font-bold text-white shadow-md transition hover:brightness-110 disabled:opacity-50"
                >
                  {isSavingCourse && <LoaderCircle className="size-3.5 animate-spin" />}
                  {editingCourse ? 'Cập nhật môn học' : 'Tạo môn học'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: COURSE CLOS 3NF */}
      {closCourse && (
        <AdminCourseClosModal course={closCourse} onClose={() => setClosCourse(null)} />
      )}

      {/* MODAL: ASSIGN DEPARTMENT HEAD */}
      {showAssignHeadModal && assignHeadCourse && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Gán Trưởng bộ môn"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
        >
          <div className="w-full max-w-sm rounded-3xl border border-[#82aedb]/50 bg-white p-6 shadow-2xl">
            <div className="flex items-center justify-between border-b border-[#82aedb]/30 pb-3">
              <div className="flex items-center gap-2">
                <GraduationCap className="size-5 text-[#1C4D8D]" />
                <h3 className="text-base font-bold text-[#0F2854]">Gán Trưởng Bộ Môn</h3>
              </div>
              <button
                type="button"
                aria-label="Đóng"
                onClick={() => setShowAssignHeadModal(false)}
                className="text-[#94a3b8] transition hover:text-[#0F2854]"
              >
                <X className="size-5" />
              </button>
            </div>

            <p className="mt-3 text-xs text-[#63738a]">
              Phân công Trưởng bộ môn phụ trách học thuật cho môn:
            </p>
            <p className="mt-1 font-bold text-[#0F2854]">
              {assignHeadCourse.code} — {assignHeadCourse.name}
            </p>

            <form onSubmit={handleAssignHead} className="mt-4 space-y-3 text-xs">
              <div>
                <label className="block font-bold text-[#0F2854]">Chọn Trưởng bộ môn (*)</label>
                <select
                  required
                  value={selectedHeadUserId}
                  onChange={(e) => setSelectedHeadUserId(e.target.value)}
                  className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 text-xs font-semibold outline-none focus:border-[#4988C4]"
                >
                  <option value="">-- Chọn giảng viên giữ vai trò department_head --</option>
                  {departmentHeads.map((dh) => (
                    <option key={dh.id} value={dh.id}>
                      {dh.fullName} ({dh.email})
                    </option>
                  ))}
                </select>
              </div>

              <div className="flex items-center justify-end gap-2 pt-4">
                <button
                  type="button"
                  onClick={() => setShowAssignHeadModal(false)}
                  className="rounded-xl border border-[#82aedb]/50 px-4 py-2 font-bold text-[#64748b] transition hover:bg-[#f1f5f9]"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isAssigningHead}
                  className="flex items-center gap-1.5 rounded-xl bg-[#0F2854] px-4 py-2 font-bold text-white transition hover:bg-[#1C4D8D] disabled:opacity-50"
                >
                  {isAssigningHead && <LoaderCircle className="size-3.5 animate-spin" />}
                  Lưu phân công
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: SEND NOTIFICATION 3NF */}
      {showSendNotifModal && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Phát thông báo hệ thống mới"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
        >
          <div className="w-full max-w-lg rounded-3xl border border-[#82aedb]/50 bg-white p-6 shadow-2xl">
            <div className="flex items-center justify-between border-b border-[#82aedb]/30 pb-3">
              <div className="flex items-center gap-2">
                <Bell className="size-5 text-[#1C4D8D]" />
                <h3 className="text-base font-bold text-[#0F2854]">
                  Phát Thông Báo Nội Bộ Mới (3NF)
                </h3>
              </div>
              <button
                type="button"
                aria-label="Đóng"
                onClick={() => setShowSendNotifModal(false)}
                className="text-[#94a3b8] transition hover:text-[#0F2854]"
              >
                <X className="size-5" />
              </button>
            </div>

            <form onSubmit={handleSendNotification} className="mt-4 space-y-3 text-xs">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label htmlFor="notifTargetScope" className="block font-bold text-[#0F2854]">
                    Phạm vi nhận (*)
                  </label>
                  <select
                    id="notifTargetScope"
                    value={notifForm.targetScope}
                    onChange={(e) => {
                      const nextScope = e.target.value as 'ALL_ACTIVE' | 'ROLE' | 'USER'
                      setNotifForm({
                        ...notifForm,
                        targetScope: nextScope,
                      })
                      if (nextScope === 'USER') {
                        void loadRecipientUsers(recipientSearch)
                      }
                    }}
                    className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 font-semibold outline-none"
                  >
                    <option value="ALL_ACTIVE">Toàn bộ tài khoản active</option>
                    <option value="ROLE">Theo vai trò RBAC</option>
                    <option value="USER">Cá nhân cụ thể</option>
                  </select>
                </div>
                <div>
                  <label htmlFor="notifType" className="block font-bold text-[#0F2854]">
                    Phân loại (*)
                  </label>
                  <select
                    id="notifType"
                    value={notifForm.type}
                    onChange={(e) => setNotifForm({ ...notifForm, type: e.target.value })}
                    className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 font-semibold outline-none"
                  >
                    {NOTIFICATION_TYPES.map((t) => (
                      <option key={t.value} value={t.value}>
                        {t.label}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              {notifForm.targetScope === 'ROLE' && (
                <div>
                  <label htmlFor="notifTargetRole" className="block font-bold text-[#0F2854]">
                    Vai trò nhận thông báo (*)
                  </label>
                  <select
                    id="notifTargetRole"
                    value={notifForm.targetRole || 'student'}
                    onChange={(e) => setNotifForm({ ...notifForm, targetRole: e.target.value })}
                    className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 font-semibold outline-none"
                  >
                    {SYSTEM_ROLES.map((r) => (
                      <option key={r.value} value={r.value}>
                        {r.label}
                      </option>
                    ))}
                  </select>
                </div>
              )}

              {notifForm.targetScope === 'USER' && (
                <div className="space-y-2">
                  <div>
                    <label htmlFor="notifRecipientSearch" className="block font-bold text-[#0F2854]">
                      Tìm tài khoản người nhận (Email / Họ tên / MSSV)
                    </label>
                    <div className="mt-1 flex items-center gap-2">
                      <input
                        id="notifRecipientSearch"
                        type="text"
                        value={recipientSearch}
                        onChange={(e) => setRecipientSearch(e.target.value)}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter') {
                            e.preventDefault()
                            void loadRecipientUsers(recipientSearch)
                          }
                        }}
                        placeholder="Nhập MSSV, email hoặc họ tên rồi bấm Tìm..."
                        className="flex-1 rounded-xl border border-[#82aedb]/50 px-3 py-1.5 text-xs outline-none focus:border-[#4988C4]"
                      />
                      <button
                        type="button"
                        disabled={isLoadingRecipients}
                        onClick={() => void loadRecipientUsers(recipientSearch)}
                        className="inline-flex items-center gap-1 rounded-xl bg-[#0F2854] px-3 py-1.5 text-xs font-bold text-white transition hover:bg-[#1C4D8D] disabled:opacity-50"
                      >
                        {isLoadingRecipients ? (
                          <LoaderCircle className="size-3.5 animate-spin" />
                        ) : (
                          <Search className="size-3.5" />
                        )}
                        Tìm
                      </button>
                    </div>
                  </div>

                  {recipientsError && (
                    <div
                      role="alert"
                      className="flex items-center justify-between gap-2 rounded-xl border border-[#f0c5c9] bg-[#fff5f6] px-3 py-2 text-[11px] font-semibold text-[#a43d4b]"
                    >
                      <span>{recipientsError}</span>
                      <button
                        type="button"
                        onClick={() => void loadRecipientUsers(recipientSearch)}
                        className="shrink-0 rounded-lg border border-[#f0c5c9] bg-white px-2 py-1 font-bold text-[#a43d4b]"
                      >
                        Thử lại
                      </button>
                    </div>
                  )}

                  <div>
                    <label htmlFor="notifTargetUser" className="block font-bold text-[#0F2854]">
                      Tài khoản nhận thông báo (*)
                    </label>
                    <select
                      id="notifTargetUser"
                      required
                      value={notifForm.targetUserId || ''}
                      onChange={(e) => setNotifForm({ ...notifForm, targetUserId: e.target.value })}
                      className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 font-semibold outline-none"
                    >
                      <option value="">
                        {isLoadingRecipients
                          ? '-- Đang tìm tài khoản active... --'
                          : `-- Chọn tài khoản đang hoạt động (${recipientUsers.length} kết quả) --`}
                      </option>
                      {recipientUsers.map((u) => (
                        <option key={u.id} value={u.id}>
                          {u.fullName} ({u.email} · {u.role})
                        </option>
                      ))}
                    </select>
                  </div>
                </div>
              )}

              <div>
                <label htmlFor="notifTitle" className="block font-bold text-[#0F2854]">
                  Tiêu đề thông báo (* ≤ 255 ký tự)
                </label>
                <input
                  id="notifTitle"
                  type="text"
                  required
                  maxLength={255}
                  value={notifForm.title}
                  onChange={(e) => setNotifForm({ ...notifForm, title: e.target.value })}
                  placeholder="ví dụ: Thông báo lịch bảo trì hệ thống luyện thi vấn đáp"
                  className="mt-1 w-full rounded-xl border border-[#82aedb]/50 px-3 py-2 text-xs outline-none focus:border-[#4988C4]"
                />
              </div>

              <div>
                <label htmlFor="notifMessage" className="block font-bold text-[#0F2854]">
                  Nội dung thông báo (* ≤ 2000 ký tự)
                </label>
                <textarea
                  id="notifMessage"
                  required
                  maxLength={2000}
                  value={notifForm.message}
                  onChange={(e) => setNotifForm({ ...notifForm, message: e.target.value })}
                  placeholder="Nhập nội dung chi tiết..."
                  className="mt-1 min-h-24 w-full rounded-xl border border-[#82aedb]/50 p-2.5 text-xs outline-none focus:border-[#4988C4]"
                />
              </div>

              <div className="flex items-center justify-end gap-2 pt-3">
                <button
                  type="button"
                  onClick={() => setShowSendNotifModal(false)}
                  className="rounded-xl border border-[#82aedb]/50 px-4 py-2 font-bold text-[#64748b]"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isSendingNotif}
                  className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-[#1C4D8D] to-[#4988C4] px-4 py-2 font-bold text-white shadow-md transition hover:brightness-110 disabled:opacity-50"
                >
                  {isSendingNotif ? (
                    <LoaderCircle className="size-3.5 animate-spin" />
                  ) : (
                    <Send className="size-3.5" />
                  )}
                  Gửi thông báo
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: CONFIRM DELETE NOTIFICATION */}
      {deletingNotif && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Xác nhận thu hồi thông báo"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
        >
          <div className="w-full max-w-md rounded-3xl border border-[#f0c5c9] bg-white p-6 shadow-2xl">
            <div className="flex items-center gap-2 text-[#dc2626]">
              <AlertTriangle className="size-5" />
              <h3 className="text-base font-bold">Thu Hồi Thông Báo</h3>
            </div>
            <p className="mt-2 text-xs text-[#475569]">
              Bạn có chắc chắn muốn thu hồi thông báo{' '}
              <strong className="text-[#0F2854]">"{deletingNotif.title}"</strong> đã gửi tới{' '}
              <strong>{deletingNotif.userEmail}</strong>? Hành động sẽ được ghi vào Audit Log.
            </p>
            <div className="mt-5 flex justify-end gap-2 text-xs">
              <button
                type="button"
                onClick={() => setDeletingNotif(null)}
                className="rounded-xl border border-[#82aedb]/50 px-4 py-2 font-bold text-[#64748b]"
              >
                Hủy
              </button>
              <button
                type="button"
                disabled={isDeletingNotif}
                onClick={() => void handleConfirmDeleteNotification()}
                className="inline-flex items-center gap-1.5 rounded-xl bg-[#dc2626] px-4 py-2 font-bold text-white transition hover:bg-[#b91c1c] disabled:opacity-50"
              >
                {isDeletingNotif && <LoaderCircle className="size-3.5 animate-spin" />}
                Xác nhận thu hồi
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
