import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  Eye,
  EyeOff,
  KeyRound,
  LoaderCircle,
  LockKeyhole,
  LogOut,
  ShieldAlert,
  ShieldCheck,
  UserRound,
} from 'lucide-react'
import universityTechLogo from '@/assets/university-tech-logo.png'
import { FieldError, fieldInvalidClass } from '@/components/common/formField'
import { toast } from '@/components/common/toastStore'
import { useAuth } from '@/hooks/useAuth'
import { cn } from '@/lib/utils'
import type { ProblemDetails } from '@/types/auth.types'

function getChangePasswordErrorMessage(error: unknown): string {
  if (!error || typeof error !== 'object') return 'Đổi mật khẩu thất bại. Vui lòng kiểm tra lại.'
  const problem = error as Partial<ProblemDetails>
  return problem.detail || problem.title || 'Đổi mật khẩu thất bại. Vui lòng kiểm tra lại.'
}

export function ChangePasswordPage() {
  const navigate = useNavigate()
  const { user, changePassword, logout } = useAuth()
  const isForcedChange = Boolean(user?.mustChangePassword)

  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')

  const [showCurrentPassword, setShowCurrentPassword] = useState(false)
  const [showNewPassword, setShowNewPassword] = useState(false)
  const [showConfirmPassword, setShowConfirmPassword] = useState(false)

  const [currentPasswordError, setCurrentPasswordError] = useState<string | null>(null)
  const [newPasswordError, setNewPasswordError] = useState<string | null>(null)
  const [confirmPasswordError, setConfirmPasswordError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  // Password rules validation
  const hasMinLength = newPassword.length >= 8
  const hasUpper = /[A-Z]/.test(newPassword)
  const hasLower = /[a-z]/.test(newPassword)
  const hasNumber = /[0-9]/.test(newPassword)
  const hasSpecial = /[^a-zA-Z0-9]/.test(newPassword)

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()

    let hasError = false
    if (!currentPassword) {
      setCurrentPasswordError('Vui lòng nhập mật khẩu hiện tại.')
      hasError = true
    } else {
      setCurrentPasswordError(null)
    }

    if (!newPassword) {
      setNewPasswordError('Vui lòng nhập mật khẩu mới.')
      hasError = true
    } else if (newPassword.length < 8) {
      setNewPasswordError('Mật khẩu mới phải có tối thiểu 8 ký tự.')
      hasError = true
    } else if (newPassword === currentPassword) {
      setNewPasswordError('Mật khẩu mới không được trùng với mật khẩu hiện tại.')
      hasError = true
    } else {
      setNewPasswordError(null)
    }

    if (!confirmPassword) {
      setConfirmPasswordError('Vui lòng xác nhận mật khẩu mới.')
      hasError = true
    } else if (confirmPassword !== newPassword) {
      setConfirmPasswordError('Xác nhận mật khẩu không khớp với mật khẩu mới.')
      hasError = true
    } else {
      setConfirmPasswordError(null)
    }

    if (hasError) return

    setIsSubmitting(true)
    try {
      await changePassword(currentPassword, newPassword)
      toast.success('Đổi mật khẩu thành công! Mật khẩu mới đã được áp dụng.')
      navigate(user?.role === 'Student' ? '/student/overview' : '/', { replace: true })
    } catch (error) {
      toast.error(getChangePasswordErrorMessage(error))
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleBack = () => {
    navigate(user?.role === 'Student' ? '/student/overview' : '/', { replace: true })
  }

  const handleLogout = async () => {
    await logout()
    navigate('/login', { replace: true })
  }

  return (
    <div className="flex min-h-screen flex-col items-center justify-center bg-gradient-to-br from-[#0B1E3F] via-[#0F2854] to-[#1C4D8D] px-4 py-8 text-white">
      <div className="w-full max-w-md">
        {/* Branding header */}
        <div className="mb-6 flex flex-col items-center text-center">
          <img
            src={universityTechLogo}
            alt="University Logo"
            className="mb-3 h-14 w-auto object-contain drop-shadow"
          />
          <h1 className="text-xl font-bold tracking-tight text-white sm:text-2xl">
            {isForcedChange ? 'Đổi Mật Khẩu Lần Đầu' : 'Thay Đổi Mật Khẩu'}
          </h1>
          <p className="mt-1 text-xs text-[#a9caeb]">
            Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng AI (FA26SE166)
          </p>
        </div>

        {/* User Account Summary */}
        {user && (
          <div className="mb-4 flex items-center gap-3 rounded-2xl border border-[#82aedb]/35 bg-white/10 px-4 py-3 text-xs backdrop-blur-sm">
            <div className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-[#4988C4]/30 text-white">
              <UserRound className="size-4" />
            </div>
            <div className="min-w-0 flex-1">
              <p className="truncate font-bold text-white">
                {user.fullName}
                {user.studentCode ? ` · ${user.studentCode}` : ''}
              </p>
              <p className="truncate text-[#b8d4f2]">{user.email}</p>
            </div>
          </div>
        )}

        {/* Notice banner */}
        {isForcedChange ? (
          <div className="mb-6 rounded-2xl border border-[#fbbf24]/40 bg-[#b45309]/20 p-4 text-xs leading-relaxed text-[#fef08a] backdrop-blur-sm">
            <div className="flex items-start gap-2.5">
              <ShieldAlert className="size-5 shrink-0 text-[#f59e0b]" />
              <div>
                <p className="font-bold text-[#fef08a]">Yêu cầu bảo mật bắt buộc</p>
                <p className="mt-1 text-[#fef3c7]">
                  Tài khoản của bạn ({user?.email}) được tạo với mật khẩu tạm thời. Để bảo vệ dữ liệu cá nhân và tuân thủ quy chế khảo thí, bạn bắt buộc phải thiết lập mật khẩu mới trước khi tiếp tục.
                </p>
              </div>
            </div>
          </div>
        ) : (
          <div className="mb-6 rounded-2xl border border-[#82aedb]/30 bg-white/5 p-4 text-xs leading-relaxed text-[#d4e4f7] backdrop-blur-sm">
            <div className="flex items-start gap-2.5">
              <KeyRound className="size-5 shrink-0 text-[#82aedb]" />
              <div>
                <p className="font-bold text-white">Quản lý bảo mật tài khoản</p>
                <p className="mt-1 text-[#b8d4f2]">
                  Mật khẩu mới cần dài tối thiểu 8 ký tự, khác mật khẩu hiện tại; khuyến nghị kết hợp chữ hoa, chữ thường, chữ số và ký tự đặc biệt.
                </p>
              </div>
            </div>
          </div>
        )}

        {/* Card Form */}
        <div className="rounded-3xl border border-[#82aedb]/40 bg-[#0F2854]/90 p-6 shadow-2xl backdrop-blur-md sm:p-8">
          <form onSubmit={handleSubmit} className="space-y-4">
            {/* Current Password */}
            <div>
              <label
                htmlFor="current-password"
                className="block text-xs font-semibold uppercase tracking-wider text-[#d4e4f7]"
              >
                Mật khẩu hiện tại
              </label>
              <div className="relative mt-1.5">
                <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3.5 text-[#82aedb]">
                  <LockKeyhole className="size-4" />
                </div>
                <input
                  id="current-password"
                  type={showCurrentPassword ? 'text' : 'password'}
                  value={currentPassword}
                  onChange={(event) => {
                    setCurrentPassword(event.target.value)
                    if (currentPasswordError) setCurrentPasswordError(null)
                  }}
                  autoComplete="current-password"
                  placeholder="Nhập mật khẩu hiện tại"
                  className={cn(
                    'block w-full rounded-xl border border-[#82aedb]/40 bg-white/10 py-2.5 pr-10 pl-10 text-sm text-white placeholder-[#82aedb]/60 transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/40',
                    currentPasswordError && fieldInvalidClass,
                  )}
                />
                <button
                  type="button"
                  onClick={() => setShowCurrentPassword((prev) => !prev)}
                  className="absolute inset-y-0 right-0 flex items-center pr-3 text-[#82aedb] transition hover:text-white"
                  aria-label={showCurrentPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                >
                  {showCurrentPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                </button>
              </div>
              <FieldError message={currentPasswordError} />
            </div>

            {/* New Password */}
            <div>
              <label
                htmlFor="new-password"
                className="block text-xs font-semibold uppercase tracking-wider text-[#d4e4f7]"
              >
                Mật khẩu mới
              </label>
              <div className="relative mt-1.5">
                <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3.5 text-[#82aedb]">
                  <KeyRound className="size-4" />
                </div>
                <input
                  id="new-password"
                  type={showNewPassword ? 'text' : 'password'}
                  value={newPassword}
                  onChange={(event) => {
                    setNewPassword(event.target.value)
                    if (newPasswordError) setNewPasswordError(null)
                  }}
                  autoComplete="new-password"
                  placeholder="Nhập mật khẩu mới (tối thiểu 8 ký tự)"
                  className={cn(
                    'block w-full rounded-xl border border-[#82aedb]/40 bg-white/10 py-2.5 pr-10 pl-10 text-sm text-white placeholder-[#82aedb]/60 transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/40',
                    newPasswordError && fieldInvalidClass,
                  )}
                />
                <button
                  type="button"
                  onClick={() => setShowNewPassword((prev) => !prev)}
                  className="absolute inset-y-0 right-0 flex items-center pr-3 text-[#82aedb] transition hover:text-white"
                  aria-label={showNewPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                >
                  {showNewPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                </button>
              </div>
              <FieldError message={newPasswordError} />

              {/* Password strength checklist */}
              {newPassword.length > 0 && (
                <div className="mt-2.5 grid grid-cols-2 gap-1.5 rounded-xl border border-white/10 bg-black/20 p-2.5 text-[11px]">
                  <span className={cn('flex items-center gap-1.5', hasMinLength ? 'text-[#34d399]' : 'text-white/50')}>
                    {hasMinLength ? <CheckCircle2 className="size-3 shrink-0" /> : <AlertCircle className="size-3 shrink-0" />}
                    Tối thiểu 8 ký tự
                  </span>
                  <span className={cn('flex items-center gap-1.5', hasUpper ? 'text-[#34d399]' : 'text-white/50')}>
                    {hasUpper ? <CheckCircle2 className="size-3 shrink-0" /> : <AlertCircle className="size-3 shrink-0" />}
                    Chữ in hoa (A-Z)
                  </span>
                  <span className={cn('flex items-center gap-1.5', hasLower ? 'text-[#34d399]' : 'text-white/50')}>
                    {hasLower ? <CheckCircle2 className="size-3 shrink-0" /> : <AlertCircle className="size-3 shrink-0" />}
                    Chữ thường (a-z)
                  </span>
                  <span className={cn('flex items-center gap-1.5', hasNumber ? 'text-[#34d399]' : 'text-white/50')}>
                    {hasNumber ? <CheckCircle2 className="size-3 shrink-0" /> : <AlertCircle className="size-3 shrink-0" />}
                    Chữ số (0-9)
                  </span>
                  <span className={cn('col-span-2 flex items-center gap-1.5', hasSpecial ? 'text-[#34d399]' : 'text-white/50')}>
                    {hasSpecial ? <CheckCircle2 className="size-3 shrink-0" /> : <AlertCircle className="size-3 shrink-0" />}
                    Ký tự đặc biệt (!@#$%...)
                  </span>
                </div>
              )}
            </div>

            {/* Confirm New Password */}
            <div>
              <label
                htmlFor="confirm-password"
                className="block text-xs font-semibold uppercase tracking-wider text-[#d4e4f7]"
              >
                Xác nhận mật khẩu mới
              </label>
              <div className="relative mt-1.5">
                <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3.5 text-[#82aedb]">
                  <ShieldCheck className="size-4" />
                </div>
                <input
                  id="confirm-password"
                  type={showConfirmPassword ? 'text' : 'password'}
                  value={confirmPassword}
                  onChange={(event) => {
                    setConfirmPassword(event.target.value)
                    if (confirmPasswordError) setConfirmPasswordError(null)
                  }}
                  autoComplete="new-password"
                  placeholder="Nhập lại mật khẩu mới"
                  className={cn(
                    'block w-full rounded-xl border border-[#82aedb]/40 bg-white/10 py-2.5 pr-10 pl-10 text-sm text-white placeholder-[#82aedb]/60 transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/40',
                    confirmPasswordError && fieldInvalidClass,
                  )}
                />
                <button
                  type="button"
                  onClick={() => setShowConfirmPassword((prev) => !prev)}
                  className="absolute inset-y-0 right-0 flex items-center pr-3 text-[#82aedb] transition hover:text-white"
                  aria-label={showConfirmPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                >
                  {showConfirmPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                </button>
              </div>
              <FieldError message={confirmPasswordError} />
            </div>

            {/* Submit button */}
            <button
              type="submit"
              disabled={isSubmitting}
              className="mt-6 flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-[#4988C4] to-[#1C4D8D] py-3 text-sm font-bold text-white shadow-lg transition duration-200 hover:brightness-110 focus-visible:outline-2 focus-visible:outline-white disabled:cursor-not-allowed disabled:opacity-50"
            >
              {isSubmitting ? (
                <>
                  <LoaderCircle className="size-4 animate-spin" />
                  Đang cập nhật mật khẩu...
                </>
              ) : (
                'Xác Nhận Đổi Mật Khẩu'
              )}
            </button>
          </form>

          {/* Back / Logout actions */}
          <div className="mt-6 flex flex-wrap items-center justify-center gap-4 border-t border-white/10 pt-4 text-center">
            {!isForcedChange && (
              <button
                type="button"
                onClick={handleBack}
                className="inline-flex items-center gap-1.5 text-xs font-semibold text-[#b8d4f2] transition hover:text-white"
              >
                <ArrowLeft className="size-3.5" />
                {user?.role === 'Student' ? 'Quay lại Cổng sinh viên' : 'Quay lại trang chủ'}
              </button>
            )}
            <button
              type="button"
              onClick={handleLogout}
              className="inline-flex items-center gap-1.5 text-xs font-semibold text-[#82aedb] transition hover:text-white"
            >
              <LogOut className="size-3.5" />
              Đăng xuất khỏi tài khoản
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
