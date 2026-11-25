export async function sha256Hex(blob: Blob): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', await blob.arrayBuffer())
  return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, '0')).join('')
}

export async function retryOnce<T>(operation: () => Promise<T>, delayMs = 600): Promise<T> {
  try {
    return await operation()
  } catch (firstError) {
    await new Promise((resolve) => window.setTimeout(resolve, delayMs))
    try {
      return await operation()
    } catch {
      throw firstError
    }
  }
}
