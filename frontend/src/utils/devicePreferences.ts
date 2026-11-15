const STORAGE_KEY = 'oral_exam_device_preferences'

export interface DevicePreferences {
  autoReadQuestions: boolean
  confirmBeforeLeavingMockExam: boolean
}

export const DEFAULT_DEVICE_PREFERENCES: DevicePreferences = {
  autoReadQuestions: false,
  confirmBeforeLeavingMockExam: true,
}

export function readDevicePreferences(): DevicePreferences {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return DEFAULT_DEVICE_PREFERENCES
    const parsed = JSON.parse(raw) as Partial<DevicePreferences>
    return {
      autoReadQuestions:
        typeof parsed.autoReadQuestions === 'boolean'
          ? parsed.autoReadQuestions
          : DEFAULT_DEVICE_PREFERENCES.autoReadQuestions,
      confirmBeforeLeavingMockExam:
        typeof parsed.confirmBeforeLeavingMockExam === 'boolean'
          ? parsed.confirmBeforeLeavingMockExam
          : DEFAULT_DEVICE_PREFERENCES.confirmBeforeLeavingMockExam,
    }
  } catch {
    return DEFAULT_DEVICE_PREFERENCES
  }
}

export function updateDevicePreferences(
  changes: Partial<DevicePreferences>,
): DevicePreferences {
  const next = { ...readDevicePreferences(), ...changes }
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(next))
  } catch {
    // Trình duyệt có thể chặn storage; vẫn áp dụng cho phiên giao diện hiện tại.
  }
  return next
}
