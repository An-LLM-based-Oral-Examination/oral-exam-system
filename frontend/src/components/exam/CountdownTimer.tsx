import React, { useState, useEffect } from 'react'
import { Clock } from 'lucide-react'
import { cn } from '@/utils/cn'

interface CountdownTimerProps {
  totalSeconds: number
  onExpire?: () => void
  label?: string
}

export const CountdownTimer: React.FC<CountdownTimerProps> = ({
  totalSeconds,
  onExpire,
  label = 'Thời gian còn lại',
}) => {
  const [secondsLeft, setSecondsLeft] = useState(totalSeconds)

  useEffect(() => {
    setSecondsLeft(totalSeconds)
  }, [totalSeconds])

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
        'flex items-center space-x-2 px-3 py-1.5 rounded-lg border font-mono text-sm font-bold transition-colors',
        isUrgent
          ? 'bg-rose-50 text-rose-700 border-rose-300 dark:bg-rose-950/40 dark:text-rose-300 animate-pulse'
          : 'bg-slate-50 text-slate-700 border-slate-200 dark:bg-slate-800 dark:text-slate-300'
      )}
    >
      <Clock className="w-4 h-4" />
      <span className="text-xs font-sans font-normal text-slate-500 mr-1">{label}:</span>
      <span>
        {minutes < 10 ? `0${minutes}` : minutes}:{seconds < 10 ? `0${seconds}` : seconds}
      </span>
    </div>
  )
}
