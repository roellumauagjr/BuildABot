import { motion } from 'motion/react'
import { COLORS } from '../constants/theme'

interface StatRadarProps {
  stats: { hp: number; def: number; spd: number; attack: number }
  size?: number
}

export function StatRadar({ stats, size = 180 }: StatRadarProps) {
  const maxStat = 200
  const statsList = [
    { label: 'HP', value: stats?.hp ?? 0 },
    { label: 'DEF', value: stats?.def ?? 0 },
    { label: 'SPD', value: stats?.spd ?? 0 },
    { label: 'ATK', value: stats?.attack ?? 0 }
  ]
  
  const hasStats = statsList.some(s => s.value > 0)
  if (!hasStats) {
    return (
      <svg width={size} height={size}>
        <text 
          x={size/2} 
          y={size/2} 
          textAnchor="middle" 
          fill={COLORS.textSecondary} 
          fontSize={12} 
          fontWeight={500}
          opacity={0.5}
          style={{ letterSpacing: '1px', textTransform: 'uppercase' }}
        >
          Add parts to see stats
        </text>
      </svg>
    )
  }
  
  const angleStep = (Math.PI * 2) / statsList.length
  
  const points = statsList.map((stat, i) => {
    const angle = i * angleStep - Math.PI / 2
    const radius = Math.min((stat.value / maxStat) * (size / 2 - 25), size / 2 - 10)
    return {
      x: size / 2 + Math.cos(angle) * radius,
      y: size / 2 + Math.sin(angle) * radius
    }
  })
  
  const pathData = points.map((p, i) => 
    `${i === 0 ? 'M' : 'L'} ${p.x} ${p.y}`
  ).join(' ') + ' Z'
  
  return (
    <svg width={size} height={size} style={{ overflow: 'visible' }}>
      {/* Background circles */}
      {[0.25, 0.5, 0.75, 1].map((ratio, i) => (
        <circle
          key={i}
          cx={size/2}
          cy={size/2}
          r={ratio * (size/2 - 20)}
          fill="none"
          stroke={COLORS.gray5}
          strokeWidth={1}
        />
      ))}
      
      {/* Axis lines */}
      {statsList.map((_, i) => {
        const angle = i * angleStep - Math.PI / 2
        return (
          <line
            key={i}
            x1={size/2}
            y1={size/2}
            x2={size/2 + Math.cos(angle) * (size/2 - 20)}
            y2={size/2 + Math.sin(angle) * (size/2 - 20)}
            stroke={COLORS.gray5}
            strokeWidth={1}
          />
        )
      })}
      
      {/* Data polygon */}
      <motion.path
        d={pathData}
        fill={`${COLORS.primary}20`}
        stroke={COLORS.primary}
        strokeWidth={2}
        initial={{ opacity: 0, pathLength: 0 }}
        animate={{ opacity: 1, pathLength: 1 }}
        transition={{ duration: 0.5 }}
      />
      
      {/* Data points */}
      {points.map((p, i) => (
        <motion.circle
          key={i}
          cx={p.x}
          cy={p.y}
          r={5}
          fill={COLORS.primary}
          initial={{ scale: 0 }}
          animate={{ scale: 1 }}
          transition={{ delay: 0.2 }}
        />
      ))}
      
      {/* Labels */}
      {statsList.map((stat, i) => {
        const angle = i * angleStep - Math.PI / 2
        const labelRadius = size / 2 + 18
        const x = size / 2 + Math.cos(angle) * labelRadius
        const y = size / 2 + Math.sin(angle) * labelRadius
        return (
          <text
            key={i}
            x={x}
            y={y}
            textAnchor="middle"
            dominantBaseline="middle"
            fontSize={11}
            fontWeight={600}
            fill={COLORS.textSecondary}
          >
            {stat.label}
          </text>
        )
      })}
    </svg>
  )
}