import { useEffect, useRef, useState } from 'react'
import { FileAudio2, LoaderCircle, ShieldCheck, Volume2, VolumeX } from 'lucide-react'

interface WaveformPlayerProps {
  audioUrl?: string | null
  hashSha256?: string | null
  studentCode: string
  seatNumber: number
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
    context.fillStyle = index <= playedBars ? '#245eea' : '#b9cbe8'
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
}: WaveformPlayerProps) {
  const audioRef = useRef<HTMLAudioElement>(null)
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const [waveform, setWaveform] = useState<number[] | null>(null)
  const [waveformState, setWaveformState] = useState<WaveformState>('idle')
  const [progress, setProgress] = useState(0)
  const [duration, setDuration] = useState(0)
  const [playbackRate, setPlaybackRate] = useState(1)
  const fileName = getAudioFileName(seatNumber, studentCode)

  useEffect(() => {
    if (!audioUrl) return

    let cancelled = false
    let audioContext: AudioContext | null = null

    const loadWaveform = async () => {
      setWaveform(null)
      setWaveformState('loading')
      try {
        const response = await fetch(audioUrl, { credentials: 'omit' })
        if (!response.ok) throw new Error('Audio request failed')

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
  }, [audioUrl])

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return

    const render = () => drawWaveform(canvas, waveform, progress)
    render()
    const observer = new ResizeObserver(render)
    observer.observe(canvas)
    return () => observer.disconnect()
  }, [progress, waveform])

  useEffect(() => {
    if (audioRef.current) audioRef.current.playbackRate = playbackRate
  }, [playbackRate])

  const updateProgress = () => {
    const audio = audioRef.current
    if (!audio) return
    setDuration(Number.isFinite(audio.duration) ? audio.duration : 0)
    setProgress(audio.duration > 0 ? audio.currentTime / audio.duration : 0)
  }

  return (
    <section
      aria-labelledby="evidence-audio-title"
      className="overflow-hidden rounded-2xl border border-[#dce5f1] bg-white shadow-[0_10px_34px_rgba(31,61,105,0.06)]"
    >
      <div className="flex flex-col gap-3 border-b border-[#e8edf4] px-4 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-5">
        <div className="flex min-w-0 items-center gap-3">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-[#eaf1ff] text-[#245eea]">
            <FileAudio2 className="size-[18px]" aria-hidden="true" />
          </span>
          <div className="min-w-0">
            <h3 id="evidence-audio-title" className="text-sm font-bold text-[#13213a]">
              Bằng chứng âm thanh gốc
            </h3>
            <p className="mt-0.5 truncate font-mono text-xs font-semibold text-[#527099]">
              {fileName}
            </p>
          </div>
        </div>

        {hashSha256 ? (
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
        {audioUrl ? (
          <div className="rounded-2xl border border-[#d9e5f4] bg-[linear-gradient(135deg,#f5f9ff,#edf5ff)] p-4">
            <div className="mb-3 flex flex-wrap items-center justify-between gap-2 text-xs font-semibold text-[#3e5f89]">
              <span className="flex items-center gap-2">
                <Volume2 className="size-4 text-[#245eea]" aria-hidden="true" />
                Nghe lại bản ghi trên Cloudflare R2
              </span>
              <label className="inline-flex items-center gap-2 text-xs font-bold text-[#527099]">
                Tốc độ
                <select
                  value={playbackRate}
                  onChange={(event) => setPlaybackRate(Number(event.target.value))}
                  className="rounded-lg border border-[#cbd9eb] bg-white px-2 py-1 text-xs font-bold text-[#2c4d78] outline-none focus:border-[#245eea] focus:ring-2 focus:ring-[#dce8ff]"
                  aria-label="Tốc độ phát audio"
                >
                  <option value={1}>1.0x</option>
                  <option value={1.25}>1.25x</option>
                  <option value={1.5}>1.5x</option>
                </select>
              </label>
            </div>

            <div className="rounded-xl border border-[#d6e2f2] bg-white/75 px-3 py-2">
              <canvas
                ref={canvasRef}
                className="h-16 w-full"
                aria-label="Dạng sóng của bản ghi âm"
                role="img"
              />
              <div className="mt-1 flex items-center gap-2 text-xs text-[#7185a1]" aria-live="polite">
                {waveformState === 'loading' && <LoaderCircle className="size-3 animate-spin" />}
                {waveformState === 'loading' && 'Đang phân tích dạng sóng…'}
                {waveformState === 'unavailable' && 'Không thể dựng dạng sóng từ URL hiện tại; vẫn có thể nghe audio.'}
                {waveformState === 'ready' && `${duration > 0 ? `${duration.toFixed(1)} giây` : 'Đã sẵn sàng'} · ${Math.round(progress * 100)}%`}
              </div>
            </div>

            <audio
              ref={audioRef}
              className="mt-3 h-10 w-full"
              controls
              preload="metadata"
              src={audioUrl}
              onLoadedMetadata={updateProgress}
              onTimeUpdate={updateProgress}
              onEnded={() => setProgress(1)}
            >
              Trình duyệt của bạn không hỗ trợ phát tệp âm thanh WebM.
            </audio>
          </div>
        ) : (
          <div className="flex min-h-28 flex-col items-center justify-center rounded-2xl border border-dashed border-[#ccd8e8] bg-[#f8faff] px-5 text-center">
            <VolumeX className="size-6 text-[#8fa0b8]" aria-hidden="true" />
            <p className="mt-2 text-xs font-bold text-[#455873]">Chưa nhận được tệp âm thanh</p>
            <p className="mt-1 max-w-md text-xs leading-relaxed text-[#78889e]">
              Hệ thống sẽ hiển thị trình phát khi ca thi trả về URL bằng chứng hợp lệ.
            </p>
          </div>
        )}

        <div className="rounded-xl border border-[#e4eaf2] bg-[#fbfcfe] px-3 py-2.5">
          <p className="text-xs font-bold tracking-[0.08em] text-[#7e8da1] uppercase">
            Mã băm toàn vẹn
          </p>
          <p className="mt-1 font-mono text-xs leading-relaxed break-all text-[#465a75]">
            {hashSha256 ?? 'Không có dữ liệu SHA-256 để đối chiếu.'}
          </p>
        </div>
      </div>
    </section>
  )
}

export { WaveformPlayer as AudioPlayer }
