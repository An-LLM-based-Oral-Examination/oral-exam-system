import { lazy, Suspense, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  AudioLines,
  BrainCircuit,
  ChartNoAxesCombined,
  ChevronDown,
  CircleHelp,
  Eye,
  EyeOff,
  KeyRound,
  LoaderCircle,
  LockKeyhole,
  LogIn,
  Mail,
  ShieldCheck,
} from 'lucide-react'
import universityTechLogo from '@/assets/university-tech-logo.png'
import { useAuth } from '@/hooks/useAuth'
import type { ProblemDetails } from '@/types/auth.types'
import './LoginPage.css'

const LoginThreeScene = lazy(() =>
  import('./LoginThreeScene').then((module) => ({ default: module.LoginThreeScene })),
)

type DemoRole = 'Student' | 'Instructor' | 'Admin'

const DEFAULT_LOGIN_ERROR = 'Đăng nhập thất bại. Vui lòng kiểm tra lại thông tin.'
const REMEMBERED_EMAIL_KEY = 'oral_exam_remembered_email'

const DEMO_ROLES: { role: DemoRole; label: string }[] = [
  { role: 'Student', label: 'Sinh viên' },
  { role: 'Instructor', label: 'Giảng viên' },
  { role: 'Admin', label: 'Quản trị viên' },
]

const FEATURE_ITEMS = [
  {
    title: 'Voice',
    description: 'Luyện nói & phát âm',
    icon: AudioLines,
    accent: 'text-[#70dfff]',
  },
  {
    title: 'LLM',
    description: 'Lập luận theo ngữ cảnh',
    icon: BrainCircuit,
    accent: 'text-[#9aa7ff]',
  },
  {
    title: 'Assessment',
    description: 'Phản hồi theo rubric',
    icon: ChartNoAxesCombined,
    accent: 'text-[#4ee7bd]',
  },
]

function getRememberedEmail() {
  try {
    return window.localStorage.getItem(REMEMBERED_EMAIL_KEY) ?? ''
  } catch {
    return ''
  }
}

function saveRememberedEmail(email: string | null) {
  try {
    if (email) {
      window.localStorage.setItem(REMEMBERED_EMAIL_KEY, email)
    } else {
      window.localStorage.removeItem(REMEMBERED_EMAIL_KEY)
    }
  } catch {
    // Login must still succeed when browser storage is unavailable.
  }
}

function getLoginErrorMessage(error: unknown): string {
  if (!error || typeof error !== 'object') return DEFAULT_LOGIN_ERROR

  const problem = error as Partial<ProblemDetails>
  return problem.detail || problem.title || DEFAULT_LOGIN_ERROR
}

