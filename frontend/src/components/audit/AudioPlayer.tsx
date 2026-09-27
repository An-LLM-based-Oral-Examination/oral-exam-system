import React, { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Play, Pause, Volume2, ShieldCheck } from 'lucide-react'

interface AudioPlayerProps {
  audioUrl?: string
  hashSha256?: string
  studentCode?: string
  seatNumber?: number
}

export const AudioPlayer: React.FC<AudioPlayerProps> = ({
  audioUrl,
  hashSha256 = 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
  studentCode = 'SE180123',
  seatNumber = 12,
}) => {
  const [isPlaying, setIsPlaying] = useState(false)
  const [speed, setSpeed] = useState<number>(1.0)

  const togglePlay = () => setIsPlaying(!isPlaying)

  return (
    <div className="p-4 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 space-y-3">
      <div className="flex items-center justify-between text-xs">
        <div className="flex items-center space-x-2">
          <Volume2 className="w-4 h-4 text-orange-600" />
          <span className="font-semibold text-slate-800 dark:text-slate-200">
            Tệp ghi âm Cloudflare R2: <code className="text-orange-600 font-mono">{seatNumber}_{studentCode}.webm</code>
          </span>
        </div>
        <div className="flex items-center space-x-1 text-emerald-600 text-[11px] font-mono">
          <ShieldCheck className="w-3.5 h-3.5" />
          <span>SHA-256 Niêm phong</span>
        </div>
      </div>

      <div className="flex items-center space-x-3 bg-slate-50 dark:bg-slate-800/60 p-3 rounded-lg">
        <Button variant="primary" size="sm" onClick={togglePlay} className="h-9 w-9 p-0 rounded-full">
          {isPlaying ? <Pause className="w-4 h-4" /> : <Play className="w-4 h-4 ml-0.5" />}
        </Button>

        {/* Waveform placeholder */}
        <div className="flex-1 h-8 bg-slate-200 dark:bg-slate-700 rounded flex items-center px-3 justify-center text-xs text-slate-500">
          {audioUrl ? 'Đang phát thanh ghi âm thực tế' : 'Waveform Visualizer (Tương tác Timestamp)'}
        </div>

        {/* Speed toggle */}
        <div className="flex space-x-1">
          {[1.0, 1.25, 1.5].map((s) => (
            <button
              key={s}
              onClick={() => setSpeed(s)}
              className={`px-2 py-1 text-xs font-semibold rounded ${
                speed === s
                  ? 'bg-orange-600 text-white'
                  : 'bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-300'
              }`}
            >
              {s}x
            </button>
          ))}
        </div>
      </div>

      <div className="text-[10px] text-slate-400 truncate font-mono">
        Mã băm: {hashSha256}
      </div>
    </div>
  )
}
