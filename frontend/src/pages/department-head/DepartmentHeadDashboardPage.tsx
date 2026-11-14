import { Link } from 'react-router-dom'
import { ClipboardCheck, FileCheck2 } from 'lucide-react'
import { PageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { ROUTE_PATHS } from '@/routes/routePaths'

export function DepartmentHeadDashboardPage() {
  return (
    <div className={ds.page}>
      <PageBanner
        eyebrow="CỔNG TRƯỞNG BỘ MÔN"
        title="Điều hành học thuật"
        description="Quản lý duyệt câu hỏi, cấu trúc đề và quy trình phúc khảo theo phạm vi môn học được phân công."
      />
      <section className={ds.section} aria-labelledby="department-head-workspace-title">
        <SectionHeader
          icon={FileCheck2}
          title="Không gian nghiệp vụ"
          hint="Mỗi thao tác được giới hạn theo vai trò Trưởng Bộ môn"
        />
        <div className="grid gap-3 p-5 sm:grid-cols-2">
          <Link to={ROUTE_PATHS.departmentHead.questionReview} className={ds.outlineButton}>
            <FileCheck2 className="size-4" aria-hidden="true" />
            Duyệt câu hỏi và đề
          </Link>
          <Link to={ROUTE_PATHS.departmentHead.appeals} className={ds.outlineButton}>
            <ClipboardCheck className="size-4" aria-hidden="true" />
            Theo dõi phúc khảo
          </Link>
        </div>
      </section>
    </div>
  )
}
