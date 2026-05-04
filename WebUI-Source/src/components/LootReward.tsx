import React, { Suspense } from 'react'
import { Canvas } from '@react-three/fiber'
import { OrbitControls, Float, Sparkles, PerspectiveCamera } from '@react-three/drei'
import { motion } from 'motion/react'
import { COLORS } from '../constants/theme'

// ─────────────────────────────────────────────────────────────────────────────
// LootReward
//
// Accepts `type` as ANY of:
//   'plastic' | 'PlasticBottle'  → shows Plastic Bottle 3D
//   'metal'   | 'MetalCan'       → shows Metal Can 3D
//   'paper'   | 'PaperCup'       → shows Paper Cup 3D
//
// 3D models are 100% procedural Three.js geometry — zero CDN requests.
// No Environment preset (downloads HDR from GitHub CDN, fails on Android WebView).
// No ContactShadows (requires Environment).
// ─────────────────────────────────────────────────────────────────────────────

interface LootRewardProps {
  type: string   // accepts both 'plastic' and 'PlasticBottle' etc.
  onConfirm: () => void
  onDiscard: () => void
}

// Normalise the raw material string coming from Unity into one of our known keys
function normaliseType(raw: string): 'plastic' | 'metal' | 'paper' {
  if (!raw) return 'plastic'
  const t = raw.toLowerCase()
  if (t === 'plastic' || t.includes('plastic') || t.includes('bottle')) return 'plastic'
  if (t === 'metal'   || t.includes('metal')   || t.includes('can'))    return 'metal'
  if (t === 'paper'   || t.includes('paper')   || t.includes('cup') || t.includes('mug')) return 'paper'
  return 'plastic'
}

// ─── 3D Models (procedural — no external assets) ──────────────────────────

function PlasticBottle() {
  return (
    <group>
      {/* Body */}
      <mesh position={[0, -0.2, 0]} castShadow>
        <cylinderGeometry args={[0.35, 0.4, 0.8, 10]} />
        <meshStandardMaterial color="#007AFF" roughness={0.1} metalness={0.2} transparent opacity={0.85} />
      </mesh>
      {/* Shoulder taper */}
      <mesh position={[0, 0.25, 0]} castShadow>
        <cylinderGeometry args={[0.15, 0.35, 0.3, 10]} />
        <meshStandardMaterial color="#007AFF" roughness={0.1} transparent opacity={0.85} />
      </mesh>
      {/* Neck */}
      <mesh position={[0, 0.45, 0]} castShadow>
        <cylinderGeometry args={[0.14, 0.14, 0.12, 10]} />
        <meshStandardMaterial color="#007AFF" roughness={0.1} transparent opacity={0.85} />
      </mesh>
      {/* Cap */}
      <mesh position={[0, 0.56, 0]} castShadow>
        <cylinderGeometry args={[0.16, 0.16, 0.1, 10]} />
        <meshStandardMaterial color="#ffffff" roughness={0.3} />
      </mesh>
      {/* Label band */}
      <mesh position={[0, -0.12, 0]}>
        <cylinderGeometry args={[0.37, 0.42, 0.32, 10]} />
        <meshStandardMaterial color="#ffffff" roughness={0.5} />
      </mesh>
    </group>
  )
}

function MetalCan() {
  return (
    <group>
      {/* Main body */}
      <mesh castShadow>
        <cylinderGeometry args={[0.42, 0.42, 1.0, 14]} />
        <meshStandardMaterial color="#c8c8cc" metalness={0.95} roughness={0.08} />
      </mesh>
      {/* Top lid */}
      <mesh position={[0, 0.51, 0]}>
        <cylinderGeometry args={[0.40, 0.40, 0.04, 14]} />
        <meshStandardMaterial color="#aaaaaa" metalness={1} roughness={0.05} />
      </mesh>
      {/* Pull tab */}
      <mesh position={[0.12, 0.55, 0]} rotation={[0, 0, 0.2]}>
        <boxGeometry args={[0.22, 0.03, 0.09]} />
        <meshStandardMaterial color="#888888" metalness={1} roughness={0.1} />
      </mesh>
      {/* Tab hole ring */}
      <mesh position={[0.04, 0.53, 0]} rotation={[Math.PI / 2, 0, 0]}>
        <torusGeometry args={[0.06, 0.015, 8, 12]} />
        <meshStandardMaterial color="#999" metalness={1} roughness={0.1} />
      </mesh>
      {/* Ridge rings */}
      {[-0.35, -0.12, 0.12, 0.35].map((y, i) => (
        <mesh key={i} position={[0, y, 0]} rotation={[Math.PI / 2, 0, 0]}>
          <torusGeometry args={[0.42, 0.018, 6, 14]} />
          <meshStandardMaterial color="#999" metalness={1} roughness={0.05} />
        </mesh>
      ))}
    </group>
  )
}

function PaperCup() {
  return (
    <group>
      {/* Cup body (tapered) */}
      <mesh position={[0, -0.05, 0]} castShadow>
        <cylinderGeometry args={[0.48, 0.34, 1.1, 16]} />
        <meshStandardMaterial color="#f0f0f0" roughness={0.9} />
      </mesh>
      {/* Sleeve */}
      <mesh position={[0, -0.12, 0]}>
        <cylinderGeometry args={[0.44, 0.39, 0.5, 16]} />
        <meshStandardMaterial color="#34C759" roughness={0.8} />
      </mesh>
      {/* Rim */}
      <mesh position={[0, 0.5, 0]} rotation={[Math.PI / 2, 0, 0]}>
        <torusGeometry args={[0.48, 0.04, 8, 16]} />
        <meshStandardMaterial color="#dddddd" roughness={0.9} />
      </mesh>
      {/* Lid */}
      <mesh position={[0, 0.58, 0]}>
        <cylinderGeometry args={[0.5, 0.48, 0.08, 16]} />
        <meshStandardMaterial color="#888888" roughness={0.3} transparent opacity={0.6} />
      </mesh>
      {/* Sip spout */}
      <mesh position={[0.1, 0.64, 0]}>
        <boxGeometry args={[0.18, 0.04, 0.1]} />
        <meshStandardMaterial color="#777" roughness={0.3} transparent opacity={0.6} />
      </mesh>
    </group>
  )
}

