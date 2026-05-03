import React, { Suspense } from 'react'
import { Canvas } from '@react-three/fiber'
import { OrbitControls, Float, Sparkles, PerspectiveCamera, Environment, ContactShadows } from '@react-three/drei'
import { motion } from 'motion/react'
import { COLORS } from '../constants/theme'

interface LootRewardProps {
  type: 'plastic' | 'metal' | 'paper'
  onConfirm: () => void
  onDiscard: () => void
}

function ItemModel({ type }: { type: string }) {
  // Detailed procedural low-poly models
  if (type === 'plastic') {
    return (
      <group>
        {/* Bottle Body */}
        <mesh position={[0, -0.2, 0]} castShadow>
          <cylinderGeometry args={[0.35, 0.4, 0.8, 8]} />
          <meshStandardMaterial color="#007AFF" roughness={0.1} metalness={0.2} transparent opacity={0.8} flatShading={true} />
        </mesh>
        {/* Bottle Neck */}
        <mesh position={[0, 0.35, 0]} castShadow>
          <cylinderGeometry args={[0.15, 0.35, 0.3, 8]} />
          <meshStandardMaterial color="#007AFF" roughness={0.1} transparent opacity={0.8} flatShading={true} />
        </mesh>
        {/* Bottle Cap */}
        <mesh position={[0, 0.55, 0]} castShadow>
          <cylinderGeometry args={[0.16, 0.16, 0.1, 8]} />
          <meshStandardMaterial color="#fff" flatShading={true} />
        </mesh>
        {/* Label */}
        <mesh position={[0, -0.1, 0]}>
          <cylinderGeometry args={[0.36, 0.41, 0.3, 8]} />
          <meshStandardMaterial color="#fff" flatShading={true} />
        </mesh>
      </group>
    )
  }
  
  if (type === 'metal') {
    return (
      <group>
        {/* Can Body */}
        <mesh castShadow>
          <cylinderGeometry args={[0.45, 0.45, 1, 12]} />
          <meshStandardMaterial color="#e5e5ea" metalness={1} roughness={0.1} flatShading={true} />
        </mesh>
        {/* Can Ridges */}
        {[0.3, 0.1, -0.1, -0.3].map((y, i) => (
          <mesh key={i} position={[0, y, 0]} rotation={[Math.PI/2, 0, 0]}>
            <torusGeometry args={[0.45, 0.02, 8, 12]} />
            <meshStandardMaterial color="#888" metalness={1} flatShading={true} />
          </mesh>
        ))}
        {/* Tab */}
        <mesh position={[0.1, 0.51, 0]} rotation={[0, 0, 0.1]}>
          <boxGeometry args={[0.2, 0.02, 0.1]} />
          <meshStandardMaterial color="#ccc" metalness={1} flatShading={true} />
        </mesh>
      </group>
    )
  }
  
  if (type === 'paper') {
    return (
      <group>
        {/* Cup Body (Tapered) */}
        <mesh position={[0, -0.1, 0]} castShadow>
          <cylinderGeometry args={[0.5, 0.35, 1.1, 16]} />
          <meshStandardMaterial color="#fff" roughness={1} flatShading={true} />
        </mesh>
        {/* Rim */}
        <mesh position={[0, 0.45, 0]} castShadow rotation={[Math.PI/2, 0, 0]}>
          <torusGeometry args={[0.5, 0.04, 8, 16]} />
          <meshStandardMaterial color="#fff" roughness={1} flatShading={true} />
        </mesh>
        {/* Sleeve */}
        <mesh position={[0, -0.1, 0]}>
          <cylinderGeometry args={[0.46, 0.4, 0.5, 16]} />
          <meshStandardMaterial color="#34C759" roughness={0.8} flatShading={true} />
        </mesh>
      </group>
    )
  }

  return null
}

