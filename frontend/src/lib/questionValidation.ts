export const MAX_QUESTION_TITLE_LENGTH = 300
export const MAX_RUBRIC_NAME_LENGTH = 200
export const MAX_IMAGE_URL_LENGTH = 500
export const MAX_IMAGE_DESCRIPTION_LENGTH = 2000

const SAFE_LOCAL_IMAGE_PATH_REGEX =
  /^\/uploads\/questions\/(?:[A-Za-z0-9_-]+\/)*[A-Za-z0-9_-]+\.(png|jpg|jpeg|webp)$/i

function hasControlCharacters(value: string): boolean {
  for (let i = 0; i < value.length; i += 1) {
    const code = value.charCodeAt(i)
    if (code < 32 || code === 127) {
      return true
    }
  }
  return false
}

function hasAtSignInAuthority(url: string): boolean {
  const schemeDelimiterIndex = url.indexOf('://')
  if (schemeDelimiterIndex < 0) return true
  const authorityStart = schemeDelimiterIndex + 3
  let authorityEnd = url.length
  for (const delimiter of ['/', '?', '#']) {
    const idx = url.indexOf(delimiter, authorityStart)
    if (idx >= 0 && idx < authorityEnd) {
      authorityEnd = idx
    }
  }
  return url.slice(authorityStart, authorityEnd).includes('@')
}

function isRestrictedIpv4Octets(first: number, second: number, third: number, fourth: number): boolean {
  if (
    [first, second, third, fourth].some(
      (octet) => !Number.isInteger(octet) || octet < 0 || octet > 255,
    )
  ) {
    return true
  }
  return (
    first === 0 ||
    first === 10 ||
    first === 127 ||
    (first === 100 && second >= 64 && second <= 127) ||
    (first === 169 && second === 254) ||
    (first === 172 && second >= 16 && second <= 31) ||
    (first === 192 && second === 168) ||
    first >= 224
  )
}

function isRestrictedIpv4Host(host: string): boolean {
  const parts = host.split('.')
  if (parts.length !== 4 || parts.some((part) => !/^\d+$/.test(part))) {
    return false
  }
  const octets = parts.map((part) => Number(part))
  return isRestrictedIpv4Octets(octets[0], octets[1], octets[2], octets[3])
}

function parseIpv4DottedQuad(token: string): [number, number] | null {
  const parts = token.split('.')
  if (parts.length !== 4 || parts.some((part) => !/^\d{1,3}$/.test(part))) {
    return null
  }
  const octets = parts.map((part) => Number(part))
  if (octets.some((octet) => octet < 0 || octet > 255)) {
    return null
  }
  return [(octets[0] << 8) | octets[1], (octets[2] << 8) | octets[3]]
}

function parseIpv6Side(side: string, allowEmbeddedIpv4: boolean): number[] | null {
  if (side === '') return []
  const tokens = side.split(':')
  const hextets: number[] = []
  for (let i = 0; i < tokens.length; i += 1) {
    const token = tokens[i]
    if (!token) return null
    if (allowEmbeddedIpv4 && i === tokens.length - 1 && token.includes('.')) {
      const quad = parseIpv4DottedQuad(token)
      if (!quad) return null
      hextets.push(quad[0], quad[1])
      continue
    }
    if (!/^[0-9a-f]{1,4}$/i.test(token)) {
      return null
    }
    hextets.push(Number.parseInt(token, 16))
  }
  return hextets
}

