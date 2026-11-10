declare global {
  interface Window {
    google?: {
      accounts: {
        id: {
          initialize: (options: {
            client_id: string
            callback: (response: { credential: string }) => void
            auto_select?: boolean
            itp_support?: boolean
          }) => void
          renderButton: (element: HTMLElement, options: Record<string, string | number>) => void
          cancel: () => void
        }
      }
    }
  }
}

export type GoogleCredentialResponse = { credential: string }
export type GoogleCredentialCallback = (response: GoogleCredentialResponse) => void

let isInitialized = false
let currentClientId: string | null = null
let activeCallback: GoogleCredentialCallback | null = null

export const googleAuthManager = {
  /**
   * Khởi tạo Google Identity Services theo mô hình Singleton duy nhất.
   * Nếu đã khởi tạo với cùng ClientId, cập nhật callback mới nhất và không gọi lại initialize().
   */
  initialize(clientId: string, callback: GoogleCredentialCallback): boolean {
    if (typeof window === 'undefined' || !window.google?.accounts?.id) {
      return false
    }

    // Cập nhật callback mới nhất để tránh stale closures từ React component
    activeCallback = callback

    // Nếu đã khởi tạo với cùng clientId, bỏ qua gọi lại initialize() để triệt tiêu cảnh báo [GSI_LOGGER]
    if (isInitialized && currentClientId === clientId) {
      return true
    }

    window.google.accounts.id.initialize({
      client_id: clientId,
      callback: (response) => {
        activeCallback?.(response)
      },
      auto_select: false,
      itp_support: true,
    })

    isInitialized = true
    currentClientId = clientId
    return true
  },

  /**
   * Render nút đăng nhập Google vào phần tử HTML chỉ định.
   */
  renderButton(element: HTMLElement, options?: Record<string, string | number>): void {
    if (typeof window === 'undefined' || !window.google?.accounts?.id) return
    element.replaceChildren()
    window.google.accounts.id.renderButton(element, {
      theme: 'outline',
      size: 'large',
      width: '360',
      text: 'signin_with',
      shape: 'rectangular',
      ...options,
    })
  },

  /**
   * Kiểm tra SDK Google Identity đã nạp và sẵn sàng chưa.
   */
  isLoaded(): boolean {
    return Boolean(typeof window !== 'undefined' && window.google?.accounts?.id)
  },

  /**
   * Trạng thái đã khởi tạo singleton.
   */
  isInitialized(): boolean {
    return isInitialized
  },

  /**
   * Reset trạng thái singleton khi cần (dành cho kiểm thử hoặc đổi clientId).
   */
  reset(): void {
    isInitialized = false
    currentClientId = null
    activeCallback = null
  },
}