export default function LootReward({ type, onConfirm, onDiscard }: LootRewardProps) {
  const materialData = {
    plastic: { label: 'PLASTIC BOTTLE', color: '#007AFF', reward: '5-8 SC' },
    metal: { label: 'METAL CAN', color: '#FF9500', reward: '8-12 SC' },
    paper: { label: 'PAPER CUP', color: '#34C759', reward: '3-5 SC' }
  }
  
  const data = materialData[type] || materialData.plastic

  return (
    <motion.div 
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      className="loot-overlay"
      style={{
        position: 'fixed',
        inset: 0,
        background: 'radial-gradient(circle at center, rgba(0,122,255,0.3) 0%, rgba(0,0,0,0.95) 70%)',
        zIndex: 1000,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center'
      }}
    >
      <div style={{ width: '100%', height: '50vh' }}>
        <Canvas shadows>
          <PerspectiveCamera makeDefault position={[0, 0, 4]} fov={50} />
          <ambientLight intensity={0.5} />
          <spotLight position={[10, 10, 10]} angle={0.15} penumbra={1} intensity={1} />
          <pointLight position={[-10, -10, -10]} />
          
          <Suspense fallback={null}>
            <Float speed={5} rotationIntensity={2} floatIntensity={2}>
              <ItemModel type={type} />
              <Sparkles count={50} scale={2} size={2} speed={0.4} color={data.color} />
            </Float>
            <Environment preset="city" />
            <ContactShadows opacity={0.4} scale={10} blur={2} far={4.5} />
          </Suspense>
          
          <OrbitControls enableZoom={false} autoRotate autoRotateSpeed={4} />
        </Canvas>
      </div>

      <motion.div 
        initial={{ y: 50, opacity: 0 }}
        animate={{ y: 0, opacity: 1 }}
        transition={{ delay: 0.5, type: 'spring' }}
        className="loot-card"
        style={{
          textAlign: 'center',
          padding: '20px 40px',
          background: 'rgba(255,255,255,0.05)',
          backdropFilter: 'blur(20px)',
          borderRadius: 30,
          border: '1px solid rgba(255,255,255,0.1)',
          marginTop: -20
        }}
      >
        <h3 style={{ color: 'rgba(255,255,255,0.5)', fontSize: 12, letterSpacing: 3, fontWeight: 800, margin: '0 0 8px' }}>
          OBJECT CLASSIFIED
        </h3>
        <h1 style={{ color: '#fff', fontSize: 32, fontWeight: 900, margin: '0 0 16px', letterSpacing: -1 }}>
          {data.label}
        </h1>
        
        <div style={{ 
          display: 'flex', 
          alignItems: 'center', 
          justifyContent: 'center', 
          gap: 12,
          padding: '12px 24px',
          background: `${data.color}20`,
          borderRadius: 20,
          marginBottom: 32,
          border: `1px solid ${data.color}40`
        }}>
          <span style={{ fontSize: 24 }}>⚡</span>
          <span style={{ color: data.color, fontWeight: 800, fontSize: 18 }}>+{data.reward}</span>
        </div>

        <div style={{ display: 'flex', gap: 12, width: '100%', justifyContent: 'center' }}>
          <motion.button
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.95 }}
            onClick={onDiscard}
            style={{
              padding: '18px 30px',
              borderRadius: 20,
              background: 'rgba(255,255,255,0.1)',
              color: 'rgba(255,255,255,0.6)',
              border: '1px solid rgba(255,255,255,0.1)',
              fontSize: 16,
              fontWeight: 800,
              cursor: 'pointer',
            }}
          >
            DISCARD
          </motion.button>

          <motion.button
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.95 }}
            onClick={onConfirm}
            style={{
              padding: '18px 60px',
              borderRadius: 20,
              background: data.color,
              color: '#fff',
              border: 'none',
              fontSize: 18,
              fontWeight: 800,
              cursor: 'pointer',
              boxShadow: `0 10px 30px ${data.color}40`,
              flex: 1
            }}
          >
            COLLECT
          </motion.button>
        </div>
      </motion.div>
    </motion.div>
  )
}
