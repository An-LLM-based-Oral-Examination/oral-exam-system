/** Shared gradient, border and shadow palette for colored content cards. */
export const coloredCardTones = {
  blue: 'border-[#88b2de]/55 bg-[linear-gradient(145deg,#e9f5fc_0%,#cae3f5_100%)] shadow-[0_14px_28px_rgba(28,77,141,0.18)]',
  green: 'border-[#82cfad]/55 bg-[linear-gradient(145deg,#e8f7f1_0%,#c6ebd9_100%)] shadow-[0_14px_28px_rgba(22,131,106,0.16)]',
  violet: 'border-[#c4afe8]/55 bg-[linear-gradient(145deg,#f2eafc_0%,#ddcef7_100%)] shadow-[0_14px_28px_rgba(109,76,196,0.16)]',
  amber: 'border-[#e8c77f]/55 bg-[linear-gradient(145deg,#fff8e7_0%,#f8e4b2_100%)] shadow-[0_14px_28px_rgba(194,106,18,0.16)]',
  rose: 'border-[#efb5d3]/55 bg-[linear-gradient(145deg,#fff0f6_0%,#f8d5e5_100%)] shadow-[0_14px_28px_rgba(194,65,91,0.16)]',
} as const

/** Stronger backgrounds for selected cards; keep the shared border and shadow. */
export const selectedCardBackgrounds = {
  blue: 'bg-[linear-gradient(145deg,#d8ecfa_0%,#b4d7ef_100%)]',
  green: 'bg-[linear-gradient(145deg,#e0f4eb_0%,#b9e4d0_100%)]',
  violet: 'bg-[linear-gradient(145deg,#e9ddf9_0%,#cbb6ef_100%)]',
  amber: 'bg-[linear-gradient(145deg,#fff4db_0%,#f5dca0_100%)]',
  rose: 'bg-[linear-gradient(145deg,#fde8f1_0%,#f4c8dc_100%)]',
} as const
