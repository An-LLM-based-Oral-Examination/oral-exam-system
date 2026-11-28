import { Link } from 'react-router-dom'
import { Bell, BookOpen, FileCheck2, SlidersHorizontal } from 'lucide-react'
import { PageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { ROUTE_PATHS } from '@/routes/routePaths'

export function DepartmentHeadDashboardPage() {
  return (
    <div className={ds.page}>
      <PageBanner
        eyebrow="CỔNG TRƯỞNG BM"
        title="Điều hành học thuật"
        description="Quản lý môn học, duyệt câu hỏi và chuẩn bị đề thi cho các môn phụ trách."
      />
      <section className={ds.section} aria-labelledby="department-head-workspace-title">
        <SectionHeader
          icon={FileCheck2}
          title="Không gian nghiệp vụ"
          hint="Mỗi thao tác được giới hạn theo vai trò Trưởng Bộ môn"
        />
        <div className="grid gap-3 p-5 sm:grid-cols-3">
          <Link to={ROUTE_PATHS.departmentHead.questionReview} className={ds.outlineButton}>
            <FileCheck2 className="size-4" aria-hidden="true" />
            Duyệt câu hỏi và đề
          </Link>
          <Link to="/department-head/exam-structures" className={ds.outlineButton}>
            <SlidersHorizontal className="size-4" aria-hidden="true" />
            Chuẩn bị đề thi
          </Link>
          <Link to={ROUTE_PATHS.departmentHead.courses} className={ds.outlineButton}>
            <BookOpen className="size-4" aria-hidden="true" />
            Quản lý môn học &amp; Phân công GV
          </Link>
        </div>
        <div className="mx-5 mb-5 flex items-start gap-2.5 rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] px-4 py-3 text-xs font-medium text-[#52657d]">
          <Bell className="mt-0.5 size-4 shrink-0 text-[#1C4D8D]" aria-hidden="true" />
          <p>
            Thông báo phát sinh đơn phúc khảo trong môn phụ trách được gửi tự động về hộp thư Thông
            báo trên thanh tiêu đề; quyền phân công và quyết định phúc khảo thuộc Cán bộ Khảo thí.
          </p>
        </div>
      </section>
    </div>
  )
}
