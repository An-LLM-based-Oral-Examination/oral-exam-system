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

const panelShadow = 'shadow-[0_1px_0_#fff_inset,0_8px_16px_rgba(15,40,84,0.16)]'

export const ds = {
  page: 'space-y-6',
  panelShadow,
  banner:
    'relative isolate overflow-hidden rounded-[22px] border border-[#0F2854] bg-[linear-gradient(118deg,#0F2854_0%,#1C4D8D_58%,#4988C4_100%)] shadow-[0_16px_36px_rgba(15,40,84,0.22)]',
  bannerChip:
    'inline-flex w-fit items-center gap-2 rounded-full border border-white/35 bg-white/10 px-3 py-1 text-sm font-semibold text-white shadow-[inset_0_1px_0_rgba(255,255,255,0.18)]',
  bannerTitle:
    'mt-3 max-w-2xl text-[clamp(1.4rem,2.4vw,1.9rem)] leading-[1.2] font-bold tracking-[-0.04em] text-white',
  bannerText: 'mt-2.5 max-w-2xl text-sm leading-relaxed text-white/80',
  section:
    `overflow-hidden rounded-2xl border border-[#82aedb]/55 bg-white ${panelShadow}`,
  sectionHeader:
    'rounded-t-2xl border-b border-[#82aedb]/55 bg-[linear-gradient(180deg,#CCE9F6_0%,#F4FBFD_100%)] px-5 py-3 sm:px-6',
  /** Khối heading: hàng icon+title+badge, hint bên dưới căn theo icon. */
  sectionHeading: 'min-w-0',
  sectionMeta: 'min-w-0',
  sectionTitleRow: 'flex flex-wrap items-center gap-2.5',
  sectionIconWrap:
    'flex size-8 shrink-0 items-center justify-center rounded-lg bg-[#4988C4]/22 text-[#1C4D8D]',
  sectionIcon: 'size-4 text-[#1C4D8D]',
  sectionTitle: 'text-xl font-bold tracking-[-0.02em] text-[#0F2854] sm:text-[22px]',
  sectionBadge:
    'rounded-full border border-[#82aedb]/55 bg-white px-2.5 py-0.5 text-xs font-bold text-[#1C4D8D]',
  sectionHint: 'mt-0.5 text-sm text-[#5b6b84]',
  sectionBody: 'rounded-b-2xl bg-white p-4 sm:p-5',
  primaryButton:
    'inline-flex h-10 items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-sm font-bold text-white shadow-[0_8px_16px_rgba(28,77,141,0.22)] transition duration-200 hover:-translate-y-0.5 hover:bg-[#0F2854] hover:shadow-[0_12px_22px_rgba(15,40,84,0.28)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:pointer-events-none disabled:opacity-45 motion-reduce:transform-none',
  outlineButton:
    'inline-flex h-10 items-center justify-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-4 text-sm font-bold text-[#1C4D8D] transition duration-200 hover:-translate-y-0.5 hover:border-[#1C4D8D] hover:bg-[#E7F3FB] hover:shadow-[0_8px_16px_rgba(15,40,84,0.14)] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] disabled:pointer-events-none motion-reduce:transform-none',
  successChip:
    'inline-flex items-center gap-1 rounded-full border border-[#b7e4cf] bg-[#e8f8f1] px-2.5 py-1 text-xs font-bold text-[#16836a]',
  dangerChip:
    'inline-flex items-center gap-1 rounded-full border border-[#f3c2c8] bg-[#fdecec] px-2.5 py-1 text-xs font-bold text-[#c2415b]',
  infoChip:
    'inline-flex items-center gap-1 rounded-full border border-[#82aedb]/55 bg-[#F4FBFD] px-2.5 py-1 text-xs font-bold text-[#1C4D8D]',
  field:
    'min-h-11 w-full rounded-xl border border-[#4988C4] bg-white px-3 text-sm font-semibold text-[#0F2854] outline-none placeholder:font-medium placeholder:text-[#8aa0b8] focus:ring-2 focus:ring-[#4988C4]/30',
  /** Card mềm (lớp/môn/ca): nền mist → trắng + accent trên. */
  softCard:
    'relative flex h-full flex-col overflow-hidden rounded-[22px] border border-[#82aedb]/55 bg-[linear-gradient(180deg,#EAF5FB_0%,#F8FBFD_22%,#FFFFFF_48%,#FFFFFF_100%)]',
  softCardHover:
    'transition duration-200 hover:-translate-y-0.5 hover:border-[#4988C4] hover:bg-[linear-gradient(180deg,#DCEFFA_0%,#F4FBFD_24%,#F8FBFD_52%,#F8FBFD_100%)] hover:shadow-[0_14px_28px_rgba(15,40,84,0.16)]',
  softCardAccentWrap:
    'pointer-events-none absolute inset-x-0 top-0 h-14 overflow-hidden rounded-t-[21px]',
  softCardAccentGlow:
    'absolute inset-x-6 top-0 h-10 bg-[radial-gradient(ellipse_at_top,rgba(73,136,196,0.18)_0%,rgba(189,232,245,0.12)_42%,transparent_72%)]',
  softCardAccentLine:
    'absolute top-0 right-3 left-3 h-[3px] rounded-b-full bg-[linear-gradient(90deg,transparent_0%,rgba(189,232,245,0.35)_8%,rgba(73,136,196,0.38)_18%,rgba(73,136,196,0.52)_50%,rgba(73,136,196,0.38)_82%,rgba(189,232,245,0.35)_92%,transparent_100%)]',
  softCardBody: 'relative flex h-full flex-1 flex-col p-3.5',
} as const

