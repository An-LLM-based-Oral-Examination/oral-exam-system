import { lazy, Suspense, useEffect, useRef, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  ArrowRight,
  AudioLines,
  BrainCircuit,
  ChartNoAxesCombined,
  Eye,
  EyeOff,
  LoaderCircle,
  LockKeyhole,
  ShieldCheck,
  UserRound,
} from 'lucide-react'
import universityTechLogo from '@/assets/university-tech-logo.png'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass } from '@/components/common/formFieldClasses'
import { toast } from '@/components/common/toastStore'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import { useAuth } from '@/hooks/useAuth'
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
  return userMessage(error, DEFAULT_LOGIN_ERROR)
}

export function LoginPage() {
  const navigate = useNavigate()
  const { loginWithPassword, loginWithGoogle } = useAuth()
  const [isLoading, setIsLoading] = useState(false)
  const [usernameError, setUsernameError] = useState<string | null>(null)
  const [passwordError, setPasswordError] = useState<string | null>(null)
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const googleButtonRef = useRef<HTMLDivElement>(null)
  const [googleConfigured, setGoogleConfigured] = useState(Boolean(import.meta.env.VITE_GOOGLE_CLIENT_ID))

  const handlePasswordLogin = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const normalizedUsername = username.trim()

    const nextUsernameError = normalizedUsername ? null : 'Vui lòng nhập username.'
    const nextPasswordError = password ? null : 'Vui lòng nhập mật khẩu.'
    setUsernameError(nextUsernameError)
    setPasswordError(nextPasswordError)
    if (nextUsernameError || nextPasswordError) return

    setIsLoading(true)
    try {
      await loginWithPassword(normalizedUsername, password)
      navigate('/')
    } catch (loginError: unknown) {
      toast.error(getLoginErrorMessage(loginError))
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
          void loginWithGoogle(credential)
            .then(() => navigate('/'))
            .catch((loginError: unknown) => toast.error(getLoginErrorMessage(loginError)))
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
    <main className="login-page-shell h-dvh w-full overflow-hidden bg-[#0F2854] p-0.5">
      <div className="grid h-full min-h-0 overflow-hidden rounded-[10px] bg-[#FFFFFF] lg:grid-cols-[58%_42%]">
        <section
          className="relative hidden h-full min-h-0 overflow-hidden bg-[#071426] lg:block"
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

          <div className="login-hero-copy pointer-events-none absolute top-[6.5%] left-[7.5%] right-[4%] z-10">
            <div className="login-hero-badge mb-5 inline-flex items-center gap-2 rounded-full border border-[#3d8ccc]/25 bg-[#0b2744]/70 px-3 py-1.5 text-xs font-semibold tracking-[0.12em] text-[#87dffc] uppercase shadow-[0_0_18px_rgba(56,189,248,0.08)] backdrop-blur-md">
              <span className="h-1.5 w-1.5 rounded-full bg-[#50e1ff] shadow-[0_0_9px_#50e1ff]" />
              AI-powered oral examination
            </div>

            <h1 className="text-[clamp(1.85rem,3.2vw,3.4rem)] leading-none font-bold tracking-[-0.045em] whitespace-nowrap text-white">
              Practice. Speak.{' '}
              <span className="bg-gradient-to-r from-[#64d8ff] to-[#9ebeff] bg-clip-text text-transparent">
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

        <section className="relative flex h-full min-h-0 flex-col overflow-hidden bg-[linear-gradient(180deg,#EAF5FB_0%,#F4FBFD_42%,#FFFFFF_100%)]">
          <div
            className="pointer-events-none absolute inset-0"
            aria-hidden="true"
            style={{
              backgroundImage:
                'radial-gradient(ellipse at 88% 8%, rgba(73,136,196,0.12), transparent 38%), radial-gradient(ellipse at 12% 92%, rgba(189,232,245,0.22), transparent 44%)',
            }}
          />

          <header className="login-header relative z-10 flex shrink-0 items-center justify-between gap-3 px-6 pt-4 sm:px-8 sm:pt-5">
            <div>
              <p className="text-[11px] font-bold tracking-[0.12em] text-[#1C4D8D] uppercase">
                Cổng đăng nhập
              </p>
              <p className="mt-0.5 text-xs font-semibold text-[#5b6b84]">Oral Exam System</p>
            </div>
            <span className="inline-flex items-center gap-1.5 rounded-full border border-[#b7e4cf] bg-[#e8f8f1] px-2.5 py-1 text-[11px] font-bold text-[#16836a]">
              <span className="size-1.5 rounded-full bg-[#22c58b] shadow-[0_0_6px_rgba(34,197,139,0.55)]" />
              Hệ thống sẵn sàng
            </span>
          </header>

          <div className="login-form-region relative z-10 flex min-h-0 flex-1 items-center justify-center overflow-y-auto px-6 py-4 sm:px-8">
            <div className="w-full max-w-[400px]">
              <div className="login-form-intro mb-6 text-center">
                <div className="login-logo relative mx-auto mb-4 flex h-[5.75rem] w-[5.75rem] items-center justify-center sm:h-[6.5rem] sm:w-[6.5rem]">
                  <img
                    src={universityTechLogo}
                    alt="University Tech"
                    className="size-full object-contain"
                    draggable={false}
                  />
                </div>
                <h2 className="text-[1.45rem] font-bold tracking-[-0.03em] text-[#0F2854]">
                  Chào mừng trở lại
                </h2>
                <p className="login-subtitle mx-auto mt-1.5 max-w-[34ch] text-sm leading-relaxed text-[#6d7f97]">
                  Đăng nhập để luyện tập và thi vấn đáp với phản hồi AI theo rubric.
                </p>
              </div>

              <form onSubmit={handlePasswordLogin} className="space-y-3.5" noValidate>
                <div>
                  <label
                    htmlFor="login-username"
                    className="mb-1.5 block text-[11px] font-bold tracking-[0.08em] text-[#6d7f97] uppercase"
                  >
                    Username
                  </label>
                  <div className="relative">
                    <UserRound className="pointer-events-none absolute top-1/2 left-3.5 size-4 -translate-y-1/2 text-[#4988C4]" />
                    <input
                      id="login-username"
                      name="username"
                      type="text"
                      value={username}
                      onChange={(event) => {
                        setUsername(event.target.value)
                        setUsernameError(null)
                      }}
                      autoComplete="username"
                      placeholder="Nhập username của bạn"
                      aria-invalid={Boolean(usernameError)}
                      className={cn(
                        'login-input min-h-11 w-full rounded-xl border border-[#B7D4EB] bg-white/85 px-3.5 py-2 pl-10 text-sm font-semibold text-[#0F2854] outline-none transition placeholder:font-medium placeholder:text-[#9aadc2] focus:border-[#4988C4] focus:bg-white focus:ring-4 focus:ring-[#4988C4]/12',
                        usernameError && fieldInvalidClass,
                      )}
                      disabled={isLoading}
                      required
                    />
                  </div>
                  <FieldError message={usernameError} />
                </div>

                <div>
                  <label
                    htmlFor="login-password"
                    className="mb-1.5 block text-[11px] font-bold tracking-[0.08em] text-[#6d7f97] uppercase"
                  >
                    Mật khẩu
                  </label>
                  <div className="relative">
                    <LockKeyhole className="pointer-events-none absolute top-1/2 left-3.5 size-4 -translate-y-1/2 text-[#4988C4]" />
                    <input
                      id="login-password"
                      name="password"
                      type={showPassword ? 'text' : 'password'}
                      value={password}
                      onChange={(event) => {
                        setPassword(event.target.value)
                        setPasswordError(null)
                      }}
                      autoComplete="current-password"
                      placeholder="Nhập mật khẩu"
                      aria-invalid={Boolean(passwordError)}
                      className={cn(
                        'login-password-input min-h-11 w-full rounded-xl border border-[#B7D4EB] bg-white/85 px-11 py-2 text-sm font-semibold text-[#0F2854] outline-none transition placeholder:font-medium placeholder:text-[#9aadc2] focus:border-[#4988C4] focus:bg-white focus:ring-4 focus:ring-[#4988C4]/12',
                        passwordError && fieldInvalidClass,
                      )}
                      disabled={isLoading}
                      required
                    />
                    <button
                      type="button"
                      aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                      onClick={() => setShowPassword((visible) => !visible)}
                      className="absolute top-1/2 right-2.5 flex size-8 -translate-y-1/2 items-center justify-center rounded-lg text-[#6d7f97] transition hover:bg-[#E7F3FB] hover:text-[#1C4D8D] focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-[#1C4D8D]"
                    >
                      {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                    </button>
                  </div>
                  <FieldError message={passwordError} />
                </div>

                <button
                  type="submit"
                  disabled={isLoading}
                  className="login-submit mt-0.5 inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-sm font-bold text-white shadow-[0_8px_20px_rgba(28,77,141,0.2)] transition hover:-translate-y-0.5 hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-wait disabled:opacity-60 motion-reduce:transform-none"
                >
                  {isLoading ? (
                    <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                  ) : (
                    <ArrowRight className="size-4" />
                  )}
                  {isLoading ? 'Đang đăng nhập…' : 'Đăng nhập'}
                </button>
              </form>

              <div className="login-divider my-5 flex items-center gap-3" aria-hidden="true">
                <span className="h-px flex-1 bg-[#cfe0ef]" />
                <span className="text-[11px] font-semibold tracking-[0.1em] text-[#8aa0b8] uppercase">
                  Hoặc
                </span>
                <span className="h-px flex-1 bg-[#cfe0ef]" />
              </div>

              <div className="login-google rounded-2xl border border-[#82aedb]/45 bg-white/70 p-3.5">
                <div className="mb-2.5 flex items-center justify-between gap-3">
                  <div className="min-w-0">
                    <p className="text-sm font-bold text-[#0F2854]">Đăng nhập bằng Google</p>
                    <p className="mt-0.5 text-xs leading-relaxed text-[#6d7f97]">
                      Tài khoản được cấp quyền bởi nhà trường.
                    </p>
                  </div>
                  <span className="flex size-8 shrink-0 items-center justify-center rounded-xl border border-[#82aedb]/40 bg-white text-[#1C4D8D]">
                    <ShieldCheck className="size-3.5" />
                  </span>
                </div>
                <div ref={googleButtonRef} className="flex min-h-10 justify-center" />
                {isLoading && (
                  <p className="mt-2 text-center text-xs font-semibold text-[#1C4D8D]">
                    Đang xác thực tài khoản…
                  </p>
                )}
                {!googleConfigured && (
                  <p className="mt-2 rounded-xl border border-[#f3ddb0] bg-[#fff8eb] px-2.5 py-1.5 text-center text-xs font-semibold text-[#9a6414]">
                    Chưa cấu hình VITE_GOOGLE_CLIENT_ID cho môi trường này.
                  </p>
                )}
              </div>

              <div className="login-security mt-5 text-center">
                <p className="flex items-center justify-center gap-1.5 text-xs text-[#6d7f97]">
                  <ShieldCheck className="size-3.5 text-[#4988C4]" />
                  Phiên đăng nhập được bảo vệ an toàn.
                </p>
                <p id="login-support" className="login-support mt-1.5 text-xs text-[#8aa0b8]">
                  Cần hỗ trợ? Liên hệ trợ giảng hoặc quản trị viên hệ thống.
                </p>
              </div>
            </div>
          </div>

          <footer className="login-footer relative z-10 shrink-0 px-6 pb-4 text-center text-[11px] text-[#8aa0b8] sm:px-8">
            © 2026 Faculty of Computer Science & Engineering. All rights reserved.
          </footer>
        </section>
      </div>
    </main>
  )
}
