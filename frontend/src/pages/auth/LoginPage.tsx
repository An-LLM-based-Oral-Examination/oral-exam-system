import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  CheckCircle2,
  Eye,
  EyeOff,
  HelpCircle,
  Lock,
  LogIn,
  Mail,
  ShieldCheck,
  User,
  UserCheck,
} from 'lucide-react'
import universityTechLogo from '@/assets/university-tech-logo.png'
import { OverlayModal } from '@/components/ui/OverlayDrawer'
import { useAuth } from '@/hooks/useAuth'
import type { ProblemDetails, UserRole } from '@/types/auth.types'
import './LoginPage.css'

declare global {
  interface Window {
    google?: {
      accounts: {
        id: {
          initialize: (options: {
            client_id: string
            callback: (response: { credential: string }) => void
          }) => void
          renderButton: (element: HTMLElement, options: Record<string, string | number>) => void
          cancel: () => void
        }
      }
    }
  }
}

const DEFAULT_LOGIN_ERROR = 'Đăng nhập thất bại. Vui lòng kiểm tra lại thông tin tài khoản.'

interface GoogleAccountOption {
  label: string
  sublabel: string
  role: UserRole
  email: string
  username: string
  defaultPassword: string
  fullName: string
}

const GOOGLE_ROLE_ACCOUNTS: GoogleAccountOption[] = [
  {
    label: 'Sinh viên (thanhnqse184236@fpt.edu.vn)',
    sublabel: 'Nguyễn Quang Thành · Username: thanhnqse184236 · MSSV: SE184236',
    role: 'Student',
    email: 'thanhnqse184236@fpt.edu.vn',
    username: 'thanhnqse184236',
    defaultPassword: 'ChangeMe-SE184236!',
    fullName: 'Nguyễn Quang Thành',
  },
  {
    label: 'Sinh viên (hoanglvse170001@fpt.edu.vn)',
    sublabel: 'Lê Vũ Hoàng · Username: hoalvse170001 · Có lịch thi Phòng Lab 301',
    role: 'Student',
    email: 'hoanglvse170001@fpt.edu.vn',
    username: 'hoalvse170001',
    defaultPassword: 'ChangeMe-SE170001!',
    fullName: 'Lê Vũ Hoàng',
  },
  {
    label: 'Giảng viên (totnt@fe.edu.vn)',
    sublabel: 'Nguyễn Trọng Tốt · Username: totnt · Soạn đề & Chấm thi vấn đáp',
    role: 'Instructor',
    email: 'totnt@fe.edu.vn',
    username: 'totnt',
    defaultPassword: 'ChangeMe-Lecturer!',
    fullName: 'Nguyễn Trọng Tốt',
  },
  {
    label: 'Trưởng Bộ môn (headse@fe.edu.vn)',
    sublabel: 'Trần Văn Trưởng · Username: headse · Duyệt đề, Cấu hình môn & Phúc khảo',
    role: 'DepartmentHead',
    email: 'headse@fe.edu.vn',
    username: 'headse',
    defaultPassword: 'ChangeMe-Head!',
    fullName: 'Trần Văn Trưởng',
  },
  {
    label: 'Cán bộ Coi thi (haind@fe.edu.vn)',
    sublabel: 'Nguyễn Đăng Hải · Username: haind · Điểm danh thí sinh & Giám sát phòng máy',
    role: 'Proctor',
    email: 'haind@fe.edu.vn',
    username: 'haind',
    defaultPassword: 'ChangeMe-Proctor!',
    fullName: 'Nguyễn Đăng Hải',
  },
  {
    label: 'Quản trị hệ thống (admin@fpt.edu.vn)',
    sublabel: 'Nguyễn Quang Thành · Username: admin · Quản lý tài khoản, Học kỳ & Cấu hình',
    role: 'Admin',
    email: 'admin@fpt.edu.vn',
    username: 'admin',
    defaultPassword: 'ChangeMe-Admin!',
    fullName: 'Nguyễn Quang Thành',
  },
]

function getLoginErrorMessage(error: unknown): string {
  if (!error || typeof error !== 'object') return DEFAULT_LOGIN_ERROR

  const problem = error as Partial<ProblemDetails>
  return problem.detail || problem.title || DEFAULT_LOGIN_ERROR
}

