import { ClipboardCheck, KeyRound, UsersRound } from 'lucide-react'
import { StudentClassPanel } from '@/components/student/StudentClassPanel'
import { ds } from '@/design/system'
import { BannerFact, PageBanner } from '@/design/ui'

export function StudentClassesPage() {
  return (
    <div className={ds.page}>
      <StudentClassPanel renderBanner={(search) => (
        <PageBanner
          className="z-[25] overflow-visible"
          icon={UsersRound}
          eyebrow="Lớp học phần"
          title="Quản lý lớp học"
          description="Tìm lớp theo học kỳ, tham gia bằng mật khẩu hoặc mở chi tiết lớp để xem lịch kiểm tra."
          extras={
            <div className="flex w-full flex-wrap items-center gap-2">
              <div className="w-full sm:w-auto">{search}</div>
              <BannerFact icon={KeyRound} tone="green">Tham gia bằng mật khẩu</BannerFact>
              <BannerFact icon={ClipboardCheck} tone="violet">Xem lịch kiểm tra</BannerFact>
            </div>
          }
        />
      )} />
    </div>
  )
}
