import { useEffect, useState } from 'react'

interface QuestionImageProps {
  imageUrl?: string | null
  imageAlt?: string | null
  className?: string
}

export function QuestionImage({ imageUrl, imageAlt, className = '' }: QuestionImageProps) {
  const [hasError, setHasError] = useState(false)
  const normalizedImageUrl = imageUrl?.trim()

  useEffect(() => {
    setHasError(false)
  }, [normalizedImageUrl])

  if (!normalizedImageUrl || hasError) return null

  return (
    <figure
      className={`overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-[#F8FBFE] p-2 shadow-[0_6px_20px_rgba(31,61,105,0.05)] ${className}`}
    >
      <div className="flex min-h-40 items-center justify-center rounded-xl bg-white p-2 sm:min-h-52">
        <img
          src={normalizedImageUrl}
          alt={imageAlt?.trim() || 'Hình ảnh minh họa cho câu hỏi'}
          loading="lazy"
          onError={() => setHasError(true)}
          className="max-h-[min(28rem,60vh)] w-full rounded-lg object-contain"
        />
      </div>
    </figure>
  )
}
