import { useState } from 'react'
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
  const resolvedImageUrl = resolvePublicAssetUrl(imageUrl)
  const caption = imageAlt?.trim() || ''
  const hasError = Boolean(resolvedImageUrl && failedUrl === resolvedImageUrl)

  if (!resolvedImageUrl || hasError) return null

  return (
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
        <img
          src={resolvedImageUrl}
          alt={showCaption && caption ? caption : 'Hình ảnh minh họa cho câu hỏi'}
          loading="lazy"
          onError={() => setFailedUrl(resolvedImageUrl)}
          className={cn(
            'rounded-xl object-contain',
            fillHeight
              ? 'h-full w-full'
              : framed
                ? 'w-full max-h-[min(28rem,60vh)]'
                : 'h-auto w-full',
          )}
        />
      </div>
      {showCaption && caption ? (
        <figcaption className="relative z-10 mt-auto shrink-0 px-1 pb-1 text-center text-xs leading-relaxed text-[#5b6b84]">
          {caption}
        </figcaption>
      ) : null}
    </figure>
  )
}
