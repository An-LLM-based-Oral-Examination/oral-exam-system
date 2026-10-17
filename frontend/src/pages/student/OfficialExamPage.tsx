import { useState } from 'react'
import { Building2, CalendarDays, Clock3, MapPin } from 'lucide-react'
import { OverlayModal } from '@/components/ui/OverlayDrawer'
import { cn } from '@/lib/utils'

interface ScheduledExamItem {
  id: string
  subjectCode: string
  subjectName: string
  shiftName: string
  examDateLabel: string
  timeRangeLabel: string
  room: string
  building: string
  urgencyTone: 'amber' | 'emerald'
  urgencyLabel: string
}

const SCHEDULE_ITEMS: ScheduledExamItem[] = [
  {
    id: 'sched-prn231',
    subjectCode: 'PRN231',
    subjectName: 'Building Cross-Platform Back-End Applications with .NET',
    shiftName: 'Kíp 1',
    examDateLabel: '24/10/2026',
    timeRangeLabel: '08:00 – 09:30',
    room: 'Lab 301',
    building: 'Tòa nhà Innovation · ĐH FPT TP.HCM',
    urgencyTone: 'amber',
    urgencyLabel: 'Sắp diễn ra · Đáng chú ý',
  },
  {
    id: 'sched-swd392',
    subjectCode: 'SWD392',
    subjectName: 'Software Architecture and Design',
    shiftName: 'Kíp 3',
    examDateLabel: '28/10/2026',
    timeRangeLabel: '13:30 – 15:00',
    room: 'Lab A305',
    building: 'Tòa nhà Alpha · ĐH FPT TP.HCM',
    urgencyTone: 'emerald',
    urgencyLabel: 'Đã xếp lịch · Không gấp',
  },
]

