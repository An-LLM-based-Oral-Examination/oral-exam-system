import { lazy, Suspense, useEffect, useRef, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  ArrowRight,
  AudioLines,
  BrainCircuit,
  ChartNoAxesCombined,
  ChevronDown,
  CircleHelp,
  Eye,
  EyeOff,
  LoaderCircle,
  LockKeyhole,
  ShieldCheck,
  UserRound,
} from 'lucide-react'
import universityTechLogo from '@/assets/university-tech-logo.png'
import { useAuth } from '@/hooks/useAuth'
import type { ProblemDetails } from '@/types/auth.types'
import './LoginPage.css'

declare global {
  interface Window {
    google?: {
      accounts: {
        id: {
          initialize: (options: { client_id: string; callback: (response: { credential: string }) => void }) => void
          renderButton: (element: HTMLElement, options: Record<string, string | number>) => void
          cancel: () => void
        }
      }
    }
  }
}
const LoginThreeScene = lazy(() =>
  import('./LoginThreeScene').then((module) => ({ default: module.LoginThreeScene })),
)

const DEFAULT_LOGIN_ERROR = 'Đăng nhập thất bại. Vui lòng kiểm tra lại thông tin.'

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

function getLoginErrorMessage(error: unknown): string {
  if (!error || typeof error !== 'object') return DEFAULT_LOGIN_ERROR

  const problem = error as Partial<ProblemDetails>
  return problem.detail || problem.title || DEFAULT_LOGIN_ERROR
}

