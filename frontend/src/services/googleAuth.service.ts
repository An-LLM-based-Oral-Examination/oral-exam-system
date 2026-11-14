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
            nonce?: string
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
let currentNonce: string | null = null
let activeCallback: GoogleCredentialCallback | null = null

export const googleAuthManager = {
  /**
   * Khởi tạo Google Identity Services theo mô hình Singleton duy nhất.
   * Cùng ClientId và nonce chỉ cập nhật callback; challenge mới cần initialize lại.
   */
  initialize(clientId: string, callback: GoogleCredentialCallback, nonce: string): boolean {
    if (!nonce || nonce.length < 32 || nonce.length > 128) throw new Error('Thiếu phiên đăng nhập Google hợp lệ.')
    if (typeof window === 'undefined' || !window.google?.accounts?.id) {
      return false
    }

    // Cập nhật callback mới nhất để tránh stale closures từ React component
    activeCallback = callback

    // Một challenge chỉ initialize một lần; nonce mới vô hiệu hóa callback cũ.
    if (isInitialized && currentClientId === clientId && currentNonce === nonce) {
      return true
    }

    window.google.accounts.id.initialize({
      client_id: clientId,
      callback: (response) => {
        if (currentNonce === nonce && currentClientId === clientId) activeCallback?.(response)
      },
      auto_select: false,
      itp_support: true,
      nonce,
    })

    isInitialized = true
    currentClientId = clientId
    currentNonce = nonce
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
    currentNonce = null
    activeCallback = null
  },
}