export function OfficialExamPage() {
  const [selectedSchedule, setSelectedSchedule] = useState<ScheduledExamItem | null>(null)

  return (
    <div lang="vi" className="space-y-4 pb-6 font-sans">
      <section className="rounded-2xl border border-slate-200 bg-gradient-to-br from-[#FFF7ED]/80 via-white to-amber-50/30 p-5 shadow-xs">
        <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-center">
          <div>
            <h1 className="text-xl font-semibold text-slate-900">Lịch thi vấn đáp chính thức</h1>
            <p className="mt-1 text-xs text-slate-500">
              Thông báo thời gian và địa điểm phòng thi chính thức học kỳ Fall 2026.
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2 text-xs">
            <span className="inline-flex items-center gap-1.5 rounded-lg border border-amber-200 bg-amber-50 px-2.5 py-1 font-semibold text-amber-800">
              <span className="size-2 rounded-full bg-amber-500 animate-pulse" />
              Sắp diễn ra
            </span>
            <span className="inline-flex items-center gap-1.5 rounded-lg border border-emerald-200 bg-emerald-50 px-2.5 py-1 font-semibold text-emerald-700">
              <span className="size-2 rounded-full bg-emerald-500" />
              Đã xếp lịch
            </span>
          </div>
        </div>
      </section>

      <section
        aria-label="Danh sách thông báo lịch thi"
        className="grid grid-cols-1 gap-4 md:grid-cols-2"
      >
        {SCHEDULE_ITEMS.map((item) => {
          const isAmber = item.urgencyTone === 'amber'
          return (
            <button
              key={item.id}
              type="button"
              onClick={() => setSelectedSchedule(item)}
              className={cn(
                'w-full cursor-pointer rounded-2xl border border-slate-200 border-l-4 bg-white p-5 text-left shadow-xs transition-all hover:-translate-y-0.5 hover:border-slate-300 hover:shadow-md focus-visible:outline-2 focus-visible:outline-[#F26F21]',
                isAmber ? 'border-l-amber-500' : 'border-l-emerald-500',
              )}
            >
              <div>
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="inline-block rounded-md border border-slate-200 bg-slate-100 px-2.5 py-0.5 text-xs font-semibold text-slate-800">
                    {item.subjectCode}
                  </span>
                  <span
                    className={cn(
                      'inline-flex items-center gap-1.5 rounded-md border px-2.5 py-0.5 text-xs font-semibold',
                      isAmber
                        ? 'border-amber-200 bg-amber-50 text-amber-800'
                        : 'border-emerald-200 bg-emerald-50 text-emerald-700',
                    )}
                  >
                    <span
                      className={cn(
                        'size-1.5 rounded-full',
                        isAmber ? 'bg-amber-500 animate-pulse' : 'bg-emerald-500',
                      )}
                    />
                    {item.urgencyLabel}
                  </span>
                </div>
                <h2 className="mt-2.5 text-base font-semibold text-slate-900">
                  {item.subjectName}
                </h2>
              </div>

              <div
                className={cn(
                  'mt-4 grid grid-cols-3 gap-3 rounded-xl border p-3.5 text-xs',
                  isAmber
                    ? 'border-amber-200/70 bg-amber-50/40'
                    : 'border-emerald-200/70 bg-emerald-50/30',
                )}
              >
                <div>
                  <p className="text-slate-500">Ngày thi</p>
                  <p className="mt-1 font-semibold text-slate-900">{item.examDateLabel}</p>
                </div>
                <div>
                  <p className="text-slate-500">Giờ thi</p>
                  <p className="mt-1 font-semibold text-slate-900">{item.timeRangeLabel}</p>
                </div>
                <div>
                  <p className="text-slate-500">Phòng thi</p>
                  <p
                    className={cn(
                      'mt-1 inline-flex items-center gap-1 rounded-md border px-2 py-0.5 font-semibold',
                      isAmber
                        ? 'border-amber-200 bg-amber-100/70 text-amber-900'
                        : 'border-emerald-200 bg-emerald-100/70 text-emerald-800',
                    )}
                  >
                    {item.room}
                  </p>
                </div>
              </div>
            </button>
          )
        })}
      </section>

      <OverlayModal
        open={Boolean(selectedSchedule)}
        onClose={() => setSelectedSchedule(null)}
        badge={selectedSchedule?.subjectCode}
        title={selectedSchedule?.subjectName ?? 'Chi tiết lịch thi'}
        subtitle="Thông tin thời gian và địa điểm phòng thi chính thức"
        size="md"
        footer={
          <div className="flex justify-end">
            <button
              type="button"
              onClick={() => setSelectedSchedule(null)}
              className="inline-flex min-h-9 items-center justify-center rounded-xl bg-[#F26F21] px-4 text-xs font-semibold text-white transition hover:bg-[#D95B16]"
            >
              Đóng
            </button>
          </div>
        }
      >
        {selectedSchedule &&
          (() => {
            const isAmber = selectedSchedule.urgencyTone === 'amber'
            return (
              <div className="space-y-3 text-sm text-slate-600">
                <div className="flex items-center justify-between rounded-xl border border-slate-200 bg-slate-50 px-3.5 py-2.5 text-xs">
                  <span className="font-medium text-slate-600">Trạng thái lịch thi</span>
                  <span
                    className={cn(
                      'inline-flex items-center gap-1.5 rounded-md border px-2.5 py-0.5 font-semibold',
                      isAmber
                        ? 'border-amber-200 bg-amber-50 text-amber-800'
                        : 'border-emerald-200 bg-emerald-50 text-emerald-700',
                    )}
                  >
                    <span
                      className={cn(
                        'size-1.5 rounded-full',
                        isAmber ? 'bg-amber-500 animate-pulse' : 'bg-emerald-500',
                      )}
                    />
                    {selectedSchedule.urgencyLabel}
                  </span>
                </div>

                <div className="grid grid-cols-3 gap-3 text-xs">
                  <div className="rounded-xl border border-amber-200/80 bg-amber-50/50 p-3.5">
                    <p className="flex items-center gap-1.5 font-medium text-amber-800">
                      <CalendarDays className="size-3.5 text-amber-600" />
                      Ngày thi
                    </p>
                    <p className="mt-1.5 text-sm font-semibold text-slate-900">
                      {selectedSchedule.examDateLabel}
                    </p>
                  </div>

                  <div className="rounded-xl border border-emerald-200/80 bg-emerald-50/50 p-3.5">
                    <p className="flex items-center gap-1.5 font-medium text-emerald-800">
                      <Clock3 className="size-3.5 text-emerald-600" />
                      Giờ thi ({selectedSchedule.shiftName})
                    </p>
                    <p className="mt-1.5 text-sm font-semibold text-slate-900">
                      {selectedSchedule.timeRangeLabel}
                    </p>
                  </div>

                  <div className="rounded-xl border border-amber-200 bg-amber-50 p-3.5">
                    <p className="flex items-center gap-1.5 font-medium text-amber-900">
                      <MapPin className="size-3.5 text-[#F26F21]" />
                      Phòng thi
                    </p>
                    <p className="mt-1.5 text-sm font-semibold text-[#F26F21]">
                      {selectedSchedule.room}
                    </p>
                  </div>
                </div>

                <div className="flex items-start gap-3 rounded-xl border border-slate-200 bg-white p-3.5 text-xs">
                  <span className="flex size-8 shrink-0 items-center justify-center rounded-lg border border-emerald-200 bg-emerald-50 text-emerald-600">
                    <Building2 className="size-4" />
                  </span>
                  <div>
                    <p className="text-slate-500">Địa điểm tòa nhà</p>
                    <p className="mt-0.5 font-semibold text-slate-900">
                      {selectedSchedule.building}
                    </p>
                  </div>
                </div>
              </div>
            )
          })()}
      </OverlayModal>
    </div>
  )
}

