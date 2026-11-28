import { useCallback, useEffect, useRef, useState } from 'react'
import { FileAudio2, LoaderCircle, RefreshCw, ShieldCheck, Volume2, VolumeX } from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { officialExamService } from '@/services/officialExam.service'
import { userMessage } from '@/lib/userMessage'

interface WaveformPlayerProps {
  audioUrl?: string | null
  hashSha256?: string | null
  studentCode: string
  seatNumber: number
  ticketId?: string | null
  submissionId?: string | null
  onRefreshAudioUrl?: () => Promise<string | null>
}

type WaveformState = 'idle' | 'loading' | 'ready' | 'unavailable'

const BAR_COUNT = 96

function getAudioFileName(seatNumber: number, studentCode: string) {
  return `${String(seatNumber).padStart(2, '0')}_${studentCode}.webm`
}

function extractAmplitudes(audioBuffer: AudioBuffer) {
  const channel = audioBuffer.getChannelData(0)
  const blockSize = Math.max(1, Math.floor(channel.length / BAR_COUNT))
  const amplitudes: number[] = []

  for (let index = 0; index < BAR_COUNT; index += 1) {
    const start = index * blockSize
    const end = Math.min(channel.length, start + blockSize)
    let peak = 0

    for (let sampleIndex = start; sampleIndex < end; sampleIndex += 1) {
      peak = Math.max(peak, Math.abs(channel[sampleIndex] ?? 0))
    }

    amplitudes.push(Math.max(0.06, Math.min(1, peak)))
  }

  return amplitudes
}

function drawWaveform(
  canvas: HTMLCanvasElement,
  amplitudes: number[] | null,
  progress: number,
) {
  const bounds = canvas.getBoundingClientRect()
  const pixelRatio = window.devicePixelRatio || 1
  const width = Math.max(1, Math.floor(bounds.width * pixelRatio))
  const height = Math.max(1, Math.floor(bounds.height * pixelRatio))

  if (canvas.width !== width || canvas.height !== height) {
    canvas.width = width
    canvas.height = height
  }

  const context = canvas.getContext('2d')
  if (!context) return

  context.clearRect(0, 0, width, height)
  if (!amplitudes) {
    context.strokeStyle = '#b9cbe8'
    context.lineWidth = Math.max(1, pixelRatio)
    context.setLineDash([pixelRatio * 4, pixelRatio * 4])
    context.beginPath()
    context.moveTo(0, height / 2)
    context.lineTo(width, height / 2)
    context.stroke()
    context.setLineDash([])
    return
  }

  const bars = amplitudes
  const gap = Math.max(1, pixelRatio * 2)
  const barWidth = Math.max(1, (width - gap * (bars.length - 1)) / bars.length)
  const center = height / 2
  const playedBars = Math.floor(progress * bars.length)

  bars.forEach((amplitude, index) => {
    const barHeight = Math.max(pixelRatio * 3, amplitude * height * 0.82)
    const x = index * (barWidth + gap)
    const y = center - barHeight / 2
    context.fillStyle = index <= playedBars ? '#4988C4' : '#b9cbe8'
    context.beginPath()
    context.roundRect(x, y, barWidth, barHeight, barWidth / 2)
    context.fill()
  })
}

