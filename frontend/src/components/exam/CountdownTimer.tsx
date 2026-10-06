import React, { useState, useEffect } from 'react'
import { Clock } from 'lucide-react'
import { cn } from '@/utils/cn'

interface CountdownTimerProps {
  totalSeconds: number
  onExpire?: () => void
  label?: string
}

const CountdownTimerInstance: React.FC<CountdownTimerProps> = ({
  totalSeconds,
  onExpire,
  label = 'Thời gian còn lại',
}) => {
  const [secondsLeft, setSecondsLeft] = useState(totalSeconds)

  useEffect(() => {
    if (secondsLeft <= 0) {
      if (onExpire) onExpire()
      return
    }
    const interval = setInterval(() => {
      setSecondsLeft((prev) => prev - 1)
    }, 1000)
    return () => clearInterval(interval)
  }, [secondsLeft, onExpire])

  const minutes = Math.floor(secondsLeft / 60)
  const seconds = secondsLeft % 60
  const isUrgent = secondsLeft < 60

  return (
    <div
      className={cn(
        'flex items-center space-x-2 rounded-lg border px-3 py-1.5 font-mono text-sm font-bold transition-colors',
        isUrgent
          ? 'animate-pulse border-rose-300 bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300'
          : 'border-slate-200 bg-slate-50 text-slate-700 dark:bg-slate-800 dark:text-slate-300',
      )}
    >
      <Clock className="h-4 w-4" />
      <span className="mr-1 font-sans text-xs font-normal text-slate-500">{label}:</span>
      <span>
        {minutes < 10 ? `0${minutes}` : minutes}:{seconds < 10 ? `0${seconds}` : seconds}
      </span>
    </div>
  )
}

export const CountdownTimer: React.FC<CountdownTimerProps> = (props) => (
  <CountdownTimerInstance key={props.totalSeconds} {...props} />
)
