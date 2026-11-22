import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import {
  Eye,
  EyeOff,
  KeyRound,
  Lock,
  Pencil,
  RefreshCw,
  ShieldCheck,
  Unlock,
  UserCheck,
  UserPlus,
  UsersRound,
  UserX,
} from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FilterDropMenu, type FilterDropMenuOption } from '@/components/common/FilterDropMenu'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass } from '@/components/common/formFieldClasses'
import { ListPagination } from '@/components/common/ListPagination'
import { LoadingState } from '@/components/common/LoadingState'
import { SearchField } from '@/components/common/SearchField'
import { SubmitDialogFooter } from '@/components/common/SubmitDialogFooter'
import { toast } from '@/components/common/toastStore'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { BannerFact, PageBanner, SectionHeader } from '@/design/ui'
import { useAuth } from '@/hooks/useAuth'
import { useSearchInput } from '@/hooks/useSearchInput'
import { useUrlQueryState } from '@/hooks/useUrlQueryState'
import {
  extractProblemDetails,
  mapProblemFieldErrors,
  type NormalizedProblem,
} from '@/lib/problemDetails'
import { cn } from '@/lib/utils'
import {
  adminService,
  DEFAULT_ADMIN_PAGE_SIZE,
  type AdminUserItem,
  type CreateUserPayload,
  type UpdateUserPayload,
} from '@/services/admin.service'
import {
  ADMIN_ROLE_OPTIONS,
  getAdminRoleShortLabel,
  normalizeAdminRoleValue,
  USER_STATUS_FILTER_OPTIONS,
} from '@/pages/admin/adminConstants'
import { AdminStateBanner } from '@/pages/admin/components/AdminStateBanner'

const ROLE_FILTER_OPTIONS: FilterDropMenuOption[] = [
  { value: '', label: 'Tất cả vai trò' },
  ...ADMIN_ROLE_OPTIONS.map((item) => ({ value: item.value, label: item.shortLabel })),
]

const ADMIN_ROLE_VALUES = new Set(ADMIN_ROLE_OPTIONS.map((item) => item.value))
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const USERNAME_PATTERN = /^[a-z0-9._-]+$/

const STATUS_VALUES = ['all', 'active', 'locked'] as const
type UserStatusFilter = (typeof STATUS_VALUES)[number]

type CreateUserField = 'email' | 'fullName' | 'password' | 'role' | 'studentCode' | 'username'
type EditProfileField = 'email' | 'fullName' | 'username' | 'studentCode'
type ResetPasswordField = 'temporaryPassword' | 'reason'

const CREATE_FIELDS: readonly CreateUserField[] = [
  'email',
  'fullName',
  'password',
  'role',
  'studentCode',
  'username',
]
const EDIT_PROFILE_FIELDS: readonly EditProfileField[] = [
  'email',
  'fullName',
  'username',
  'studentCode',
]
const RESET_FIELDS: readonly ResetPasswordField[] = ['temporaryPassword', 'reason']

function formatDate(raw: string): string {
  const parsed = new Date(raw)
  return Number.isNaN(parsed.getTime()) ? raw : parsed.toLocaleDateString('vi-VN')
}

