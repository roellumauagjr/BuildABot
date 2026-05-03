import React, { useMemo, useEffect } from 'react'
import { Canvas } from '@react-three/fiber'
import { OrbitControls, PerspectiveCamera, Float, useGLTF, Center } from '@react-three/drei'
import * as THREE from 'three'

// Import GLBs as Vite asset URLs — processed through the build pipeline.
// With assetsInlineLimit:600KB they become base64 data URIs, eliminating
// the file:// fetch that Android WebView blocks.
import robot1Url from '../assets/models/Robot1.glb?url'
import robot2Url from '../assets/models/Robot2.glb?url'

// ─── Silhouette material (locked robots) ─────────────────────────────────────
const silhouetteMaterial = new THREE.MeshPhongMaterial({
  color: '#00F2FF',
  transparent: true,
  opacity: 0.4,
  emissive: '#00F2FF',
  emissiveIntensity: 0.8,
  wireframe: true,
})

// ─── Model URL lookup ─────────────────────────────────────────────────────────
function getModelPath(robotId) {
  return robotId === 'bot_lvl2' ? robot2Url : robot1Url
}

// ─── GLTFRobot — loads the actual GLB, applies silhouette if locked ───────────
function GLTFRobot({ modelPath, isUnlocked }) {
  const { scene } = useGLTF(modelPath)

  const { processedScene, scale } = useMemo(() => {
    const clonedScene = scene.clone(true)

    const box = new THREE.Box3().setFromObject(clonedScene)
    const size = new THREE.Vector3()
    box.getSize(size)
    const maxDim = Math.max(size.x, size.y, size.z)
    const targetScale = 2.6 / (maxDim || 1)

    if (!isUnlocked) {
      clonedScene.traverse((node) => {
        if (node.isMesh) {
          node.material = silhouetteMaterial
        }
      })
    }

    return { processedScene: clonedScene, scale: targetScale }
  }, [scene, isUnlocked])

  return (
    <Float speed={2} rotationIntensity={0.5} floatIntensity={0.5}>
      <Center>
        <primitive object={processedScene} scale={scale} />
      </Center>
    </Float>
  )
}

// ─── Error boundary ───────────────────────────────────────────────────────────
class RobotErrorBoundary extends React.Component {
  state = { hasError: false }
  static getDerivedStateFromError() { return { hasError: true } }
  componentDidCatch(err) { console.error('[RobotPreview] Error:', err) }
  render() {
    if (this.state.hasError) {
      return (
        <div style={{ width:'100%', height:'100%', display:'flex',
          alignItems:'center', justifyContent:'center', fontSize:40, opacity:0.4 }}>
          🤖
        </div>
      )
    }
    return this.props.children
  }
}

// ─── Inactive placeholder — zero WebGL cost ───────────────────────────────────
function InactivePlaceholder({ isUnlocked }) {
  const color = isUnlocked ? '#007AFF' : '#00F2FF'
  return (
    <div style={{ width:'100%', height:'100%', display:'flex',
      alignItems:'center', justifyContent:'center', opacity:0.25 }}>
      <div style={{ width:50, height:50, borderRadius:'50%', border:`2px solid ${color}` }} />
    </div>
  )
}

// ─── Active 3D canvas ─────────────────────────────────────────────────────────
function ActiveCanvas({ modelPath, isUnlocked }) {
  useEffect(() => {
    return () => {
      // Free GPU memory when slot unmounts (user navigated away or swiped)
      try { useGLTF.clear(modelPath) } catch (_) {}
    }
  }, [modelPath])

  return (
    <Canvas
      frameloop="demand"
      shadows={false}
      gl={{ antialias: false, alpha: true, powerPreference: 'high-performance' }}
      dpr={[1, 1.5]}
    >
      <PerspectiveCamera makeDefault position={[0, 0, 3.5]} fov={50} />
      <ambientLight intensity={1.5} />
      <pointLight position={[10, 10, 10]} intensity={2} />
      <spotLight position={[-10, 10, 10]} angle={0.15} penumbra={1} intensity={1.5} />
      <directionalLight position={[0, 5, 5]} intensity={1} />

      <React.Suspense fallback={null}>
        <GLTFRobot modelPath={modelPath} isUnlocked={isUnlocked} />
      </React.Suspense>

      <OrbitControls
        enableZoom={false}
        enablePan={false}
        autoRotate
        autoRotateSpeed={4}
        minPolarAngle={Math.PI / 2.5}
        maxPolarAngle={Math.PI / 1.5}
      />
    </Canvas>
  )
}

// ─── Public component ─────────────────────────────────────────────────────────
// active=false → zero-cost placeholder (pass active={activeIndex === idx})
export default function RobotPreview({ robotId, isUnlocked, active = true }) {
  const modelPath = getModelPath(robotId)

  if (!active) return <InactivePlaceholder isUnlocked={isUnlocked} />

  return (
    <RobotErrorBoundary>
      <div style={{ width:'100%', height:'100%' }}>
        <ActiveCanvas modelPath={modelPath} isUnlocked={isUnlocked} />
      </div>
    </RobotErrorBoundary>
  )
}
