// Haptic feedback using Vibration API

type HapticType = 'light' | 'medium' | 'heavy' | 'success' | 'warning' | 'error'

export function haptic(type: HapticType = 'light'): void {
  if (typeof navigator === 'undefined') return
  if (!navigator.vibrate) return
  
  const patterns: Record<HapticType, number | number[]> = {
    light: 10,
    medium: 20,
    heavy: 40,
    success: [0, 10, 50, 10],
    warning: [0, 20, 50, 20],
    error: [0, 30, 50, 30, 50, 30]
  }
  
  navigator.vibrate(patterns[type])
}

export function playSound(type: 'tap' | 'success' | 'error' = 'tap'): void {
  haptic(type === 'tap' ? 'light' : type === 'success' ? 'success' : 'error')
}