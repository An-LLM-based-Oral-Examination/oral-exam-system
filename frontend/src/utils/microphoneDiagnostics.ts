export type BrowserFamily = 'chrome' | 'coccoc' | 'edge' | 'firefox' | 'safari' | 'other'

export type MicrophoneIssueKind =
  'permission_denied' | 'device_missing' | 'device_busy' | 'unsupported' | 'silent' | 'unknown'

export type MicrophoneConnectionState =
  'idle' | 'connected' | 'disconnected' | 'reconnecting' | 'reconnected'

export interface BrowserPermissionGuide {
  family: BrowserFamily
  browserLabel: string
  badgeLabel: string
  steps: readonly string[]
  osHint: string
}

export const BROWSER_GUIDE_ORDER: readonly BrowserFamily[] = [
  'chrome',
  'edge',
  'coccoc',
  'firefox',
  'safari',
]

const BROWSER_GUIDES: Record<BrowserFamily, BrowserPermissionGuide> = {
  chrome: {
    family: 'chrome',
    browserLabel: 'Google Chrome',
    badgeLabel: 'Chrome',
    steps: [
      'Nhấn vào biểu tượng Tùy chỉnh trang (2 thanh trượt) hoặc Ổ khóa nằm ngay bên trái thanh địa chỉ URL.',
      'Tại dòng Microphone (Máy thu âm), gạt công tắc hoặc đổi trạng thái sang Cho phép (Allow).',
      'Nhấn nút "Thử kết nối lại Micro" bên dưới để tiếp tục ngay (không bấm F5 / tải lại trang).',
    ],
    osHint:
      'Nếu đã bật trên Chrome nhưng vẫn báo chặn, hãy kiểm tra Windows Settings > Privacy & security > Microphone (hoặc macOS System Settings > Privacy & Security > Microphone) đã cấp quyền cho Google Chrome.',
  },
  coccoc: {
    family: 'coccoc',
    browserLabel: 'Trình duyệt Cốc Cốc',
    badgeLabel: 'Cốc Cốc',
    steps: [
      'Nhấn vào biểu tượng Ổ khóa hoặc Tùy chỉnh trang ở bên trái thanh địa chỉ Cốc Cốc.',
      'Tại mục Máy thu âm (Microphone), chuyển trạng thái từ Chặn sang Cho phép.',
      'Nhấn nút "Thử kết nối lại Micro" bên dưới mà không cần tải lại trang.',
    ],
    osHint:
      'Nếu hệ điều hành đang tắt quyền thu âm, hãy bật Microphone cho Cốc Cốc trong phần Quyền riêng tư (Privacy) của máy tính.',
  },
  edge: {
    family: 'edge',
    browserLabel: 'Microsoft Edge',
    badgeLabel: 'Edge',
    steps: [
      'Nhấn vào biểu tượng Ổ khóa bên trái thanh địa chỉ URL, sau đó mở Quyền cho trang web này (Permissions for this site).',
      'Tìm mục Microphone và đổi từ Chặn (Block) sang Cho phép (Allow).',
      'Quay lại tab bài thi này và nhấn nút "Thử kết nối lại Micro" bên dưới (không bấm F5).',
    ],
    osHint:
      'Trên máy phòng Lab Windows: nếu vẫn bị khóa, hãy kiểm tra Windows Settings > Privacy & security > Microphone đang ở trạng thái On.',
  },
  firefox: {
    family: 'firefox',
    browserLabel: 'Mozilla Firefox',
    badgeLabel: 'Firefox',
    steps: [
      'Nhấn vào biểu tượng Microphone bị gạch chéo hoặc biểu tượng Ổ khóa ở bên trái thanh địa chỉ URL.',
      'Nhấn dấu "X" bên cạnh dòng "Đã chặn (Blocked)" của mục Sử dụng Microphone để gỡ trạng thái chặn.',
      'Nhấn nút "Thử kết nối lại Micro" bên dưới rồi chọn Cho phép (Allow) trên hộp thoại vừa hiện ra.',
    ],
    osHint:
      'Mẹo: Tích thêm ô "Ghi nhớ quyết định này" trên hộp thoại của Firefox để không bị hỏi lại khi chuyển câu.',
  },
  safari: {
    family: 'safari',
    browserLabel: 'Apple Safari',
    badgeLabel: 'Safari',
    steps: [
      'Mở menu Safari > Cài đặt cho trang web này... (Settings for This Website) hoặc nhấn biểu tượng aA trên thanh địa chỉ.',
      'Tại mục Microphone, chuyển từ Từ chối (Deny) sang Cho phép (Allow).',
      'Nhấn nút "Thử kết nối lại Micro" bên dưới để tiếp tục làm bài mà không đóng tab.',
    ],
    osHint:
      'Trên macOS: đảm bảo Safari đã được tích chọn trong System Settings > Privacy & Security > Microphone.',
  },
  other: {
    family: 'other',
    browserLabel: 'Trình duyệt hiện tại',
    badgeLabel: 'Mặc định',
    steps: [
      'Nhấn vào biểu tượng Ổ khóa hoặc Cài đặt trang nằm ở phía bên trái thanh địa chỉ URL.',
      'Tìm quyền Microphone (Máy thu âm) và đổi sang Cho phép (Allow).',
      'Nhấn nút "Thử kết nối lại Micro" bên dưới để tiếp tục ngay mà không tải lại trang.',
    ],
    osHint:
      'Kiểm tra thêm cài đặt Quyền riêng tư (Privacy > Microphone) của hệ điều hành nếu trình duyệt vẫn chưa truy cập được thiết bị.',
  },
}

