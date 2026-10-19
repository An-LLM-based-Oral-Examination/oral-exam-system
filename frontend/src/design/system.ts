export const palette = {
  ink: '#0F2854',
  brand: '#1C4D8D',
  accent: '#4988C4',
  mist: '#BDE8F5',
  sidebar: '#D7F1F8',
  canvas: '#FFFFFF',
  success: '#16836a',
  successBg: '#e8f8f1',
  successBorder: '#b7e4cf',
  danger: '#c2415b',
  dangerBg: '#fdecec',
  dangerBorder: '#f3c2c8',
  muted: '#5b6b84',
} as const

export const ds = {
  page: 'space-y-6',
  banner:
    'relative isolate overflow-hidden rounded-[22px] border border-[#0F2854] bg-[linear-gradient(118deg,#0F2854_0%,#1C4D8D_58%,#4988C4_100%)] shadow-[0_16px_36px_rgba(15,40,84,0.22)]',
  bannerChip:
    'inline-flex items-center gap-2 rounded-full border border-white/25 bg-white/12 px-3 py-1 text-xs font-bold tracking-[0.08em] text-white uppercase',
  bannerTitle:
    'mt-3 text-[clamp(1.6rem,3vw,2.35rem)] leading-[1.08] font-bold tracking-[-0.045em] text-white',
  bannerText: 'mt-2 max-w-[640px] text-sm leading-relaxed text-white/80',
  section:
    'overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-white shadow-[0_1px_0_#fff_inset,0_16px_40px_rgba(15,40,84,0.08)]',
  sectionHeader:
    'border-b border-[#4988C4] bg-[linear-gradient(180deg,#D7F1F8_0%,#F4FBFD_100%)] px-5 py-3 sm:px-6',
  sectionTitleRow: 'flex items-center gap-2.5',
  sectionIconWrap:
    'flex size-8 shrink-0 items-center justify-center rounded-lg bg-[#4988C4]/22 text-[#1C4D8D]',
  sectionIcon: 'size-4 text-[#1C4D8D]',
  sectionTitle: 'text-[22px] font-bold tracking-[-0.02em] text-[#0F2854]',
  sectionHint: 'mt-0.5 text-sm text-[#5b6b84]',
  sectionBody: 'bg-white p-4 sm:p-5',
  primaryButton:
    'inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-sm font-bold text-white shadow-[0_8px_16px_rgba(28,77,141,0.22)] transition hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-45',
  outlineButton:
    'inline-flex min-h-10 items-center justify-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-4 text-sm font-bold text-[#1C4D8D] transition hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-[#1C4D8D]',
  bannerButton:
    'inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-white bg-white px-4 text-sm font-bold text-[#0F2854] transition hover:bg-[#BDE8F5] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white',
  successChip:
    'inline-flex items-center gap-1 rounded-full border border-[#b7e4cf] bg-[#e8f8f1] px-2.5 py-1 text-xs font-bold text-[#16836a]',
  dangerChip:
    'inline-flex items-center gap-1 rounded-full border border-[#f3c2c8] bg-[#fdecec] px-2.5 py-1 text-xs font-bold text-[#c2415b]',
  infoChip:
    'inline-flex items-center gap-1 rounded-full border border-[#4988C4]/30 bg-[#F4FBFD] px-2.5 py-1 text-xs font-bold text-[#1C4D8D]',
  field:
    'min-h-11 w-full rounded-xl border border-[#4988C4] bg-white px-3 text-sm font-semibold text-[#0F2854] outline-none placeholder:font-medium placeholder:text-[#8aa0b8] focus:ring-2 focus:ring-[#4988C4]/30',
} as const
