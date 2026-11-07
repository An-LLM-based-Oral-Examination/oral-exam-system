import { cn } from '@/lib/utils'

/**
 * Card nhiều màu cho sinh viên — không dùng line accent như giảng viên.
 * Nền pastel + orb mềm góc để tạo chiều sâu.
 */
export const STUDENT_CARD_TONES = [
  {
    shell: 'border-[#8eb8e0]/70 bg-[linear-gradient(165deg,#eef7fc_0%,#d9eef9_55%,#f8fcfe_100%)]',
    hoverBorder: 'hover:border-[#1C4D8D]',
    orbA: 'bg-[#4988C4]/28',
    orbB: 'bg-[#BDE8F5]/55',
  },
  {
    shell: 'border-[#89d0b0]/70 bg-[linear-gradient(165deg,#eefaf4_0%,#d7f3e6_55%,#f7fcfa_100%)]',
    hoverBorder: 'hover:border-[#16836a]',
    orbA: 'bg-[#16836a]/25',
    orbB: 'bg-[#b7e4cf]/55',
  },
  {
    shell: 'border-[#e3bd7b]/70 bg-[linear-gradient(165deg,#fff8eb_0%,#ffecce_55%,#fffdf8_100%)]',
    hoverBorder: 'hover:border-[#c26a12]',
    orbA: 'bg-[#c26a12]/22',
    orbB: 'bg-[#f3d19a]/50',
  },
  {
    shell: 'border-[#b79fe2]/70 bg-[linear-gradient(165deg,#f6f1fc_0%,#ebe4fc_55%,#fbf9fe_100%)]',
    hoverBorder: 'hover:border-[#6d4cc4]',
    orbA: 'bg-[#6d4cc4]/24',
    orbB: 'bg-[#c4afe8]/50',
  },
  {
    shell: 'border-[#83c5da]/70 bg-[linear-gradient(165deg,#eef8fb_0%,#dbf1f8_55%,#f7fcfd_100%)]',
    hoverBorder: 'hover:border-[#0e7490]',
    orbA: 'bg-[#0e7490]/24',
    orbB: 'bg-[#9ad6e6]/50',
  },
  {
    shell: 'border-[#e5a0b2]/70 bg-[linear-gradient(165deg,#fff1f4_0%,#fee3e8_55%,#fffafb_100%)]',
    hoverBorder: 'hover:border-[#c2415b]',
    orbA: 'bg-[#c2415b]/22',
    orbB: 'bg-[#f3c2c8]/50',
  },
  {
    shell: 'border-[#acd198]/70 bg-[linear-gradient(165deg,#f3faeb_0%,#e8f5dd_55%,#fafdf6_100%)]',
    hoverBorder: 'hover:border-[#3d7a2a]',
    orbA: 'bg-[#3d7a2a]/22',
    orbB: 'bg-[#c5e3ae]/50',
  },
  {
    shell: 'border-[#db95ba]/70 bg-[linear-gradient(165deg,#fff1f7_0%,#fee2ee_55%,#fffafc_100%)]',
    hoverBorder: 'hover:border-[#be185d]',
    orbA: 'bg-[#be185d]/22',
    orbB: 'bg-[#f0b4d0]/50',
  },
] as const

export function studentCardToneIndex(seed: string | number) {
  if (typeof seed === 'number') return Math.abs(seed) % STUDENT_CARD_TONES.length
  let hash = 0
  for (let index = 0; index < seed.length; index += 1) {
    hash = (hash * 31 + seed.charCodeAt(index)) >>> 0
  }
  return hash % STUDENT_CARD_TONES.length
}

export function studentCardTone(seed: string | number) {
  return STUDENT_CARD_TONES[studentCardToneIndex(seed)]
}

export function studentCardShellClass(seed: string | number, interactive = true) {
  const tone = studentCardTone(seed)
  return cn(
    'relative flex h-full flex-col overflow-hidden rounded-[22px] border shadow-[0_1px_0_rgba(255,255,255,0.7)_inset,0_10px_22px_rgba(15,40,84,0.11)]',
    tone.shell,
    interactive &&
      cn(
        'transition-[transform,box-shadow,border-color] duration-200 hover:-translate-y-0.5 hover:shadow-[0_16px_28px_rgba(15,40,84,0.15)]',
        tone.hoverBorder,
      ),
  )
}
