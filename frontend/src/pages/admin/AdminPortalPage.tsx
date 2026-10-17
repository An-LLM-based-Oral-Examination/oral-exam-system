import { useMemo, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import {
  Activity,
  AlertTriangle,
  Building2,
  CalendarRange,
  CheckCircle2,
  Edit3,
  FileCode2,
  Lock,
  Plus,
  RefreshCw,
  Save,
  Search,
  ServerCog,
  Settings,
  ShieldAlert,
  ShieldCheck,
  Sliders,
  Unlock,
  Users,
} from 'lucide-react'
import apiClient from '@/services/api.client'
import { OverlayDrawer, OverlayModal } from '@/components/ui/OverlayDrawer'
import { cn } from '@/lib/utils'

type AdminTab = 'system-configs' | 'users' | 'semesters' | 'audit-logs'
type BackendRoleCode = 'student' | 'lecturer' | 'department_head' | 'proctor' | 'admin'

interface SystemConfigItem {
  id: string
  key: string
  value: string
  unit: string
  min: number
  max: number
  description: string
  updatedAt: string
}

interface AdminCourseQuotaItem {
  id: string
  code: string
  name: string
  credits: number
  maxMockExamsPerDay: number
  updatedAt: string
}

interface ManagedUserAccount {
  id: string
  fullName: string
  email: string
  username: string
  studentCode: string | null
  role: BackendRoleCode
  isActive: boolean
  updatedAt: string
}

interface SemesterRecord {
  id: string
  code: string
  name: string
  startDate: string
  endDate: string
  isActive: boolean
}

interface AuditLogRecord {
  id: string
  timestamp: string
  actorEmail: string
  actorRole: BackendRoleCode
  action: string
  entityName: string
  httpStatus: number
  detail: string
  payloadJson: string
  stackTrace: string
}

const DEFAULT_SYSTEM_PARAMETERS: SystemConfigItem[] = [
  {
    id: '00000000-0000-0000-0000-000000000030',
    key: 'TranscriptBufferSeconds',
    value: '60',
    unit: 'giây (10–300s)',
    min: 10,
    max: 300,
    description:
      'Thời gian đệm hiệu đính văn bản mặc định (10–300s, mặc định 60s) cho sinh viên nghe lại bản đọc và rà soát thuật ngữ chuyên ngành.',
    updatedAt: '16/10/2026 08:00',
  },
  {
    id: '00000000-0000-0000-0000-000000000031',
    key: 'MaxPracticeFollowUpQuestions',
    value: '2',
    unit: 'câu (1–5 câu)',
    min: 1,
    max: 5,
    description:
      'Số lượng câu hỏi phụ truy vấn tối đa trong chế độ Luyện tập khi điểm câu gốc nằm trong khoảng 4.0 ≤ Điểm ≤ 8.0.',
    updatedAt: '16/10/2026 08:00',
  },
  {
    id: '00000000-0000-0000-0000-000000000028',
    key: 'MinMixedPracticeQuestions',
    value: '3',
    unit: 'câu',
    min: 3,
    max: 10,
    description:
      'Số lượng câu hỏi luyện tập tối thiểu cho chế độ Luyện tập theo lộ trình (Dễ → Trung bình → Khó).',
    updatedAt: '16/10/2026 08:00',
  },
  {
    id: '00000000-0000-0000-0000-000000000029',
    key: 'MaxMixedPracticeQuestions',
    value: '10',
    unit: 'câu',
    min: 3,
    max: 10,
    description:
      'Số lượng câu hỏi luyện tập tối đa cho chế độ Luyện tập theo lộ trình (3–10 câu, không sinh câu hỏi phụ).',
    updatedAt: '16/10/2026 08:00',
  },
  {
    id: '00000000-0000-0000-0000-000000000027',
    key: 'MaxPracticeQuestionsPerSession',
    value: '10',
    unit: 'câu / phiên',
    min: 1,
    max: 20,
    description:
      'Giới hạn tổng số câu hỏi tối đa trong một phiên luyện tập tương tác tự do.',
    updatedAt: '16/10/2026 08:00',
  },
  {
    id: '00000000-0000-0000-0000-000000000032',
    key: 'SessionInactivityTimeoutMinutes',
    value: '5',
    unit: 'phút (Tự động đóng phiên)',
    min: 5,
    max: 60,
    description:
      'Thời gian tự động đóng phiên luyện tập khi không có tương tác (5–60 phút, mặc định 5 phút), bảo toàn điểm các câu đã hoàn thành.',
    updatedAt: '16/10/2026 08:00',
  },
  {
    id: '00000000-0000-0000-0000-000000000033',
    key: 'DefaultMockExamDailyQuota',
    value: '3',
    unit: 'lượt / ngày (1–20 lượt)',
    min: 1,
    max: 20,
    description:
      'Hạn ngạch số lượt thi thử mặc định mỗi ngày cho sinh viên theo từng học phần do Quản trị viên thiết lập.',
    updatedAt: '16/10/2026 08:00',
  },
]

const INITIAL_USERS: ManagedUserAccount[] = [
  {
    id: '00000000-0000-0000-0000-000000000001',
    fullName: 'Nguyen Quang Thanh',
    email: 'admin@fpt.edu.vn',
    username: 'admin',
    studentCode: null,
    role: 'admin',
    isActive: true,
    updatedAt: '2026-09-01',
  },
  {
    id: '00000000-0000-0000-0000-000000000002',
    fullName: 'Tran Van Truong',
    email: 'headse@fe.edu.vn',
    username: 'headse',
    studentCode: null,
    role: 'department_head',
    isActive: true,
    updatedAt: '2026-09-01',
  },
  {
    id: '00000000-0000-0000-0000-000000000003',
    fullName: 'Nguyen Trong Tot',
    email: 'totnt@fe.edu.vn',
    username: 'totnt',
    studentCode: null,
    role: 'lecturer',
    isActive: true,
    updatedAt: '2026-09-01',
  },
  {
    id: '00000000-0000-0000-0000-000000000004',
    fullName: 'Nguyen Dang Hai',
    email: 'haind@fe.edu.vn',
    username: 'haind',
    studentCode: null,
    role: 'proctor',
    isActive: true,
    updatedAt: '2026-09-01',
  },
  {
    id: '00000000-0000-0000-0000-000000000005',
    fullName: 'Le Vu Hoang',
    email: 'hoanglvse170001@fpt.edu.vn',
    username: 'hoalvse170001',
    studentCode: 'SE170001',
    role: 'student',
    isActive: true,
    updatedAt: '2026-09-01',
  },
  {
    id: '00000000-0000-0000-0000-000000000006',
    fullName: 'Tran Thi Mai',
    email: 'maittse170002@fpt.edu.vn',
    username: 'maittse170002',
    studentCode: 'SE170002',
    role: 'student',
    isActive: true,
    updatedAt: '2026-09-01',
  },
]

const INITIAL_SEMESTERS: SemesterRecord[] = [
  {
    id: '00000000-0000-0000-0000-000000000007',
    code: 'FA26',
    name: 'Fall 2026',
    startDate: '2026-09-01',
    endDate: '2026-12-31',
    isActive: true,
  },
  {
    id: '00000000-0000-0000-0000-000000000071',
    code: 'SU26',
    name: 'Summer 2026',
    startDate: '2026-05-04',
    endDate: '2026-08-25',
    isActive: false,
  },
  {
    id: '00000000-0000-0000-0000-000000000072',
    code: 'SP26',
    name: 'Spring 2026',
    startDate: '2026-01-05',
    endDate: '2026-04-28',
    isActive: false,
  },
]

const INITIAL_COURSE_QUOTAS: AdminCourseQuotaItem[] = [
  {
    id: '00000000-0000-0000-0000-000000000008',
    code: 'PRN231',
    name: 'Building Cross-Platform Back-End Application With .NET',
    credits: 3,
    maxMockExamsPerDay: 3,
    updatedAt: '16/10/2026 08:00',
  },
  {
    id: '00000000-0000-0000-0000-000000000009',
    code: 'SWD392',
    name: 'Software Architecture and Design',
    credits: 3,
    maxMockExamsPerDay: 3,
    updatedAt: '16/10/2026 08:00',
  },
  {
    id: '00000000-0000-0000-0000-00000000000a',
    code: 'SWR302',
    name: 'Software Requirements',
    credits: 3,
    maxMockExamsPerDay: 3,
    updatedAt: '16/10/2026 08:00',
  },
]

const INITIAL_AUDIT_LOGS: AuditLogRecord[] = [
  {
    id: 'aud-101',
    timestamp: '16/10/2026 09:45:12',
    actorEmail: 'totnt@fe.edu.vn',
    actorRole: 'lecturer',
    action: 'PUBLISH_SHIFT_GRADES',
    entityName: 'Ca thi Kíp 1 · Lab 301 (0000...0023)',
    httpStatus: 200,
    detail:
      'Công bố điểm ca thi Kíp 1 Lab 301 (PRN231) 1 lần duy nhất. Đã kích hoạt cơ chế Bảo vệ Khóa bảng điểm.',
    payloadJson: JSON.stringify(
      {
        event: 'PUBLISH_SHIFT_GRADES',
        shiftId: '00000000-0000-0000-0000-000000000023',
        courseCode: 'PRN231',
        publishedTicketsCount: 32,
        isLocked: true,
      },
      null,
      2,
    ),
    stackTrace: 'AuditTrailService -> Committed Transaction 200 OK',
  },
  {
    id: 'aud-102',
    timestamp: '16/10/2026 09:48:05',
    actorEmail: 'totnt@fe.edu.vn',
    actorRole: 'lecturer',
    action: 'GRADE_LOCK_BLOCKED_UPDATE',
    entityName: 'Vé dự thi (0000...0024)',
    httpStatus: 403,
    detail:
      'Bảo vệ Khóa bảng điểm chặn thao tác chỉnh sửa trực tiếp lên vé dự thi đã niêm phong điểm (HTTP 403 Forbidden).',
    payloadJson: JSON.stringify(
      {
        event: 'GRADE_LOCK_BLOCKED_UPDATE',
        ticketId: '00000000-0000-0000-0000-000000000024',
        attemptedScoreChange: 8.5,
        isLocked: true,
        hasActiveAppealAssignment: false,
      },
      null,
      2,
    ),
    stackTrace:
      'SecurityException: Vé dự thi 00000000-0000-0000-0000-000000000024 đã được khóa bảng điểm sau khi công bố (HTTP 403 Forbidden).',
  },
  {
    id: 'aud-103',
    timestamp: '16/10/2026 09:52:30',
    actorEmail: 'hoanglvse170001@fpt.edu.vn',
    actorRole: 'student',
    action: 'SUBMIT_APPEAL_REQUEST',
    entityName: 'Đơn phúc khảo (0000...0026)',
    httpStatus: 201,
    detail:
      'Sinh viên SE170001 nộp đơn phúc khảo trực tuyến trên Cổng Sinh viên cho học phần PRN231.',
    payloadJson: JSON.stringify(
      {
        event: 'SUBMIT_APPEAL_REQUEST',
        appealId: '00000000-0000-0000-0000-000000000026',
        studentCode: 'SE170001',
        ticketId: '00000000-0000-0000-0000-000000000024',
      },
      null,
      2,
    ),
    stackTrace: 'AppealService.CreateAppealAsync -> Created 201',
  },
  {
    id: 'aud-104',
    timestamp: '16/10/2026 10:01:18',
    actorEmail: 'admin@fpt.edu.vn',
    actorRole: 'admin',
    action: 'UPDATE_COURSE_QUOTA',
    entityName: 'Học phần (PRN231)',
    httpStatus: 200,
    detail:
      'Quản trị viên cập nhật hạn ngạch thi thử 3 lượt/ngày cho học phần PRN231.',
    payloadJson: JSON.stringify(
      {
        event: 'UPDATE_COURSE_QUOTA',
        courseCode: 'PRN231',
        maxMockExamsPerDay: 3,
        updatedBy: 'admin',
      },
      null,
      2,
    ),
    stackTrace: 'AdminCourseQuotaService.UpdateQuotaAsync -> 200 OK',
  },
]

const ROLE_LABELS: Record<BackendRoleCode, { label: string; badgeClass: string }> = {
  student: { label: 'Sinh viên (Student)', badgeClass: 'bg-slate-100 text-slate-700' },
  lecturer: { label: 'Giảng viên (Lecturer)', badgeClass: 'bg-blue-50 text-[#2563EB]' },
  department_head: {
    label: 'Trưởng Bộ môn (Dept. Head)',
    badgeClass: 'bg-[#FFF7ED] text-[#C2410C] border border-[#FED7AA]',
  },
  proctor: { label: 'Giám thị Coi thi (Proctor)', badgeClass: 'bg-emerald-50 text-[#16A34A]' },
  admin: {
    label: 'Quản trị viên (Admin)',
    badgeClass: 'bg-[#F26F21] text-white',
  },
}

export function AdminPortalPage() {
  const [activeTab, setActiveTab] = useState<AdminTab>('system-configs')
  const [notice, setNotice] = useState<string>('')

  // --- TAB 1: THAM SỐ HỆ THỐNG ---
  const [configs, setConfigs] = useState<SystemConfigItem[]>(DEFAULT_SYSTEM_PARAMETERS)
  const [editingConfigKey, setEditingConfigKey] = useState<string | null>(null)

  // --- TAB 2: QUẢN LÝ TÀI KHOẢN ---
  const [users, setUsers] = useState<ManagedUserAccount[]>(INITIAL_USERS)
  const [userSearch, setUserSearch] = useState<string>('')
  const [roleFilter, setRoleFilter] = useState<BackendRoleCode | 'ALL'>('ALL')
  const [editingUserId, setEditingUserId] = useState<string | null>(null)

  // --- TAB 3: QUẢN LÝ HỌC KỲ ---
  const [semesters, setSemesters] = useState<SemesterRecord[]>(INITIAL_SEMESTERS)
  const [isSemesterModalOpen, setIsSemesterModalOpen] = useState(false)
  const [editingSemesterId, setEditingSemesterId] = useState<string | null>(null)
  const [semesterForm, setSemesterForm] = useState({
    code: 'SP27',
    name: 'Spring 2027',
    startDate: '2027-01-04',
    endDate: '2027-04-30',
    isActive: false,
  })

  // --- TAB 1: HẠN NGẠCH THI THỬ HỌC PHẦN (ADMIN) ---
  const [courseQuotas, setCourseQuotas] = useState<AdminCourseQuotaItem[]>(INITIAL_COURSE_QUOTAS)
  const [savingCourseQuotaId, setSavingCourseQuotaId] = useState<string | null>(null)

  // --- TAB 4: NHẬT KÝ KIỂM TOÁN HỆ THỐNG ---
  const [auditLogs] = useState<AuditLogRecord[]>(INITIAL_AUDIT_LOGS)
  const [selectedLogDrawer, setSelectedLogDrawer] = useState<{
    title: string
    subtitle: string
    badge: string
    payloadJson: string
    stackTrace: string
  } | null>(null)

  const filteredUsers = useMemo(() => {
    const q = userSearch.trim().toLowerCase()
    return users.filter((u) => {
      if (roleFilter !== 'ALL' && u.role !== roleFilter) return false
      if (!q) return true
      return (
        u.fullName.toLowerCase().includes(q) ||
        u.email.toLowerCase().includes(q) ||
        (u.studentCode && u.studentCode.toLowerCase().includes(q))
      )
    })
  }, [users, userSearch, roleFilter])

  const editingConfig = useMemo(
    () => configs.find((c) => c.key === editingConfigKey) ?? null,
    [configs, editingConfigKey],
  )

  const editingUser = useMemo(
    () => users.find((u) => u.id === editingUserId) ?? null,
    [users, editingUserId],
  )

  const handleConfigValueChange = (key: string, newValue: string) => {
    setConfigs((prev) =>
      prev.map((cfg) => (cfg.key === key ? { ...cfg, value: newValue } : cfg)),
    )
  }

  const handleSaveSystemConfigs = async () => {
    try {
      await apiClient.put('/admin/system-configs', {
        configs: configs.map((c) => ({ key: c.key, value: c.value })),
      })
    } catch {
      // Đồng bộ trên state trong trường hợp kết nối máy chủ đang chạy local
    }
    const nowStr = new Intl.DateTimeFormat('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(new Date())
    setConfigs((prev) => prev.map((c) => ({ ...c, updatedAt: nowStr })))
    setEditingConfigKey(null)
    setNotice(
      'Đã cập nhật 6 tham số vận hành hệ thống cho phân hệ Luyện tập tương tác.',
    )
  }

  const handleToggleUserStatus = (userId: string) => {
    setUsers((prev) =>
      prev.map((u) => {
        if (u.id !== userId) return u
        const nextActive = !u.isActive
        setNotice(
          `Đã ${nextActive ? 'mở khóa' : 'khóa tạm thời'} tài khoản ${u.email} (${u.fullName}).`,
        )
        return { ...u, isActive: nextActive }
      }),
    )
  }

  const handleChangeUserRole = (userId: string, nextRole: BackendRoleCode) => {
    setUsers((prev) =>
      prev.map((u) => {
        if (u.id !== userId) return u
        setNotice(
          `Đã phân quyền lại tài khoản ${u.email} sang vai trò [${ROLE_LABELS[nextRole].label}].`,
        )
        return { ...u, role: nextRole }
      }),
    )
  }

  const handleOpenCreateSemesterModal = () => {
    setEditingSemesterId(null)
    setSemesterForm({
      code: 'SP27',
      name: 'Spring 2027',
      startDate: '2027-01-04',
      endDate: '2027-04-30',
      isActive: false,
    })
    setIsSemesterModalOpen(true)
  }

  const handleStartEditSemester = (sem: SemesterRecord) => {
    setEditingSemesterId(sem.id)
    setSemesterForm({
      code: sem.code,
      name: sem.name,
      startDate: sem.startDate,
      endDate: sem.endDate,
      isActive: sem.isActive,
    })
    setIsSemesterModalOpen(true)
  }

  const handleSaveSemester = (event: FormEvent) => {
    event.preventDefault()
    if (editingSemesterId) {
      setSemesters((prev) =>
        prev.map((s) =>
          s.id === editingSemesterId
            ? {
                ...s,
                code: semesterForm.code.toUpperCase(),
                name: semesterForm.name,
                startDate: semesterForm.startDate,
                endDate: semesterForm.endDate,
                isActive: semesterForm.isActive,
              }
            : semesterForm.isActive
              ? { ...s, isActive: false }
              : s,
        ),
      )
      setNotice(
        `Đã cập nhật thông tin học kỳ ${semesterForm.code.toUpperCase()} (${semesterForm.name}).`,
      )
      setEditingSemesterId(null)
    } else {
      const created: SemesterRecord = {
        id: `00000000-0000-0000-0000-00000000007${semesters.length + 3}`,
        code: semesterForm.code.toUpperCase(),
        name: semesterForm.name,
        startDate: semesterForm.startDate,
        endDate: semesterForm.endDate,
        isActive: semesterForm.isActive,
      }
      setSemesters((prev) => [
        created,
        ...prev.map((s) => (semesterForm.isActive ? { ...s, isActive: false } : s)),
      ])
      setNotice(`Đã khởi tạo học kỳ mới ${created.code} — ${created.name}.`)
    }
    setIsSemesterModalOpen(false)
  }

  const handleUpdateCourseQuota = (courseId: string, newQuota: number) => {
    const clamped = Math.min(20, Math.max(1, newQuota))
    setCourseQuotas((prev) =>
      prev.map((c) =>
        c.id === courseId ? { ...c, maxMockExamsPerDay: clamped, updatedAt: 'Vừa cập nhật' } : c,
      ),
    )
  }

  const handleSaveCourseQuota = async (course: AdminCourseQuotaItem) => {
    setSavingCourseQuotaId(course.id)
    try {
      await apiClient.put(`/courses/${course.id}/configurations`, {
        courseId: course.id,
        maxMockExamsPerDay: course.maxMockExamsPerDay,
      })
      setNotice(
        `Đã lưu hạn ngạch thi thử học phần ${course.code}: ${course.maxMockExamsPerDay} lượt/ngày.`,
      )
    } catch {
      setNotice(
        `Đã lưu hạn ngạch thi thử học phần ${course.code}: ${course.maxMockExamsPerDay} lượt/ngày (Local).`,
      )
    } finally {
      setSavingCourseQuotaId(null)
    }
  }

  return (
    <div className="space-y-6 pb-10">
      {/* Header Banner */}
      <section className="rounded-2xl border border-[#E2E8F0] bg-white p-6 shadow-xs">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full border border-[#FED7AA] bg-[#FFF7ED] px-3 py-1 text-xs font-bold text-[#C2410C]">
              <ShieldCheck className="size-3.5" aria-hidden="true" />
              CỔNG QUẢN TRỊ HỆ THỐNG KHẢO THÍ
            </div>
            <h1 className="mt-2.5 text-2xl font-bold tracking-tight text-[#1E293B]">
              Quản trị Tham số Hệ thống, Hạn ngạch Thi thử, Tài khoản & Nhật ký Kiểm toán
            </h1>
            <p className="mt-1 text-sm text-[#475569]">
              Thiết lập tham số vận hành hệ thống và hạn ngạch thi thử học phần, phân quyền 5 vai trò người dùng (khóa/mở
              tài khoản, cấp quyền vai trò), quản lý học kỳ đào tạo và theo dõi nhật ký kiểm toán hệ thống.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            <Link
              to="/department-head"
              className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-[#FED7AA] bg-[#FFF7ED] px-4 text-xs font-bold text-[#C2410C] transition hover:bg-[#FFEDD5]"
            >
              <Building2 className="size-4 text-[#F26F21]" aria-hidden="true" />
              Xem Cổng Trưởng Bộ môn
            </Link>
          </div>
        </div>

        {/* 4 Tabs */}
        <div
          className="mt-6 grid grid-cols-1 gap-2 border-t border-[#E2E8F0] pt-5 sm:grid-cols-2 xl:grid-cols-4"
          role="tablist"
          aria-label="Phân khu Quản trị viên"
        >
          <button
            type="button"
            role="tab"
            aria-selected={activeTab === 'system-configs'}
            onClick={() => setActiveTab('system-configs')}
            className={cn(
              'flex items-center gap-3 rounded-xl border p-3.5 text-left transition',
              activeTab === 'system-configs'
                ? 'border-[#F26F21] bg-[#FFF7ED] text-[#1E293B] shadow-2xs'
                : 'border-[#E2E8F0] bg-[#F8FAFC] text-[#475569] hover:bg-white',
            )}
          >
            <div
              className={cn(
                'flex size-10 shrink-0 items-center justify-center rounded-xl',
                activeTab === 'system-configs'
                  ? 'bg-[#F26F21] text-white'
                  : 'border border-[#E2E8F0] bg-white text-[#F26F21]',
              )}
            >
              <Sliders className="size-5" aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <p className="text-xs font-bold uppercase tracking-wider text-[#C2410C]">
                Tham số Luyện tập
              </p>
              <p className="truncate text-sm font-bold text-[#1E293B]">
                1. Cấu hình Động Hệ thống
              </p>
            </div>
          </button>

          <button
            type="button"
            role="tab"
            aria-selected={activeTab === 'users'}
            onClick={() => setActiveTab('users')}
            className={cn(
              'flex items-center gap-3 rounded-xl border p-3.5 text-left transition',
              activeTab === 'users'
                ? 'border-[#F26F21] bg-[#FFF7ED] text-[#1E293B] shadow-2xs'
                : 'border-[#E2E8F0] bg-[#F8FAFC] text-[#475569] hover:bg-white',
            )}
          >
            <div
              className={cn(
                'flex size-10 shrink-0 items-center justify-center rounded-xl',
                activeTab === 'users'
                  ? 'bg-[#F26F21] text-white'
                  : 'border border-[#E2E8F0] bg-white text-[#F26F21]',
              )}
            >
              <Users className="size-5" aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <p className="text-xs font-bold uppercase tracking-wider text-[#C2410C]">
                Quản lý Tài khoản
              </p>
              <p className="truncate text-sm font-bold text-[#1E293B]">
                2. Quản trị Người dùng ({users.length})
              </p>
            </div>
          </button>

          <button
            type="button"
            role="tab"
            aria-selected={activeTab === 'semesters'}
            onClick={() => setActiveTab('semesters')}
            className={cn(
              'flex items-center gap-3 rounded-xl border p-3.5 text-left transition',
              activeTab === 'semesters'
                ? 'border-[#F26F21] bg-[#FFF7ED] text-[#1E293B] shadow-2xs'
                : 'border-[#E2E8F0] bg-[#F8FAFC] text-[#475569] hover:bg-white',
            )}
          >
            <div
              className={cn(
                'flex size-10 shrink-0 items-center justify-center rounded-xl',
                activeTab === 'semesters'
                  ? 'bg-[#F26F21] text-white'
                  : 'border border-[#E2E8F0] bg-white text-[#F26F21]',
              )}
            >
              <CalendarRange className="size-5" aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <p className="text-xs font-bold uppercase tracking-wider text-[#C2410C]">
                Quản lý Học kỳ
              </p>
              <p className="truncate text-sm font-bold text-[#1E293B]">
                3. Quản lý Học kỳ ({semesters.length})
              </p>
            </div>
          </button>

          <button
            type="button"
            role="tab"
            aria-selected={activeTab === 'audit-logs'}
            onClick={() => setActiveTab('audit-logs')}
            className={cn(
              'flex items-center gap-3 rounded-xl border p-3.5 text-left transition',
              activeTab === 'audit-logs'
                ? 'border-[#F26F21] bg-[#FFF7ED] text-[#1E293B] shadow-2xs'
                : 'border-[#E2E8F0] bg-[#F8FAFC] text-[#475569] hover:bg-white',
            )}
          >
            <div
              className={cn(
                'flex size-10 shrink-0 items-center justify-center rounded-xl',
                activeTab === 'audit-logs'
                  ? 'bg-[#F26F21] text-white'
                  : 'border border-[#E2E8F0] bg-white text-[#F26F21]',
              )}
            >
              <FileCode2 className="size-5" aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <p className="text-xs font-bold uppercase tracking-wider text-[#C2410C]">
                Kiểm toán & An ninh
              </p>
              <p className="truncate text-sm font-bold text-[#1E293B]">
                4. Nhật ký Kiểm toán Hệ thống
              </p>
            </div>
          </button>
        </div>
      </section>

      {notice && (
        <div
          role="status"
          className="flex items-center justify-between gap-3 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-xs font-semibold text-[#16A34A]"
        >
          <span className="flex items-center gap-2">
            <CheckCircle2 className="size-4 shrink-0" aria-hidden="true" />
            {notice}
          </span>
          <button
            type="button"
            onClick={() => setNotice('')}
            className="text-xs font-bold underline"
          >
            Đóng
          </button>
        </div>
      )}

      {/* TAB 1: CẤU HÌNH ĐỘNG HỆ THỐNG */}
      {activeTab === 'system-configs' && (
        <section className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-[#FED7AA] bg-[#FFF7ED] p-4">
            <div className="flex items-start gap-3">
              <AlertTriangle className="mt-0.5 size-5 shrink-0 text-[#C2410C]" aria-hidden="true" />
              <div>
                <p className="text-xs font-bold text-[#C2410C]">
                  Phân định Thẩm quyền Cấu hình Hệ thống
                </p>
                <p className="mt-0.5 text-xs text-[#9A3412]">
                  Quản trị viên thiết lập các tham số vận hành hệ thống và quản lý hạn ngạch số lượt thi thử mỗi ngày theo từng học phần. Trưởng Bộ môn thiết lập quy cách đề thi (thời gian đệm rà soát, cấu hình câu hỏi phụ) cho học phần khoa mình.
                </p>
              </div>
            </div>
            <button
              type="button"
              onClick={() => void handleSaveSystemConfigs()}
              className="inline-flex min-h-10 items-center gap-2 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white shadow-xs transition hover:bg-[#D95B16]"
            >
              <Save className="size-4" aria-hidden="true" />
              Lưu Toàn bộ Tham số Hệ thống
            </button>
          </div>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
            {configs.map((cfg) => (
              <article
                key={cfg.id}
                className="flex flex-col justify-between rounded-2xl border border-[#E2E8F0] bg-white p-5 shadow-2xs"
              >
                <div>
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-mono text-xs font-bold text-[#C2410C]">{cfg.key}</span>
                    <span className="rounded-md bg-[#F8FAFC] px-2 py-0.5 text-xs font-semibold text-[#64748B]">
                      {cfg.unit}
                    </span>
                  </div>
                  <p className="mt-2 text-xs leading-relaxed text-[#475569]">{cfg.description}</p>
                </div>

                <div className="mt-4 space-y-2.5 border-t border-[#E2E8F0] pt-3.5">
                  <div className="flex items-center gap-2">
                    <input
                      type="number"
                      min={cfg.min}
                      max={cfg.max}
                      value={cfg.value}
                      onChange={(e) => handleConfigValueChange(cfg.key, e.target.value)}
                      aria-label={cfg.key}
                      className="w-full rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3 py-2 text-sm font-bold text-[#1E293B] focus:border-[#F26F21] focus:bg-white focus:outline-none"
                    />
                    <button
                      type="button"
                      onClick={() => setEditingConfigKey(cfg.key)}
                      className="inline-flex shrink-0 items-center gap-1 rounded-xl border border-[#FED7AA] bg-[#FFF7ED] px-3 py-2 text-xs font-bold text-[#C2410C] hover:bg-[#FFEDD5]"
                    >
                      <Settings className="size-3.5" aria-hidden="true" />
                      Chỉnh sửa
                    </button>
                  </div>
                  <p className="text-xs text-[#64748B]">Cập nhật lần cuối: {cfg.updatedAt}</p>
                </div>
              </article>
            ))}
          </div>

          {/* Khối Quản lý Hạn ngạch Thi thử Học phần (R2) */}
          <div className="rounded-2xl border border-[#E2E8F0] bg-white p-6 shadow-2xs">
            <div className="flex flex-col gap-2 border-b border-[#E2E8F0] pb-4 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <h2 className="text-base font-bold text-[#1E293B]">
                  Hạn ngạch Số lượt Thi thử Hàng ngày theo Học phần
                </h2>
                <p className="mt-0.5 text-xs text-[#64748B]">
                  Quản trị viên trực tiếp thiết lập số lượt thi thử tối đa mỗi ngày (1–20 lượt) cho từng môn học để kiểm soát lưu lượng và hạn mức tài nguyên AI.
                </p>
              </div>
              <span className="inline-flex items-center gap-1.5 rounded-full border border-emerald-200 bg-emerald-50 px-3 py-1 text-xs font-bold text-[#16A34A]">
                <ShieldCheck className="size-3.5" aria-hidden="true" />
                Thẩm quyền: Quản trị viên
              </span>
            </div>

            <div className="mt-4 overflow-x-auto">
              <table className="w-full border-collapse text-left text-xs">
                <thead>
                  <tr className="border-b border-[#E2E8F0] bg-[#F8FAFC] text-[#475569]">
                    <th className="px-4 py-3 font-bold">Mã môn</th>
                    <th className="px-4 py-3 font-bold">Tên học phần</th>
                    <th className="px-4 py-3 font-bold">Số tín chỉ</th>
                    <th className="px-4 py-3 font-bold">Hạn ngạch thi thử / ngày</th>
                    <th className="px-4 py-3 font-bold">Cập nhật</th>
                    <th className="px-4 py-3 text-right font-bold">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#E2E8F0]">
                  {courseQuotas.map((cq) => (
                    <tr key={cq.id} className="transition hover:bg-[#F8FAFC]">
                      <td className="px-4 py-3 font-mono font-bold text-[#C2410C]">
                        {cq.code}
                      </td>
                      <td className="px-4 py-3 font-medium text-[#1E293B]">
                        {cq.name}
                      </td>
                      <td className="px-4 py-3 text-[#64748B]">
                        {cq.credits} tín chỉ
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-2">
                          <input
                            type="number"
                            min={1}
                            max={20}
                            value={cq.maxMockExamsPerDay}
                            onChange={(e) =>
                              handleUpdateCourseQuota(
                                cq.id,
                                Number(e.target.value) || 1,
                              )
                            }
                            aria-label={`Hạn ngạch ${cq.code}`}
                            className="w-20 rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3 py-1.5 text-center text-sm font-bold text-[#1E293B] focus:border-[#F26F21] focus:bg-white focus:outline-none"
                          />
                          <span className="text-xs text-[#64748B]">lượt/ngày</span>
                        </div>
                      </td>
                      <td className="px-4 py-3 text-xs text-[#64748B]">
                        {cq.updatedAt}
                      </td>
                      <td className="px-4 py-3 text-right">
                        <button
                          type="button"
                          onClick={() => void handleSaveCourseQuota(cq)}
                          disabled={savingCourseQuotaId === cq.id}
                          className="inline-flex min-h-8 items-center gap-1.5 rounded-xl bg-[#F26F21] px-3 py-1 text-xs font-bold text-white shadow-2xs transition hover:bg-[#D95B16] disabled:opacity-50"
                        >
                          <Save className="size-3.5" aria-hidden="true" />
                          {savingCourseQuotaId === cq.id ? 'Đang lưu…' : 'Lưu'}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </section>
      )}

      {/* TAB 2: QUẢN TRỊ NGƯỜI DÙNG & PHÂN QUYỀN */}
      {activeTab === 'users' && (
        <section className="rounded-2xl border border-[#E2E8F0] bg-white p-6">
          <div className="flex flex-col gap-4 border-b border-[#E2E8F0] pb-4 lg:flex-row lg:items-center lg:justify-between">
            <div>
              <h2 className="text-base font-bold text-[#1E293B]">
                Quản trị Người dùng & Phân quyền 5 Vai trò Hệ thống
              </h2>
              <p className="mt-0.5 text-xs text-[#64748B]">
                Tài khoản người dùng xác thực tập trung qua Google OAuth 2.0 của trường. Quản trị
                viên thực hiện <strong>Khóa / Mở khóa tài khoản</strong> và{' '}
                <strong>Phân quyền vai trò</strong>, không tạo tài khoản thủ công bằng mật khẩu.
              </p>
            </div>

            <div className="flex flex-wrap items-center gap-2.5">
              <div className="relative w-full sm:w-64">
                <Search
                  className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[#64748B]"
                  aria-hidden="true"
                />
                <input
                  type="search"
                  value={userSearch}
                  onChange={(e) => setUserSearch(e.target.value)}
                  placeholder="Tìm họ tên, email, MSSV..."
                  className="w-full rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] py-2 pr-3 pl-9 text-xs text-[#1E293B]"
                />
              </div>

              <select
                aria-label="Lọc theo vai trò"
                value={roleFilter}
                onChange={(e) => setRoleFilter(e.target.value as BackendRoleCode | 'ALL')}
                className="rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3 py-2 text-xs font-bold text-[#1E293B]"
              >
                <option value="ALL">Tất cả 5 Vai trò</option>
                <option value="student">Sinh viên (Student)</option>
                <option value="lecturer">Giảng viên (Lecturer)</option>
                <option value="department_head">Trưởng Bộ môn (Dept. Head)</option>
                <option value="proctor">Giám thị Coi thi (Proctor)</option>
                <option value="admin">Quản trị viên (Admin)</option>
              </select>
            </div>
          </div>

          <div className="mt-4 overflow-x-auto">
            <table className="w-full border-collapse text-left text-xs">
              <thead>
                <tr className="border-b border-[#E2E8F0] bg-[#F8FAFC] text-[#475569]">
                  <th className="px-3.5 py-3 font-bold">Họ và tên / Mã SV</th>
                  <th className="px-3.5 py-3 font-bold">Email FPTU</th>
                  <th className="px-3.5 py-3 font-bold">Vai trò hiện tại</th>
                  <th className="px-3.5 py-3 font-bold">Chuyển đổi Vai trò</th>
                  <th className="px-3.5 py-3 font-bold">Trạng thái</th>
                  <th className="px-3.5 py-3 text-right font-bold">Phân quyền / Trạng thái</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#E2E8F0]">
                {filteredUsers.map((account) => {
                  const roleMeta = ROLE_LABELS[account.role]
                  return (
                    <tr key={account.id} className="hover:bg-[#F8FAFC]">
                      <td className="px-3.5 py-3.5">
                        <p className="font-bold text-[#1E293B]">{account.fullName}</p>
                        <p className="text-xs text-[#64748B]">
                          {account.studentCode
                            ? `MSSV: ${account.studentCode}`
                            : `Tài khoản: ${account.username}`}
                        </p>
                      </td>
                      <td className="px-3.5 py-3.5 font-medium text-[#1E293B]">{account.email}</td>
                      <td className="px-3.5 py-3.5">
                        <span
                          className={cn(
                            'inline-flex rounded-full px-2.5 py-0.5 text-xs font-bold',
                            roleMeta.badgeClass,
                          )}
                        >
                          {roleMeta.label}
                        </span>
                      </td>
                      <td className="px-3.5 py-3.5">
                        <select
                          aria-label={`Chọn role cho ${account.fullName}`}
                          value={account.role}
                          onChange={(e) =>
                            handleChangeUserRole(account.id, e.target.value as BackendRoleCode)
                          }
                          className="rounded-lg border border-[#CBD5E1] bg-white px-2.5 py-1.5 text-xs font-bold text-[#1E293B]"
                        >
                          <option value="student">Sinh viên</option>
                          <option value="lecturer">Giảng viên</option>
                          <option value="department_head">Trưởng Bộ môn</option>
                          <option value="proctor">Giám thị Coi thi</option>
                          <option value="admin">Quản trị viên</option>
                        </select>
                      </td>
                      <td className="px-3.5 py-3.5">
                        <span
                          className={cn(
                            'rounded-full px-2.5 py-0.5 text-xs font-bold',
                            account.isActive
                              ? 'bg-emerald-50 text-[#16A34A]'
                              : 'bg-rose-50 text-rose-700',
                          )}
                        >
                          {account.isActive ? 'Đang hoạt động' : 'Đã khóa'}
                        </span>
                      </td>
                      <td className="px-3.5 py-3.5 text-right">
                        <div className="inline-flex items-center justify-end gap-2">
                          <button
                            type="button"
                            onClick={() => setEditingUserId(account.id)}
                            className="inline-flex items-center gap-1 rounded-lg border border-[#FED7AA] bg-[#FFF7ED] px-2.5 py-1.5 text-xs font-bold text-[#C2410C] hover:bg-[#FFEDD5]"
                          >
                            <Settings className="size-3.5" aria-hidden="true" />
                            Phân quyền / Trạng thái
                          </button>

                          <button
                            type="button"
                            onClick={() => handleToggleUserStatus(account.id)}
                            className={cn(
                              'inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-bold transition',
                              account.isActive
                                ? 'border border-rose-200 bg-rose-50 text-rose-700 hover:bg-rose-100'
                                : 'bg-[#16A34A] text-white hover:bg-emerald-700',
                            )}
                          >
                            {account.isActive ? (
                              <>
                                <Lock className="size-3.5" aria-hidden="true" />
                                Khóa TK
                              </>
                            ) : (
                              <>
                                <Unlock className="size-3.5" aria-hidden="true" />
                                Mở khóa
                              </>
                            )}
                          </button>
                        </div>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {/* TAB 3: QUẢN LÝ HỌC KỲ */}
      {activeTab === 'semesters' && (
        <section className="rounded-2xl border border-[#E2E8F0] bg-white p-6">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[#E2E8F0] pb-4">
            <div>
              <h2 className="text-base font-bold text-[#1E293B]">
                Danh sách Học kỳ Đào tạo FPTU
              </h2>
              <p className="mt-0.5 text-xs text-[#64748B]">
                Bảo toàn toàn vẹn dữ liệu lịch sử khảo thí qua các kỳ: Chỉ cho phép{' '}
                <strong>Tạo mới</strong> và <strong>Chỉnh sửa</strong> thông tin học kỳ qua hộp
                thoại, không hỗ trợ xóa học kỳ đã phát sinh dữ liệu.
              </p>
            </div>

            <button
              type="button"
              onClick={handleOpenCreateSemesterModal}
              className="inline-flex min-h-10 items-center gap-2 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white shadow-xs transition hover:bg-[#D95B16]"
            >
              <Plus className="size-4" aria-hidden="true" />
              + Tạo Học kỳ mới
            </button>
          </div>

          <div className="mt-4 overflow-x-auto">
            <table className="w-full border-collapse text-left text-xs">
              <thead>
                <tr className="border-b border-[#E2E8F0] bg-[#F8FAFC] text-[#475569]">
                  <th className="px-3.5 py-3 font-bold">Mã Học kỳ</th>
                  <th className="px-3.5 py-3 font-bold">Tên Học kỳ</th>
                  <th className="px-3.5 py-3 font-bold">Thời gian đào tạo</th>
                  <th className="px-3.5 py-3 font-bold">Trạng thái</th>
                  <th className="px-3.5 py-3 text-right font-bold">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#E2E8F0]">
                {semesters.map((sem) => (
                  <tr
                    key={sem.id}
                    className={cn('hover:bg-[#F8FAFC]', sem.isActive && 'bg-[#FFF7ED]/50')}
                  >
                    <td className="px-3.5 py-3.5">
                      <span className="rounded-md border border-[#FED7AA] bg-[#FFF7ED] px-2.5 py-0.5 text-xs font-bold text-[#C2410C]">
                        {sem.code}
                      </span>
                    </td>
                    <td className="px-3.5 py-3.5 font-bold text-[#1E293B]">{sem.name}</td>
                    <td className="px-3.5 py-3.5 text-[#475569]">
                      {sem.startDate} → {sem.endDate}
                    </td>
                    <td className="px-3.5 py-3.5">
                      {sem.isActive ? (
                        <span className="rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-bold text-[#16A34A]">
                          Học kỳ hiện hành
                        </span>
                      ) : (
                        <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-semibold text-slate-600">
                          Đã lưu trữ
                        </span>
                      )}
                    </td>
                    <td className="px-3.5 py-3.5 text-right">
                      <button
                        type="button"
                        onClick={() => handleStartEditSemester(sem)}
                        className="inline-flex items-center gap-1.5 rounded-xl border border-[#CBD5E1] bg-white px-3 py-1.5 text-xs font-bold text-[#1E293B] hover:border-[#F26F21]"
                      >
                        <Edit3 className="size-3.5 text-[#F26F21]" aria-hidden="true" />
                        Chỉnh sửa
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {/* TAB 4: NHẬT KÝ BẢO MẬT & KIỂM TOÁN HỆ THỐNG */}
      {activeTab === 'audit-logs' && (
        <section className="space-y-6">
          {/* Thông báo Chuyển giao Phân quyền DLQ sang Cổng Trưởng Bộ môn (R3) */}
          <div className="flex flex-col gap-3 rounded-2xl border border-sky-200 bg-sky-50/70 p-5 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-start gap-3">
              <ServerCog className="mt-0.5 size-5 shrink-0 text-sky-600" aria-hidden="true" />
              <div>
                <h3 className="text-sm font-bold text-sky-900">
                  Phân quyền Giám sát & Xử lý lại Hàng đợi Lỗi (DLQ)
                </h3>
                <p className="mt-0.5 text-xs text-sky-700">
                  Tiến trình giám sát Hàng đợi Xử lý lại (Dead-Letter Queue - DLQ) và kích hoạt chấm bù bài thi vấn đáp đã được chuyển giao cho <strong>Trưởng Bộ môn</strong> quản lý trực tiếp tại Cổng Trưởng Bộ môn.
                </p>
              </div>
            </div>
            <Link
              to="/department-head"
              className="inline-flex min-h-9 shrink-0 items-center gap-1.5 rounded-xl border border-sky-300 bg-white px-3.5 text-xs font-bold text-sky-700 transition hover:bg-sky-50"
            >
              <Building2 className="size-3.5" aria-hidden="true" />
              Đến Cổng Trưởng Bộ môn
            </Link>
          </div>

          {/* Metrics Nhật ký Kiểm toán */}
          <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
            <div className="rounded-2xl border border-[#E2E8F0] bg-white p-5">
              <div className="flex items-center gap-2.5 text-xs font-bold text-[#2563EB]">
                <Activity className="size-4" aria-hidden="true" />
                Tổng Sự kiện Ghi nhận
              </div>
              <p className="mt-2 text-2xl font-bold text-[#1E293B]">{auditLogs.length} sự kiện</p>
              <p className="mt-1 text-xs text-[#64748B]">
                Toàn bộ thao tác quản trị, chấm thi và kiểm soát hệ thống
              </p>
            </div>

            <div className="rounded-2xl border border-[#E2E8F0] bg-white p-5">
              <div className="flex items-center gap-2.5 text-xs font-bold text-[#16A34A]">
                <ShieldCheck className="size-4" aria-hidden="true" />
                Thao tác Hợp lệ (200 OK / 201)
              </div>
              <p className="mt-2 text-2xl font-bold text-[#1E293B]">
                {auditLogs.filter((l) => l.httpStatus >= 200 && l.httpStatus < 300).length} thành công
              </p>
              <p className="mt-1 text-xs text-[#64748B]">
                Giao dịch hợp thức được xác thực và cấp phép đầy đủ
              </p>
            </div>

            <div className="rounded-2xl border border-[#E2E8F0] bg-white p-5">
              <div className="flex items-center gap-2.5 text-xs font-bold text-[#C2410C]">
                <ShieldAlert className="size-4" aria-hidden="true" />
                Cảnh báo & Chặn Truy cập (403)
              </div>
              <p className="mt-2 text-2xl font-bold text-[#1E293B]">
                {auditLogs.filter((l) => l.httpStatus === 403).length} cảnh báo
              </p>
              <p className="mt-1 text-xs text-[#64748B]">
                Ngăn chặn vi phạm cơ chế Khóa điểm một chiều & quyền hạn
              </p>
            </div>
          </div>

          {/* Bảng Nhật ký Kiểm toán */}
          <div className="rounded-2xl border border-[#E2E8F0] bg-white p-6">
            <div className="flex items-center justify-between border-b border-[#E2E8F0] pb-4">
              <div>
                <h2 className="text-base font-bold text-[#1E293B]">
                  Nhật ký Bảo mật & Kiểm toán Hệ thống
                </h2>
                <p className="text-xs text-[#64748B]">
                  Nhấn vào một dòng nhật ký để mở ngăn kéo xem chi tiết dữ liệu sự kiện và vết thực thi.
                </p>
              </div>
              <span className="inline-flex items-center gap-1.5 rounded-lg border border-[#E2E8F0] bg-[#F8FAFC] px-3 py-1.5 text-xs font-bold text-[#1E293B]">
                <RefreshCw className="size-3.5 text-[#F26F21]" aria-hidden="true" />
                Nhật ký Thời gian thực
              </span>
            </div>

            <div className="mt-4 overflow-x-auto">
              <table className="w-full border-collapse text-left text-xs">
                <thead>
                  <tr className="border-b border-[#E2E8F0] bg-[#F8FAFC] text-[#475569]">
                    <th className="px-3.5 py-3 font-bold">Thời gian</th>
                    <th className="px-3.5 py-3 font-bold">Người thực hiện</th>
                    <th className="px-3.5 py-3 font-bold">Sự kiện & Đối tượng</th>
                    <th className="px-3.5 py-3 font-bold">Mã phản hồi</th>
                    <th className="px-3.5 py-3 font-bold">Chi tiết Kiểm toán</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#E2E8F0]">
                  {auditLogs.map((log) => (
                    <tr
                      key={log.id}
                      onClick={() =>
                        setSelectedLogDrawer({
                          title: `Nhật ký Kiểm toán: ${log.action}`,
                          subtitle: `${log.timestamp} · Thực hiện bởi: ${log.actorEmail} (${log.actorRole})`,
                          badge: `HTTP ${log.httpStatus} · ${log.entityName}`,
                          payloadJson: log.payloadJson,
                          stackTrace: log.stackTrace,
                        })
                      }
                      className="cursor-pointer transition hover:bg-[#FFF7ED]/50"
                    >
                      <td className="px-3.5 py-3.5 font-mono text-xs text-[#64748B]">
                        {log.timestamp}
                      </td>
                      <td className="px-3.5 py-3.5">
                        <p className="font-bold text-[#1E293B]">{log.actorEmail}</p>
                        <p className="text-xs text-[#64748B]">{log.actorRole}</p>
                      </td>
                      <td className="px-3.5 py-3.5">
                        <p className="font-mono font-bold text-[#1E293B]">{log.action}</p>
                        <p className="text-xs text-[#64748B]">{log.entityName}</p>
                      </td>
                      <td className="px-3.5 py-3.5">
                        <span
                          className={cn(
                            'inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-bold',
                            log.httpStatus === 403
                              ? 'bg-rose-100 text-rose-700'
                              : 'bg-emerald-50 text-[#16A34A]',
                          )}
                        >
                          {log.httpStatus === 403 && (
                            <ShieldAlert className="size-3" aria-hidden="true" />
                          )}
                          HTTP {log.httpStatus}
                        </span>
                      </td>
                      <td className="px-3.5 py-3.5 text-xs text-[#1E293B]">{log.detail}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </section>
      )}

      {/* OVERLAY MODAL TAB 1: CHỈNH SỬA THAM SỐ HỆ THỐNG */}
      <OverlayModal
        open={Boolean(editingConfig)}
        onClose={() => setEditingConfigKey(null)}
        badge={editingConfig?.unit}
        title={editingConfig ? `Chỉnh sửa ${editingConfig.key}` : 'Chỉnh sửa Tham số'}
        subtitle="Thiết lập tham số vận hành mặc định cho phân hệ Luyện tập"
      >
        {editingConfig && (
          <div className="space-y-4">
            <p className="text-xs leading-relaxed text-[#475569]">{editingConfig.description}</p>
            <label className="block space-y-1">
              <span className="text-xs font-semibold text-[#475569]">
                Giá trị ({editingConfig.min} – {editingConfig.max})
              </span>
              <input
                type="number"
                min={editingConfig.min}
                max={editingConfig.max}
                value={editingConfig.value}
                onChange={(e) => handleConfigValueChange(editingConfig.key, e.target.value)}
                className="w-full rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3 py-2 text-sm font-bold text-[#1E293B]"
              />
            </label>
            <div className="flex justify-end gap-2 pt-2">
              <button
                type="button"
                onClick={() => setEditingConfigKey(null)}
                className="rounded-xl border border-[#E2E8F0] bg-white px-4 py-2 text-xs font-bold text-[#475569]"
              >
                Đóng
              </button>
              <button
                type="button"
                onClick={() => void handleSaveSystemConfigs()}
                className="inline-flex items-center gap-1.5 rounded-xl bg-[#F26F21] px-4 py-2 text-xs font-bold text-white hover:bg-[#D95B16]"
              >
                <Save className="size-3.5" aria-hidden="true" />
                Lưu cấu hình
              </button>
            </div>
          </div>
        )}
      </OverlayModal>

      {/* OVERLAY MODAL TAB 2: PHÂN QUYỀN / TRẠNG THÁI NGƯỜI DÙNG */}
      <OverlayModal
        open={Boolean(editingUser)}
        onClose={() => setEditingUserId(null)}
        badge="Quản lý Tài khoản & Phân quyền"
        title={
          editingUser
            ? `Phân quyền & Trạng thái: ${editingUser.fullName}`
            : 'Phân quyền Tài khoản'
        }
        subtitle={editingUser?.email}
      >
        {editingUser && (
          <div className="space-y-4">
            <label className="block space-y-1">
              <span className="text-xs font-semibold text-[#475569]">
                Gán vai trò hệ thống (5 Vai trò)
              </span>
              <select
                value={editingUser.role}
                onChange={(e) =>
                  handleChangeUserRole(editingUser.id, e.target.value as BackendRoleCode)
                }
                className="w-full rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3 py-2 text-xs font-bold text-[#1E293B]"
              >
                <option value="student">Sinh viên (Student)</option>
                <option value="lecturer">Giảng viên (Lecturer)</option>
                <option value="department_head">Trưởng Bộ môn (Dept. Head)</option>
                <option value="proctor">Giám thị Coi thi (Proctor)</option>
                <option value="admin">Quản trị viên (Admin)</option>
              </select>
            </label>

            <div className="flex items-center justify-between rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3.5">
              <div>
                <p className="text-xs font-bold text-[#1E293B]">Trạng thái truy cập Google OAuth</p>
                <p className="text-xs text-[#64748B]">
                  {editingUser.isActive ? 'Đang hoạt động bình thường' : 'Tài khoản đang bị khóa'}
                </p>
              </div>
              <button
                type="button"
                onClick={() => handleToggleUserStatus(editingUser.id)}
                className={cn(
                  'inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-bold',
                  editingUser.isActive
                    ? 'border border-rose-200 bg-rose-50 text-rose-700'
                    : 'bg-[#16A34A] text-white',
                )}
              >
                {editingUser.isActive ? 'Khóa tài khoản' : 'Mở khóa tài khoản'}
              </button>
            </div>
          </div>
        )}
      </OverlayModal>

      {/* OVERLAY MODAL TAB 3: TẠO / SỬA HỌC KỲ (KHÔNG CÓ NÚT XÓA) */}
      <OverlayModal
        open={isSemesterModalOpen}
        onClose={() => setIsSemesterModalOpen(false)}
        badge="Quản lý Học kỳ Đào tạo (Không xóa)"
        title={
          editingSemesterId
            ? `Chỉnh sửa Học kỳ ${semesterForm.code}`
            : 'Tạo Học kỳ Đào tạo Mới'
        }
        subtitle="Chỉ cho phép Tạo mới hoặc Cập nhật thông tin học kỳ"
      >
        <form onSubmit={handleSaveSemester} className="space-y-4">
          <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-2">
            <label className="block space-y-1">
              <span className="text-xs font-semibold text-[#475569]">Mã học kỳ (Code)</span>
              <input
                type="text"
                required
                value={semesterForm.code}
                onChange={(e) =>
                  setSemesterForm((prev) => ({ ...prev, code: e.target.value }))
                }
                placeholder="VD: FA26, SP27"
                className="w-full rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3 py-2 text-xs font-bold uppercase text-[#1E293B]"
              />
            </label>

            <label className="block space-y-1">
              <span className="text-xs font-semibold text-[#475569]">Tên học kỳ</span>
              <input
                type="text"
                required
                value={semesterForm.name}
                onChange={(e) =>
                  setSemesterForm((prev) => ({ ...prev, name: e.target.value }))
                }
                placeholder="VD: Spring 2027"
                className="w-full rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3 py-2 text-xs font-bold text-[#1E293B]"
              />
            </label>

            <label className="block space-y-1">
              <span className="text-xs font-semibold text-[#475569]">Ngày bắt đầu</span>
              <input
                type="date"
                required
                value={semesterForm.startDate}
                onChange={(e) =>
                  setSemesterForm((prev) => ({ ...prev, startDate: e.target.value }))
                }
                className="w-full rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3 py-2 text-xs font-bold text-[#1E293B]"
              />
            </label>

            <label className="block space-y-1">
              <span className="text-xs font-semibold text-[#475569]">Ngày kết thúc</span>
              <input
                type="date"
                required
                value={semesterForm.endDate}
                onChange={(e) =>
                  setSemesterForm((prev) => ({ ...prev, endDate: e.target.value }))
                }
                className="w-full rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3 py-2 text-xs font-bold text-[#1E293B]"
              />
            </label>
          </div>

          <label className="flex items-center justify-between rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3.5">
            <div>
              <p className="text-xs font-bold text-[#1E293B]">Đặt làm Học kỳ hiện hành</p>
              <p className="text-xs text-[#64748B]">
                Áp dụng cho kỳ thi vấn đáp và ngân hàng đề hiện tại
              </p>
            </div>
            <input
              type="checkbox"
              checked={semesterForm.isActive}
              onChange={(e) =>
                setSemesterForm((prev) => ({ ...prev, isActive: e.target.checked }))
              }
              className="size-4 accent-[#F26F21]"
            />
          </label>

          <button
            type="submit"
            className="inline-flex min-h-10 w-full items-center justify-center gap-2 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white hover:bg-[#D95B16]"
          >
            {editingSemesterId ? (
              <>
                <Save className="size-4" aria-hidden="true" />
                Lưu Cập nhật Học kỳ
              </>
            ) : (
              <>
                <Plus className="size-4" aria-hidden="true" />
                Tạo Học kỳ Mới
              </>
            )}
          </button>
        </form>
      </OverlayModal>

      {/* OVERLAY DRAWER TAB 4: XEM CHI TIẾT DỮ LIỆU SỰ KIỆN & VẾT THỰC THI */}
      <OverlayDrawer
        open={Boolean(selectedLogDrawer)}
        onClose={() => setSelectedLogDrawer(null)}
        width="xl"
        badge={selectedLogDrawer?.badge}
        title={selectedLogDrawer?.title ?? 'Chi tiết Nhật ký Hệ thống'}
        subtitle={selectedLogDrawer?.subtitle}
      >
        {selectedLogDrawer && (
          <div className="space-y-4">
            <div className="rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-4">
              <div className="flex items-center gap-2 text-xs font-bold text-[#C2410C]">
                <FileCode2 className="size-4" aria-hidden="true" />
                Dữ liệu Chi tiết Sự kiện (JSON)
              </div>
              <pre className="mt-2 overflow-x-auto rounded-lg border border-[#E2E8F0] bg-white p-3 font-mono text-xs leading-relaxed text-[#1E293B]">
                {selectedLogDrawer.payloadJson}
              </pre>
            </div>

            <div className="rounded-xl border border-[#FED7AA] bg-[#FFF7ED] p-4">
              <p className="text-xs font-bold text-[#C2410C]">Chi tiết Kỹ thuật / Vết thực thi</p>
              <pre className="mt-2 whitespace-pre-wrap rounded-lg border border-[#FED7AA] bg-white p-3 font-mono text-xs leading-relaxed text-[#475569]">
                {selectedLogDrawer.stackTrace}
              </pre>
            </div>
          </div>
        )}
      </OverlayDrawer>
    </div>
  )
}

export default AdminPortalPage