export function WaveformPlayer({
  audioUrl,
  hashSha256,
  studentCode,
  seatNumber,
  ticketId,
  submissionId,
  onRefreshAudioUrl,
}: WaveformPlayerProps) {
  const audioRef = useRef<HTMLAudioElement>(null)
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const autoRefreshAttemptedKeyRef = useRef<string | null>(null)
  const refreshInFlightRef = useRef<Promise<string | null> | null>(null)
  const pendingResumeTimeRef = useRef<number | null>(null)
  const [waveform, setWaveform] = useState<number[] | null>(null)
  const [waveformState, setWaveformState] = useState<WaveformState>('idle')
  const [progress, setProgress] = useState(0)
  const [duration, setDuration] = useState(0)
  const [playbackRate, setPlaybackRate] = useState(1)
  const [refreshed, setRefreshed] = useState<{
    sourceKey: string
    url: string
    hashSha256?: string | null
  } | null>(null)
  const [refreshingUrl, setRefreshingUrl] = useState(false)
  const [refreshError, setRefreshError] = useState('')
  const [playbackErrorKey, setPlaybackErrorKey] = useState<string | null>(null)

  const fileName = getAudioFileName(seatNumber, studentCode)
  const sourceKey = `${ticketId ?? ''}|${submissionId ?? ''}|${audioUrl ?? ''}`
  const activeAudioUrl = refreshed?.sourceKey === sourceKey ? refreshed.url : (audioUrl ?? null)
  const effectiveHash =
    refreshed?.sourceKey === sourceKey && refreshed.hashSha256
      ? refreshed.hashSha256
      : hashSha256
  const hasPlaybackError = playbackErrorKey === sourceKey
  const canRefresh = Boolean(
    onRefreshAudioUrl ||
      (ticketId && typeof officialExamService.getAudioPlaybackUrl === 'function'),
  )

  const refreshAudioUrl = useCallback(
    async (options?: {
      preservePlaybackTime?: boolean
      manual?: boolean
    }): Promise<string | null> => {
      if (!canRefresh) return null
      if (refreshInFlightRef.current) return refreshInFlightRef.current

      const savedTime =
        options?.preservePlaybackTime && audioRef.current ? audioRef.current.currentTime : 0

      setRefreshingUrl(true)
      setRefreshError('')
      const task = (async () => {
        try {
          let nextUrl: string | null = null
          let nextHash: string | null | undefined
          if (onRefreshAudioUrl) {
            nextUrl = await onRefreshAudioUrl()
          } else if (ticketId && typeof officialExamService.getAudioPlaybackUrl === 'function') {
            const response = await officialExamService.getAudioPlaybackUrl(ticketId, submissionId)
            nextUrl = response.downloadUrl
            nextHash = response.audioHashSha256
          }

          if (!nextUrl) {
            if (options?.manual) {
              setRefreshError('Máy chủ không trả về liên kết âm thanh mới.')
            }
            return null
          }

          pendingResumeTimeRef.current = savedTime > 0 ? savedTime : null
          setRefreshed({ sourceKey, url: nextUrl, hashSha256: nextHash })
          setPlaybackErrorKey(null)
          return nextUrl
        } catch (error) {
          setRefreshError(userMessage(error, 'Không thể làm mới liên kết âm thanh R2.'))
          return null
        } finally {
          setRefreshingUrl(false)
          refreshInFlightRef.current = null
        }
      })()

      refreshInFlightRef.current = task
      return task
    },
    [canRefresh, onRefreshAudioUrl, sourceKey, submissionId, ticketId],
  )

  useEffect(() => {
    if (!activeAudioUrl) return

    let cancelled = false
    let audioContext: AudioContext | null = null

    const loadWaveform = async () => {
      setWaveform(null)
      setWaveformState('loading')
      try {
        const response = await fetch(activeAudioUrl, { credentials: 'omit' })
        if (!response.ok) {
          if (
            !cancelled &&
            canRefresh &&
            autoRefreshAttemptedKeyRef.current !== sourceKey
          ) {
            autoRefreshAttemptedKeyRef.current = sourceKey
            const renewedUrl = await refreshAudioUrl()
            if (renewedUrl && renewedUrl !== activeAudioUrl) {
              return
            }
          }
          throw new Error('Audio request failed')
        }

        const bytes = await response.arrayBuffer()
        const AudioContextConstructor =
          window.AudioContext ??
          (window as typeof window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
        if (!AudioContextConstructor) throw new Error('AudioContext is unavailable')

        audioContext = new AudioContextConstructor()
        const decoded = await audioContext.decodeAudioData(bytes.slice(0))
        if (cancelled) return

        setWaveform(extractAmplitudes(decoded))
        setWaveformState('ready')
      } catch {
        if (!cancelled) {
          setWaveform(null)
          setWaveformState('unavailable')
        }
      } finally {
        await audioContext?.close().catch(() => undefined)
      }
    }

    void loadWaveform()
    return () => {
      cancelled = true
      void audioContext?.close().catch(() => undefined)
    }
  }, [activeAudioUrl, canRefresh, refreshAudioUrl, sourceKey])

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return

    const render = () => drawWaveform(canvas, waveform, progress)
    render()
    if (typeof ResizeObserver === 'undefined') return
    const observer = new ResizeObserver(render)
    observer.observe(canvas)
    return () => observer.disconnect()
  }, [progress, waveform])

  useEffect(() => {
    if (audioRef.current) audioRef.current.playbackRate = playbackRate
  }, [playbackRate])

  const handleLoadedMetadata = () => {
    const audio = audioRef.current
    if (!audio) return
    if (pendingResumeTimeRef.current !== null) {
      const targetTime = pendingResumeTimeRef.current
      pendingResumeTimeRef.current = null
      const maxDuration = Number.isFinite(audio.duration) ? audio.duration : targetTime
      audio.currentTime = Math.min(targetTime, maxDuration)
    }
    setDuration(Number.isFinite(audio.duration) ? audio.duration : 0)
    setProgress(audio.duration > 0 ? audio.currentTime / audio.duration : 0)
  }

  const updateProgress = () => {
    const audio = audioRef.current
    if (!audio) return
    setDuration(Number.isFinite(audio.duration) ? audio.duration : 0)
    setProgress(audio.duration > 0 ? audio.currentTime / audio.duration : 0)
  }

  const handleAudioError = () => {
    if (canRefresh && autoRefreshAttemptedKeyRef.current !== sourceKey) {
      autoRefreshAttemptedKeyRef.current = sourceKey
      void refreshAudioUrl({ preservePlaybackTime: true }).then((renewedUrl) => {
        if (!renewedUrl) setPlaybackErrorKey(sourceKey)
      })
      return
    }
    setPlaybackErrorKey(sourceKey)
  }

  return (
    <section
      aria-labelledby="evidence-audio-title"
      className="overflow-hidden rounded-2xl border border-[#82aedb]/55 bg-white shadow-[0_10px_34px_rgba(31,61,105,0.06)]"
    >
      <div className="flex flex-col gap-3 border-b border-[#82aedb]/55 px-4 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-5">
        <div className="flex min-w-0 items-center gap-3">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-[#4988C4]/16 text-[#1C4D8D]">
            <FileAudio2 className="size-[18px]" aria-hidden="true" />
          </span>
          <div className="min-w-0">
            <h3 id="evidence-audio-title" className="text-sm font-bold text-[#0F2854]">
              Bằng chứng âm thanh gốc
            </h3>
            <p className="mt-0.5 truncate font-mono text-xs font-semibold text-[#527099]">
              {fileName}
            </p>
          </div>
        </div>

        {effectiveHash ? (
          <span className="inline-flex w-fit items-center gap-1.5 rounded-full border border-[#bfe9dc] bg-[#effbf7] px-2.5 py-1 text-xs font-bold text-[#14745d]">
            <ShieldCheck className="size-3.5" aria-hidden="true" />
            SHA-256 đã ghi nhận
          </span>
        ) : (
          <span className="inline-flex w-fit items-center gap-1.5 rounded-full border border-[#f1dec0] bg-[#fff9ed] px-2.5 py-1 text-xs font-bold text-[#956319]">
            Chưa có mã niêm phong
          </span>
        )}
      </div>

      <div className="space-y-4 p-4 sm:p-5">
        {activeAudioUrl ? (
          <div className="rounded-2xl border border-[#82aedb]/55 bg-[linear-gradient(135deg,#BDE8F5,#edf5ff)] p-4">
            <div className="mb-3 flex flex-wrap items-center justify-between gap-2 text-sm font-semibold text-[#3e5f89]">
              <span className="flex items-center gap-2">
                <Volume2 className="size-4 text-[#1C4D8D]" aria-hidden="true" />
                Nghe lại bản ghi trên Cloudflare R2
              </span>
              <div className="flex flex-wrap items-center gap-2">
                {canRefresh &&
                (waveformState === 'unavailable' || hasPlaybackError || Boolean(refreshError)) ? (
                  <button
                    type="button"
                    disabled={refreshingUrl}
                    onClick={() => void refreshAudioUrl({ preservePlaybackTime: true, manual: true })}
                    className="inline-flex items-center gap-1.5 rounded-lg border border-[#4988C4] bg-white px-2.5 py-1 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#eef7fd] disabled:opacity-50"
                  >
                    <RefreshCw className={refreshingUrl ? 'size-3.5 animate-spin' : 'size-3.5'} />
                    {refreshingUrl ? 'Đang làm mới…' : 'Làm mới liên kết âm thanh'}
                  </button>
                ) : null}
                <label className="inline-flex items-center gap-2 text-xs font-bold text-[#527099]">
                  Tốc độ
                  <select
                    value={playbackRate}
                    onChange={(event) => setPlaybackRate(Number(event.target.value))}
                    className="rounded-lg border border-[#4988C4] bg-white px-2 py-1 text-xs font-bold text-[#2c4d78] outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                    aria-label="Tốc độ phát audio"
                  >
                    <option value={1}>1.0x</option>
                    <option value={1.25}>1.25x</option>
                    <option value={1.5}>1.5x</option>
                  </select>
                </label>
              </div>
            </div>

            <div className="rounded-xl border border-[#82aedb]/55 bg-white/75 px-3 py-2">
              <canvas
                ref={canvasRef}
                className="h-16 w-full"
                aria-label="Dạng sóng của bản ghi âm"
                role="img"
              />
              <div className="mt-1 flex items-center gap-2 text-xs text-[#7185a1]" aria-live="polite">
                {(waveformState === 'loading' || refreshingUrl) && (
                  <LoaderCircle className="size-3 animate-spin" />
                )}
                {refreshingUrl
                  ? 'Liên kết âm thanh hết hạn, đang cấp lại đường dẫn R2 mới…'
                  : waveformState === 'loading'
                    ? 'Đang phân tích dạng sóng…'
                    : waveformState === 'unavailable'
                      ? 'Không thể dựng dạng sóng từ URL hiện tại; vẫn có thể nghe audio hoặc làm mới liên kết.'
                      : waveformState === 'ready'
                        ? `${duration > 0 ? `${duration.toFixed(1)} giây` : 'Đã sẵn sàng'} · ${Math.round(progress * 100)}%`
                        : null}
              </div>
            </div>

            {hasPlaybackError || refreshError ? (
              <p
                role="alert"
                className="mt-2.5 rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-3 py-2 text-xs font-semibold text-[#a43d4b]"
              >
                {refreshError ||
                  'Không thể phát âm thanh (liên kết ký tạm thời có thể đã hết hạn sau 15 phút). Nhấn “Làm mới liên kết âm thanh” để thử lại.'}
              </p>
            ) : null}

            <audio
              ref={audioRef}
              className="mt-3 h-10 w-full"
              controls
              preload="metadata"
              src={activeAudioUrl}
              onLoadedMetadata={handleLoadedMetadata}
              onTimeUpdate={updateProgress}
              onEnded={() => setProgress(1)}
              onError={handleAudioError}
            >
              Trình duyệt của bạn không hỗ trợ phát tệp âm thanh WebM.
            </audio>
          </div>
        ) : (
          <EmptyState
            compact
            icon={VolumeX}
            title="Chưa nhận được tệp âm thanh"
            description="Trình phát sẽ hiển thị khi có tệp âm thanh hợp lệ."
            action={
              canRefresh ? (
                <button
                  type="button"
                  disabled={refreshingUrl}
                  onClick={() => void refreshAudioUrl({ manual: true })}
                  className="inline-flex items-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-3 py-1.5 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#eef7fd] disabled:opacity-50"
                >
                  <RefreshCw className={refreshingUrl ? 'size-3.5 animate-spin' : 'size-3.5'} />
                  {refreshingUrl ? 'Đang lấy liên kết…' : 'Tải liên kết âm thanh'}
                </button>
              ) : undefined
            }
          />
        )}

        <div className="rounded-xl border border-[#82aedb]/55 bg-white px-3 py-2.5">
          <p className="text-xs font-bold tracking-[0.08em] text-[#7e8da1] uppercase">
            Mã băm toàn vẹn
          </p>
          <p className="mt-1 font-mono text-xs leading-relaxed break-all text-[#465a75]">
            {effectiveHash ?? 'Không có dữ liệu SHA-256 để đối chiếu.'}
          </p>
        </div>
      </div>
    </section>
  )
}

export { WaveformPlayer as AudioPlayer }