export function LoginPage() {
  const navigate = useNavigate()
  const { login } = useAuth()
  const rememberedEmail = getRememberedEmail()
  const [email, setEmail] = useState(rememberedEmail)
  const [password, setPassword] = useState('')
  const [rememberEmail, setRememberEmail] = useState(Boolean(rememberedEmail))
  const [showPassword, setShowPassword] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleLogin = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setIsLoading(true)
    setError(null)

    try {
      await login({ email, password })
      saveRememberedEmail(rememberEmail ? email : null)
      navigate('/')
    } catch (loginError: unknown) {
      setError(getLoginErrorMessage(loginError))
    } finally {
      setIsLoading(false)
    }
  }

  const handleQuickLogin = (role: DemoRole) => {
    const mockUsers = {
      Student: {
        id: 'usr-stu-01',
        email: 'student@fpt.edu.vn',
        fullName: 'Nguyễn Văn Sinh Viên',
        role: 'Student' as const,
      },
      Instructor: {
        id: 'usr-ins-01',
        email: 'instructor@fpt.edu.vn',
        fullName: 'ThS. Nguyễn Thị Cẩm Hương',
        role: 'Instructor' as const,
      },
      Admin: {
        id: 'usr-adm-01',
        email: 'admin@fpt.edu.vn',
        fullName: 'Quản Trị Viên Hệ Thống',
        role: 'Admin' as const,
      },
    }

    sessionStorage.setItem('access_token', 'mock_dev_jwt_token')
    sessionStorage.setItem('user_profile', JSON.stringify(mockUsers[role]))
    window.location.assign('/')
  }

  return (
    <main className="login-page-shell min-h-dvh w-full bg-[#7868ef] p-0.5">
      <div className="grid min-h-[calc(100dvh-4px)] overflow-hidden rounded-[7px] bg-[#f8fafc] lg:grid-cols-[58%_42%]">
        <section
          className="relative hidden min-h-[calc(100dvh-4px)] overflow-hidden bg-[#071426] lg:block"
          aria-label="Giới thiệu hệ thống luyện thi vấn đáp"
        >
          <div
            className="absolute inset-0 opacity-70"
            style={{
              backgroundImage:
                'radial-gradient(circle at 18% 8%, rgba(45, 99, 196, 0.22), transparent 27%), radial-gradient(circle at 72% 73%, rgba(13, 79, 158, 0.16), transparent 35%)',
            }}
          />
          <div className="absolute inset-0 bg-[linear-gradient(180deg,rgba(5,16,38,0)_55%,rgba(5,15,35,0.84)_100%)]" />

          <Suspense fallback={null}>
            <LoginThreeScene />
          </Suspense>

          <div className="login-hero-copy pointer-events-none absolute top-[6.5%] left-[7.5%] z-10 max-w-[560px] pr-8">
            <div className="login-hero-badge mb-5 inline-flex items-center gap-2 rounded-full border border-[#3d8ccc]/25 bg-[#0b2744]/70 px-3 py-1.5 text-[10px] font-semibold tracking-[0.12em] text-[#87dffc] uppercase shadow-[0_0_18px_rgba(56,189,248,0.08)] backdrop-blur-md">
              <span className="h-1.5 w-1.5 rounded-full bg-[#50e1ff] shadow-[0_0_9px_#50e1ff]" />
              AI-powered oral examination
            </div>

            <h1 className="text-[clamp(2.75rem,4.25vw,4.5rem)] leading-[0.94] font-bold tracking-[-0.055em] text-white">
              Practice. Speak.
              <span className="block bg-gradient-to-r from-[#64d8ff] to-[#9ebeff] bg-clip-text text-transparent">
                Improve.
              </span>
            </h1>
            <p className="login-hero-description mt-5 max-w-[440px] text-[clamp(0.86rem,1.05vw,1rem)] leading-relaxed text-[#c4d0e2]">
              Build confidence for your Software Engineering oral examinations with AI-powered
              practice and feedback.
            </p>
          </div>

          <div className="absolute right-[6%] bottom-[8.8%] left-[6%] z-10 grid grid-cols-3 gap-2.5">
            {FEATURE_ITEMS.map(({ title, description, icon: Icon, accent }) => (
              <div
                key={title}
                className="login-feature-card flex min-w-0 items-center gap-2.5 rounded-lg border border-white/8 bg-[#0b1931]/72 px-3 py-2 shadow-[0_8px_28px_rgba(0,0,0,0.2)] backdrop-blur-md"
              >
                <div
                  className={`flex size-7 shrink-0 items-center justify-center rounded-md bg-white/5 ${accent}`}
                >
                  <Icon className="size-3.5" strokeWidth={1.8} />
                </div>
                <div className="min-w-0">
                  <p className={`text-[8px] font-bold tracking-[0.12em] uppercase ${accent}`}>
                    {title}
                  </p>
                  <p className="truncate text-[8px] text-[#8999b1]">{description}</p>
                </div>
              </div>
            ))}
          </div>

          <div className="absolute right-[7%] bottom-[3.6%] left-[7%] z-10 flex items-center gap-5 text-[9px] text-[#9aabc1]">
            <span className="flex items-center gap-1.5">
              <ShieldCheck className="size-3 text-[#72b9f4]" />
              Bảo vệ phiên đăng nhập
            </span>
            <span className="h-3 w-px bg-white/10" />
            <span className="flex items-center gap-1.5">
              <LockKeyhole className="size-3 text-[#54e2c0]" />
              Mã hóa TLS đầu cuối
            </span>
          </div>
        </section>

        <section className="relative flex min-h-[calc(100dvh-4px)] flex-col overflow-y-auto bg-[linear-gradient(145deg,#ffffff_0%,#f8fafe_58%,#f3f6fb_100%)]">
          <header className="login-header flex shrink-0 items-start justify-between gap-4 px-6 pt-5 sm:px-10 sm:pt-7">
            <p className="pt-1 text-[9px] font-semibold tracking-[0.1em] text-[#70819a] uppercase">
              Department portal
            </p>

            <div className="flex flex-col items-end gap-1.5">
              <button
                type="button"
                className="login-status-button flex items-center gap-2 rounded-lg border border-[#dce4ef] bg-white/90 px-3 py-1.5 text-[9px] font-semibold text-[#67758a] shadow-sm transition hover:border-[#cbd7e8] hover:bg-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#2b5dde]"
                aria-label="Môi trường hiện tại: mô phỏng"
              >
                <span className="size-1.5 rounded-full bg-[#7aa9f4]" />
                Simulation Status
                <ChevronDown className="size-3" />
              </button>
              <span className="login-status-line flex items-center gap-1.5 pr-1 text-[9px] font-medium text-[#51667a]">
                <span className="size-1.5 rounded-full bg-[#22c58b] shadow-[0_0_7px_rgba(34,197,139,0.55)]" />
                Gateway Active
              </span>
            </div>
          </header>

          <div className="login-form-region flex flex-1 items-center justify-center px-6 py-8 sm:px-10">
            <div className="w-full max-w-[410px]">
              <div className="login-form-intro mb-7 text-center">
                <div className="login-logo relative mx-auto mb-4 flex size-[104px] items-center justify-center">
                  <img
                    src={universityTechLogo}
                    alt="University Tech"
                    className="size-full object-contain"
                    draggable={false}
                  />
                </div>
                <h2 className="text-[1.45rem] font-bold tracking-[-0.035em] text-[#0b1733]">
                  Oral Exam System
                </h2>
                <p className="login-subtitle mt-1.5 text-[11px] text-[#738198]">
                  AI-powered Oral Examination Practice & Assessment
                </p>
              </div>

              <form onSubmit={handleLogin} className="flex flex-col gap-4">
                {error && (
                  <div
                    role="alert"
                    className="login-alert rounded-lg border border-red-200 bg-red-50 px-3.5 py-3 text-xs leading-relaxed text-red-700"
                  >
                    {error}
                  </div>
                )}

                <div className="flex flex-col gap-1.5">
                  <label htmlFor="login-email" className="text-[11px] font-semibold text-[#344258]">
                    Email FPT <span className="text-[#ef4e68]">*</span>
                  </label>
                  <div className="relative">
                    <Mail
                      className="pointer-events-none absolute top-1/2 left-3.5 size-4 -translate-y-1/2 text-[#97a5b8]"
                      strokeWidth={1.7}
                    />
                    <input
                      id="login-email"
                      name="email"
                      type="email"
                      value={email}
                      onChange={(event) => setEmail(event.target.value)}
                      placeholder="ten@fpt.edu.vn"
                      autoComplete="email"
                      required
                      disabled={isLoading}
                      className="login-input h-11 w-full rounded-lg border border-[#d7e0eb] bg-white pr-3.5 pl-10 text-[12px] text-[#15223a] shadow-[0_2px_8px_rgba(30,64,112,0.035)] transition outline-none placeholder:text-[#a7b1c0] hover:border-[#becbdd] focus:border-[#4a72de] focus:ring-3 focus:ring-[#4a72de]/12 disabled:cursor-not-allowed disabled:bg-slate-50"
                    />
                  </div>
                </div>

                <div className="flex flex-col gap-1.5">
                  <label
                    htmlFor="login-password"
                    className="text-[11px] font-semibold text-[#344258]"
                  >
                    Mật khẩu <span className="text-[#ef4e68]">*</span>
                  </label>
                  <div className="relative">
                    <KeyRound
                      className="pointer-events-none absolute top-1/2 left-3.5 size-4 -translate-y-1/2 text-[#97a5b8]"
                      strokeWidth={1.7}
                    />
                    <input
                      id="login-password"
                      name="password"
                      type={showPassword ? 'text' : 'password'}
                      value={password}
                      onChange={(event) => setPassword(event.target.value)}
                      placeholder="Nhập mật khẩu"
                      autoComplete="current-password"
                      required
                      disabled={isLoading}
                      className="login-input login-password-input h-11 w-full rounded-lg border border-[#d7e0eb] bg-white pr-11 pl-10 text-[12px] text-[#15223a] shadow-[0_2px_8px_rgba(30,64,112,0.035)] transition outline-none placeholder:text-[#a7b1c0] hover:border-[#becbdd] focus:border-[#4a72de] focus:ring-3 focus:ring-[#4a72de]/12 disabled:cursor-not-allowed disabled:bg-slate-50"
                    />
                    <button
                      type="button"
                      onClick={() => setShowPassword((isVisible) => !isVisible)}
                      disabled={isLoading}
                      className="absolute top-1/2 right-3 flex size-7 -translate-y-1/2 items-center justify-center rounded-md text-[#91a0b3] transition hover:bg-[#edf2f8] hover:text-[#445875] focus-visible:outline-2 focus-visible:outline-[#4a72de] disabled:cursor-not-allowed"
                      aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                      aria-pressed={showPassword}
                    >
                      {showPassword ? (
                        <EyeOff className="size-4" strokeWidth={1.7} />
                      ) : (
                        <Eye className="size-4" strokeWidth={1.7} />
                      )}
                    </button>
                  </div>
                </div>

                <div className="flex items-center justify-between gap-4 text-[10px]">
                  <label className="flex cursor-pointer items-center gap-2 text-[#64748b]">
                    <input
                      type="checkbox"
                      checked={rememberEmail}
                      onChange={(event) => setRememberEmail(event.target.checked)}
                      disabled={isLoading}
                      className="size-3.5 rounded border-[#cbd5e1] accent-[#2b5dde]"
                    />
                    Ghi nhớ email
                  </label>
                  <a
                    href="#login-support"
                    className="font-semibold text-[#2858d7] transition hover:text-[#1d47b7] hover:underline focus-visible:rounded focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#2b5dde]"
                  >
                    Quên mật khẩu?
                  </a>
                </div>

                <button
                  type="submit"
                  disabled={isLoading}
                  className="login-submit flex h-11 w-full items-center justify-center gap-2 rounded-lg bg-[#2859d8] px-4 text-[12px] font-semibold text-white shadow-[0_8px_20px_rgba(40,89,216,0.22)] transition hover:bg-[#204fc7] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#2859d8] disabled:cursor-not-allowed disabled:opacity-65"
                >
                  {isLoading ? (
                    <LoaderCircle className="size-4 animate-spin" aria-hidden="true" />
                  ) : (
                    <LogIn className="size-3.5" aria-hidden="true" />
                  )}
                  {isLoading ? 'Đang đăng nhập...' : 'Đăng nhập'}
                </button>
              </form>

              <div className="login-security mt-5 border-t border-[#e5eaf1] pt-4 text-center">
                <p className="flex items-center justify-center gap-1.5 text-[9px] text-[#8794a7]">
                  <ShieldCheck className="size-3.5 text-[#6984b3]" />
                  Tài khoản được bảo vệ bằng xác thực phiên an toàn.
                </p>
                <p id="login-support" className="login-support mt-2 text-[9px] text-[#8794a7]">
                  Cần hỗ trợ? Liên hệ trợ giảng hoặc quản trị viên hệ thống.
                </p>
              </div>

              <details className="login-demo group mt-4 rounded-lg border border-[#e0e6ef] bg-white/65 text-[10px] text-[#66758b]">
                <summary className="login-demo-summary flex cursor-pointer list-none items-center justify-center gap-1.5 px-3 py-2.5 font-semibold transition hover:text-[#3658a5] focus-visible:outline-2 focus-visible:outline-[#2b5dde] [&::-webkit-details-marker]:hidden">
                  Đăng nhập nhanh để kiểm thử
                  <ChevronDown className="size-3 transition-transform group-open:rotate-180" />
                </summary>
                <div className="login-demo-grid grid grid-cols-3 gap-2 border-t border-[#e6ebf2] p-2.5">
                  {DEMO_ROLES.map(({ role, label }) => (
                    <button
                      key={role}
                      type="button"
                      onClick={() => handleQuickLogin(role)}
                      className="login-demo-button rounded-md border border-[#d8e0ec] bg-white px-2 py-2 font-medium text-[#52637a] transition hover:border-[#93aae2] hover:bg-[#f4f7ff] hover:text-[#2856c4] focus-visible:outline-2 focus-visible:outline-[#2b5dde]"
                    >
                      {label}
                    </button>
                  ))}
                </div>
              </details>
            </div>
          </div>

          <footer className="login-footer shrink-0 px-6 pb-5 text-center text-[9px] text-[#9aa6b6] sm:px-10 sm:pb-7">
            <div className="login-footer-divider mb-3 h-px bg-[#e8ecf2]" />© 2026 Faculty of
            Computer Science & Engineering. All rights reserved.
          </footer>

          <div className="pointer-events-none absolute right-8 bottom-20 hidden size-24 rounded-full bg-[#6ea8ff]/6 blur-3xl sm:block" />
          <CircleHelp className="pointer-events-none absolute right-6 bottom-6 hidden size-3 text-[#b7c1cf] sm:block" />
        </section>
      </div>
    </main>
  )
}
