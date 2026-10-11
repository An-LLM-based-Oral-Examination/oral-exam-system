import React from 'react'
import { Lock, Mic } from 'lucide-react'

interface VoiceFirstGateProps {
  isRecordingFinished: boolean
  transcript: string
  onChange: (value: string) => void
}

export const VoiceFirstGate: React.FC<VoiceFirstGateProps> = ({
  isRecordingFinished,
  transcript,
  onChange,
}) => {
  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between text-xs text-slate-500">
        <span>Văn bản câu trả lời (Voice-First Guard):</span>
        {!isRecordingFinished ? (
          <span className="flex items-center font-semibold text-amber-600">
            <Lock className="mr-1 h-3.5 w-3.5" />
            Đang khóa soạn thảo — Hãy nói qua Micro trước!
          </span>
        ) : (
          <span className="font-semibold text-emerald-600">
            Đã mở khóa — Bạn có thể chỉnh sửa lỗi nhận diện
          </span>
        )}
      </div>

      <div className="relative">
        <textarea
          value={transcript}
          onChange={(e) => onChange(e.target.value)}
          disabled={!isRecordingFinished}
          placeholder="Hãy bấm 'Bắt đầu nói' và trả lời bằng giọng nói qua micro..."
          className="h-36 w-full resize-none rounded-lg border border-slate-300 p-3 text-sm focus:ring-2 focus:ring-orange-500 disabled:cursor-not-allowed disabled:bg-slate-100 dark:border-slate-700 dark:disabled:bg-slate-800/60"
        />

        {!isRecordingFinished && (
          <div className="pointer-events-none absolute inset-0 flex items-center justify-center rounded-lg bg-slate-100/40 backdrop-blur-[1px] dark:bg-slate-900/40">
            <div className="flex items-center space-x-2 rounded-full border border-slate-200 bg-white px-4 py-2 text-xs font-semibold text-slate-700 shadow-md dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300">
              <Mic className="h-4 w-4 animate-pulse text-orange-600" />
              <span>Bắt buộc hoàn thành phát biểu qua Micro</span>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