/** Overlay / popup / drawer — dùng chung cho form dialog, confirm, side drawer. */
export const overlay = {
  scrim:
    'fixed inset-0 flex items-end justify-center bg-[#0F2854]/50 p-0 backdrop-blur-[3px] sm:items-center sm:p-5',
  scrimCenter:
    'fixed inset-0 flex items-center justify-center bg-[#0F2854]/50 p-4 backdrop-blur-[3px] sm:p-5',
  scrimDrawer:
    'fixed inset-0 bg-[#0F2854]/45 backdrop-blur-[2px] lg:pointer-events-none lg:bg-transparent lg:backdrop-blur-none',
  panel:
    'flex w-full flex-col overflow-hidden rounded-t-3xl border border-[#82aedb]/55 bg-white shadow-[0_28px_80px_rgba(15,40,84,0.28)] sm:rounded-3xl',
  panelCompact:
    'flex w-full flex-col overflow-hidden rounded-[22px] border border-[#82aedb]/55 bg-white shadow-[0_24px_70px_rgba(15,40,84,0.28)]',
  drawer:
    'fixed inset-y-0 right-0 flex w-full flex-col overscroll-contain border-l border-[#82aedb]/55 bg-white shadow-[-20px_0_60px_rgba(10,28,57,0.2)]',
  header:
    'shrink-0 border-b border-[#82aedb]/55 bg-[linear-gradient(180deg,#D7F1F8_0%,#F4FBFD_100%)] px-5 py-4',
  body: 'min-h-0 flex-1 overflow-y-auto overscroll-contain bg-[#F8FBFD] px-5 pt-4 pb-6',
  footer:
    'shrink-0 flex flex-col-reverse gap-2 border-t border-[#82aedb]/55 bg-[#F4FBFD] px-5 py-3.5 sm:flex-row sm:justify-end sm:px-5',
  close:
    'flex size-9 shrink-0 items-center justify-center rounded-xl border border-[#82aedb]/55 bg-white text-[#5b6b84] transition hover:bg-[#F4FBFD] hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] disabled:opacity-45',
  iconWrap:
    'flex size-10 shrink-0 items-center justify-center rounded-2xl bg-[#1C4D8D] text-white',
  eyebrow: 'text-[11px] font-bold tracking-[0.08em] text-[#1C4D8D] uppercase',
  title: 'mt-0.5 text-lg font-bold tracking-[-0.02em] text-[#0F2854]',
  z: {
    drawer: 'z-40',
    modal: 'z-50',
    nested: 'z-[60]',
    confirm: 'z-[90]',
    popover: 'z-[120]',
    toast: 'z-[240]',
  },
} as const

export const overlaySize = {
  sm: 'max-w-md',
  md: 'max-w-xl',
  lg: 'max-w-2xl',
  xl: 'max-w-3xl',
  '2xl': 'max-w-4xl',
  '3xl': 'max-w-5xl',
  '6xl': 'max-w-6xl',
} as const
