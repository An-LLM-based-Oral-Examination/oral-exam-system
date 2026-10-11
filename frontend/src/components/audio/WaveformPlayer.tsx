import { FileAudio2, ShieldCheck, Volume2, VolumeX } from 'lucide-react'

interface WaveformPlayerProps {
  audioUrl?: string | null
  hashSha256?: string | null
  studentCode: string
  seatNumber: number
}

function getAudioFileName(seatNumber: number, studentCode: string) {
  return `${String(seatNumber).padStart(2, '0')}_${studentCode}.webm`
}

export function WaveformPlayer({
  audioUrl,
  hashSha256,
  studentCode,
  seatNumber,
}: WaveformPlayerProps) {
  const fileName = getAudioFileName(seatNumber, studentCode)

  return (
    <section
      aria-labelledby="evidence-audio-title"
      className="overflow-hidden rounded-2xl border border-[#dce5f1] bg-white shadow-[0_10px_34px_rgba(31,61,105,0.06)]"
    >
      <div className="flex flex-col gap-3 border-b border-[#e8edf4] px-4 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-5">
        <div className="flex min-w-0 items-center gap-3">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-[#eaf1ff] text-[#245eea]">
            <FileAudio2 className="size-[18px]" aria-hidden="true" />
          </span>
          <div className="min-w-0">
            <h3 id="evidence-audio-title" className="text-sm font-bold text-[#13213a]">
              Bằng chứng âm thanh gốc
            </h3>
            <p className="mt-0.5 truncate font-mono text-[10px] font-semibold text-[#527099]">
              {fileName}
            </p>
          </div>
        </div>

        {hashSha256 ? (
          <span className="inline-flex w-fit items-center gap-1.5 rounded-full border border-[#bfe9dc] bg-[#effbf7] px-2.5 py-1 text-[10px] font-bold text-[#14745d]">
            <ShieldCheck className="size-3.5" aria-hidden="true" />
            SHA-256 đã ghi nhận
          </span>
        ) : (
          <span className="inline-flex w-fit items-center gap-1.5 rounded-full border border-[#f1dec0] bg-[#fff9ed] px-2.5 py-1 text-[10px] font-bold text-[#956319]">
            Chưa có mã niêm phong
          </span>
        )}
      </div>

      <div className="space-y-4 p-4 sm:p-5">
        {audioUrl ? (
          <div className="rounded-2xl border border-[#d9e5f4] bg-[linear-gradient(135deg,#f5f9ff,#edf5ff)] p-4">
            <div className="mb-3 flex items-center gap-2 text-[11px] font-semibold text-[#3e5f89]">
              <Volume2 className="size-4 text-[#245eea]" aria-hidden="true" />
              Nghe lại bản ghi trên Cloudflare R2
            </div>
            <audio className="h-10 w-full" controls preload="metadata" src={audioUrl}>
              Trình duyệt của bạn không hỗ trợ phát tệp âm thanh WebM.
            </audio>
          </div>
        ) : (
          <div className="flex min-h-28 flex-col items-center justify-center rounded-2xl border border-dashed border-[#ccd8e8] bg-[#f8faff] px-5 text-center">
            <VolumeX className="size-6 text-[#8fa0b8]" aria-hidden="true" />
            <p className="mt-2 text-xs font-bold text-[#455873]">Chưa nhận được tệp âm thanh</p>
            <p className="mt-1 max-w-md text-[10px] leading-relaxed text-[#78889e]">
              Hệ thống sẽ hiển thị trình phát khi ca thi trả về URL bằng chứng hợp lệ.
            </p>
          </div>
        )}

        <div className="rounded-xl border border-[#e4eaf2] bg-[#fbfcfe] px-3 py-2.5">
          <p className="text-[9px] font-bold tracking-[0.08em] text-[#7e8da1] uppercase">
            Mã băm toàn vẹn
          </p>
          <p className="mt-1 font-mono text-[10px] leading-relaxed break-all text-[#465a75]">
            {hashSha256 ?? 'Không có dữ liệu SHA-256 để đối chiếu.'}
          </p>
        </div>
      </div>
    </section>
  )
}

export { WaveformPlayer as AudioPlayer }
