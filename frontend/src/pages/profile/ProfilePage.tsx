import { useState } from 'react'
import { ChangePasswordPage } from '@/pages/auth/ChangePasswordPage'
import {
  BadgeCheck,
  CalendarDays,
  KeyRound,
  LogOut,
  Mail,
  ShieldCheck,
  SlidersHorizontal,
  UserRound,
  UsersRound,
  Volume2,
} from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'
import { canonicalRole } from '@/routes/routeAccess'
import { ds } from '@/design/system'
import { SectionHeader } from '@/design/ui'
import { cn } from '@/lib/utils'
import {
  readDevicePreferences,
  updateDevicePreferences,
  type DevicePreferences,
} from '@/utils/devicePreferences'

const ROLE_LABELS = {
  Student: 'Sinh viên',
  Instructor: 'Giảng viên',
  DepartmentHead: 'Trưởng Bộ môn',
  ExaminationOfficer: 'Cán bộ Khảo thí',
  Proctor: 'Giám thị',
  Admin: 'Quản trị viên',
} as const

function getInitials(fullName: string) {
  return fullName
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(-2)
    .map((word) => word.charAt(0).toLocaleUpperCase('vi-VN'))
    .join('')
}

function formatCreatedAt(value?: string) {
  if (!value) return 'Chưa cập nhật'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return 'Chưa cập nhật'
  return date.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

function ProfileField({
  icon: Icon,
  label,
  value,
}: {
  icon: typeof UserRound
  label: string
  value: string
}) {
  return (
    <div className="flex min-w-0 items-center gap-3 rounded-xl border border-[#82aedb]/45 bg-[#F8FBFD] px-4 py-3">
      <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-[#E7F3FB] text-[#1C4D8D]">
        <Icon className="size-4" aria-hidden="true" />
      </span>
      <div className="min-w-0">
        <p className="text-[10px] font-bold tracking-[0.08em] text-[#718096] uppercase">{label}</p>
        <p className="mt-0.5 truncate text-sm font-bold text-[#0F2854]" title={value}>{value}</p>
      </div>
    </div>
  )
}

function PreferenceToggle({
  icon: Icon,
  checked,
  title,
  description,
  onChange,
}: {
  icon: typeof UserRound
  checked: boolean
  title: string
  description: string
  onChange: (checked: boolean) => void
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      className="flex h-[88px] w-full items-center justify-between gap-4 rounded-xl border border-[#82aedb]/45 bg-white px-4 py-2.5 text-left transition hover:border-[#4988C4] hover:bg-[#F8FBFD] focus-visible:outline-2 focus-visible:outline-[#1C4D8D]"
    >
      <span className="flex min-w-0 items-start gap-3">
        <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-[#E7F3FB] text-[#1C4D8D]">
          <Icon className="size-4" aria-hidden="true" />
        </span>
        <span className="min-w-0">
          <span className="block text-base font-bold text-[#0F2854]">{title}</span>
          <span className="mt-0.5 block text-xs leading-relaxed text-[#5b6b84]">{description}</span>
        </span>
      </span>
      <span
        className={cn(
          'relative h-6 w-11 shrink-0 rounded-full transition-colors',
          checked ? 'bg-[#16836a]' : 'bg-[#cbd5e1]',
        )}
        aria-hidden="true"
      >
        <span
          className={cn(
            'absolute top-0.5 size-5 rounded-full bg-white shadow-sm transition-[left]',
            checked ? 'left-5' : 'left-0.5',
          )}
        />
      </span>
    </button>
  )
}

export function ProfilePage() {
  const { user } = useAuth()
  const [passwordOpen, setPasswordOpen] = useState(false)
  const [preferences, setPreferences] = useState<DevicePreferences>(() => readDevicePreferences())
  const fullName = user?.fullName?.trim() || 'Người dùng'
  const portalRole = (user ? canonicalRole(user.role) : null) ?? 'Student'
  const roleLabel = ROLE_LABELS[portalRole]
  const identityCode = portalRole === 'Student' ? (user?.studentCode || 'Chưa cập nhật') : roleLabel
  const changePreference = <Key extends keyof DevicePreferences>(
    key: Key,
    value: DevicePreferences[Key],
  ) => {
    const next = updateDevicePreferences({ [key]: value })
    setPreferences(next)
  }

  return (
    <div className={ds.page}>
      {portalRole === 'Student' && (
      <section className={cn(ds.section, 'bg-[linear-gradient(135deg,#dff1fa_0%,#f4fbfd_48%,#ffffff_100%)]')}>
        <div className="flex flex-col gap-5 p-5 lg:flex-row lg:items-center">
          <span className="grid size-20 shrink-0 place-items-center rounded-2xl bg-[#0F2854] text-2xl font-black text-white shadow-[0_10px_24px_rgba(15,40,84,0.24)]">
            {getInitials(fullName) || 'ND'}
          </span>
          <div className="min-w-0 flex-1">
            <h2 className="truncate text-2xl font-black text-[#0F2854]">{fullName}</h2>
            <div className="mt-3 flex flex-wrap gap-2">
              <span className="inline-flex items-center gap-1.5 rounded-full border border-[#82aedb]/55 bg-white px-2.5 py-1 text-xs font-bold text-[#1C4D8D]">
                <UsersRound className="size-3.5" aria-hidden="true" />
                {roleLabel}
              </span>
              <span className="inline-flex items-center gap-1.5 rounded-full border border-[#b7e4cf] bg-[#eef9f5] px-2.5 py-1 text-xs font-bold text-[#16836a]">
                <BadgeCheck className="size-3.5" aria-hidden="true" />
                Tài khoản đang hoạt động
              </span>
            </div>
          </div>
          <div className="grid w-full gap-2.5 sm:grid-cols-3 lg:w-auto lg:min-w-[620px]">
            <ProfileField icon={Mail} label="Email" value={user?.email || 'Chưa cập nhật'} />
            <ProfileField
              icon={UsersRound}
              label={portalRole === 'Student' ? 'Mã sinh viên' : 'Vai trò'}
              value={identityCode}
            />
            <ProfileField icon={CalendarDays} label="Ngày tạo tài khoản" value={formatCreatedAt(user?.createdAt)} />
          </div>
        </div>
      </section>
      )}

      <div className="grid gap-4 lg:auto-rows-fr lg:grid-cols-2 lg:items-stretch">
        {portalRole !== 'Student' && (
          <section className={cn(ds.section, 'h-full')}>
            <SectionHeader
              icon={UserRound}
              title="Thông tin cá nhân"
              hint="Thông tin định danh được đồng bộ từ tài khoản hệ thống"
            />
            <div className={cn(ds.sectionBody, 'space-y-5')}>
              <div className="flex flex-col gap-4 rounded-2xl border border-[#82aedb]/55 bg-[linear-gradient(135deg,#eef8fc_0%,#ffffff_65%)] p-4 sm:flex-row sm:items-center">
                <span className="grid size-20 shrink-0 place-items-center rounded-2xl bg-[#0F2854] text-2xl font-black text-white shadow-[0_10px_24px_rgba(15,40,84,0.24)]">
                  {getInitials(fullName) || 'ND'}
                </span>
                <div className="min-w-0">
                  <h2 className="truncate text-xl font-black text-[#0F2854]">{fullName}</h2>
                  <p className="mt-1 truncate text-sm text-[#5b6b84]">{user?.email || 'Chưa cập nhật email'}</p>
                  <span className="mt-2 inline-flex items-center gap-1.5 rounded-full border border-[#82aedb]/55 bg-white px-2.5 py-1 text-xs font-bold text-[#1C4D8D]">
                    <UsersRound className="size-3.5" aria-hidden="true" />
                    {roleLabel}
                  </span>
                </div>
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <ProfileField icon={UserRound} label="Họ và tên" value={fullName} />
                <ProfileField icon={Mail} label="Email" value={user?.email || 'Chưa cập nhật'} />
                <ProfileField icon={UsersRound} label="Vai trò" value={roleLabel} />
                <ProfileField icon={CalendarDays} label="Ngày tạo tài khoản" value={formatCreatedAt(user?.createdAt)} />
              </div>
            </div>
          </section>
        )}
        <section className={cn(ds.section, 'flex h-full flex-col', portalRole === 'Student' && 'order-2')}>
          <SectionHeader
            icon={ShieldCheck}
            title="Bảo mật tài khoản"
            hint="Kiểm soát quyền truy cập tài khoản"
          />
          <div
            className={cn(
              ds.sectionBody,
              'flex flex-1 flex-col',
              portalRole === 'Student' ? 'gap-3' : 'gap-5',
            )}
          >
            <div
              className={cn(
                'flex items-center rounded-2xl border border-[#b7e4cf] bg-[linear-gradient(135deg,#e8f8f1_0%,#f7fcfa_100%)] p-4',
                portalRole === 'Student' ? 'h-[88px]' : 'h-28',
              )}
            >
              <div className="flex items-start gap-3">
                <span className="grid size-11 shrink-0 place-items-center rounded-xl border border-[#b7e4cf] bg-white text-[#16836a] shadow-sm">
                  <ShieldCheck className="size-5" aria-hidden="true" />
                </span>
                <div>
                  <p className="text-base font-bold text-[#0F2854]">Tài khoản được bảo vệ</p>
                  <p className="mt-1 text-xs leading-relaxed text-[#5b6b84]">
                    Mật khẩu được mã hóa và phiên đăng nhập được kiểm soát bằng token bảo mật.
                  </p>
                </div>
              </div>
            </div>

            <div className="flex h-full flex-1 flex-col justify-between gap-4 rounded-2xl border border-[#82aedb]/55 bg-[linear-gradient(135deg,#edf7fc_0%,#ffffff_72%)] p-4 sm:flex-row sm:items-center">
              <div className="flex min-w-0 items-start gap-3">
                <span className="grid size-11 shrink-0 place-items-center rounded-xl border border-[#82aedb]/45 bg-[#E7F3FB] text-[#1C4D8D]">
                  <KeyRound className="size-5" aria-hidden="true" />
                </span>
                <div className="min-w-0">
                  <p className="text-base font-bold text-[#0F2854]">Mật khẩu đăng nhập</p>
                  <p className="mt-1 text-xs leading-relaxed text-[#5b6b84]">
                    Đổi mật khẩu định kỳ và không dùng lại ở dịch vụ khác.
                  </p>
                  <span className="mt-2 inline-flex rounded-full border border-[#f0d09d] bg-[#fff7e8] px-2.5 py-1 text-[11px] font-bold text-[#b45f0b]">
                    Khuyến nghị bảo mật
                  </span>
                </div>
              </div>
              <button type="button" onClick={() => setPasswordOpen(true)} className={cn(ds.outlineButton, 'shrink-0 self-start sm:self-center')}>
                <KeyRound className="size-4" aria-hidden="true" />
                Đổi mật khẩu
              </button>
            </div>
          </div>
        </section>

        {portalRole === 'Student' && (
          <section className={cn(ds.section, 'order-1 flex h-full flex-col')}>
            <SectionHeader
              icon={SlidersHorizontal}
              title="Tùy chọn trên thiết bị này"
              hint="Được lưu trên trình duyệt hiện tại"
            />
            <div className={cn(ds.sectionBody, 'flex flex-1 flex-col gap-3')}>
              <div className="grid gap-3">
              <PreferenceToggle
                icon={Volume2}
                checked={preferences.autoReadQuestions}
                title="Tự động đọc câu hỏi"
                description="Tự phát giọng đọc khi mở câu mới trong ca kiểm tra và thi thử."
                onChange={(checked) => changePreference('autoReadQuestions', checked)}
              />
              <PreferenceToggle
                icon={LogOut}
                checked={preferences.confirmBeforeLeavingMockExam}
                title="Xác nhận trước khi rời phòng thi thử"
                description="Hiển thị hộp thoại xác nhận trước khi tạm dừng và quay về phòng chờ."
                onChange={(checked) => changePreference('confirmBeforeLeavingMockExam', checked)}
              />
              </div>
              <p className="mt-auto px-1 text-[11px] leading-relaxed text-[#718096]">
                Các tùy chọn này không đồng bộ sang thiết bị khác và sẽ mất khi xóa dữ liệu trình duyệt.
              </p>
            </div>
          </section>
        )}
      </div>
      {passwordOpen && <ChangePasswordPage onClose={() => setPasswordOpen(false)} />}
    </div>
  )
}
