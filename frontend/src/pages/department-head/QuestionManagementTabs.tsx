import { useEffect, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { FileCheck2, Library } from 'lucide-react'
import { ROUTE_PATHS } from '@/routes/routePaths'
import { questionReviewRequestService } from '@/services/questionReviewRequest.service'
import { cn } from '@/lib/utils'

export function QuestionManagementTabs({ revision = 0 }: { revision?: number }) {
  const location = useLocation()
  const bank = location.pathname === ROUTE_PATHS.departmentHead.questionBank
  const [pending, setPending] = useState<number>()
  useEffect(() => {
    const controller = new AbortController()
    void Promise.all(
      ['PENDING', 'IN_PROGRESS'].map((status) =>
        questionReviewRequestService.list({ status, page: 1 }, controller.signal),
      ),
    )
      .then((results) => {
        if (!controller.signal.aborted)
          setPending(results.reduce((sum, page) => sum + page.totalCount, 0))
      })
      .catch(() => {})
    return () => controller.abort()
  }, [revision])

  return (
    <nav
      aria-label="Quản lý câu hỏi"
      className="grid w-full grid-cols-2 gap-1 rounded-2xl border border-[#82aedb]/55 bg-white/80 p-1 shadow-[inset_0_1px_0_rgba(255,255,255,0.9)]"
    >
      {[
        {
          label: 'Yêu cầu duyệt',
          to: ROUTE_PATHS.departmentHead.questionReview,
          active: !bank,
          icon: FileCheck2,
        },
        {
          label: 'Ngân hàng câu hỏi',
          to: ROUTE_PATHS.departmentHead.questionBank,
          active: bank,
          icon: Library,
        },
      ].map(({ label, to, active, icon: Icon }) => (
        <Link
          key={to}
          to={to}
          aria-current={active ? 'page' : undefined}
          className={cn(
            'inline-flex h-10 items-center justify-center gap-2 rounded-[14px] px-3 py-2 text-[15px] font-bold transition duration-200',
            active
              ? 'bg-[#1C4D8D] text-white shadow-[0_8px_18px_rgba(28,77,141,0.28)]'
              : 'text-[#5f7088] hover:bg-[#F4FBFD] hover:text-[#0F2854]',
          )}
        >
          <Icon className="size-4 shrink-0" />
          {label}
          {to === ROUTE_PATHS.departmentHead.questionReview &&
            pending !== undefined &&
            pending > 0 && (
              <span
                aria-label={`${pending} yêu cầu chưa xử lý xong`}
                className={cn(
                  'rounded-full px-2 py-0.5 text-xs',
                  active ? 'bg-white/20 text-white' : 'bg-[#E7F3FB] text-[#1C4D8D]',
                )}
              >
                {pending}
              </span>
            )}
        </Link>
      ))}
    </nav>
  )
}
