import { coloredCardTones } from '@/design/coloredCardTones'
import { LoaderCircle, Mic2 } from 'lucide-react'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { MICROPHONE_STATUS, useMicrophoneCheck } from '@/hooks/useMicrophoneCheck'

type MicrophoneCheck = ReturnType<typeof useMicrophoneCheck>

export function MicrophoneCheckPanel({ check }: { check: MicrophoneCheck }) {
  const { deviceState, deviceIssue, deviceNames, micLevel, handleDeviceCheck } = check
  const deviceStatus = MICROPHONE_STATUS[deviceState]
  return (
    <div
      className={cn(
        'rounded-2xl border p-3.5',
        ds.panelShadow,
        deviceState === 'ready'
          ? coloredCardTones.green
          : deviceState === 'error'
            ? coloredCardTones.rose
            : 'border-[#82aedb]/55 bg-[#F4FBFD]',
      )}
    >
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0 flex-1">
          <p
            className={cn('flex items-center gap-2 text-sm font-bold', deviceStatus.color)}
          >
            <span className={cn('size-2 shrink-0 rounded-full', deviceStatus.dot)} />
            <span className="truncate">{deviceState === 'error' && deviceIssue === 'silent'
              ? 'Chưa nghe thấy giọng nói'
              : deviceStatus.title}</span>
          </p>
          <p title={deviceState === 'ready' ? deviceNames : undefined} className="mt-1 flex min-w-0 gap-1 text-xs leading-relaxed text-[#5b6b84]">
            {deviceState === 'error' && deviceIssue === 'silent'
              ? <span className="truncate">Hãy nói gần micro một câu ngắn rồi thử lại.</span>
              : deviceState === 'ready' && deviceNames
                ? <><span className="truncate">{deviceNames}</span><span className="shrink-0">· Đã thu được giọng</span></>
                : <span className="truncate">{deviceStatus.helper}</span>}
          </p>
        </div>
        <button
          type="button"
          onClick={() => void handleDeviceCheck()}
          disabled={deviceState === 'checking' || deviceState === 'testing'}
          className="inline-flex min-h-10 shrink-0 items-center justify-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#0F2854] shadow-[0_3px_8px_rgba(23,48,96,0.05)] hover:-translate-y-0.5 hover:border-[#4988C4] disabled:cursor-wait disabled:opacity-60 motion-reduce:transform-none"
        >
          {deviceState === 'checking' || deviceState === 'testing' ? (
            <LoaderCircle className="size-3.5 animate-spin" />
          ) : (
            <Mic2 className="size-3.5" />
          )}
          {deviceState === 'testing'
            ? 'Đang nghe…'
            : deviceState === 'ready'
              ? 'Kiểm tra lại'
              : 'Kiểm tra micro'}
        </button>
      </div>
      {deviceState === 'testing' && (
        <div className="mt-3 flex h-8 items-end gap-1" aria-hidden="true">
          {Array.from({ length: 16 }, (_, index) => (
            <span key={index} className={cn('w-1.5 flex-1 rounded-full transition-colors', micLevel > index / 16 ? 'bg-[#1C4D8D]' : 'bg-[#e4ebf5]')}
              style={{ height: `${10 + index}px` }} />
          ))}
        </div>
      )}
    </div>
  )
}