export function LoginPage() {
  const navigate = useNavigate()
  const { loginWithGoogle, loginWithPassword, changePassword } = useAuth()
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [googleEmail, setGoogleEmail] = useState('')
  const [googleModalOpen, setGoogleModalOpen] = useState(false)
  const [gsiLoaded, setGsiLoaded] = useState(false)
  const [roleModalOpen, setRoleModalOpen] = useState(false)
  const [originHelpModalOpen, setOriginHelpModalOpen] = useState(false)
  const [changePasswordModalOpen, setChangePasswordModalOpen] = useState(false)
  const [currentPasswordInput, setCurrentPasswordInput] = useState('')
  const [newPasswordInput, setNewPasswordInput] = useState('')
  const [confirmPasswordInput, setConfirmPasswordInput] = useState('')
  const [changePasswordError, setChangePasswordError] = useState<string | null>(null)
  const googleButtonRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined
    if (!clientId || !googleButtonRef.current) return

    const renderGoogleButton = () => {
      if (!window.google || !googleButtonRef.current) return
      window.google.accounts.id.initialize({
        client_id: clientId,
        callback: ({ credential }) => {
          setIsLoading(true)
          setError(null)
          void loginWithGoogle(credential)
            .then(() => navigate('/'))
            .catch((loginError: unknown) => setError(getLoginErrorMessage(loginError)))
            .finally(() => setIsLoading(false))
        },
      })
      googleButtonRef.current.replaceChildren()
      window.google.accounts.id.renderButton(googleButtonRef.current, {
        theme: 'outline',
        size: 'large',
        width: '380',
        text: 'continue_with',
        shape: 'rectangular',
      })
      setGsiLoaded(true)
    }

    if (window.google) {
      renderGoogleButton()
      return
    }

    const existingScript = document.querySelector<HTMLScriptElement>(
      'script[data-google-identity]',
    )
    const script = existingScript ?? document.createElement('script')
    if (!existingScript) {
      script.src = 'https://accounts.google.com/gsi/client'
      script.async = true
      script.defer = true
      script.dataset.googleIdentity = 'true'
      document.head.appendChild(script)
    }
    script.addEventListener('load', renderGoogleButton)
    return () => script.removeEventListener('load', renderGoogleButton)
  }, [loginWithGoogle, navigate])

  const executeGoogleEmailSignIn = async (emailInput: string, fullNameHint?: string) => {
    const trimmedEmail = emailInput.trim().toLowerCase()
    if (!trimmedEmail || !trimmedEmail.includes('@')) {
      setError('Vui lòng nhập địa chỉ email Google hợp lệ (ví dụ: sinhvien@fpt.edu.vn).')
      return
    }

    const matchedAccount = GOOGLE_ROLE_ACCOUNTS.find(
      (item) => item.email.toLowerCase() === trimmedEmail,
    )
    const resolvedName = fullNameHint || matchedAccount?.fullName || 'Sinh viên FPT'

    setIsLoading(true)
    setError(null)
    try {
      await loginWithGoogle(`dev-google:${trimmedEmail}|${resolvedName}`)
      navigate('/')
    } catch (loginError: unknown) {
      setError(getLoginErrorMessage(loginError))
    } finally {
      setIsLoading(false)
    }
  }

  const handleDirectGoogleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setGoogleModalOpen(false)
    void executeGoogleEmailSignIn(googleEmail)
  }

  const handlePasswordLoginSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const trimmedUser = username.trim()
    if (!trimmedUser || !password) {
      setError('Vui lòng nhập đầy đủ Tên đăng nhập và Mật khẩu.')
      return
    }

    setIsLoading(true)
    setError(null)
    try {
      await loginWithPassword(trimmedUser, password)
      const savedProfileRaw = sessionStorage.getItem('user_profile')
      const savedProfile = savedProfileRaw
        ? (JSON.parse(savedProfileRaw) as { mustChangePassword?: boolean })
        : null
      if (savedProfile?.mustChangePassword) {
        setCurrentPasswordInput(password)
        setNewPasswordInput('')
        setConfirmPasswordInput('')
        setChangePasswordError(null)
        setChangePasswordModalOpen(true)
        return
      }
      navigate('/')
    } catch (loginError: unknown) {
      setError(getLoginErrorMessage(loginError))
    } finally {
      setIsLoading(false)
    }
  }

  const handleChangePasswordSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!currentPasswordInput || !newPasswordInput) {
      setChangePasswordError('Vui lòng nhập đầy đủ mật khẩu hiện tại và mật khẩu mới.')
      return
    }
    if (newPasswordInput.length < 8) {
      setChangePasswordError('Mật khẩu mới phải có tối thiểu 8 ký tự.')
      return
    }
    if (newPasswordInput !== confirmPasswordInput) {
      setChangePasswordError('Mật khẩu xác nhận không khớp với mật khẩu mới.')
      return
    }

    setIsLoading(true)
    setChangePasswordError(null)
    try {
      await changePassword(currentPasswordInput, newPasswordInput)
      setChangePasswordModalOpen(false)
      navigate('/')
    } catch (changeError: unknown) {
      setChangePasswordError(getLoginErrorMessage(changeError))
    } finally {
      setIsLoading(false)
    }
  }

  const handleQuickGoogleRole = (account: GoogleAccountOption) => {
    setRoleModalOpen(false)
    setUsername(account.username)
    setPassword(account.defaultPassword)
    void executeGoogleEmailSignIn(account.email, account.fullName)
  }

  return (
    <main className="login-page-shell flex min-h-dvh w-full flex-col items-center justify-center px-4 py-8 text-[#1E293B]">
      <div className="w-full max-w-4xl overflow-hidden rounded-3xl border border-[#FED7AA] bg-white shadow-xl">
        <div className="grid grid-cols-1 md:grid-cols-12">
          {/* Cột trái: Thương hiệu FPT University & Hệ thống Vấn đáp AI */}
          <section className="flex flex-col justify-between border-b border-[#FED7AA] bg-gradient-to-br from-[#FFF7ED] via-[#FFFAF5] to-[#FFEDD5]/40 p-7 sm:p-9 md:col-span-6 md:border-r md:border-b-0">
            <div>
              {/* Header thương hiệu */}
              <div className="flex items-center gap-3">
                <div className="flex size-11 items-center justify-center rounded-2xl border border-[#FED7AA] bg-white p-2 shadow-xs">
                  <img
                    src={universityTechLogo}
                    alt="FPT University"
                    className="size-full object-contain"
                    draggable={false}
                  />
                </div>
                <div>
                  <span className="inline-flex items-center gap-1 rounded-full border border-[#FED7AA] bg-white px-2.5 py-0.5 text-xs font-bold text-[#C2410C]">
                    FPT University
                  </span>
                  <h1 className="mt-0.5 text-base font-bold tracking-tight text-[#1E293B]">
                    OralExam AI Portal
                  </h1>
                </div>
              </div>

              {/* Sóng âm thanh biểu trưng Viva Voce */}
              <div className="my-6 flex flex-col items-center justify-center rounded-2xl border border-[#FED7AA]/70 bg-white/70 p-6 text-center shadow-2xs backdrop-blur-xs">
                <div className="flex items-center justify-center gap-1.5">
                  <span className="h-3 w-1 rounded-full bg-[#F26F21]/40" />
                  <span className="h-6 w-1 rounded-full bg-[#F26F21]/70" />
                  <span className="h-10 w-1.5 rounded-full bg-[#F26F21]" />
                  <span className="h-14 w-2 rounded-full bg-[#EA580C]" />
                  <span className="h-8 w-1.5 rounded-full bg-[#F26F21]" />
                  <span className="h-12 w-2 rounded-full bg-[#EA580C]" />
                  <span className="h-6 w-1.5 rounded-full bg-[#F26F21]" />
                  <span className="h-10 w-1.5 rounded-full bg-[#F26F21]/80" />
                  <span className="h-5 w-1 rounded-full bg-[#F26F21]/60" />
                  <span className="h-3 w-1 rounded-full bg-[#F26F21]/40" />
                </div>

                <h2 className="mt-4 text-sm font-bold text-[#1E293B]">
                  Khảo thí &amp; Đánh giá Vấn đáp AI
                </h2>
                <p className="mt-1 text-xs text-[#64748B]">
                  Tương tác giọng nói trực tiếp theo chuẩn CLO môn học
                </p>
              </div>

              {/* 3 tính năng cốt lõi tinh gọn */}
              <div className="space-y-2">
                <div className="flex items-center gap-3 rounded-xl border border-[#FED7AA]/60 bg-white/90 px-3.5 py-2.5 shadow-2xs">
                  <div className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-[#FFF7ED] text-[#F26F21]">
                    <ShieldCheck className="size-4" />
                  </div>
                  <p className="text-xs font-medium text-[#475569]">
                    <strong className="font-bold text-[#1E293B]">Luyện tập tự do:</strong> Giám khảo AI phản hồi tức thì
                  </p>
                </div>

                <div className="flex items-center gap-3 rounded-xl border border-[#FED7AA]/60 bg-white/90 px-3.5 py-2.5 shadow-2xs">
                  <div className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-[#FFF7ED] text-[#F26F21]">
                    <UserCheck className="size-4" />
                  </div>
                  <p className="text-xs font-medium text-[#475569]">
                    <strong className="font-bold text-[#1E293B]">Thi thử tính giờ:</strong> Bốc đề ngẫu nhiên theo thang 10
                  </p>
                </div>

                <div className="flex items-center gap-3 rounded-xl border border-[#FED7AA]/60 bg-white/90 px-3.5 py-2.5 shadow-2xs">
                  <div className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-[#FFF7ED] text-[#F26F21]">
                    <CheckCircle2 className="size-4" />
                  </div>
                  <p className="text-xs font-medium text-[#475569]">
                    <strong className="font-bold text-[#1E293B]">Kỳ thi chính thức:</strong> Tổ chức tập trung tại phòng máy
                  </p>
                </div>
              </div>
            </div>

            {/* Chân trang cột trái */}
            <div className="mt-6 border-t border-[#FED7AA]/80 pt-3 text-center text-xs text-[#64748B]">
              Khối ngành Kỹ thuật Phần mềm · <span className="font-medium text-[#1E293B]">Đại học FPT</span>
            </div>
          </section>

          {/* Cột phải: Form Đăng nhập Tối giản chuẩn Enterprise */}
          <section className="flex flex-col justify-between p-7 sm:p-9 md:col-span-6">
            <div>
              <div className="pb-2">
                <div className="flex items-center justify-between">
                  <h2 className="text-2xl font-bold tracking-tight text-[#1E293B]">
                    Đăng nhập
                  </h2>
                  <span className="inline-flex items-center gap-1.5 rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-0.5 text-xs font-bold text-[#16A34A]">
                    <span className="size-1.5 rounded-full bg-[#16A34A]" />
                    FPTU Portal
                  </span>
                </div>
                <p className="mt-1 text-xs text-[#64748B]">
                  Nhập thông tin tài khoản để truy cập hệ thống
                </p>
              </div>

              {error && (
                <div
                  role="alert"
                  className="mt-3 rounded-xl border border-red-200 bg-red-50 px-3.5 py-2.5 text-xs font-medium text-[#E11D48]"
                >
                  {error}
                </div>
              )}

              {/* Form Đăng nhập Username & Password */}
              <form onSubmit={handlePasswordLoginSubmit} className="mt-5 space-y-3.5">
                <div>
                  <label
                    htmlFor="provisioned-username"
                    className="block text-xs font-semibold text-[#1E293B]"
                  >
                    Tên đăng nhập / MSSV
                  </label>
                  <div className="relative mt-1.5">
                    <User className="pointer-events-none absolute top-1/2 left-3.5 size-4 -translate-y-1/2 text-[#94A3B8]" />
                    <input
                      id="provisioned-username"
                      type="text"
                      value={username}
                      onChange={(event) => setUsername(event.target.value)}
                      placeholder="MSSV hoặc Tên đăng nhập"
                      autoComplete="username"
                      className="w-full rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] py-2.5 pr-3.5 pl-10 text-sm font-medium text-[#1E293B] placeholder:text-[#94A3B8] transition focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#F26F21]/20 focus:outline-none"
                    />
                  </div>
                </div>

                <div>
                  <label
                    htmlFor="provisioned-password"
                    className="block text-xs font-semibold text-[#1E293B]"
                  >
                    Mật khẩu
                  </label>
                  <div className="relative mt-1.5">
                    <Lock className="pointer-events-none absolute top-1/2 left-3.5 size-4 -translate-y-1/2 text-[#94A3B8]" />
                    <input
                      id="provisioned-password"
                      type={showPassword ? 'text' : 'password'}
                      value={password}
                      onChange={(event) => setPassword(event.target.value)}
                      placeholder="Mật khẩu"
                      autoComplete="current-password"
                      className="w-full rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] py-2.5 pr-10 pl-10 text-sm font-medium text-[#1E293B] placeholder:text-[#94A3B8] transition focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#F26F21]/20 focus:outline-none"
                    />
                    <button
                      type="button"
                      onClick={() => setShowPassword(!showPassword)}
                      tabIndex={-1}
                      aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                      className="absolute top-1/2 right-3 -translate-y-1/2 text-[#94A3B8] hover:text-[#475569] cursor-pointer"
                    >
                      {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                    </button>
                  </div>
                </div>

                <button
                  type="submit"
                  disabled={isLoading}
                  className="inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-xl bg-[#F26F21] px-4 text-sm font-bold text-white shadow-xs transition hover:bg-[#D95B16] active:bg-[#C2410C] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] disabled:opacity-60 cursor-pointer"
                >
                  <LogIn className="size-4" />
                  {isLoading ? 'Đang đăng nhập...' : 'Đăng nhập'}
                </button>
              </form>

              {/* Dải phân cách */}
              <div className="relative my-4">
                <div className="absolute inset-0 flex items-center">
                  <div className="w-full border-t border-[#E2E8F0]" />
                </div>
                <div className="relative flex justify-center text-xs">
                  <span className="bg-white px-2.5 font-medium text-[#94A3B8]">
                    Hoặc tiếp tục với
                  </span>
                </div>
              </div>

              {/* 1 Nút Google Duy nhất chuẩn Brand */}
              <div>
                <div ref={googleButtonRef} className="flex min-h-10 justify-center empty:hidden" />

                {!gsiLoaded && (
                  <button
                    type="button"
                    disabled={isLoading}
                    onClick={() => setGoogleModalOpen(true)}
                    className="inline-flex min-h-11 w-full items-center justify-center gap-2.5 rounded-xl border border-[#E2E8F0] bg-white px-4 text-sm font-semibold text-[#1E293B] shadow-2xs transition hover:border-[#FED7AA] hover:bg-[#FFF7ED]/50 active:bg-[#FFEDD5]/40 cursor-pointer"
                  >
                    <svg className="size-4.5" viewBox="0 0 24 24">
                      <path
                        fill="#4285F4"
                        d="M23.745 12.27c0-.7-.06-1.4-.19-2.07H12v4.51h6.6c-.29 1.52-1.14 2.82-2.4 3.68v3.05h3.88c2.27-2.09 3.665-5.17 3.665-9.17z"
                      />
                      <path
                        fill="#34A853"
                        d="M12 24c3.24 0 5.95-1.08 7.93-2.91l-3.88-3.05c-1.08.72-2.45 1.16-4.05 1.16-3.12 0-5.77-2.1-6.72-4.93H1.25v3.15C3.26 21.36 7.34 24 12 24z"
                      />
                      <path
                        fill="#FBBC05"
                        d="M5.28 14.27c-.25-.72-.38-1.49-.38-2.27s.13-1.55.38-2.27V6.58H1.25C.45 8.18 0 9.99 0 12s.45 3.82 1.25 5.42l4.03-3.15z"
                      />
                      <path
                        fill="#EA4335"
                        d="M12 4.75c1.77 0 3.35.61 4.6 1.8l3.42-3.42C17.95 1.19 15.24 0 12 0 7.34 0 3.26 2.64 1.25 6.58l4.03 3.15c.95-2.83 3.6-4.98 6.72-4.98z"
                      />
                    </svg>
                    Tiếp tục với Google (@fpt.edu.vn)
                  </button>
                )}
              </div>

              {/* Chân form: 2 liên kết phụ tinh tế */}
              <div className="mt-4 flex items-center justify-between border-t border-[#F1F5F9] pt-3 text-xs">
                <button
                  type="button"
                  onClick={() => setOriginHelpModalOpen(true)}
                  className="inline-flex items-center gap-1 font-medium text-[#64748B] hover:text-[#C2410C] transition cursor-pointer"
                >
                  <HelpCircle className="size-3.5 text-[#F26F21]" />
                  Trợ giúp
                </button>

                <button
                  type="button"
                  onClick={() => setRoleModalOpen(true)}
                  className="inline-flex items-center gap-1 font-medium text-[#64748B] hover:text-[#C2410C] transition cursor-pointer"
                >
                  <UserCheck className="size-3.5 text-[#F26F21]" />
                  Tài khoản mẫu (Demo)
                </button>
              </div>
            </div>

            <p className="mt-5 border-t border-[#F1F5F9] pt-3 text-center text-xs text-stone-500">
              © 2026 Trường Đại học FPT · All rights reserved.
            </p>
          </section>
        </div>
      </div>

      {/* Modal: Đăng nhập Google FPT */}
      <OverlayModal
        open={googleModalOpen}
        onClose={() => setGoogleModalOpen(false)}
        badge="Google OAuth 2.0"
        title="Đăng nhập tài khoản Google FPT"
        subtitle="Sử dụng email @fpt.edu.vn hoặc @fe.edu.vn để truy cập:"
        size="sm"
      >
        <form onSubmit={handleDirectGoogleSubmit} className="space-y-3.5">
          <div>
            <label htmlFor="modal-google-email" className="block text-xs font-semibold text-[#1E293B]">
              Email Google FPT Education
            </label>
            <div className="relative mt-1.5">
              <Mail className="pointer-events-none absolute top-1/2 left-3.5 size-4 -translate-y-1/2 text-[#F26F21]" />
              <input
                id="modal-google-email"
                type="email"
                value={googleEmail}
                onChange={(e) => setGoogleEmail(e.target.value)}
                placeholder="sinhvien@fpt.edu.vn"
                autoComplete="email"
                className="w-full rounded-xl border border-[#FED7AA] bg-[#FFF7ED]/30 py-2.5 pr-3.5 pl-10 text-sm font-medium text-[#1E293B] placeholder:text-[#94A3B8] focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#F26F21]/20 focus:outline-none"
              />
            </div>
          </div>

          <button
            type="submit"
            disabled={isLoading}
            className="inline-flex min-h-10 w-full items-center justify-center gap-2 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white shadow-xs transition hover:bg-[#D95B16] disabled:opacity-60 cursor-pointer"
          >
            {isLoading ? 'Đang xác thực...' : 'Tiếp tục'}
          </button>
        </form>
      </OverlayModal>

      {/* Modal: Chuyển nhanh tài khoản theo vai trò (Dành cho Demo) */}
      <OverlayModal
        open={roleModalOpen}
        onClose={() => setRoleModalOpen(false)}
        badge="Tài khoản thử nghiệm"
        title="Chọn tài khoản trải nghiệm"
        subtitle="Chọn vai trò để kiểm thử các phân hệ của hệ thống:"
        size="md"
      >
        <div className="space-y-2.5">
          {GOOGLE_ROLE_ACCOUNTS.map((account) => (
            <button
              key={account.email}
              type="button"
              disabled={isLoading}
              onClick={() => handleQuickGoogleRole(account)}
              className="flex w-full items-center justify-between gap-3 rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3.5 text-left transition hover:border-[#F26F21] hover:bg-[#FFF7ED] focus-visible:outline-2 focus-visible:outline-[#F26F21] disabled:opacity-60 cursor-pointer"
            >
              <div className="min-w-0">
                <p className="truncate text-sm font-bold text-[#1E293B]">{account.label}</p>
                <p className="mt-0.5 text-xs text-[#64748B]">{account.sublabel}</p>
              </div>
              <span className="inline-flex shrink-0 items-center gap-1 rounded-lg bg-[#FFF7ED] px-2.5 py-1 text-xs font-bold text-[#C2410C] ring-1 ring-[#FED7AA]">
                <CheckCircle2 className="size-3.5 text-[#F26F21]" />
                Chọn
              </span>
            </button>
          ))}
        </div>
      </OverlayModal>

      {/* Modal: Hướng dẫn hỗ trợ đăng nhập */}
      <OverlayModal
        open={originHelpModalOpen}
        onClose={() => setOriginHelpModalOpen(false)}
        badge="Hỗ trợ đăng nhập"
        title="Hướng dẫn đăng nhập hệ thống"
        subtitle="Phương thức truy cập hợp lệ vào hệ thống Khảo thí Vấn đáp AI:"
        size="md"
      >
        <div className="space-y-3 text-xs leading-relaxed text-[#475569]">
          <div className="rounded-xl border border-[#FED7AA] bg-[#FFF7ED] p-3.5 text-[#9A3412]">
            <p className="font-bold text-[#C2410C]">
              1. Tài khoản do Nhà trường cấp:
            </p>
            <p className="mt-1">
              Sử dụng Tên đăng nhập (hoặc MSSV) cùng mật khẩu đã được phòng Đào tạo / Khảo thí cung cấp để đăng nhập trực tiếp.
            </p>
          </div>

          <div className="rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3.5">
            <p className="font-bold text-[#1E293B]">
              2. Đăng nhập bằng Google Education:
            </p>
            <p className="mt-1">
              Bấm nút <strong>&ldquo;Google (@fpt.edu.vn)&rdquo;</strong> để đăng nhập bằng tài khoản email chính thức của trường.
            </p>
          </div>
        </div>
      </OverlayModal>

      {/* Modal: Đổi mật khẩu bắt buộc cho tài khoản Nhà trường cấp lần đầu */}
      <OverlayModal
        open={changePasswordModalOpen}
        onClose={() => {
          setChangePasswordModalOpen(false)
          navigate('/')
        }}
        badge="Bảo mật tài khoản"
        title="Đổi mật khẩu tài khoản Nhà trường cấp"
        subtitle="Tài khoản của bạn đang sử dụng mật khẩu mặc định. Vui lòng cập nhật mật khẩu mới (tối thiểu 8 ký tự):"
        size="sm"
      >
        <form onSubmit={handleChangePasswordSubmit} className="space-y-3.5">
          {changePasswordError && (
            <div
              role="alert"
              className="rounded-xl border border-red-200 bg-red-50 px-3.5 py-2.5 text-xs font-medium text-[#E11D48]"
            >
              {changePasswordError}
            </div>
          )}

          <div>
            <label
              htmlFor="change-current-password"
              className="block text-xs font-semibold text-[#1E293B]"
            >
              Mật khẩu hiện tại
            </label>
            <input
              id="change-current-password"
              type="password"
              value={currentPasswordInput}
              onChange={(event) => setCurrentPasswordInput(event.target.value)}
              autoComplete="current-password"
              className="mt-1.5 w-full rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] px-3.5 py-2.5 text-sm font-medium text-[#1E293B] focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#F26F21]/20 focus:outline-none"
            />
          </div>

          <div>
            <label
              htmlFor="change-new-password"
              className="block text-xs font-semibold text-[#1E293B]"
            >
              Mật khẩu mới (tối thiểu 8 ký tự)
            </label>
            <input
              id="change-new-password"
              type="password"
              value={newPasswordInput}
              onChange={(event) => setNewPasswordInput(event.target.value)}
              autoComplete="new-password"
              placeholder="Nhập mật khẩu mới"
              className="mt-1.5 w-full rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] px-3.5 py-2.5 text-sm font-medium text-[#1E293B] focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#F26F21]/20 focus:outline-none"
            />
          </div>

          <div>
            <label
              htmlFor="change-confirm-password"
              className="block text-xs font-semibold text-[#1E293B]"
            >
              Xác nhận mật khẩu mới
            </label>
            <input
              id="change-confirm-password"
              type="password"
              value={confirmPasswordInput}
              onChange={(event) => setConfirmPasswordInput(event.target.value)}
              autoComplete="new-password"
              placeholder="Nhập lại mật khẩu mới"
              className="mt-1.5 w-full rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] px-3.5 py-2.5 text-sm font-medium text-[#1E293B] focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#F26F21]/20 focus:outline-none"
            />
          </div>

          <div className="flex items-center justify-end gap-2 pt-1">
            <button
              type="button"
              onClick={() => {
                setChangePasswordModalOpen(false)
                navigate('/')
              }}
              className="inline-flex min-h-10 items-center justify-center rounded-xl border border-[#E2E8F0] bg-white px-4 text-xs font-bold text-[#475569] transition hover:bg-[#F8FAFC] cursor-pointer"
            >
              Để sau
            </button>
            <button
              type="submit"
              disabled={isLoading}
              className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white shadow-xs transition hover:bg-[#D95B16] disabled:opacity-60 cursor-pointer"
            >
              {isLoading ? 'Đang cập nhật...' : 'Cập nhật mật khẩu'}
            </button>
          </div>
        </form>
      </OverlayModal>
    </main>
  )
}
