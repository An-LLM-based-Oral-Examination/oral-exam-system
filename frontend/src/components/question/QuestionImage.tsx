import { useEffect, useState } from 'react'
import { createPortal } from 'react-dom'
import { Maximize2, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import { resolvePublicAssetUrl } from '@/utils/mediaUrl'

interface QuestionImageProps {
  imageUrl?: string | null
  imageAlt?: string | null
  className?: string
  /** Stretch to match sibling column height (side-by-side layouts). */
  fillHeight?: boolean
  /** Show imageDescription/imageAlt as visible caption under the image. */
  showCaption?: boolean
  /** Draw own border/background. Turn off when nested inside a shared frame. */
  framed?: boolean
}

export function QuestionImage({
  imageUrl,
  imageAlt,
  className = '',
  fillHeight = false,
  showCaption = true,
  framed = true,
}: QuestionImageProps) {
  const [failedUrl, setFailedUrl] = useState<string | null>(null)
  const [isExpanded, setIsExpanded] = useState(false)
  const resolvedImageUrl = resolvePublicAssetUrl(imageUrl)
  const caption = imageAlt?.trim() || ''
  const hasError = Boolean(resolvedImageUrl && failedUrl === resolvedImageUrl)
  const altText = showCaption && caption ? caption : 'Hình ảnh minh họa cho câu hỏi'

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

  if (!resolvedImageUrl || hasError) return null

  return (
    <>
      <figure
        className={cn(
          'overflow-hidden',
          framed &&
            'rounded-2xl border border-[#4988C4]/45 bg-[#F8FBFE] p-2 shadow-[0_6px_20px_rgba(31,61,105,0.05)]',
          fillHeight && 'relative flex h-full min-h-0 flex-col',
          className,
        )}
      >
        <div
          className={cn(
            'flex items-center justify-center overflow-hidden',
            framed ? 'rounded-xl bg-white p-2' : 'rounded-xl',
            fillHeight
              ? 'absolute inset-0 min-h-0'
              : framed
                ? 'min-h-40 sm:min-h-52'
                : null,
          )}
        >
          <button
            type="button"
            onClick={() => setIsExpanded(true)}
            aria-label="Phóng to hình ảnh câu hỏi"
            className="group relative flex h-full w-full items-center justify-center rounded-lg focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
          >
            <img
              src={resolvedImageUrl}
              alt={altText}
              loading="lazy"
              onError={() => setFailedUrl(resolvedImageUrl)}
              className={cn(
                'cursor-zoom-in rounded-xl object-contain',
                fillHeight
                  ? 'h-full w-full'
                  : framed
                    ? 'w-full max-h-[min(28rem,60vh)]'
                    : 'h-auto w-full',
              )}
            />
            <span className="pointer-events-none absolute right-2 top-2 flex size-8 items-center justify-center rounded-full bg-[#0F2854]/75 text-white opacity-0 shadow-md transition-opacity group-hover:opacity-100 group-focus-visible:opacity-100">
              <Maximize2 className="size-4" />
            </span>
          </button>
        </div>
        {showCaption && caption ? (
          <figcaption className="relative z-10 mt-auto shrink-0 px-1 pb-1 text-center text-xs leading-relaxed text-[#5b6b84]">
            {caption}
          </figcaption>
        ) : null}
      </figure>

      {isExpanded &&
        createPortal(
          <div
            role="dialog"
            aria-modal="true"
            aria-label="Hình ảnh câu hỏi phóng to"
            className="fixed inset-0 z-[100] flex items-center justify-center bg-[#07152d]/85 p-4 backdrop-blur-sm sm:p-8"
            onMouseDown={(event) => {
              if (event.target === event.currentTarget) setIsExpanded(false)
            }}
          >
            <button
              type="button"
              onClick={() => setIsExpanded(false)}
              aria-label="Đóng hình phóng to"
              className="absolute right-4 top-4 flex size-10 items-center justify-center rounded-full border border-white/35 bg-white/15 text-white shadow-lg transition hover:bg-white/25 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white sm:right-7 sm:top-7"
            >
              <X className="size-5" />
            </button>
            <img
              src={resolvedImageUrl}
              alt={altText}
              className="max-h-[calc(100vh-2rem)] max-w-[calc(100vw-2rem)] rounded-xl object-contain shadow-2xl sm:max-h-[calc(100vh-4rem)] sm:max-w-[calc(100vw-4rem)]"
            />
          </div>,
          document.body,
        )}
    </>
  )
}