export function detectBrowserFamily(customUserAgent?: string): BrowserFamily {
  const ua = (
    customUserAgent ?? (typeof navigator !== 'undefined' ? navigator.userAgent : '')
  ).toLowerCase()
  if (!ua) return 'other'

  if (ua.includes('coc_coc_browser') || ua.includes('coccoc')) return 'coccoc'
  if (ua.includes('edg/') || ua.includes('edga/') || ua.includes('edgios/')) return 'edge'
  if (ua.includes('firefox/') || ua.includes('fxios/')) return 'firefox'
  if (ua.includes('chrome/') || ua.includes('crios/') || ua.includes('chromium/')) {
    return 'chrome'
  }
  if (ua.includes('safari/') && !ua.includes('opr/') && !ua.includes('opera')) {
    return 'safari'
  }
  return 'other'
}

export function getBrowserPermissionGuide(family?: BrowserFamily): BrowserPermissionGuide {
  const resolved = family ?? detectBrowserFamily()
  return BROWSER_GUIDES[resolved] ?? BROWSER_GUIDES.other
}

export function classifyMicrophoneError(error: unknown): MicrophoneIssueKind {
  if (!error) return 'unknown'

  if (typeof error === 'string') {
    const normalized = error.trim().toLowerCase()
    if (
      normalized === 'not-allowed' ||
      normalized === 'service-not-allowed' ||
      normalized.includes('permission') ||
      normalized.includes('denied') ||
      normalized.includes('chặn quyền')
    ) {
      return 'permission_denied'
    }
    if (
      normalized === 'audio-capture' ||
      normalized.includes('notfound') ||
      normalized.includes('not found') ||
      normalized.includes('unavailable') ||
      normalized.includes('không tìm thấy micro') ||
      normalized.includes('ngắt kết nối')
    ) {
      return 'device_missing'
    }
    if (normalized.includes('not supported') || normalized.includes('chưa hỗ trợ')) {
      return 'unsupported'
    }
  }

  if (typeof error === 'object' && error !== null) {
    const name = 'name' in error && typeof error.name === 'string' ? error.name : ''
    const message = 'message' in error && typeof error.message === 'string' ? error.message : ''
    const combined = `${name} ${message}`.toLowerCase()

    if (
      name === 'NotAllowedError' ||
      name === 'PermissionDeniedError' ||
      name === 'SecurityError' ||
      combined.includes('permission denied') ||
      combined.includes('permission dismissed') ||
      combined.includes('not allowed')
    ) {
      return 'permission_denied'
    }

    if (
      name === 'NotFoundError' ||
      name === 'DevicesNotFoundError' ||
      name === 'OverconstrainedError' ||
      combined.includes('requested device not found') ||
      combined.includes('device not found') ||
      combined.includes('microphone is unavailable') ||
      combined.includes('no audioinput')
    ) {
      return 'device_missing'
    }

    if (
      name === 'NotReadableError' ||
      name === 'TrackStartError' ||
      name === 'AbortError' ||
      combined.includes('could not start audio source') ||
      combined.includes('not readable') ||
      combined.includes('hardware error')
    ) {
      return 'device_busy'
    }

    if (combined.includes('chưa hỗ trợ') || combined.includes('not supported')) {
      return 'unsupported'
    }
  }

  return 'unknown'
}

export function getMicrophoneIssueMessage(issue: MicrophoneIssueKind): string {
  switch (issue) {
    case 'permission_denied':
      return 'Trình duyệt đang chặn quyền sử dụng Microphone. Hãy mở lại quyền theo hướng dẫn bên dưới rồi bấm kết nối lại.'
    case 'device_missing':
      return 'Không tìm thấy tai nghe hoặc Microphone đầu vào. Hãy cắm chặt lại giắc tai nghe / cổng USB để hệ thống tự động kết nối lại.'
    case 'device_busy':
      return 'Microphone đang bị ứng dụng khác (Zoom, Teams, Discord...) chiếm quyền hoặc bị tắt trong cài đặt hệ điều hành.'
    case 'unsupported':
      return 'Trình duyệt hiện tại chưa hỗ trợ thu âm trực tiếp qua Microphone. Vui lòng mở bằng Chrome, Edge hoặc Firefox.'
    case 'silent':
      return 'Chưa nhận được tín hiệu giọng nói từ Microphone. Hãy kiểm tra nút gạt tắt tiếng (Mute) trên dây tai nghe và nói gần micro hơn.'
    default:
      return 'Không thể kết nối Microphone. Hãy kiểm tra lại thiết bị âm thanh và thử kết nối lại.'
  }
}

export async function hasAvailableAudioInputDevice(): Promise<boolean> {
  if (typeof navigator === 'undefined' || !navigator.mediaDevices?.enumerateDevices) {
    return true
  }
  try {
    const devices = await navigator.mediaDevices.enumerateDevices()
    return devices.some((device) => device.kind === 'audioinput')
  } catch {
    return true
  }
}
