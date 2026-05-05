import React, { useState, useEffect } from 'react'
import MainApp from './components/MainApp'
import { useGameStore } from './stores/useGameStore'
import { AnimatePresence, motion } from 'motion/react'

// Bridge initialization
if (typeof window !== 'undefined') {
  if (!window.Unity) {
    window.Unity = { call: (json) => console.log('[UnityBridge STUB]', json) }
  }
  if (!window.dispatchUnityEvent) {
    window.dispatchUnityEvent = (action, payload) => {
      window.dispatchEvent(new CustomEvent('unityEvent', { detail: { action, payload } }))
    }
  }
  if (!window._unityBridge) {
    window._unityBridge = {
      send: (action, payload) => {
        const envelope = JSON.stringify({ action, payload: JSON.stringify(payload ?? {}) })
        window.Unity.call(envelope)
      },
    }
  }
}

// Material mapping from Unity Roboflow
const MATERIAL_MAP = {
  PlasticBottle: { material: 'plastic', name: 'Plastic Bottle', color: '#007AFF', id: 1, isRecyclable: true },
  MetalCan: { material: 'metal', name: 'Aluminum Can', color: '#FF9500', id: 2, isRecyclable: true },
  PaperCup: { material: 'paper', name: 'Paper Cup', color: '#34C759', id: 3, isRecyclable: true },
  Paper: { material: 'paper', name: 'Paper', color: '#34C759', id: 11, isRecyclable: true },
  CardboardBox: { material: 'paper', name: 'Cardboard', color: '#34C759', id: 4, isRecyclable: true },
  PlasticContainer: { material: 'plastic', name: 'Plastic Container', color: '#007AFF', id: 5, isRecyclable: true },
  MetalBottle: { material: 'metal', name: 'Metal Bottle', color: '#FF9500', id: 6, isRecyclable: true },
  None: { material: null, name: 'Unknown', color: '#8E8E93', id: 0, isRecyclable: false }
}

export default function App() {
  const { addMaterial, addToInventory, addScrap, materials } = useGameStore()
  const [isLoading, setIsLoading] = useState(true)
  
  // Listen for Unity events
  useEffect(() => {
    const handleUnityEvent = (e) => {
      const { action, payload } = e.detail || {}
      if (!action) return
      
      if (action === 'SCAN_COMPLETE') {
        try {
          const data = typeof payload === 'string' ? JSON.parse(payload) : payload
          const materialData = MATERIAL_MAP[data.material] || MATERIAL_MAP['None']
          
          if (materialData.material && data.isRecyclable) {
            addMaterial(materialData.material, 1)
            addToInventory({
              ...materialData,
              displayName: data.displayName || materialData.name,
              rawClass: data.rawClass,
              confidence: data.confidence,
              isRecyclable: data.isRecyclable,
              instanceId: `unity_scan_${Date.now()}_${Math.random()}`
            })
            addScrap(5)
          }
        } catch (err) {
          console.error('Failed to parse scan result:', err)
        }
      }
      
      // Handle Battle Rewards from AR Battle
      if (action === 'BATTLE_REWARD') {
        try {
          const data = typeof payload === 'string' ? JSON.parse(payload) : payload
          
          // Apply material changes (positive for win, negative for loss)
          if (data.plastic !== 0) addMaterial('plastic', data.plastic)
          if (data.metal !== 0) addMaterial('metal', data.metal)
          if (data.paper !== 0) addMaterial('paper', data.paper)
          
          // Show toast notification (you can replace this with your actual toast system)
          console.log(`[BATTLE] ${data.message}`)
          
          // Optional: Add scrap bonus for win
          if (data.won) {
            addScrap(Math.floor(Math.random() * 11) + 10) // +10-20 scrap for win
          }
        } catch (err) {
          console.error('Failed to parse battle reward:', err)
        }
      }
    }
    
    if (typeof window !== 'undefined') {
      window.addEventListener('unityEvent', handleUnityEvent)
      return () => window.removeEventListener('unityEvent', handleUnityEvent)
    }
  }, [addMaterial, addToInventory, addScrap, materials])
  
  // Show loading while Zustand hydrates
  useEffect(() => {
    const timer = setTimeout(() => setIsLoading(false), 1200)
    return () => clearTimeout(timer)
  }, [])
  
  return (
    <div className="app-container">
      <AnimatePresence>
        {isLoading ? (
          <motion.div
            key="loader"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0, scale: 0.95 }}
            transition={{ duration: 0.3 }}
            className="loader-screen"
          >
            <motion.div
              animate={{ 
                scale: [1, 1.1, 1],
                rotate: [0, 5, -5, 0]
              }}
              transition={{ repeat: Infinity, duration: 1.5 }}
              className="loader-icon"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <rect x="4" y="8" width="16" height="8" rx="2" />
                <circle cx="8" cy="12" r="2" />
                <circle cx="16" cy="12" r="2" />
                <path d="M8 16h8" />
              </svg>
            </motion.div>
            <span className="loader-text">Loading...</span>
          </motion.div>
        ) : (
          <motion.div
            key="app"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ duration: 0.4 }}
            className="app-content"
          >
            <MainApp />
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  )
}