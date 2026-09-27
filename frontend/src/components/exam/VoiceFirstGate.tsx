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
          <span className="flex items-center text-amber-600 font-semibold">
            <Lock className="w-3.5 h-3.5 mr-1" />
            Đang khóa soạn thảo — Hãy nói qua Micro trước!
          </span>
        ) : (
          <span className="text-emerald-600 font-semibold">
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
          className="w-full h-36 rounded-lg border border-slate-300 dark:border-slate-700 p-3 text-sm focus:ring-2 focus:ring-orange-500 disabled:bg-slate-100 dark:disabled:bg-slate-800/60 disabled:cursor-not-allowed resize-none"
        />

        {!isRecordingFinished && (
          <div className="absolute inset-0 flex items-center justify-center bg-slate-100/40 dark:bg-slate-900/40 backdrop-blur-[1px] rounded-lg pointer-events-none">
            <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-md px-4 py-2 rounded-full flex items-center space-x-2 text-xs font-semibold text-slate-700 dark:text-slate-300">
              <Mic className="w-4 h-4 text-orange-600 animate-pulse" />
              <span>Bắt buộc hoàn thành phát biểu qua Micro</span>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
