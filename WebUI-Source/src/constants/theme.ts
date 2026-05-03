// Apple HIG-inspired design system with iOS system colors
export const COLORS = {
  // Base
  background: '#FFFFFF',
  backgroundSecondary: '#F5F5F7',
  backgroundTertiary: '#F4F5F9',
  
  // iOS System Colors
  primary: '#007AFF',
  secondary: '#34C759',
  tertiary: '#FF9500',
  quaternary: '#FF2D55',
  purple: '#AF52DE',
  red: '#FF3B30',
  orange: '#FF9500',
  yellow: '#FFCC00',
  
  // Neutrals (iOS grays)
  gray1: '#8E8E93',
  gray2: '#AEAEB2',
  gray3: '#C7C7CC',
  gray4: '#D1D1D6',
  gray5: '#E5E5EA',
  gray6: '#F2F2F7',
  
  // Text
  textPrimary: '#000000',
  textSecondary: 'rgba(60, 60, 67, 0.6)',
  textTertiary: 'rgba(60, 60, 67, 0.3)',
  
  // Material Colors
  plastic: '#007AFF',
  metal: '#FF9500',
  paper: '#34C759',
} as const

export const SPACING = {
  xs: 4,
  sm: 8,
  md: 16,
  lg: 24,
  xl: 32,
  xxl: 48,
} as const

export const RADIUS = {
  sm: 8,
  md: 12,
  lg: 16,
  xl: 20,
  xxl: 24,
  full: 9999,
} as const

export const SHADOW = {
  sm: '0 1px 2px rgba(0,0,0,0.1)',
  md: '0 4px 12px rgba(0,0,0,0.08)',
  lg: '0 8px 24px rgba(0,0,0,0.12)',
  xl: '0 16px 48px rgba(0,0,0,0.16)',
} as const

export const TYPOGRAPHY = {
  largeTitle: { size: 34, weight: 700 as const, lineHeight: 41 },
  title1: { size: 28, weight: 700 as const, lineHeight: 34 },
  title2: { size: 22, weight: 700 as const, lineHeight: 28 },
  title3: { size: 20, weight: 600 as const, lineHeight: 25 },
  headline: { size: 17, weight: 600 as const, lineHeight: 22 },
  body: { size: 17, weight: 400 as const, lineHeight: 22 },
  callout: { size: 16, weight: 400 as const, lineHeight: 21 },
  subhead: { size: 15, weight: 400 as const, lineHeight: 20 },
  footnote: { size: 13, weight: 400 as const, lineHeight: 18 },
  caption1: { size: 12, weight: 400 as const, lineHeight: 16 },
  caption2: { size: 11, weight: 400 as const, lineHeight: 13 },
} as const

export const ANIMATION = {
  springBouncy: { type: 'spring', stiffness: 300, damping: 20 },
  springSmooth: { type: 'spring', stiffness: 200, damping: 25 },
  springGentle: { type: 'spring', stiffness: 150, damping: 30 },
  fast: 0.15,
  normal: 0.3,
  slow: 0.5,
} as const