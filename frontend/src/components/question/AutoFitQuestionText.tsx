import { useLayoutEffect, useRef } from 'react'
import { cn } from '@/lib/utils'

interface AutoFitQuestionTextProps {
  orderIndex: number
  content: string
  className?: string
  minPx?: number
  maxPx?: number
}

/**
 * Scales font size so the question text fills its container height
 * without overflowing (used beside an image that drives the row height).
 */
export function AutoFitQuestionText({
  orderIndex,
  content,
  className = '',
  minPx = 12,
  maxPx = 24,
}: AutoFitQuestionTextProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const textRef = useRef<HTMLHeadingElement>(null)

  useLayoutEffect(() => {
    const container = containerRef.current
    const text = textRef.current
    if (!container || !text) return

    const fit = () => {
      const available = container.clientHeight
      if (available <= 0) return

      let low = minPx
      let high = maxPx
      let best = minPx

      for (let i = 0; i < 14; i += 1) {
        const mid = (low + high) / 2
        text.style.fontSize = `${mid}px`
        if (text.scrollHeight <= available + 1) {
          best = mid
          low = mid
        } else {
          high = mid
        }
      }

      text.style.fontSize = `${best}px`
    }

    fit()

    const observer = new ResizeObserver(() => fit())
    observer.observe(container)
    const panel = container.closest('[data-question-panel]')
    if (panel instanceof HTMLElement) {
      observer.observe(panel)
      const images = panel.querySelectorAll('img')
      images.forEach((image) => {
        image.addEventListener('load', fit)
        observer.observe(image)
      })
    }

    return () => {
      observer.disconnect()
      if (panel instanceof HTMLElement) {
        panel.querySelectorAll('img').forEach((image) => {
          image.removeEventListener('load', fit)
        })
      }
    }
  }, [content, maxPx, minPx, orderIndex])

  return (
    <div
      ref={containerRef}
      className={cn('flex h-full min-h-0 w-full items-center', className)}
    >
      <h2
        ref={textRef}
        className="w-full leading-snug font-semibold whitespace-pre-line text-[#0F2854]"
        style={{ fontSize: maxPx }}
      >
        <span className="font-semibold">Câu {orderIndex}. </span>
        {content}
      </h2>
    </div>
  )
}
