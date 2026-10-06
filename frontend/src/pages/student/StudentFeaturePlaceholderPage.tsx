import { Link } from 'react-router-dom'
import { ArrowLeft, BarChart3, CalendarCheck2, type LucideIcon } from 'lucide-react'

interface StudentFeaturePlaceholderPageProps {
  title: string
  description: string
  icon: 'exam' | 'results'
}

const FEATURE_ICONS: Record<StudentFeaturePlaceholderPageProps['icon'], LucideIcon> = {
  exam: CalendarCheck2,
  results: BarChart3,
}

export function StudentFeaturePlaceholderPage({
  title,
  description,
  icon,
}: StudentFeaturePlaceholderPageProps) {
  const Icon = FEATURE_ICONS[icon]

  return (
    <section className="flex min-h-[calc(100dvh-9rem)] items-center justify-center py-8">
      <div className="w-full max-w-xl rounded-3xl border border-[#dfe7f2] bg-white p-7 text-center shadow-[0_18px_55px_rgba(31,61,105,0.08)] sm:p-10">
        <div className="mx-auto flex size-16 items-center justify-center rounded-2xl bg-[#edf3ff] text-[#245eea]">
          <Icon className="size-7" />
        </div>
        <h1 className="mt-5 text-2xl font-bold tracking-[-0.04em] text-[#0b1830]">{title}</h1>
        <p className="mx-auto mt-2 max-w-md text-sm leading-relaxed text-[#68778c]">
          {description}
        </p>
        <Link
          to="/student/overview"
          className="mt-6 inline-flex min-h-11 items-center gap-2 rounded-xl bg-[#245eea] px-5 text-sm font-semibold text-white transition hover:bg-[#194fcf] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea]"
        >
          <ArrowLeft className="size-4" />
          Về trang Tổng quan
        </Link>
      </div>
    </section>
  )
}
