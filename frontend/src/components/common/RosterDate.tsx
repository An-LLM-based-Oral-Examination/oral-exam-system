const dateFormatter = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  timeZone: 'Asia/Ho_Chi_Minh',
})

export function RosterDate({ label, value }: { label: string; value?: string | null }) {
  const date = value ? new Date(value) : null
  return (
    <div className="shrink-0 whitespace-nowrap">
      <p className="text-xs text-[#5b6b84]">{label}</p>
      <p className="text-sm font-semibold tabular-nums text-[#0F2854]">
        {date && Number.isFinite(date.getTime())
          ? <time dateTime={date.toISOString()}>{dateFormatter.format(date)}</time>
          : '—'}
      </p>
    </div>
  )
}