export function AdminUsersPage() {
  const { user: currentUser } = useAuth()
  const { getString, getEnum, getPage, setParams, resetParams } = useUrlQueryState({
    q: '',
    role: '',
    status: 'all',
    page: 1,
  })

  const urlQuery = getString('q')
  const rawRoleFilter = normalizeAdminRoleValue(getString('role'))
  const roleFilter = ADMIN_ROLE_VALUES.has(rawRoleFilter) ? rawRoleFilter : ''
  const statusFilter: UserStatusFilter = getEnum('status', STATUS_VALUES, 'all')
  const page = getPage('page', 1)

  const searchInput = useSearchInput((nextQuery) => {
    setParams({ q: nextQuery })
  }, urlQuery)

  const [users, setUsers] = useState<AdminUserItem[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [totalPages, setTotalPages] = useState(0)
  const [loading, setLoading] = useState(true)
  const [listProblem, setListProblem] = useState<NormalizedProblem | null>(null)
  const [statusMessage, setStatusMessage] = useState('')
  const requestIdRef = useRef(0)

  // Create user dialog
  const [showCreateDialog, setShowCreateDialog] = useState(false)
  const [createForm, setCreateForm] = useState<CreateUserPayload>({
    email: '',
    fullName: '',
    password: '',
    role: 'student',
    studentCode: '',
    username: '',
  })
  const [createFieldErrors, setCreateFieldErrors] = useState<Partial<Record<CreateUserField, string>>>(
    {},
  )
  const [createDialogProblem, setCreateDialogProblem] = useState<NormalizedProblem | null>(null)
  const [showCreatePassword, setShowCreatePassword] = useState(false)
  const [isCreatingUser, setIsCreatingUser] = useState(false)

  // Edit profile dialog (R3)
  const [editProfileUser, setEditProfileUser] = useState<AdminUserItem | null>(null)
  const [editProfileForm, setEditProfileForm] = useState({
    email: '',
    fullName: '',
    username: '',
    studentCode: '',
  })
  const [editProfileFieldErrors, setEditProfileFieldErrors] = useState<
    Partial<Record<EditProfileField, string>>
  >({})
  const [editProfileProblem, setEditProfileProblem] = useState<NormalizedProblem | null>(null)
  const [isUpdatingProfile, setIsUpdatingProfile] = useState(false)

  // Edit role dialog
  const [editRoleUser, setEditRoleUser] = useState<AdminUserItem | null>(null)
  const [nextRole, setNextRole] = useState('')
  const [roleDialogProblem, setRoleDialogProblem] = useState<NormalizedProblem | null>(null)
  const [isUpdatingRole, setIsUpdatingRole] = useState(false)

  // Lock / Unlock confirm dialog
  const [statusUser, setStatusUser] = useState<AdminUserItem | null>(null)
  const [statusReason, setStatusReason] = useState('')
  const [statusDialogProblem, setStatusDialogProblem] = useState<NormalizedProblem | null>(null)
  const [isUpdatingStatus, setIsUpdatingStatus] = useState(false)

  // Reset password dialog
  const [resetPwdUser, setResetPwdUser] = useState<AdminUserItem | null>(null)
  const [tempPassword, setTempPassword] = useState('')
  const [showResetPassword, setShowResetPassword] = useState(false)
  const [resetReason, setResetReason] = useState('')
  const [resetFieldErrors, setResetFieldErrors] = useState<
    Partial<Record<ResetPasswordField, string>>
  >({})
  const [resetDialogProblem, setResetDialogProblem] = useState<NormalizedProblem | null>(null)
  const [isResettingPwd, setIsResettingPwd] = useState(false)

  const loadUsers = useCallback(async () => {
    const requestId = ++requestIdRef.current
    setLoading(true)
    setListProblem(null)
    try {
      const result = await adminService.getUsers({
        search: urlQuery || undefined,
        role: roleFilter || undefined,
        isActive: statusFilter === 'all' ? undefined : statusFilter === 'active',
        page,
        pageSize: DEFAULT_ADMIN_PAGE_SIZE,
      })
      if (requestId !== requestIdRef.current) return
      setUsers(result.items ?? [])
      setTotalCount(result.totalCount ?? 0)
      setTotalPages(result.totalPages ?? 0)
    } catch (err: unknown) {
      if (requestId !== requestIdRef.current) return
      const parsed = extractProblemDetails(err, 'Không thể tải danh sách người dùng.')
      setListProblem(parsed)
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false)
      }
    }
  }, [page, roleFilter, statusFilter, urlQuery])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadUsers()
    }, 0)
    return () => {
      window.clearTimeout(timer)
      requestIdRef.current += 1
    }
  }, [loadUsers])

  const hasActiveFilters = Boolean(urlQuery || roleFilter || statusFilter !== 'all')

  const handleOpenCreate = () => {
    setCreateForm({
      email: '',
      fullName: '',
      password: '',
      role: 'student',
      studentCode: '',
      username: '',
    })
    setCreateFieldErrors({})
    setCreateDialogProblem(null)
    setShowCreatePassword(false)
    setShowCreateDialog(true)
  }

  const handleCreateUser = async (event: FormEvent) => {
    event.preventDefault()
    if (isCreatingUser) return

    const normalizedEmail = createForm.email.trim().toLowerCase()
    const normalizedFullName = createForm.fullName.trim()
    const isStudent = createForm.role === 'student'
    const normalizedStudentCode = createForm.studentCode?.trim().toUpperCase() ?? ''
    const normalizedUsername = createForm.username?.trim().toLowerCase() ?? ''

    const errors: Partial<Record<CreateUserField, string>> = {}
    if (!normalizedEmail) {
      errors.email = 'Vui lòng nhập email tài khoản.'
    } else if (!EMAIL_PATTERN.test(normalizedEmail)) {
      errors.email = 'Email không đúng định dạng.'
    } else if (normalizedEmail.length > 255) {
      errors.email = 'Email tối đa 255 ký tự.'
    }
    if (normalizedFullName.length < 2 || normalizedFullName.length > 150) {
      errors.fullName = 'Họ và tên phải từ 2 đến 150 ký tự.'
    }
    if (createForm.password.length < 8) {
      errors.password = 'Mật khẩu tạm thời phải có tối thiểu 8 ký tự.'
    }
    if (isStudent) {
      if (!normalizedStudentCode) {
        errors.studentCode = 'Vai trò sinh viên bắt buộc nhập Mã sinh viên (MSSV).'
      } else if (normalizedStudentCode.length > 20) {
        errors.studentCode = 'Mã sinh viên (MSSV) tối đa 20 ký tự.'
      }
    } else if (normalizedUsername) {
      if (normalizedUsername.length < 3 || normalizedUsername.length > 32) {
        errors.username = 'Tên đăng nhập phải từ 3 đến 32 ký tự.'
      } else if (!USERNAME_PATTERN.test(normalizedUsername)) {
        errors.username =
          'Tên đăng nhập chỉ được chứa chữ thường (a-z), số (0-9), dấu chấm (.), gạch dưới (_) và gạch ngang (-).'
      }
    }

    if (Object.keys(errors).length > 0) {
      setCreateFieldErrors(errors)
      return
    }

    setIsCreatingUser(true)
    setCreateFieldErrors({})
    setCreateDialogProblem(null)
    try {
      const payload: CreateUserPayload = {
        email: normalizedEmail,
        fullName: normalizedFullName,
        password: createForm.password,
        role: createForm.role,
        ...(isStudent
          ? { studentCode: normalizedStudentCode }
          : normalizedUsername
            ? { username: normalizedUsername }
            : {}),
      }
      await adminService.createUser(payload)
      const successMsg = `Đã tạo tài khoản ${normalizedEmail} (buộc đổi mật khẩu lần đầu).`
      toast.success(successMsg)
      setStatusMessage(successMsg)
      setShowCreateDialog(false)
      if (page !== 1) {
        setParams({ page: 1 })
      } else {
        void loadUsers()
      }
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Không thể tạo tài khoản người dùng.')
      setCreateFieldErrors(
        mapProblemFieldErrors(err, CREATE_FIELDS, {
          email: 'email',
          fullName: 'fullName',
          password: 'password',
          role: 'role',
          studentCode: 'studentCode',
          username: 'username',
        }),
      )
      setCreateDialogProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setIsCreatingUser(false)
    }
  }

  const handleOpenEditProfile = (item: AdminUserItem) => {
    setEditProfileUser(item)
    setEditProfileForm({
      email: item.email,
      fullName: item.fullName,
      username: item.username ?? '',
      studentCode: item.studentCode ?? '',
    })
    setEditProfileFieldErrors({})
    setEditProfileProblem(null)
  }

  const handleUpdateProfile = async (event: FormEvent) => {
    event.preventDefault()
    if (isUpdatingProfile || !editProfileUser) return

    const normalizedEmail = editProfileForm.email.trim().toLowerCase()
    const normalizedFullName = editProfileForm.fullName.trim()
    const isStudent = normalizeAdminRoleValue(editProfileUser.role) === 'student'
    const normalizedStudentCode = editProfileForm.studentCode.trim().toUpperCase()
    const normalizedUsername = editProfileForm.username.trim().toLowerCase()

    const errors: Partial<Record<EditProfileField, string>> = {}
    if (!normalizedEmail) {
      errors.email = 'Vui lòng nhập email tài khoản.'
    } else if (!EMAIL_PATTERN.test(normalizedEmail)) {
      errors.email = 'Email không đúng định dạng.'
    } else if (normalizedEmail.length > 255) {
      errors.email = 'Email tối đa 255 ký tự.'
    }
    if (normalizedFullName.length < 2 || normalizedFullName.length > 150) {
      errors.fullName = 'Họ và tên phải từ 2 đến 150 ký tự.'
    }
    if (isStudent) {
      if (!normalizedStudentCode) {
        errors.studentCode = 'Vai trò sinh viên bắt buộc nhập Mã sinh viên (MSSV).'
      } else if (normalizedStudentCode.length > 20) {
        errors.studentCode = 'Mã sinh viên (MSSV) tối đa 20 ký tự.'
      }
    } else if (normalizedUsername) {
      if (normalizedUsername.length < 3 || normalizedUsername.length > 32) {
        errors.username = 'Tên đăng nhập phải từ 3 đến 32 ký tự.'
      } else if (!USERNAME_PATTERN.test(normalizedUsername)) {
        errors.username =
          'Tên đăng nhập chỉ được chứa chữ thường (a-z), số (0-9), dấu chấm (.), gạch dưới (_) và gạch ngang (-).'
      }
    }

    if (Object.keys(errors).length > 0) {
      setEditProfileFieldErrors(errors)
      return
    }

    setIsUpdatingProfile(true)
    setEditProfileFieldErrors({})
    setEditProfileProblem(null)
    try {
      const payload: UpdateUserPayload = {
        email: normalizedEmail,
        fullName: normalizedFullName,
        ...(isStudent
          ? { studentCode: normalizedStudentCode }
          : { username: normalizedUsername || null, studentCode: null }),
      }
      const updated = await adminService.updateUser(editProfileUser.id, payload)
      const emailChanged = normalizedEmail !== editProfileUser.email.trim().toLowerCase()
      const usernameChanged =
        !isStudent &&
        (normalizedUsername || null) !== (editProfileUser.username?.trim().toLowerCase() || null)
      const authNotice =
        emailChanged || usernameChanged
          ? ' (đã tăng auth_version và thu hồi phiên đăng nhập cũ)'
          : ''
      const successMsg = `Đã cập nhật hồ sơ cho ${updated.email}${authNotice}.`
      toast.success(successMsg)
      setStatusMessage(successMsg)
      setEditProfileUser(null)
      void loadUsers()
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Không thể cập nhật thông tin tài khoản.')
      setEditProfileFieldErrors(
        mapProblemFieldErrors(err, EDIT_PROFILE_FIELDS, {
          email: 'email',
          fullName: 'fullName',
          username: 'username',
          studentCode: 'studentCode',
        }),
      )
      setEditProfileProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setIsUpdatingProfile(false)
    }
  }

  const handleUpdateRole = async (event: FormEvent) => {
    event.preventDefault()
    if (isUpdatingRole || !editRoleUser || !nextRole) return

    setIsUpdatingRole(true)
    setRoleDialogProblem(null)
    try {
      await adminService.updateUserRole(editRoleUser.id, { role: nextRole })
      const successMsg = `Đã đổi vai trò cho ${editRoleUser.fullName} thành ${getAdminRoleShortLabel(nextRole)} và thu hồi phiên đăng nhập cũ.`
      toast.success(successMsg)
      setStatusMessage(successMsg)
      setEditRoleUser(null)
      void loadUsers()
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Không thể cập nhật vai trò người dùng.')
      setRoleDialogProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setIsUpdatingRole(false)
    }
  }

  const handleConfirmToggleStatus = async () => {
    if (isUpdatingStatus || !statusUser) return
    const targetActive = !statusUser.isActive

    setIsUpdatingStatus(true)
    setStatusDialogProblem(null)
    try {
      await adminService.updateUserStatus(statusUser.id, {
        isActive: targetActive,
        reason: statusReason.trim() || undefined,
      })
      const successMsg = targetActive
        ? `Đã mở khóa tài khoản ${statusUser.email}.`
        : `Đã khóa tài khoản ${statusUser.email} và tăng auth_version để thu hồi phiên.`
      toast.success(successMsg)
      setStatusMessage(successMsg)
      setStatusUser(null)
      setStatusReason('')
      void loadUsers()
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Không thể cập nhật trạng thái tài khoản.')
      setStatusDialogProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setIsUpdatingStatus(false)
    }
  }

  const handleResetPassword = async (event: FormEvent) => {
    event.preventDefault()
    if (isResettingPwd || !resetPwdUser) return

    const errors: Partial<Record<ResetPasswordField, string>> = {}
    if (tempPassword.trim().length < 8 || tempPassword.trim().length > 128) {
      errors.temporaryPassword = 'Mật khẩu tạm thời phải từ 8 đến 128 ký tự.'
    }
    if (resetReason.trim().length > 500) {
      errors.reason = 'Lý do cấp lại tối đa 500 ký tự.'
    }

    if (Object.keys(errors).length > 0) {
      setResetFieldErrors(errors)
      return
    }

    setIsResettingPwd(true)
    setResetFieldErrors({})
    setResetDialogProblem(null)
    try {
      await adminService.resetUserPassword(resetPwdUser.id, {
        temporaryPassword: tempPassword,
        reason: resetReason.trim() || undefined,
      })
      const successMsg = `Đã cấp lại mật khẩu tạm thời cho ${resetPwdUser.email} (mustChangePassword = true).`
      toast.success(successMsg)
      setStatusMessage(successMsg)
      setResetPwdUser(null)
      setTempPassword('')
      setShowResetPassword(false)
      setResetReason('')
      void loadUsers()
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Không thể cấp lại mật khẩu tạm thời.')
      setResetFieldErrors(mapProblemFieldErrors(err, RESET_FIELDS))
      setResetDialogProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setIsResettingPwd(false)
    }
  }

  return (
    <div className={ds.page}>
      <PageBanner
        variant="calm"
        icon={UsersRound}
        eyebrow="Quản trị tài khoản · 6 vai trò RBAC"
        title="Quản lý người dùng"
        description="Tìm kiếm, tạo tài khoản (buộc đổi mật khẩu lần đầu), cập nhật hồ sơ, điều chỉnh vai trò, khóa/mở tài khoản và cấp lại mật khẩu tạm thời kèm thu hồi phiên qua auth_version."
        extras={
          <>
            <BannerFact icon={UsersRound} surface="calm">
              <span className="tabular-nums">Tổng tài khoản: {totalCount.toLocaleString('vi-VN')}</span>
            </BannerFact>
            <BannerFact icon={ShieldCheck} tone="green" surface="calm">
              Thu hồi phiên tức thì (auth_version)
            </BannerFact>
          </>
        }
      />

      <AdminStateBanner
        problem={listProblem}
        statusMessage={statusMessage}
        onRetry={listProblem ? () => void loadUsers() : undefined}
        onDismiss={() => {
          setListProblem(null)
          setStatusMessage('')
        }}
      />

      <section className="overflow-hidden rounded-2xl border border-[#0F2854]/10 bg-white">
        <SectionHeader
          variant="calm"
          icon={UsersRound}
          title="Danh sách tài khoản"
          badge={<span className="tabular-nums">{totalCount}</span>}
          hint="Lọc theo từ khóa, vai trò RBAC hoặc trạng thái hoạt động"
          action={
            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                onClick={() => void loadUsers()}
                disabled={loading}
                className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-[#0F2854]/15 bg-white px-3.5 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50 motion-reduce:transition-none"
              >
                <RefreshCw
                  className={cn('size-4 motion-reduce:animate-none', loading && 'animate-spin')}
                  strokeWidth={1.5}
                  aria-hidden="true"
                />
                Làm mới
              </button>
              <button
                type="button"
                onClick={handleOpenCreate}
                className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-xs font-medium text-white transition-colors duration-150 hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
              >
                <UserPlus className="size-4" strokeWidth={1.5} aria-hidden="true" />
                Tạo tài khoản mới
              </button>
            </div>
          }
        />

        {/* Toolbar */}
        <div className="flex flex-col gap-3 border-b border-[#0F2854]/8 bg-white p-4 sm:flex-row sm:flex-wrap sm:items-center sm:p-5">
          <div className="relative min-w-0 flex-1 sm:min-w-[220px]">
            <SearchField
              aria-label="Tìm người dùng theo email, họ tên hoặc MSSV"
              placeholder="Tìm email, họ tên, MSSV…"
              value={searchInput.draft}
              onChange={(event) => searchInput.change(event.target.value)}
              onKeyDown={searchInput.onKeyDown}
              className="h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-normal"
            />
          </div>

          <FilterDropMenu
            label="Vai trò"
            value={roleFilter}
            options={ROLE_FILTER_OPTIONS}
            onChange={(value) => setParams({ role: value })}
            className="w-full sm:w-48"
            buttonClassName="min-h-11 rounded-xl border-[#0F2854]/15 font-medium"
          />

          <FilterDropMenu
            label="Trạng thái tài khoản"
            value={statusFilter}
            options={[...USER_STATUS_FILTER_OPTIONS]}
            onChange={(value) => setParams({ status: value })}
            className="w-full sm:w-48"
            buttonClassName="min-h-11 rounded-xl border-[#0F2854]/15 font-medium"
          />

          {hasActiveFilters ? (
            <button
              type="button"
              onClick={() => {
                searchInput.reset('')
                resetParams(['q', 'role', 'status', 'page'])
              }}
              className="inline-flex min-h-11 items-center justify-center rounded-xl border border-[#0F2854]/15 bg-white px-3.5 text-xs font-medium text-[#5b6b84] transition-colors duration-150 hover:bg-[#F4FBFD] hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
            >
              Xóa bộ lọc
            </button>
          ) : null}
        </div>

        {loading ? (
          <LoadingState label="Đang tải danh sách tài khoản…" className="min-h-56" />
        ) : listProblem && users.length === 0 ? null : users.length === 0 ? (
          <div className="p-5">
            <EmptyState
              icon={UsersRound}
              title={
                hasActiveFilters
                  ? 'Không tìm thấy tài khoản phù hợp'
                  : 'Chưa có tài khoản nào trong hệ thống'
              }
              description={
                hasActiveFilters
                  ? 'Thử thay đổi từ khóa tìm kiếm, vai trò hoặc trạng thái hoạt động.'
                  : 'Tạo tài khoản đầu tiên để cấp quyền truy cập cho giảng viên, khảo thí hoặc sinh viên.'
              }
              action={
                hasActiveFilters ? (
                  <button
                    type="button"
                    onClick={() => {
                      searchInput.reset('')
                      resetParams(['q', 'role', 'status', 'page'])
                    }}
                    className="inline-flex min-h-11 items-center rounded-xl border border-[#0F2854]/15 bg-white px-4 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                  >
                    Xóa bộ lọc
                  </button>
                ) : (
                  <button
                    type="button"
                    onClick={handleOpenCreate}
                    className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-xs font-medium text-white transition-colors duration-150 hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                  >
                    <UserPlus className="size-4" strokeWidth={1.5} aria-hidden="true" />
                    Tạo tài khoản mới
                  </button>
                )
              }
            />
          </div>
        ) : (
          <>
            {/* Desktop Table */}
            <div className="hidden overflow-x-auto md:block">
              <table className="w-full min-w-[820px] text-left text-sm">
                <thead className="border-b border-[#0F2854]/10 bg-[#F8FBFD] text-xs font-medium text-[#5b6b84]">
                  <tr>
                    <th className="px-5 py-3">Người dùng</th>
                    <th className="px-4 py-3">Mã định danh</th>
                    <th className="px-4 py-3">Vai trò</th>
                    <th className="px-4 py-3">Trạng thái</th>
                    <th className="px-4 py-3">Ngày tạo</th>
                    <th className="px-5 py-3 text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#0F2854]/8">
                  {users.map((item) => {
                    const isSelf = item.id === currentUser?.id
                    return (
                      <tr key={item.id} className="hover:bg-[#F8FBFD]">
                        <td className="px-5 py-3.5">
                          <div className="min-w-0">
                            <p className="font-medium text-[#0F2854]">
                              {item.fullName}
                              {isSelf ? (
                                <span className="ml-2 rounded-md border border-[#0F2854]/10 bg-[#F4FBFD] px-1.5 py-0.5 text-xs font-normal text-[#1C4D8D]">
                                  Tài khoản hiện tại
                                </span>
                              ) : null}
                            </p>
                            <p className="mt-0.5 text-xs text-[#5b6b84]">{item.email}</p>
                          </div>
                        </td>
                        <td className="px-4 py-3.5 font-mono text-xs text-[#5b6b84]">
                          {item.studentCode ? (
                            <span className="text-[#0F2854]">{item.studentCode}</span>
                          ) : item.username ? (
                            <span>@{item.username}</span>
                          ) : (
                            '—'
                          )}
                        </td>
                        <td className="px-4 py-3.5">
                          <span className="inline-flex items-center rounded-md border border-[#0F2854]/10 bg-[#F4FBFD] px-2.5 py-1 text-xs font-medium text-[#1C4D8D]">
                            {getAdminRoleShortLabel(item.role)}
                          </span>
                        </td>
                        <td className="px-4 py-3.5">
                          <div className="flex flex-wrap items-center gap-1.5">
                            <span
                              className={cn(
                                'inline-flex items-center gap-1 rounded-md border px-2 py-0.5 text-xs font-medium',
                                item.isActive
                                  ? 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]'
                                  : 'border-[#f3c2c8] bg-[#fdecec] text-[#c2415b]',
                              )}
                            >
                              {item.isActive ? (
                                <UserCheck className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
                              ) : (
                                <UserX className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
                              )}
                              {item.isActive ? 'Hoạt động' : 'Đã khóa'}
                            </span>
                            {item.mustChangePassword ? (
                              <span className="rounded-md border border-[#f3ddb0] bg-[#fff8eb] px-2 py-0.5 text-xs text-[#9a6b12]">
                                Đổi MK lần đầu
                              </span>
                            ) : null}
                          </div>
                        </td>
                        <td className="px-4 py-3.5 text-xs text-[#5b6b84] tabular-nums">
                          {formatDate(item.createdAt)}
                        </td>
                        <td className="px-5 py-3.5">
                          <div className="flex flex-wrap justify-end gap-1.5">
                            <button
                              type="button"
                              onClick={() => handleOpenEditProfile(item)}
                              className="inline-flex min-h-9 items-center gap-1 rounded-lg border border-[#0F2854]/15 bg-white px-2.5 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                            >
                              <Pencil className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
                              Sửa
                            </button>
                            <button
                              type="button"
                              onClick={() => {
                                setEditRoleUser(item)
                                setNextRole(normalizeAdminRoleValue(item.role))
                                setRoleDialogProblem(null)
                              }}
                              className="inline-flex min-h-9 items-center rounded-lg border border-[#0F2854]/15 bg-white px-2.5 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                            >
                              Đổi vai trò
                            </button>
                            <button
                              type="button"
                              onClick={() => {
                                setStatusUser(item)
                                setStatusReason('')
                                setStatusDialogProblem(null)
                              }}
                              className={cn(
                                'inline-flex min-h-9 items-center gap-1 rounded-lg border px-2.5 text-xs font-medium transition-colors duration-150 focus-visible:outline-2 focus-visible:outline-offset-2 motion-reduce:transition-none',
                                item.isActive
                                  ? 'border-[#f3c2c8] bg-[#fdecec] text-[#c2415b] hover:bg-[#f9dce0] focus-visible:outline-[#c2415b]'
                                  : 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a] hover:bg-[#d5f2e6] focus-visible:outline-[#16836a]',
                              )}
                            >
                              {item.isActive ? (
                                <>
                                  <Lock className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
                                  Khóa
                                </>
                              ) : (
                                <>
                                  <Unlock className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
                                  Mở khóa
                                </>
                              )}
                            </button>
                            <button
                              type="button"
                              onClick={() => {
                                setResetPwdUser(item)
                                setTempPassword('')
                                setShowResetPassword(false)
                                setResetReason('')
                                setResetFieldErrors({})
                                setResetDialogProblem(null)
                              }}
                              className="inline-flex min-h-9 items-center gap-1 rounded-lg border border-[#0F2854]/15 bg-white px-2.5 text-xs font-medium text-[#0F2854] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                            >
                              <KeyRound className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
                              Cấp lại MK
                            </button>
                          </div>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>

            {/* Mobile Card List (zero horizontal overflow at 375px, >= 44px touch targets) */}
            <div className="space-y-3 p-4 md:hidden">
              {users.map((item) => {
                const isSelf = item.id === currentUser?.id
                return (
                  <article
                    key={item.id}
                    className="rounded-xl border border-[#0F2854]/10 bg-white p-4"
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0">
                        <p className="text-sm font-semibold text-[#0F2854]">{item.fullName}</p>
                        <p className="mt-0.5 text-xs text-[#5b6b84] break-all">{item.email}</p>
                      </div>
                      <span
                        className={cn(
                          'inline-flex shrink-0 items-center rounded-md border px-2 py-0.5 text-xs font-medium',
                          item.isActive
                            ? 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]'
                            : 'border-[#f3c2c8] bg-[#fdecec] text-[#c2415b]',
                        )}
                      >
                        {item.isActive ? 'Hoạt động' : 'Đã khóa'}
                      </span>
                    </div>

                    <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-[#5b6b84]">
                      <span className="rounded-md border border-[#0F2854]/10 bg-[#F4FBFD] px-2 py-0.5 font-medium text-[#1C4D8D]">
                        {getAdminRoleShortLabel(item.role)}
                      </span>
                      {item.mustChangePassword ? (
                        <span className="rounded-md border border-[#f3ddb0] bg-[#fff8eb] px-2 py-0.5 text-xs text-[#9a6b12]">
                          Đổi MK lần đầu
                        </span>
                      ) : null}
                      {item.studentCode ? (
                        <span className="font-mono text-[#0F2854]">{item.studentCode}</span>
                      ) : item.username ? (
                        <span className="font-mono">@{item.username}</span>
                      ) : null}
                      {isSelf ? (
                        <span className="text-[#1C4D8D]">(Tài khoản hiện tại)</span>
                      ) : null}
                    </div>

                    <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-4">
                      <button
                        type="button"
                        onClick={() => handleOpenEditProfile(item)}
                        className="inline-flex min-h-11 items-center justify-center gap-1 rounded-xl border border-[#0F2854]/15 bg-white px-3 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                      >
                        <Pencil className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
                        Sửa
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          setEditRoleUser(item)
                          setNextRole(normalizeAdminRoleValue(item.role))
                          setRoleDialogProblem(null)
                        }}
                        className="inline-flex min-h-11 items-center justify-center rounded-xl border border-[#0F2854]/15 bg-white px-3 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                      >
                        Đổi vai trò
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          setStatusUser(item)
                          setStatusReason('')
                          setStatusDialogProblem(null)
                        }}
                        className={cn(
                          'inline-flex min-h-11 items-center justify-center gap-1 rounded-xl border px-3 text-xs font-medium transition-colors duration-150 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none',
                          item.isActive
                            ? 'border-[#f3c2c8] bg-[#fdecec] text-[#c2415b] hover:bg-[#f9dce0]'
                            : 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a] hover:bg-[#d5f2e6]',
                        )}
                      >
                        {item.isActive ? 'Khóa tài khoản' : 'Mở khóa tài khoản'}
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          setResetPwdUser(item)
                          setTempPassword('')
                          setShowResetPassword(false)
                          setResetReason('')
                          setResetFieldErrors({})
                          setResetDialogProblem(null)
                        }}
                        className="inline-flex min-h-11 items-center justify-center gap-1 rounded-xl border border-[#0F2854]/15 bg-white px-3 text-xs font-medium text-[#0F2854] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                      >
                        <KeyRound className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
                        Cấp lại MK
                      </button>
                    </div>
                  </article>
                )
              })}
            </div>

            {totalPages > 1 ? (
              <ListPagination
                label="Phân trang danh sách người dùng"
                page={page}
                pageCount={Math.max(1, totalPages)}
                onPageChange={(nextPage) => setParams({ page: nextPage })}
              />
            ) : null}
          </>
        )}
      </section>

      {/* Create User FormDialog */}
      <FormDialog
        open={showCreateDialog}
        onClose={() => {
          if (!isCreatingUser) setShowCreateDialog(false)
        }}
        busy={isCreatingUser}
        variant="calm"
        icon={UserPlus}
        eyebrow="Quản trị tài khoản"
        title="Tạo tài khoản người dùng mới"
        footer={
          <SubmitDialogFooter
            form="admin-create-user-form"
            onCancel={() => setShowCreateDialog(false)}
            label="Tạo tài khoản"
            busyLabel="Đang tạo…"
            busy={isCreatingUser}
            icon={UserPlus}
            variant="calm"
          />
        }
      >
        <form id="admin-create-user-form" noValidate onSubmit={handleCreateUser} className="space-y-4">
          <div className="rounded-xl border border-[#0F2854]/10 bg-white p-3 text-xs text-[#5b6b84]">
            Tài khoản mới luôn đặt <strong className="font-medium text-[#0F2854]">mustChangePassword = true</strong> để bắt buộc đổi mật khẩu ngay lần đăng nhập đầu tiên.
          </div>

          {createDialogProblem ? <AdminStateBanner problem={createDialogProblem} /> : null}

          <div>
            <label htmlFor="create-user-email" className="block text-xs font-medium text-[#0F2854]">
              Email đăng nhập
            </label>
            <input
              id="create-user-email"
              type="email"
              value={createForm.email}
              onChange={(event) => {
                setCreateForm((prev) => ({ ...prev, email: event.target.value }))
                setCreateFieldErrors((prev) => ({ ...prev, email: undefined }))
              }}
              placeholder="name@fpt.edu.vn"
              aria-invalid={Boolean(createFieldErrors.email)}
              className={cn(
                ds.field,
                'mt-1.5 h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-normal',
                createFieldErrors.email && fieldInvalidClass,
              )}
            />
            <FieldError message={createFieldErrors.email} />
          </div>

          <div>
            <label
              htmlFor="create-user-fullname"
              className="block text-xs font-medium text-[#0F2854]"
            >
              Họ và tên
            </label>
            <input
              id="create-user-fullname"
              type="text"
              value={createForm.fullName}
              onChange={(event) => {
                setCreateForm((prev) => ({ ...prev, fullName: event.target.value }))
                setCreateFieldErrors((prev) => ({ ...prev, fullName: undefined }))
              }}
              placeholder="Nguyễn Văn A"
              aria-invalid={Boolean(createFieldErrors.fullName)}
              className={cn(
                ds.field,
                'mt-1.5 h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-normal',
                createFieldErrors.fullName && fieldInvalidClass,
              )}
            />
            <FieldError message={createFieldErrors.fullName} />
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label htmlFor="create-user-role" className="block text-xs font-medium text-[#0F2854]">
                Vai trò RBAC
              </label>
              <select
                id="create-user-role"
                value={createForm.role}
                onChange={(event) => {
                  const nextRoleValue = event.target.value
                  setCreateForm((prev) => ({
                    ...prev,
                    role: nextRoleValue,
                    studentCode: nextRoleValue === 'student' ? prev.studentCode : '',
                    username: nextRoleValue === 'student' ? '' : prev.username,
                  }))
                  setCreateFieldErrors((prev) => ({
                    ...prev,
                    role: undefined,
                    studentCode: undefined,
                    username: undefined,
                  }))
                }}
                aria-invalid={Boolean(createFieldErrors.role)}
                className={cn(
                  ds.field,
                  'mt-1.5 h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-normal',
                  createFieldErrors.role && fieldInvalidClass,
                )}
              >
                {ADMIN_ROLE_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
              <FieldError message={createFieldErrors.role} />
            </div>

            <div>
              <label
                htmlFor="create-user-password"
                className="block text-xs font-medium text-[#0F2854]"
              >
                Mật khẩu tạm thời (tối thiểu 8 ký tự)
              </label>
              <div className="relative mt-1.5">
                <input
                  id="create-user-password"
                  type={showCreatePassword ? 'text' : 'password'}
                  value={createForm.password}
                  onChange={(event) => {
                    setCreateForm((prev) => ({ ...prev, password: event.target.value }))
                    setCreateFieldErrors((prev) => ({ ...prev, password: undefined }))
                  }}
                  placeholder="Nhập mật khẩu tạm…"
                  aria-invalid={Boolean(createFieldErrors.password)}
                  className={cn(
                    ds.field,
                    'h-11 min-h-11 rounded-xl border-[#0F2854]/15 pr-11 font-normal',
                    createFieldErrors.password && fieldInvalidClass,
                  )}
                />
                <button
                  type="button"
                  aria-label={showCreatePassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                  aria-pressed={showCreatePassword}
                  onClick={() => setShowCreatePassword((visible) => !visible)}
                  className="absolute top-1/2 right-1.5 flex size-9 -translate-y-1/2 items-center justify-center rounded-lg text-[#5b6b84] transition hover:bg-[#E7F3FB] hover:text-[#1C4D8D] focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-[#1C4D8D]"
                >
                  {showCreatePassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                </button>
              </div>
              <FieldError message={createFieldErrors.password} />
            </div>
          </div>

          {createForm.role === 'student' ? (
            <div>
              <label
                htmlFor="create-user-student-code"
                className="block text-xs font-medium text-[#0F2854]"
              >
                Mã sinh viên (MSSV)
              </label>
              <input
                id="create-user-student-code"
                type="text"
                value={createForm.studentCode ?? ''}
                onChange={(event) => {
                  setCreateForm((prev) => ({ ...prev, studentCode: event.target.value }))
                  setCreateFieldErrors((prev) => ({ ...prev, studentCode: undefined }))
                }}
                placeholder="VD: SE180101"
                aria-invalid={Boolean(createFieldErrors.studentCode)}
                className={cn(
                  ds.field,
                  'mt-1.5 h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-mono font-normal uppercase placeholder:font-sans placeholder:normal-case',
                  createFieldErrors.studentCode && fieldInvalidClass,
                )}
              />
              <FieldError message={createFieldErrors.studentCode} />
            </div>
          ) : (
            <div>
              <label
                htmlFor="create-user-username"
                className="block text-xs font-medium text-[#0F2854]"
              >
                Tên đăng nhập (username — tùy chọn)
              </label>
              <input
                id="create-user-username"
                type="text"
                value={createForm.username ?? ''}
                onChange={(event) => {
                  setCreateForm((prev) => ({ ...prev, username: event.target.value }))
                  setCreateFieldErrors((prev) => ({ ...prev, username: undefined }))
                }}
                placeholder="vd: giangvien.se"
                aria-invalid={Boolean(createFieldErrors.username)}
                className={cn(
                  ds.field,
                  'mt-1.5 h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-mono font-normal lowercase placeholder:font-sans placeholder:normal-case',
                  createFieldErrors.username && fieldInvalidClass,
                )}
              />
              <p className="mt-1 text-xs text-[#5b6b84]">
                3–32 ký tự (a-z, 0-9, ., _, -); bỏ trống nếu chỉ dùng email/Google.
              </p>
              <FieldError message={createFieldErrors.username} />
            </div>
          )}
        </form>
      </FormDialog>

      {/* Edit Profile FormDialog (R3) */}
      <FormDialog
        open={Boolean(editProfileUser)}
        onClose={() => {
          if (!isUpdatingProfile) setEditProfileUser(null)
        }}
        busy={isUpdatingProfile}
        variant="calm"
        icon={Pencil}
        eyebrow="Hồ sơ tài khoản"
        title="Chỉnh sửa thông tin tài khoản"
        footer={
          <SubmitDialogFooter
            form="admin-edit-profile-form"
            onCancel={() => setEditProfileUser(null)}
            label="Lưu thay đổi"
            busyLabel="Đang lưu…"
            busy={isUpdatingProfile}
            icon={Pencil}
            variant="calm"
          />
        }
      >
        {editProfileUser ? (
          <form
            id="admin-edit-profile-form"
            noValidate
            onSubmit={handleUpdateProfile}
            className="space-y-4"
          >
            <div className="rounded-xl border border-[#0F2854]/10 bg-white p-3.5 text-xs text-[#5b6b84]">
              <div className="flex flex-wrap items-center gap-2">
                <span className="inline-flex items-center rounded-md border border-[#0F2854]/10 bg-[#F4FBFD] px-2 py-0.5 font-medium text-[#1C4D8D]">
                  {getAdminRoleShortLabel(editProfileUser.role)}
                </span>
                {normalizeAdminRoleValue(editProfileUser.role) === 'student' &&
                editProfileUser.username ? (
                  <span className="font-mono text-[#0F2854]">@{editProfileUser.username}</span>
                ) : null}
              </div>
              <p className="mt-2">
                Thay đổi Email hoặc Tên đăng nhập (username) sẽ tự động tăng{' '}
                <strong className="font-medium text-[#0F2854]">auth_version</strong> và thu hồi mọi
                Refresh Token đang hoạt động.
              </p>
            </div>

            {editProfileProblem ? <AdminStateBanner problem={editProfileProblem} /> : null}

            <div>
              <label htmlFor="edit-user-email" className="block text-xs font-medium text-[#0F2854]">
                Email đăng nhập
              </label>
              <input
                id="edit-user-email"
                type="email"
                value={editProfileForm.email}
                onChange={(event) => {
                  setEditProfileForm((prev) => ({ ...prev, email: event.target.value }))
                  setEditProfileFieldErrors((prev) => ({ ...prev, email: undefined }))
                }}
                placeholder="name@fpt.edu.vn"
                aria-invalid={Boolean(editProfileFieldErrors.email)}
                className={cn(
                  ds.field,
                  'mt-1.5 h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-normal',
                  editProfileFieldErrors.email && fieldInvalidClass,
                )}
              />
              <FieldError message={editProfileFieldErrors.email} />
            </div>

            <div>
              <label
                htmlFor="edit-user-fullname"
                className="block text-xs font-medium text-[#0F2854]"
              >
                Họ và tên
              </label>
              <input
                id="edit-user-fullname"
                type="text"
                value={editProfileForm.fullName}
                onChange={(event) => {
                  setEditProfileForm((prev) => ({ ...prev, fullName: event.target.value }))
                  setEditProfileFieldErrors((prev) => ({ ...prev, fullName: undefined }))
                }}
                placeholder="Nguyễn Văn A"
                aria-invalid={Boolean(editProfileFieldErrors.fullName)}
                className={cn(
                  ds.field,
                  'mt-1.5 h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-normal',
                  editProfileFieldErrors.fullName && fieldInvalidClass,
                )}
              />
              <FieldError message={editProfileFieldErrors.fullName} />
            </div>

            {normalizeAdminRoleValue(editProfileUser.role) === 'student' ? (
              <div>
                <label
                  htmlFor="edit-user-student-code"
                  className="block text-xs font-medium text-[#0F2854]"
                >
                  Mã sinh viên (MSSV)
                </label>
                <input
                  id="edit-user-student-code"
                  type="text"
                  value={editProfileForm.studentCode}
                  onChange={(event) => {
                    setEditProfileForm((prev) => ({ ...prev, studentCode: event.target.value }))
                    setEditProfileFieldErrors((prev) => ({ ...prev, studentCode: undefined }))
                  }}
                  placeholder="VD: SE180101"
                  aria-invalid={Boolean(editProfileFieldErrors.studentCode)}
                  className={cn(
                    ds.field,
                    'mt-1.5 h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-mono font-normal uppercase placeholder:font-sans placeholder:normal-case',
                    editProfileFieldErrors.studentCode && fieldInvalidClass,
                  )}
                />
                <FieldError message={editProfileFieldErrors.studentCode} />
              </div>
            ) : (
              <div>
                <label
                  htmlFor="edit-user-username"
                  className="block text-xs font-medium text-[#0F2854]"
                >
                  Tên đăng nhập (username — tùy chọn)
                </label>
                <input
                  id="edit-user-username"
                  type="text"
                  value={editProfileForm.username}
                  onChange={(event) => {
                    setEditProfileForm((prev) => ({ ...prev, username: event.target.value }))
                    setEditProfileFieldErrors((prev) => ({ ...prev, username: undefined }))
                  }}
                  placeholder="vd: giangvien.se"
                  aria-invalid={Boolean(editProfileFieldErrors.username)}
                  className={cn(
                    ds.field,
                    'mt-1.5 h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-mono font-normal lowercase placeholder:font-sans placeholder:normal-case',
                    editProfileFieldErrors.username && fieldInvalidClass,
                  )}
                />
                <p className="mt-1 text-xs text-[#5b6b84]">
                  3–32 ký tự (a-z, 0-9, ., _, -); xóa trống để gỡ tên đăng nhập.
                </p>
                <FieldError message={editProfileFieldErrors.username} />
              </div>
            )}
          </form>
        ) : null}
      </FormDialog>

      {/* Edit Role FormDialog */}
      <FormDialog
        open={Boolean(editRoleUser)}
        onClose={() => {
          if (!isUpdatingRole) setEditRoleUser(null)
        }}
        busy={isUpdatingRole}
        variant="calm"
        icon={ShieldCheck}
        eyebrow="Phân quyền RBAC"
        title="Thay đổi vai trò tài khoản"
        size="sm"
        footer={
          <SubmitDialogFooter
            form="admin-edit-role-form"
            onCancel={() => setEditRoleUser(null)}
            label="Lưu vai trò"
            busyLabel="Đang lưu…"
            busy={isUpdatingRole}
            icon={ShieldCheck}
            variant="calm"
          />
        }
      >
        {editRoleUser ? (
          <form id="admin-edit-role-form" noValidate onSubmit={handleUpdateRole} className="space-y-4">
            <div className="rounded-xl border border-[#0F2854]/10 bg-white p-3.5 text-xs text-[#5b6b84]">
              <p className="font-medium text-[#0F2854]">{editRoleUser.fullName}</p>
              <p className="mt-0.5">{editRoleUser.email}</p>
              <p className="mt-2">
                Đổi vai trò sẽ tự động tăng <strong className="font-medium text-[#0F2854]">auth_version</strong> và thu hồi mọi Refresh Token đang hoạt động.
              </p>
            </div>

            {editRoleUser.id === currentUser?.id ? (
              <div
                role="status"
                className="rounded-xl border border-[#f3ddb0] bg-[#fff8eb] p-3 text-xs text-[#9a6b12]"
              >
                Lưu ý: Hệ thống không cho phép tự hạ quyền Admin của chính tài khoản đang đăng nhập (HTTP 409).
              </div>
            ) : null}

            {roleDialogProblem ? <AdminStateBanner problem={roleDialogProblem} /> : null}

            <div>
              <label htmlFor="edit-user-role" className="block text-xs font-medium text-[#0F2854]">
                Vai trò mới
              </label>
              <select
                id="edit-user-role"
                value={nextRole}
                onChange={(event) => setNextRole(event.target.value)}
                className={cn(
                  ds.field,
                  'mt-1.5 h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-normal',
                )}
              >
                {ADMIN_ROLE_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </div>
          </form>
        ) : null}
      </FormDialog>

      {/* Lock / Unlock ConfirmDialog */}
      {statusUser ? (
        <ConfirmDialog
          tone={statusUser.isActive ? 'danger' : 'info'}
          variant="calm"
          icon={statusUser.isActive ? Lock : Unlock}
          eyebrow="Kiểm soát truy cập & auth_version"
          title={
            statusUser.isActive
              ? `Khóa tài khoản ${statusUser.email}?`
              : `Mở khóa tài khoản ${statusUser.email}?`
          }
          message={
            statusUser.isActive
              ? 'Khóa tài khoản sẽ tăng auth_version và thu hồi ngay các phiên đăng nhập hiện tại của người dùng.'
              : 'Tài khoản sẽ được phép đăng nhập trở lại với vai trò hiện hành.'
          }
          confirmLabel={statusUser.isActive ? 'Xác nhận khóa tài khoản' : 'Xác nhận mở khóa'}
          cancelLabel="Hủy"
          pending={isUpdatingStatus}
          onCancel={() => {
            if (!isUpdatingStatus) {
              setStatusUser(null)
              setStatusDialogProblem(null)
            }
          }}
          onConfirm={() => void handleConfirmToggleStatus()}
        >
          <div className="space-y-3">
            {statusUser.id === currentUser?.id && statusUser.isActive ? (
              <p className="rounded-xl border border-[#f3ddb0] bg-[#fff8eb] p-2.5 text-xs text-[#9a6b12]">
                Lưu ý: Tài khoản Admin không thể tự khóa chính mình (HTTP 409).
              </p>
            ) : null}
            {statusDialogProblem ? <AdminStateBanner problem={statusDialogProblem} /> : null}
            <div>
              <label htmlFor="status-reason" className="block text-xs font-medium text-[#0F2854]">
                Lý do thay đổi trạng thái (tùy chọn)
              </label>
              <textarea
                id="status-reason"
                value={statusReason}
                onChange={(event) => setStatusReason(event.target.value)}
                placeholder="Ghi chú lưu Audit Log…"
                className="mt-1.5 min-h-20 w-full rounded-xl border border-[#0F2854]/15 bg-white p-2.5 text-sm text-[#0F2854] outline-none focus:border-[#1C4D8D] focus:ring-2 focus:ring-[#1C4D8D]/15"
              />
            </div>
          </div>
        </ConfirmDialog>
      ) : null}

      {/* Reset Temporary Password FormDialog */}
      <FormDialog
        open={Boolean(resetPwdUser)}
        onClose={() => {
          if (!isResettingPwd) setResetPwdUser(null)
        }}
        busy={isResettingPwd}
        variant="calm"
        icon={KeyRound}
        eyebrow="Bảo mật tài khoản"
        title="Cấp lại mật khẩu tạm thời"
        size="sm"
        footer={
          <SubmitDialogFooter
            form="admin-reset-password-form"
            onCancel={() => setResetPwdUser(null)}
            label="Xác nhận cấp lại"
            busyLabel="Đang cấp lại…"
            busy={isResettingPwd}
            icon={KeyRound}
            variant="calm"
          />
        }
      >
        {resetPwdUser ? (
          <form id="admin-reset-password-form" noValidate onSubmit={handleResetPassword} className="space-y-4">
            <div className="rounded-xl border border-[#0F2854]/10 bg-white p-3.5 text-xs text-[#5b6b84]">
              <p className="font-medium text-[#0F2854]">{resetPwdUser.fullName}</p>
              <p className="mt-0.5">{resetPwdUser.email}</p>
              <p className="mt-2">
                Hệ thống sẽ đặt <strong className="font-medium text-[#0F2854]">mustChangePassword = true</strong> và thu hồi phiên đăng nhập cũ.
              </p>
            </div>

            {resetDialogProblem ? <AdminStateBanner problem={resetDialogProblem} /> : null}

            <div>
              <label htmlFor="reset-temp-pwd" className="block text-xs font-medium text-[#0F2854]">
                Mật khẩu tạm thời mới (8–128 ký tự)
              </label>
              <div className="relative mt-1.5">
                <input
                  id="reset-temp-pwd"
                  type={showResetPassword ? 'text' : 'password'}
                  value={tempPassword}
                  onChange={(event) => {
                    setTempPassword(event.target.value)
                    setResetFieldErrors((prev) => ({ ...prev, temporaryPassword: undefined }))
                  }}
                  placeholder="Nhập mật khẩu tạm thời…"
                  aria-invalid={Boolean(resetFieldErrors.temporaryPassword)}
                  className={cn(
                    ds.field,
                    'h-11 min-h-11 rounded-xl border-[#0F2854]/15 pr-11 font-normal',
                    resetFieldErrors.temporaryPassword && fieldInvalidClass,
                  )}
                />
                <button
                  type="button"
                  aria-label={showResetPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                  aria-pressed={showResetPassword}
                  onClick={() => setShowResetPassword((visible) => !visible)}
                  className="absolute top-1/2 right-1.5 flex size-9 -translate-y-1/2 items-center justify-center rounded-lg text-[#5b6b84] transition hover:bg-[#E7F3FB] hover:text-[#1C4D8D] focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-[#1C4D8D]"
                >
                  {showResetPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                </button>
              </div>
              <FieldError message={resetFieldErrors.temporaryPassword} />
            </div>

            <div>
              <label htmlFor="reset-pwd-reason" className="block text-xs font-medium text-[#0F2854]">
                Lý do cấp lại (tùy chọn, tối đa 500 ký tự)
              </label>
              <textarea
                id="reset-pwd-reason"
                value={resetReason}
                onChange={(event) => {
                  setResetReason(event.target.value)
                  setResetFieldErrors((prev) => ({ ...prev, reason: undefined }))
                }}
                placeholder="VD: Sinh viên quên mật khẩu đăng nhập trường cấp…"
                aria-invalid={Boolean(resetFieldErrors.reason)}
                className={cn(
                  'mt-1.5 min-h-20 w-full rounded-xl border border-[#0F2854]/15 bg-white p-2.5 text-sm text-[#0F2854] outline-none focus:border-[#1C4D8D] focus:ring-2 focus:ring-[#1C4D8D]/15',
                  resetFieldErrors.reason && fieldInvalidClass,
                )}
              />
              <FieldError message={resetFieldErrors.reason} />
            </div>
          </form>
        ) : null}
      </FormDialog>
    </div>
  )
}