export function LoginPage() {
  const navigate = useNavigate()
  const { loginWithPassword, loginWithGoogle } = useAuth()
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const googleButtonRef = useRef<HTMLDivElement>(null)
  const [googleConfigured, setGoogleConfigured] = useState(Boolean(import.meta.env.VITE_GOOGLE_CLIENT_ID))

  const handlePasswordLogin = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const normalizedUsername = username.trim()

    if (!normalizedUsername || !password) {
      setError('Vui lòng nhập đầy đủ username và mật khẩu.')
      return
    }

    setIsLoading(true)
    setError(null)
    try {
      await loginWithPassword(normalizedUsername, password)
      navigate('/')
    } catch (loginError: unknown) {
      setError(getLoginErrorMessage(loginError))
    } finally {
      setIsLoading(false)
    }
  }

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
        width: '360',
        text: 'signin_with',
        shape: 'rectangular',
      })
      setGoogleConfigured(true)
    }

    if (window.google) {
      renderGoogleButton()
      return
    }

    const existingScript = document.querySelector<HTMLScriptElement>('script[data-google-identity]')
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

  return (
    <main className="login-page-shell min-h-dvh w-full bg-[#7868ef] p-0.5">
      <div className="grid min-h-[calc(100dvh-4px)] overflow-hidden rounded-[7px] bg-[#FFFFFF] lg:grid-cols-[58%_42%]">
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
            <div className="login-hero-badge mb-5 inline-flex items-center gap-2 rounded-full border border-[#3d8ccc]/25 bg-[#0b2744]/70 px-3 py-1.5 text-xs font-semibold tracking-[0.12em] text-[#87dffc] uppercase shadow-[0_0_18px_rgba(56,189,248,0.08)] backdrop-blur-md">
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
                  <p className={`text-xs font-bold tracking-[0.12em] uppercase ${accent}`}>
                    {title}
                  </p>
                  <p className="truncate text-xs text-[#8999b1]">{description}</p>
                </div>
              </div>
            ))}
          </div>

          <div className="absolute right-[7%] bottom-[3.6%] left-[7%] z-10 flex items-center gap-5 text-xs text-[#9aabc1]">
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
            <p className="pt-1 text-xs font-semibold tracking-[0.1em] text-[#70819a] uppercase">
              Department portal
            </p>

            <div className="flex flex-col items-end gap-1.5">
              <button
                type="button"
                className="login-status-button flex items-center gap-2 rounded-lg border border-[#4988C4]/45 bg-white/90 px-3 py-1.5 text-xs font-semibold text-[#67758a] shadow-sm transition hover:border-[#cbd7e8] hover:bg-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#2b5dde]"
                aria-label="Môi trường hiện tại: mô phỏng"
              >
                <span className="size-1.5 rounded-full bg-[#7aa9f4]" />
                Simulation Status
                <ChevronDown className="size-3" />
              </button>
              <span className="login-status-line flex items-center gap-1.5 pr-1 text-xs font-medium text-[#51667a]">
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
                <h2 className="text-[1.45rem] font-bold tracking-[-0.035em] text-[#0b1733] sm:text-lg">
                  Oral Exam System
                </h2>
                <p className="login-subtitle mt-1.5 text-sm text-[#738198]">
                  AI-powered Oral Examination Practice & Assessment
                </p>
              </div>

              <form onSubmit={handlePasswordLogin} className="space-y-3.5" noValidate>
                <div>
                  <label htmlFor="login-username" className="mb-1.5 block text-xs font-bold text-[#30415f]">
                    Username
                  </label>
                  <div className="relative">
                    <UserRound className="pointer-events-none absolute top-1/2 left-3.5 size-4 -translate-y-1/2 text-[#7f91ac]" />
                    <input
                      id="login-username"
                      name="username"
                      type="text"
                      value={username}
                      onChange={(event) => {
                        setUsername(event.target.value)
                        setError(null)
                      }}
                      autoComplete="username"
                      placeholder="Nhập username của bạn"
                      className="login-input min-h-11 w-full rounded-xl border border-[#cbd8ea] bg-white px-3.5 py-2.5 pl-10 text-sm text-[#1b2f50] outline-none transition placeholder:text-[#9aa8ba] focus:border-[#4e87df] focus:ring-4 focus:ring-[#4e87df]/10"
                      disabled={isLoading}
                      required
                    />
                  </div>
                </div>

                <div>
                  <label htmlFor="login-password" className="mb-1.5 block text-xs font-bold text-[#30415f]">
                    Mật khẩu
                  </label>
                  <div className="relative">
                    <LockKeyhole className="pointer-events-none absolute top-1/2 left-3.5 size-4 -translate-y-1/2 text-[#7f91ac]" />
                    <input
                      id="login-password"
                      name="password"
                      type={showPassword ? 'text' : 'password'}
                      value={password}
                      onChange={(event) => {
                        setPassword(event.target.value)
                        setError(null)
                      }}
                      autoComplete="current-password"
                      placeholder="Nhập mật khẩu"
                      className="login-password-input min-h-11 w-full rounded-xl border border-[#cbd8ea] bg-white px-11 py-2.5 text-sm text-[#1b2f50] outline-none transition placeholder:text-[#9aa8ba] focus:border-[#4e87df] focus:ring-4 focus:ring-[#4e87df]/10"
                      disabled={isLoading}
                      required
                    />
                    <button
                      type="button"
                      aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                      onClick={() => setShowPassword((visible) => !visible)}
                      className="absolute top-1/2 right-2.5 flex size-8 -translate-y-1/2 items-center justify-center rounded-lg text-[#6f82a0] transition hover:bg-[#edf3fb] hover:text-[#2b5dde] focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-[#2b5dde]"
                    >
                      {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                    </button>
                  </div>
                </div>

                <button
                  type="submit"
                  disabled={isLoading}
                  className="login-submit inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-xl bg-[#1c5fc4] px-4 text-sm font-bold text-white shadow-[0_10px_24px_rgba(28,95,196,0.22)] transition hover:-translate-y-0.5 hover:bg-[#164b9d] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#2b5dde] disabled:cursor-wait disabled:opacity-60 motion-reduce:transform-none"
                >
                  {isLoading ? (
                    <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                  ) : (
                    <ArrowRight className="size-4" />
                  )}
                  {isLoading ? 'Đang đăng nhập…' : 'Đăng nhập'}
                </button>
              </form>

              <div className="my-5 flex items-center gap-3" aria-hidden="true">
                <span className="h-px flex-1 bg-[#e3eaf3]" />
                <span className="text-[11px] font-semibold text-[#96a2b3]">HOẶC</span>
                <span className="h-px flex-1 bg-[#e3eaf3]" />
              </div>

              <div className="rounded-xl border border-[#4988C4]/45 bg-white/80 p-3.5 shadow-[0_4px_18px_rgba(30,64,112,0.04)]">
                <div className="mb-2.5 flex items-center justify-between gap-3">
                  <div>
                    <p className="text-sm font-semibold text-[#253653]">Đăng nhập bằng Google</p>
                    <p className="mt-0.5 text-xs text-[#7b8ba1]">Xác thực OAuth 2.0 theo tài khoản đã được cấp quyền.</p>
                  </div>
                  <ShieldCheck className="size-4 shrink-0 text-[#2f70d9]" />
                </div>
                <div ref={googleButtonRef} className="flex min-h-10 justify-center" />
                {isLoading && (
                  <p className="mt-2 text-center text-xs font-semibold text-[#4a6dcc]">
                    Đang xác thực tài khoản…
                  </p>
                )}
                {!googleConfigured && (
                  <p className="mt-2 text-center text-xs text-[#a16a16]">
                    Chưa cấu hình VITE_GOOGLE_CLIENT_ID cho môi trường này.
                  </p>
                )}
              </div>

              {error && (
                <div
                  role="alert"
                  className="login-alert mt-3 rounded-lg border border-red-200 bg-red-50 px-3.5 py-3 text-xs leading-relaxed text-red-700"
                >
                  {error}
                </div>
              )}

              <div className="login-security mt-5 border-t border-[#e5eaf1] pt-4 text-center">
                <p className="flex items-center justify-center gap-1.5 text-xs text-[#8794a7]">
                  <ShieldCheck className="size-3.5 text-[#6984b3]" />
                  Tài khoản được bảo vệ bằng xác thực phiên an toàn.
                </p>
                <p id="login-support" className="login-support mt-2 text-xs text-[#8794a7]">
                  Cần hỗ trợ? Liên hệ trợ giảng hoặc quản trị viên hệ thống.
                </p>
              </div>

            </div>
          </div>

          <footer className="login-footer shrink-0 px-6 pb-5 text-center text-xs text-[#9aa6b6] sm:px-10 sm:pb-7">
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
