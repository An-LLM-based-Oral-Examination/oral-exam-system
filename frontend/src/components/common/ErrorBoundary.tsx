import { Component, useEffect, type ErrorInfo, type ReactNode } from 'react'
import {
  Outlet,
  isRouteErrorResponse,
  useLocation,
  useNavigate,
  useRouteError,
} from 'react-router-dom'
import { AlertTriangle, Home, RefreshCw } from 'lucide-react'
import { ds } from '@/design/system'
import { useAuth } from '@/hooks/useAuth'
import { cn } from '@/lib/utils'
import { getRoleHomePath } from '@/routes/routeAccess'

export type ErrorBoundaryScope = 'app' | 'portal'

const DEFAULT_PORTAL_LABEL = 'Hệ thống Vấn đáp AI'

/** Lỗi tải gói giao diện (thường do vừa deploy phiên bản mới hoặc mất mạng ngắn). */
function isChunkLoadError(error: unknown): boolean {
  const message = (error instanceof Error ? error.message : String(error ?? '')).toLowerCase()
  return (
    message.includes('failed to fetch dynamically imported module') ||
    message.includes('importing a module script failed') ||
    message.includes('error loading dynamically imported module') ||
    message.includes('unable to preload css')
  )
}

function describeError(error: unknown): { title: string; description: string } {
  if (isChunkLoadError(error)) {
    return {
      title: 'Không tải được giao diện mới nhất',
      description:
        'Hệ thống vừa được cập nhật hoặc kết nối mạng đang không ổn định nên chưa tải được màn hình này. Hãy bấm "Tải lại trang" để tiếp tục.',
    }
  }
  if (isRouteErrorResponse(error) && error.status === 404) {
    return {
      title: 'Không tìm thấy trang',
      description: 'Đường dẫn bạn mở không tồn tại hoặc đã được di chuyển. Hãy quay về trang chủ.',
    }
  }
  return {
    title: 'Đã xảy ra sự cố hiển thị',
    description:
      'Màn hình này gặp lỗi không mong muốn. Hãy thử tải lại trang hoặc quay về trang chủ. Nếu lỗi lặp lại, vui lòng liên hệ quản trị viên hệ thống.',
  }
}

interface ErrorFallbackProps {
  scope: ErrorBoundaryScope
  portalLabel: string
  error: unknown
  onReload: () => void
  onGoHome: () => void
}

function ErrorFallback({ scope, portalLabel, error, onReload, onGoHome }: ErrorFallbackProps) {
  const { title, description } = describeError(error)

  return (
    <div
      data-space-ignore="true"
      className={cn(
        'flex items-center justify-center',
        scope === 'app' ? 'min-h-dvh bg-[#F4FBFD] p-4' : 'min-h-[50vh] px-2 py-8',
      )}
    >
      <section
        role="alert"
        aria-live="assertive"
        className="w-full max-w-lg rounded-2xl border border-[#f3c2c8] bg-white p-6 text-center shadow-[0_12px_32px_rgba(15,40,84,0.1)] sm:p-8"
      >
        <span className="mx-auto flex size-14 items-center justify-center rounded-full bg-[#fdecec] text-[#c2415b]">
          <AlertTriangle className="size-7" aria-hidden="true" />
        </span>
        <p className="mt-4 text-xs font-bold tracking-[0.12em] text-[#1C4D8D] uppercase">
          {portalLabel}
        </p>
        <h2 className="mt-1 text-xl font-bold text-[#0F2854]">{title}</h2>
        <p className="mt-2 text-sm leading-relaxed text-[#5b6b84]">{description}</p>
        <div className="mt-6 flex flex-col justify-center gap-3 sm:flex-row">
          <button
            type="button"
            data-space-ignore="true"
            onClick={onReload}
            className={cn(ds.primaryButton, 'w-full sm:w-auto')}
          >
            <RefreshCw className="size-4" aria-hidden="true" />
            Tải lại trang
          </button>
          <button
            type="button"
            data-space-ignore="true"
            onClick={onGoHome}
            className={cn(ds.outlineButton, 'w-full sm:w-auto')}
          >
            <Home className="size-4" aria-hidden="true" />
            Về trang chủ
          </button>
        </div>
      </section>
    </div>
  )
}

interface ErrorBoundaryProps {
  children?: ReactNode
  scope?: ErrorBoundaryScope
  portalLabel?: string
  /** Khi giá trị này đổi (ví dụ đổi route), trạng thái lỗi được xóa để hiển thị lại nội dung. */
  resetKey?: string
  onReload?: () => void
  onNavigateHome?: () => void
}

interface ErrorBoundaryState {
  hasError: boolean
  error: unknown
}

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { hasError: false, error: null }

  static getDerivedStateFromError(error: unknown): ErrorBoundaryState {
    return { hasError: true, error }
  }

  componentDidCatch(error: unknown, info: ErrorInfo) {
    // Chỉ ghi vào console phục vụ chẩn đoán; không hiển thị stack trace cho người dùng cuối.
    console.error(
      `[ErrorBoundary] ${this.props.portalLabel ?? DEFAULT_PORTAL_LABEL}`,
      error,
      info.componentStack,
    )
  }

  componentDidUpdate(prevProps: ErrorBoundaryProps) {
    if (this.state.hasError && prevProps.resetKey !== this.props.resetKey) {
      this.setState({ hasError: false, error: null })
    }
  }

  private handleReload = () => {
    if (this.props.onReload) this.props.onReload()
    else window.location.reload()
  }

  private handleGoHome = () => {
    this.setState({ hasError: false, error: null })
    if (this.props.onNavigateHome) this.props.onNavigateHome()
    else window.location.assign('/')
  }

  render() {
    if (!this.state.hasError) return this.props.children

    return (
      <ErrorFallback
        scope={this.props.scope ?? 'portal'}
        portalLabel={this.props.portalLabel ?? DEFAULT_PORTAL_LABEL}
        error={this.state.error}
        onReload={this.handleReload}
        onGoHome={this.handleGoHome}
      />
    )
  }
}

interface RoleRouteErrorBoundaryProps {
  children?: ReactNode
  scope?: ErrorBoundaryScope
  portalLabel: string
}

/**
 * Ranh giới lỗi cho từng nhánh route theo vai trò. Đặt BÊN TRONG layout (Sidebar/Header)
 * và BÊN TRONG ProtectedRoute để không thay đổi thứ tự kiểm tra quyền.
 */
export function RoleRouteErrorBoundary({
  children,
  scope = 'portal',
  portalLabel,
}: RoleRouteErrorBoundaryProps) {
  const location = useLocation()
  const navigate = useNavigate()
  const { user } = useAuth()
  const homePath = user ? getRoleHomePath(user.role) : '/'

  return (
    <ErrorBoundary
      scope={scope}
      portalLabel={portalLabel}
      resetKey={`${location.pathname}${location.search}`}
      onNavigateHome={() => navigate(homePath)}
    >
      {children ?? <Outlet />}
    </ErrorBoundary>
  )
}

/** `errorElement` gốc của Router: thay màn lỗi tiếng Anh mặc định của React Router. */
export function RouterErrorElement() {
  const routeError = useRouteError()
  const navigate = useNavigate()

  useEffect(() => {
    console.error('[RouterErrorElement]', routeError)
  }, [routeError])

  return (
    <ErrorFallback
      scope="app"
      portalLabel={DEFAULT_PORTAL_LABEL}
      error={routeError}
      onReload={() => window.location.reload()}
      onGoHome={() => navigate('/', { replace: true })}
    />
  )
}
