import React, { useState, useCallback, useEffect } from 'react'
import { motion, AnimatePresence } from 'motion/react'
import { Icon } from './Icons'
import { useGameStore } from '../stores/useGameStore'
import { useUnityBridge, ACTIONS } from '../hooks/useUnityBridge'
import { COLORS } from '../constants/theme'
import LootReward from './LootReward'

// ─────────────────────────────────────────────────────────────────────────────
// ScannerScreen
//
// Camera strategy: NO getUserMedia. The Unity AR camera passthrough renders
// through the transparent WebView, acting as the live viewfinder.
// Tapping the shutter sends CAPTURE_AND_SCAN to Unity → Unity grabs an
// ARCameraManager CPU image → Roboflow → SCAN_COMPLETE / SCAN_EMPTY back.
// ─────────────────────────────────────────────────────────────────────────────
export default function ScannerScreen({ onCapture, onBack }) {
  const [isProcessing,  setIsProcessing]  = useState(false)
  const [identified,    setIdentified]    = useState(null)
  const [capturedFrame, setCapturedFrame] = useState(null)
  const [scanStage,     setScanStage]     = useState('idle')
  const [analysisError, setAnalysisError] = useState(null)

  const { } = useGameStore()   // state reads only — mutations handled by wrapper
  const sendToUnity = useUnityBridge()

  // ── Shutter ───────────────────────────────────────────────────────────────
  const handleShutter = useCallback(() => {
    if (isProcessing) return
    setScanStage('capturing')
    setIsProcessing(true)
    setAnalysisError(null)

    const inUnityApp =
      typeof window !== 'undefined' &&
      (typeof window.Unity !== 'undefined' || typeof window._unityBridge !== 'undefined')

    if (inUnityApp) {
      // Unity grabs the AR camera frame → encodes to JPEG → Roboflow
      sendToUnity(ACTIONS.CAPTURE_AND_SCAN)
      // Safety fallback at 10s if Unity doesn't respond
      setTimeout(() => {
        setIsProcessing(p => { if (p) simulateFallback(); return p })
      }, 10000)
      return
    }

    // ── Browser / desktop fallback ────────────────────────────────────────
    simulateFallback()
  }, [isProcessing])

  // ── Listen for Unity events ───────────────────────────────────────────────
  useEffect(() => {
    const onUnityEvent = (e) => {
      const { action, payload } = e.detail || {}

      if (action === 'FRAME_CAPTURED' && payload) {
        // Unity sent the freeze-frame — display it while analysis runs
        const p = typeof payload === 'string' ? JSON.parse(payload) : payload
        setCapturedFrame('data:image/jpeg;base64,' + p.base64Image)
        setScanStage('processing')
      } else if (action === 'SCAN_COMPLETE' && payload) {
        const p = typeof payload === 'string' ? JSON.parse(payload) : payload
        setIdentified({
          material:    p.material,
          displayName: p.displayName,
          confidence:  p.confidence,
          isRecyclable: p.isRecyclable,
        })
        setScanStage('loot')
        setIsProcessing(false)
      } else if (action === 'SCAN_EMPTY') {
        setAnalysisError('Nothing recognised. Point camera at recyclable trash and try again.')
        setScanStage('idle')
        setCapturedFrame(null)
        setIsProcessing(false)
      }
    }

    window.addEventListener('unityEvent', onUnityEvent)
    return () => window.removeEventListener('unityEvent', onUnityEvent)
  }, [])

  // ── Fallback (browser / Unity timeout) ────────────────────────────────────
  const simulateFallback = () => {
    const types = ['plastic', 'metal', 'paper']
    const t = types[Math.floor(Math.random() * types.length)]
    const names = { plastic: 'Plastic Bottle', metal: 'Metal Can', paper: 'Paper Cup' }
    setIdentified({ material: t, displayName: names[t], confidence: 0.93, isRecyclable: true })
    setScanStage('loot')
    setIsProcessing(false)
  }

  // ── Navigation / reset ────────────────────────────────────────────────────
  const handleCollect = () => {
    // All inventory/material mutations live in ScannerScreenWrapper (MainApp).
    // Here we just pass the identified item up and reset local UI state.
    onCapture?.(identified)
    resetScanner()
  }

  const resetScanner = () => {
    setIdentified(null)
    setCapturedFrame(null)
    setScanStage('idle')
    setIsProcessing(false)
    setAnalysisError(null)
  }

  // ── Render ────────────────────────────────────────────────────────────────
  // scanner-container is TRANSPARENT — the Unity AR camera background renders
  // through the WebView and acts as the live camera viewfinder.
  return (
    <div className="scanner-container">

      {/* Freeze-frame: shown after Unity sends FRAME_CAPTURED */}
      {capturedFrame && scanStage !== 'idle' && (
        <div className="camera-view">
          <img src={capturedFrame} className="camera-video" alt="Captured frame" />
        </div>
      )}

      {/* Shutter Flash */}
      <AnimatePresence>
        {scanStage === 'capturing' && (
          <motion.div
            initial={{ opacity: 1 }} animate={{ opacity: 0 }} exit={{ opacity: 0 }}
            className="shutter-flash"
          />
        )}
      </AnimatePresence>

      {/* HUD overlay */}
      <div className="scanner-overlay">
        <div className="scanner-top-bar">
          <motion.button whileTap={{ scale: 0.9 }} onClick={onBack} className="icon-btn">
            <Icon name="times" size={20} color="#fff" />
          </motion.button>
          <div className="scanner-status">
            <div className="pulse-dot" />
            <span>AI SCANNER ACTIVE</span>
          </div>
          <div style={{ width: 44 }} />
        </div>

        {scanStage === 'idle' && (
          <>
            <div className="scan-reticle">
              <div className="reticle-corner tl" />
              <div className="reticle-corner tr" />
              <div className="reticle-corner bl" />
              <div className="reticle-corner br" />
              <motion.div
                animate={{ y: [0, 340, 0] }}
                transition={{ repeat: Infinity, duration: 3, ease: 'linear' }}
                className="scan-line"
              />
            </div>

            <div className="bottom-controls">
              <p className="instruction">POSITION TRASH WITHIN FRAME</p>
              <button className="shutter-btn" onClick={handleShutter}>
                <div className="shutter-btn-inner" />
              </button>
            </div>
          </>
        )}

        {isProcessing && (
          <div className="analysis-overlay">
            <div className="ai-brain-anim">
              <Icon name="radar" size={48} color={COLORS.primary} />
            </div>
            <h2>ANALYZING OBJECT...</h2>
            <p>Using YOLO World Real-time Detection</p>
          </div>
        )}

        {analysisError && (
          <div className="analysis-overlay error">
            <Icon name="warning" size={48} color={COLORS.orange} />
            <h2 style={{ color: COLORS.orange }}>SCAN FAILED</h2>
            <p>{analysisError}</p>
            <button onClick={resetScanner} className="retry-btn" style={{ pointerEvents: 'auto' }}>
              Try Again
            </button>
          </div>
        )}
      </div>

      {/* Loot reward */}
      <AnimatePresence>
        {scanStage === 'loot' && identified && (
          <LootReward
            type={identified.material}
            onConfirm={handleCollect}
            onDiscard={resetScanner}
          />
        )}
      </AnimatePresence>

      <style>{`
        .scanner-container {
          position: fixed; inset: 0; z-index: 1000;
          background: transparent;
        }
        .camera-view {
          position: absolute; inset: 0;
          display: flex; align-items: center; justify-content: center;
          background: transparent;
        }
        .camera-video {
          width: 100%; height: 100%; object-fit: cover;
        }
        .scanner-overlay {
          position: absolute; inset: 0; z-index: 10;
          display: flex; flex-direction: column; pointer-events: none;
        }
        .scanner-top-bar {
          padding: 50px 20px 20px;
          display: flex; justify-content: space-between; align-items: center;
          pointer-events: auto;
        }
        .icon-btn {
          width: 44px; height: 44px; border-radius: 14px;
          background: rgba(0,0,0,0.5);
          display: flex; align-items: center; justify-content: center;
          border: none; cursor: pointer;
        }
        .scanner-status {
          display: flex; align-items: center; gap: 8px;
          padding: 8px 16px; background: rgba(0,0,0,0.5);
          border-radius: 20px; color: #fff;
          font-size: 11px; font-weight: 800; letter-spacing: 1px;
        }
        .pulse-dot {
          width: 6px; height: 6px; background: #34C759; border-radius: 50%;
          box-shadow: 0 0 10px #34C759; animation: pulse 2s infinite;
        }
        @keyframes pulse {
          0%   { opacity:1; transform:scale(1); }
          50%  { opacity:0.3; transform:scale(1.5); }
          100% { opacity:1; transform:scale(1); }
        }
        .scan-reticle {
          position: absolute; top: 50%; left: 50%;
          transform: translate(-50%,-50%);
          width: 280px; height: 340px;
          border: 1px solid rgba(255,255,255,0.1);
        }
        .reticle-corner { position: absolute; width: 30px; height: 30px; border: 4px solid #fff; }
        .tl { top:-2px; left:-2px;   border-right:none; border-bottom:none; border-top-left-radius:12px; }
        .tr { top:-2px; right:-2px;  border-left:none;  border-bottom:none; border-top-right-radius:12px; }
        .bl { bottom:-2px; left:-2px;  border-right:none; border-top:none; border-bottom-left-radius:12px; }
        .br { bottom:-2px; right:-2px; border-left:none;  border-top:none; border-bottom-right-radius:12px; }
        .scan-line {
          position: absolute; top: 0; left: 0; width: 100%; height: 2px;
          background: linear-gradient(90deg, transparent, ${COLORS.primary}, transparent);
          box-shadow: 0 0 15px ${COLORS.primary};
        }
        .bottom-controls {
          position: absolute; bottom: 60px; left: 0; right: 0;
          display: flex; flex-direction: column; align-items: center; gap: 20px;
          pointer-events: auto;
        }
        .instruction {
          color: #fff; font-size: 12px; font-weight: 700;
          letter-spacing: 2px; text-shadow: 0 2px 4px rgba(0,0,0,0.5);
        }
        .shutter-btn {
          width: 80px; height: 80px; border-radius: 50%;
          border: 4px solid #fff; background: transparent;
          padding: 4px; cursor: pointer;
        }
        .shutter-btn-inner {
          width: 100%; height: 100%; background: #fff; border-radius: 50%;
        }
        .retry-btn {
          margin-top: 20px; padding: 12px 24px;
          background: ${COLORS.primary}; color: #fff;
          border: none; border-radius: 12px; cursor: pointer;
        }
        .analysis-overlay {
          position: absolute; inset: 0; background: rgba(0,0,0,0.85);
          display: flex; flex-direction: column;
          align-items: center; justify-content: center;
          color: #fff; text-align: center;
          pointer-events: auto; z-index: 20; padding: 30px;
        }
        .analysis-overlay.error { border: 2px solid ${COLORS.orange}40; }
        .ai-brain-anim { margin-bottom: 24px; animation: spin 3s infinite linear; }
        @keyframes spin { from { rotate: 0deg; } to { rotate: 360deg; } }
        .analysis-overlay h2 { margin: 0 0 8px; font-weight: 800; }
        .analysis-overlay p  { opacity: 0.5; font-size: 14px; }
        .shutter-flash { position: fixed; inset: 0; background: #fff; z-index: 100; }
      `}</style>
    </div>
  )
}