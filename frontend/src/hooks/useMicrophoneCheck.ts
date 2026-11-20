import { useCallback, useEffect, useRef, useState } from 'react'

type DeviceState = 'idle' | 'checking' | 'testing' | 'ready' | 'error' | 'unsupported'
type DeviceIssue = '' | 'permission' | 'silent'

const MIC_VOICE_THRESHOLD = 0.012
const MIC_TEST_LIMIT_MS = 10000
const MIC_VOICE_HOLD_MS = 1500

function readMicrophoneLevel(samples: Uint8Array) {
  let squareSum = 0
  for (const sample of samples) {
    const normalized = (sample - 128) / 128
    squareSum += normalized * normalized
  }
  return Math.sqrt(squareSum / samples.length)
}

export const MICROPHONE_STATUS: Record<
  DeviceState,
  { title: string; helper: string; color: string; dot: string }
> = {
  idle: {
    title: 'Chưa kiểm tra micro',
    helper: 'Nói thử một câu trước khi vào phòng.',
    color: 'text-[#5d6d83]',
    dot: 'bg-[#9aa9bc]',
  },
  checking: {
    title: 'Đang kết nối micro…',
    helper: 'Vui lòng cho phép trình duyệt sử dụng micro.',
    color: 'text-[#1C4D8D]',
    dot: 'bg-[#1C4D8D] animate-pulse motion-reduce:animate-none',
  },
  testing: {
    title: 'Hãy nói thử một câu',
    helper: 'Thanh mức nhích lên khi micro thu được giọng.',
    color: 'text-[#1C4D8D]',
    dot: 'bg-[#1C4D8D] animate-pulse motion-reduce:animate-none',
  },
  ready: {
    title: 'Micro đã sẵn sàng',
    helper: 'Đã thu được giọng nói.',
    color: 'text-[#16836a]',
    dot: 'bg-[#33c391] shadow-[0_0_8px_rgba(51,195,145,0.65)]',
  },
  error: {
    title: 'Không thể truy cập micro',
    helper: 'Hãy cấp quyền micro rồi kiểm tra lại.',
    color: 'text-[#cf4b61]',
    dot: 'bg-[#ed6075]',
  },
  unsupported: {
    title: 'Trình duyệt chưa hỗ trợ kiểm tra',
    helper: 'Bạn có thể thử lại bằng Chrome hoặc Edge phiên bản mới.',
    color: 'text-[#a26812]',
    dot: 'bg-[#e1a13f]',
  },
}


export function useMicrophoneCheck() {
  const [deviceState, setDeviceState] = useState<DeviceState>('idle')
  const [deviceIssue, setDeviceIssue] = useState<DeviceIssue>('')
  const [deviceNames, setDeviceNames] = useState('')
  const [micLevel, setMicLevel] = useState(0)
  const micTestRef = useRef<{ cancelled: boolean; stop: () => void } | null>(null)

  const stopMicTest = useCallback(() => {
    micTestRef.current?.stop()
    micTestRef.current = null
  }, [])
  useEffect(() => () => stopMicTest(), [stopMicTest])

  const handleDeviceCheck = async () => {
    const AudioContextConstructor =
      window.AudioContext ??
      (window as typeof window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext

    if (!navigator.mediaDevices?.getUserMedia || !AudioContextConstructor) {
      setDeviceIssue('')
      setDeviceState('unsupported')
      return
    }

    stopMicTest()
    setMicLevel(0)
    setDeviceIssue('')
    setDeviceNames('')
    setDeviceState('checking')

    const session = {
      cancelled: false,
      stop: () => undefined as void,
    }
    micTestRef.current = session

    let stream: MediaStream | null = null
    let audioContext: AudioContext | null = null
    let sourceNode: MediaStreamAudioSourceNode | null = null

    session.stop = () => {
      session.cancelled = true
      sourceNode?.disconnect()
      sourceNode = null
      if (audioContext) void audioContext.close().catch(() => undefined)
      audioContext = null
      stream?.getTracks().forEach((track) => track.stop())
      stream = null
    }

    try {
      stream = await navigator.mediaDevices.getUserMedia({
        audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
      })
      if (session.cancelled) return

      const audioTrack = stream.getAudioTracks()[0]
      if (!audioTrack || audioTrack.readyState !== 'live') {
        throw new Error('Microphone is unavailable.')
      }

      audioContext = new AudioContextConstructor()
      if (audioContext.state === 'suspended') await audioContext.resume()
      if (session.cancelled) return

      const analyser = audioContext.createAnalyser()
      analyser.fftSize = 2048
      analyser.smoothingTimeConstant = 0.35
      sourceNode = audioContext.createMediaStreamSource(stream)
      sourceNode.connect(analyser)

      setDeviceNames(audioTrack.label || 'Microphone mặc định')
      setDeviceState('testing')

      const samples = new Uint8Array(analyser.fftSize)
      const startedAt = performance.now()
      let peak = 0
      let heardAt = 0

      while (!session.cancelled && performance.now() - startedAt < MIC_TEST_LIMIT_MS) {
        analyser.getByteTimeDomainData(samples)
        const level = readMicrophoneLevel(samples)
        peak = Math.max(peak, level)
        setMicLevel(Math.min(1, level / 0.08))
        if (level >= MIC_VOICE_THRESHOLD && heardAt === 0) heardAt = performance.now()
        if (heardAt > 0 && performance.now() - heardAt >= MIC_VOICE_HOLD_MS) break
        await new Promise((resolve) => window.setTimeout(resolve, 80))
      }

      if (session.cancelled) return

      const heardVoice = !audioTrack.muted && (heardAt > 0 || peak >= MIC_VOICE_THRESHOLD)
      if (!heardVoice) {
        setMicLevel(0)
        setDeviceIssue('silent')
        setDeviceState('error')
        return
      }

      setMicLevel(0)
      setDeviceIssue('')
      setDeviceState('ready')
    } catch {
      if (session.cancelled) return
      setMicLevel(0)
      setDeviceIssue('permission')
      setDeviceState('error')
    } finally {
      session.stop()
      if (micTestRef.current === session) micTestRef.current = null
    }
  }
  return { deviceState, setDeviceState, deviceIssue, deviceNames, micLevel, setMicLevel, stopMicTest, handleDeviceCheck }
}