function parseIpv6Bytes(rawHost: string): Uint8Array | null {
  const hasLeftBracket = rawHost.startsWith('[')
  const hasRightBracket = rawHost.endsWith(']')
  if (hasLeftBracket !== hasRightBracket) {
    return null
  }
  const inner = hasLeftBracket ? rawHost.slice(1, -1) : rawHost
  if (!inner || !inner.includes(':') || inner.includes('%')) {
    return null
  }

  const doubleColonParts = inner.split('::')
  if (doubleColonParts.length > 2) {
    return null
  }

  let groups: number[]
  if (doubleColonParts.length === 1) {
    const parsed = parseIpv6Side(doubleColonParts[0], true)
    if (!parsed || parsed.length !== 8) {
      return null
    }
    groups = parsed
  } else {
    const [leftRaw, rightRaw] = doubleColonParts
    const left = parseIpv6Side(leftRaw, false)
    const right = parseIpv6Side(rightRaw, true)
    if (!left || !right || left.length + right.length >= 8) {
      return null
    }
    const zeroFillCount = 8 - (left.length + right.length)
    groups = [...left, ...Array<number>(zeroFillCount).fill(0), ...right]
  }

  const bytes = new Uint8Array(16)
  for (let i = 0; i < 8; i += 1) {
    bytes[i * 2] = (groups[i] >> 8) & 0xff
    bytes[i * 2 + 1] = groups[i] & 0xff
  }
  return bytes
}

function isRestrictedIpv6Bytes(bytes: Uint8Array): boolean {
  const isIpv4Mapped =
    bytes[0] === 0 &&
    bytes[1] === 0 &&
    bytes[2] === 0 &&
    bytes[3] === 0 &&
    bytes[4] === 0 &&
    bytes[5] === 0 &&
    bytes[6] === 0 &&
    bytes[7] === 0 &&
    bytes[8] === 0 &&
    bytes[9] === 0 &&
    bytes[10] === 0xff &&
    bytes[11] === 0xff

  if (isIpv4Mapped) {
    return isRestrictedIpv4Octets(bytes[12], bytes[13], bytes[14], bytes[15])
  }

  const isAny = bytes.every((byte) => byte === 0)
  const isLoopback = bytes.slice(0, 15).every((byte) => byte === 0) && bytes[15] === 1
  if (isAny || isLoopback) {
    return true
  }

  const isLinkLocal = bytes[0] === 0xfe && (bytes[1] & 0xc0) === 0x80
  const isSiteLocal = bytes[0] === 0xfe && (bytes[1] & 0xc0) === 0xc0
  const isMulticast = bytes[0] === 0xff
  const isUniqueLocal = (bytes[0] & 0xfe) === 0xfc

  return isLinkLocal || isSiteLocal || isMulticast || isUniqueLocal
}

export function isSafeQuestionImageUrl(url: string): boolean {
  if (!url || url.length > MAX_IMAGE_URL_LENGTH || hasControlCharacters(url)) {
    return false
  }
  const trimmed = url.trim()
  if (!trimmed) {
    return false
  }

  if (trimmed.toLowerCase().startsWith('/uploads/questions/')) {
    return SAFE_LOCAL_IMAGE_PATH_REGEX.test(trimmed)
  }

  try {
    const parsed = new URL(trimmed)
    if (
      parsed.protocol !== 'https:' ||
      parsed.username !== '' ||
      parsed.password !== '' ||
      hasAtSignInAuthority(trimmed)
    ) {
      return false
    }

    const host = parsed.hostname.replace(/\.+$/, '').toLowerCase()
    if (
      !host ||
      host === 'localhost' ||
      host.endsWith('.localhost') ||
      host.endsWith('.local') ||
      host.endsWith('.internal') ||
      host.endsWith('.localdomain') ||
      isRestrictedIpv4Host(host)
    ) {
      return false
    }

    const ipv6Bytes = parseIpv6Bytes(host)
    if (ipv6Bytes !== null) {
      return !isRestrictedIpv6Bytes(ipv6Bytes)
    }
    if (host.includes(':') || host.startsWith('[') || host.endsWith(']')) {
      return false
    }

    return true
  } catch {
    return false
  }
}

export function hasValidScorePrecision(value: number): boolean {
  return Number.isFinite(value) && Math.abs(Math.round(value * 100) - value * 100) < 1e-9
}

export function roundScore(value: number): number {
  return Math.round(value * 100) / 100
}

export function roundWeight(maxScore: number): number {
  return Math.round((maxScore / 10) * 10000) / 10000
}
