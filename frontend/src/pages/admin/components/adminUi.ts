export const adminUi = {
  /** Khung nội dung chính (bảng, form card, panel chẩn đoán) */
  card: 'overflow-hidden rounded-2xl border border-[#b6cde2] bg-white shadow-[0_1px_3px_rgba(15,40,84,0.06)]',
  cardPadded:
    'rounded-2xl border border-[#b6cde2] bg-white p-5 shadow-[0_1px_3px_rgba(15,40,84,0.06)]',
  subCard: 'rounded-xl border border-slate-300 bg-[#f7fafd] p-4',
  mobileCard: 'rounded-xl border border-slate-300 bg-white p-4 shadow-[0_1px_2px_rgba(15,40,84,0.04)]',

  /** Thanh tiêu đề phụ của section (khi trang có >= 2 nhóm nội dung) */
  sectionBar:
    'flex flex-col gap-3 border-b border-slate-200 bg-[#f4f8fc] px-5 py-3.5 sm:flex-row sm:items-center sm:justify-between',
  sectionTitle: 'text-base font-bold tracking-[-0.01em] text-[#0F2854]',
  sectionCount:
    'inline-flex items-center rounded-full border border-[#94b8da] bg-white px-2.5 py-0.5 text-xs font-semibold text-[#1C4D8D] tabular-nums',

  /** Thanh công cụ lọc / tìm kiếm */
  toolbar:
    'flex flex-col gap-3 border-b border-slate-200 bg-[#f8fbfe] px-4 py-3.5 sm:flex-row sm:flex-wrap sm:items-center sm:px-5',

  /** Bảng dữ liệu rõ nét */
  table: 'w-full text-left text-sm text-[#0F2854]',
  thead: 'border-b border-slate-300 bg-[#eef5fc] text-xs font-semibold uppercase tracking-[0.03em] text-[#1e3a5f]',
  tbody: 'divide-y divide-slate-200 bg-white',
  tr: 'transition-colors duration-150 hover:bg-[#f2f8fe] motion-reduce:transition-none',

  /** Input / Select / Textarea sắc nét */
  input:
    'h-11 min-h-11 rounded-xl border border-slate-300 bg-white text-sm font-normal text-[#0F2854] placeholder:text-slate-500 focus:border-[#1C4D8D] focus:ring-2 focus:ring-[#1C4D8D]/20',
  textarea:
    'mt-1.5 min-h-20 w-full rounded-xl border border-slate-300 bg-white p-3 text-sm text-[#0F2854] placeholder:text-slate-500 outline-none focus:border-[#1C4D8D] focus:ring-2 focus:ring-[#1C4D8D]/20',
  filterButton:
    'min-h-11 rounded-xl border border-slate-300 bg-white text-sm font-medium text-[#0F2854] hover:border-[#1C4D8D]',
  label: 'block text-sm font-semibold text-[#0F2854]',
  helperText: 'mt-1.5 text-xs leading-relaxed text-slate-600',
  infoBox: 'rounded-xl border border-slate-300 bg-[#f4f8fc] p-3.5 text-xs leading-relaxed text-slate-700',

  /** Nút bấm phân cấp rõ ràng */
  btnPrimary:
    'inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-sm font-semibold text-white shadow-xs transition-colors duration-150 hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50 motion-reduce:transition-none',
  btnSecondary:
    'inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-slate-300 bg-white px-3.5 text-sm font-medium text-[#0F2854] transition-colors duration-150 hover:border-[#1C4D8D] hover:bg-[#eef6fd] hover:text-[#1C4D8D] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50 motion-reduce:transition-none',
  btnSubtle:
    'inline-flex min-h-11 items-center justify-center gap-1.5 rounded-xl border border-slate-300 bg-white px-3.5 text-sm font-medium text-slate-700 transition-colors duration-150 hover:border-slate-400 hover:bg-slate-50 hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50 motion-reduce:transition-none',

  /** Nút thao tác trên hàng bảng */
  btnRowPrimary:
    'inline-flex min-h-9 items-center justify-center gap-1.5 rounded-lg border border-[#94b8da] bg-[#f4f9fe] px-3 text-[13px] font-semibold text-[#1C4D8D] transition-colors duration-150 hover:border-[#1C4D8D] hover:bg-[#e5f1fc] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50 motion-reduce:transition-none',
  btnRowNeutral:
    'inline-flex min-h-9 items-center justify-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 text-[13px] font-medium text-[#0F2854] transition-colors duration-150 hover:border-slate-400 hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50 motion-reduce:transition-none',
  btnRowDanger:
    'inline-flex min-h-9 items-center justify-center gap-1.5 rounded-lg border border-rose-300 bg-rose-50 px-3 text-[13px] font-semibold text-rose-700 transition-colors duration-150 hover:border-rose-400 hover:bg-rose-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-rose-600 disabled:opacity-50 motion-reduce:transition-none',
  btnRowSuccess:
    'inline-flex min-h-9 items-center justify-center gap-1.5 rounded-lg border border-emerald-300 bg-emerald-50 px-3 text-[13px] font-semibold text-emerald-800 transition-colors duration-150 hover:border-emerald-400 hover:bg-emerald-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-700 disabled:opacity-50 motion-reduce:transition-none',

  /** Nút mobile full-height (>= 44px touch target) */
  btnMobilePrimary:
    'inline-flex min-h-11 items-center justify-center gap-1.5 rounded-xl border border-[#94b8da] bg-[#f4f9fe] px-3 text-sm font-semibold text-[#1C4D8D] transition-colors duration-150 hover:border-[#1C4D8D] hover:bg-[#e5f1fc] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50 motion-reduce:transition-none',
  btnMobileNeutral:
    'inline-flex min-h-11 items-center justify-center gap-1.5 rounded-xl border border-slate-300 bg-white px-3 text-sm font-medium text-[#0F2854] transition-colors duration-150 hover:border-slate-400 hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50 motion-reduce:transition-none',
  btnMobileDanger:
    'inline-flex min-h-11 items-center justify-center gap-1.5 rounded-xl border border-rose-300 bg-rose-50 px-3 text-sm font-semibold text-rose-700 transition-colors duration-150 hover:border-rose-400 hover:bg-rose-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-rose-600 disabled:opacity-50 motion-reduce:transition-none',
  btnMobileSuccess:
    'inline-flex min-h-11 items-center justify-center gap-1.5 rounded-xl border border-emerald-300 bg-emerald-50 px-3 text-sm font-semibold text-emerald-800 transition-colors duration-150 hover:border-emerald-400 hover:bg-emerald-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-700 disabled:opacity-50 motion-reduce:transition-none',

  /** Badge trạng thái (bo tròn pill để tách hẳn với nút bấm) */
  badgeInfo:
    'inline-flex items-center gap-1 rounded-full border border-[#94b8da] bg-[#edf5fd] px-2.5 py-0.5 text-xs font-semibold text-[#1C4D8D]',
  badgeSuccess:
    'inline-flex items-center gap-1 rounded-full border border-emerald-300 bg-emerald-50 px-2.5 py-0.5 text-xs font-semibold text-emerald-800',
  badgeWarning:
    'inline-flex items-center gap-1 rounded-full border border-amber-300 bg-amber-50 px-2.5 py-0.5 text-xs font-semibold text-amber-800',
  badgeDanger:
    'inline-flex items-center gap-1 rounded-full border border-rose-300 bg-rose-50 px-2.5 py-0.5 text-xs font-semibold text-rose-800',
  badgeNeutral:
    'inline-flex items-center gap-1 rounded-full border border-slate-300 bg-slate-100 px-2.5 py-0.5 text-xs font-semibold text-slate-700',
} as const