function ItemModel({ type }: { type: 'plastic' | 'metal' | 'paper' }) {
  if (type === 'plastic') return <PlasticBottle />
  if (type === 'metal')   return <MetalCan />
  if (type === 'paper')   return <PaperCup />
  return null
}

// ─── Component ────────────────────────────────────────────────────────────────

export default function LootReward({ type, onConfirm, onDiscard }: LootRewardProps) {
  const normType = normaliseType(type)

  const materialData = {
    plastic: { label: 'PLASTIC BOTTLE', color: '#007AFF', reward: '5-8 SC', desc: 'Recyclable Plastic' },
    metal:   { label: 'METAL CAN',      color: '#FF9500', reward: '8-12 SC', desc: 'Recyclable Aluminum' },
    paper:   { label: 'PAPER CUP',      color: '#34C759', reward: '3-5 SC', desc: 'Recyclable Paper' },
  }

  const data = materialData[normType]

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      style={{
        position: 'fixed',
        inset: 0,
        background: `radial-gradient(circle at center, ${data.color}30 0%, rgba(0,0,0,0.96) 65%)`,
        zIndex: 1000,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      {/* 3D Canvas — pure procedural geometry, zero CDN requests */}
      <div style={{ width: '100%', height: '52vh' }}>
        <Canvas>
          <PerspectiveCamera makeDefault position={[0, 0.3, 3.8]} fov={46} />

          {/* Lighting — all local, no HDR/CDN */}
          <ambientLight intensity={0.6} />
          <directionalLight position={[4, 6, 4]}  intensity={1.4} color="#ffffff" />
          <directionalLight position={[-4, 2, -2]} intensity={0.5} color={data.color} />
          <pointLight        position={[0, -2, 2]}  intensity={0.6} color={data.color} />

          <Suspense fallback={null}>
            <Float speed={4} rotationIntensity={1.8} floatIntensity={1.5}>
              <ItemModel type={normType} />
              <Sparkles count={40} scale={2.2} size={2.5} speed={0.3} color={data.color} />
            </Float>
          </Suspense>

          <OrbitControls enableZoom={false} autoRotate autoRotateSpeed={5} />
        </Canvas>
      </div>

      {/* Info card */}
      <motion.div
        initial={{ y: 40, opacity: 0 }}
        animate={{ y: 0,  opacity: 1 }}
        transition={{ delay: 0.35, type: 'spring', stiffness: 280 }}
        style={{
          textAlign: 'center',
          padding: '22px 40px 28px',
          background: 'rgba(255,255,255,0.06)',
          backdropFilter: 'blur(20px)',
          borderRadius: 28,
          border: `1px solid ${data.color}30`,
          marginTop: -16,
          width: '88%',
          maxWidth: 360,
        }}
      >
        <p style={{ color: 'rgba(255,255,255,0.45)', fontSize: 11, letterSpacing: 3, fontWeight: 800, margin: '0 0 6px' }}>
          OBJECT CLASSIFIED
        </p>
        <h1 style={{ color: '#fff', fontSize: 28, fontWeight: 900, margin: '0 0 4px', letterSpacing: -0.5 }}>
          {data.label}
        </h1>
        <p style={{ color: data.color, fontSize: 13, fontWeight: 600, margin: '0 0 18px', opacity: 0.8 }}>
          {data.desc}
        </p>

        {/* Reward badge */}
        <div style={{
          display: 'inline-flex', alignItems: 'center', gap: 8,
          padding: '10px 22px',
          background: `${data.color}20`,
          border: `1px solid ${data.color}40`,
          borderRadius: 18,
          marginBottom: 24,
        }}>
          <span style={{ fontSize: 20 }}>⚡</span>
          <span style={{ color: data.color, fontWeight: 800, fontSize: 18 }}>+{data.reward}</span>
        </div>

        {/* Buttons */}
        <div style={{ display: 'flex', gap: 10 }}>
          <motion.button
            whileTap={{ scale: 0.93 }}
            onClick={onDiscard}
            style={{
              flex: 1,
              padding: '16px 0',
              borderRadius: 18,
              background: 'rgba(255,255,255,0.09)',
              color: 'rgba(255,255,255,0.55)',
              border: '1px solid rgba(255,255,255,0.12)',
              fontSize: 15,
              fontWeight: 800,
              cursor: 'pointer',
            }}
          >
            DISCARD
          </motion.button>

          <motion.button
            whileTap={{ scale: 0.93 }}
            onClick={onConfirm}
            style={{
              flex: 2,
              padding: '16px 0',
              borderRadius: 18,
              background: data.color,
              color: '#fff',
              border: 'none',
              fontSize: 17,
              fontWeight: 800,
              cursor: 'pointer',
              boxShadow: `0 8px 28px ${data.color}50`,
            }}
          >
            COLLECT
          </motion.button>
        </div>
      </motion.div>
    </motion.div>
  )
}
