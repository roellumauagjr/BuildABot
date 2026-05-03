import React, { useRef, useMemo } from 'react'
import { Canvas } from '@react-three/fiber'
import { OrbitControls, PerspectiveCamera, Float, useGLTF, Center } from '@react-three/drei'
import * as THREE from 'three'

// Consistent material for silhouettes
const silhouetteMaterial = new THREE.MeshPhongMaterial({
  color: "#00F2FF",
  transparent: true,
  opacity: 0.4,
  emissive: "#00F2FF",
  emissiveIntensity: 0.8,
  wireframe: true
})

function GLTFRobot({ modelPath, isUnlocked }) {
  const { scene } = useGLTF(modelPath)
  
  // Procedural scaling mechanism
  const { processedScene, scale } = useMemo(() => {
    // Clone to avoid modifying the original cached asset
    const clonedScene = scene.clone()
    
    // Calculate precise bounding box of the actual mesh data
    const box = new THREE.Box3().setFromObject(clonedScene)
    const size = new THREE.Vector3()
    box.getSize(size)
    
    // Procedural Scaling: Calculate scale based on the largest dimension
    // We want the model to dominate the view. Target size 2.8 units 
    // for a camera distance of 3.5 creates a tight, "premium" fit.
    const maxDim = Math.max(size.x, size.y, size.z)
    const targetScale = 2.6 / (maxDim || 1)
    
    // Apply Silhouette Material if locked
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
      {/* Center component handles the complex alignment logic automatically and perfectly */}
      <Center>
        <primitive 
          object={processedScene} 
          scale={scale}
        />
      </Center>
    </Float>
  )
}

export default function RobotPreview({ robotId, isUnlocked }) {
  const modelPath = robotId === 'bot_lvl2' 
    ? '/models/Robot2.glb' 
    : '/models/Robot1.glb'

  return (
    <div style={{ width: '100%', height: '100%', position: 'relative' }}>
      <Canvas shadows gl={{ antialias: true, alpha: true }}>
        {/* Adjusted Camera for a more intimate, large-scale feel */}
        <PerspectiveCamera makeDefault position={[0, 0, 3.5]} fov={50} />
        
        {/* High-quality lighting setup to show off the models */}
        <ambientLight intensity={1.5} />
        <pointLight position={[10, 10, 10]} intensity={2} />
        <spotLight position={[-10, 10, 10]} angle={0.15} penumbra={1} intensity={1.5} castShadow />
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
    </div>
  )
}

// Preload models for better UX
useGLTF.preload('/models/Robot1.glb')
useGLTF.preload('/models/Robot2.glb')
