import { useEffect, useState } from 'react'
import { createPortal } from 'react-dom'
import { Maximize2, X } from 'lucide-react'

interface QuestionImageProps {
  imageUrl?: string | null
  imageAlt?: string | null
  className?: string
}

export function QuestionImage({ imageUrl, imageAlt, className = '' }: QuestionImageProps) {
  const [failedImageUrl, setFailedImageUrl] = useState<string | null>(null)
  const [isExpanded, setIsExpanded] = useState(false)
  const normalizedImageUrl = imageUrl?.trim()
  const normalizedImageAlt = imageAlt?.trim() || 'Question illustration image'

  useEffect(() => {
    if (!isExpanded) return

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setIsExpanded(false)
    }
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    window.addEventListener('keydown', handleKeyDown)

    return () => {
      document.body.style.overflow = previousOverflow
      window.removeEventListener('keydown', handleKeyDown)
    }
  }, [isExpanded])

  if (!normalizedImageUrl || failedImageUrl === normalizedImageUrl) return null

  return (
    <>
      <figure
        className={`overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-[#F8FBFE] p-2 shadow-[0_6px_20px_rgba(31,61,105,0.05)] ${className}`}
      >
        <div className="flex min-h-40 items-center justify-center rounded-xl bg-white p-2 sm:min-h-52">
          <button
            type="button"
            onClick={() => setIsExpanded(true)}
            aria-label="Enlarge question image"
            className="group relative flex h-full w-full items-center justify-center rounded-lg focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
          >
            <img
              src={normalizedImageUrl}
              alt={normalizedImageAlt}
              loading="lazy"
              onError={() => setFailedImageUrl(normalizedImageUrl)}
              className="max-h-[min(28rem,60vh)] w-full cursor-zoom-in rounded-lg object-contain"
            />
            <span className="pointer-events-none absolute right-2 top-2 flex size-8 items-center justify-center rounded-full bg-[#0F2854]/75 text-white opacity-0 shadow-md transition-opacity group-hover:opacity-100 group-focus-visible:opacity-100">
              <Maximize2 className="size-4" />
            </span>
          </button>
        </div>
      </figure>

      {isExpanded &&
        createPortal(
          <div
            role="dialog"
            aria-modal="true"
            aria-label="Expanded question image"
            className="fixed inset-0 z-[100] flex items-center justify-center bg-[#07152d]/85 p-4 backdrop-blur-sm sm:p-8"
            onMouseDown={(event) => {
              if (event.target === event.currentTarget) setIsExpanded(false)
            }}
          >
            <button
              type="button"
              onClick={() => setIsExpanded(false)}
              aria-label="Close expanded image"
              className="absolute right-4 top-4 flex size-10 items-center justify-center rounded-full border border-white/35 bg-white/15 text-white shadow-lg transition hover:bg-white/25 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white sm:right-7 sm:top-7"
            >
              <X className="size-5" />
            </button>
            <img
              src={normalizedImageUrl}
              alt={normalizedImageAlt}
              className="max-h-[calc(100vh-2rem)] max-w-[calc(100vw-2rem)] rounded-xl object-contain shadow-2xl sm:max-h-[calc(100vh-4rem)] sm:max-w-[calc(100vw-4rem)]"
            />
          </div>,
          document.body,
        )}
    </>
  )
}
