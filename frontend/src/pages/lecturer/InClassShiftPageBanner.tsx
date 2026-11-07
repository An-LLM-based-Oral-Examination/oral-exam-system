import { ClipboardList, Lock, Presentation, ScanSearch } from 'lucide-react'
import { BannerFact, LecturerPageBanner } from '@/design/ui'

export function InClassShiftPageBanner() {
  return (
    <LecturerPageBanner
      icon={ClipboardList}
      eyebrow="Ca kiểm tra"
      title="Ca kiểm tra trên lớp"
      description="Tạo ca Progress Test / Quiz, chiếu mã OTP xoay 5 phút, hậu kiểm và chốt điểm. Không lưu audio lên R2."
      extras={
        <>
          <BannerFact icon={Presentation}>Mã OTP 5 phút</BannerFact>
          <BannerFact icon={ScanSearch} tone="green">
            Hậu kiểm bài nộp
          </BannerFact>
          <BannerFact icon={Lock} tone="violet">
            Chốt điểm một lần
          </BannerFact>
        </>
      }
    />
  )
}
