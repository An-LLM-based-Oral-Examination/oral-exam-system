import { useEffect, useRef, useState } from 'react'
import {
  AlertTriangle,
  CheckCircle2,
  Headphones,
  LoaderCircle,
  LockKeyhole,
  RefreshCw,
  Wrench,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import {
  BROWSER_GUIDE_ORDER,
  detectBrowserFamily,
  getBrowserPermissionGuide,
  getMicrophoneIssueMessage,
  hasAvailableAudioInputDevice,
  type BrowserFamily,
  type MicrophoneConnectionState,
  type MicrophoneIssueKind,
} from '@/utils/microphoneDiagnostics'

export interface MicrophoneRecoveryBannerProps {
  issue?: MicrophoneIssueKind | null
  connectionState?: MicrophoneConnectionState
  deviceLabel?: string
  onRetry?: () => void | boolean | Promise<void | boolean>
  isRetrying?: boolean
  preserveWorkNotice?: boolean
  autoRetryOnPermissionGrant?: boolean
  autoRetryOnDeviceChange?: boolean
  className?: string
}

export function MicrophoneRecoveryBanner({
  issue = null,
  connectionState = 'idle',
  deviceLabel,
  onRetry,
  isRetrying = false,
  preserveWorkNotice = false,
  autoRetryOnPermissionGrant = true,
  autoRetryOnDeviceChange = true,
  className,
}: MicrophoneRecoveryBannerProps) {
  const [detectedFamily] = useState<BrowserFamily>(() => detectBrowserFamily())
  const [selectedFamily, setSelectedFamily] = useState<BrowserFamily>(() => detectBrowserFamily())
  const onRetryRef = useRef(onRetry)

  useEffect(() => {
    onRetryRef.current = onRetry
  }, [onRetry])

  // Tự động kích hoạt kết nối lại ngay khi người dùng vừa bật quyền Allow trên thanh địa chỉ trình duyệt
  useEffect(() => {
    if (
      !autoRetryOnPermissionGrant ||
      issue !== 'permission_denied' ||
      typeof navigator === 'undefined' ||
      !navigator.permissions?.query
    ) {
      return
    }

    let cancelled = false
    let permissionStatus: PermissionStatus | null = null
    const handleChange = () => {
      if (cancelled || !permissionStatus) return
      if (permissionStatus.state === 'granted') {
        void onRetryRef.current?.()
      }
    }

    void navigator.permissions
      .query({ name: 'microphone' as PermissionName })
      .then((status) => {
        if (cancelled) return
        permissionStatus = status
        permissionStatus.addEventListener?.('change', handleChange)
        if (status.state === 'granted') {
          void onRetryRef.current?.()
        }
      })
      .catch(() => {
        // Một số trình duyệt không hỗ trợ Permissions API cho microphone; nút thử lại thủ công vẫn hoạt động.
      })

    return () => {
      cancelled = true
      permissionStatus?.removeEventListener?.('change', handleChange)
    }
  }, [autoRetryOnPermissionGrant, issue])

  // Tự động thử kết nối lại ở màn chờ kiểm tra khi sinh viên cắm tai nghe mới vào máy
  useEffect(() => {
    const shouldWatchDevicePlug =
      autoRetryOnDeviceChange &&
      (issue === 'device_missing' || issue === 'silent') &&
      connectionState !== 'disconnected' &&
      connectionState !== 'reconnecting'

    if (
      !shouldWatchDevicePlug ||
      typeof navigator === 'undefined' ||
      !navigator.mediaDevices?.addEventListener
    ) {
      return
    }

    let cancelled = false
    let debounceTimer: number | null = null

    const handleDeviceChange = () => {
      if (debounceTimer !== null) window.clearTimeout(debounceTimer)
      debounceTimer = window.setTimeout(() => {
        void (async () => {
          const available = await hasAvailableAudioInputDevice()
          if (!cancelled && available) {
            void onRetryRef.current?.()
          }
        })()
      }, 350)
    }

    navigator.mediaDevices.addEventListener('devicechange', handleDeviceChange)
    return () => {
      cancelled = true
      if (debounceTimer !== null) window.clearTimeout(debounceTimer)
      navigator.mediaDevices.removeEventListener('devicechange', handleDeviceChange)
    }
  }, [autoRetryOnDeviceChange, connectionState, issue])

  if (connectionState === 'reconnected' && !issue) {
    return (
      <div
        role="status"
        aria-live="polite"
        data-space-ignore="true"
        className={cn(
          'flex flex-wrap items-center justify-between gap-2 rounded-2xl border border-[#b7e4cf] bg-[#eefbf5] px-4 py-3 text-xs font-semibold text-[#15755c]',
          className,
        )}
      >
        <span className="flex items-center gap-2">
          <CheckCircle2 className="size-4 shrink-0 text-[#16836a]" />
          <span>
            Đã tự động kết nối lại tai nghe / Microphone
            {deviceLabel ? ` (${deviceLabel})` : ''}. Bài đang làm vẫn được giữ nguyên — bạn có thể
            tiếp tục trả lời.
          </span>
        </span>
      </div>
    )
  }

  const isHeadsetUnpluggedMidSession =
    connectionState === 'disconnected' || connectionState === 'reconnecting'

  if (!isHeadsetUnpluggedMidSession && !issue) {
    return null
  }

  if (isHeadsetUnpluggedMidSession) {
    const reconnecting = connectionState === 'reconnecting' || isRetrying
    return (
      <div
        role="alert"
        aria-live="assertive"
        data-space-ignore="true"
        className={cn(
          'rounded-2xl border border-[#f5c97b] bg-[#fffaf0] p-4 text-left text-[#7a4b08] shadow-[0_8px_20px_rgba(122,75,8,0.08)]',
          className,
        )}
      >
        <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex items-start gap-3">
            <span className="mt-0.5 flex size-9 shrink-0 items-center justify-center rounded-xl bg-[#f59e0b]/15 text-[#b45309]">
              {reconnecting ? (
                <LoaderCircle className="size-5 animate-spin" />
              ) : (
                <Headphones className="size-5" />
              )}
            </span>
            <div className="min-w-0 space-y-1">
              <p className="text-sm font-bold text-[#693d04]">
                {reconnecting
                  ? 'Đang tự động kết nối lại tai nghe / Microphone…'
                  : 'Tai nghe / Microphone vừa bị ngắt kết nối'}
              </p>
              <p className="text-xs leading-relaxed text-[#85540e]">
                Hãy cắm chặt lại giắc tai nghe hoặc cổng USB. Hệ thống đang tự động dò thiết bị để
                nối tiếp phiên thu âm ngay khi nhận lại tín hiệu.
              </p>
              {preserveWorkNotice && (
                <p className="text-xs font-semibold text-[#16785f]">
                  An toàn dữ liệu: Toàn bộ câu trả lời, bản thu trước đó và thời gian làm bài vẫn
                  được giữ nguyên — tuyệt đối không bấm F5 tải lại trang.
                </p>
              )}
            </div>
          </div>
          {onRetry && (
            <button
              type="button"
              data-space-ignore="true"
              onClick={() => void onRetry()}
              disabled={reconnecting}
              className="inline-flex min-h-9 shrink-0 items-center justify-center gap-1.5 rounded-xl border border-[#d97706]/45 bg-white px-3.5 text-xs font-bold text-[#92400e] transition hover:bg-[#fef3c7] disabled:cursor-wait disabled:opacity-60"
            >
              {reconnecting ? (
                <LoaderCircle className="size-3.5 animate-spin" />
              ) : (
                <RefreshCw className="size-3.5" />
              )}
              {reconnecting ? 'Đang kết nối…' : 'Kết nối lại ngay'}
            </button>
          )}
        </div>
      </div>
    )
  }

  const activeIssue = issue ?? 'unknown'
  const guide = getBrowserPermissionGuide(selectedFamily)
  const detectedGuide = getBrowserPermissionGuide(detectedFamily)

  return (
    <div
      role="alert"
      data-space-ignore="true"
      className={cn(
        'rounded-2xl border border-[#f3c2c8] bg-[#fff6f7] p-4 text-left text-[#7f1d2d] shadow-[0_8px_22px_rgba(179,61,82,0.07)]',
        className,
      )}
    >
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="flex items-start gap-2.5">
          <span className="mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-xl bg-[#e11d48]/12 text-[#b33d52]">
            {activeIssue === 'permission_denied' ? (
              <LockKeyhole className="size-4" />
            ) : activeIssue === 'device_busy' ? (
              <Wrench className="size-4" />
            ) : (
              <AlertTriangle className="size-4" />
            )}
          </span>
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <p className="text-sm font-bold text-[#881b2f]">
                {activeIssue === 'permission_denied'
                  ? 'Quyền truy cập Microphone đang bị chặn'
                  : activeIssue === 'device_missing'
                    ? 'Chưa nhận diện được tai nghe / Microphone'
                    : activeIssue === 'device_busy'
                      ? 'Microphone đang bận hoặc bị khóa hệ thống'
                      : activeIssue === 'silent'
                        ? 'Chưa thu được tín hiệu giọng nói'
                        : 'Không thể khởi động Microphone'}
              </p>
              {activeIssue === 'permission_denied' && (
                <span className="inline-flex items-center rounded-full border border-[#1C4D8D]/25 bg-white px-2.5 py-0.5 text-[11px] font-bold text-[#1C4D8D]">
                  Trình duyệt: {detectedGuide.browserLabel}
                </span>
              )}
            </div>
            <p className="mt-1 text-xs leading-relaxed text-[#6b2737]">
              {getMicrophoneIssueMessage(activeIssue)}
            </p>
            {preserveWorkNotice && (
              <p className="mt-1.5 text-xs font-semibold text-[#16785f]">
                Bài đang làm và câu trả lời đã lưu được bảo toàn 100% — không cần tải lại trang
                (F5).
              </p>
            )}
          </div>
        </div>

        {onRetry && (
          <button
            type="button"
            data-space-ignore="true"
            onClick={() => void onRetry()}
            disabled={isRetrying}
            className="inline-flex min-h-9 shrink-0 items-center justify-center gap-1.5 rounded-xl bg-[#1C4D8D] px-3.5 text-xs font-bold text-white shadow-[0_6px_14px_rgba(28,77,141,0.2)] transition hover:bg-[#0F2854] disabled:cursor-wait disabled:opacity-60"
          >
            {isRetrying ? (
              <LoaderCircle className="size-3.5 animate-spin" />
            ) : (
              <RefreshCw className="size-3.5" />
            )}
            {isRetrying ? 'Đang kiểm tra…' : 'Thử kết nối lại Micro'}
          </button>
        )}
      </div>

      {activeIssue === 'permission_denied' && (
        <div className="mt-3.5 rounded-xl border border-[#82aedb]/45 bg-white p-3 text-[#0F2854]">
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[#e6eef7] pb-2">
            <span className="text-xs font-bold text-[#0F2854]">
              Cách cấp lại quyền trên {guide.browserLabel}:
            </span>
            <div
              className="flex flex-wrap gap-1"
              role="tablist"
              aria-label="Chọn trình duyệt hướng dẫn"
            >
              {BROWSER_GUIDE_ORDER.map((family) => {
                const tabGuide = getBrowserPermissionGuide(family)
                const active = selectedFamily === family
                return (
                  <button
                    key={family}
                    type="button"
                    role="tab"
                    data-space-ignore="true"
                    aria-selected={active}
                    onClick={() => setSelectedFamily(family)}
                    className={cn(
                      'rounded-lg px-2 py-1 text-[11px] font-bold transition',
                      active
                        ? 'bg-[#1C4D8D] text-white'
                        : 'bg-[#F4FBFD] text-[#4c6280] hover:bg-[#e3effa]',
                    )}
                  >
                    {tabGuide.badgeLabel}
                  </button>
                )
              })}
            </div>
          </div>

          <ol className="mt-2.5 space-y-1.5 text-xs leading-relaxed text-[#2c3f59]">
            {guide.steps.map((step, index) => (
              <li key={step} className="flex items-start gap-2">
                <span className="mt-0.5 flex size-4 shrink-0 items-center justify-center rounded-full bg-[#1C4D8D]/12 text-[10px] font-extrabold text-[#1C4D8D]">
                  {index + 1}
                </span>
                <span>{step}</span>
              </li>
            ))}
          </ol>

          <p className="mt-2.5 rounded-lg bg-[#F4FBFD] px-2.5 py-1.5 text-[11px] leading-relaxed text-[#526580]">
            {guide.osHint}
          </p>
        </div>
      )}

      {activeIssue === 'device_busy' && (
        <ul className="mt-3 space-y-1 rounded-xl border border-[#82aedb]/45 bg-white p-3 text-xs leading-relaxed text-[#2c3f59]">
          <li>
            1. Tắt tính năng thu âm của các ứng dụng đang chạy ngầm (Zoom, Teams, Discord, OBS).
          </li>
          <li>
            2. Kiểm tra cài đặt hệ điều hành (Windows Privacy &amp; security &gt; Microphone) đang
            cho phép ứng dụng trình duyệt sử dụng micro.
          </li>
          <li>3. Rút tai nghe ra cắm lại rồi bấm &quot;Thử kết nối lại Micro&quot;.</li>
        </ul>
      )}
    </div>
  )
}
