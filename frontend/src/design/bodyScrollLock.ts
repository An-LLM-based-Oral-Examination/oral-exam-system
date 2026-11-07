/** Khóa scroll trang nền với đếm tham chiếu — tránh kẹt khi nhiều overlay mở/đóng chồng nhau. */
let lockCount = 0
let previousBodyOverflow = ''
let previousHtmlOverflow = ''

export function lockBodyScroll() {
  if (typeof document === 'undefined') return () => {}

  if (lockCount === 0) {
    previousBodyOverflow = document.body.style.overflow
    previousHtmlOverflow = document.documentElement.style.overflow
    document.body.style.overflow = 'hidden'
    document.documentElement.style.overflow = 'hidden'
  }
  lockCount += 1

  let released = false
  return () => {
    if (released) return
    released = true
    lockCount = Math.max(0, lockCount - 1)
    if (lockCount === 0) {
      document.body.style.overflow = previousBodyOverflow
      document.documentElement.style.overflow = previousHtmlOverflow
    }
  }
}

/** Gỡ khóa kẹt (HMR / restore sai). Chỉ gọi khi layout mount, không khi overlay đang mở. */
export function resetBodyScrollLock() {
  if (typeof document === 'undefined') return
  lockCount = 0
  previousBodyOverflow = ''
  previousHtmlOverflow = ''
  document.body.style.overflow = ''
  document.documentElement.style.overflow = ''
}
