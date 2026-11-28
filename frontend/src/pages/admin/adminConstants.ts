export interface AdminRoleOption {
  value: string
  label: string
  shortLabel: string
}

export const ADMIN_ROLE_OPTIONS: readonly AdminRoleOption[] = [
  { value: 'student', label: 'Sinh viên (student)', shortLabel: 'Sinh viên' },
  { value: 'lecturer', label: 'Giảng viên (lecturer)', shortLabel: 'Giảng viên' },
  { value: 'department_head', label: 'Trưởng bộ môn (department_head)', shortLabel: 'Trưởng BM' },
  {
    value: 'examination_officer',
    label: 'Cán bộ khảo thí (examination_officer)',
    shortLabel: 'Khảo thí',
  },
  { value: 'proctor', label: 'Giám thị phòng lab (proctor)', shortLabel: 'Giám thị' },
  { value: 'admin', label: 'Quản trị viên (admin)', shortLabel: 'Quản trị viên' },
]

export const USER_STATUS_FILTER_OPTIONS = [
  { value: 'all', label: 'Tất cả trạng thái' },
  { value: 'active', label: 'Đang hoạt động' },
  { value: 'locked', label: 'Đã khóa' },
] as const

export const NOTIFICATION_TYPE_OPTIONS = [
  { value: 'SYSTEM_ANNOUNCEMENT', label: 'Thông báo hệ thống' },
  { value: 'ACADEMIC_NOTICE', label: 'Thông báo học vụ' },
  { value: 'MAINTENANCE_ALERT', label: 'Cảnh báo bảo trì' },
  { value: 'SECURITY_ALERT', label: 'Cảnh báo bảo mật' },
] as const

export const NOTIFICATION_READ_FILTER_OPTIONS = [
  { value: 'all', label: 'Tất cả trạng thái đọc' },
  { value: 'unread', label: 'Chưa đọc' },
  { value: 'read', label: 'Đã đọc' },
] as const

export const NOTIFICATION_SCOPE_OPTIONS = [
  { value: 'ALL_ACTIVE', label: 'Toàn bộ tài khoản đang hoạt động' },
  { value: 'ROLE', label: 'Theo vai trò RBAC' },
  { value: 'USER', label: 'Một tài khoản cụ thể' },
] as const

export const AUDIT_ENTITY_PRESETS = [
  { value: '', label: 'Tất cả thực thể' },
  { value: 'users', label: 'users — Tài khoản' },
  { value: 'semesters', label: 'semesters — Học kỳ' },
  { value: 'course_offerings', label: 'course_offerings — Môn mở theo kỳ' },
  { value: 'academic_programs', label: 'academic_programs — Chương trình ĐT' },
  { value: 'courses', label: 'courses — Môn học' },
  { value: 'course_clos', label: 'course_clos — Chuẩn đầu ra CLO' },
  { value: 'system_configs', label: 'system_configs — Cấu hình MF-01' },
  { value: 'notifications', label: 'notifications — Thông báo 3NF' },
] as const

export const AUDIT_ACTION_PRESETS = [
  { value: '', label: 'Tất cả hành động' },
  { value: 'USER_CREATED', label: 'USER_CREATED — Tạo tài khoản' },
  { value: 'USERS_BULK_IMPORTED', label: 'USERS_BULK_IMPORTED — Import tài khoản hàng loạt' },
  { value: 'USER_UPDATED', label: 'USER_UPDATED — Cập nhật hồ sơ tài khoản' },
  { value: 'USER_ROLE_CHANGED', label: 'USER_ROLE_CHANGED — Đổi vai trò' },
  { value: 'USER_STATUS_CHANGED', label: 'USER_STATUS_CHANGED — Khóa/Mở tài khoản' },
  { value: 'USER_PASSWORD_RESET', label: 'USER_PASSWORD_RESET — Cấp lại mật khẩu' },
  { value: 'SEMESTER_CREATED', label: 'SEMESTER_CREATED — Tạo học kỳ' },
  { value: 'SEMESTER_UPDATED', label: 'SEMESTER_UPDATED — Cập nhật học kỳ' },
  {
    value: 'SEMESTER_COURSE_OFFERINGS_SYNCED',
    label: 'SEMESTER_COURSE_OFFERINGS_SYNCED — Đồng bộ môn mở theo kỳ',
  },
  { value: 'ACADEMIC_PROGRAM_CREATED', label: 'ACADEMIC_PROGRAM_CREATED — Tạo chương trình ĐT' },
  {
    value: 'ACADEMIC_PROGRAM_UPDATED',
    label: 'ACADEMIC_PROGRAM_UPDATED — Cập nhật chương trình ĐT',
  },
  {
    value: 'ACADEMIC_PROGRAM_DEPARTMENT_HEAD_ASSIGNED',
    label: 'ACADEMIC_PROGRAM_DEPARTMENT_HEAD_ASSIGNED — Gán Trưởng BM ngành',
  },
  { value: 'COURSE_CREATED', label: 'COURSE_CREATED — Tạo môn học' },
  { value: 'COURSE_UPDATED', label: 'COURSE_UPDATED — Cập nhật môn học' },
  {
    value: 'COURSE_DEPARTMENT_HEAD_ASSIGNED',
    label: 'COURSE_DEPARTMENT_HEAD_ASSIGNED — Gán Trưởng BM môn học',
  },
  { value: 'SYSTEM_CONFIG_UPDATED', label: 'SYSTEM_CONFIG_UPDATED — Sửa cấu hình MF-01' },
  { value: 'NOTIFICATION_DISPATCHED', label: 'NOTIFICATION_DISPATCHED — Phát thông báo' },
  { value: 'NOTIFICATION_DELETED', label: 'NOTIFICATION_DELETED — Thu hồi thông báo' },
] as const

const ADMIN_ROLE_ALIASES: Record<string, string> = {
  student: 'student',
  lecturer: 'lecturer',
  instructor: 'lecturer',
  departmenthead: 'department_head',
  department_head: 'department_head',
  examinationofficer: 'examination_officer',
  examination_officer: 'examination_officer',
  proctor: 'proctor',
  admin: 'admin',
}

export function normalizeAdminRoleValue(role: string): string {
  const trimmed = role.trim()
  if (!trimmed) return ''
  const compact = trimmed.toLowerCase()
  return ADMIN_ROLE_ALIASES[compact] ?? compact
}

export function getAdminRoleShortLabel(role: string): string {
  const normalized = normalizeAdminRoleValue(role)
  const matched = ADMIN_ROLE_OPTIONS.find((item) => item.value === normalized)
  return matched ? matched.shortLabel : role
}

export function getNotificationTypeLabel(type: string): string {
  const normalized = type.trim().toUpperCase()
  const matched = NOTIFICATION_TYPE_OPTIONS.find((item) => item.value === normalized)
  return matched ? matched.label : type
}
