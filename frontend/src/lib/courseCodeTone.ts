const COURSE_CODE_TONES = [
  'border border-[#4988C4]/35 bg-[#4988C4]/16 text-[#1C4D8D]',
  'border border-[#16836a]/30 bg-[#e8f8f3] text-[#16836a]',
  'border border-[#c26a12]/30 bg-[#fff4e8] text-[#c26a12]',
  'border border-[#6d4cc4]/30 bg-[#f3eefe] text-[#6d4cc4]',
  'border border-[#0e7490]/30 bg-[#e7f6fb] text-[#0e7490]',
  'border border-[#c2415b]/30 bg-[#fdecef] text-[#c2415b]',
  'border border-[#3d7a2a]/30 bg-[#eef8e8] text-[#3d7a2a]',
  'border border-[#be185d]/30 bg-[#fff0f6] text-[#be185d]',
]

function courseToneIndex(code: string) {
  let hash = 0
  for (let index = 0; index < code.length; index += 1) hash = (hash * 31 + code.charCodeAt(index)) >>> 0
  return hash
}

export function courseCodeTone(code: string) {
  return COURSE_CODE_TONES[courseToneIndex(code) % COURSE_CODE_TONES.length]
}

