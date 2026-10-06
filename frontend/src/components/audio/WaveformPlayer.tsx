import React, { useState } from 'react'
import { Button } from '@/components/common/Button'
import { Play, Pause, Volume2, ShieldCheck } from 'lucide-react'

interface WaveformPlayerProps {
  audioUrl?: string
  hashSha256?: string
  studentCode?: string
  seatNumber?: number
}

export const WaveformPlayer: React.FC<WaveformPlayerProps> = ({
  audioUrl,
  hashSha256 = 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
  studentCode = 'SE180123',
  seatNumber = 12,
}) => {
  const [isPlaying, setIsPlaying] = useState(false)
  const [speed, setSpeed] = useState<number>(1.0)

  const togglePlay = () => setIsPlaying(!isPlaying)

  return (
    <div className="space-y-3 rounded-xl border border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-center justify-between text-xs">
        <div className="flex items-center space-x-2">
          <Volume2 className="h-4 w-4 text-orange-600" />
          <span className="font-semibold text-slate-800 dark:text-slate-200">
            Tệp ghi âm Cloudflare R2:{' '}
            <code className="font-mono text-orange-600">
              {seatNumber}_{studentCode}.webm
            </code>
          </span>
        </div>
        <div className="flex items-center space-x-1 font-mono text-[11px] text-emerald-600">
          <ShieldCheck className="h-3.5 w-3.5" />
          <span>SHA-256 Niêm phong</span>
        </div>
      </div>

      <div className="flex items-center space-x-3 rounded-lg bg-slate-50 p-3 dark:bg-slate-800/60">
        <Button
          variant="primary"
          size="sm"
          onClick={togglePlay}
          className="h-9 w-9 rounded-full p-0"
        >
          {isPlaying ? <Pause className="h-4 w-4" /> : <Play className="ml-0.5 h-4 w-4" />}
        </Button>

        {/* Waveform placeholder */}
        <div className="flex h-8 flex-1 items-center justify-center rounded bg-slate-200 px-3 text-xs text-slate-500 dark:bg-slate-700">
          {audioUrl
            ? 'Đang phát thanh ghi âm thực tế'
            : 'Waveform Visualizer (Tương tác Timestamp)'}
        </div>

        {/* Speed toggle */}
        <div className="flex space-x-1">
          {[1.0, 1.25, 1.5].map((s) => (
            <button
              key={s}
              onClick={() => setSpeed(s)}
              className={`rounded px-2 py-1 text-xs font-semibold ${
                speed === s
                  ? 'bg-orange-600 text-white'
                  : 'bg-slate-200 text-slate-700 dark:bg-slate-700 dark:text-slate-300'
              }`}
            >
              {s}x
            </button>
          ))}
        </div>
      </div>

      <div className="truncate font-mono text-[10px] text-slate-400">Mã băm: {hashSha256}</div>
    </div>
  )
}

export { WaveformPlayer as AudioPlayer }
